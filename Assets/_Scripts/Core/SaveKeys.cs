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

    public static string CueOwned(string cueId) => "CueOwned_" + cueId;
    public static string CueEnergy(string cueId) => "CueEnergy_" + cueId;
}
