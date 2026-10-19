using UnityEngine;

// Gameplay redesign G6: the cue never cuts through the table or the balls.
public static class CueChecks
{
    private const int EdgePoints = 48;

    // Random cue ball positions (60% within 0-8R of a cushion or a ball) and aims. For each, the cue's settled
    // pose (CueClearance elevation, no smoothing), at rest and fully pulled back: 48 points along its underside
    // (and along the two lower sides at 45 degrees) must be outside every cushion, rail and ball volume. The
    // volumes are built here from TableGeometry segments, independently of CueClearance's own frame model.
    public static string NoIntersection(int samples)
    {
        var report = new CheckReport("CueChecks.NoIntersection");
        MatchConfig config = PhysicsChecks.Config;
        CueClearanceSettings settings = config.CueClearanceSettings();
        var geometry = new TableGeometry(config.ToPoolSettings());
        float radius = config.BallRadius;
        var random = new System.Random(77);

        int intersections = 0, nearCases = 0, capped = 0;
        float maxElevation = 0f, sumElevation = 0f;
        string firstFailure = null;
        for (int n = 0; n < samples; n++)
        {
            BallState[] balls = PhysicsChecks.Scatter(random, geometry);
            for (int i = 1; i < balls.Length; i++)
                balls[i].Pocketed = random.NextDouble() < 0.35;

            bool near = random.NextDouble() < 0.6;
            Vector2 away = Vector2.zero;
            Vector2 cue = Vector2.zero;
            for (int attempt = 0; attempt < 500; attempt++)
            {
                if (near && random.NextDouble() < 0.5)
                {
                    // Gap 0-8R to a cushion nose.
                    CushionSegment rail = RandomRail(geometry, random);
                    Vector2 outward = Outward(rail);
                    Vector2 point = Vector2.Lerp(rail.A, rail.B, (float)random.NextDouble());
                    cue = point - outward * (radius + (float)random.NextDouble() * 8f * radius);
                    away = -outward;
                }
                else if (near)
                {
                    // Gap 0-8R to another ball.
                    int other = 1 + random.Next(15);
                    balls[other].Pocketed = false;
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    cue = balls[other].Pos + offset * (2f * radius + 0.0002f + (float)random.NextDouble() * 8f * radius);
                    away = offset;
                }
                else
                {
                    cue = new Vector2(Mathf.Lerp(-geometry.MaxX, geometry.MaxX, (float)random.NextDouble()),
                        Mathf.Lerp(-geometry.MaxY, geometry.MaxY, (float)random.NextDouble()));
                }
                if (geometry.IsInsidePlayArea(cue) && RackBuilder.IsFree(balls, cue, radius + 0.0001f, 0))
                    break;
            }
            balls[0].Pos = cue;
            balls[0].Pocketed = false;
            if (near)
                nearCases++;

            // Half of the near cases aim away from the obstacle (the cue then lies over it), the rest anywhere.
            float aimAngle = (float)random.NextDouble() * Mathf.PI * 2f;
            Vector2 aim = new Vector2(Mathf.Cos(aimAngle), Mathf.Sin(aimAngle));
            if (near && random.NextDouble() < 0.5)
                aim = ShotAccuracy.Rotate(away, ((float)random.NextDouble() * 2f - 1f) * 45f).normalized;

            float tipGap = config.BallRadius + config.CueTipGap;
            float elevation = CueClearance.Elevation(settings, balls, cue, aim, tipGap);
            maxElevation = Mathf.Max(maxElevation, elevation);
            sumElevation += elevation;
            if (elevation >= config.CueMaxElevation - 0.01f)
                capped++;

            foreach (float pull in new[] { 0f, 1f })
            {
                if (Intersects(config, settings, geometry, balls, cue, aim, elevation, tipGap + pull * config.CuePullDistance, out string what))
                {
                    intersections++;
                    firstFailure ??= $"cue ball {cue}, aim {aim}, elevation {elevation:F1} deg, pull {pull}: {what}";
                    break;
                }
            }
        }

        report.Check(intersections == 0, $"{intersections} of {samples} cue poses cut into the table or a ball" +
                                         (firstFailure != null ? $" (first: {firstFailure})" : ""));
        report.Info($"{samples} poses ({nearCases} within 8R of a cushion or ball): elevation avg {sumElevation / samples:F1} deg, " +
                    $"max {maxElevation:F1} deg, at the {config.CueMaxElevation:F0} deg cap {capped}");
        return report.ToString();
    }

    private static bool Intersects(MatchConfig config, in CueClearanceSettings settings, TableGeometry geometry, BallState[] balls,
        Vector2 cue, Vector2 aim, float elevation, float tipOffset, out string what)
    {
        Vector3 ballCenter = new(cue.x, config.BallRadius, cue.y);
        Quaternion rotation = CueView.PoseRotation(new Vector3(aim.x, 0f, aim.y), elevation);
        Vector3 forward = rotation * Vector3.forward;
        Vector3 tip = ballCenter - forward * tipOffset;
        Vector3[] edges =
        {
            rotation * Vector3.down,
            rotation * new Vector3(0.7071f, -0.7071f, 0f),
            rotation * new Vector3(-0.7071f, -0.7071f, 0f)
        };

        for (int i = 0; i < EdgePoints; i++)
        {
            float s = config.CueLength * i / (EdgePoints - 1);
            float r = CueClearance.RadiusFromTip(settings, s);
            Vector3 axis = tip - forward * s;
            foreach (Vector3 edge in edges)
            {
                Vector3 point = axis + edge * r;
                if (InsideTable(config, geometry, point, out what))
                    return true;
                for (int b = 1; b < balls.Length; b++)
                {
                    if (balls[b].Pocketed)
                        continue;
                    if ((point - new Vector3(balls[b].Pos.x, config.BallRadius, balls[b].Pos.y)).sqrMagnitude < config.BallRadius * config.BallRadius)
                    {
                        what = $"ball {b} at s = {s:F3} m";
                        return true;
                    }
                }
            }
        }
        what = null;
        return false;
    }

    // Cushion and jaw prisms (top sloping from the nose to the back, undercut ignored), the rail frame, the cloth.
    private static bool InsideTable(MatchConfig config, TableGeometry geometry, Vector3 point, out string what)
    {
        var flat = new Vector2(point.x, point.z);
        float y = point.y;
        what = null;
        if (y >= Mathf.Max(config.RailTop, config.CushionBackHeight, config.CushionNoseHeight))
            return false;

        float innerX = geometry.HalfWidth + config.CushionDepth;
        float innerZ = geometry.HalfLength + config.CushionDepth;
        bool inFrame = Mathf.Abs(flat.x) <= innerX + config.RailWidth && Mathf.Abs(flat.y) <= innerZ + config.RailWidth;
        if (inFrame && (Mathf.Abs(flat.x) > innerX || Mathf.Abs(flat.y) > innerZ) && y <= config.RailTop && y >= TablePrefabRailBottom)
        {
            what = $"rail at {point}";
            return true;
        }
        if (y < 0f && Mathf.Abs(flat.x) <= innerX && Mathf.Abs(flat.y) <= innerZ)
        {
            what = $"cloth at {point}";
            return true;
        }

        foreach (CushionSegment segment in geometry.Cushions)
        {
            Vector2 along = segment.B - segment.A;
            float length = along.magnitude;
            Vector2 direction = along / length;
            Vector2 outward;
            float depth;
            if (segment.IsJaw)
            {
                Vector2 mid = (segment.A + segment.B) * 0.5f;
                outward = new Vector2(-direction.y, direction.x);
                if (Vector2.Dot(NearestPocket(geometry, mid) - mid, outward) > 0f)
                    outward = -outward;
                depth = config.JawThickness;
            }
            else
            {
                outward = Outward(segment);
                depth = config.CushionDepth;
            }
            Vector2 local = flat - segment.A;
            float t = Vector2.Dot(local, direction);
            float o = Vector2.Dot(local, outward);
            if (t < 0f || t > length || o < 0f || o > depth || y < 0f)
                continue;
            float top = Mathf.Lerp(config.CushionNoseHeight, config.CushionBackHeight, o / depth);
            if (y <= top)
            {
                what = $"{(segment.IsJaw ? "jaw" : "cushion")} at {point}";
                return true;
            }
        }
        return false;
    }

    // Apron top in TablePrefabBuilder: the rail box goes down to here.
    private const float TablePrefabRailBottom = -0.1f;

    private static CushionSegment RandomRail(TableGeometry geometry, System.Random random)
    {
        while (true)
        {
            CushionSegment segment = geometry.Cushions[random.Next(geometry.Cushions.Length)];
            if (!segment.IsJaw)
                return segment;
        }
    }

    private static Vector2 Outward(CushionSegment segment)
    {
        Vector2 mid = (segment.A + segment.B) * 0.5f;
        return Mathf.Abs(segment.A.x - segment.B.x) < 1e-4f ? new Vector2(Mathf.Sign(mid.x), 0f) : new Vector2(0f, Mathf.Sign(mid.y));
    }

    private static Vector2 NearestPocket(TableGeometry geometry, Vector2 point)
    {
        Vector2 best = geometry.Pockets[0].Center;
        foreach (PocketInfo pocket in geometry.Pockets)
        {
            if ((pocket.Center - point).sqrMagnitude < (best - point).sqrMagnitude)
                best = pocket.Center;
        }
        return best;
    }
}
