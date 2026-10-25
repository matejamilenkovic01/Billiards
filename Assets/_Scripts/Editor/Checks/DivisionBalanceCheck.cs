using UnityEditor;
using UnityEngine;

// Win rates per ranked division (v3 V10): a simulated player with the division's expected cue (good, average and
// weak release timing) against the division's bot (its BotProfile and cue). The player chooses shots like the base
// BotProfile (the best shot choice): a human sees the table at least as well as the bot. With cueVariants, the
// average player also plays one cue better and one worse than expected.
// strict = false: everything is reported. strict = true (user decision 2026-10-24, like BotBalanceCheck in commit 2:
// with wide release windows the timing alone separates good from average little in the upper divisions): the
// average player must sit on the division's target +- 6 p.p. and the order must hold (good >= average - 5 p.p.,
// average >= weak + 5 p.p.; in Diamond good and average are about equal, the champion cue's window leaves timing
// little to decide — user decision 2026-10-25); the good / weak targets and the cue steps stay in the report.
public static class DivisionBalanceCheck
{
    private const float Tolerance = 0.06f;

    // v3 V10 targets: average ~, good >=, weak ~ (per division, Bronze first).
    private static readonly float[] AverageTarget = { 0.62f, 0.60f, 0.58f, 0.56f, 0.55f };
    private static readonly float[] GoodMin = { 0.75f, 0.72f, 0.70f, 0.68f, 0.66f };
    private static readonly float[] WeakTarget = { 0.48f, 0.45f, 0.42f, 0.40f, 0.38f };

    public static string Run(int matches = 200, bool strict = false, bool cueVariants = true)
    {
        var report = new CheckReport("DivisionBalanceCheck");
        MatchConfig config = PhysicsChecks.Config;
        var divisions = AssetDatabase.LoadAssetAtPath<DivisionCatalog>(DataAssetBuilder.DivisionCatalogPath);
        var cues = AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath);
        var baseProfile = AssetDatabase.LoadAssetAtPath<BotProfile>(DataAssetBuilder.BotProfilePath);
        PoolSettings pool = config.ToPoolSettings();
        var table = new TableGeometry(pool);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        report.Info($"{matches} matches per row; player shot choice = base BotProfile; {(strict ? "strict" : "first pass: targets reported only")}");

        for (int d = 0; d < divisions.Count; d++)
        {
            DivisionDefinition division = divisions.At(d);
            BotSettings bot = division.Bot.ToBotSettings(config);
            int cueIndex = cues.Cues.FindIndex(c => c.Id == division.BotCueId);
            int seed = 10000 * (d + 1);

            float good = WinRate(table, pool, bot, Player(baseProfile, config, cues.Cues[cueIndex], BotBalanceCheck.GoodSigmaMs), matches, seed, out string goodInfo);
            float average = WinRate(table, pool, bot, Player(baseProfile, config, cues.Cues[cueIndex], BotBalanceCheck.AverageSigmaMs), matches, seed + 1000, out string averageInfo);
            float weak = WinRate(table, pool, bot, Player(baseProfile, config, cues.Cues[cueIndex], BotBalanceCheck.BadSigmaMs), matches, seed + 2000, out string weakInfo);
            report.Info($"{division.Name} (bot {bot.TimingSigmaMs:F0} ms, {division.BotCueId}, cut {bot.MaxCutAngle:F0}, {bot.CandidatesToSimulate} cand.): " +
                        $"good {good:P0} (>= {GoodMin[d]:P0}), average {average:P0} (~{AverageTarget[d]:P0}), weak {weak:P0} (~{WeakTarget[d]:P0})");
            report.Info($"  good {goodInfo}; average {averageInfo}; weak {weakInfo}");
            Gate(report, strict, Mathf.Abs(average - AverageTarget[d]) <= Tolerance, $"{division.Name}: average player {AverageTarget[d]:P0} +- 6 p.p.");
            Gate(report, strict, good >= average - 0.05f && average >= weak + 0.05f, $"{division.Name}: order good >= average - 5 p.p., average >= weak + 5 p.p.");
            Gate(report, false, good >= GoodMin[d] - Tolerance, $"{division.Name}: good player >= {GoodMin[d]:P0} (-6 p.p.)");
            Gate(report, false, Mathf.Abs(weak - WeakTarget[d]) <= Tolerance, $"{division.Name}: weak player {WeakTarget[d]:P0} +- 6 p.p.");

            if (!cueVariants)
                continue;
            string variants = "";
            if (cueIndex + 1 < cues.Cues.Count)
            {
                float better = WinRate(table, pool, bot, Player(baseProfile, config, cues.Cues[cueIndex + 1], BotBalanceCheck.AverageSigmaMs), matches, seed + 3000, out _);
                variants += $"one cue better {better:P0} ({better - average:+0.0%;-0.0%})  ";
                Gate(report, false, better - average >= 0.06f - 0.02f && better - average <= 0.10f + 0.02f, $"{division.Name}: one cue better +6..+10 p.p.");
            }
            if (cueIndex > 0)
            {
                float worse = WinRate(table, pool, bot, Player(baseProfile, config, cues.Cues[cueIndex - 1], BotBalanceCheck.AverageSigmaMs), matches, seed + 4000, out _);
                variants += $"one cue worse {worse:P0} ({worse - average:+0.0%;-0.0%})";
                Gate(report, false, average - worse >= 0.06f - 0.02f && average - worse <= 0.10f + 0.02f, $"{division.Name}: one cue worse -6..-10 p.p.");
            }
            if (variants.Length > 0)
                report.Info($"  average player, {variants}");
        }
        report.Info($"{watch.Elapsed.TotalSeconds:F0}s");
        return report.ToString();
    }

    // Tuning aid: one division with the bot's timing set to `botSigmaMs` (0 = the asset's), good / average / weak.
    public static string Probe(string divisionId, float botSigmaMs = 0f, int matches = 200)
    {
        MatchConfig config = PhysicsChecks.Config;
        var divisions = AssetDatabase.LoadAssetAtPath<DivisionCatalog>(DataAssetBuilder.DivisionCatalogPath);
        var cues = AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath);
        var baseProfile = AssetDatabase.LoadAssetAtPath<BotProfile>(DataAssetBuilder.BotProfilePath);
        PoolSettings pool = config.ToPoolSettings();
        var table = new TableGeometry(pool);
        DivisionDefinition division = divisions.Get(divisionId);
        BotSettings bot = division.Bot.ToBotSettings(config);
        if (botSigmaMs > 0f)
            bot.TimingSigmaMs = botSigmaMs;
        CueDefinition cue = cues.Get(division.BotCueId);
        int seed = 10000 * (divisions.IndexOf(divisionId) + 1);
        float good = WinRate(table, pool, bot, Player(baseProfile, config, cue, BotBalanceCheck.GoodSigmaMs), matches, seed, out _);
        float average = WinRate(table, pool, bot, Player(baseProfile, config, cue, BotBalanceCheck.AverageSigmaMs), matches, seed + 1000, out _);
        float weak = WinRate(table, pool, bot, Player(baseProfile, config, cue, BotBalanceCheck.BadSigmaMs), matches, seed + 2000, out _);
        return $"{division.Name} bot {bot.TimingSigmaMs:F0} ms: good {good:P1}, average {average:P1}, weak {weak:P1}";
    }

    private static BotSettings Player(BotProfile shotChoice, MatchConfig config, CueDefinition cue, float sigmaMs)
    {
        BotSettings player = shotChoice.ToBotSettings(config);
        player.TimingSigmaMs = sigmaMs;
        player.Accuracy = config.AccuracySettings(cue.GreenHalf, cue.YellowWidth, cue.OscillationPeriod);
        player.Power = config.PowerCurve(cue.MaxSpeed);
        return player;
    }

    private static float WinRate(TableGeometry table, PoolSettings pool, BotSettings bot, BotSettings player, int matches, int seed, out string info) =>
        BotBalanceCheck.WinRate(table, pool, bot, player, matches, seed, out info);

    private static void Gate(CheckReport report, bool strict, bool ok, string text)
    {
        if (strict)
            report.Check(ok, text);
        else
            report.Info($"  {(ok ? "in target" : "OFF target")}: {text}");
    }
}
