using UnityEditor;
using UnityEngine;

// Headless checks of PoolPhysics against the tuning targets (implementation_definition 4.6.1 / 8).
public static class PhysicsChecks
{
    public static MatchConfig Config => AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);

    public static CueCatalog Cues => AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath);

    // The power curve of a cue (v4 V1); the checks without a cue use the House Cue's.
    public static PowerCurve Curve(string cueId = "house") => Config.PowerCurve((Cues.Get(cueId) ?? Cues.Starter).MaxSpeed);

    public static float SpeedFromPull(float pull01, string cueId = "house") => Curve(cueId).SpeedFromPull(pull01);

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
        Determinism() + "\n" + Timings() + "\n" + Collisions() + "\n" + SlideRoll() + "\n" + Spin() + "\n" + StraightPot() + "\n" + NoTunneling(1000);

    public static string Determinism()
    {
        var report = new CheckReport("PhysicsChecks.Determinism");
        Vector2[] first = Break(7, 1f).FinalPositions.Clone() as Vector2[];
        Vector2[] second = Break(7, 1f).FinalPositions;
        bool identical = true;
        for (int i = 0; i < first.Length; i++)
            identical &= first[i] == second[i];
        report.Check(identical, "same break twice gives bit-identical final positions");

        // v4: random tips and elevations on a scattered table, twice each.
        var random = new System.Random(3);
        PowerCurve curve = Curve();
        float spinMax = Config.SpinMaxOffset;
        int differing = 0;
        for (int shot = 0; shot < 20; shot++)
        {
            BallState[] balls = Scatter(random, CreatePhysics().Table);
            ShotParams parameters = RandomShot(random, curve, spinMax);
            PoolPhysics a = CreatePhysics(), b = CreatePhysics();
            a.SetBalls(balls);
            b.SetBalls(balls);
            a.Simulate(parameters);
            b.Simulate(parameters);
            for (int i = 0; i < balls.Length; i++)
            {
                if (a.Balls[i].Pos != b.Balls[i].Pos || a.Balls[i].Pocketed != b.Balls[i].Pocketed)
                {
                    differing++;
                    break;
                }
            }
        }
        report.Check(differing == 0, $"20 shots with random tip and elevation, twice: bit-identical ({differing} differ)");
        return report.ToString();
    }

    // Any direction, pull, tip inside SpinMaxOffset and elevation 5-40 deg (the checks' random spin shots).
    // Thread-safe: no asset access.
    public static ShotParams RandomShot(System.Random random, in PowerCurve curve, float spinMax)
    {
        float angle = (float)random.NextDouble() * 360f;
        var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        float speed = curve.SpeedFromPull((float)random.NextDouble());
        return new ShotParams(direction, speed, RandomTip(random, spinMax), Mathf.Lerp(5f, 40f, (float)random.NextDouble()));
    }

    public static Vector2 RandomTip(System.Random random, float spinMax)
    {
        float tipAngle = (float)random.NextDouble() * Mathf.PI * 2f;
        float tipRadius = spinMax * Mathf.Sqrt((float)random.NextDouble());
        return new Vector2(Mathf.Cos(tipAngle), Mathf.Sin(tipAngle)) * tipRadius;
    }

    public static string Timings()
    {
        var report = new CheckReport("PhysicsChecks.Timings");
        MatchConfig config = Config;

        // Break at full power (v4: House 4.5 m/s ... Legend 6.5 m/s).
        float min = float.MaxValue, max = 0f, sum = 0f;
        int houseTotal = 0, legendTotal = 0;
        const int breaks = 20;
        for (int seed = 0; seed < breaks; seed++)
        {
            ShotResult result = Break(seed, 1f, "house");
            min = Mathf.Min(min, result.Duration);
            max = Mathf.Max(max, result.Duration);
            sum += result.Duration;
            houseTotal += result.Pocketed.Count;
            legendTotal += Break(seed, 1f, "legend").Pocketed.Count;
        }
        report.Info($"break 100% House ({SpeedFromPull(1f, "house"):F1} m/s): settle {min:F2}-{max:F2}s (avg {sum / breaks:F2}s), avg {houseTotal / (float)breaks:F2} balls pocketed; " +
                    $"Legend ({SpeedFromPull(1f, "legend"):F1} m/s): avg {legendTotal / (float)breaks:F2}");
        report.Check(min >= 5f && max <= 9f, "House break settles in 5-9 s");
        report.Check(legendTotal >= houseTotal, "Legend break pockets on average at least as many as House");

        // Shots on an empty table, nearly straight up the table: path length of the cue ball (with the cushions), and
        // on an open table with no cushions (the V1 target: v3 MED rolled ~11 m = ~5 table lengths).
        foreach (float pull in new[] { 0.3f, 0.5f })
        {
            PoolPhysics physics = CreatePhysics();
            var balls = new BallState[BallGroups.BallCount];
            for (int i = 0; i < balls.Length; i++)
            {
                balls[i].Number = i;
                balls[i].Pocketed = i != 0;
            }
            balls[0].Pos = new Vector2(0.1f, -0.8f);
            physics.SetBalls(balls);
            float speed = SpeedFromPull(pull);
            physics.BeginShot(new ShotParams(new Vector2(0.02f, 1f), speed));
            float path = 0f;
            Vector2 last = physics.Balls[0].Pos;
            while (physics.IsMoving && physics.Time < 60f)
            {
                physics.Step();
                path += (physics.Balls[0].Pos - last).magnitude;
                last = physics.Balls[0].Pos;
            }

            PoolPhysics open = OpenTwoBalls(Vector2.zero, new Vector2(50f, 50f));
            open.Simulate(new ShotParams(Vector2.up, speed));
            float free = open.Balls[0].Pos.y / config.PlayLength;
            report.Info($"{pull:P0} shot: speed {speed:F2} m/s, on the table {path:F2} m = {path / config.PlayLength:F2} table lengths in {physics.Time:F1}s; " +
                        $"no cushions {open.Balls[0].Pos.y:F2} m = {free:F2} lengths");
            if (pull == 0.5f)
                report.Check(free >= 1.8f && free <= 3.2f, "MED (50%) shot with nothing in the way rolls 1.8-3.2 table lengths");
        }
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
        report.Info($"(b) 30 deg cut: object leaves at {objectAngle:F2} deg (line of centres 30), separation {separation:F1} deg");
        // v4: cut-induced throw turns the object ball a little towards the cue ball's direction.
        report.Check(30f - objectAngle > 0f && 30f - objectAngle < 4f, "cut: object ball leaves along the line of centres, thrown forward by < 4 deg");
        report.Check(Mathf.Abs(separation - 90f) < 6f, "cut: balls separate at ~90 deg");

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
        Prediction prediction = predictor.Predict(physics.Balls, new ShotParams(Vector2.up, 2f), null, 0.3f);
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
        foreach (float pull in new[] { 0.1f, 0.25f, 0.3f, 0.5f, 1f })
        {
            physics = OpenTwoBalls(Vector2.zero, new Vector2(50f, 50f));
            physics.BeginShot(new ShotParams(Vector2.up, SpeedFromPull(pull)));
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
            if (pull == 0.25f) slideSoft = slide;
            if (pull == 0.3f) slideMid = slide;
            line.Append($" {pull:P0} ({SpeedFromPull(pull):F2} m/s) {slide * 100f:F0} cm,");
        }
        report.Info(line.ToString().TrimEnd(','));
        // v4 curve: 25-30% = 0.66-0.79 m/s (v3: 20-30% = 0.6-1.25 m/s).
        report.Check(slideSoft >= 0.05f && slideMid <= 0.25f, "soft shots (25-30%) slide 5-25 cm, then roll");
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

    // The draw test (user decision 2026-10-25: real cloth friction, mu 0.2): b -0.5 at ~2.5 m/s (p ~0.65) from 0.5 m
    // comes back 0.8-1.6 m; at MED the same shot is a stun or rolls on a little.
    public const float DrawSpeed = 2.5f;
    public const float DrawGap = 0.5f;

    // Off-centre hits (implementation_definition_iteration_v4 V1). Speeds are the cue ball's off the tip.
    public static string Spin()
    {
        var report = new CheckReport("PhysicsChecks.Spin");
        MatchConfig config = Config;

        // Power curve: the same for every cue up to HARD, then each cue's own top speed.
        PowerCurve house = Curve();
        float soft = house.SpeedFromPull(0.15f), med = house.SpeedFromPull(0.5f), hard = house.SpeedFromPull(0.85f);
        var tops = new System.Text.StringBuilder();
        bool topsOk = true;
        foreach (CueDefinition cue in Cues.Cues)
        {
            float top = Curve(cue.Id).SpeedFromPull(1f);
            topsOk &= Mathf.Abs(top - cue.MaxSpeed) < 1e-4f && Mathf.Abs(Curve(cue.Id).SpeedFromPull(0.5f) - med) < 1e-5f;
            tops.Append($" {cue.Id} {top:F1}");
        }
        report.Info($"power curve: SOFT 15% {soft:F2}, MED 50% {med:F2}, HARD 85% {hard:F2} m/s; 100%:{tops}");
        report.Check(soft >= 0.45f && soft <= 0.6f, "p 0.15 -> 0.45-0.6 m/s");
        report.Check(med >= 1.4f && med <= 1.6f, "p 0.5 -> 1.4-1.6 m/s");
        report.Check(hard >= 3.4f && hard <= 3.6f, "p 0.85 -> 3.4-3.6 m/s");
        report.Check(topsOk, "p 1.0 = the cue's MaxSpeed; below HARD the same for every cue");

        // Natural roll: b = 0.4 rolls off the tip.
        PoolPhysics physics = OpenTwoBalls(Vector2.zero, new Vector2(50f, 50f));
        physics.BeginShot(new ShotParams(Vector2.up, med, new Vector2(0f, 0.4f), config.CueElevation));
        float slip = (physics.Balls[0].Vel - physics.Balls[0].Roll).magnitude;
        report.Info($"natural roll (b +0.4): slip off the tip {slip:F4} m/s");
        report.Check(slip < 0.02f, "natural roll: slip < 0.02 m/s");

        // Stun: the b that leaves no roll at the contact, full hit 0.5 m. At MED even full draw is used up by then
        // (mu 0.2, user decision 2026-10-25), so at the draw speed.
        float low = -config.SpinMaxOffset, high = 0.4f;
        for (int i = 0; i < 30; i++)
        {
            float mid = (low + high) * 0.5f;
            FullHit(DrawSpeed, mid, 0.5f, out float rollAtContact);
            if (rollAtContact > 0f) high = mid; else low = mid;
        }
        float stunB = (low + high) * 0.5f;
        float stun = FullHit(DrawSpeed, stunB, 0.5f, out _);
        report.Info($"stun (b {stunB:+0.00;-0.00}, 0.5 m, {DrawSpeed:F1} m/s): cue ball moves {stun * 100f:F1} cm after the contact");
        report.Check(Mathf.Abs(stun) < 0.05f, "stun: cue ball moves < 5 cm after a full hit");

        float draw = FullHit(DrawSpeed, -0.5f, DrawGap, out _);
        float follow = FullHit(med, 0.5f, 0.5f, out _);
        float drawMed = FullHit(med, -0.5f, 0.5f, out _);
        report.Info($"draw (b -0.5, {DrawGap:F2} m, {DrawSpeed:F2} m/s): {-draw:F2} m back (MED {med:F2} m/s: {-drawMed:F2} m back); follow (b +0.5, 0.5 m, MED): {follow:F2} m on");
        report.Check(-draw >= 0.8f && -draw <= 1.6f, "draw: comes back 0.8-1.6 m");
        report.Check(follow >= 0.5f && follow <= 1.5f, "follow: goes on 0.5-1.5 m");

        // Side spin into a cushion at 45 deg, 1.5 m/s: heading up and right into the right rail, left english runs.
        float plain = CushionExit(0f), running = CushionExit(-0.5f), reverse = CushionExit(0.5f);
        report.Info($"cushion at 45 deg, 1.5 m/s: out at {plain:F1} deg from the normal; running english {running:F1} ({running - plain:+0.0;-0.0}), reverse {reverse:F1} ({reverse - plain:+0.0;-0.0})");
        report.Check(running - plain >= 6f && running - plain <= 20f, "running english opens the rebound by 6-20 deg");
        report.Check(plain - reverse >= 6f && plain - reverse <= 20f, "reverse english closes it by 6-20 deg");

        // Squirt: a = 0.5, level cue.
        physics = OpenTwoBalls(Vector2.zero, new Vector2(50f, 50f));
        physics.BeginShot(new ShotParams(Vector2.up, med, new Vector2(0.5f, 0f), config.CueElevation));
        while (physics.IsMoving && physics.Balls[0].Pos.magnitude < 0.5f)
            physics.Step();
        float squirt = Vector2.SignedAngle(Vector2.up, physics.Balls[0].Pos);
        report.Info($"squirt (a +0.5): the cue ball runs {squirt:+0.00;-0.00} deg off the cue's line (+ = left)");
        report.Check(squirt >= 1.5f && squirt <= 2.5f, "squirt: 1.5-2.5 deg away from the side hit");

        // Throw: 30 deg cut, centre, slow and fast.
        float slowThrow = Throw(0.5f), fastThrow = Throw(3f);
        report.Info($"throw (30 deg cut, centre): {slowThrow:F2} deg at 0.5 m/s, {fastThrow:F2} deg at 3 m/s (off the ghost-ball line)");
        report.Check(slowThrow >= 1.5f && slowThrow <= 4f, "throw: slow 1.5-4 deg");
        report.Check(fastThrow < 1.5f, "throw: fast < 1.5 deg");

        // Masse: a = 0.5 at 30 deg elevation, 1.5 m/s; nearly level (3 deg) stays straight.
        float masse = Swerve(30f), level = Swerve(3f);
        report.Info($"masse (a +0.5, 1.5 m/s): {masse * 100f:F1} cm off its first line after 1 m at 30 deg, {level * 100f:F2} cm at 3 deg");
        report.Check(masse >= 0.03f, "masse: >= 3 cm after 1 m at 30 deg");
        report.Check(level < 0.005f, "masse: < 0.5 cm at 3 deg");

        // Cushion restitution, straight in.
        float slowBack = Rebound(0.5f), fastBack = Rebound(4f);
        report.Info($"cushion restitution: {slowBack:P0} back at 0.5 m/s, {fastBack:P0} at 4 m/s");
        report.Check(slowBack >= 0.85f, "slow hit: >= 85% of the normal speed back");
        report.Check(fastBack <= 0.75f, "fast hit: <= 75% back");
        return report.ToString();
    }

    // Full hit `gap` metres from the ghost ball: the cue ball's signed travel along the shot after the contact.
    private static float FullHit(float speed, float b, float gap, out float rollAtContact)
    {
        float radius = Config.BallRadius;
        PoolPhysics physics = OpenTwoBalls(new Vector2(0f, -gap - 2f * radius), Vector2.zero);
        physics.BeginShot(new ShotParams(Vector2.up, speed, new Vector2(0f, b), Config.CueElevation));
        rollAtContact = 0f;
        while (physics.IsMoving && physics.Result.FirstContactBall < 0)
        {
            rollAtContact = physics.Balls[0].Roll.y;
            physics.Step();
        }
        float contact = physics.Result.FirstContactCuePos.y;
        while (physics.IsMoving)
            physics.Step();
        return physics.Balls[0].Pos.y - contact;
    }

    // Degrees from the cushion normal right after the bounce, side offset a (no elevation).
    private static float CushionExit(float a)
    {
        PoolPhysics physics = TwoBalls(new Vector2(0.3f, 0.25f), new Vector2(-0.4f, -0.9f));
        physics.BeginShot(new ShotParams(new Vector2(1f, 1f), 1.5f, new Vector2(a, 0f), Config.CueElevation));
        while (physics.IsMoving && physics.Result.CueCushions == 0)
            physics.Step();
        return Vector2.Angle(physics.Balls[0].Vel, Vector2.left);
    }

    private static float Throw(float speed)
    {
        float radius = Config.BallRadius;
        var objectDirection = new Vector2(Mathf.Sin(30f * Mathf.Deg2Rad), Mathf.Cos(30f * Mathf.Deg2Rad));
        Vector2 ghost = -objectDirection * 2f * radius;
        PoolPhysics physics = OpenTwoBalls(ghost - Vector2.up * 0.3f, Vector2.zero);
        physics.BeginShot(new ShotParams(Vector2.up, speed));
        while (physics.IsMoving && physics.Result.FirstContactBall < 0)
            physics.Step();
        Vector2 centres = (Vector2.zero - physics.Result.FirstContactCuePos).normalized;
        return Vector2.Angle(centres, physics.Balls[1].Vel);
    }

    // Sideways distance (m) from the cue ball's first line once it is 1 m along it.
    private static float Swerve(float elevation)
    {
        PoolPhysics physics = OpenTwoBalls(Vector2.zero, new Vector2(50f, 50f));
        physics.BeginShot(new ShotParams(Vector2.up, 1.5f, new Vector2(0.5f, 0f), elevation));
        Vector2 line = physics.Balls[0].Vel.normalized;
        while (physics.IsMoving && Vector2.Dot(physics.Balls[0].Pos, line) < 1f)
            physics.Step();
        Vector2 at = physics.Balls[0].Pos;
        return Mathf.Abs(at.x * line.y - at.y * line.x);
    }

    // Share of the normal speed a ball keeps, rolling straight into the right rail at `speed`.
    private static float Rebound(float speed)
    {
        PoolPhysics physics = TwoBalls(new Vector2(Config.HalfWidth - Config.BallRadius - 0.004f, 0.5f), new Vector2(-0.4f, -0.9f));
        physics.BeginShot(new ShotParams(Vector2.right, speed, new Vector2(0f, 0.4f), Config.CueElevation));
        float before = speed;
        while (physics.IsMoving && physics.Result.CueCushions == 0)
        {
            before = physics.Balls[0].Vel.x;
            physics.Step();
        }
        return -physics.Balls[0].Vel.x / before;
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
            physics.Simulate(new ShotParams(-towardTable, SpeedFromPull(0.55f)));
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
        PowerCurve legend = Curve("legend");
        float spinMax = config.SpinMaxOffset;
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

            // Half the shots with random spin and elevation (v4), the strongest cue's curve.
            ShotParams parameters = RandomShot(random, legend, spinMax);
            if (shot % 4 < 2)
                parameters.Tip = Vector2.zero;
            ShotResult result = physics.Simulate(parameters);
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

    public static ShotResult Break(int seed, float power) => Break(seed, power, "house");

    public static ShotResult Break(int seed, float power, string cueId)
    {
        MatchConfig config = Config;
        PoolPhysics physics = CreatePhysics();
        var random = new System.Random(seed);
        BallState[] balls = RackBuilder.Build(random, physics.Table, config.RackGap);
        physics.SetBalls(balls);
        Vector2 apex = physics.Table.FootSpot;
        return physics.Simulate(new ShotParams(apex - balls[0].Pos, SpeedFromPull(power, cueId)));
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
