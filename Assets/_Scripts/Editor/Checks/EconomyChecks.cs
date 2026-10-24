using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Runs ProfileService v2 (v3 V10-V12) against a scratch profile in Edit mode, with the real catalogs; the real
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
        ObjectiveCatalog objectives = profile.Objectives;
        CueCatalog cues = profile.Catalog;
        int bronze = divisions.IndexOf("bronze"), silver = divisions.IndexOf("silver"), gold = divisions.IndexOf("gold");
        int diamond = divisions.IndexOf("diamond");
        report.Check(divisions.Count == 5 && bronze == 0 && diamond == 4, $"5 divisions, Bronze first (got {divisions.Count})");
        report.Check(objectives.Objectives.Count == 20, $"20 objectives (got {objectives.Objectives.Count})");
        report.Check(cues.Cues.Count == 6 && cues.Starter.Id == "house", "6 cues, House is the starter");

        // Migration v1 -> v2: old profile keys go, flags stay.
        PlayerPrefs.SetInt(SaveKeys.ProfileVersion, 1);
        PlayerPrefs.SetInt(SaveKeys.Coins, 777);
        PlayerPrefs.SetInt(SaveKeys.CueOwned("club"), 1);
        PlayerPrefs.SetInt(SaveKeys.RulesSeen, 1);
        profile.Migrate();
        report.Check(PlayerPrefs.GetInt(SaveKeys.ProfileVersion, 0) == ProfileService.Version, "migration writes profile version 2");
        report.Check(profile.Coins == 0 && !profile.IsOwned(cues.Get("club")), $"migration starts fresh: 0 coins, no bought cues (coins {profile.Coins})");
        report.Check(profile.RulesSeen, "migration keeps one-time flags");
        report.Check(Slots(profile) == "win_1,pocket_10,perfect_5", $"first three objectives in the slots ({Slots(profile)})");

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
        report.Check(outcome.CoinsFromMatch == profile.Economy.LossCoins && profile.Coins == 25, $"loss +5 coins (coins {profile.Coins})");
        report.Check(profile.WinStreak == 0 && profile.LossStreak == 1, "loss resets the win streak");
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.Stage == 1 && profile.Rank.Stars == 0 && outcome.Rank.StageUp, "2 stars: Bronze II");

        // Hot streak: from the 3rd win in a row on, +1 more star.
        profile.ApplyMatchResult(true, new MatchStats());
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(outcome.Rank.StreakBonus && outcome.Rank.StarsGained == 2, "3rd win in a row gives 2 stars");
        report.Check(profile.Rank.Stage == 2 && profile.Rank.Stars == 1, $"stars carry over into the next stage (stage {profile.Rank.Stage}, stars {profile.Rank.Stars})");

        // Bronze I 1 star + 2 (streak) = Silver III with 1 star carried: a checkpoint.
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(outcome.Rank.NewDivision && profile.Rank.Division == silver && profile.Rank.Stage == 0 && profile.Rank.Stars == 1,
            $"Bronze I -> Silver III checkpoint with carry ({RankLadder.Label(divisions, profile.Rank)} {profile.Rank.Stars})");
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

        // Star Shield: a loss that would cost keeps the rank.
        SetRank(profile, gold, 1, 0);
        outcome = profile.ApplyMatchResult(false, new MatchStats(), true);
        report.Check(Is(profile.Rank, gold, 1, 0) && outcome.Rank.Shielded && !outcome.Rank.StageDown, "Star Shield keeps the stage");

        // Coins by division.
        SetRank(profile, gold, 0, 0);
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        int before = profile.Coins;
        profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Coins - before == divisions.At(gold).WinCoins, $"Gold win +{divisions.At(gold).WinCoins} coins");

        // Diamond I -> Grand Champion, endless stars.
        SetRank(profile, diamond, 2, 2);
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
        outcome = profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.IsChampion && profile.Rank.ChampionStars == 0 && outcome.Rank.NewDivision, "Diamond I full -> Grand Champion");
        report.Check(RankLadder.Label(divisions, profile.Rank) == "GRAND CHAMPION" && profile.Rank.StageIndex == 15, $"champion label and stage index 15 ({profile.Rank.StageIndex})");
        before = profile.Coins;
        profile.ApplyMatchResult(true, new MatchStats());
        report.Check(profile.Rank.ChampionStars == 1 && profile.Coins - before == divisions.ChampionWinCoins, "champion win +1 star, +100 coins");
        profile.ApplyMatchResult(false, new MatchStats());
        profile.ApplyMatchResult(false, new MatchStats());
        report.Check(profile.Rank.IsChampion && profile.Rank.ChampionStars == 0, "champion loss -1 star, never below 0");
        report.Check(profile.CurrentDivision.Id == "diamond", "Grand Champion plays Diamond's bots");

        // Objectives: progress adds up over matches; CLAIM pays and brings the next one.
        profile.ResetProfile();
        var stats = new MatchStats { PerfectReleases = 2 };
        for (int i = 0; i < 4; i++)
            stats.Pots.Add(new PotRecord { Distance = 0.5f });
        outcome = profile.ApplyMatchResult(true, stats);
        report.Check(profile.ObjectiveProgress(0) == 1 && outcome.ObjectivesCompleted.Exists(o => o.Text == "Win a match" && o.Slot == 0), "win completes \"Win a match\"");
        report.Check(profile.ObjectiveProgress(1) == 4 && profile.ObjectiveProgress(2) == 2, "pots and PERFECT releases counted");
        profile.ApplyMatchResult(false, stats);
        report.Check(profile.ObjectiveProgress(1) == 8 && profile.ObjectiveProgress(2) == 4, "progress adds up over matches");
        report.Check(!profile.TryClaimObjective(1, out _), "an unfinished objective can't be claimed");
        before = profile.Coins;
        report.Check(profile.TryClaimObjective(0, out int reward) && reward == 40 && profile.Coins == before + 40, "CLAIM pays the reward");
        report.Check(profile.ObjectiveIn(0).Id == "clean_win" && profile.ObjectiveProgress(0) == 0, "the next objective takes the slot");
        report.Check(!profile.TryClaimObjective(0, out _), "a claimed objective is gone");

        // A "reach" objective is already done when it arrives late.
        SetRank(profile, silver, 0, 0);
        PlayerPrefs.SetInt(SaveKeys.ObjectiveIndex, objectives.Objectives.FindIndex(o => o.Id == "reach_silver"));
        PlayerPrefs.SetInt(SaveKeys.ObjectiveProgress(0), 1);
        PlayerPrefs.SetString(SaveKeys.ObjectiveSlot(0), "win_1");
        profile.TryClaimObjective(0, out _);
        report.Check(profile.ObjectiveIn(0).Id == "reach_silver" && profile.ObjectiveProgress(0) == 1, "Reach Silver arrives done in Silver");

        // Wins count in the division the match was played in.
        ObjectiveDefinition platinumWins = objectives.Get("platinum_wins_10");
        var won = new MatchStats { Won = true };
        var goldI = new RankState { Division = gold, Stage = 2 };
        var platinumIII = new RankState { Division = gold + 1 };
        report.Check(ObjectiveRules.Advance(platinumWins, 0, won, goldI, platinumIII, 1) == 0 &&
                     ObjectiveRules.Advance(platinumWins, 0, won, platinumIII, platinumIII, 1) == 1, "\"Win in Platinum\" counts where it was played");
        ObjectiveDefinition cut = objectives.Get("cut_45_5");
        var cutStats = new MatchStats();
        cutStats.Pots.Add(new PotRecord { Cut = 50f });
        cutStats.Pots.Add(new PotRecord { Cut = 30f });
        report.Check(ObjectiveRules.Advance(cut, 0, cutStats, goldI, goldI, 0) == 1, "cut objective counts cuts of 45 deg or more");

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

        // Abandoned match = a loss with the usual star rule.
        SetRank(profile, silver, 1, 1);
        int lossesBefore = profile.Losses;
        before = profile.Coins;
        profile.SetPendingMatch(true);
        report.Check(profile.ApplyPendingMatch(), "pending match detected");
        report.Check(Is(profile.Rank, silver, 1, 0) && profile.Losses == lossesBefore + 1 && profile.Coins == before + profile.Economy.LossCoins,
            "abandoned match counts as a loss (-1 star in Silver, +5 coins)");
        report.Check(PlayerPrefs.GetInt(SaveKeys.PendingMatch, 0) == 0, "pending flag cleared");
        report.Check(profile.ConsumeAbandonedNotice() && !profile.ConsumeAbandonedNotice(), "abandoned notice shown once");
        report.Check(!profile.ApplyPendingMatch(), "nothing without the pending flag");

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

        // The Ranked Road: 15 stages + Grand Champion, five checkpoints, the player's place.
        RoadViewData road = profile.RoadView();
        report.Check(road.Stages.Count == 16 && road.Stages.FindAll(s => s.IsCheckpoint && !s.IsChampion).Count == 5, "road: 15 stages, 5 checkpoints, Grand Champion");
        report.Check(road.CurrentIndex == 2 && road.Stages[2].State == RoadStageState.Current && road.Stages[2].Stars == 1 &&
                     road.Stages[1].State == RoadStageState.Done && road.Stages[3].State == RoadStageState.Ahead, "road: Bronze I is current with 1 star");
        report.Check(road.Stages[3].Label == "SILVER III" && road.Stages[3].VenueName == "Downtown Hall" && road.Stages[3].BotCueName == "Club Cue",
            "road: Silver III in Downtown Hall, bot plays the Club Cue");
    }

    private static void SetRank(ProfileService profile, int division, int stage, int stars)
    {
        profile.SetRank(new RankState { Division = division, Stage = stage, Stars = stars });
        PlayerPrefs.SetInt(SaveKeys.WinStreak, 0);
    }

    private static bool Is(RankState rank, int division, int stage, int stars) =>
        !rank.IsChampion && rank.Division == division && rank.Stage == stage && rank.Stars == stars;

    private static string Slots(ProfileService profile)
    {
        var ids = new List<string>();
        for (int slot = 0; slot < ObjectiveCatalog.Slots; slot++)
            ids.Add(profile.ObjectiveIn(slot)?.Id ?? "-");
        return string.Join(",", ids);
    }

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
