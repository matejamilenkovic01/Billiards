using System;

// Payloads handed to popups by controllers (or other UI), so popups never read game state themselves.
public class SettingsPopupData
{
    public readonly float SfxVolume;
    public readonly float MusicVolume;

    public SettingsPopupData(float sfxVolume, float musicVolume)
    {
        SfxVolume = sfxVolume;
        MusicVolume = musicVolume;
    }
}

public class CuesPopupData
{
    public readonly ProfileSnapshot Profile;

    public CuesPopupData(ProfileSnapshot profile) => Profile = profile;
}

public class ConfirmPopupData
{
    public readonly string Title;
    public readonly string Body;
    public readonly string ConfirmLabel;
    public readonly int Price;          // shown with a coin when > 0
    public readonly bool IsDanger;      // red confirm button (e.g. forfeit)
    public readonly Action OnConfirm;

    public ConfirmPopupData(string title, string body, string confirmLabel, Action onConfirm, int price = 0, bool isDanger = false)
    {
        Title = title;
        Body = body;
        ConfirmLabel = confirmLabel;
        OnConfirm = onConfirm;
        Price = price;
        IsDanger = isDanger;
    }
}

public class NoEnergyPopupData
{
    public readonly CueViewData Cue;
    public readonly int Coins;

    public NoEnergyPopupData(CueViewData cue, int coins)
    {
        Cue = cue;
        Coins = coins;
    }
}

public class RulesPopupData
{
    public readonly RuleId Focus;

    public RulesPopupData(RuleId focus) => Focus = focus;
}

public class MatchResultPopupData
{
    public readonly MatchResult Result;
    public readonly int PreviousBalance;

    public MatchResultPopupData(MatchResult result, int previousBalance)
    {
        Result = result;
        PreviousBalance = previousBalance;
    }
}
