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
    public const string ObjectiveIndex = "ObjectiveIndex";
    public const string TutorialDone = "TutorialDone";

    public static string CueOwned(string cueId) => "CueOwned_" + cueId;
    public static string ObjectiveSlot(int slot) => "ObjectiveSlot_" + slot;
    public static string ObjectiveProgress(int slot) => "ObjectiveProgress_" + slot;
}
