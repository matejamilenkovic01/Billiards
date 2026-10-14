using System.Collections.Generic;
using UnityEngine;

// Read-only data handed to UI through events and popup payloads. UI never reads services directly.

public enum CueState
{
    Locked = 0,
    Unlocked = 1,
    Owned = 2
}

public sealed class CueViewData
{
    public string Id;
    public string Name;
    public Sprite Sprite;
    public CueState State;
    public bool IsSelected;

    public int Price;
    public int CoinsShort;          // > 0 when the player can't afford the price
    public int WinsToUnlock;
    public int Wins;

    public bool InfiniteEnergy;
    public int Energy;
    public int MaxEnergy;
    public int RefillPrice;
    public bool CanRefill;          // owned, not full, enough coins

    public float GuideDiameters;
    public float AimLine01;         // relative to the best cue in the catalog
    public float ReleaseWindow01;

    public bool CanAfford => CoinsShort <= 0;
    public bool HasEnergy => InfiniteEnergy || Energy > 0;
}

public sealed class ProfileSnapshot
{
    public int Coins;
    public int Wins;
    public int Losses;
    public string SelectedCueId;
    public string JustPurchasedId;      // set only on the snapshot raised right after a purchase
    public List<CueViewData> Cues = new();

    public CueViewData SelectedCue => Find(SelectedCueId);

    public CueViewData Find(string id) => Cues.Find(c => c.Id == id);
}

public sealed class MatchReward
{
    public bool Won;
    public int CoinDelta;
    public int NewBalance;
    public List<string> NewUnlocks = new();   // cue display names
}

public sealed class MatchStartInfo
{
    public bool PlayerBreaks;
    public string CueName;
    public Sprite CueSprite;
    public float PullRangePixels;
    public float PullStartThreshold;
    public float GreenHalf;
    public float YellowWidth;
    public float OscillationPeriod;
}

public sealed class TurnInfo
{
    public bool IsPlayer;
    public bool BallInHand;
    public bool KitchenOnly;
    public bool IsBreak;
    public BallGroup PlayerGroup;
    public BallGroup BotGroup;
    public bool[] Pocketed = new bool[BallGroups.BallCount];   // by ball number
    public List<int> LegalTargets = new();
    public bool PlayerOnEight;
    public bool BotOnEight;
}

public sealed class MatchResult
{
    public bool Won;
    public string Reason;
    public int CoinDelta;
    public int NewBalance;
    public List<string> NewUnlocks = new();
}

public struct ReleaseMeterSettings
{
    public float GreenHalf;
    public float YellowWidth;
    public float Period;
}

public struct ShotFeedback
{
    public ReleaseZone Zone;
    public float Power01;
    public bool IsPlayer;
    public float AngleErrorDeg;
}

public enum ToastStyle
{
    Info = 0,
    Good = 1,
    Warning = 2,
    Bad = 3,
    Perfect = 4
}

public struct ToastMessage
{
    public string Text;
    public ToastStyle Style;

    public ToastMessage(string text, ToastStyle style = ToastStyle.Info)
    {
        Text = text;
        Style = style;
    }
}
