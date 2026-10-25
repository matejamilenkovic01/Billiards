using System.Collections.Generic;
using UnityEngine;

// The only owner of the player profile in PlayerPrefs (v3 V12, profile v2): coins, rank on the ladder, objectives,
// cues, the pending match and one-time flags.
public class ProfileService : SingletonPersistent<ProfileService>
{
    public const int Version = 2;

    [SerializeField] private CueCatalog _catalog;
    [SerializeField] private DivisionCatalog _divisions;
    [SerializeField] private ObjectiveCatalog _objectives;
    [SerializeField] private EconomyConfig _economy;

    private bool _abandonedNoticePending;

    // The last match, for its rewarded offers (v3 V12): what a Star Shield gives back, what Double coins doubles.
    private RankState _lastRankBefore;
    private bool _lastShieldable;
    private bool _lastDoubleable;
    private int _lastMatchCoins;
    private bool _starterPackShownThisSession;

    // The day the rewarded limits count in; the checks move it to test "next day".
    public static System.Func<System.DateTime> Clock = () => System.DateTime.Now;

    public CueCatalog Catalog => _catalog;
    public DivisionCatalog Divisions => _divisions;
    public ObjectiveCatalog Objectives => _objectives;
    public EconomyConfig Economy => _economy;
    public int Coins => PlayerPrefs.GetInt(SaveKeys.Coins, _economy.StartCoins);
    public int Wins => PlayerPrefs.GetInt(SaveKeys.Wins, 0);
    public int Losses => PlayerPrefs.GetInt(SaveKeys.Losses, 0);
    public int MatchesPlayed => PlayerPrefs.GetInt(SaveKeys.MatchesPlayed, 0);
    public int WinStreak => PlayerPrefs.GetInt(SaveKeys.WinStreak, 0);
    public int LossStreak => PlayerPrefs.GetInt(SaveKeys.LossStreak, 0);
    // Shots the player has taken, ever (only counted up to what the first-shots hints need).
    public int ShotsTaken => PlayerPrefs.GetInt(SaveKeys.ShotsTaken, 0);
    private const int ShotsTakenCap = 100;
    // The House Rules card has been shown once (automatically before the first match).
    public bool RulesSeen => PlayerPrefs.GetInt(SaveKeys.RulesSeen, 0) == 1;
    // The tutorial has been finished or skipped once (v3 V9); a fresh install starts with it.
    public bool TutorialDone => PlayerPrefs.GetInt(SaveKeys.TutorialDone, 0) == 1;

    public void MarkRulesSeen()
    {
        PlayerPrefs.SetInt(SaveKeys.RulesSeen, 1);
        PlayerPrefs.Save();
    }

    // One-time hints already shown (bitmask of HintId).
    public bool WasHintShown(HintId hint) => (PlayerPrefs.GetInt(SaveKeys.HintsShown, 0) & (int)hint) != 0;

    public void MarkHintShown(HintId hint)
    {
        PlayerPrefs.SetInt(SaveKeys.HintsShown, PlayerPrefs.GetInt(SaveKeys.HintsShown, 0) | (int)hint);
        PlayerPrefs.Save();
    }

    public CueDefinition SelectedCue => _catalog.Get(SelectedCueId);

    public string SelectedCueId
    {
        get
        {
            string id = PlayerPrefs.GetString(SaveKeys.SelectedCue, _catalog.Starter.Id);
            CueDefinition cue = _catalog.Get(id);
            return cue != null && IsOwned(cue) ? id : _catalog.Starter.Id;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        Migrate();
        ApplyPendingMatch();
    }

    private void OnEnable()
    {
        GameEvents.OnCueSelectRequested += HandleCueSelectRequested;
        GameEvents.OnCueBuyRequested += HandleCueBuyRequested;
        GameEvents.OnObjectiveClaimRequested += HandleObjectiveClaimRequested;
        GameEvents.OnShotFired += HandleShotFired;
        GameEvents.OnTutorialCompleted += HandleTutorialCompleted;
        GameEvents.OnRewardedAdFinished += HandleRewardedAdFinished;
        GameEvents.OnFakeDoor += HandleFakeDoor;
        GameEvents.OnSurveyAnswered += HandleSurveyAnswered;
        GameEvents.OnSurveySkipped += HandleSurveySkipped;
    }

    private void OnDisable()
    {
        GameEvents.OnCueSelectRequested -= HandleCueSelectRequested;
        GameEvents.OnCueBuyRequested -= HandleCueBuyRequested;
        GameEvents.OnObjectiveClaimRequested -= HandleObjectiveClaimRequested;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnTutorialCompleted -= HandleTutorialCompleted;
        GameEvents.OnRewardedAdFinished -= HandleRewardedAdFinished;
        GameEvents.OnFakeDoor -= HandleFakeDoor;
        GameEvents.OnSurveyAnswered -= HandleSurveyAnswered;
        GameEvents.OnSurveySkipped -= HandleSurveySkipped;
    }

    private void HandleTutorialCompleted(bool skipped)
    {
        PlayerPrefs.SetInt(SaveKeys.TutorialDone, 1);
        PlayerPrefs.Save();
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (!feedback.IsPlayer || ShotsTaken >= ShotsTakenCap)
            return;
        PlayerPrefs.SetInt(SaveKeys.ShotsTaken, ShotsTaken + 1);
        PlayerPrefs.Save();
    }

    #region Version
    // Profile v1 (coins / wins / cue energy) had no real users: anything older than v2 starts fresh (sound settings
    // and the one-time flags stay).
    public void Migrate()
    {
        if (PlayerPrefs.GetInt(SaveKeys.ProfileVersion, 0) >= Version)
        {
            EnsureObjectiveSlots();
            return;
        }
        ResetProfile();
        Debug.Log("ProfileService: profile migrated to v2 (fresh start)");
    }

    // Everything the profile owns except volumes, tutorial / rules / hint flags.
    public void ResetProfile()
    {
        foreach (string key in ProfileKeys())
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.SetInt(SaveKeys.Coins, _economy.StartCoins);
        PlayerPrefs.SetInt(SaveKeys.ProfileVersion, Version);
        EnsureObjectiveSlots();
        PlayerPrefs.Save();
    }

    public IEnumerable<string> ProfileKeys()
    {
        yield return SaveKeys.Coins;
        yield return SaveKeys.Wins;
        yield return SaveKeys.Losses;
        yield return SaveKeys.MatchesPlayed;
        yield return SaveKeys.WinStreak;
        yield return SaveKeys.LossStreak;
        yield return SaveKeys.Division;
        yield return SaveKeys.Stage;
        yield return SaveKeys.Stars;
        yield return SaveKeys.ChampionStars;
        yield return SaveKeys.BestStageReached;
        yield return SaveKeys.ObjectiveIndex;
        yield return SaveKeys.SelectedCue;
        yield return SaveKeys.PendingMatch;
        yield return SaveKeys.RewardedDay;
        yield return SaveKeys.StarShieldsToday;
        yield return SaveKeys.StarterPackShows;
        yield return SaveKeys.StarterPackTapped;
        yield return SaveKeys.StarterPackNotify;
        yield return SaveKeys.OnlineTapped;
        yield return SaveKeys.OnlineNotify;
        yield return SaveKeys.SurveyDone;
        yield return SaveKeys.SurveyScore;
        yield return SaveKeys.SurveyMissing;
        for (int slot = 0; slot < ObjectiveCatalog.Slots; slot++)
        {
            yield return SaveKeys.ObjectiveSlot(slot);
            yield return SaveKeys.ObjectiveProgress(slot);
        }
        foreach (CueDefinition cue in _catalog.Cues)
        {
            yield return SaveKeys.CueOwned(cue.Id);
        }
    }
    #endregion

    #region Rank
    public RankState Rank
    {
        get
        {
            int division = PlayerPrefs.GetInt(SaveKeys.Division, 0);
            bool champion = division >= _divisions.Count;
            return new RankState
            {
                Division = Mathf.Min(division, _divisions.Count - 1),
                Stage = champion ? DivisionCatalog.StagesPerDivision - 1 : PlayerPrefs.GetInt(SaveKeys.Stage, 0),
                Stars = champion ? 0 : PlayerPrefs.GetInt(SaveKeys.Stars, 0),
                ChampionStars = PlayerPrefs.GetInt(SaveKeys.ChampionStars, 0),
                IsChampion = champion
            };
        }
    }

    public DivisionDefinition CurrentDivision => _divisions.At(Rank.Division);

    // v4 V4: the 8-ball rule the player plays now (Grand Champion has its own) and its card text.
    public RuleSetId CurrentRules => Rank.IsChampion ? _divisions.ChampionRules : CurrentDivision.Rules;
    public string CurrentRuleText => Rank.IsChampion ? _divisions.ChampionRuleText : CurrentDivision.RuleText;

    public void SetRank(RankState rank)
    {
        PlayerPrefs.SetInt(SaveKeys.Division, rank.IsChampion ? _divisions.Count : rank.Division);
        PlayerPrefs.SetInt(SaveKeys.Stage, rank.Stage);
        PlayerPrefs.SetInt(SaveKeys.Stars, rank.Stars);
        PlayerPrefs.SetInt(SaveKeys.ChampionStars, rank.ChampionStars);
        PlayerPrefs.SetInt(SaveKeys.BestStageReached, Mathf.Max(PlayerPrefs.GetInt(SaveKeys.BestStageReached, 0), rank.StageIndex));
    }

    // Whether losing right now would cost a star or a stage (the Star Shield offer, v3 V12).
    public bool LossWouldCost => RankLadder.LossCosts(_divisions, Rank);
    #endregion

    #region Objectives
    public ObjectiveDefinition ObjectiveIn(int slot) => _objectives.Get(PlayerPrefs.GetString(SaveKeys.ObjectiveSlot(slot), ""));

    public int ObjectiveProgress(int slot) => PlayerPrefs.GetInt(SaveKeys.ObjectiveProgress(slot), 0);

    // Empty slots take the next objectives on the list.
    private void EnsureObjectiveSlots()
    {
        for (int slot = 0; slot < ObjectiveCatalog.Slots; slot++)
        {
            if (ObjectiveIn(slot) == null)
                FillSlot(slot);
        }
    }

    private void FillSlot(int slot)
    {
        int next = PlayerPrefs.GetInt(SaveKeys.ObjectiveIndex, 0);
        if (next >= _objectives.Objectives.Count)
        {
            PlayerPrefs.SetString(SaveKeys.ObjectiveSlot(slot), "");
            PlayerPrefs.SetInt(SaveKeys.ObjectiveProgress(slot), 0);
            return;
        }
        ObjectiveDefinition objective = _objectives.Objectives[next];
        PlayerPrefs.SetInt(SaveKeys.ObjectiveIndex, next + 1);
        PlayerPrefs.SetString(SaveKeys.ObjectiveSlot(slot), objective.Id);
        PlayerPrefs.SetInt(SaveKeys.ObjectiveProgress(slot), ObjectiveRules.Initial(objective, Rank));
    }

    // CLAIM: the reward is paid and the next objective takes the slot.
    public bool TryClaimObjective(int slot, out int reward)
    {
        reward = 0;
        ObjectiveDefinition objective = ObjectiveIn(slot);
        if (objective == null || ObjectiveProgress(slot) < objective.Target)
            return false;
        reward = objective.Reward;
        PlayerPrefs.SetInt(SaveKeys.Coins, Coins + reward);
        FillSlot(slot);
        PlayerPrefs.Save();
        RaiseChanged();
        return true;
    }
    #endregion

    #region Queries
    public bool IsOwned(CueDefinition cue) =>
        cue.IsStarter || PlayerPrefs.GetInt(SaveKeys.CueOwned(cue.Id), 0) == 1;

    public CueState GetState(CueDefinition cue) => IsOwned(cue) ? CueState.Owned : CueState.ForSale;

    // The cheapest cue not owned yet.
    public CueDefinition NextCue
    {
        get
        {
            CueDefinition best = null;
            foreach (CueDefinition cue in _catalog.Cues)
            {
                if (!IsOwned(cue) && (best == null || cue.Price < best.Price))
                    best = cue;
            }
            return best;
        }
    }

    public ProfileSnapshot GetSnapshot(string justPurchasedId = null)
    {
        int coins = Coins;
        string selectedId = SelectedCueId;
        float maxGuide = 0f;
        float maxWindow = 0f;
        foreach (CueDefinition cue in _catalog.Cues)
        {
            maxGuide = Mathf.Max(maxGuide, cue.ObjectGuideDiameters);
            maxWindow = Mathf.Max(maxWindow, ReleaseWindowScore(cue));
        }

        var snapshot = new ProfileSnapshot
        {
            Coins = coins,
            Wins = Wins,
            Losses = Losses,
            SelectedCueId = selectedId,
            JustPurchasedId = justPurchasedId,
            Rank = RankView(Rank)
        };

        int selectedOverall = CueRatings.Overall(_catalog.Get(selectedId));
        CueDefinition next = NextCue;
        foreach (CueDefinition cue in _catalog.Cues)
        {
            var view = new CueViewData
            {
                Id = cue.Id,
                Name = cue.Name,
                Sprite = cue.Sprite,
                State = GetState(cue),
                IsSelected = cue.Id == selectedId,
                Price = cue.Price,
                CoinsShort = Mathf.Max(0, cue.Price - coins),
                Aim = CueRatings.Aim(cue),
                Control = CueRatings.Control(cue),
                Timing = CueRatings.Timing(cue),
                Overall = CueRatings.Overall(cue),
                OverallVsSelected = CueRatings.Overall(cue) - selectedOverall,
                RecommendedFor = RecommendedFor(cue.Id),
                GuideDiameters = cue.ObjectGuideDiameters,
                AimLine01 = maxGuide > 0f ? cue.ObjectGuideDiameters / maxGuide : 0f,
                ReleaseWindow01 = maxWindow > 0f ? ReleaseWindowScore(cue) / maxWindow : 0f
            };
            snapshot.Cues.Add(view);
            if (next != null && cue.Id == next.Id)
                snapshot.NextCue = view;
        }

        for (int slot = 0; slot < ObjectiveCatalog.Slots; slot++)
        {
            ObjectiveDefinition objective = ObjectiveIn(slot);
            if (objective != null)
                snapshot.Objectives.Add(ObjectiveView(slot, objective, ObjectiveProgress(slot)));
        }
        return snapshot;
    }

    private static ObjectiveViewData ObjectiveView(int slot, ObjectiveDefinition objective, int progress) => new()
    {
        Slot = slot,
        Id = objective.Id,
        Text = objective.Text,
        Progress = progress,
        Target = objective.Target,
        Reward = objective.Reward
    };

    // The Ranked Road (v3 V12): every stage from Bronze III up to Grand Champion, and where the player is.
    public RoadViewData RoadView()
    {
        RankState rank = Rank;
        var road = new RoadViewData { CurrentIndex = rank.StageIndex, ChampionStars = rank.ChampionStars };
        for (int d = 0; d < _divisions.Count; d++)
        {
            DivisionDefinition division = _divisions.At(d);
            CueDefinition botCue = _catalog.Get(division.BotCueId);
            for (int stage = 0; stage < DivisionCatalog.StagesPerDivision; stage++)
            {
                int index = d * DivisionCatalog.StagesPerDivision + stage;
                road.Stages.Add(new RoadStageViewData
                {
                    Label = RankLadder.Label(_divisions, new RankState { Division = d, Stage = stage }),
                    DivisionName = division.Name,
                    VenueName = division.VenueName,
                    VenueThumbnail = division.Venue != null ? division.Venue.Thumbnail : null,
                    BotCueName = botCue != null ? botCue.Name : "",
                    StarsPerStage = division.StarsPerStage,
                    Stars = index == road.CurrentIndex ? rank.Stars : 0,
                    IsCheckpoint = stage == 0,
                    State = index < road.CurrentIndex ? RoadStageState.Done : index == road.CurrentIndex ? RoadStageState.Current : RoadStageState.Ahead,
                    Color = division.Color
                });
            }
        }
        DivisionDefinition last = _divisions.At(_divisions.Count - 1);
        road.Stages.Add(new RoadStageViewData
        {
            Label = "GRAND CHAMPION",
            DivisionName = "Grand Champion",
            VenueName = last.VenueName,
            VenueThumbnail = last.Venue != null ? last.Venue.Thumbnail : null,
            BotCueName = _catalog.Get(last.BotCueId)?.Name ?? "",
            IsCheckpoint = true,
            IsChampion = true,
            Stars = rank.ChampionStars,
            State = rank.IsChampion ? RoadStageState.Current : RoadStageState.Ahead,
            Color = UnityEngine.Color.Lerp(last.Color, new Color(1f, 0.84f, 0.3f), 0.6f)
        });
        return road;
    }

    public RankViewData RankView(RankState rank)
    {
        DivisionDefinition division = _divisions.At(rank.Division);
        int toNext = RankLadder.StarsToNext(_divisions, rank, out bool checkpoint, out string nextLabel);
        return new RankViewData
        {
            Label = RankLadder.Label(_divisions, rank),
            DivisionName = rank.IsChampion ? "Grand Champion" : division.Name,
            VenueName = division.VenueName,
            Division = rank.Division,
            Stage = rank.Stage,
            Stars = rank.Stars,
            StarsPerStage = division.StarsPerStage,
            StarsToNext = toNext,
            NextLabel = nextLabel,
            NextIsCheckpoint = checkpoint,
            IsChampion = rank.IsChampion,
            ChampionStars = rank.ChampionStars,
            Color = division.Color
        };
    }

    // The division whose bot plays this cue (v3 V11: "Recommended for Gold").
    private string RecommendedFor(string cueId)
    {
        foreach (DivisionDefinition division in _divisions.Divisions)
        {
            if (division.BotCueId == cueId)
                return division.Name;
        }
        return null;
    }

    // Wider green/yellow and a slower bar both make the release easier.
    private static float ReleaseWindowScore(CueDefinition cue) =>
        (cue.GreenHalf + cue.YellowWidth * 0.5f) * cue.OscillationPeriod;
    #endregion

    #region Match
    // Set while a match is running: killing the app mid-match counts as a loss on the next boot.
    public void SetPendingMatch(bool pending)
    {
        PlayerPrefs.SetInt(SaveKeys.PendingMatch, pending ? 1 : 0);
        PlayerPrefs.Save();
    }

    // A finished match (v3 V10/V12): stars by the ladder's rules, coins for the division, objectives move on.
    public MatchOutcome ApplyMatchResult(bool won, MatchStats stats, bool shield = false)
    {
        stats ??= new MatchStats();
        stats.Won = won;
        var outcome = new MatchOutcome { CoinsBefore = Coins };
        CueDefinition next = NextCue;
        bool couldAfford = next != null && Coins >= next.Price;

        int winStreak = won ? WinStreak + 1 : 0;
        PlayerPrefs.SetInt(SaveKeys.WinStreak, winStreak);
        PlayerPrefs.SetInt(SaveKeys.LossStreak, won ? 0 : LossStreak + 1);
        PlayerPrefs.SetInt(SaveKeys.MatchesPlayed, MatchesPlayed + 1);
        if (won)
            PlayerPrefs.SetInt(SaveKeys.Wins, Wins + 1);
        else
            PlayerPrefs.SetInt(SaveKeys.Losses, Losses + 1);

        RankState before = Rank;
        RankChange change = RankLadder.Apply(_divisions, before, won, winStreak, shield);
        SetRank(change.After);
        outcome.Rank = change;
        outcome.RankBefore = RankLadder.Label(_divisions, before);
        outcome.RankAfter = RankLadder.Label(_divisions, change.After);

        int coins = won ? (before.IsChampion ? _divisions.ChampionWinCoins : _divisions.At(before.Division).WinCoins) : _economy.LossCoins;
        outcome.CoinsFromMatch = coins;
        _lastRankBefore = before;
        _lastMatchCoins = coins;
        _lastShieldable = !won && change.StarsGained < 0;
        _lastDoubleable = won && coins > 0;
        outcome.StarShieldAvailable = _lastShieldable && StarShieldsLeftToday > 0;
        outcome.DoubleCoinsAvailable = _lastDoubleable;
        PlayerPrefs.SetInt(SaveKeys.Coins, Coins + coins);

        for (int slot = 0; slot < ObjectiveCatalog.Slots; slot++)
        {
            ObjectiveDefinition objective = ObjectiveIn(slot);
            if (objective == null)
                continue;
            int progress = ObjectiveProgress(slot);
            int advanced = ObjectiveRules.Advance(objective, progress, stats, before, change.After, winStreak);
            PlayerPrefs.SetInt(SaveKeys.ObjectiveProgress(slot), advanced);
            if (progress < objective.Target && advanced >= objective.Target)
                outcome.ObjectivesCompleted.Add(ObjectiveView(slot, objective, advanced));
        }

        PlayerPrefs.SetInt(SaveKeys.PendingMatch, 0);
        PlayerPrefs.Save();
        outcome.CoinsAfter = Coins;
        if (next != null && !couldAfford && Coins >= next.Price)
            outcome.CueAffordable = next.Name;
        RaiseChanged();
        return outcome;
    }

    // A match left running (app killed) counts as a loss on the next boot, with the usual rules for stars.
    public bool ApplyPendingMatch()
    {
        if (PlayerPrefs.GetInt(SaveKeys.PendingMatch, 0) != 1)
            return false;

        ApplyMatchResult(false, new MatchStats());
        _lastShieldable = _lastDoubleable = false;
        _abandonedNoticePending = true;
        Debug.Log("ProfileService: last match was abandoned, counted as a loss");
        return true;
    }

    // MenuController shows the "abandoned" toast once.
    public bool ConsumeAbandonedNotice()
    {
        bool pending = _abandonedNoticePending;
        _abandonedNoticePending = false;
        return pending;
    }
    #endregion

    #region Rewarded (simulated), fake doors, survey (v3 V12)
    private string Today => Clock().ToString("yyyyMMdd");

    public int StarShieldsLeftToday =>
        PlayerPrefs.GetString(SaveKeys.RewardedDay, "") == Today
            ? Mathf.Max(0, _economy.StarShieldsPerDay - PlayerPrefs.GetInt(SaveKeys.StarShieldsToday, 0))
            : _economy.StarShieldsPerDay;

    // The last loss gives back what it took (once per match, StarShieldsPerDay a day).
    public bool TryStarShield()
    {
        if (!_lastShieldable || StarShieldsLeftToday <= 0)
            return false;
        int used = PlayerPrefs.GetString(SaveKeys.RewardedDay, "") == Today ? PlayerPrefs.GetInt(SaveKeys.StarShieldsToday, 0) : 0;
        PlayerPrefs.SetString(SaveKeys.RewardedDay, Today);
        PlayerPrefs.SetInt(SaveKeys.StarShieldsToday, used + 1);
        SetRank(_lastRankBefore);
        _lastShieldable = false;
        PlayerPrefs.Save();
        RaiseChanged();
        return true;
    }

    // The last win's coins once more (not the objectives' rewards); once per match.
    public bool TryDoubleCoins(out int added)
    {
        added = 0;
        if (!_lastDoubleable)
            return false;
        added = _lastMatchCoins * Mathf.Max(1, _economy.DoubleCoinsPerMatch);
        PlayerPrefs.SetInt(SaveKeys.Coins, Coins + added);
        _lastDoubleable = false;
        PlayerPrefs.Save();
        RaiseChanged();
        return true;
    }

    private void HandleRewardedAdFinished(RewardedPlacement placement, bool rewarded)
    {
        if (!rewarded)
            return;
        if (placement == RewardedPlacement.StarShield)
            TryStarShield();
        else if (placement == RewardedPlacement.DoubleCoins)
            TryDoubleCoins(out _);
    }

    // On the menu: at the Silver checkpoint or after StarterPackAfterMatches matches; once a session,
    // StarterPackMaxShows in all, never right after StarterPackSkipAfterLosses losses in a row.
    public bool StarterPackDue =>
        !_starterPackShownThisSession
        && PlayerPrefs.GetInt(SaveKeys.StarterPackShows, 0) < _economy.StarterPackMaxShows
        && LossStreak < _economy.StarterPackSkipAfterLosses
        && (Rank.Division >= 1 || Rank.IsChampion || MatchesPlayed >= _economy.StarterPackAfterMatches);

    public bool SurveyDue => PlayerPrefs.GetInt(SaveKeys.SurveyDone, 0) == 0 && MatchesPlayed >= _economy.SurveyAfterMatches;

    public bool NotifyOn(FakeDoorId id) => PlayerPrefs.GetInt(id == FakeDoorId.StarterPack ? SaveKeys.StarterPackNotify : SaveKeys.OnlineNotify, 0) == 1;

    private void HandleFakeDoor(FakeDoorId id, FakeDoorAction action)
    {
        bool pack = id == FakeDoorId.StarterPack;
        switch (action)
        {
            case FakeDoorAction.Shown when pack:
                _starterPackShownThisSession = true;
                PlayerPrefs.SetInt(SaveKeys.StarterPackShows, PlayerPrefs.GetInt(SaveKeys.StarterPackShows, 0) + 1);
                break;
            case FakeDoorAction.Tapped:
                string tapped = pack ? SaveKeys.StarterPackTapped : SaveKeys.OnlineTapped;
                PlayerPrefs.SetInt(tapped, PlayerPrefs.GetInt(tapped, 0) + 1);
                break;
            case FakeDoorAction.NotifyOn:
            case FakeDoorAction.NotifyOff:
                PlayerPrefs.SetInt(pack ? SaveKeys.StarterPackNotify : SaveKeys.OnlineNotify, action == FakeDoorAction.NotifyOn ? 1 : 0);
                break;
        }
        PlayerPrefs.Save();
        Debug.Log($"ProfileService: fake door {id} {action}");
    }

    private void HandleSurveyAnswered(int score, int missing)
    {
        PlayerPrefs.SetInt(SaveKeys.SurveyDone, 1);
        PlayerPrefs.SetInt(SaveKeys.SurveyScore, score);
        PlayerPrefs.SetInt(SaveKeys.SurveyMissing, missing);
        PlayerPrefs.Save();
    }

    private void HandleSurveySkipped()
    {
        PlayerPrefs.SetInt(SaveKeys.SurveyDone, 1);
        PlayerPrefs.Save();
    }
    #endregion

    #region Inventory actions
    public bool TrySelect(string cueId, out string reason)
    {
        CueDefinition cue = _catalog.Get(cueId);
        if (cue == null || !IsOwned(cue))
        {
            reason = "You don't own this cue.";
            return false;
        }

        PlayerPrefs.SetString(SaveKeys.SelectedCue, cue.Id);
        PlayerPrefs.Save();
        reason = null;
        return true;
    }

    public bool TryBuy(string cueId, out string reason)
    {
        CueDefinition cue = _catalog.Get(cueId);
        reason = null;
        if (cue == null)
            reason = "Unknown cue.";
        else if (IsOwned(cue))
            reason = "You already own this cue.";
        else if (Coins < cue.Price)
            reason = $"Need {cue.Price - Coins} more coins.";

        if (reason != null)
            return false;

        PlayerPrefs.SetInt(SaveKeys.Coins, Coins - cue.Price);
        PlayerPrefs.SetInt(SaveKeys.CueOwned(cue.Id), 1);
        PlayerPrefs.Save();
        return true;
    }

    private void HandleObjectiveClaimRequested(int slot)
    {
        if (TryClaimObjective(slot, out int reward))
            GameEvents.RaiseToastRequested(new ToastMessage($"+{reward} coins", ToastStyle.Good));
    }

    private void HandleCueSelectRequested(string cueId)
    {
        if (TrySelect(cueId, out string reason))
            RaiseChanged();
        else
            GameEvents.RaisePurchaseFailed(reason);
    }

    // A new cue isn't selected automatically: its card asks to SELECT it (pulse + toast).
    private void HandleCueBuyRequested(string cueId)
    {
        if (!TryBuy(cueId, out string reason))
        {
            GameEvents.RaisePurchaseFailed(reason);
            return;
        }

        GameEvents.RaiseProfileChanged(GetSnapshot(cueId));
        GameEvents.RaiseToastRequested(new ToastMessage($"{_catalog.Get(cueId).Name} purchased!", ToastStyle.Good));
    }
    #endregion

    public void RaiseChanged() => GameEvents.RaiseProfileChanged(GetSnapshot());
}
