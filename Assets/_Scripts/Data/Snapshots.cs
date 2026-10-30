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
    public int Power;
    public int Overall;
    public int OverallVsSelected;
    // v4 V9: the difference to the selected cue per bar.
    public int AimVsSelected;
    public int ControlVsSelected;
    public int TimingVsSelected;
    public int PowerVsSelected;
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

// What the practice card shows for a try shot (v4 V9).
public sealed class PracticeStepInfo
{
    public int Index;
    public int Count;
    public string Header;       // "TRYING: PRO CUE"
    public string Diff;         // "Green +40% · Bar 19% slower · Aim line ×2 · Power +0.9 m/s"
    public string Title;
    public string Instruction;
}

// What the comparison screen after the try shows.
public sealed class CueTryResultData
{
    public CueViewData Tried;
    public CueViewData Selected;
    public int Shots;
    public int Perfect;
    public int Good;
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

public enum RankCelebration
{
    StarGained = 0,
    StarLost = 1,
    StageUp = 2,
    Checkpoint = 3,
    ObjectiveDone = 4,
    // v4 V11: what the result and the celebration screen show.
    DivisionUp = 5,
    ObjectiveSetComplete = 6,
    ChampionMilestone = 7
}

public sealed class ObjectiveViewData
{
    public string Id;
    public int Division;             // the set (division index) it belongs to
    public bool Playable;            // moves in a lost match too: pays "cue coins"
    public bool Claimed;
    public bool Locked;              // the next division's set: shown, not counted yet
    public string Text;
    public int Progress;
    public int Target;
    public int Reward;
    public bool Claimable => !Locked && !Claimed && Progress >= Target;
}

// One pot by the player, for the objectives (v3 V12).
public struct PotRecord
{
    public float Distance;   // the ball's way to the pocket, metres
    public float Cut;        // degrees, when it was the ball the cue ball hit first
    public bool Bank;        // it touched a cushion before dropping
    public bool FineAim;     // FINE AIM was used on this shot
    public float DrawBack;   // low tip: metres the cue ball came back against the shot direction after contact (0 if not)
    public bool Follow;      // high tip and the cue ball kept rolling forward 10 cm+
    public bool Straight;    // a cut of 10 degrees or less on the ball the cue ball hit first
    public float StunDistance; // metres the cue ball ended from where it hit the ball (meaningful when Straight)
    public bool SideSpin;    // |a| >= 0.25
    public bool Called;      // dropped in the pocket the player called
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
    public bool CalledEightWin;     // won by potting the 8 in the called pocket
    public readonly List<PotRecord> Pots = new();
    public readonly List<int> VisitPots = new();   // balls potted in each visit (a visit = until the turn passes)

    public int VisitsWithPots(int count)
    {
        int visits = 0;
        foreach (int pots in VisitPots)
        {
            if (pots >= count)
                visits++;
        }
        return visits;
    }
}

// What a finished match changed in the profile (v3 V10/V12).
public sealed class MatchOutcome
{
    public RankChange Rank;
    public string RankBefore;
    public string RankAfter;
    public int CoinsBefore;
    public int CoinsAfter;
    public int CoinsFromMatch;          // the win / loss coins of the division
    public int StageRewardCoins;        // new stages (II, I) of a division, once each (v4 V11)
    public int DivisionRewardCoins;     // entering a division (checkpoint), once each
    public int MilestoneRewardCoins;    // Grand Champion: every 10 stars
    public readonly List<ObjectiveViewData> ObjectivesCompleted = new();   // objectives that became claimable
    public string CueAffordable;                                // a cue name that just became affordable, or null
    public int RankRewardCoins => StageRewardCoins + DivisionRewardCoins + MilestoneRewardCoins;
}

// "Gold opponents use the Pro Cue" (v4 V12): the division's cue the player does not own a match for yet.
public sealed class CueGapData
{
    public string DivisionName;
    public string CueId;
    public string CueName;
    public int Price;
    public int Coins;
    public int PlayObjectivesLeft;     // objectives that pay in a loss too and are not claimed yet
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
    public List<ObjectiveViewData> Objectives = new();   // the three shown on the menu (v4 V12)
    public CueViewData NextCue;         // the cheapest cue not owned yet (null when all are owned)
    public CueGapData CueGap;           // the division's cue is better than the best the player owns (null if not)

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
    public bool IsPractice;
    public string CueName;
    public Sprite CueSprite;
    public float PullTravelPixels;
    // v4 V2: the spin picker's limit (ball radii from the centre).
    public float SpinMaxOffset = 0.5f;
    // v4 V4: the 8-ball rule of this match and its card text.
    public RuleSetId Rules;
    public string RuleText = "";
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
    // v4 V4: the rule, whether the shooter must call this shot, and the opponent's choice for the shooter's 8.
    public RuleSetId Rules;
    public bool NeedsCall;
    public int CalledPocket = -1;
    public int EightPocket = -1;
    public PocketCallKind EightPocketKind;
}

// The pocket map (v4 V5): the table from above for calling a pocket, or for choosing one for the opponent's 8.
// Table plane metres; one instance, refilled and raised again whenever something on it changes.
public sealed class PocketMapInfo
{
    public bool Show;
    public bool ForOpponent;               // the big map: the player blocks / picks a pocket for the bot's 8
    public PocketChoiceKind Choice;
    public string Title = "";
    public Vector2 HalfSize;               // play area half width, half length
    public readonly Vector2[] Balls = new Vector2[BallGroups.BallCount];
    public readonly bool[] OnTable = new bool[BallGroups.BallCount];
    public readonly Vector2[] Pockets = new Vector2[6];
    public bool HasAim;
    public Vector2 AimFrom;
    public Vector2 AimTo;
    public int Suggested = -1;
    public int Called = -1;
    public int EightPocket = -1;
    public PocketCallKind EightPocketKind;
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
    public int RankRewardCoins;         // stage / division / milestone rewards, shown as a row of their own
    public int StageRewardCoins;
    public int DivisionRewardCoins;
    public int MilestoneRewardCoins;
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
