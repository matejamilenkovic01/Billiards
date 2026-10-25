using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Headless matches: the bot against a "simulated player" that uses the same BotBrain and cue and only differs in
// its release timing (v3 V8: N(0, sigma) off the centre of green). Good timing must win, bad timing must lose.
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

        float good = WinRate(table, pool, bot, WithTiming(bot, GoodSigmaMs), matchesPerProfile, 1000, out string goodInfo);
        float bad = WinRate(table, pool, bot, WithTiming(bot, BadSigmaMs), matchesPerProfile, 2000, out string badInfo);
        float same = WinRate(table, pool, bot, bot, matchesPerProfile, 3000, out string sameInfo);

        report.Info($"bot timing sigma {bot.TimingSigmaMs:F0} ms, {matchesPerProfile} matches per profile, {watch.Elapsed.TotalSeconds:F0}s");
        report.Info($"good player ({GoodSigmaMs:F0} ms) wins {good:P1} {goodInfo}");
        report.Info($"bad player ({BadSigmaMs:F0} ms) wins {bad:P1} {badInfo}");
        report.Info($"same as bot ({bot.TimingSigmaMs:F0} ms) wins {same:P1} {sameInfo}");
        // v3 V8 ranges (user decision 2026-10-20): with timed releases a weak player rarely lets go in red at
        // normal powers, so skill separates less than with the old zone odds; the per-division balance is the real
        // gate (DivisionBalanceCheck).
        report.Check(good >= 0.55f, "good player wins >= 55%");
        report.Check(bad <= 0.42f, "bad player wins <= 42%");
        report.Check(Mathf.Abs(same - 0.5f) <= 0.08f, "same timing as the bot wins 50% +- 8%");
        report.Check(good > same && same > bad, "good > same > bad");
        return report.ToString();
    }

    // Simulated players (v3 V8): good, average and weak release timing.
    public const float GoodSigmaMs = 30f;
    public const float AverageSigmaMs = 45f;
    public const float BadSigmaMs = 65f;

    public static BotSettings WithTiming(BotSettings settings, float sigmaMs)
    {
        settings.TimingSigmaMs = sigmaMs;
        return settings;
    }

    public static float WinRate(TableGeometry table, PoolSettings pool, BotSettings bot, BotSettings player, int matches, int seedBase, out string info)
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
    public static Side? PlayMatch(int seed, TableGeometry table, PoolSettings pool, BotSettings bot, BotSettings player, out int shots, out int fouls) =>
        PlayMatch(seed, table, pool, bot, player, out shots, out fouls, null, 0f, 0f);

    // The same, optionally recording the player side's stats (ProgressionSim; `fineAimShare` of their shots count as
    // FINE AIM) and every shot (MatchLengthCheck).
    public static Side? PlayMatch(int seed, TableGeometry table, PoolSettings pool, BotSettings bot, BotSettings player, out int shots, out int fouls,
        MatchStatsRecorder recorder, float fineAimShare, float bankIgnoreBeforeDrop, List<MatchLengthCheck.ShotLog> log = null,
        RuleSetId rules = RuleSetId.Standard)
    {
        var random = new System.Random(seed);
        var physics = new PoolPhysics(table, pool) { RecordCollisions = recorder != null, TrackRotation = false };
        recorder?.Reset();
        // Each side chooses with its own shot-choice settings (the same here; DivisionBalanceCheck differs).
        var botBrain = new BotBrain(table, pool, bot);
        var playerBrain = new BotBrain(table, pool, player);
        BallState[] balls = RackBuilder.Build(random, table, pool.RackGap);
        RulesState state = RulesState.NewRack(random.NextDouble() < 0.5 ? Side.Player : Side.Bot, rules);
        fouls = 0;

        for (shots = 0; shots < MaxShotsPerMatch; shots++)
        {
            // v4 V4: on the shooter's first turn on the 8 the other side blocks / picks its pocket.
            if (state.NeedsOpponentEightChoice(state.Turn))
                state.EightPocket[(int)state.Turn] = (state.Turn == Side.Player ? botBrain : playerBrain)
                    .ChooseEightPocketForOpponent(state, balls, state.Turn, random);
            BotDecision decision = (state.Turn == Side.Player ? playerBrain : botBrain).ChooseShot(state, balls, random);
            if (state.NeedsCall(state.Turn))
                state.CalledPocket = decision.TargetPocket;
            if (decision.PlaceCueBall)
            {
                balls[0].Pos = decision.CueBallPosition;
                balls[0].Stop();
                balls[0].Pocketed = false;
            }

            BotSettings shooter = state.Turn == Side.Player ? player : bot;
            float pull = Mathf.Clamp01(shooter.Power.PullFromSpeed(decision.Speed));
            ShotAccuracy.Outcome release = BotBrain.Release(shooter, pull, random, out _);
            Vector2 direction = ShotAccuracy.Rotate(decision.Direction, release.AngleErrorDeg);
            float speed = Mathf.Min(decision.Speed * release.PowerFactor, shooter.Power.MaxSpeed);

            bool byPlayer = state.Turn == Side.Player;
            if (recorder != null && byPlayer)
                recorder.ShotStart(balls, direction, release.Zone, random.NextDouble() < fineAimShare);
            physics.SetBalls(balls);
            ShotResult result = physics.Simulate(new ShotParams(direction, speed), 30f);
            RulesOutcome outcome = EightBallRules.Evaluate(state, result);
            if (recorder != null && byPlayer)
                recorder.ShotEnd(state, outcome, result, table, bankIgnoreBeforeDrop);
            log?.Add(new MatchLengthCheck.ShotLog { ByPlayer = byPlayer, Rolling = result.Duration, Foul = outcome.Foul != FoulType.None, GroupsAssigned = outcome.GroupsAssigned });
            System.Array.Copy(physics.Balls, balls, balls.Length);
            if (outcome.Foul != FoulType.None)
                fouls++;

            if (outcome.RespotEight)
            {
                balls[8].Pocketed = false;
                balls[8].Stop();
                balls[8].Pos = RackBuilder.SpotPosition(balls, table, 8);
            }

            state = outcome.Next;
            if (state.IsOver)
            {
                shots++;
                recorder?.Finish(state.Winner == Side.Player, state);
                return state.Winner;
            }
        }
        return null;
    }
}
