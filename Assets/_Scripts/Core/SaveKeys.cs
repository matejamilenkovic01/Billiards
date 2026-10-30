public static class SaveKeys
{
    public const string Coins = "Coins";
    public const string Wins = "Wins";
    public const string Losses = "Losses";
    public const string SelectedCue = "SelectedCue";
    public const string PendingMatch = "PendingMatch";
    public const string ShotsTaken = "ShotsTaken";
    public const string RulesSeen = "RulesSeen";
    public const string HintsShown = "HintsShown";
    public const string SfxVolume = "SfxVolume";
    public const string MusicVolume = "MusicVolume";
    public const string Haptics = "Haptics";

    // Profile v2 (v3 V12).
    public const string ProfileVersion = "ProfileVersion";
    public const string MatchesPlayed = "MatchesPlayed";
    public const string WinStreak = "WinStreak";
    public const string LossStreak = "LossStreak";
    public const string Division = "Division";
    public const string Stage = "Stage";
    public const string Stars = "Stars";
    public const string ChampionStars = "ChampionStars";
    public const string BestStageReached = "BestStageReached";
    public const string TutorialDone = "TutorialDone";

    // Profile v3 (v4 V14): objectives per id, rank rewards, analytics counters.
    public const string RewardGrantedStage = "RewardGrantedStage";   // the highest stage whose reward was paid
    public const string ChampionMilestones = "ChampionMilestones";   // Grand Champion milestone rewards paid
    public const string InstallDay = "InstallDay";                   // UTC date of the first start, yyyyMMdd
    public const string SessionCount = "SessionCount";
    public const string MatchesToday = "MatchesToday";
    public const string MatchesTodayDate = "MatchesTodayDate";

    // Keys of profile v2 that v3 removes (the monetisation probes and the slot objectives); only used to delete them.
    // Written in two parts so a search for the removed probes (v4 V14) finds only real leftovers.
    public static readonly string[] Retired =
    {
        "ObjectiveIndex", "Reward" + "edDay", "Star" + "ShieldsToday", "Star" + "Shields", "Starter" + "PackShows",
        "Starter" + "PackTapped", "Starter" + "PackNotify", "OnlineTapped", "OnlineNotify", "Sur" + "veyDone", "Sur" + "veyScore",
        "Sur" + "veyMissing", "ObjectiveSlot_0", "ObjectiveSlot_1", "ObjectiveSlot_2", "ObjectiveProgress_0", "ObjectiveProgress_1",
        "ObjectiveProgress_2"
    };

    public static string CueOwned(string cueId) => "CueOwned_" + cueId;
    public static string ObjectiveProgress(string id) => "ObjectiveProgress_" + id;
    public static string ObjectiveClaimed(string id) => "ObjectiveClaimed_" + id;
    public static string ObjectiveSetBonus(string divisionId) => "ObjectiveSetBonus_" + divisionId;
}
