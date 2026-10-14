using UnityEngine;

public enum PredictionHit
{
    None = 0,
    Ball = 1,
    Cushion = 2,
    Pocket = 3
}

public struct Prediction
{
    public PredictionHit Hit;
    public Vector2 Start;
    public Vector2 Point;          // cue ball centre at the first event (ghost ball for a ball hit)
    public int HitBall;
    public Vector2 ObjectDir;      // direction the hit ball leaves in
    public Vector2 CueDeflectDir;  // stun-shot tangent line for the cue ball (zero on a full hit)
    public bool IsLegal;

    // One cushion bounce, then the next event.
    public bool HasBounce;
    public Vector2 BounceDir;
    public PredictionHit SecondHit;
    public Vector2 SecondPoint;
    public int SecondBall;
    public Vector2 SecondObjectDir;
    public bool SecondIsLegal;

    // Cut angle in degrees between the aim and the object ball direction (0 = full ball).
    public float CutAngle => Hit == PredictionHit.Ball ? Vector2.Angle(Point - Start, ObjectDir) : 0f;
}

// Analytical first-contact prediction for the aim guide: swept circle along the aim, no full simulation,
// so it is cheap enough for every frame. Same geometry and radius as PoolPhysics.
public sealed class ShotPredictor
{
    private const float MaxDistance = 10f;

    private readonly TableGeometry _table;
    private readonly float _radius;
    private readonly float _cushionRestitution;
    private readonly float _cushionFriction;

    public ShotPredictor(TableGeometry table, float cushionRestitution, float cushionFriction)
    {
        _table = table;
        _radius = table.BallRadius;
        _cushionRestitution = cushionRestitution;
        _cushionFriction = cushionFriction;
    }

    public Prediction Predict(BallState[] balls, Vector2 direction, bool[] legalTargets)
    {
        direction = direction.normalized;
        Vector2 start = balls[0].Pos;
        var prediction = new Prediction { Start = start, HitBall = -1, SecondBall = -1 };

        Cast(balls, start, direction, out PredictionHit hit, out float distance, out int ball, out Vector2 cushionNormal);
        prediction.Hit = hit;
        prediction.Point = start + direction * distance;
        if (hit == PredictionHit.Ball)
        {
            prediction.HitBall = ball;
            prediction.ObjectDir = (balls[ball].Pos - prediction.Point).normalized;
            Vector2 tangent = direction - Vector2.Dot(direction, prediction.ObjectDir) * prediction.ObjectDir;
            prediction.CueDeflectDir = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector2.zero;
            prediction.IsLegal = legalTargets == null || legalTargets[ball];
            return prediction;
        }

        prediction.IsLegal = true;
        if (hit != PredictionHit.Cushion)
            return prediction;

        // Same bounce rule as the simulation: restitution on the normal, friction on the tangent.
        float normalSpeed = Vector2.Dot(direction, cushionNormal);
        Vector2 tangentPart = direction - normalSpeed * cushionNormal;
        Vector2 bounce = (-_cushionRestitution * normalSpeed * cushionNormal + (1f - _cushionFriction) * tangentPart).normalized;
        prediction.HasBounce = true;
        prediction.BounceDir = bounce;

        Vector2 from = prediction.Point + cushionNormal * 1e-4f;
        Cast(balls, from, bounce, out PredictionHit second, out float secondDistance, out int secondBall, out _);
        prediction.SecondHit = second;
        prediction.SecondPoint = from + bounce * secondDistance;
        if (second == PredictionHit.Ball)
        {
            prediction.SecondBall = secondBall;
            prediction.SecondObjectDir = (balls[secondBall].Pos - prediction.SecondPoint).normalized;
            prediction.SecondIsLegal = legalTargets == null || legalTargets[secondBall];
        }
        return prediction;
    }

    private void Cast(BallState[] balls, Vector2 origin, Vector2 direction, out PredictionHit hit, out float distance,
        out int hitBall, out Vector2 cushionNormal)
    {
        hit = PredictionHit.None;
        distance = MaxDistance;
        hitBall = -1;
        cushionNormal = Vector2.zero;

        float contact = 2f * _radius;
        for (int i = 1; i < balls.Length; i++)
        {
            if (balls[i].Pocketed)
                continue;
            if (RayCircle(origin, direction, balls[i].Pos, contact, out float t) && t < distance)
            {
                distance = t;
                hit = PredictionHit.Ball;
                hitBall = i;
            }
        }

        foreach (CushionSegment segment in _table.Cushions)
        {
            if (RayCapsule(origin, direction, segment.A, segment.B, _radius, out float t, out Vector2 normal) && t < distance)
            {
                distance = t;
                hit = PredictionHit.Cushion;
                hitBall = -1;
                cushionNormal = normal;
            }
        }

        foreach (PocketInfo pocket in _table.Pockets)
        {
            if (RayCircle(origin, direction, pocket.Center, pocket.CaptureRadius, out float t) && t < distance)
            {
                distance = t;
                hit = PredictionHit.Pocket;
                hitBall = -1;
            }
        }
    }

    // First t >= 0 where |origin + direction*t - center| = radius, entering the circle.
    private static bool RayCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius, out float t)
    {
        Vector2 f = origin - center;
        float b = Vector2.Dot(f, direction);
        float c = f.sqrMagnitude - radius * radius;
        t = 0f;
        if (c < 0f)
            return false;   // already inside (overlapping): ignore
        float discriminant = b * b - c;
        if (discriminant < 0f || b > 0f)
            return false;
        t = -b - Mathf.Sqrt(discriminant);
        return t >= 0f;
    }

    // Swept circle against a segment = ray against the segment's capsule.
    private static bool RayCapsule(Vector2 origin, Vector2 direction, Vector2 a, Vector2 b, float radius, out float t, out Vector2 normal)
    {
        t = float.MaxValue;
        normal = Vector2.zero;
        bool found = false;

        Vector2 ab = b - a;
        float length = ab.magnitude;
        if (length > 1e-6f)
        {
            Vector2 along = ab / length;
            var side = new Vector2(-along.y, along.x);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 n = side * s;
                float facing = Vector2.Dot(direction, n);
                if (facing >= 0f)
                    continue;   // moving away from this face
                float height = Vector2.Dot(origin - a, n) - radius;
                if (height < 0f)
                    continue;
                float hitT = height / -facing;
                Vector2 point = origin + direction * hitT;
                float projection = Vector2.Dot(point - a, along);
                if (projection >= 0f && projection <= length && hitT < t)
                {
                    t = hitT;
                    normal = n;
                    found = true;
                }
            }
        }

        found |= RayKnuckle(origin, direction, a, radius, ref t, ref normal);
        found |= RayKnuckle(origin, direction, b, radius, ref t, ref normal);
        return found;
    }

    private static bool RayKnuckle(Vector2 origin, Vector2 direction, Vector2 end, float radius, ref float t, ref Vector2 normal)
    {
        if (!RayCircle(origin, direction, end, radius, out float hitT) || hitT >= t)
            return false;

        t = hitT;
        normal = (origin + direction * hitT - end).normalized;
        return true;
    }
}
