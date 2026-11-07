using UnityEngine;

// v5 V4: how a pocket looks; the physics (TableGeometry: capture circle, jaws) is unchanged. The visible hole is a
// circle through the two points where the jaw lines meet the rails' inner edges (MatchConfig.PocketHoleRadiusCorner /
// Side), so it sits further out than the capture circle, partly under the rail's cut, and opens between the jaws as on
// a real table (WPA: 142 / 104 degree jaws, the hole behind a shelf). One place for the table builder, the ball's drop,
// the rim glow, the call rings, the bird's-eye markers and the camera's framing.
public static class PocketLook
{
    public struct Hole
    {
        public Vector2 Center;
        public float Radius;
        // Outward, away from the table (PocketInfo.Inward).
        public Vector2 Axis;
        // Where each jaw line meets its rail's inner edge (on the circle).
        public Vector2 JawA, JawB;
        // Half the angle of the side that opens to the cloth, round -Axis (radians).
        public float MouthHalf;
        // The hole's back, on the axis.
        public Vector2 Back => Center + Axis * Radius;
    }

    // The camera asks for the rims every frame: the holes of the last table are kept (same geometry and config).
    private static TableGeometry _cachedGeometry;
    private static MatchConfig _cachedConfig;
    private static Hole[] _cached;

    public static Hole Of(TableGeometry geometry, MatchConfig config, int pocket)
    {
        if (geometry != _cachedGeometry || config != _cachedConfig || _cached == null)
        {
            _cached = new Hole[geometry.Pockets.Length];
            for (int i = 0; i < _cached.Length; i++)
                _cached[i] = Compute(geometry, config, i);
            _cachedGeometry = geometry;
            _cachedConfig = config;
        }
        return _cached[pocket];
    }

    private static Hole Compute(TableGeometry geometry, MatchConfig config, int pocket)
    {
        PocketInfo info = geometry.Pockets[pocket];
        Vector2 axis = info.Inward.normalized;
        float innerX = geometry.HalfWidth + config.CushionDepth;
        float innerY = geometry.HalfLength + config.CushionDepth;
        var points = new Vector2[2];
        int found = 0;
        foreach (CushionSegment segment in geometry.Cushions)
        {
            if (!segment.IsJaw || found == 2 || NearestPocket(geometry, (segment.A + segment.B) * 0.5f) != pocket)
                continue;
            // The nose is the end further from the pocket; the jaw line runs from it past the other end.
            bool aIsNose = Vector2.Dot(segment.A - info.Center, axis) < Vector2.Dot(segment.B - info.Center, axis);
            Vector2 nose = aIsNose ? segment.A : segment.B;
            Vector2 direction = ((aIsNose ? segment.B : segment.A) - nose).normalized;
            bool onLongRail = Mathf.Abs(Mathf.Abs(nose.x) - geometry.HalfWidth) < 1e-3f;
            float t = onLongRail
                ? (Mathf.Sign(nose.x) * innerX - nose.x) / direction.x
                : (Mathf.Sign(nose.y) * innerY - nose.y) / direction.y;
            points[found++] = nose + direction * t;
        }

        Vector2 middle = (points[0] + points[1]) * 0.5f;
        float half = (points[1] - points[0]).magnitude * 0.5f;
        float radius = Mathf.Max(info.IsCorner ? config.PocketHoleRadiusCorner : config.PocketHoleRadiusSide, half + 0.002f);
        Vector2 center = middle + axis * Mathf.Sqrt(radius * radius - half * half);
        return new Hole
        {
            Center = center,
            Radius = radius,
            Axis = axis,
            JawA = points[0],
            JawB = points[1],
            MouthHalf = Vector2.Angle(points[0] - center, -axis) * Mathf.Deg2Rad
        };
    }

    private static int NearestPocket(TableGeometry geometry, Vector2 point)
    {
        int best = 0;
        for (int i = 1; i < geometry.Pockets.Length; i++)
        {
            if ((geometry.Pockets[i].Center - point).sqrMagnitude < (geometry.Pockets[best].Center - point).sqrMagnitude)
                best = i;
        }
        return best;
    }
}
