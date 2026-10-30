using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// v3 V12 pace: Monte Carlo over the real ladder, coins, objectives and cue prices. Win chances per division and
// player (good / average / weak timing, the division's expected cue) and the per-match stats the objectives read
// come from headless matches (BotBalanceCheck.PlayMatch with MatchStatsRecorder); each player then plays up to
// MaxMatches, buys the next cue as soon as it can, claims objectives at once and never uses rewarded.
// Win chances near 50% make the pace very sensitive to them, hence 500 matches per row by default (~13 min).
// Assumptions (no data yet): a cue one level better or worse than the division's moves the win chance by
// CuePerLevel; the player uses FINE AIM on FineAimShare of shots.
public static class ProgressionSim
{
    private const int MaxMatches = 250;
    private const float CuePerLevel = 0.07f;
    private const float FineAimShare = 0.3f;

    private static readonly string[] Types = { "average", "good", "weak" };
    private static readonly float[] Sigmas = { BotBalanceCheck.AverageSigmaMs, BotBalanceCheck.GoodSigmaMs, BotBalanceCheck.BadSigmaMs };

    // Targets for the average player (median matches), v3 V12.
    private static readonly (string name, int min, int max)[] Targets =
    {
        ("Silver checkpoint", 6, 9), ("Club Cue", 5, 9), ("Gold checkpoint", 20, 28), ("Pro Cue", 18, 28),
        ("Platinum checkpoint", 40, 50), ("Master Cue", 40, 55), ("Diamond checkpoint", 70, 90)
    };
    private static readonly string[] Milestones =
    {
        "Silver checkpoint", "Gold checkpoint", "Platinum checkpoint", "Diamond checkpoint", "Grand Champion",
        "Club Cue", "Pro Cue", "Master Cue", "Champion Cue", "Legend Cue"
    };

    public static string Run(int players = 2000, int matchesPerRow = 500, bool strict = false)
    {
        var report = new CheckReport("ProgressionSim");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        MatchConfig config = PhysicsChecks.Config;
        var divisions = AssetDatabase.LoadAssetAtPath<DivisionCatalog>(DataAssetBuilder.DivisionCatalogPath);
        var cues = AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath);
        var objectives = AssetDatabase.LoadAssetAtPath<ObjectiveCatalog>(DataAssetBuilder.ObjectiveCatalogPath);
        var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(DataAssetBuilder.EconomyConfigPath);
        var baseProfile = AssetDatabase.LoadAssetAtPath<BotProfile>(DataAssetBuilder.BotProfilePath);

        // Win chances and stats pools from headless matches.
        var win = new float[divisions.Count, Types.Length];
        var pools = new List<MatchStats>[divisions.Count, Types.Length, 2];
        PoolSettings pool = config.ToPoolSettings();
        var table = new TableGeometry(pool);
        for (int d = 0; d < divisions.Count; d++)
        {
            DivisionDefinition division = divisions.At(d);
            BotSettings bot = division.Bot.ToBotSettings(config);
            CueDefinition cue = cues.Get(division.BotCueId);
            for (int t = 0; t < Types.Length; t++)
            {
                BotSettings player = baseProfile.ToBotSettings(config);
                player.TimingSigmaMs = Sigmas[t];
                player.Accuracy = config.AccuracySettings(cue.GreenHalf, cue.YellowWidth, cue.OscillationPeriod);
                player.Power = config.PowerCurve(cue.MaxSpeed);
                pools[d, t, 0] = new List<MatchStats>();
                pools[d, t, 1] = new List<MatchStats>();
                var results = new (Side? winner, MatchStats stats)[matchesPerRow];
                System.Threading.Tasks.Parallel.For(0, matchesPerRow, i =>
                {
                    var recorder = new MatchStatsRecorder();
                    Side? winner = BotBalanceCheck.PlayMatch(50000 + d * 1000 + t * 300 + i, table, pool, bot, player, out _, out _, recorder, FineAimShare, config.BankIgnoreBeforeDrop);
                    results[i] = (winner, recorder.Stats);
                });
                int wins = 0, decided = 0;
                foreach (var (winner, stats) in results)
                {
                    if (winner == null)
                        continue;
                    decided++;
                    bool won = winner == Side.Player;
                    if (won)
                        wins++;
                    pools[d, t, won ? 1 : 0].Add(stats);
                }
                win[d, t] = decided > 0 ? wins / (float)decided : 0.5f;
            }
            report.Info($"{division.Name}: win average {win[d, 0]:P0}, good {win[d, 1]:P0}, weak {win[d, 2]:P0} ({matchesPerRow} matches each)");
        }

        // The players.
        var expectedCue = new int[divisions.Count];
        for (int d = 0; d < divisions.Count; d++)
            expectedCue[d] = cues.Cues.FindIndex(c => c.Id == divisions.At(d).BotCueId);
        var medians = new Dictionary<string, float>[Types.Length];
        for (int t = 0; t < Types.Length; t++)
        {
            var reached = Milestones.ToDictionary(m => m, _ => new List<int>());
            var objectiveDone = new Dictionary<string, int>();
            var random = new System.Random(777 + t);
            for (int p = 0; p < players; p++)
                PlayCareer(random, t, divisions, cues, objectives, economy, win, pools, expectedCue, reached, objectiveDone);

            medians[t] = new Dictionary<string, float>();
            var line = new StringBuilder($"{Types[t]} player, median matches: ");
            foreach (string milestone in Milestones)
            {
                List<int> list = reached[milestone];
                float share = list.Count / (float)players;
                float median = share >= 0.5f ? Percentile(list, players, 0.5f) : float.PositiveInfinity;
                medians[t][milestone] = median;
                line.Append($"{milestone} {(float.IsInfinity(median) ? $">{MaxMatches}" : median.ToString("F0"))}; ");
            }
            report.Info(line.ToString().TrimEnd(' ', ';'));
            if (t == 0)
            {
                var rates = new StringBuilder("objectives done within 100 matches (average): ");
                foreach (ObjectiveDefinition objective in objectives.All)
                    rates.Append($"{objective.Id} {(objectiveDone.TryGetValue(objective.Id, out int done) ? done : 0) / (float)players:P0}, ");
                report.Info(rates.ToString().TrimEnd(' ', ','));
            }
        }

        foreach (var (name, min, max) in Targets)
        {
            float median = medians[0][name];
            Gate(report, strict, median >= min && median <= max, $"average: {name} at {median:F0} matches (target {min}-{max})");
        }
        float goodGold = medians[1]["Gold checkpoint"], averageGold = medians[0]["Gold checkpoint"];
        Gate(report, strict, goodGold <= averageGold * 0.8f, $"good player faster: Gold at {goodGold:F0} vs {averageGold:F0}");
        float weakGold = medians[2]["Gold checkpoint"];
        // Reported only (user decision 2026-10-25): helping the weak player in Silver would push the average one
        // under the Gold target.
        Gate(report, false, weakGold <= 28f, $"weak player reaches Gold by ~day 5 (28 matches): {weakGold:F0}");
        report.Info($"{players} players per type, {watch.Elapsed.TotalSeconds:F0}s");
        return report.ToString();
    }

    private static void PlayCareer(System.Random random, int type, DivisionCatalog divisions, CueCatalog cues, ObjectiveCatalog objectives,
        EconomyConfig economy, float[,] win, List<MatchStats>[,,] pools, int[] expectedCue, Dictionary<string, List<int>> reached, Dictionary<string, int> objectiveDone)
    {
        var rank = new RankState();
        int coins = economy.StartCoins;
        int best = 0;
        int streak = 0;
        // v4 V12: every objective of an open set moves; a finished one is claimed at once (a set's bonus with the last).
        var progress = new Dictionary<string, int>();
        var claimed = new HashSet<string>();
        int rewardStage = 0;
        int championPaid = 0;
        var hit = new HashSet<string>();

        for (int match = 1; match <= MaxMatches; match++)
        {
            int d = rank.IsChampion ? divisions.Count - 1 : rank.Division;
            float chance = Mathf.Clamp(win[d, type] + CuePerLevel * (best - expectedCue[d]), 0.03f, 0.97f);
            bool won = random.NextDouble() < chance;
            List<MatchStats> pool = pools[d, type, won ? 1 : 0];
            MatchStats stats = pool.Count > 0 ? pool[random.Next(pool.Count)] : new MatchStats();
            stats.Won = won;

            streak = won ? streak + 1 : 0;
            RankState before = rank;
            rank = RankLadder.Apply(divisions, rank, won, streak).After;
            coins += won ? divisions.WinCoinsFor(before) : divisions.LossCoinsFor(before);
            for (int stage = rewardStage + 1; stage <= rank.StageIndex; stage++)
                coins += divisions.RewardForStage(stage, out _);
            rewardStage = Mathf.Max(rewardStage, rank.StageIndex);
            if (rank.IsChampion && divisions.ChampionMilestoneStars > 0)
            {
                int due = rank.ChampionStars / divisions.ChampionMilestoneStars;
                coins += Mathf.Max(0, due - championPaid) * divisions.ChampionMilestoneReward;
                championPaid = Mathf.Max(championPaid, due);
            }

            int open = Mathf.Min(objectives.Sets.Count, Mathf.Max(rewardStage, 0) / DivisionCatalog.StagesPerDivision + 1);
            for (int set = 0; set < open; set++)
            {
                ObjectiveSet objectiveSet = objectives.Sets[set];
                foreach (ObjectiveDefinition objective in objectiveSet.Objectives)
                {
                    if (claimed.Contains(objective.Id))
                        continue;
                    progress.TryGetValue(objective.Id, out int value);
                    value = ObjectiveRules.Advance(objective, value, stats, before, rank, streak);
                    progress[objective.Id] = value;
                    if (value < objective.Target)
                        continue;
                    claimed.Add(objective.Id);
                    coins += objective.Reward;
                    if (match <= 100)
                        objectiveDone[objective.Id] = objectiveDone.TryGetValue(objective.Id, out int n) ? n + 1 : 1;
                    if (objectiveSet.Objectives.TrueForAll(o => claimed.Contains(o.Id)))
                        coins += objectiveSet.Bonus;
                }
            }

            while (best + 1 < cues.Cues.Count && coins >= cues.Cues[best + 1].Price)
            {
                coins -= cues.Cues[best + 1].Price;
                best++;
            }

            Mark(hit, reached, "Silver checkpoint", rank.Division >= 1 || rank.IsChampion, match);
            Mark(hit, reached, "Gold checkpoint", rank.Division >= 2 || rank.IsChampion, match);
            Mark(hit, reached, "Platinum checkpoint", rank.Division >= 3 || rank.IsChampion, match);
            Mark(hit, reached, "Diamond checkpoint", rank.Division >= 4 || rank.IsChampion, match);
            Mark(hit, reached, "Grand Champion", rank.IsChampion, match);
            Mark(hit, reached, "Club Cue", best >= 1, match);
            Mark(hit, reached, "Pro Cue", best >= 2, match);
            Mark(hit, reached, "Master Cue", best >= 3, match);
            Mark(hit, reached, "Champion Cue", best >= 4, match);
            Mark(hit, reached, "Legend Cue", best >= 5, match);
        }
    }

    private static void Mark(HashSet<string> hit, Dictionary<string, List<int>> reached, string milestone, bool now, int match)
    {
        if (now && hit.Add(milestone))
            reached[milestone].Add(match);
    }

    // The p-th percentile over all players, counting those who never got there as "after the end".
    private static float Percentile(List<int> values, int players, float p)
    {
        values.Sort();
        int index = Mathf.Clamp(Mathf.CeilToInt(p * players) - 1, 0, values.Count - 1);
        return values[index];
    }

    private static void Gate(CheckReport report, bool strict, bool ok, string text)
    {
        if (strict)
            report.Check(ok, text);
        else
            report.Info($"  {(ok ? "in target" : "OFF target")}: {text}");
    }
}
