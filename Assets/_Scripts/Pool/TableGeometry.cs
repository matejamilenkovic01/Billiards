using UnityEngine;

public struct CushionSegment
{
    public Vector2 A;
    public Vector2 B;
    public bool IsJaw;
}

public struct PocketInfo
{
    public Vector2 Center;
    public float CaptureRadius;
    public bool IsCorner;
    // Unit vector from the table into the pocket (for the drop animation).
    public Vector2 Inward;
}

// Table plane in metres: x in [-HalfWidth, HalfWidth], y (world z) in [-HalfLength, HalfLength].
// The head end (kitchen, break) is at -y. Cushion segments are the cushion nose lines; a ball touches one
// when its centre is within BallRadius of the segment, so segment ends act as rounded knuckles.
// Pockets: 0 head-left, 1 head-right, 2 side-left, 3 side-right, 4 foot-left, 5 foot-right.
public sealed class TableGeometry
{
    public readonly float HalfWidth;
    public readonly float HalfLength;
    public readonly float BallRadius;
    public readonly float HeadStringZ;
    public readonly float FootSpotZ;
    public readonly CushionSegment[] Cushions;
    public readonly PocketInfo[] Pockets;

    public Vector2 FootSpot => new(0f, FootSpotZ);

    public TableGeometry(in PoolSettings settings)
    {
        HalfWidth = settings.HalfWidth;
        HalfLength = settings.HalfLength;
        BallRadius = settings.BallRadius;
        HeadStringZ = settings.HeadStringZ;
        FootSpotZ = settings.FootSpotZ;

        float w = HalfWidth;
        float l = HalfLength;
        // Corner mouth is measured point to point across the opening, so each rail stops mouth/sqrt(2) short.
        float corner = settings.CornerMouth / Mathf.Sqrt(2f);
        float side = settings.SideMouth * 0.5f;
        float jaw = settings.JawDepth;
        // Jaws lean towards the pocket axis a little, so the throat is narrower than the mouth.
        const float cornerConverge = 1.25f;
        const float sideConverge = 0.25f;

        var cushions = new System.Collections.Generic.List<CushionSegment>(18);
        foreach (float sx in new[] { -1f, 1f })
        {
            // Long rails (x = +-w), split by the side pocket.
            cushions.Add(Rail(new Vector2(sx * w, -l + corner), new Vector2(sx * w, -side)));
            cushions.Add(Rail(new Vector2(sx * w, side), new Vector2(sx * w, l - corner)));
        }
        foreach (float sy in new[] { -1f, 1f })
            cushions.Add(Rail(new Vector2(-w + corner, sy * l), new Vector2(w - corner, sy * l)));

        foreach (float sx in new[] { -1f, 1f })
        {
            foreach (float sy in new[] { -1f, 1f })
            {
                // Corner jaws: one from the long rail end, one from the short rail end.
                var longEnd = new Vector2(sx * w, sy * (l - corner));
                cushions.Add(Jaw(longEnd, new Vector2(sx, sy * cornerConverge), jaw));
                var shortEnd = new Vector2(sx * (w - corner), sy * l);
                cushions.Add(Jaw(shortEnd, new Vector2(sx * cornerConverge, sy), jaw));
            }

            // Side jaws, leaning towards the pocket centre line.
            cushions.Add(Jaw(new Vector2(sx * w, side), new Vector2(sx, -sideConverge), jaw));
            cushions.Add(Jaw(new Vector2(sx * w, -side), new Vector2(sx, sideConverge), jaw));
        }
        Cushions = cushions.ToArray();

        float cornerOffset = settings.CornerPocketInset / Mathf.Sqrt(2f);
        Pockets = new[]
        {
            CornerPocket(-1f, -1f, cornerOffset, settings.CornerPocketRadius),
            CornerPocket(1f, -1f, cornerOffset, settings.CornerPocketRadius),
            SidePocket(-1f, settings.SidePocketInset, settings.SidePocketRadius),
            SidePocket(1f, settings.SidePocketInset, settings.SidePocketRadius),
            CornerPocket(-1f, 1f, cornerOffset, settings.CornerPocketRadius),
            CornerPocket(1f, 1f, cornerOffset, settings.CornerPocketRadius)
        };
    }

    private PocketInfo CornerPocket(float sx, float sy, float offset, float radius) => new()
    {
        Center = new Vector2(sx * (HalfWidth + offset), sy * (HalfLength + offset)),
        CaptureRadius = radius,
        IsCorner = true,
        Inward = new Vector2(sx, sy).normalized
    };

    private PocketInfo SidePocket(float sx, float inset, float radius) => new()
    {
        Center = new Vector2(sx * (HalfWidth + inset), 0f),
        CaptureRadius = radius,
        IsCorner = false,
        Inward = new Vector2(sx, 0f)
    };

    private static CushionSegment Rail(Vector2 a, Vector2 b) => new() { A = a, B = b, IsJaw = false };

    private static CushionSegment Jaw(Vector2 start, Vector2 direction, float length) =>
        new() { A = start, B = start + direction.normalized * length, IsJaw = true };

    // Ball centre limits: inside the cushion noses by one radius.
    public float MaxX => HalfWidth - BallRadius;
    public float MaxY => HalfLength - BallRadius;

    public bool IsInKitchen(Vector2 position) => position.y <= HeadStringZ;

    public Vector2 ClampToPlayArea(Vector2 position, bool kitchenOnly)
    {
        float maxY = kitchenOnly ? HeadStringZ : MaxY;
        return new Vector2(Mathf.Clamp(position.x, -MaxX, MaxX), Mathf.Clamp(position.y, -MaxY, maxY));
    }

    public bool IsInsidePlayArea(Vector2 position, float tolerance = 0f) =>
        Mathf.Abs(position.x) <= MaxX + tolerance && Mathf.Abs(position.y) <= MaxY + tolerance;

    // Squared distance from p to segment ab, and the closest point.
    public static float DistanceSquaredToSegment(Vector2 p, Vector2 a, Vector2 b, out Vector2 closest)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        closest = a + ab * t;
        return (p - closest).sqrMagnitude;
    }
}
