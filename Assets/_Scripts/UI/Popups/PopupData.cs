using System;

// Payloads handed to popups by controllers (or other UI), so popups never read game state themselves.
public class SettingsPopupData
{
    public readonly float SfxVolume;
    public readonly float MusicVolume;
    public readonly bool HapticsOn;
    public readonly bool ShowHowToPlay;    // the menu only (not in a match)

    public SettingsPopupData(float sfxVolume, float musicVolume, bool hapticsOn, bool showHowToPlay)
    {
        SfxVolume = sfxVolume;
        MusicVolume = musicVolume;
        HapticsOn = hapticsOn;
        ShowHowToPlay = showHowToPlay;
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

public class RulesPopupData
{
    public readonly RuleId Focus;
    // v4 V4: the 8-ball rule of the player's division, shown in its own row on top (empty = hidden).
    public readonly string DivisionRule;
    public readonly RuleSetId Rules;

    public RulesPopupData(RuleId focus, string divisionRule = null, RuleSetId rules = RuleSetId.Standard)
    {
        Focus = focus;
        DivisionRule = divisionRule;
        Rules = rules;
    }
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

public class CueTryResultPopupData
{
    public readonly CueTryResultData Result;

    public CueTryResultPopupData(CueTryResultData result) => Result = result;
}

public class RankedRoadPopupData
{
    public readonly RoadViewData Road;

    public RankedRoadPopupData(RoadViewData road) => Road = road;
}

public class SimulatedAdData
{
    public readonly RewardedPlacement Placement;
    public readonly float Seconds;
    public readonly Action<bool> OnClosed;     // completed

    public SimulatedAdData(RewardedPlacement placement, float seconds, Action<bool> onClosed)
    {
        Placement = placement;
        Seconds = seconds;
        OnClosed = onClosed;
    }
}

// A fake door (v3 V12): the offer, or the "coming soon" card; Notify me is a switch kept in the profile.
public class FakeDoorPopupData
{
    public readonly FakeDoorId Id;
    public readonly bool NotifyOn;
    public readonly CueViewData Cue;           // Starter Pack: the Pro Cue
    public readonly int OverallVsHouse;
    public readonly string Price;

    public FakeDoorPopupData(FakeDoorId id, bool notifyOn, CueViewData cue = null, int overallVsHouse = 0, string price = null)
    {
        Id = id;
        NotifyOn = notifyOn;
        Cue = cue;
        OverallVsHouse = overallVsHouse;
        Price = price;
    }
}
