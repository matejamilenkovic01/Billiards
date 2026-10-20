using UnityEditor;
using UnityEngine;

// Headless checks of PoolPhysics against the tuning targets (implementation_definition 4.6.1 / 8).
public static class PhysicsChecks
{
    public static MatchConfig Config => AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);

    public static PoolPhysics CreatePhysics()
    {
        PoolSettings settings = Config.ToPoolSettings();
        return new PoolPhysics(new TableGeometry(settings), settings);
    }

    // A table so big that no cushion or pocket gets in the way: single-ball motion measured on its own.
    public static PoolPhysics CreateOpenPhysics()
    {
        PoolSettings settings = Config.ToPoolSettings();
        settings.HalfWidth = settings.HalfLength = 100f;
        return new PoolPhysics(new TableGeometry(settings), settings);
    }

    public static string All() =>
        Determinism() + "\n" + Timings() + "\n" + Collisions() + "\n" + SlideRoll() + "\n" + StraightPot() + "\n" + NoTunneling(1000);

    public static string Determinism()
    {
        var report = new CheckReport("PhysicsChecks.Determinism");
        Vector2[] first = Break(7, 1f).FinalPositions.Clone() as Vector2[];
        Vector2[] second = Break(7, 1f).FinalPositions;
        bool identical = true;
        for (int i = 0; i < first.Length; i++)
            identical &= first[i] == second[i];
        report.Check(identical, "same break twice gives bit-identical final positions");
        return report.ToString();
    }

    public static string Timings()
    {
        var report = new CheckReport("PhysicsChecks.Timings");
        MatchConfig config = Config;

        float min = float.MaxValue, max = 0f, sum = 0f;
        int pocketedTotal = 0;
        const int breaks = 10;
        for (int seed = 0; seed < breaks; seed++)
        {
            ShotResult result = Break(seed, 1f);
            min = Mathf.Min(min, result.Duration);
            max = Mathf.Max(max, result.Duration);
            sum += result.Duration;
            pocketedTotal += result.Pocketed.Count;
        }
        report.Info($"break 100%: settle {min:F2}-{max:F2}s (avg {sum / breaks:F2}s), avg {pocketedTotal / (float)breaks:F1} balls pocketed");
        report.Check(min >= 5f && max <= 9f, "break settles in 5-9 s");

        // 30% shot on an empty table, straight up the table: path length of the cue ball.
        PoolPhysics physics = CreatePhysics();
        var balls = new BallState[BallGroups.BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].Number = i;
            balls[i].Pocketed = i != 0;
        }
        balls[0].Pos = new Vector2(0.1f, -0.8f);
        physics.SetBalls(balls);
        float speed = config.SpeedFromPull(0.3f);
        physics.BeginShot(new ShotParams(new Vector2(0.02f, 1f), speed));
        float path = 0f;
        Vector2 last = physics.Balls[0].Pos;
        while (physics.IsMoving && physics.Time < 60f)
        {
            physics.Step();
            path += (physics.Balls[0].Pos - last).magnitude;
            last = physics.Balls[0].Pos;
        }
        float lengths = path / config.PlayLength;
        report.Info($"30% shot: speed {speed:F2} m/s, cue ball travels {path:F2} m = {lengths:F2} table lengths in {physics.Time:F1}s");
        report.Check(lengths >= 1f && lengths <= 1.6f, "30% shot travels 1-1.6 table lengths");
        return report.ToString();
    }

    public static string Collisions()
    {
        var report = new CheckReport("PhysicsChecks.Collisions");
        float radius = Config.BallRadius;

        // (a) full ball hit: cue nearly stops, object takes ~95% of the speed.
        PoolPhysics physics = TwoBalls(new Vector2(0f, -0.3f), new Vector2(0f, 0f));
        physics.BeginShot(new ShotParams(Vector2.up, 2f));
        float cueBefore = RunToFirstContact(physics);
        float cueAfter = physics.Balls[0].Vel.magnitude;
        float objectAfter = physics.Balls[1].Vel.magnitude;
        report.Info($"(a) full hit: cue {cueBefore:F3} -> {cueAfter:F3} m/s, object {objectAfter:F3} m/s ({objectAfter / cueBefore:P0})");
        report.Check(cueAfter < 0.05f * cueBefore, "full hit: cue ball nearly stops");
        report.Check(Mathf.Abs(objectAfter / cueBefore - 0.975f) < 0.03f, "full hit: object keeps ~95-98% of the speed");

        // (b) 30 degree cut: ghost-ball aim, the two balls separate at ~90 degrees.
        var objectDirection = new Vector2(Mathf.Sin(30f * Mathf.Deg2Rad), Mathf.Cos(30f * Mathf.Deg2Rad));
        Vector2 ghost = Vector2.zero - objectDirection * 2f * radius;
        physics = TwoBalls(ghost - Vector2.up * 0.3f, Vector2.zero);
        physics.BeginShot(new ShotParams(Vector2.up, 2f));
        RunToFirstContact(physics);
        float objectAngle = Vector2.Angle(Vector2.up, physics.Balls[1].Vel);
        float separation = Vector2.Angle(physics.Balls[0].Vel, physics.Balls[1].Vel);
        report.Info($"(b) 30 deg cut: object leaves at {objectAngle:F2} deg, separation {separation:F1} deg");
        report.Check(Mathf.Abs(objectAngle - 30f) < 0.5f, "cut: object ball leaves along the line of centres");
        report.Check(Mathf.Abs(separation - 90f) < 5f, "cut: balls separate at ~90 deg");

        // (c) 45 degree into the right long rail, away from pockets.
        physics = TwoBalls(new Vector2(0.3f, 0.25f), new Vector2(-0.4f, -0.9f));
        var incoming = new Vector2(1f, 1f).normalized;
        physics.BeginShot(new ShotParams(incoming, 1.5f));
        float before = 0f;
        while (physics.IsMoving && physics.Balls[0].Vel.x > 0f)
        {
            before = physics.Balls[0].Vel.magnitude;
            physics.Step();
        }
        Vector2 outgoing = physics.Balls[0].Vel;
        float outAngle = Vector2.Angle(outgoing, Vector2.left);
        report.Info($"(c) cushion 45 deg in: out at {outAngle:F1} deg from the normal, speed {before:F2} -> {outgoing.magnitude:F2}");
        report.Check(Mathf.Abs(outAngle - 45f) < 6f, "cushion: rebound angle ~45 deg");
        report.Check(outgoing.magnitude < before * 0.95f, "cushion: rebound is slower");

        // (d) jaws: dead centre into the side pocket drops; clipping the jaw knuckle bounces out.
        physics = TwoBalls(new Vector2(0f, 0f), new Vector2(-0.4f, -0.9f));
        physics.Simulate(new ShotParams(Vector2.right, 1.5f));
        report.Check(physics.Balls[0].Pocketed && physics.Balls[0].PocketIndex == 3, "centre of the side pocket: pocketed");

        float knuckleY = Config.SideMouth * 0.5f + radius * 0.4f;
        physics = TwoBalls(new Vector2(0f, knuckleY), new Vector2(-0.4f, -0.9f));
        physics.Simulate(new ShotParams(Vector2.right, 1.5f));
        report.Check(!physics.Balls[0].Pocketed, $"jaw knuckle hit at y={knuckleY:F3}: bounces out");

        physics = TwoBalls(new Vector2(0f, 0.56f), new Vector2(-0.4f, -0.9f));
        Vector2 toCorner = physics.Table.Pockets[5].Center - physics.Balls[0].Pos;
        physics.Simulate(new ShotParams(toCorner, 1.5f));
        report.Check(physics.Balls[0].Pocketed && physics.Balls[0].PocketIndex == 5, "straight into the corner: pocketed");
        return report.ToString();
    }

    // Sliding -> rolling (implementation_definition_v3 V4): stun up close, follow from afar, the curved cue ball
    // path after a cut, and how far a ball slides off the cue.
    public static string SlideRoll()
    {
        var report = new CheckReport("PhysicsChecks.SlideRoll");
        MatchConfig config = Config;
        float radius = config.BallRadius;
        float eps = config.RollSlipEpsilon;

        // Full hit up close: the cue ball still slides at contact and stops dead (stun).
        PoolPhysics physics = OpenTwoBalls(new Vector2(0f, -0.15f), Vector2.zero);
        physics.BeginShot(new ShotParams(Vector2.up, 2f));
        float contact = RunToFirstContact(physics);
        float cueAfter = physics.Balls[0].Vel.magnitude;
        float objectStart = physics.Balls[1].Vel.magnitude;
        Vector2 cueAt = physics.Balls[0].Pos, objectAt = physics.Balls[1].Pos;
        float rollingSpeed = 0f, rollingAfter = 0f;
        while (physics.IsMoving)
        {
            physics.Step();
            if (rollingSpeed == 0f && (physics.Balls[1].Vel - physics.Balls[1].Roll).magnitude <= eps)
            {
                rollingSpeed = physics.Balls[1].Vel.magnitude;
                rollingAfter = (physics.Balls[1].Pos - objectAt).magnitude;
            }
        }
        float cueTravel = (physics.Balls[0].Pos - cueAt).magnitude;
        report.Info($"close full hit (0.15 m, 2 m/s): cue {contact:F3} m/s at contact -> {cueAfter:F3} m/s, travels {cueTravel * 100f:F1} cm; " +
                    $"object {objectStart / contact:P1} of the cue ball's contact speed, rolls after {rollingAfter:F2} m at {rollingSpeed / contact:P1}");
        report.Check(cueAfter < 0.1f && cueTravel < 0.05f, "close full hit: cue ball stops (< 0.1 m/s, < 5 cm)");
        report.Check(objectStart / contact >= 0.93f && objectStart / contact <= 0.98f, "close full hit: object leaves with 93-98% of the cue ball's speed");
        report.Check(rollingSpeed / contact >= 0.66f && rollingSpeed / contact <= 0.73f, "close full hit: object rolls on at 66-73% (5/7)");

        // Full hit from afar: the cue ball arrives rolling and follows through.
        physics = OpenTwoBalls(new Vector2(0f, -1.2f), Vector2.zero);
        physics.BeginShot(new ShotParams(Vector2.up, 2f));
        RunToFirstContact(physics);
        bool arrivedRolling = (physics.Balls[0].Roll - physics.Balls[0].Vel).magnitude > 0.5f * physics.Balls[0].Roll.magnitude
                              && physics.Balls[0].Roll.magnitude > 0.3f;
        cueAt = physics.Balls[0].Pos;
        objectAt = physics.Balls[1].Pos;
        while (physics.IsMoving)
            physics.Step();
        float follow = (physics.Balls[0].Pos - cueAt).magnitude / Mathf.Max(1e-4f, (physics.Balls[1].Pos - objectAt).magnitude);
        report.Info($"far full hit (1.2 m, 2 m/s): cue ball follows {(physics.Balls[0].Pos - cueAt).magnitude:F2} m = {follow:P0} of the object ball's {(physics.Balls[1].Pos - objectAt).magnitude:F2} m");
        report.Check(arrivedRolling, "far full hit: cue ball arrives rolling (keeps its roll after the contact)");
        report.Check(follow >= 0.15f && follow <= 0.40f && physics.Balls[0].Pos.y > cueAt.y, "far full hit: follows forward 15-40% of the object ball's path");

        // 30 degree cut from afar: the cue ball bends forward off the tangent line, along the predicted path.
        var objectDirection = new Vector2(Mathf.Sin(30f * Mathf.Deg2Rad), Mathf.Cos(30f * Mathf.Deg2Rad));
        Vector2 ghost = -objectDirection * 2f * radius;
        physics = OpenTwoBalls(ghost - Vector2.up * 1.2f, Vector2.zero);
        var predictor = new ShotPredictor(physics.Table, physics.Settings);
        Prediction prediction = predictor.Predict(physics.Balls, Vector2.up, null, 2f, 0.3f);
        physics.BeginShot(new ShotParams(Vector2.up, 2f));
        RunToFirstContact(physics);
        Vector2 tangent = new Vector2(-objectDirection.y, objectDirection.x);
        if (Vector2.Dot(tangent, Vector2.up) < 0f)
            tangent = -tangent;
        var trail = new System.Collections.Generic.List<Vector2> { physics.Result.FirstContactCuePos, physics.Balls[0].Pos };
        float arc = 0f;
        while (physics.IsMoving && arc < 0.35f)
        {
            physics.Step();
            arc += (physics.Balls[0].Pos - trail[trail.Count - 1]).magnitude;
            trail.Add(physics.Balls[0].Pos);
        }
        float worst = PathError(prediction, trail);
        Vector2 heading = (trail[trail.Count - 1] - trail[trail.Count - 2]).normalized;
        // Forward = turned from the tangent towards the original shot line.
        float bend = Vector2.Angle(tangent, Vector2.up) - Vector2.Angle(heading, Vector2.up);
        report.Info($"30 deg cut (1.2 m, 2 m/s): after 0.3 m the cue ball heads {bend:F1} deg off the tangent towards the shot line; " +
                    $"predicted path ({prediction.CuePathCount} points) within {worst * 1000f:F2} mm");
        report.Check(bend > 2f, "cut: cue ball path bends forward from the tangent");
        report.Check(prediction.CuePathCount >= 3 && worst <= 0.002f, "cut: predicted cue ball path within 2 mm over 0.3 m");

        // Off the cue: slides first, then rolls, without a jump in speed.
        var line = new System.Text.StringBuilder("off the cue, slide before rolling:");
        bool smooth = true;
        float slideSoft = 0f, slideMid = 0f;
        foreach (float pull in new[] { 0.1f, 0.2f, 0.3f, 0.5f, 1f })
        {
            physics = OpenTwoBalls(Vector2.zero, new Vector2(50f, 50f));
            physics.BeginShot(new ShotParams(Vector2.up, config.SpeedFromPull(pull)));
            float slide = -1f, lastSpeed = physics.Balls[0].Vel.magnitude, worstJump = 0f;
            while (physics.IsMoving)
            {
                physics.Step();
                float speed = physics.Balls[0].Vel.magnitude;
                if (speed > 0f)   // the final snap to rest below StopSpeed is not a jump
                    worstJump = Mathf.Max(worstJump, Mathf.Abs(speed - lastSpeed));
                lastSpeed = speed;
                if (slide < 0f && (physics.Balls[0].Vel - physics.Balls[0].Roll).magnitude <= eps)
                    slide = physics.Balls[0].Pos.y;
            }
            smooth &= worstJump <= 1.5f * BallMotion.SlideDecel(physics.Settings) * config.TimeStep;
            if (pull == 0.2f) slideSoft = slide;
            if (pull == 0.3f) slideMid = slide;
            line.Append($" {pull:P0} ({config.SpeedFromPull(pull):F2} m/s) {slide * 100f:F0} cm,");
        }
        report.Info(line.ToString().TrimEnd(','));
        report.Check(slideSoft >= 0.05f && slideMid <= 0.25f, "soft shots (20-30%) slide 5-25 cm, then roll");
        report.Check(smooth, "no jump in speed when the slide turns into rolling");
        return report.ToString();
    }

    // Worst distance of the predicted cue ball path points (within the simulated arc) from the simulated trail.
    public static float PathError(in Prediction prediction, System.Collections.Generic.List<Vector2> trail)
    {
        float worst = 0f, arc = 0f;
        float trailLength = 0f;
        for (int i = 1; i < trail.Count; i++)
            trailLength += (trail[i] - trail[i - 1]).magnitude;
        for (int p = 0; p < prediction.CuePathCount; p++)
        {
            if (p > 0)
                arc += (prediction.CuePath[p] - prediction.CuePath[p - 1]).magnitude;
            if (arc > trailLength)
                break;
            float best = float.MaxValue;
            for (int i = 1; i < trail.Count; i++)
                best = Mathf.Min(best, TableGeometry.DistanceSquaredToSegment(prediction.CuePath[p], trail[i - 1], trail[i], out _));
            worst = Mathf.Max(worst, Mathf.Sqrt(best));
        }
        return worst;
    }

    private static PoolPhysics OpenTwoBalls(Vector2 cue, Vector2 target)
    {
        PoolPhysics physics = CreateOpenPhysics();
        var balls = new BallState[BallGroups.BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].Number = i;
            balls[i].Pocketed = i > 1;
        }
        balls[0].Pos = cue;
        balls[1].Pos = target;
        physics.SetBalls(balls);
        return physics;
    }

    // Cue, object and corner pocket in one line, perfect release: always pocketed.
    public static string StraightPot()
    {
        var report = new CheckReport("PhysicsChecks.StraightPot");
        var random = new System.Random(11);
        float radius = Config.BallRadius;
        int potted = 0;
        const int shots = 50;
        for (int i = 0; i < shots; i++)
        {
            PoolPhysics physics = CreatePhysics();
            int pocket = new[] { 0, 1, 4, 5 }[i % 4];
            Vector2 pocketCenter = physics.Table.Pockets[pocket].Center;
            Vector2 towardTable = (-pocketCenter).normalized;
            float objectDistance = Mathf.Lerp(0.15f, 0.9f, (float)random.NextDouble());
            float cueDistance = Mathf.Lerp(0.15f, 0.7f, (float)random.NextDouble());
            Vector2 objectPos = pocketCenter + towardTable * objectDistance;
            Vector2 cuePos = objectPos + towardTable * (cueDistance + 2f * radius);
            physics = TwoBalls(cuePos, objectPos);
            physics.Simulate(new ShotParams(-towardTable, Config.SpeedFromPull(0.55f)));
            if (physics.Balls[1].Pocketed)
                potted++;
        }
        report.Info($"{potted}/{shots} straight pots");
        report.Check(potted == shots, "every straight pot with a green release drops");
        return report.ToString();
    }

    public static string NoTunneling(int shots)
    {
        var report = new CheckReport("PhysicsChecks.NoTunneling");
        MatchConfig config = Config;
        float radius = config.BallRadius;
        PoolSettings settings = config.ToPoolSettings();
        var table = new TableGeometry(settings);
        int outside = 0, overlaps = 0, escaped = 0;
        long worstOverlapMicrons = 0;
        long steps = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        // One seed per shot, so the shots run in parallel and stay reproducible.
        System.Threading.Tasks.Parallel.For(0, shots, shot =>
        {
            var random = new System.Random(1234 + shot * 7919);
            var physics = new PoolPhysics(table, settings) { RecordCollisions = false };
            BallState[] balls = shot % 2 == 0
                ? RackBuilder.Build(random, physics.Table, config.RackGap)
                : Scatter(random, physics.Table);
            if (shot % 2 == 0)
                balls[0].Pos = new Vector2(Mathf.Lerp(-0.4f, 0.4f, (float)random.NextDouble()), Mathf.Lerp(-1.0f, -0.6f, (float)random.NextDouble()));
            physics.SetBalls(balls);

            float angle = (float)random.NextDouble() * 360f;
            var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float speed = ShotAccuracy.SpeedFromPull((float)random.NextDouble(), config.MinSpeed, config.MaxSpeed, config.PowerExponent);
            ShotResult result = physics.Simulate(new ShotParams(direction, speed));
            System.Threading.Interlocked.Add(ref steps, Mathf.RoundToInt(result.Duration / settings.TimeStep));
            System.Threading.Interlocked.Add(ref escaped, result.Escaped);

            for (int i = 0; i < physics.Balls.Length; i++)
            {
                if (physics.Balls[i].Pocketed)
                    continue;
                if (!physics.Table.IsInsidePlayArea(physics.Balls[i].Pos, 0.002f) && !InPocketMouth(physics, physics.Balls[i].Pos))
                    System.Threading.Interlocked.Increment(ref outside);
                for (int j = i + 1; j < physics.Balls.Length; j++)
                {
                    if (physics.Balls[j].Pocketed)
                        continue;
                    float overlap = 2f * radius - (physics.Balls[i].Pos - physics.Balls[j].Pos).magnitude;
                    long microns = (long)(overlap * 1e6f);
                    long seen;
                    while (microns > (seen = System.Threading.Interlocked.Read(ref worstOverlapMicrons))
                           && System.Threading.Interlocked.CompareExchange(ref worstOverlapMicrons, microns, seen) != seen) { }
                    if (overlap > 0.0005f)
                        System.Threading.Interlocked.Increment(ref overlaps);
                }
            }
        });

        report.Info($"{shots} shots, {steps} steps in {watch.ElapsedMilliseconds} ms (parallel)");
        report.Info($"outside play area: {outside}, overlaps > 0.5 mm: {overlaps} (worst {worstOverlapMicrons / 1000f:F3} mm), escaped: {escaped}");
        report.Check(outside == 0, "no ball ends outside the play area");
        report.Check(overlaps == 0, "no overlaps > 0.5 mm at rest");
        report.Check(escaped == 0, "no ball escapes the table");
        return report.ToString();
    }

    // A ball may legitimately rest in a pocket mouth (between the cushion ends) without dropping.
    private static bool InPocketMouth(PoolPhysics physics, Vector2 position)
    {
        foreach (PocketInfo pocket in physics.Table.Pockets)
        {
            if ((position - pocket.Center).magnitude < pocket.CaptureRadius + 0.08f)
                return true;
        }
        return false;
    }

    public static ShotResult Break(int seed, float power)
    {
        MatchConfig config = Config;
        PoolPhysics physics = CreatePhysics();
        var random = new System.Random(seed);
        BallState[] balls = RackBuilder.Build(random, physics.Table, config.RackGap);
        physics.SetBalls(balls);
        Vector2 apex = physics.Table.FootSpot;
        return physics.Simulate(new ShotParams(apex - balls[0].Pos, config.SpeedFromPull(power)));
    }

    public static PoolPhysics TwoBalls(Vector2 cue, Vector2 target)
    {
        PoolPhysics physics = CreatePhysics();
        var balls = new BallState[BallGroups.BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].Number = i;
            balls[i].Pocketed = i > 1;
        }
        balls[0].Pos = cue;
        balls[1].Pos = target;
        physics.SetBalls(balls);
        return physics;
    }

    public static BallState[] Scatter(System.Random random, TableGeometry table)
    {
        var balls = new BallState[BallGroups.BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].Number = i;
            // Unplaced balls still sit at the origin, so the centre spot is avoided too; that's fine here.
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var position = new Vector2(
                    Mathf.Lerp(-table.MaxX, table.MaxX, (float)random.NextDouble()),
                    Mathf.Lerp(-table.MaxY, table.MaxY, (float)random.NextDouble()));
                if (RackBuilder.IsFree(balls, position, table.BallRadius * 1.02f, i) || attempt == 199)
                {
                    balls[i].Pos = position;
                    break;
                }
            }
        }
        return balls;
    }

    private static float RunToFirstContact(PoolPhysics physics)
    {
        float speed = 0f;
        while (physics.IsMoving && physics.Result.FirstContactBall < 0)
        {
            speed = physics.Balls[0].Vel.magnitude;
            physics.Step();
        }
        return speed;
    }
}
