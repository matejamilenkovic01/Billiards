using UnityEngine;

// The visual table around the play area, as seen by the cue and the aim camera (metres above the cloth).
public struct CueClearanceSettings
{
    public float BallRadius;
    public float HalfWidth;
    public float HalfLength;
    public float CushionDepth;
    public float CushionNoseHeight;
    public float CushionBackHeight;
    public float RailWidth;
    public float RailTop;
    public float CueLength;
    public float CueButtRadius;
    // How far the cue slides back on a full pull: the elevation is held while pulling, so it must already
    // clear what the butt reaches then.
    public float PullDistance;
    public float MinElevation;
    public float MaxElevation;
    public float Clearance;
}

// Gameplay redesign G6: how high a player lifts the butt of the cue so it clears the cushion, the rail and the
// balls behind the cue ball. Display only: the shot itself is unchanged (no masse). Table plane coordinates.
//
// The cue's axis goes through the cue ball centre, tilted up by the elevation towards the butt. Balls: the
// axis must stay at least R + cue radius + clearance from each ball centre (exact line distance). Cushion
// and rail: their height only grows outwards until the rail's outer edge, so the cue's underside only has to
// clear them where its footprint first reaches the cushion nose and where it first reaches the rail.
public static class CueClearance
{
    private const int Iterations = 3;

    // Cue lathe profile: t = 0 at the butt, 1 at the tip; radius as a fraction of the butt radius.
    public static float Radius01(float t) => Mathf.Lerp(1f, 0.4f, Mathf.Pow(Mathf.Clamp01(t), 0.85f));

    public static float RadiusFromTip(in CueClearanceSettings s, float fromTip) =>
        s.CueButtRadius * Radius01(1f - Mathf.Max(0f, fromTip) / s.CueLength);

    // How far a point is past the cushion noses (negative on the cloth).
    public static float FrameDistance(in CueClearanceSettings s, Vector2 point) =>
        Mathf.Max(Mathf.Abs(point.x) - s.HalfWidth, Mathf.Abs(point.y) - s.HalfLength);

    // Top of the cushion (sloping up from the nose) and then the rail at that distance; negative = nothing.
    public static float FrameHeight(in CueClearanceSettings s, float distance)
    {
        if (distance < 0f)
            return -1f;
        if (distance < s.CushionDepth)
            return Mathf.Lerp(s.CushionNoseHeight, s.CushionBackHeight, distance / s.CushionDepth);
        if (distance < s.CushionDepth + s.RailWidth)
            return s.RailTop;
        return -1f;
    }

    // First distance along `direction` from `origin` where a footprint of `reach` gets `distance` past the noses.
    public static float FrameCrossing(in CueClearanceSettings s, Vector2 origin, Vector2 direction, float distance, float reach)
    {
        if (FrameDistance(s, origin) + reach >= distance)
            return 0f;
        float best = float.MaxValue;
        if (Mathf.Abs(direction.x) > 1e-5f)
            best = Mathf.Min(best, (Mathf.Sign(direction.x) * (s.HalfWidth + distance - reach) - origin.x) / direction.x);
        if (Mathf.Abs(direction.y) > 1e-5f)
            best = Mathf.Min(best, (Mathf.Sign(direction.y) * (s.HalfLength + distance - reach) - origin.y) / direction.y);
        return Mathf.Max(0f, best);
    }

    // Smallest elevation (degrees, within Min..Max) at which the cue clears everything behind the cue ball.
    // tipOffset: distance from the cue ball centre back to the cue tip.
    public static float Elevation(in CueClearanceSettings s, BallState[] balls, Vector2 cueBall, Vector2 aim, float tipOffset)
    {
        Vector2 back = -aim.normalized;
        float minRadians = s.MinElevation * Mathf.Deg2Rad;
        float maxRadians = s.MaxElevation * Mathf.Deg2Rad;
        float elevation = minRadians;
        float reach = s.CueButtRadius;
        float noseX = FrameCrossing(s, cueBall, back, 0f, reach);
        float railX = FrameCrossing(s, cueBall, back, s.CushionDepth, reach);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            float cos = Mathf.Cos(elevation);
            float tipX = tipOffset * cos;
            float required = minRadians;
            required = Mathf.Max(required, FrameRequirement(s, cueBall, back, Mathf.Max(noseX, tipX), tipOffset, cos, reach));
            required = Mathf.Max(required, FrameRequirement(s, cueBall, back, Mathf.Max(railX, tipX), tipOffset, cos, reach));

            for (int i = 1; i < balls.Length; i++)
            {
                if (balls[i].Pocketed)
                    continue;
                Vector2 offset = balls[i].Pos - cueBall;
                float behind = Vector2.Dot(offset, back);
                if (behind <= 0f)
                    continue;
                float along = behind * cos - tipOffset;
                if (along > s.CueLength + s.PullDistance)
                    continue;
                float lateral = Mathf.Abs(offset.x * back.y - offset.y * back.x);
                float needed = s.BallRadius + RadiusFromTip(s, along) + s.Clearance;
                if (lateral >= needed)
                    continue;
                // Distance from the ball centre to the axis: sqrt(lateral^2 + (behind * sin)^2).
                float sin = Mathf.Sqrt(needed * needed - lateral * lateral) / behind;
                required = Mathf.Max(required, sin >= 1f ? Mathf.PI * 0.5f : Mathf.Asin(sin));
            }

            elevation = Mathf.Clamp(required, minRadians, maxRadians);
        }
        return elevation * Mathf.Rad2Deg;
    }

    // Elevation needed for the cue's underside to pass over the frame at horizontal distance x behind the ball.
    private static float FrameRequirement(in CueClearanceSettings s, Vector2 cueBall, Vector2 back, float x, float tipOffset, float cos, float reach)
    {
        if (x <= 1e-4f || x / cos - tipOffset > s.CueLength + s.PullDistance)
            return 0f;
        // Just past the crossing, so a zone start is judged by the zone it starts.
        float height = FrameHeight(s, FrameDistance(s, cueBall + back * x) + reach + 1e-4f);
        if (height < 0f)
            return 0f;
        float radius = RadiusFromTip(s, x / cos - tipOffset);
        float rise = height + s.Clearance - s.BallRadius + radius / cos;
        return Mathf.Atan2(rise, x);
    }

    // Lowest eye height (above the cloth) at `distance` behind the cue ball from which the line of sight to the
    // cue ball centre passes over the cushion and rail, and the eye itself is never inside or just over the frame.
    public static float MinEyeHeight(in CueClearanceSettings s, Vector2 cueBall, Vector2 aim, float distance, float sightMargin, float frameMargin)
    {
        Vector2 back = -aim.normalized;
        float minimum = 0f;
        float eyeFrame = FrameDistance(s, cueBall + back * distance);
        if (eyeFrame >= 0f && eyeFrame < s.CushionDepth + s.RailWidth + 0.05f)
            minimum = s.RailTop + frameMargin;

        float noseX = FrameCrossing(s, cueBall, back, 0f, 0f);
        float railX = FrameCrossing(s, cueBall, back, s.CushionDepth, 0f);
        minimum = Mathf.Max(minimum, SightHeight(s, s.CushionNoseHeight, noseX, distance, sightMargin));
        minimum = Mathf.Max(minimum, SightHeight(s, s.RailTop, railX, distance, sightMargin));
        return minimum;
    }

    private static float SightHeight(in CueClearanceSettings s, float obstacle, float x, float distance, float margin)
    {
        if (x <= 1e-4f || x >= distance)
            return 0f;
        return s.BallRadius + (obstacle + margin - s.BallRadius) * distance / x;
    }
}
