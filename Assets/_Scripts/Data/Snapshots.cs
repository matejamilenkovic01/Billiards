using System.Collections.Generic;
using UnityEngine;

// Read-only data handed to UI through events and popup payloads. UI never reads services directly.

public enum CueState
{
    ForSale = 1,
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

    // Ratings 0-99 (v3 V11) and the difference to the selected cue.
    public int Aim;
    public int Control;
    public int Timing;
    public int Overall;
    public int OverallVsSelected;
    public string RecommendedFor;   // the division where this is the bot's cue

    public float GuideDiameters;
    public float AimLine01;         // relative to the best cue in the catalog
    public float ReleaseWindow01;

    public bool CanAfford => CoinsShort <= 0;
}

// Where the player stands on the ladder (v3 V10) and what the next step is.
public sealed class RankViewData
{
    public string Label;            // "SILVER II", "GRAND CHAMPION"
    public string DivisionName;
    public string VenueName;
    public int Division;
    public int Stage;
    public int Stars;
    public int StarsPerStage;
    public int StarsToNext;
    public string NextLabel;
    public bool NextIsCheckpoint;
    public bool IsChampion;
    public int ChampionStars;
    public Color Color;
}

// A CPU character (v3 V10): name, one line of character, portrait (generated ones show initials over it).
public sealed class OpponentViewData
{
    public string Id;
    public string Name;
    public string ShortName;
    public string Line;
    public Sprite Portrait;
    public bool GeneratedPortrait;
    public string Initials;
    public Color Color;
}

public enum RoadStageState
{
    Done = 0,
    Current = 1,
    Ahead = 2
}

// One step of the Ranked Road: a stage of a division, or Grand Champion at the top.
public sealed class RoadStageViewData
{
    public string Label;            // "SILVER II"
    public string DivisionName;
    public string VenueName;
    public Sprite VenueThumbnail;
    public string BotCueName;
    public int StarsPerStage;
    public int Stars;               // stars held there (the current stage only)
    public bool IsCheckpoint;       // the first stage of a division
    public bool IsChampion;
    public RoadStageState State;
    public Color Color;
}

public sealed class RoadViewData
{
    public List<RoadStageViewData> Stages = new();   // Bronze III first, Grand Champion last
    public int CurrentIndex;
    public int ChampionStars;
}

// What a results screen celebrates.
public enum TutorialStepResult
{
    Started = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3
}

// What the tutorial panel shows for a lesson.
public sealed class TutorialStepInfo
{
    public int Index;
    public int Count;
    public string Title;
    public string Instruction;
}

// How the release bar is eased for the player's shots (the tutorial; normal play leaves it off).
public struct ReleaseAssist
{
    public bool AlwaysGreen;
    public float PeriodScale;
    public float GreenScale;

    public static ReleaseAssist None => new() { PeriodScale = 1f, GreenScale = 1f };
}

// Simulated rewarded ads (v3 V12); a real SDK replaces the overlay in phase 2.
public enum RewardedPlacement
{
    StarShield = 0,     // a loss that takes a star or a stage: keep it
    DoubleCoins = 1     // a win: x2 the match's coins
}

// Fake doors (v3 V12): nothing is charged or granted; they measure interest.
public enum FakeDoorId
{
    StarterPack = 0,
    OnlineRanked = 1
}

public enum FakeDoorAction
{
    Shown = 0,
    Tapped = 1,
    NotifyOn = 2,
    NotifyOff = 3,
    Closed = 4
}

[System.Flags]
public enum SurveyMissing
{
    None = 0,
    Spin = 1 << 0,
    Online = 1 << 1,
    MoreVenues = 1 << 2,
    BetterGraphics = 1 << 3,
    EasierAiming = 1 << 4,
    FasterOpponents = 1 << 5,
    Nothing = 1 << 6
}

public enum RankCelebration
{
    StarGained = 0,
    StarLost = 1,
    StageUp = 2,
    Checkpoint = 3,
    ObjectiveDone = 4
}

public sealed class ObjectiveViewData
{
    public int Slot;
    public string Id;
    public string Text;
    public int Progress;
    public int Target;
    public int Reward;
    public bool Claimable => Progress >= Target;
}

// One pot by the player, for the objectives (v3 V12).
public struct PotRecord
{
    public float Distance;   // the ball's way to the pocket, metres
    public float Cut;        // degrees, when it was the ball the cue ball hit first
    public bool Bank;        // it touched a cushion before dropping
    public bool FineAim;     // FINE AIM was used on this shot
}

// What the player did in one match (v3 V12): filled by the match, read by the objectives.
public sealed class MatchStats
{
    public bool Won;
    public int Fouls;
    public int PerfectReleases;
    public int MaxPerfectStreak;
    public int TwoBallShots;
    public int RunOuts;
    public int OpponentLeft;
    public readonly List<PotRecord> Pots = new();
}

// What a finished match changed in the profile (v3 V10/V12).
public sealed class MatchOutcome
{
    public RankChange Rank;
    public string RankBefore;
    public string RankAfter;
    public int CoinsBefore;
    public int CoinsAfter;
    public int CoinsFromMatch;
    public readonly List<ObjectiveViewData> ObjectivesCompleted = new();   // objectives that became claimable
    public string CueAffordable;                                // a cue name that just became affordable, or null
    public bool StarShieldAvailable;    // the loss took something and a shield is left today
    public bool DoubleCoinsAvailable;   // a win with coins to double
}

public sealed class ProfileSnapshot
{
    public int Coins;
    public int Wins;
    public int Losses;
    public string SelectedCueId;
    public string JustPurchasedId;      // set only on the snapshot raised right after a purchase
    public List<CueViewData> Cues = new();
    public RankViewData Rank;
    public List<ObjectiveViewData> Objectives = new();
    public CueViewData NextCue;         // the cheapest cue not owned yet (null when all are owned)

    public CueViewData SelectedCue => Find(SelectedCueId);

    public CueViewData Find(string id) => Cues.Find(c => c.Id == id);
}

public sealed class MatchStartInfo
{
    public bool PlayerBreaks;
    public string PlayerName;      // "YOU"
    public string OpponentName;    // the CPU character's name
    public OpponentViewData Opponent;
    public RankViewData Rank;
    public string VenueName;
    public float VsIntroDuration;
    public bool IsTutorial;
    public string CueName;
    public Sprite CueSprite;
    public float PullTravelPixels;
    // v4 V2: the spin picker's limit (ball radii from the centre).
    public float SpinMaxOffset = 0.5f;
    public float PullGrabZone;
    public float PullStartThreshold;
    public int PowerHintShotsLeft;      // the power hint shows until the player has taken this many more shots
    public float PowerIdleHintSeconds;
    public float FineAimRepeatRate;
    public float FineAimRepeatDelay;
    public float ReleaseFreezeDuration;
    public float FoulBannerDuration;
    public float GroupBannerDuration;
    public float GreenHalf;
    public float YellowWidth;
    public float OscillationPeriod;
}

public sealed class TurnInfo
{
    public bool IsPlayer;          // side: the player's / the bot's
    public bool IsHuman;           // the player shoots (false on both sides in Editor bot vs bot)
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
    public string Title;
    public string Reason;
    public int CoinDelta;
    public int NewBalance;
    public string RankBefore;
    public string RankAfter;
    public RankViewData RankBeforeView;
    public RankViewData RankAfterView;
    public int StarsGained;
    public bool StreakBonus;
    public bool StageUp;
    public bool StageDown;
    public bool NewDivision;
    public bool LossCostNothing;
    public string NewVenueName;         // the venue a new division opens
    public string CueAffordable;        // a cue that just became affordable
    public bool StarShieldAvailable;
    public bool DoubleCoinsAvailable;
    public List<ObjectiveViewData> ObjectivesCompleted = new();
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
    public bool IsBreak;
    public bool UsedFineAim;
    // Where the cue ball is on screen (pixels): the PERFECT / GOOD / MISS stamp appears above it (v3 V7).
    public Vector2 CueBallScreen;
    // Where the tip met the cue ball (ball radii, x right, y up) and the cue's elevation in degrees (v4 V1/V2).
    public Vector2 Tip;
    public float Elevation;
}

// How a pocketed ball counts for the shooter (v3 V7): the rim glows green, red or white, the haptic differs.
public enum PocketFxKind
{
    Legal = 0,     // the shooter's group (any ball on an open table), or the 8 when it's time
    Foul = 1,      // the cue ball, or the 8 too early
    Opponent = 2   // the other side's ball
}

// One row of the House Rules card (RulesPopup), so a foul can point at the rule it broke.
public enum RuleId
{
    None = 0,
    Groups = 1,
    GroupAssignment = 2,
    OwnBallFirst = 3,
    RailAfterContact = 4,
    BallInHand = 5,
    EightBall = 6
}

public struct FoulInfo
{
    public FoulType Foul;
    public bool ByPlayer;     // the player fouled (the opponent gets ball in hand), or the opponent did
    public bool MatchOver;    // a foul on the 8-ball: no ball in hand, the match is decided
    public RuleId Rule;
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
