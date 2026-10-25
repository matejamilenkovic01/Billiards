using System.Collections.Generic;
using UnityEngine;

// The aim guide must show what the simulation will do (v4 V1): the simulated ShotPredictor against a separate full
// PoolPhysics run of the same shot (spin, squirt, throw, masse included), and the analytic first contact (the bot's
// candidates, centre hits) against the simulation.
public static class PredictorChecks
{
    // The last missed shot, for debugging from `eval`.
    public static BallState[] MissBalls;
    public static ShotParams MissShot;
    public static float MissArrow;

    public static string MatchesSimulation(int aims = 500)
    {
        var report = new CheckReport("PredictorChecks.MatchesSimulation");
        MatchConfig config = PhysicsChecks.Config;
        PowerCurve curve = PhysicsChecks.Curve();
        float radius = config.BallRadius;
        float[] arrowLengths = { 2f, 3f, 4f, 6f, 8f, 10f };
        var random = new System.Random(99);
        PoolPhysics physics = PhysicsChecks.CreatePhysics();
        var predictor = new ShotPredictor(physics.Table, physics.Settings);
        var before = new List<Vector2>(4096);
        var after = new List<Vector2>(4096);

        int ballFirst = 0, cushionFirst = 0, pocketFirst = 0, stopped = 0;
        int kindMismatch = 0, ballMismatch = 0, pointMismatch = 0, directionMismatch = 0, lineMisses = 0, pathMisses = 0;
        float worstPoint = 0f, worstAngle = 0f, worstLine = 0f, worstPath = 0f;
        int analyticCompared = 0, analyticMismatch = 0;
        float worstAnalytic = 0f;

        for (int aim = 0; aim < aims; aim++)
        {
            BallState[] balls = PhysicsChecks.Scatter(random, physics.Table);
            for (int i = 7; i < balls.Length; i++)
                balls[i].Pocketed = true;

            // Mostly aimed at a ball with a random cut (thin cuts are the hard case); the rest anywhere.
            Vector2 direction;
            if (aim % 4 != 0)
            {
                Vector2 target = balls[1 + random.Next(6)].Pos;
                Vector2 toTarget = (target - balls[0].Pos).normalized;
                float offset = ((float)random.NextDouble() * 2f - 1f) * 0.95f * 2f * radius;
                direction = (target + new Vector2(-toTarget.y, toTarget.x) * offset - balls[0].Pos).normalized;
            }
            else
            {
                float angle = (float)random.NextDouble() * 360f;
                direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            }
            float speed = curve.SpeedFromPull(Mathf.Lerp(0.15f, 1f, (float)random.NextDouble()));
            // A quarter at the centre (also compared with the analytic contact); 30% of the rest with a raised cue.
            bool centre = aim % 4 == 1;
            Vector2 tip = centre ? Vector2.zero : PhysicsChecks.RandomTip(random, config.SpinMaxOffset);
            float elevation = !centre && random.NextDouble() < 0.3 ? Mathf.Lerp(5f, 40f, (float)random.NextDouble()) : config.CueElevation;
            var shot = new ShotParams(direction, speed, tip, elevation);
            float arrow = arrowLengths[aim % arrowLengths.Length] * 2f * radius;

            Prediction prediction = predictor.Predict(balls, shot, null, arrow);
            Run(physics, balls, shot, arrow, before, after, out PredictionHit hit, out int hitBall, out Vector2 point, out Vector2 objectDir);

            switch (hit)
            {
                case PredictionHit.Ball: ballFirst++; break;
                case PredictionHit.Cushion: cushionFirst++; break;
                case PredictionHit.Pocket: pocketFirst++; break;
                default: stopped++; break;
            }
            if (hit != prediction.Hit)
            {
                kindMismatch++;
                continue;
            }
            if (hit == PredictionHit.Ball)
            {
                if (hitBall != prediction.HitBall)
                {
                    ballMismatch++;
                    continue;
                }
                float pointError = (point - prediction.Point).magnitude;
                float angleError = Vector2.Angle(objectDir, prediction.ObjectDir);
                worstPoint = Mathf.Max(worstPoint, pointError);
                worstAngle = Mathf.Max(worstAngle, angleError);
                if (pointError > 0.001f) pointMismatch++;
                if (angleError > 0.5f) directionMismatch++;

                if (centre)
                {
                    // The analytic contact (bot candidates) is the same ball and ghost for a centre hit.
                    Prediction analytic = predictor.Predict(balls, direction, null);
                    analyticCompared++;
                    float error = analytic.Hit == PredictionHit.Ball && analytic.HitBall == hitBall ? (analytic.Point - point).magnitude : float.MaxValue;
                    worstAnalytic = Mathf.Max(worstAnalytic, error);
                    if (error > 0.001f)
                        analyticMismatch++;
                }
            }

            float lineError = PolylineError(prediction.Line, prediction.LineCount, before);
            worstLine = Mathf.Max(worstLine, lineError);
            if (lineError > 0.002f)
                lineMisses++;
            if (hit == PredictionHit.Ball || hit == PredictionHit.Cushion)
            {
                float pathError = PolylineError(prediction.CuePath, prediction.CuePathCount, after);
                if (pathError > worstPath)
                    worstPath = pathError;
                if (pathError > 0.002f)
                {
                    pathMisses++;
                    MissBalls = (BallState[])balls.Clone();
                    MissShot = shot;
                    MissArrow = arrow;
                    if (pathMisses <= 3)
                        report.Info($"  path miss {pathError * 1000f:F2} mm: {hit}, speed {speed:F2}, tip {tip}, elevation {elevation:F0}, {prediction.CuePathCount} pts, aim {aim}, worst so far {worstPath * 1000f:F2}");
                }
            }
        }

        // Cost of one guide prediction (Editor), and its garbage: a full rack (the break aim, the worst case: every ball
        // moves after the contact) and a typical table (7 balls left).
        BallState[] rack = RackBuilder.Build(new System.Random(4), physics.Table, config.RackGap);
        rack[0].Pos = new Vector2(0.1f, -0.7f);
        BallState[] typical = PhysicsChecks.Scatter(new System.Random(12), physics.Table);
        for (int i = 8; i < typical.Length; i++)
            typical[i].Pocketed = true;
        Time(predictor, rack, curve, config, out float rackAverage, out float rackWorst, out long rackAllocated);
        Time(predictor, typical, curve, config, out float typicalAverage, out float typicalWorst, out long typicalAllocated);

        report.Info($"{aims} shots (random direction, pull 15-100%, tip, 30% raised cue): {ballFirst} ball first, {cushionFirst} cushion, {pocketFirst} pocket, {stopped} stop short");
        report.Info($"worst ghost error {worstPoint * 1000f:F3} mm, object direction {worstAngle:F3} deg, line before the event {worstLine * 1000f:F3} mm, path after {worstPath * 1000f:F3} mm");
        report.Info($"analytic contact vs simulation (centre hits): {analyticCompared} compared, worst {worstAnalytic * 1000f:F3} mm");
        report.Info($"guide prediction, Legend-length arrow, 200 random aims/pulls/tips: typical table avg {typicalAverage:F3} ms (worst {typicalWorst:F3}), " +
                    $"full rack avg {rackAverage:F3} ms (worst {rackWorst:F3}); {typicalAllocated + rackAllocated} B allocated");
        report.Check(ballFirst >= aims / 3, "enough ball-first shots to compare");
        report.Check(kindMismatch == 0 && ballMismatch == 0, $"same first event and ball ({kindMismatch} + {ballMismatch} mismatches)");
        report.Check(pointMismatch == 0, $"ghost point within 1 mm ({pointMismatch} misses)");
        report.Check(directionMismatch == 0, $"object direction (throw included) within 0.5 deg ({directionMismatch} misses)");
        report.Check(lineMisses == 0, $"cue ball path before the event within 2 mm ({lineMisses} misses)");
        report.Check(pathMisses == 0, $"cue ball path after the event within 2 mm over the arrow length ({pathMisses} misses)");
        report.Check(analyticMismatch == 0, $"analytic contact = simulated for centre hits within 1 mm ({analyticMismatch} misses)");
        report.Check(typicalAllocated + rackAllocated == 0, "guide prediction allocates nothing");
        report.Check(typicalAverage <= 1.5f, "typical table: <= 1.5 ms per prediction on average (Editor)");
        return report.ToString();
    }

    private static void Time(ShotPredictor predictor, BallState[] balls, in PowerCurve curve, MatchConfig config, out float averageMs, out float worstMs, out long allocated)
    {
        float arrow = 10f * 2f * config.BallRadius;
        int target = 1;
        while (balls[target].Pocketed)
            target++;
        for (int i = 0; i < 20; i++)
            predictor.Predict(balls, new ShotParams(balls[target].Pos - balls[0].Pos, 1.5f), null, arrow);
        var random = new System.Random(8);
        var watch = new System.Diagnostics.Stopwatch();
        worstMs = 0f;
        const int timed = 200;
        allocated = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < timed; i++)
        {
            float angle = Mathf.Lerp(-25f, 25f, (float)random.NextDouble());
            var shot = new ShotParams(ShotAccuracy.Rotate(balls[target].Pos - balls[0].Pos, angle), curve.SpeedFromPull((float)random.NextDouble()),
                PhysicsChecks.RandomTip(random, config.SpinMaxOffset), config.CueElevation);
            long start = watch.ElapsedTicks;
            watch.Start();
            predictor.Predict(balls, shot, null, arrow);
            watch.Stop();
            worstMs = Mathf.Max(worstMs, (watch.ElapsedTicks - start) * 1000f / System.Diagnostics.Stopwatch.Frequency);
        }
        allocated = System.GC.GetAllocatedBytesForCurrentThread() - allocated;
        averageMs = (float)watch.Elapsed.TotalMilliseconds / timed;
    }

    // A separate full simulation of the shot: the cue ball's trail up to its first event and after it (the arrow
    // length, or until it stops, drops or meets the next thing), with the same events the predictor reports.
    public static void Run(PoolPhysics physics, BallState[] balls, in ShotParams shot, float arrow, List<Vector2> before, List<Vector2> after,
        out PredictionHit hit, out int hitBall, out Vector2 point, out Vector2 objectDir)
    {
        before.Clear();
        after.Clear();
        hit = PredictionHit.None;
        hitBall = -1;
        point = Vector2.zero;
        objectDir = Vector2.zero;
        physics.SetBalls(balls);
        physics.BeginShot(shot);
        ShotResult result = physics.Result;
        before.Add(physics.Balls[0].Pos);
        float arc = 0f;
        for (int step = 0; step < 15000 && physics.IsMoving; step++)
        {
            physics.Step();
            BallState cue = physics.Balls[0];
            if (hit == PredictionHit.None)
            {
                if (result.FirstContactBall >= 0)
                {
                    hit = PredictionHit.Ball;
                    hitBall = result.FirstContactBall;
                    point = result.FirstContactCuePos;
                    objectDir = result.FirstContactObjectDir;
                }
                else if (result.CueCushions > 0)
                {
                    hit = PredictionHit.Cushion;
                    point = cue.Pos;
                }
                else if (cue.Pocketed || !cue.IsMoving)
                {
                    if (cue.Pocketed)
                        hit = PredictionHit.Pocket;
                    before.Add(cue.Pos);
                    return;
                }
                else
                {
                    before.Add(cue.Pos);
                    continue;
                }
                before.Add(point);
                after.Add(point);
                if (hit == PredictionHit.Cushion)
                    continue;
            }

            bool next = (hit == PredictionHit.Cushion && (result.FirstContactBall >= 0 || result.CueCushions > 1))
                        || (hit == PredictionHit.Ball && result.CueBallContacts > 1) || cue.Pocketed;
            Vector2 at = hit == PredictionHit.Cushion && result.FirstContactBall >= 0 ? result.FirstContactCuePos : cue.Pos;
            arc += (at - after[after.Count - 1]).magnitude;
            after.Add(at);
            if (arc >= arrow || next || !cue.IsMoving)
                return;
        }
    }

    // Worst distance from the simulated trail (up to where the predicted polyline ends) to the predicted polyline,
    // and from each predicted point to the trail: the drawn path follows the real one both ways.
    public static float PolylineError(Vector2[] points, int count, List<Vector2> trail)
    {
        if (points == null || count < 2 || trail.Count < 2)
            return 0f;
        float predictedLength = 0f;
        for (int i = 1; i < count; i++)
            predictedLength += (points[i] - points[i - 1]).magnitude;

        float worst = 0f, arc = 0f;
        for (int t = 0; t < trail.Count; t++)
        {
            if (t > 0)
                arc += (trail[t] - trail[t - 1]).magnitude;
            if (arc > predictedLength)
                break;
            worst = Mathf.Max(worst, Mathf.Sqrt(DistanceSquaredToPolyline(trail[t], points, count)));
        }
        for (int p = 0; p < count; p++)
        {
            float best = float.MaxValue;
            for (int t = 1; t < trail.Count; t++)
                best = Mathf.Min(best, TableGeometry.DistanceSquaredToSegment(points[p], trail[t - 1], trail[t], out _));
            worst = Mathf.Max(worst, Mathf.Sqrt(best));
        }
        return worst;
    }

    private static float DistanceSquaredToPolyline(Vector2 point, Vector2[] points, int count)
    {
        float best = float.MaxValue;
        for (int i = 1; i < count; i++)
            best = Mathf.Min(best, TableGeometry.DistanceSquaredToSegment(point, points[i - 1], points[i], out _));
        return best;
    }
}
