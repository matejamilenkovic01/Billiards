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

    public static string All() =>
        Determinism() + "\n" + Timings() + "\n" + Collisions() + "\n" + StraightPot() + "\n" + NoTunneling(1000);

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
        report.Check(lengths >= 1f && lengths <= 1.5f, "30% shot travels 1-1.5 table lengths");
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
        var random = new System.Random(1234);
        int outside = 0, overlaps = 0, escaped = 0;
        float worstOverlap = 0f;
        long steps = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        for (int shot = 0; shot < shots; shot++)
        {
            PoolPhysics physics = CreatePhysics();
            physics.RecordCollisions = false;
            BallState[] balls = shot % 2 == 0
                ? RackBuilder.Build(random, physics.Table, config.RackGap)
                : Scatter(random, physics.Table);
            if (shot % 2 == 0)
                balls[0].Pos = new Vector2(Mathf.Lerp(-0.4f, 0.4f, (float)random.NextDouble()), Mathf.Lerp(-1.0f, -0.6f, (float)random.NextDouble()));
            physics.SetBalls(balls);

            float angle = (float)random.NextDouble() * 360f;
            var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float speed = config.SpeedFromPull((float)random.NextDouble());
            ShotResult result = physics.Simulate(new ShotParams(direction, speed));
            steps += Mathf.RoundToInt(result.Duration / config.TimeStep);
            escaped += result.Escaped;

            for (int i = 0; i < physics.Balls.Length; i++)
            {
                if (physics.Balls[i].Pocketed)
                    continue;
                if (!physics.Table.IsInsidePlayArea(physics.Balls[i].Pos, 0.002f) && !InPocketMouth(physics, physics.Balls[i].Pos))
                    outside++;
                for (int j = i + 1; j < physics.Balls.Length; j++)
                {
                    if (physics.Balls[j].Pocketed)
                        continue;
                    float overlap = 2f * radius - (physics.Balls[i].Pos - physics.Balls[j].Pos).magnitude;
                    worstOverlap = Mathf.Max(worstOverlap, overlap);
                    if (overlap > 0.0005f)
                        overlaps++;
                }
            }
        }

        report.Info($"{shots} shots, {steps} steps in {watch.ElapsedMilliseconds} ms ({watch.ElapsedMilliseconds * 1000.0 / Mathf.Max(1, steps):F2} us/step)");
        report.Info($"outside play area: {outside}, overlaps > 0.5 mm: {overlaps} (worst {worstOverlap * 1000f:F3} mm), escaped: {escaped}");
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
