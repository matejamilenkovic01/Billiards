using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Headless matches: the bot against a "simulated player" that uses the same BotBrain and only differs in how
// often it hits the green zone. Good releases must win, bad releases must lose (implementation_definition 8).
public static class BotBalanceCheck
{
    private const int MaxShotsPerMatch = 250;

    public static string Run(int matchesPerProfile = 200)
    {
        var report = new CheckReport("BotBalanceCheck");
        MatchConfig config = PhysicsChecks.Config;
        var profile = AssetDatabase.LoadAssetAtPath<BotProfile>(DataAssetBuilder.BotProfilePath);
        PoolSettings pool = config.ToPoolSettings();
        var table = new TableGeometry(pool);
        BotSettings bot = profile.ToBotSettings(config);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        float good = WinRate(table, pool, bot, WithRelease(bot, 0.75f, 0.20f, 0.05f), matchesPerProfile, 1000, out string goodInfo);
        float bad = WinRate(table, pool, bot, WithRelease(bot, 0.20f, 0.45f, 0.35f), matchesPerProfile, 2000, out string badInfo);
        float same = WinRate(table, pool, bot, bot, matchesPerProfile, 3000, out string sameInfo);

        report.Info($"bot release: green {bot.GreenChance:P0} / yellow {bot.YellowChance:P0} / red {bot.RedChance:P0}, {matchesPerProfile} matches per profile, {watch.Elapsed.TotalSeconds:F0}s");
        report.Info($"good player (75/20/5) wins {good:P1} {goodInfo}");
        report.Info($"bad player (20/45/35) wins {bad:P1} {badInfo}");
        report.Info($"same as bot (45/40/15) wins {same:P1} {sameInfo}");
        report.Check(good >= 0.70f, "good player wins >= 70%");
        report.Check(bad <= 0.30f, "bad player wins <= 30%");
        report.Check(Mathf.Abs(same - 0.5f) <= 0.08f, "same release as the bot wins 50% +- 8%");
        return report.ToString();
    }

    private static BotSettings WithRelease(BotSettings settings, float green, float yellow, float red)
    {
        settings.GreenChance = green;
        settings.YellowChance = yellow;
        settings.RedChance = red;
        return settings;
    }

    private static float WinRate(TableGeometry table, PoolSettings pool, BotSettings bot, BotSettings player, int matches, int seedBase, out string info)
    {
        int playerWins = 0, decided = 0, totalShots = 0, fouls = 0;
        Parallel.For(0, matches, i =>
        {
            Side? winner = PlayMatch(seedBase + i, table, pool, bot, player, out int shots, out int matchFouls);
            Interlocked.Add(ref totalShots, shots);
            Interlocked.Add(ref fouls, matchFouls);
            if (winner == null)
                return;
            Interlocked.Increment(ref decided);
            if (winner == Side.Player)
                Interlocked.Increment(ref playerWins);
        });

        info = $"({playerWins}/{decided} decided, avg {totalShots / (float)matches:F1} shots, {fouls / (float)matches:F1} fouls per match)";
        return decided > 0 ? playerWins / (float)decided : 0f;
    }

    // One full match; null if it hits the shot cap.
    public static Side? PlayMatch(int seed, TableGeometry table, PoolSettings pool, BotSettings bot, BotSettings player, out int shots, out int fouls)
    {
        var random = new System.Random(seed);
        var physics = new PoolPhysics(table, pool) { RecordCollisions = false };
        var brain = new BotBrain(table, pool, bot);
        BallState[] balls = RackBuilder.Build(random, table, pool.RackGap);
        RulesState state = RulesState.NewRack(random.NextDouble() < 0.5 ? Side.Player : Side.Bot);
        fouls = 0;

        for (shots = 0; shots < MaxShotsPerMatch; shots++)
        {
            BotDecision decision = brain.ChooseShot(state, balls, random);
            if (decision.PlaceCueBall)
            {
                balls[0].Pos = decision.CueBallPosition;
                balls[0].Vel = balls[0].Roll = Vector2.zero;
                balls[0].Pocketed = false;
            }

            BotSettings shooter = state.Turn == Side.Player ? player : bot;
            ShotAccuracy.Outcome release = BotBrain.Release(shooter, random, out _);
            Vector2 direction = ShotAccuracy.Rotate(decision.Direction, release.AngleErrorDeg);
            float speed = Mathf.Min(decision.Speed * release.PowerFactor, shooter.MaxSpeed);

            physics.SetBalls(balls);
            ShotResult result = physics.Simulate(new ShotParams(direction, speed), 30f);
            RulesOutcome outcome = EightBallRules.Evaluate(state, result);
            System.Array.Copy(physics.Balls, balls, balls.Length);
            if (outcome.Foul != FoulType.None)
                fouls++;

            if (outcome.RespotEight)
            {
                balls[8].Pocketed = false;
                balls[8].Vel = balls[8].Roll = Vector2.zero;
                balls[8].Pos = RackBuilder.SpotPosition(balls, table, 8);
            }

            state = outcome.Next;
            if (state.IsOver)
            {
                shots++;
                return state.Winner;
            }
        }
        return null;
    }
}
