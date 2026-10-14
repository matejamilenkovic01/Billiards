using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Runs ProfileService against a scratch profile in Edit mode; the real PlayerPrefs profile is restored afterwards.
public static class EconomyChecks
{
    public static string All()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath);
        var report = new CheckReport("EconomyChecks");
        Dictionary<string, object> backup = Backup(catalog);
        var go = new GameObject("EconomyChecks_ProfileService") { hideFlags = HideFlags.HideAndDontSave };

        try
        {
            ProfileService profile = go.AddComponent<ProfileService>();
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_catalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Run(profile, catalog, report);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Restore(catalog, backup);
        }

        return report.ToString();
    }

    private static void Run(ProfileService profile, CueCatalog catalog, CheckReport report)
    {
        CueDefinition rookie = catalog.Get("rookie");
        CueDefinition club = catalog.Get("club");
        CueDefinition pro = catalog.Get("pro");

        Clear(catalog);
        report.Check(profile.Coins == 10, $"start coins 10 (got {profile.Coins})");
        report.Check(profile.SelectedCueId == "rookie", "rookie selected by default");
        report.Check(profile.IsOwned(rookie), "rookie owned from the start");
        report.Check(profile.CanStartMatch(out _), "can start a match with rookie");

        MatchReward win = profile.ApplyMatchResult(true);
        report.Check(profile.Coins == 20 && win.CoinDelta == 10 && win.NewBalance == 20, $"win +10 (coins {profile.Coins})");
        report.Check(win.NewUnlocks.Contains(club.Name), "first win unlocks Club Cue");
        report.Check(profile.GetState(club) == CueState.Unlocked, "club unlocked after 1 win");
        report.Check(profile.GetState(pro) == CueState.Locked, "pro still locked after 1 win");

        MatchReward loss = profile.ApplyMatchResult(false);
        report.Check(profile.Coins == 19 && loss.CoinDelta == -1, $"loss -1 (coins {profile.Coins})");
        report.Check(profile.Losses == 1, "loss counted");

        PlayerPrefs.SetInt(SaveKeys.Coins, 0);
        loss = profile.ApplyMatchResult(false);
        report.Check(profile.Coins == 0 && loss.CoinDelta == 0, "loss never goes below 0");

        report.Check(!profile.TryBuy("club", out string reason) && reason.Contains("more coins"), "buy without enough coins rejected");
        PlayerPrefs.SetInt(SaveKeys.Coins, 500);
        report.Check(!profile.TryBuy("pro", out reason) && reason.Contains("Win"), "buy locked cue rejected");
        report.Check(!profile.TrySelect("club", out _), "select not-owned cue rejected");

        PlayerPrefs.SetInt(SaveKeys.Coins, 30);
        report.Check(profile.TryBuy("club", out _), "buy unlocked cue with exact coins");
        report.Check(profile.Coins == 0 && profile.IsOwned(club), "buy spends price, cue owned");
        report.Check(profile.GetEnergy(club) == club.MaxEnergy, "bought cue starts with full energy");
        report.Check(profile.SelectedCueId == "rookie", "buying does not auto-select");
        report.Check(!profile.TryBuy("club", out _), "buying an owned cue rejected");

        report.Check(!profile.TryRefill("club", out reason) && reason.Contains("full"), "refill on full energy rejected");
        report.Check(!profile.TryRefill("rookie", out _), "refill on infinite cue rejected");

        profile.ConsumeEnergyForMatch();
        report.Check(profile.GetEnergy(rookie) == int.MaxValue, "rookie energy stays infinite");

        report.Check(profile.TrySelect("club", out _) && profile.SelectedCueId == "club", "select owned cue");
        profile.ConsumeEnergyForMatch();
        report.Check(profile.GetEnergy(club) == club.MaxEnergy - 1, "energy -1 per match");

        report.Check(!profile.TryRefill("club", out reason) && reason.Contains("more coins"), "refill without coins rejected");
        PlayerPrefs.SetInt(SaveKeys.Coins, club.RefillPrice);
        report.Check(profile.TryRefill("club", out _) && profile.GetEnergy(club) == club.MaxEnergy && profile.Coins == 0,
            "refill restores full energy for the refill price");

        PlayerPrefs.SetInt(SaveKeys.CueEnergy("club"), 0);
        report.Check(!profile.CanStartMatch(out reason) && reason.Contains("out of energy"), "cannot start with 0 energy");

        PlayerPrefs.SetInt(SaveKeys.Wins, 2);
        win = profile.ApplyMatchResult(true);
        report.Check(win.NewUnlocks.Count == 1 && win.NewUnlocks[0] == pro.Name, "third win unlocks Pro Cue only");

        // Abandoned match: flag left at 1 means the app died mid-match.
        PlayerPrefs.SetInt(SaveKeys.Coins, 5);
        int lossesBefore = profile.Losses;
        profile.SetPendingMatch(true);
        report.Check(profile.ApplyPendingMatchPenalty(), "pending match detected");
        report.Check(profile.Coins == 4 && profile.Losses == lossesBefore + 1, "pending match costs a loss (-1)");
        report.Check(PlayerPrefs.GetInt(SaveKeys.PendingMatch, 0) == 0, "pending flag cleared");
        report.Check(profile.ConsumeAbandonedNotice() && !profile.ConsumeAbandonedNotice(), "abandoned notice shown once");
        report.Check(!profile.ApplyPendingMatchPenalty(), "no penalty without pending flag");

        ProfileSnapshot snapshot = profile.GetSnapshot();
        report.Check(snapshot.Cues.Count == catalog.Cues.Count, "snapshot lists every cue");
        report.Check(snapshot.Find("legend").AimLine01 >= 0.999f && snapshot.Find("rookie").AimLine01 < snapshot.Find("club").AimLine01,
            "aim line stat grows with better cues");
        report.Check(snapshot.Find("legend").ReleaseWindow01 >= 0.999f && snapshot.Find("rookie").ReleaseWindow01 < snapshot.Find("pro").ReleaseWindow01,
            "release window stat grows with better cues");
    }

    // Manual testing in Play mode: snapshot the real profile, play with it, put it back.
    private static Dictionary<string, object> _manualBackup;

    public static string BackupProfile()
    {
        _manualBackup = Backup(AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath));
        return $"profile backed up ({_manualBackup.Count} keys)";
    }

    public static string RestoreProfile()
    {
        if (_manualBackup == null)
            return "nothing to restore";
        Restore(AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath), _manualBackup);
        _manualBackup = null;
        return "profile restored";
    }

    public static string SetProfile(int coins, int wins, params string[] owned)
    {
        PlayerPrefs.SetInt(SaveKeys.Coins, coins);
        PlayerPrefs.SetInt(SaveKeys.Wins, wins);
        foreach (string id in owned)
            PlayerPrefs.SetInt(SaveKeys.CueOwned(id), 1);
        PlayerPrefs.Save();
        return $"coins={coins} wins={wins} owned+={string.Join(",", owned)}";
    }

    private static IEnumerable<string> IntKeys(CueCatalog catalog)
    {
        yield return SaveKeys.Coins;
        yield return SaveKeys.Wins;
        yield return SaveKeys.Losses;
        yield return SaveKeys.PendingMatch;
        foreach (CueDefinition cue in catalog.Cues)
        {
            yield return SaveKeys.CueOwned(cue.Id);
            yield return SaveKeys.CueEnergy(cue.Id);
        }
    }

    private static void Clear(CueCatalog catalog)
    {
        foreach (string key in IntKeys(catalog))
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.DeleteKey(SaveKeys.SelectedCue);
    }

    private static Dictionary<string, object> Backup(CueCatalog catalog)
    {
        var backup = new Dictionary<string, object>();
        foreach (string key in IntKeys(catalog))
        {
            if (PlayerPrefs.HasKey(key))
                backup[key] = PlayerPrefs.GetInt(key);
        }
        if (PlayerPrefs.HasKey(SaveKeys.SelectedCue))
            backup[SaveKeys.SelectedCue] = PlayerPrefs.GetString(SaveKeys.SelectedCue);
        return backup;
    }

    private static void Restore(CueCatalog catalog, Dictionary<string, object> backup)
    {
        Clear(catalog);
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
