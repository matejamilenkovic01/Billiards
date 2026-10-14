using UnityEngine;

// The aim guide must show what the simulation will do: compare ShotPredictor's first contact with PoolPhysics.
public static class PredictorChecks
{
    public static string MatchesSimulation(int aims = 200)
    {
        var report = new CheckReport("PredictorChecks.MatchesSimulation");
        MatchConfig config = PhysicsChecks.Config;
        var random = new System.Random(99);
        int compared = 0, ballMismatch = 0, pointMismatch = 0, directionMismatch = 0;
        float worstPoint = 0f, worstAngle = 0f;
        int cushionFirst = 0, pocketFirst = 0;

        for (int aim = 0; aim < aims; aim++)
        {
            PoolPhysics physics = PhysicsChecks.CreatePhysics();
            var predictor = new ShotPredictor(physics.Table, config.CushionRestitution, config.CushionFriction);
            BallState[] balls = PhysicsChecks.Scatter(random, physics.Table);
            // Fewer balls than a full rack keeps long, interesting sight lines.
            for (int i = 7; i < balls.Length; i++)
                balls[i].Pocketed = true;
            physics.SetBalls(balls);

            // Mostly aimed at a ball with a random cut (thin cuts are the hard case); the rest fully random.
            Vector2 direction;
            if (aim % 4 != 0)
            {
                int target = 1 + random.Next(6);
                Vector2 toTarget = (physics.Balls[target].Pos - physics.Balls[0].Pos).normalized;
                float offset = ((float)random.NextDouble() * 2f - 1f) * 0.95f * 2f * config.BallRadius;
                Vector2 aimPoint = physics.Balls[target].Pos + new Vector2(-toTarget.y, toTarget.x) * offset;
                direction = (aimPoint - physics.Balls[0].Pos).normalized;
            }
            else
            {
                float angle = (float)random.NextDouble() * 360f;
                direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            }
            Prediction prediction = predictor.Predict(physics.Balls, direction, null);
            if (prediction.Hit == PredictionHit.Cushion)
                cushionFirst++;
            if (prediction.Hit == PredictionHit.Pocket)
                pocketFirst++;
            if (prediction.Hit != PredictionHit.Ball)
                continue;

            ShotResult result = physics.Simulate(new ShotParams(direction, config.MaxSpeed));
            compared++;
            if (result.FirstContactBall != prediction.HitBall)
            {
                ballMismatch++;
                continue;
            }

            float pointError = (result.FirstContactCuePos - prediction.Point).magnitude;
            float angleError = Vector2.Angle(result.FirstContactObjectDir, prediction.ObjectDir);
            worstPoint = Mathf.Max(worstPoint, pointError);
            worstAngle = Mathf.Max(worstAngle, angleError);
            if (pointError > 0.001f)
                pointMismatch++;
            if (angleError > 0.5f)
                directionMismatch++;
        }

        report.Info($"{aims} aims: {compared} hit a ball first ({cushionFirst} cushion first, {pocketFirst} pocket first)");
        report.Info($"worst ghost error {worstPoint * 1000f:F3} mm, worst object direction error {worstAngle:F3} deg");
        report.Check(compared >= aims / 3, "enough ball-first aims to compare");
        report.Check(ballMismatch == 0, $"same first ball ({ballMismatch} mismatches)");
        report.Check(pointMismatch == 0, $"ghost point within 1 mm ({pointMismatch} misses)");
        report.Check(directionMismatch == 0, $"object direction within 0.5 deg ({directionMismatch} misses)");
        return report.ToString();
    }
}
