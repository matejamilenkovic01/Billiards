using UnityEngine;

// Gameplay redesign G8: the player's starting aim must not be a ready-made pot.
public static class AimChecks
{
    // Random table states (a simulated break, then 0-5 random shots, random groups). For each, the default aim:
    // its predicted object ball line must not end in a pocket in >= 99% of states, and whenever a legal ball
    // can be reached directly it must hit a legal ball. A full simulation at medium power is reported too.
    public static string DefaultAimNotAPot(int states)
    {
        var report = new CheckReport("AimChecks.DefaultAimNotAPot");
        MatchConfig config = PhysicsChecks.Config;
        PoolSettings settings = config.ToPoolSettings();
        var geometry = new TableGeometry(settings);
        var physics = new PoolPhysics(geometry, settings) { RecordCollisions = false };
        var check = new PoolPhysics(geometry, settings) { RecordCollisions = false };
        var predictor = new ShotPredictor(geometry, settings.CushionRestitution, settings.CushionFriction);
        var legal = new bool[BallGroups.BallCount];

        int samples = 0, pots = 0, withClear = 0, clearHits = 0, straightIns = 0, simulatedPots = 0;
        for (int seed = 0; samples < states && seed < states * 4; seed++)
        {
            var random = new System.Random(5000 + seed);
            physics.SetBalls(RackBuilder.Build(random, geometry, config.RackGap));
            physics.Simulate(new ShotParams(geometry.FootSpot - physics.Balls[0].Pos, config.SpeedFromPull(0.9f + 0.1f * (float)random.NextDouble())));
            int extraShots = random.Next(0, 6);
            for (int shot = 0; shot <= extraShots; shot++)
            {
                BallState[] balls = physics.Balls;
                if (balls[0].Pocketed)
                {
                    var start = new Vector2(Mathf.Lerp(-geometry.MaxX, geometry.MaxX, (float)random.NextDouble()),
                        Mathf.Lerp(-geometry.MaxY, geometry.MaxY, (float)random.NextDouble()));
                    balls[0].Pos = RackBuilder.FindFreeSpot(balls, geometry, start, false);
                    balls[0].Pocketed = false;
                    physics.SetBalls(balls);
                }
                if (shot == extraShots)
                    break;
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                physics.Simulate(new ShotParams(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), config.SpeedFromPull(Mathf.Lerp(0.2f, 0.7f, (float)random.NextDouble()))));
            }

            BallState[] state = physics.Balls;
            RulesState rules = RulesState.NewRack(Side.Player);
            rules.IsBreakShot = false;
            int groupRoll = random.Next(0, 3);
            rules.Groups[0] = groupRoll == 0 ? BallGroup.Open : groupRoll == 1 ? BallGroup.Solids : BallGroup.Stripes;
            rules.Groups[1] = groupRoll == 0 ? BallGroup.Open : groupRoll == 1 ? BallGroup.Stripes : BallGroup.Solids;
            for (int n = 1; n < state.Length; n++)
                rules.Pocketed[n] = state[n].Pocketed;
            rules.FillLegalTargets(Side.Player, legal);
            bool anyLegal = false;
            for (int n = 1; n < state.Length; n++)
                anyLegal |= legal[n] && !state[n].Pocketed;
            if (!anyLegal || state[BallGroups.EightBall].Pocketed)
                continue;

            samples++;
            Vector2 aim = DefaultAim.Choose(predictor, state, legal, false, geometry.FootSpot, geometry.BallRadius, random);
            Prediction prediction = predictor.Predict(state, aim, legal);
            int nearestClear = DefaultAim.NearestLegal(predictor, state, legal, true);
            if (nearestClear >= 0)
            {
                withClear++;
                if (prediction.Hit == PredictionHit.Ball && prediction.IsLegal)
                    clearHits++;
                Vector2 fullBall = (state[nearestClear].Pos - state[0].Pos).normalized;
                if (DefaultAim.IsPot(predictor, state, fullBall, legal, nearestClear))
                    straightIns++;
            }
            if (prediction.Hit == PredictionHit.Ball && DefaultAim.IsPot(predictor, state, aim, legal, prediction.HitBall))
                pots++;

            if (prediction.Hit == PredictionHit.Ball)
            {
                check.SetBalls(state);
                ShotResult result = check.Simulate(new ShotParams(aim, config.SpeedFromPull(0.45f)));
                foreach (PocketEvent pocketed in result.Pocketed)
                {
                    if (pocketed.Number == prediction.HitBall)
                    {
                        simulatedPots++;
                        break;
                    }
                }
            }
        }

        report.Check(samples == states, $"{samples} of {states} usable states generated");
        report.Check(pots <= samples * 0.01f, $"default aim predicted to pot in {pots}/{samples} states (allowed 1%)");
        report.Check(clearHits == withClear, $"hits a legal ball in {clearHits}/{withClear} states where one can be reached directly");
        report.Info($"{samples} states: predicted pot {pots} ({100f * pots / Mathf.Max(1, samples):F1} %), " +
                    $"legal hit when reachable {clearHits}/{withClear}, full-ball line was straight-in (turned) {straightIns}");
        report.Info($"simulated at 45% power: target pocketed {simulatedPots}/{samples} ({100f * simulatedPots / Mathf.Max(1, samples):F1} %)");

        // Break: full ball on the apex ball.
        physics.SetBalls(RackBuilder.Build(new System.Random(3), geometry, config.RackGap));
        Vector2 breakAim = DefaultAim.Choose(predictor, physics.Balls, null, true, geometry.FootSpot, geometry.BallRadius, new System.Random(3));
        Prediction breakPrediction = predictor.Predict(physics.Balls, breakAim, null);
        report.Check(breakPrediction.Hit == PredictionHit.Ball && breakPrediction.CutAngle < 0.01f,
            $"break aim is a full hit on the apex ball (cut {breakPrediction.CutAngle:F3} deg)");
        return report.ToString();
    }
}
