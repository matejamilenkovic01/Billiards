using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// v3 V10: a ranked match takes a median of 6 minutes or less. Headless matches (the average player against each
// division's bot) give the shots, the fouls and the time the balls roll; the timeline adds the VS intro and coin
// toss, the bot's turn (its profile's strike time plus the characters' mean extra thinking), the player's turn
// (PlayerTurnSeconds, an assumption until there is play data), the resolve pause, the banners and the end popup.
public static class MatchLengthCheck
{
    private const float PlayerTurnSeconds = 9f;
    // v4 V5 assumptions: calling a pocket adds this to the player's turn; blocking / picking for the bot this.
    private const float CallSeconds = 1.5f;
    private const float ChoiceSeconds = 3f;

    public struct ShotLog
    {
        public bool ByPlayer;
        public float Rolling;
        public bool Foul;
        public bool GroupsAssigned;
        public bool Called;
        public bool PlayerChose;
    }

    public static string Run(int matches = 100)
    {
        var report = new CheckReport("MatchLengthCheck");
        MatchConfig config = PhysicsChecks.Config;
        var divisions = AssetDatabase.LoadAssetAtPath<DivisionCatalog>(DataAssetBuilder.DivisionCatalogPath);
        var cues = AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath);
        var baseProfile = AssetDatabase.LoadAssetAtPath<BotProfile>(DataAssetBuilder.BotProfilePath);
        PoolSettings pool = config.ToPoolSettings();
        var table = new TableGeometry(pool);
        var minutes = new List<float>();
        int perDivision = Mathf.Max(1, matches / divisions.Count);
        for (int d = 0; d < divisions.Count; d++)
        {
            DivisionDefinition division = divisions.At(d);
            BotSettings bot = division.Bot.ToBotSettings(config);
            CueDefinition cue = cues.Get(division.BotCueId);
            BotSettings player = baseProfile.ToBotSettings(config);
            player.TimingSigmaMs = BotBalanceCheck.AverageSigmaMs;
            player.Accuracy = config.AccuracySettings(cue.GreenHalf, cue.YellowWidth, cue.OscillationPeriod);
            player.Power = config.PowerCurve(cue.MaxSpeed);
            float thinkExtra = 0f;
            foreach (OpponentDefinition opponent in division.Opponents)
                thinkExtra += opponent.ThinkExtra / division.Opponents.Count;
            float botTurn = division.Bot.StrikeAt + thinkExtra;
            var divisionMinutes = new List<float>();
            var callSeconds = new List<float>();
            for (int i = 0; i < perDivision; i++)
            {
                var log = new List<ShotLog>();
                Side? winner = BotBalanceCheck.PlayMatch(90000 + d * 1000 + i, table, pool, bot, player, out _, out _, null, 0f, 0f, log, division.Rules);
                if (winner == null)
                    continue;
                float seconds = config.VsIntroDuration + config.CoinFlipDuration + config.EndPopupDelay;
                float extra = 0f;
                foreach (ShotLog shot in log)
                {
                    if (shot.ByPlayer && shot.Called)
                        extra += CallSeconds;
                    if (shot.PlayerChose)
                        extra += ChoiceSeconds;
                    seconds += (shot.ByPlayer ? PlayerTurnSeconds : botTurn) + shot.Rolling + config.ResolvePause;
                    if (shot.Foul)
                        seconds += config.FoulBannerDuration;
                    if (shot.GroupsAssigned)
                        seconds += config.GroupBannerDuration;
                }
                divisionMinutes.Add((seconds + extra) / 60f);
                callSeconds.Add(extra);
            }
            minutes.AddRange(divisionMinutes);
            float divisionMedian = Median(divisionMinutes);
            report.Info($"{division.Name} ({division.Rules}): median {divisionMedian:F1} min (bot turn {botTurn:F1} s; calls and choices add a median {Median(callSeconds):F0} s per match)");
            report.Check(divisionMedian <= 6f, $"{division.Name}: median match {divisionMedian:F1} min <= 6");
        }
        float median = Median(minutes);
        report.Info($"all: median {median:F1} min over {minutes.Count} matches (player turn assumed {PlayerTurnSeconds:F0} s)");
        report.Check(median <= 6f, $"median match {median:F1} min <= 6");
        return report.ToString();
    }

    private static float Median(List<float> values)
    {
        if (values.Count == 0)
            return 0f;
        values.Sort();
        return values[values.Count / 2];
    }
}
