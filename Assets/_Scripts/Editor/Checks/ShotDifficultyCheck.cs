using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Release difficulty by cue x power (implementation_definition_v3 V8): the green / green+yellow odds for an
// average player (sigma 45 ms), and pot rates through the real physics.
public static class ShotDifficultyCheck
{
    private const float PlayerSigmaMs = BotBalanceCheck.AverageSigmaMs;
    private static readonly float[] Powers = { 0.2f, 0.5f, 0.8f, 1f };

    // The six cues of v3 V11 (GreenHalf, YellowWidth, Period); the catalog gets them in commit 7.
    private static readonly (string Name, float Green, float Yellow, float Period)[] Cues =
    {
        ("House", 0.10f, 0.30f, 1.30f), ("Club", 0.12f, 0.30f, 1.40f), ("Pro", 0.14f, 0.30f, 1.55f),
        ("Master", 0.16f, 0.29f, 1.70f), ("Champion", 0.18f, 0.28f, 1.85f), ("Legend", 0.20f, 0.27f, 2.00f)
    };

    // V8 table: green / green+yellow in percent, per cue and power.
    private static readonly int[,,] Targets =
    {
        { { 78, 100 }, { 58, 100 }, { 36, 97 }, { 23, 89 } },
        { { 89, 100 }, { 70, 100 }, { 45, 98 }, { 29, 93 } },
        { { 96, 100 }, { 82, 100 }, { 56, 99 }, { 37, 96 } },
        { { 99, 100 }, { 91, 100 }, { 67, 100 }, { 45, 98 } },
        { { 100, 100 }, { 96, 100 }, { 77, 100 }, { 54, 99 } },
        { { 100, 100 }, { 99, 100 }, { 85, 100 }, { 62, 99 } }
    };

    public static string All() => ReleaseTable() + "\n" + PotRates();

    public static string ReleaseTable(int samples = 20000)
    {
        var report = new CheckReport("ShotDifficultyCheck.ReleaseTable");
        MatchConfig config = PhysicsChecks.Config;
        ShotAccuracy.PowerScaling scaling = config.ReleaseScaling();
        float sigma = PlayerSigmaMs * 0.001f;
        var random = new System.Random(5);
        var table = new StringBuilder($"green / green+yellow, sigma {PlayerSigmaMs:F0} ms, analytic | Monte Carlo ({samples}):\n          ");
        foreach (float p in Powers)
            table.Append($"  p={p:F1}            ");
        int off = 0;
        for (int c = 0; c < Cues.Length; c++)
        {
            table.Append($"\n{Cues[c].Name,-9}");
            ShotAccuracy.Settings cue = config.AccuracySettings(Cues[c].Green, Cues[c].Yellow, Cues[c].Period);
            for (int i = 0; i < Powers.Length; i++)
            {
                ShotAccuracy.Settings window = ShotAccuracy.AtPower(cue, scaling, Powers[i]);
                // The marker crosses 4 units per period: a zone of half-width w is w * period / 4 seconds either side.
                float green = Erf(window.GreenHalf * window.Period / 4f / (sigma * Mathf.Sqrt(2f)));
                float both = Erf(Mathf.Min(1f, window.GreenHalf + window.YellowWidth) * window.Period / 4f / (sigma * Mathf.Sqrt(2f)));
                int mcGreen = 0, mcBoth = 0;
                for (int s = 0; s < samples; s++)
                {
                    float position = ShotAccuracy.PositionFromTimingError(ShotAccuracy.Gaussian(random) * sigma, window.Period);
                    ReleaseZone zone = ShotAccuracy.ZoneOf(position, window.GreenHalf, window.YellowWidth);
                    if (zone == ReleaseZone.Green) mcGreen++;
                    if (zone != ReleaseZone.Red) mcBoth++;
                }
                float mg = mcGreen / (float)samples, mb = mcBoth / (float)samples;
                table.Append($"  {green * 100f,3:F0}/{both * 100f,3:F0} | {mg * 100f,3:F0}/{mb * 100f,3:F0}");
                bool inTarget = Mathf.Abs(green * 100f - Targets[c, i, 0]) <= 5f && Mathf.Abs(both * 100f - Targets[c, i, 1]) <= 5f
                                && Mathf.Abs(mg * 100f - Targets[c, i, 0]) <= 5f && Mathf.Abs(mb * 100f - Targets[c, i, 1]) <= 5f;
                if (!inTarget)
                {
                    off++;
                    report.Info($"off target: {Cues[c].Name} p={Powers[i]:F1} wants {Targets[c, i, 0]}/{Targets[c, i, 1]}");
                }
            }
        }
        report.Info(table.ToString());
        report.Check(off == 0, $"all 24 cells within 5 p.p. of the V8 table ({off} off)");
        return report.ToString();
    }

    // Three reference shots x six cues x four powers through PoolPhysics: average timing plus 0.15 deg aim noise.
    public static string PotRates(int samples = 2000)
    {
        var report = new CheckReport("ShotDifficultyCheck.PotRates");
        MatchConfig config = PhysicsChecks.Config;
        ShotAccuracy.PowerScaling scaling = config.ReleaseScaling();
        PoolSettings settings = config.ToPoolSettings();
        var geometry = new TableGeometry(settings);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using var physicsPerThread = new ThreadLocal<PoolPhysics>(() => new PoolPhysics(geometry, settings) { RecordCollisions = true });
        // Object ball distance from the corner pocket, cue ball to ghost ball, cut angle.
        (string Name, float ObjectToPocket, float CueToGhost, float Cut)[] shots =
        {
            ("easy (straight, 0.5 m)", 0.3f, 0.5f, 0f), ("medium (30 deg, 1 m)", 0.5f, 1f, 30f), ("hard (45 deg, 1.8 m)", 0.25f, 1.8f, 45f)
        };

        var rates = new float[shots.Length, Cues.Length, Powers.Length];
        var feasible = new bool[shots.Length, Powers.Length];
        var text = new StringBuilder($"pot rate %, {samples} shots per cell, sigma {PlayerSigmaMs:F0} ms, aim noise 0.15 deg (- = a perfect shot does not reach):");
        for (int shot = 0; shot < shots.Length; shot++)
        {
            Setup(geometry, shots[shot].ObjectToPocket, shots[shot].CueToGhost, shots[shot].Cut, out BallState[] balls, out Vector2 aim, out int pocket);
            for (int i = 0; i < Powers.Length; i++)
                feasible[shot, i] = Pots(physicsPerThread, balls, aim, config.SpeedFromPull(Powers[i]), pocket);

            int cells = Cues.Length * Powers.Length;
            int shotIndex = shot;
            Parallel.For(0, cells, cell =>
            {
                int c = cell / Powers.Length, i = cell % Powers.Length;
                if (!feasible[shotIndex, i])
                    return;
                var random = new System.Random(1000 * shotIndex + cell);
                ShotAccuracy.Settings window = ShotAccuracy.AtPower(config.AccuracySettings(Cues[c].Green, Cues[c].Yellow, Cues[c].Period), scaling, Powers[i]);
                float speed = config.SpeedFromPull(Powers[i]);
                int potted = 0;
                for (int s = 0; s < samples; s++)
                {
                    float position = ShotAccuracy.PositionFromTimingError(ShotAccuracy.Gaussian(random) * PlayerSigmaMs * 0.001f, window.Period);
                    ShotAccuracy.Outcome release = ShotAccuracy.Evaluate(position, window, random);
                    Vector2 direction = ShotAccuracy.Rotate(aim, release.AngleErrorDeg + ShotAccuracy.Gaussian(random) * 0.15f);
                    if (Pots(physicsPerThread, balls, direction, Mathf.Min(speed * release.PowerFactor, config.MaxSpeed), pocket))
                        potted++;
                }
                rates[shotIndex, c, i] = potted / (float)samples;
            });

            text.Append($"\n{shots[shot].Name}:");
            for (int c = 0; c < Cues.Length; c++)
            {
                text.Append($"\n  {Cues[c].Name,-9}");
                for (int i = 0; i < Powers.Length; i++)
                    text.Append(feasible[shot, i] ? $" p{Powers[i]:F1} {rates[shot, c, i] * 100f,3:F0}" : $" p{Powers[i]:F1}   -");
            }
        }
        report.Info(text.ToString());
        report.Info($"{watch.Elapsed.TotalSeconds:F1}s");

        // Monotone (2 p.p. of Monte Carlo slack): falls with power, rises with the cue.
        const float Slack = 0.02f;
        int powerBreaks = 0, cueBreaks = 0;
        for (int shot = 0; shot < shots.Length; shot++)
        for (int c = 0; c < Cues.Length; c++)
        for (int i = 0; i < Powers.Length; i++)
        {
            if (!feasible[shot, i])
                continue;
            if (i + 1 < Powers.Length && feasible[shot, i + 1] && rates[shot, c, i + 1] > rates[shot, c, i] + Slack)
                powerBreaks++;
            if (c + 1 < Cues.Length && rates[shot, c + 1, i] + Slack < rates[shot, c, i])
                cueBreaks++;
        }
        report.Check(powerBreaks == 0, $"pot rate falls with power ({powerBreaks} exceptions)");
        report.Check(cueBreaks == 0, $"pot rate rises with the cue ({cueBreaks} exceptions)");
        report.Check(feasible[0, 0] && rates[0, 0, 0] >= 0.8f, $"easy, House, p=0.2: >= 80% ({rates[0, 0, 0]:P0})");
        float hardLegend = rates[2, Cues.Length - 1, Powers.Length - 1];
        report.Check(hardLegend >= 0.35f && hardLegend <= 0.6f, $"hard, Legend, p=1.0: 35-60% ({hardLegend:P0})");
        return report.ToString();
    }

    // Object ball `objectToPocket` from the top-right corner pocket on its entry line; the cue ball `cueToGhost`
    // behind the ghost ball at `cut` degrees, on whichever side fits the table.
    private static void Setup(TableGeometry geometry, float objectToPocket, float cueToGhost, float cut, out BallState[] balls, out Vector2 aim, out int pocket)
    {
        pocket = 5;
        PocketInfo info = geometry.Pockets[pocket];
        Vector2 objectDir = info.Inward;
        Vector2 objectPos = info.Center - objectDir * objectToPocket;
        Vector2 ghost = objectPos - objectDir * 2f * geometry.BallRadius;
        aim = ShotAccuracy.Rotate(objectDir, cut);
        if (!geometry.IsInsidePlayArea(ghost - aim * cueToGhost))
            aim = ShotAccuracy.Rotate(objectDir, -cut);
        balls = new BallState[BallGroups.BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].Number = i;
            balls[i].Pocketed = i > 1;
        }
        balls[0].Pos = ghost - aim * cueToGhost;
        balls[1].Pos = objectPos;
    }

    // The object ball drops in the target pocket before it touches a cushion (the cue ball may).
    private static bool Pots(ThreadLocal<PoolPhysics> physicsPerThread, BallState[] balls, Vector2 direction, float speed, int pocket)
    {
        PoolPhysics physics = physicsPerThread.Value;
        physics.SetBalls(balls);
        physics.BeginShot(new ShotParams(direction, speed));
        System.Collections.Generic.List<CollisionEvent> collisions = physics.Result.Collisions;
        int seen = 0;
        for (int step = 0; step < 15000 && physics.IsMoving; step++)
        {
            physics.Step();
            if (physics.Balls[1].Pocketed)
                return physics.Balls[1].PocketIndex == pocket;
            for (; seen < collisions.Count; seen++)
            {
                if (collisions[seen].Kind == CollisionKind.Cushion && collisions[seen].BallA == 1)
                    return false;
            }
        }
        return false;
    }

    // Abramowitz-Stegun 7.1.26 (|error| < 1.5e-7).
    private static float Erf(float x)
    {
        float sign = Mathf.Sign(x);
        x = Mathf.Abs(x);
        float t = 1f / (1f + 0.3275911f * x);
        float y = 1f - ((((1.061405429f * t - 1.453152027f) * t + 1.421413741f) * t - 0.284496736f) * t + 0.254829592f) * t * Mathf.Exp(-x * x);
        return sign * y;
    }
}
