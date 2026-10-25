using UnityEngine;

// The aim guide's length rule (implementation_definition_v3 V1), on the same AimGuideLayout the view draws:
// the cue ball's line ends at the first obstacle, every band after it is the cue's arrow length, and after a
// cushion there is exactly one band.
public static class GuideChecks
{
    public static string LengthRule(int aims = 200)
    {
        var report = new CheckReport("GuideChecks.LengthRule");
        MatchConfig config = PhysicsChecks.Config;
        PoolSettings settings = config.ToPoolSettings();
        var table = new TableGeometry(settings);
        var predictor = new ShotPredictor(table, settings);
        var layout = new AimGuideLayout();
        var random = new System.Random(21);
        float radius = config.BallRadius;
        float[] arrowLengths = { 2f, 3f, 4f, 6f, 8f, 10f };   // ObjectGuideDiameters of the six cues (V11)

        int ballFirst = 0, cushionFirst = 0, pocketFirst = 0, lineMisses = 0, lengthMisses = 0, cushionBandMisses = 0, stoppedEarly = 0;
        float worstLine = 0f, worstLength = 0f;
        for (int aim = 0; aim < aims; aim++)
        {
            BallState[] balls = PhysicsChecks.Scatter(random, table);
            for (int i = 7; i < balls.Length; i++)
                balls[i].Pocketed = true;
            // Half the aims at a ball with a random cut, half anywhere (mostly cushions).
            Vector2 direction;
            if (aim % 2 == 0)
            {
                Vector2 target = balls[1 + random.Next(6)].Pos;
                Vector2 toTarget = (target - balls[0].Pos).normalized;
                float offset = ((float)random.NextDouble() * 2f - 1f) * 0.9f * 2f * radius;
                direction = (target + new Vector2(-toTarget.y, toTarget.x) * offset - balls[0].Pos).normalized;
            }
            else
            {
                float angle = (float)random.NextDouble() * 360f;
                direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            }
            float arrow = arrowLengths[aim % arrowLengths.Length] * 2f * radius;
            float speed = PhysicsChecks.SpeedFromPull(Mathf.Lerp(0.15f, 1f, (float)random.NextDouble()));
            Prediction prediction = predictor.Predict(balls, new ShotParams(direction, speed), null, arrow);
            layout.Build(prediction, balls, radius, arrow);

            // (a) the line ends at the first obstacle.
            float lineError = 0f;
            switch (prediction.Hit)
            {
                case PredictionHit.Ball:
                    ballFirst++;
                    lineError = Mathf.Abs((layout.LineEnd - balls[prediction.HitBall].Pos).magnitude - 2f * radius);
                    break;
                case PredictionHit.Cushion:
                    cushionFirst++;
                    lineError = Mathf.Abs(Mathf.Sqrt(NearestCushionSq(table, layout.LineEnd)) - radius);
                    break;
                case PredictionHit.Pocket:
                    pocketFirst++;
                    PocketInfo pocket = table.Pockets[NearestPocket(table, layout.LineEnd)];
                    lineError = Mathf.Abs((layout.LineEnd - pocket.Center).magnitude - pocket.CaptureRadius);
                    break;
            }
            worstLine = Mathf.Max(worstLine, lineError);
            if (lineError > 0.001f)
                lineMisses++;

            // (b) every band is the arrow length; the cue ball's may end sooner where it stops, drops or meets something.
            for (int b = 0; b < layout.BandCount; b++)
            {
                AimGuideLayout.Band band = layout.Bands[b];
                float error = Mathf.Abs(band.Length - arrow);
                if (band.Kind != GuideBandKind.Object && prediction.CuePathCut)
                {
                    stoppedEarly++;
                    error = Mathf.Max(0f, band.Length - arrow);
                }
                worstLength = Mathf.Max(worstLength, error);
                if (error > 0.001f)
                    lengthMisses++;
            }

            // (c) after a cushion: exactly one band, the cue ball's.
            if (prediction.Hit == PredictionHit.Cushion && (layout.BandCount != 1 || layout.Bands[0].Kind != GuideBandKind.CueAfterCushion))
                cushionBandMisses++;
        }

        report.Info($"{aims} aims: {ballFirst} ball first, {cushionFirst} cushion first, {pocketFirst} pocket first; {stoppedEarly} cue ball bands end early (stop, pocket, next ball or rail)");
        report.Info($"worst line end error {worstLine * 1000f:F3} mm, worst band length error {worstLength * 1000f:F3} mm");
        report.Check(ballFirst > 20 && cushionFirst > 20, "enough ball-first and cushion-first aims");
        report.Check(lineMisses == 0, $"(a) the line ends at the first obstacle within 1 mm ({lineMisses} misses)");
        report.Check(lengthMisses == 0, $"(b) every band after the obstacle is the arrow length within 1 mm ({lengthMisses} misses)");
        report.Check(cushionBandMisses == 0, $"(c) exactly one band after a cushion ({cushionBandMisses} misses)");
        return report.ToString();
    }

    private static float NearestCushionSq(TableGeometry table, Vector2 point)
    {
        float best = float.MaxValue;
        foreach (CushionSegment segment in table.Cushions)
            best = Mathf.Min(best, TableGeometry.DistanceSquaredToSegment(point, segment.A, segment.B, out _));
        return best;
    }

    private static int NearestPocket(TableGeometry table, Vector2 point)
    {
        int best = 0;
        for (int i = 1; i < table.Pockets.Length; i++)
        {
            if ((table.Pockets[i].Center - point).sqrMagnitude < (table.Pockets[best].Center - point).sqrMagnitude)
                best = i;
        }
        return best;
    }
}
