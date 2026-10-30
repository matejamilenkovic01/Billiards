using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Runs ProfileService v3 (v3 V10-V12, v4 V11/V12/V14) against a scratch profile in Edit mode, with the real catalogs; the real
// PlayerPrefs profile is restored afterwards.
public static class EconomyChecks
{
    public static string All()
    {
        var report = new CheckReport("EconomyChecks");
        var go = new GameObject("EconomyChecks_ProfileService") { hideFlags = HideFlags.HideAndDontSave };
        ProfileService profile = Create(go);
        Dictionary<string, object> backup = Backup(profile);

        try
        {
            Run(profile, report);
        }
        finally
        {
            Restore(profile, backup);
            Object.DestroyImmediate(go);
        }

        return report.ToString();
    }

    private static ProfileService Create(GameObject go)
    {
        ProfileService profile = go.AddComponent<ProfileService>();
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("_catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath);
        serialized.FindProperty("_divisions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DivisionCatalog>(DataAssetBuilder.DivisionCatalogPath);
        serialized.FindProperty("_objectives").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ObjectiveCatalog>(DataAssetBuilder.ObjectiveCatalogPath);
        serialized.FindProperty("_economy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EconomyConfig>(DataAssetBuilder.EconomyConfigPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    private static void Run(ProfileService profile, CheckReport report)
    {
        DivisionCatalog divisions = profile.Divisions;
        CueCatalog cues = profile.Catalog;
        int bronze = divisions.IndexOf("bronze"), silver = divisions.IndexOf("silver"), gold = divisions.IndexOf("gold");
        int diamond = divisions.IndexOf("diamond");
        report.Check(divisions.Count == 5 && bronze == 0 && diamond == 4, $"5 divisions, Bronze first (got {divisions.Count})");
        report.Check(cues.Cues.Count == 6 && cues.Starter.Id == "house", "6 cues, House is the starter");
        Catalog(profile, report);
        Migration(profile, report);

        // A fresh start.
        profile.ResetProfile();
        report.Check(profile.Coins == 0, "start coins 0");
        report.Check(profile.SelectedCueId == "house" && profile.IsOwned(cues.Get("house")), "House owned and selected");
        RankState rank = profile.Rank;
        report.Check(rank.Division == bronze && rank.Stage == 0 && rank.Stars == 0 && !rank.IsChampion, "start at Bronze III, 0 stars");
        report.Check(RankLadder.Label(divisions, rank) == "BRONZE III", $"label BRONZE III ({RankLadder.Label(divisions, rank)})");

        // Bronze: a win +1 star and its coins; 2 stars fill a stage; a loss takes nothing and pays the consolation.
        MatchOutcome outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.Stars == 1 && outcome.Rank.StarsGained == 1, "win +1 star");
        report.Check(outcome.CoinsFromMatch == divisions.At(bronze).WinCoins && profile.Coins == 20, $"Bronze win +20 coins (coins {profile.Coins})");
        outcome = profile.ApplyMatchResult(false, new MatchStats());
        report.Check(profile.Rank.Stars == 1 && outcome.Rank.LossCostNothing, "Bronze loss keeps the star");
        report.Check(outcome.CoinsFromMatch == divisions.At(bronze).LossCoins && profile.Coins == 25, $"loss +5 coins (coins {profile.Coins})");
        report.Check(profile.WinStreak == 0 && profile.LossStreak == 1, "loss resets the win streak");
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.Stage == 1 && profile.Rank.Stars == 0 && outcome.Rank.StageUp, "2 stars: Bronze II");
        report.Check(outcome.StageRewardCoins == 50 && outcome.DivisionRewardCoins == 0 && profile.Coins == 25 + 20 + 50, $"Bronze II pays its stage reward (+{outcome.StageRewardCoins}, coins {profile.Coins})");

        // Hot streak: from the 3rd win in a row on, +1 more star.
        profile.ApplyMatchResult(true, new MatchStats());
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(outcome.Rank.StreakBonus && outcome.Rank.StarsGained == 2, "3rd win in a row gives 2 stars");
        report.Check(profile.Rank.Stage == 2 && profile.Rank.Stars == 1, $"stars carry over into the next stage (stage {profile.Rank.Stage}, stars {profile.Rank.Stars})");
        report.Check(outcome.StageRewardCoins == 50, "Bronze I pays its stage reward");

        // Bronze I 1 star + 2 (streak) = Silver III with 1 star carried: a checkpoint, its division reward.
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(outcome.Rank.NewDivision && profile.Rank.Division == silver && profile.Rank.Stage == 0 && profile.Rank.Stars == 1,
            $"Bronze I -> Silver III checkpoint with carry ({RankLadder.Label(divisions, profile.Rank)} {profile.Rank.Stars})");
        report.Check(outcome.DivisionRewardCoins == divisions.At(silver).DivisionReward && outcome.StageRewardCoins == 0, $"Silver pays its division reward ({outcome.DivisionRewardCoins})");
        report.Check(PlayerPrefs.GetInt(SaveKeys.RewardGrantedStage, 0) == 3, "reward stage = Silver III");
        report.Check(profile.CurrentDivision.Id == "silver" && profile.CurrentDivision.VenueId == "downtown", "Silver plays in Downtown Hall");

        // Silver: -1 star, the stage never drops; nothing below the checkpoint.
        SetRank(profile, silver, 1, 1);
        profile.ApplyMatchResult(false, new MatchStats());
        report.Check(Is(profile.Rank, silver, 1, 0), "Silver loss -1 star");
        outcome = profile.ApplyMatchResult(false, new MatchStats());
        report.Check(Is(profile.Rank, silver, 1, 0) && outcome.Rank.LossCostNothing, "Silver loss at 0 stars keeps the stage");
        SetRank(profile, silver, 0, 0);
        report.Check(!profile.LossWouldCost, "Silver III 0 stars: a loss costs nothing (checkpoint)");

        // Gold and up: at 0 stars a loss drops a stage, never below the division start.
        SetRank(profile, gold, 1, 0);
        report.Check(profile.LossWouldCost, "Gold II 0 stars: a loss costs");
        outcome = profile.ApplyMatchResult(false, new MatchStats());
        report.Check(Is(profile.Rank, gold, 0, divisions.At(gold).StarsPerStage - 1) && outcome.Rank.StageDown,
            $"Gold II 0 stars loss -> Gold III 2 stars ({RankLadder.Label(divisions, profile.Rank)} {profile.Rank.Stars})");
        SetRank(profile, gold, 0, 0);
        outcome = profile.ApplyMatchResult(false, new MatchStats());
        report.Check(Is(profile.Rank, gold, 0, 0) && outcome.Rank.LossCostNothing, "Gold III 0 stars: checkpoint holds");

        // Coins by division.
        SetRank(profile, gold, 0, 0);
        int before = profile.Coins;
        profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Coins - before == divisions.At(gold).WinCoins, $"Gold win +{divisions.At(gold).WinCoins} coins");

        // Diamond I -> Grand Champion, endless stars.
        SetRank(profile, diamond, 2, 2);
        before = profile.Coins;
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.IsChampion && profile.Rank.ChampionStars == 0 && outcome.Rank.NewDivision, "Diamond I full -> Grand Champion");
        report.Check(RankLadder.Label(divisions, profile.Rank) == "GRAND CHAMPION" && profile.Rank.StageIndex == 15, $"champion label and stage index 15 ({profile.Rank.StageIndex})");
        report.Check(outcome.DivisionRewardCoins == divisions.ChampionEntryReward && profile.Coins - before == divisions.At(diamond).WinCoins + divisions.ChampionEntryReward,
            $"Grand Champion pays {divisions.ChampionEntryReward} for the entry");
        before = profile.Coins;
        profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.ChampionStars == 1 && profile.Coins - before == divisions.ChampionWinCoins, "champion win +1 star, +100 coins");
        outcome = profile.ApplyMatchResult(false, new MatchStats());
        profile.ApplyMatchResult(false, new MatchStats());
        report.Check(profile.Rank.IsChampion && profile.Rank.ChampionStars == 0, "champion loss -1 star, never below 0");
        report.Check(outcome.CoinsFromMatch == divisions.ChampionLossCoins, "champion loss pays the champion loss coins");
        report.Check(profile.CurrentDivision.Id == "diamond", "Grand Champion plays Diamond's bots");

        Rewards(profile, report);
        LossCoins(profile, report);
        Sets(profile, report);
        Kinds(profile, report);
        CueGapCheck(profile, report);

        // Cues: buy with coins only, never auto-selected.
        profile.ResetProfile();
        CueDefinition club = cues.Get("club");
        PlayerPrefs.SetInt(SaveKeys.Coins, club.Price - 1);
        report.Check(!profile.TryBuy("club", out string reason) && reason.Contains("more coins"), "buy without enough coins rejected");
        report.Check(!profile.TrySelect("club", out _), "select not-owned cue rejected");
        PlayerPrefs.SetInt(SaveKeys.Coins, club.Price);
        report.Check(profile.TryBuy("club", out _) && profile.Coins == 0 && profile.IsOwned(club), "buy with exact coins spends the price");
        report.Check(profile.SelectedCueId == "house", "buying does not auto-select");
        report.Check(!profile.TryBuy("club", out _), "buying an owned cue rejected");
        report.Check(profile.TrySelect("club", out _) && profile.SelectedCueId == "club", "select owned cue");

        // Abandoned match = a loss with the usual star rule and the division's loss coins.
        SetRank(profile, silver, 1, 1);
        int lossesBefore = profile.Losses;
        before = profile.Coins;
        profile.SetPendingMatch(true);
        report.Check(profile.ApplyPendingMatch(), "pending match detected");
        report.Check(Is(profile.Rank, silver, 1, 0) && profile.Losses == lossesBefore + 1 && profile.Coins == before + divisions.At(silver).LossCoins,
            $"abandoned match counts as a loss (-1 star in Silver, +{divisions.At(silver).LossCoins} coins)");
        report.Check(PlayerPrefs.GetInt(SaveKeys.PendingMatch, 0) == 0, "pending flag cleared");
        report.Check(profile.ConsumeAbandonedNotice() && !profile.ConsumeAbandonedNotice(), "abandoned notice shown once");
        report.Check(!profile.ApplyPendingMatch(), "nothing without the pending flag");

        // Matches today (analytics, v4 V14): counted, reset the next day.
        System.Func<System.DateTime> clock = ProfileService.Clock;
        var now = new System.DateTime(2026, 10, 24, 12, 0, 0);
        ProfileService.Clock = () => now;
        try
        {
            profile.ResetProfile();
            profile.ApplyMatchResult(true, new MatchStats());
            profile.ApplyMatchResult(false, new MatchStats());
            report.Check(PlayerPrefs.GetInt(SaveKeys.MatchesToday, 0) == 2, "matches today counted");
            now = now.AddDays(1);
            profile.ApplyMatchResult(false, new MatchStats());
            report.Check(PlayerPrefs.GetInt(SaveKeys.MatchesToday, 0) == 1, "matches today start over the next day");
        }
        finally
        {
            ProfileService.Clock = clock;
        }

        // Snapshot: ratings from the data, the next cue, the recommended division.
        profile.ResetProfile();
        ProfileSnapshot snapshot = profile.GetSnapshot();
        report.Check(snapshot.Cues.Count == 6, "snapshot lists every cue");
        bool rising = true;
        for (int i = 1; i < snapshot.Cues.Count; i++)
            rising &= snapshot.Cues[i].Overall > snapshot.Cues[i - 1].Overall;
        report.Check(rising, "OVR grows with every cue");
        report.Check(Mathf.Abs(snapshot.Find("house").Overall - 44) <= 1 && Mathf.Abs(snapshot.Find("legend").Overall - 95) <= 1,
            $"OVR House ~44, Legend ~95 ({snapshot.Find("house").Overall}, {snapshot.Find("legend").Overall})");
        report.Check(snapshot.Find("pro").OverallVsSelected == snapshot.Find("pro").Overall - snapshot.Find("house").Overall, "+X vs your cue");
        report.Check(snapshot.Find("pro").RecommendedFor == "Gold" && snapshot.Find("house").RecommendedFor == "Bronze", "Pro recommended for Gold");
        report.Check(snapshot.NextCue != null && snapshot.NextCue.Id == "club", "next cue is Club");
        report.Check(snapshot.Rank.Label == "BRONZE III" && snapshot.Rank.StarsToNext == 2 && !snapshot.Rank.NextIsCheckpoint, "rank view: 2 stars to Bronze II");
        SetRank(profile, bronze, 2, 1);
        RankViewData view = profile.RankView(profile.Rank);
        report.Check(view.StarsToNext == 1 && view.NextIsCheckpoint && view.NextLabel == "SILVER", "rank view: 1 star to SILVER, checkpoint");

        // DIVISIONS: 5 blocks and Grand Champion, 3 stages each, five checkpoints, the player's place, rewards paid.
        DivisionsViewData divisionsView = profile.DivisionsView();
        report.Check(divisionsView.Blocks.Count == 6 && divisionsView.Blocks[5].IsChampion && divisionsView.Blocks.FindAll(b => b.Stages.Count == 3).Count == 5, "divisions: 5 blocks of 3 stages and Grand Champion");
        DivisionBlockData bronzeBlock = divisionsView.Blocks[0], silverBlock = divisionsView.Blocks[1];
        report.Check(divisionsView.CurrentIndex == 0 && bronzeBlock.State == RoadStageState.Current && silverBlock.State == RoadStageState.Ahead && silverBlock.LockText == "Reach BRONZE I",
            "divisions: Bronze is current, Silver locked with its condition");
        report.Check(bronzeBlock.Stages[2].State == RoadStageState.Current && bronzeBlock.Stages[2].Stars == 1 && bronzeBlock.Stages[1].State == RoadStageState.Done && bronzeBlock.Stages[1].Reward == 50 &&
                     silverBlock.EntryReward == 150 && silverBlock.RuleText == profile.Divisions.At(silver).RuleText && silverBlock.VenueName == "Downtown Hall" && silverBlock.BotCueName == "Club Cue",
            "divisions: Bronze I with 1 star, rewards, Silver's rule, venue and bot cue");
        report.Check(divisionsView.Blocks[5].EntryReward == profile.Divisions.ChampionEntryReward && divisionsView.Blocks[5].State == RoadStageState.Ahead, "divisions: Grand Champion's reward and lock");
        report.Check(!profile.GetSnapshot().Rank.IsNew, "no NEW chip in Bronze");
        SetRank(profile, silver, 0, 0);
        report.Check(profile.GetSnapshot().Rank.IsNew, "NEW chip on a division not looked at yet");
        profile.MarkDivisionsSeen();
        report.Check(!profile.GetSnapshot().Rank.IsNew, "looking at DIVISIONS clears the NEW chip");

        // ALL OBJECTIVES: the open sets and the next one, locked; opens on the player's division.
        ObjectivesViewData objectivesView = profile.ObjectivesView();
        report.Check(objectivesView.Sets.Count == 3 && objectivesView.CurrentTab == 1 && !objectivesView.Sets[1].Locked && objectivesView.Sets[2].Locked && objectivesView.Sets[2].LockText == "Reach GOLD to unlock",
            $"objectives view: Bronze and Silver open, Gold locked and visible ({objectivesView.Sets.Count} tabs)");
        report.Check(objectivesView.Sets[2].Objectives.Count == 6 && objectivesView.Sets[2].Objectives.TrueForAll(o => o.Locked && !o.Claimable) && objectivesView.Sets[1].Total == 6, "locked set: text and reward visible, nothing to claim");

        // The full-screen moment: a new division brings the rule, the venue and the reward.
        profile.ResetProfile();
        SetRank(profile, 0, 2, 1);
        MatchOutcome promoted = profile.ApplyMatchResult(true, new MatchStats());
        CelebrationData celebration = profile.CelebrationFor(promoted.Rank, promoted.DivisionRewardCoins);
        report.Check(celebration != null && celebration.Kind == CelebrationKind.DivisionUp && celebration.Title == "SILVER DIVISION" && celebration.Reward == 150 &&
                     celebration.Rules == RuleSetId.CallEight && celebration.VenueName == "Downtown Hall" && celebration.RuleText.Length > 0, "celebration: SILVER DIVISION with the new rule, venue and reward");
        report.Check(profile.CelebrationFor(new RankChange(), 0) == null, "no celebration without a new division");
    }

    // The data: five sets of six, at least three that pay in a loss and at most three that need a win, loss coins that grow.
    private static void Catalog(ProfileService profile, CheckReport report)
    {
        ObjectiveCatalog objectives = profile.Objectives;
        DivisionCatalog divisions = profile.Divisions;
        report.Check(objectives.Sets.Count == 5 && objectives.Count == 30, $"5 sets, 30 objectives (got {objectives.Sets.Count}, {objectives.Count})");
        var ids = new HashSet<string>();
        bool unique = true, aligned = true, enough = true;
        for (int d = 0; d < objectives.Sets.Count; d++)
        {
            ObjectiveSet set = objectives.Sets[d];
            aligned &= set.DivisionId == divisions.At(d).Id && set.Objectives.Count == 6 && set.Bonus > 0;
            int play = 0;
            foreach (ObjectiveDefinition objective in set.Objectives)
            {
                unique &= ids.Add(objective.Id);
                if (ObjectiveRules.CountsInLoss(objective.Kind))
                    play++;
            }
            enough &= play >= 3 && set.Objectives.Count - play <= 3;
        }
        report.Check(unique && aligned, "set ids match the divisions, 6 per set, ids unique, a bonus each");
        report.Check(enough, "every set has 3+ objectives that move in a loss and at most 3 that need a win");
        bool growing = true;
        for (int d = 1; d < divisions.Count; d++)
            growing &= divisions.At(d).LossCoins > divisions.At(d - 1).LossCoins && divisions.At(d).StageReward > divisions.At(d - 1).StageReward;
        report.Check(growing && divisions.ChampionLossCoins >= divisions.At(divisions.Count - 1).LossCoins, "loss coins and stage rewards grow with the division");
    }

    // Profile v2 -> v3 keeps coins, rank, cues and flags, drops the probes and slot objectives, opens the sets reached.
    private static void Migration(ProfileService profile, CheckReport report)
    {
        CueCatalog cues = profile.Catalog;
        int gold = profile.Divisions.IndexOf("gold");
        PlayerPrefs.SetInt(SaveKeys.ProfileVersion, 1);
        PlayerPrefs.SetInt(SaveKeys.Coins, 777);
        PlayerPrefs.SetInt(SaveKeys.CueOwned("club"), 1);
        PlayerPrefs.SetInt(SaveKeys.RulesSeen, 1);
        profile.Migrate();
        report.Check(PlayerPrefs.GetInt(SaveKeys.ProfileVersion, 0) == ProfileService.Version, "migration writes the profile version");
        report.Check(profile.Coins == 0 && !profile.IsOwned(cues.Get("club")), $"v1 starts fresh: 0 coins, no bought cues (coins {profile.Coins})");
        report.Check(profile.RulesSeen, "migration keeps one-time flags");
        report.Check(profile.UnlockedSets == 1 && Visible(profile) == "bronze_pocket_8,bronze_perfect_5,bronze_win", $"fresh: the Bronze set ({Visible(profile)})");

        // A v2 profile in Gold II with the probes' keys and slot objectives.
        profile.ResetProfile();
        PlayerPrefs.SetInt(SaveKeys.ProfileVersion, 2);
        PlayerPrefs.SetInt(SaveKeys.Coins, 612);
        PlayerPrefs.SetInt(SaveKeys.Wins, 14);
        PlayerPrefs.SetInt(SaveKeys.Losses, 9);
        PlayerPrefs.SetInt(SaveKeys.MatchesPlayed, 23);
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 2);
        PlayerPrefs.SetInt(SaveKeys.LossStreak, 0);
        profile.SetRank(new RankState { Division = gold, Stage = 1, Stars = 1 });
        PlayerPrefs.SetInt(SaveKeys.ChampionStars, 0);
        PlayerPrefs.SetInt(SaveKeys.CueOwned("club"), 1);
        PlayerPrefs.SetInt(SaveKeys.CueOwned("pro"), 1);
        PlayerPrefs.SetString(SaveKeys.SelectedCue, "pro");
        PlayerPrefs.SetInt(SaveKeys.TutorialDone, 1);
        PlayerPrefs.SetInt(SaveKeys.HintsShown, 0x35);
        PlayerPrefs.SetInt(SaveKeys.ShotsTaken, 61);
        foreach (string key in SaveKeys.Retired)
            PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.DeleteKey(SaveKeys.InstallDay);
        profile.Migrate();
        report.Check(PlayerPrefs.GetInt(SaveKeys.ProfileVersion, 0) == 3, "v2 -> v3: version 3");
        report.Check(profile.Coins == 612 && profile.Wins == 14 && profile.Losses == 9 && profile.MatchesPlayed == 23 && profile.WinStreak == 2,
            "v2 -> v3 keeps coins, wins, losses, matches and streaks");
        report.Check(Is(profile.Rank, gold, 1, 1) && PlayerPrefs.GetInt(SaveKeys.BestStageReached, 0) == gold * 3 + 1, "v2 -> v3 keeps the rank and the best stage");
        report.Check(profile.IsOwned(cues.Get("club")) && profile.IsOwned(cues.Get("pro")) && profile.SelectedCueId == "pro", "v2 -> v3 keeps the cues and the selection");
        report.Check(profile.TutorialDone && PlayerPrefs.GetInt(SaveKeys.HintsShown, 0) == 0x35 && profile.ShotsTaken == 61, "v2 -> v3 keeps tutorial, hints and shots");
        bool gone = true;
        foreach (string key in SaveKeys.Retired)
            gone &= !PlayerPrefs.HasKey(key);
        report.Check(gone, "v2 -> v3 drops the probes' keys and the slot objectives");
        report.Check(PlayerPrefs.GetInt(SaveKeys.RewardGrantedStage, -1) == gold * 3 + 1, "v2 -> v3: no reward is paid back (reward stage = best stage)");
        report.Check(profile.UnlockedSets == 3 && profile.ObjectiveProgress("bronze_win") == 0 && profile.ObjectiveProgress("gold_cut_45") == 0,
            "v2 -> v3: the Bronze, Silver and Gold sets open with progress 0");
        report.Check(PlayerPrefs.HasKey(SaveKeys.InstallDay), "install day set");
        profile.Migrate();
        report.Check(profile.Coins == 612, "migrating twice changes nothing");
    }

    // Each stage and division pays once; falling back and climbing again pays nothing; Grand Champion every 10 stars.
    private static void Rewards(ProfileService profile, CheckReport report)
    {
        DivisionCatalog divisions = profile.Divisions;
        int gold = divisions.IndexOf("gold"), silver = divisions.IndexOf("silver");
        report.Check(divisions.RewardForStage(0, out _) == 0 && divisions.RewardForStage(1, out bool division) == 50 && !division &&
                     divisions.RewardForStage(3, out division) == 150 && division && divisions.RewardForStage(6, out division) == 300 && division &&
                     divisions.RewardForStage(15, out division) == divisions.ChampionEntryReward && division, "reward table: Bronze II 50, Silver III 150, Gold III 300, Grand Champion 1000");

        // Climb Gold III -> II -> I, fall back to II, climb again: Gold II and I pay exactly once each.
        profile.ResetProfile();
        SetRank(profile, gold, 0, 2);
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        int coins = profile.Coins;
        MatchOutcome outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(outcome.Rank.StageUp && outcome.StageRewardCoins == divisions.At(gold).StageReward && profile.Coins - coins == divisions.At(gold).WinCoins + outcome.StageRewardCoins,
            $"Gold II: one stage reward (+{outcome.StageRewardCoins})");
        outcome = profile.ApplyMatchResult(false, new MatchStats());
        report.Check(Is(profile.Rank, gold, 0, divisions.At(gold).StarsPerStage - 1) && outcome.Rank.StageDown && outcome.RankRewardCoins == 0, "fall back to Gold III");
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(outcome.Rank.StageUp && Is(profile.Rank, gold, 1, 0) && outcome.RankRewardCoins == 0, "climbing back to Gold II pays nothing again");
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        profile.ApplyMatchResult(true, new MatchStats());
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        profile.ApplyMatchResult(true, new MatchStats());
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(Is(profile.Rank, gold, 2, 0) || Is(profile.Rank, gold, 2, 1), "up to Gold I");
        report.Check(PlayerPrefs.GetInt(SaveKeys.RewardGrantedStage, 0) == gold * 3 + 2, "Gold I paid, reward stage moved up");

        // Entering Silver pays the division reward once; a loss in Silver never drops a stage, so nothing to repay.
        profile.ResetProfile();
        SetRank(profile, silver - 1, 2, 1);
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(outcome.DivisionRewardCoins == divisions.At(silver).DivisionReward && outcome.StageRewardCoins == 0, "Silver III pays the division reward");
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        int after = profile.Coins;
        profile.ApplyMatchResult(false, new MatchStats());
        report.Check(profile.Coins - after == divisions.At(silver).LossCoins, "no reward on a loss");

        // Grand Champion: 10 stars pays 300; losing and winning back pays nothing; 20 pays again.
        profile.ResetProfile();
        profile.SetRank(new RankState { Division = divisions.Count - 1, Stage = 2, IsChampion = true, ChampionStars = 9 });
        PlayerPrefs.SetInt(SaveKeys.RewardGrantedStage, divisions.Count * 3);
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.ChampionStars == 10 && outcome.MilestoneRewardCoins == divisions.ChampionMilestoneReward, "Grand Champion: 10 stars pays the milestone");
        profile.ApplyMatchResult(false, new MatchStats());
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.ChampionStars == 10 && outcome.MilestoneRewardCoins == 0, "Grand Champion: back at 10 stars pays nothing again");
        profile.SetRank(new RankState { Division = divisions.Count - 1, Stage = 2, IsChampion = true, ChampionStars = 19 });
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.ChampionStars == 20 && outcome.MilestoneRewardCoins == divisions.ChampionMilestoneReward, "Grand Champion: 20 stars pays again");
    }

    private static void LossCoins(ProfileService profile, CheckReport report)
    {
        DivisionCatalog divisions = profile.Divisions;
        int[] expected = { 5, 8, 12, 16, 20 };
        bool all = true;
        string seen = "";
        for (int d = 0; d < divisions.Count; d++)
        {
            profile.ResetProfile();
            SetRank(profile, d, 1, 0);
            MatchOutcome outcome = profile.ApplyMatchResult(false, new MatchStats());
            all &= outcome.CoinsFromMatch == expected[d] && outcome.RankRewardCoins == 0;
            seen += outcome.CoinsFromMatch + " ";
        }
        profile.ResetProfile();
        profile.SetRank(new RankState { Division = divisions.Count - 1, Stage = 2, IsChampion = true });
        PlayerPrefs.SetInt(SaveKeys.RewardGrantedStage, divisions.Count * 3);
        all &= profile.ApplyMatchResult(false, new MatchStats()).CoinsFromMatch == 25;
        report.Check(all, $"a loss pays 5 / 8 / 12 / 16 / 20 and 25 in Grand Champion ({seen})");
    }

    // Sets: unlocked by entering the division, progress for all that are open, three on the menu (finished first),
    // CLAIM, the bonus with the last claim, older sets stay active.
    private static void Sets(ProfileService profile, CheckReport report)
    {
        ObjectiveCatalog objectives = profile.Objectives;
        int silver = profile.Divisions.IndexOf("silver");
        profile.ResetProfile();
        report.Check(profile.UnlockedSets == 1 && !profile.IsUnlocked("silver_draw"), "a fresh profile has the Bronze set only");

        // Silver objectives don't move in Bronze.
        var stats = new MatchStats { PerfectReleases = 2, TwoBallShots = 1 };
        for (int i = 0; i < 8; i++)
            stats.Pots.Add(new PotRecord { Distance = 0.5f, DrawBack = 0.5f, Follow = true });
        MatchOutcome outcome = profile.ApplyMatchResult(true, stats);
        report.Check(profile.ObjectiveProgress("silver_draw") == 0 && profile.ObjectiveProgress("silver_follow") == 0, "a set that isn't open doesn't count");
        report.Check(profile.ObjectiveProgress("bronze_pocket_8") == 8 && profile.ObjectiveProgress("bronze_perfect_5") == 2 && profile.ObjectiveProgress("bronze_win") == 1,
            "pots, PERFECT releases and the win counted");
        report.Check(profile.ObjectiveProgress("bronze_clean_win") == 1 && profile.ObjectiveProgress("bronze_two_balls") == 1,
            "progress counts for objectives that are not on the menu too");
        report.Check(outcome.ObjectivesCompleted.Exists(o => o.Id == "bronze_win") && outcome.ObjectivesCompleted.Count == 4, $"4 objectives finished ({outcome.ObjectivesCompleted.Count})");
        report.Check(Visible(profile) == "bronze_pocket_8,bronze_win,bronze_two_balls", $"finished ones first on the menu ({Visible(profile)})");
        profile.ApplyMatchResult(false, stats);
        report.Check(profile.ObjectiveProgress("bronze_perfect_5") == 4 && profile.ObjectiveProgress("bronze_pocket_8") == 8, "progress adds up over matches, capped at the target");

        // CLAIM.
        report.Check(!profile.TryClaimObjective("bronze_perfect_5", out _), "an unfinished objective can't be claimed");
        report.Check(!profile.TryClaimObjective("silver_draw", out _), "a locked objective can't be claimed");
        int before = profile.Coins;
        report.Check(profile.TryClaimObjective("bronze_win", out ProfileService.ObjectiveClaim claim) && claim.Reward == 50 && claim.SetBonus == 0 && profile.Coins == before + 50, "CLAIM pays the reward");
        report.Check(!profile.TryClaimObjective("bronze_win", out _), "a claimed objective is gone");
        report.Check(Visible(profile) == "bronze_pocket_8,bronze_two_balls,bronze_clean_win", $"the next objective takes the slot ({Visible(profile)})");

        // The bonus comes with the last claim of the set, once.
        PlayerPrefs.SetInt(SaveKeys.ObjectiveProgress("bronze_perfect_5"), 5);
        PlayerPrefs.SetInt(SaveKeys.ObjectiveProgress("bronze_fine_aim"), 3);
        int bonusPaid = 0, claimed = 1;
        foreach (ObjectiveDefinition objective in objectives.Sets[0].Objectives)
        {
            if (objective.Id == "bronze_win")
                continue;
            before = profile.Coins;
            report.Check(profile.TryClaimObjective(objective.Id, out claim), $"claim {objective.Id}");
            bonusPaid += claim.SetBonus;
            claimed++;
            if (claimed < 6)
                report.Check(claim.SetBonus == 0 && profile.Coins == before + objective.Reward, $"{objective.Id}: reward only until the last");
            else
                report.Check(claim.SetBonus == objectives.Sets[0].Bonus && claim.SetDivision == 0 && profile.Coins == before + objective.Reward + claim.SetBonus, "the last claim pays the set bonus");
        }
        report.Check(bonusPaid == objectives.Sets[0].Bonus && Visible(profile) == "", "bonus paid once, nothing left in the Bronze set");

        // Entering Silver opens its set; an older, unfinished set stays active.
        profile.ResetProfile();
        profile.ApplyMatchResult(false, new MatchStats { PerfectReleases = 1 });
        SetRank(profile, silver, 0, 0);
        report.Check(profile.UnlockedSets == 2 && profile.IsUnlocked("silver_draw"), "Silver opens the second set");
        report.Check(Visible(profile) == "bronze_pocket_8,bronze_perfect_5,bronze_win", "unfinished Bronze objectives stay first");
        var silverStats = new MatchStats { PerfectReleases = 1, MaxPerfectStreak = 3 };
        silverStats.Pots.Add(new PotRecord { DrawBack = 0.5f });
        profile.ApplyMatchResult(false, silverStats);
        report.Check(profile.ObjectiveProgress("silver_draw") == 1 && profile.ObjectiveProgress("silver_perfect_row") == 3, "Silver objectives count from the match that opened the set on");
        report.Check(profile.ObjectiveProgress("bronze_perfect_5") == 2, "and the Bronze ones still count");
        ObjectiveViewData view = profile.GetSnapshot().Objectives.Find(o => o.Id == "bronze_pocket_8");
        report.Check(view != null && view.Playable && view.Division == 0 && !view.Locked, "snapshot: pocketing is a loss-proof objective of set 0");
        report.Check(!profile.GetSnapshot().Objectives.Exists(o => o.Id == "bronze_win" && o.Playable), "snapshot: winning isn't");

        // A win is not needed for the playable ones.
        profile.ResetProfile();
        var lossStats = new MatchStats();
        for (int i = 0; i < 3; i++)
            lossStats.Pots.Add(new PotRecord { FineAim = true });
        profile.ApplyMatchResult(false, lossStats);
        report.Check(profile.ObjectiveProgress("bronze_pocket_8") == 3 && profile.ObjectiveProgress("bronze_fine_aim") == 3 && profile.ObjectiveProgress("bronze_win") == 0,
            "pots and FINE AIM pots count in a loss, the win doesn't");
    }

    // Every new kind, from a synthetic shot through MatchStatsRecorder (the same path as a match).
    private static void Kinds(ProfileService profile, CheckReport report)
    {
        ObjectiveCatalog objectives = profile.Objectives;
        MatchConfig config = PhysicsChecks.Config;
        PoolSettings pool = config.ToPoolSettings();
        var table = new TableGeometry(pool);
        Vector2 dir = Vector2.right;

        // A straight pot of the 1 into pocket 0, the cue ball stopped 5 cm after the hit.
        MatchStats Pot(Vector2 tip, float cutDeg, Vector2 contact, Vector2 cueEnd, int pocket, int called = -1, RuleSetId rules = RuleSetId.Standard)
        {
            RulesState state = RulesState.NewRack(Side.Player, rules);
            state.IsBreakShot = false;
            state.Groups[(int)Side.Player] = BallGroup.Solids;
            state.Groups[(int)Side.Bot] = BallGroup.Stripes;
            state.CalledPocket = called;
            var balls = new BallState[BallGroups.BallCount];
            for (int i = 0; i < balls.Length; i++)
                balls[i].Pos = new Vector2(-0.4f, 0.1f * i - 0.5f);
            balls[0].Pos = contact - dir * 0.05f;
            var result = new ShotResult { FirstContactBall = 1, FirstContactCuePos = contact, AnyCushionAfterContact = true };
            result.FirstContactObjectDir = Quaternion.Euler(0f, 0f, cutDeg) * dir;
            result.FinalPositions[0] = cueEnd;
            result.Pocketed.Add(new PocketEvent { Number = 1, PocketIndex = pocket, Time = 1f });
            RulesOutcome outcome = EightBallRules.Evaluate(state, result);
            var recorder = new MatchStatsRecorder();
            recorder.ShotStart(balls, dir, ReleaseZone.Yellow, false, tip);
            recorder.ShotEnd(state, outcome, result, table, 0f);
            return recorder.Stats;
        }

        RankState rank = new RankState { Division = 2 };
        int Advance(string id, MatchStats stats) => ObjectiveRules.Advance(objectives.Get(id), 0, stats, rank, rank, 1);

        Vector2 contact = new(0f, 0f);
        MatchStats draw = Pot(new Vector2(0f, -0.5f), 0f, contact, contact - dir * 0.7f, 0);
        report.Check(draw.Pots.Count == 1 && Mathf.Abs(draw.Pots[0].DrawBack - 0.7f) < 1e-3f, $"recorder: draw back {draw.Pots[0].DrawBack:F2} m");
        report.Check(Advance("silver_draw", draw) == 1 && Advance("diamond_draw_60", draw) == 1, "DrawShots: 70 cm counts for 30 cm+ and 60 cm+");
        report.Check(Advance("silver_draw", Pot(new Vector2(0f, -0.5f), 0f, contact, contact - dir * 0.2f, 0)) == 0, "DrawShots: 20 cm back doesn't");
        report.Check(Advance("silver_draw", Pot(new Vector2(0f, 0f), 0f, contact, contact - dir * 0.7f, 0)) == 0, "DrawShots: needs a low tip");

        MatchStats follow = Pot(new Vector2(0f, 0.5f), 0f, contact, contact + dir * 0.4f, 0);
        report.Check(Advance("silver_follow", follow) == 1, "FollowPots: high tip, rolled on 40 cm");
        report.Check(Advance("silver_follow", Pot(new Vector2(0f, 0.5f), 0f, contact, contact + dir * 0.05f, 0)) == 0, "FollowPots: 5 cm isn't follow");
        report.Check(Advance("silver_follow", Pot(new Vector2(0f, 0.1f), 0f, contact, contact + dir * 0.4f, 0)) == 0, "FollowPots: needs b >= 0.25");

        report.Check(Advance("gold_stun", Pot(new Vector2(0f, -0.1f), 4f, contact, contact + dir * 0.06f, 0)) == 1, "StunPots: straight, stopped within 10 cm");
        report.Check(Advance("gold_stun", Pot(new Vector2(0f, -0.1f), 30f, contact, contact + dir * 0.06f, 0)) == 0, "StunPots: a 30 degree cut isn't straight");
        report.Check(Advance("gold_stun", Pot(new Vector2(0f, -0.1f), 4f, contact, contact + dir * 0.3f, 0)) == 0, "StunPots: 30 cm away isn't a stun");

        report.Check(Advance("platinum_side", Pot(new Vector2(0.4f, 0f), 0f, contact, contact + dir * 0.3f, 0)) == 1, "SideSpinPots: |a| 0.4");
        report.Check(Advance("platinum_side", Pot(new Vector2(-0.3f, 0f), 0f, contact, contact + dir * 0.3f, 0)) == 1 &&
                     Advance("platinum_side", Pot(new Vector2(0.1f, 0f), 0f, contact, contact + dir * 0.3f, 0)) == 0, "SideSpinPots: left spin counts, 0.1 doesn't");

        MatchStats called = Pot(Vector2.zero, 0f, contact, contact + dir * 0.3f, 3, 3, RuleSetId.CallAll);
        report.Check(Advance("diamond_called_40", called) == 1, "CalledPots: the called pocket");
        MatchStats wrong = Pot(Vector2.zero, 0f, contact, contact + dir * 0.3f, 2, 3, RuleSetId.CallAll);
        report.Check(wrong.Pots.Count == 0 && Advance("diamond_called_40", wrong) == 0, "CalledPots: another pocket doesn't count at all");
        report.Check(Advance("diamond_called_40", Pot(Vector2.zero, 0f, contact, contact + dir * 0.3f, 3)) == 0, "CalledPots: nothing without a call");

        // The 8 in the called pocket wins the match: CalledEightWins.
        {
            RulesState state = RulesState.NewRack(Side.Player, RuleSetId.CallEight);
            state.IsBreakShot = false;
            state.Groups[(int)Side.Player] = BallGroup.Solids;
            state.Groups[(int)Side.Bot] = BallGroup.Stripes;
            for (int n = 1; n <= 7; n++)
                state.Pocketed[n] = true;
            state.CalledPocket = 4;
            var result = new ShotResult { FirstContactBall = 8 };
            result.Pocketed.Add(new PocketEvent { Number = 8, PocketIndex = 4 });
            RulesOutcome outcome = EightBallRules.Evaluate(state, result);
            var recorder = new MatchStatsRecorder();
            var balls = new BallState[BallGroups.BallCount];
            recorder.ShotStart(balls, dir, ReleaseZone.Green, false);
            recorder.ShotEnd(state, outcome, result, table, 0f);
            recorder.Finish(outcome.Next.Winner == Side.Player, outcome.Next);
            report.Check(recorder.Stats.CalledEightWin && Advance("silver_called_8", recorder.Stats) == 1, "CalledEightWins: the 8 in the called pocket");
            state.Rules = RuleSetId.Standard;
            state.CalledPocket = -1;
            outcome = EightBallRules.Evaluate(state, result);
            recorder = new MatchStatsRecorder();
            recorder.ShotStart(balls, dir, ReleaseZone.Green, false);
            recorder.ShotEnd(state, outcome, result, table, 0f);
            recorder.Finish(true, outcome.Next);
            report.Check(!recorder.Stats.CalledEightWin, "CalledEightWins: not without a call");
        }

        // Visits: balls potted in one turn.
        var visits = new MatchStats();
        visits.VisitPots.AddRange(new[] { 3, 2, 4, 1 });
        report.Check(Advance("platinum_visit_3", visits) == 2 && Advance("diamond_visit_3", visits) == 2, "VisitPots: two visits of 3+ balls");
        {
            // Through the recorder: two shots in one visit.
            RulesState state = RulesState.NewRack(Side.Player);
            state.IsBreakShot = false;
            state.Groups[(int)Side.Player] = BallGroup.Solids;
            state.Groups[(int)Side.Bot] = BallGroup.Stripes;
            var recorder = new MatchStatsRecorder();
            var balls = new BallState[BallGroups.BallCount];
            for (int shot = 0; shot < 3; shot++)
            {
                var result = new ShotResult { FirstContactBall = shot + 1 };
                result.Pocketed.Add(new PocketEvent { Number = shot + 1, PocketIndex = shot });
                RulesOutcome outcome = EightBallRules.Evaluate(state, result);
                recorder.ShotStart(balls, dir, ReleaseZone.Yellow, false);
                recorder.ShotEnd(state, outcome, result, table, 0f);
                state = outcome.Next;
            }
            var miss = new ShotResult { FirstContactBall = 4 };
            RulesOutcome missOutcome = EightBallRules.Evaluate(state, miss);
            recorder.ShotStart(balls, dir, ReleaseZone.Yellow, false);
            recorder.ShotEnd(state, missOutcome, miss, table, 0f);
            report.Check(recorder.Stats.VisitPots.Count == 1 && recorder.Stats.VisitPots[0] == 3, $"recorder: one visit of 3 balls ({string.Join(",", recorder.Stats.VisitPots)})");
        }

        // The "in division" kinds count where the match was played, that division or higher.
        var many = new MatchStats { PerfectReleases = 60, Won = true };
        for (int i = 0; i < 25; i++)
            many.Pots.Add(new PotRecord());
        var inBronze = new RankState { Division = 0 };
        var inSilver = new RankState { Division = 1 };
        var inPlatinum = new RankState { Division = 3 };
        var inDiamond = new RankState { Division = 4 };
        report.Check(ObjectiveRules.Advance(objectives.Get("silver_pocket_20"), 0, many, inBronze, inBronze, 0) == 0 &&
                     ObjectiveRules.Advance(objectives.Get("silver_pocket_20"), 0, many, inSilver, inSilver, 0) == 20 &&
                     ObjectiveRules.Advance(objectives.Get("silver_pocket_20"), 0, many, inPlatinum, inPlatinum, 0) == 20, "PocketBallsInDivision: from Silver on");
        report.Check(ObjectiveRules.Advance(objectives.Get("platinum_perfect_50"), 0, many, inSilver, inSilver, 0) == 0 &&
                     ObjectiveRules.Advance(objectives.Get("platinum_perfect_50"), 0, many, inPlatinum, inPlatinum, 0) == 50, "PerfectReleasesInDivision: from Platinum on");
        report.Check(ObjectiveRules.Advance(objectives.Get("diamond_win_row"), 0, many, inPlatinum, inDiamond, 3) == 0 &&
                     ObjectiveRules.Advance(objectives.Get("diamond_win_row"), 0, many, inDiamond, inDiamond, 3) == 3 &&
                     ObjectiveRules.Advance(objectives.Get("diamond_win_row"), 2, new MatchStats(), inDiamond, inDiamond, 0) == 2, "WinStreakInDivision: the streak counts when the last win was in Diamond");
        var champion = new RankState { Division = 4, Stage = 2, IsChampion = true };
        report.Check(ObjectiveRules.Advance(objectives.Get("diamond_reach_gc"), 0, many, inDiamond, inDiamond, 0) == 0 &&
                     ObjectiveRules.Advance(objectives.Get("diamond_reach_gc"), 0, many, inDiamond, champion, 1) == 1, "Reach Grand Champion");
    }

    // "Gold opponents use the Pro Cue": shown while the division's cue is better than the best owned.
    private static void CueGapCheck(ProfileService profile, CheckReport report)
    {
        int gold = profile.Divisions.IndexOf("gold");
        profile.ResetProfile();
        report.Check(profile.GetSnapshot().CueGap == null, "no cue gap in Bronze (the bot plays the House Cue)");
        SetRank(profile, gold, 0, 0);
        CueGapData gap = profile.GetSnapshot().CueGap;
        report.Check(gap != null && gap.CueId == "pro" && gap.DivisionName == "Gold" && gap.Price == profile.Catalog.Get("pro").Price, "Gold with the House Cue: the Pro Cue gap");
        report.Check(gap != null && gap.PlayObjectivesLeft >= 3, $"the gap points at objectives that pay in a loss ({gap?.PlayObjectivesLeft})");
        PlayerPrefs.SetInt(SaveKeys.CueOwned("club"), 1);
        report.Check(profile.GetSnapshot().CueGap != null, "the Club Cue doesn't close the gap");
        PlayerPrefs.SetInt(SaveKeys.CueOwned("pro"), 1);
        report.Check(profile.GetSnapshot().CueGap == null, "owning the Pro Cue closes it");
        PlayerPrefs.SetInt(SaveKeys.CueOwned("pro"), 0);
        PlayerPrefs.SetInt(SaveKeys.CueOwned("master"), 1);
        report.Check(profile.GetSnapshot().CueGap == null, "a better cue than the division's closes it too");
    }

    private static void SetRank(ProfileService profile, int division, int stage, int stars)
    {
        var rank = new RankState { Division = division, Stage = stage, Stars = stars };
        profile.SetRank(rank);
        // The checks jump around the ladder: what they jump over is not paid.
        PlayerPrefs.SetInt(SaveKeys.RewardGrantedStage, Mathf.Max(PlayerPrefs.GetInt(SaveKeys.RewardGrantedStage, 0), rank.StageIndex));
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
    }

    private static bool Is(RankState rank, int division, int stage, int stars) =>
        !rank.IsChampion && rank.Division == division && rank.Stage == stage && rank.Stars == stars;

    private static string Visible(ProfileService profile) => string.Join(",", profile.VisibleObjectives().ConvertAll(o => o.Id));

    // Manual testing in Play mode: snapshot the real profile, play with it, put it back.
    private static Dictionary<string, object> _manualBackup;

    public static string BackupProfile()
    {
        var go = new GameObject("EconomyChecks_Backup") { hideFlags = HideFlags.HideAndDontSave };
        _manualBackup = Backup(Create(go));
        Object.DestroyImmediate(go);
        return $"profile backed up ({_manualBackup.Count} keys)";
    }

    public static string RestoreProfile()
    {
        if (_manualBackup == null)
            return "nothing to restore";
        var go = new GameObject("EconomyChecks_Backup") { hideFlags = HideFlags.HideAndDontSave };
        Restore(Create(go), _manualBackup);
        Object.DestroyImmediate(go);
        _manualBackup = null;
        return "profile restored";
    }

    // The same, through a file: survives the domain reload of entering Play mode.
    public static string BackupProfileToFile(string path)
    {
        var go = new GameObject("EconomyChecks_Backup") { hideFlags = HideFlags.HideAndDontSave };
        Dictionary<string, object> backup = Backup(Create(go));
        Object.DestroyImmediate(go);
        var lines = new List<string>();
        foreach (var pair in backup)
            lines.Add($"{pair.Key}\t{(pair.Value is int ? "i" : "s")}\t{pair.Value}");
        System.IO.File.WriteAllLines(path, lines);
        return $"profile backed up to {path} ({lines.Count} keys)";
    }

    public static string RestoreProfileFromFile(string path)
    {
        var backup = new Dictionary<string, object>();
        foreach (string line in System.IO.File.ReadAllLines(path))
        {
            string[] parts = line.Split('\t');
            if (parts.Length == 3)
                backup[parts[0]] = parts[1] == "i" ? int.Parse(parts[2]) : parts[2];
        }
        var go = new GameObject("EconomyChecks_Backup") { hideFlags = HideFlags.HideAndDontSave };
        Restore(Create(go), backup);
        Object.DestroyImmediate(go);
        return $"profile restored from {path} ({backup.Count} keys)";
    }

    // Every key the checks may touch: the profile's own, its version and the one-time flags.
    private static IEnumerable<string> Keys(ProfileService profile)
    {
        foreach (string key in profile.ProfileKeys())
            yield return key;
        yield return SaveKeys.ProfileVersion;
        yield return SaveKeys.RulesSeen;
        yield return SaveKeys.InstallDay;
        yield return SaveKeys.SessionCount;
        yield return SaveKeys.TutorialDone;
        yield return SaveKeys.HintsShown;
        yield return SaveKeys.ShotsTaken;
        foreach (string key in SaveKeys.Retired)
            yield return key;
    }

    private static Dictionary<string, object> Backup(ProfileService profile)
    {
        var backup = new Dictionary<string, object>();
        foreach (string key in Keys(profile))
        {
            if (!PlayerPrefs.HasKey(key))
                continue;
            int value = PlayerPrefs.GetInt(key, int.MinValue);
            backup[key] = value != int.MinValue ? value : PlayerPrefs.GetString(key);
        }
        return backup;
    }

    private static void Restore(ProfileService profile, Dictionary<string, object> backup)
    {
        foreach (string key in Keys(profile))
            PlayerPrefs.DeleteKey(key);
        foreach (var pair in backup)
        {
            if (pair.Value is int value)
                PlayerPrefs.SetInt(pair.Key, value);
            else
                PlayerPrefs.SetString(pair.Key, (string)pair.Value);
        }
        PlayerPrefs.Save();
    }
}
