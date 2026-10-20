using UnityEngine;

// The aim guide must show what the simulation will do: compare ShotPredictor's first contact with PoolPhysics.
public static class PredictorChecks
{
    public static string MatchesSimulation(int aims = 200)
    {
        var report = new CheckReport("PredictorChecks.MatchesSimulation");
        MatchConfig config = PhysicsChecks.Config;
        var random = new System.Random(99);
        var speeds = new System.Random(7);
        int compared = 0, ballMismatch = 0, pointMismatch = 0, directionMismatch = 0;
        float worstPoint = 0f, worstAngle = 0f;
        int cushionFirst = 0, pocketFirst = 0;
        int pathsCompared = 0, pathMisses = 0;
        float worstPath = 0f;
        var trail = new System.Collections.Generic.List<Vector2>(512);

        for (int aim = 0; aim < aims; aim++)
        {
            PoolPhysics physics = PhysicsChecks.CreatePhysics();
            var predictor = new ShotPredictor(physics.Table, physics.Settings);
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

            // V4: the cue ball's path after the contact, at a random shot speed, over the first 0.3 m (or until it
            // touches something else).
            float speed = config.SpeedFromPull(Mathf.Lerp(0.15f, 1f, (float)speeds.NextDouble()));
            Prediction withPath = predictor.Predict(balls, direction, null, speed, 0.3f);
            if (!withPath.Reaches || withPath.CuePathCount < 2)
                continue;
            physics.SetBalls(balls);
            physics.BeginShot(new ShotParams(direction, speed));
            while (physics.IsMoving && physics.Result.FirstContactBall < 0)
                physics.Step();
            if (physics.Result.FirstContactBall != withPath.HitBall)
                continue;
            trail.Clear();
            trail.Add(physics.Result.FirstContactCuePos);   // the step that found the contact already moved on
            trail.Add(physics.Balls[0].Pos);
            int events = physics.Result.Collisions.Count;
            float arc = 0f;
            while (physics.IsMoving && arc < 0.3f && physics.Result.Collisions.Count == events && !physics.Balls[0].Pocketed)
            {
                physics.Step();
                arc += (physics.Balls[0].Pos - trail[trail.Count - 1]).magnitude;
                trail.Add(physics.Balls[0].Pos);
            }
            if (trail.Count < 3)
                continue;
            float pathError = PhysicsChecks.PathError(withPath, trail);
            pathsCompared++;
            worstPath = Mathf.Max(worstPath, pathError);
            if (pathError > 0.002f)
            {
                pathMisses++;
                if (pathMisses <= 4)
                    report.Info($"  miss {pathError * 1000f:F2} mm: speed {speed:F2}, cue->ghost {(withPath.Point - withPath.Start).magnitude:F2} m, cut {withPath.CutAngle:F0} deg, " +
                                $"at hit vel {withPath.CueVelAtHit.magnitude:F3} roll {withPath.CueRollAtHit.magnitude:F3} / sim {trail.Count} steps, path {withPath.CuePathCount} pts");
            }
        }

        report.Info($"{aims} aims: {compared} hit a ball first ({cushionFirst} cushion first, {pocketFirst} pocket first)");
        report.Info($"worst ghost error {worstPoint * 1000f:F3} mm, worst object direction error {worstAngle:F3} deg");
        report.Info($"cue ball path after contact: {pathsCompared} compared, worst {worstPath * 1000f:F3} mm");
        report.Check(compared >= aims / 3, "enough ball-first aims to compare");
        report.Check(pathsCompared >= compared / 2, "enough cue ball paths to compare");
        report.Check(pathMisses == 0, $"cue ball path after contact within 2 mm ({pathMisses} misses)");
        report.Check(ballMismatch == 0, $"same first ball ({ballMismatch} mismatches)");
        report.Check(pointMismatch == 0, $"ghost point within 1 mm ({pointMismatch} misses)");
        report.Check(directionMismatch == 0, $"object direction within 0.5 deg ({directionMismatch} misses)");
        return report.ToString();
    }
}
