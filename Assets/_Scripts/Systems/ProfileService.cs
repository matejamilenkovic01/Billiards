using System.Collections.Generic;
using UnityEngine;

// The only owner of the player profile in PlayerPrefs (v3 V12, v4 V14, profile v3): coins, rank on the ladder, the
// objective sets, cues, the pending match and one-time flags.
public class ProfileService : SingletonPersistent<ProfileService>
{
    public const int Version = 3;

    [SerializeField] private CueCatalog _catalog;
    [SerializeField] private DivisionCatalog _divisions;
    [SerializeField] private ObjectiveCatalog _objectives;
    [SerializeField] private EconomyConfig _economy;

    private bool _abandonedNoticePending;

    // The day the counters of "today" run in (UTC); the checks move it to test the next day.
    public static System.Func<System.DateTime> Clock = () => System.DateTime.UtcNow;

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
    }

    private void OnDisable()
    {
        GameEvents.OnCueSelectRequested -= HandleCueSelectRequested;
        GameEvents.OnCueBuyRequested -= HandleCueBuyRequested;
        GameEvents.OnObjectiveClaimRequested -= HandleObjectiveClaimRequested;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnTutorialCompleted -= HandleTutorialCompleted;
    }

    private void HandleTutorialCompleted(bool skipped)
    {
        PlayerPrefs.SetInt(SaveKeys.TutorialDone, 1);
        PlayerPrefs.Save();
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        // A try shot (v4 V9) leaves the profile as it is.
        if (MatchSetup.Mode == MatchMode.Practice || !feedback.IsPlayer || ShotsTaken >= ShotsTakenCap)
            return;
        PlayerPrefs.SetInt(SaveKeys.ShotsTaken, ShotsTaken + 1);
        PlayerPrefs.Save();
    }

    #region Version
    // Profile v1 (coins / wins / cue energy) had no real users: anything older than v2 starts fresh (sound settings
    // and the one-time flags stay). v2 -> v3 keeps coins, rank, cues and the flags, drops the probes' keys and the
    // slot objectives; the sets of every division reached open with progress 0, and no reward is paid back (v4 V14).
    public void Migrate()
    {
        int version = PlayerPrefs.GetInt(SaveKeys.ProfileVersion, 0);
        if (version >= Version)
        {
            EnsureInstallDay();
            return;
        }
        if (version < 2)
        {
            ResetProfile();
            Debug.Log("ProfileService: profile migrated to v3 (fresh start)");
            return;
        }
        foreach (string key in SaveKeys.Retired)
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.SetInt(SaveKeys.RewardGrantedStage, PlayerPrefs.GetInt(SaveKeys.BestStageReached, 0));
        PlayerPrefs.SetInt(SaveKeys.ProfileVersion, Version);
        EnsureInstallDay();
        PlayerPrefs.Save();
        Debug.Log("ProfileService: profile migrated v2 -> v3");
    }

    // Everything the profile owns except volumes, tutorial / rules / hint flags.
    public void ResetProfile()
    {
        foreach (string key in ProfileKeys())
            PlayerPrefs.DeleteKey(key);
        foreach (string key in SaveKeys.Retired)
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.SetInt(SaveKeys.Coins, _economy.StartCoins);
        PlayerPrefs.SetInt(SaveKeys.ProfileVersion, Version);
        EnsureInstallDay();
        PlayerPrefs.Save();
    }

    private void EnsureInstallDay()
    {
        if (!PlayerPrefs.HasKey(SaveKeys.InstallDay))
            PlayerPrefs.SetString(SaveKeys.InstallDay, Today);
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
        yield return SaveKeys.RewardGrantedStage;
        yield return SaveKeys.ChampionMilestones;
        yield return SaveKeys.DivisionsSeen;
        yield return SaveKeys.MatchesToday;
        yield return SaveKeys.MatchesTodayDate;
        yield return SaveKeys.SelectedCue;
        yield return SaveKeys.PendingMatch;
        foreach (ObjectiveSet set in _objectives.Sets)
        {
            yield return SaveKeys.ObjectiveSetBonus(set.DivisionId);
            foreach (ObjectiveDefinition objective in set.Objectives)
            {
                yield return SaveKeys.ObjectiveProgress(objective.Id);
                yield return SaveKeys.ObjectiveClaimed(objective.Id);
            }
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

    // Whether losing right now would cost a star or a stage.
    public bool LossWouldCost => RankLadder.LossCosts(_divisions, Rank);
    #endregion

    #region Objectives
    public int ObjectiveProgress(string id) => PlayerPrefs.GetInt(SaveKeys.ObjectiveProgress(id), 0);

    public bool IsClaimed(string id) => PlayerPrefs.GetInt(SaveKeys.ObjectiveClaimed(id), 0) == 1;

    // How many sets are open: entering a division opens its set, and it stays open (v4 V12).
    public int UnlockedSets =>
        Mathf.Min(_objectives.Sets.Count, PlayerPrefs.GetInt(SaveKeys.BestStageReached, 0) / DivisionCatalog.StagesPerDivision + 1);

    public bool IsUnlocked(string id)
    {
        int division = _objectives.DivisionOf(id);
        return division >= 0 && division < UnlockedSets;
    }

    // The objectives that count now: all of the open sets that are not claimed yet, oldest set first.
    public IEnumerable<ObjectiveDefinition> ActiveObjectives()
    {
        int open = UnlockedSets;
        for (int s = 0; s < open; s++)
        {
            foreach (ObjectiveDefinition objective in _objectives.Sets[s].Objectives)
            {
                if (!IsClaimed(objective.Id))
                    yield return objective;
            }
        }
    }

    // The slots on the menu (v4 V12): finished ones first (they wait for CLAIM), then in order; three at most.
    public List<ObjectiveDefinition> VisibleObjectives()
    {
        var done = new List<ObjectiveDefinition>();
        var rest = new List<ObjectiveDefinition>();
        foreach (ObjectiveDefinition objective in ActiveObjectives())
            (ObjectiveProgress(objective.Id) >= objective.Target ? done : rest).Add(objective);
        done.AddRange(rest);
        if (done.Count > ObjectiveCatalog.Slots)
            done.RemoveRange(ObjectiveCatalog.Slots, done.Count - ObjectiveCatalog.Slots);
        return done;
    }

    public struct ObjectiveClaim
    {
        public int Reward;
        public int SetBonus;        // paid with the last claim of a set
        public int SetDivision;     // the set that was completed, or -1
    }

    // CLAIM: the reward is paid and the next objective takes the slot; the last claim of a set also pays its bonus.
    public bool TryClaimObjective(string id, out ObjectiveClaim claim)
    {
        claim = new ObjectiveClaim { SetDivision = -1 };
        ObjectiveDefinition objective = _objectives.Get(id);
        if (objective == null || !IsUnlocked(id) || IsClaimed(id) || ObjectiveProgress(id) < objective.Target)
            return false;
        claim.Reward = objective.Reward;
        PlayerPrefs.SetInt(SaveKeys.ObjectiveClaimed(id), 1);

        int division = _objectives.DivisionOf(id);
        ObjectiveSet set = _objectives.Sets[division];
        string bonusKey = SaveKeys.ObjectiveSetBonus(set.DivisionId);
        if (PlayerPrefs.GetInt(bonusKey, 0) == 0 && set.Objectives.TrueForAll(o => IsClaimed(o.Id)))
        {
            claim.SetBonus = set.Bonus;
            claim.SetDivision = division;
            PlayerPrefs.SetInt(bonusKey, 1);
        }
        PlayerPrefs.SetInt(SaveKeys.Coins, Coins + claim.Reward + claim.SetBonus);
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

        CueDefinition selectedCue = _catalog.Get(selectedId);
        int selectedOverall = CueRatings.Overall(selectedCue);
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
                Power = CueRatings.Power(cue),
                AimVsSelected = CueRatings.Aim(cue) - CueRatings.Aim(selectedCue),
                ControlVsSelected = CueRatings.Control(cue) - CueRatings.Control(selectedCue),
                TimingVsSelected = CueRatings.Timing(cue) - CueRatings.Timing(selectedCue),
                PowerVsSelected = CueRatings.Power(cue) - CueRatings.Power(selectedCue),
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

        foreach (ObjectiveDefinition objective in VisibleObjectives())
            snapshot.Objectives.Add(ObjectiveView(objective));
        snapshot.CueGap = CueGap();
        return snapshot;
    }

    private ObjectiveViewData ObjectiveView(ObjectiveDefinition objective) => new()
    {
        Id = objective.Id,
        Division = _objectives.DivisionOf(objective.Id),
        Playable = ObjectiveRules.CountsInLoss(objective.Kind),
        Claimed = IsClaimed(objective.Id),
        Locked = !IsUnlocked(objective.Id),
        Text = objective.Text,
        Progress = ObjectiveProgress(objective.Id),
        Target = objective.Target,
        Reward = objective.Reward
    };

    // "Gold opponents use the Pro Cue" (v4 V12): the division's cue is better than the best the player owns.
    private CueGapData CueGap()
    {
        DivisionDefinition division = CurrentDivision;
        CueDefinition botCue = _catalog.Get(division.BotCueId);
        if (botCue == null || IsOwned(botCue))
            return null;
        int best = 0;
        foreach (CueDefinition cue in _catalog.Cues)
        {
            if (IsOwned(cue))
                best = Mathf.Max(best, CueRatings.Overall(cue));
        }
        if (CueRatings.Overall(botCue) <= best)
            return null;
        int playable = 0;
        foreach (ObjectiveDefinition objective in ActiveObjectives())
        {
            if (ObjectiveRules.CountsInLoss(objective.Kind))
                playable++;
        }
        return new CueGapData
        {
            DivisionName = division.Name,
            CueId = botCue.Id,
            CueName = botCue.Name,
            Price = botCue.Price,
            Coins = Coins,
            PlayObjectivesLeft = playable
        };
    }

    // DIVISIONS (v4 V11): every division as a block with its stages, the rule, the venue, the rewards; Grand
    // Champion last. A block is Done once the player is past it, Current while in it, else Ahead (locked).
    public DivisionsViewData DivisionsView()
    {
        RankState rank = Rank;
        int granted = PlayerPrefs.GetInt(SaveKeys.RewardGrantedStage, 0);
        int currentBlock = rank.IsChampion ? _divisions.Count : rank.Division;
        var view = new DivisionsViewData { CurrentIndex = currentBlock };
        for (int d = 0; d < _divisions.Count; d++)
        {
            DivisionDefinition division = _divisions.At(d);
            CueDefinition botCue = _catalog.Get(division.BotCueId);
            var block = new DivisionBlockData
            {
                Index = d,
                Name = $"{division.Name.ToUpperInvariant()} DIVISION",
                Color = division.Color,
                VenueName = division.VenueName,
                VenueThumbnail = division.Venue != null ? division.Venue.Thumbnail : null,
                BotCueName = botCue != null ? botCue.Name : "",
                Rules = division.Rules,
                RuleText = division.RuleText,
                EntryReward = d > 0 ? division.DivisionReward : 0,
                EntryPaid = granted >= d * DivisionCatalog.StagesPerDivision,
                State = d < currentBlock ? RoadStageState.Done : d == currentBlock ? RoadStageState.Current : RoadStageState.Ahead,
                LockText = d > 0 ? $"Reach {_divisions.At(d - 1).Name.ToUpperInvariant()} I" : ""
            };
            for (int stage = 0; stage < DivisionCatalog.StagesPerDivision; stage++)
            {
                int index = d * DivisionCatalog.StagesPerDivision + stage;
                block.Stages.Add(new DivisionStageData
                {
                    Label = RankLadder.Label(_divisions, new RankState { Division = d, Stage = stage }),
                    StarsPerStage = division.StarsPerStage,
                    Stars = index == rank.StageIndex ? rank.Stars : 0,
                    Reward = _divisions.RewardForStage(index, out _),
                    RewardPaid = granted >= index,
                    IsCheckpoint = stage == 0,
                    State = index < rank.StageIndex ? RoadStageState.Done : index == rank.StageIndex ? RoadStageState.Current : RoadStageState.Ahead
                });
            }
            view.Blocks.Add(block);
        }
        DivisionDefinition last = _divisions.At(_divisions.Count - 1);
        view.Blocks.Add(new DivisionBlockData
        {
            Index = _divisions.Count,
            Name = "GRAND CHAMPION",
            Color = Color.Lerp(last.Color, new Color(1f, 0.84f, 0.3f), 0.6f),
            VenueName = last.VenueName,
            VenueThumbnail = last.Venue != null ? last.Venue.Thumbnail : null,
            BotCueName = _catalog.Get(last.BotCueId)?.Name ?? "",
            Rules = _divisions.ChampionRules,
            RuleText = _divisions.ChampionRuleText,
            EntryReward = _divisions.ChampionEntryReward,
            EntryPaid = granted >= _divisions.Count * DivisionCatalog.StagesPerDivision,
            IsChampion = true,
            ChampionStars = rank.ChampionStars,
            State = rank.IsChampion ? RoadStageState.Current : RoadStageState.Ahead,
            LockText = $"Reach {last.Name.ToUpperInvariant()} I"
        });
        return view;
    }

    // The player has looked at DIVISIONS: the NEW chip goes.
    public void MarkDivisionsSeen()
    {
        PlayerPrefs.SetInt(SaveKeys.DivisionsSeen, Mathf.Max(PlayerPrefs.GetInt(SaveKeys.DivisionsSeen, 0), Rank.IsChampion ? _divisions.Count : Rank.Division));
        PlayerPrefs.Save();
        RaiseChanged();
    }

    // ALL OBJECTIVES (v4 V12): the open sets and the next division's, locked; opens on the current division's.
    public ObjectivesViewData ObjectivesView()
    {
        int open = UnlockedSets;
        int shown = Mathf.Min(_objectives.Sets.Count, open + 1);
        var view = new ObjectivesViewData { CurrentTab = Mathf.Min(Rank.IsChampion ? _objectives.Sets.Count - 1 : Rank.Division, open - 1), CueGap = CueGap() != null };
        for (int d = 0; d < shown; d++)
        {
            ObjectiveSet set = _objectives.Sets[d];
            DivisionDefinition division = _divisions.At(d);
            var tab = new ObjectiveSetData
            {
                Division = d,
                Name = division.Name.ToUpperInvariant(),
                Color = division.Color,
                Locked = d >= open,
                LockText = $"Reach {division.Name.ToUpperInvariant()} to unlock",
                Bonus = set.Bonus,
                BonusPaid = PlayerPrefs.GetInt(SaveKeys.ObjectiveSetBonus(set.DivisionId), 0) == 1,
                Total = set.Objectives.Count
            };
            foreach (ObjectiveDefinition objective in set.Objectives)
            {
                tab.Objectives.Add(ObjectiveView(objective));
                if (IsClaimed(objective.Id))
                    tab.Claimed++;
            }
            view.Sets.Add(tab);
        }
        return view;
    }

    // The full-screen celebration of a new division or of Grand Champion (null for any other result).
    public CelebrationData CelebrationFor(RankChange change, int reward)
    {
        if (!change.NewDivision)
            return null;
        RankState after = change.After;
        DivisionDefinition division = _divisions.At(after.Division);
        return new CelebrationData
        {
            Kind = after.IsChampion ? CelebrationKind.ChampionUp : CelebrationKind.DivisionUp,
            Title = after.IsChampion ? "GRAND CHAMPION" : $"{division.Name.ToUpperInvariant()} DIVISION",
            Color = RankView(after).Color,
            Before = RankView(change.Before),
            After = RankView(after),
            Rules = after.IsChampion ? _divisions.ChampionRules : division.Rules,
            RuleText = after.IsChampion ? _divisions.ChampionRuleText : division.RuleText,
            VenueName = division.VenueName,
            VenueThumbnail = division.Venue != null ? division.Venue.Thumbnail : null,
            Reward = reward
        };
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
            IsNew = PlayerPrefs.GetInt(SaveKeys.DivisionsSeen, 0) < (rank.IsChampion ? _divisions.Count : rank.Division),
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

    // A finished match (v3 V10/V12, v4 V11/V12): stars by the ladder's rules, the division's coins, a reward for a
    // stage or division reached the first time, and every open objective moves on.
    public MatchOutcome ApplyMatchResult(bool won, MatchStats stats)
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
        CountMatchToday();
        if (won)
            PlayerPrefs.SetInt(SaveKeys.Wins, Wins + 1);
        else
            PlayerPrefs.SetInt(SaveKeys.Losses, Losses + 1);

        RankState before = Rank;
        RankChange change = RankLadder.Apply(_divisions, before, won, winStreak);
        SetRank(change.After);
        outcome.Rank = change;
        outcome.RankBefore = RankLadder.Label(_divisions, before);
        outcome.RankAfter = RankLadder.Label(_divisions, change.After);

        outcome.CoinsFromMatch = won ? _divisions.WinCoinsFor(before) : _divisions.LossCoinsFor(before);
        PayRankRewards(change.After, outcome);
        PlayerPrefs.SetInt(SaveKeys.Coins, Coins + outcome.CoinsFromMatch + outcome.RankRewardCoins);

        // Every open objective moves, not only the three on the menu. A set that opened with this match counts it.
        foreach (ObjectiveDefinition objective in ActiveObjectives())
        {
            int progress = ObjectiveProgress(objective.Id);
            if (progress >= objective.Target)
                continue;
            int advanced = ObjectiveRules.Advance(objective, progress, stats, before, change.After, winStreak);
            if (advanced == progress)
                continue;
            PlayerPrefs.SetInt(SaveKeys.ObjectiveProgress(objective.Id), advanced);
            if (advanced >= objective.Target)
                outcome.ObjectivesCompleted.Add(ObjectiveView(objective));
        }

        PlayerPrefs.SetInt(SaveKeys.PendingMatch, 0);
        PlayerPrefs.Save();
        outcome.CoinsAfter = Coins;
        if (next != null && !couldAfford && Coins >= next.Price)
            outcome.CueAffordable = next.Name;
        RaiseChanged();
        return outcome;
    }

    // Each stage is paid once: a stage above the highest paid so far pays its division's StageReward, a new division
    // its DivisionReward; falling back and climbing again pays nothing. Grand Champion pays every N stars, once each.
    private void PayRankRewards(RankState after, MatchOutcome outcome)
    {
        int granted = PlayerPrefs.GetInt(SaveKeys.RewardGrantedStage, 0);
        int reached = after.StageIndex;
        for (int stage = granted + 1; stage <= reached; stage++)
        {
            int reward = _divisions.RewardForStage(stage, out bool division);
            if (division)
                outcome.DivisionRewardCoins += reward;
            else
                outcome.StageRewardCoins += reward;
        }
        if (reached > granted)
            PlayerPrefs.SetInt(SaveKeys.RewardGrantedStage, reached);

        if (after.IsChampion && _divisions.ChampionMilestoneStars > 0)
        {
            int due = after.ChampionStars / _divisions.ChampionMilestoneStars;
            int paid = PlayerPrefs.GetInt(SaveKeys.ChampionMilestones, 0);
            if (due > paid)
            {
                outcome.MilestoneRewardCoins = (due - paid) * _divisions.ChampionMilestoneReward;
                PlayerPrefs.SetInt(SaveKeys.ChampionMilestones, due);
            }
        }
    }

    private void CountMatchToday()
    {
        if (PlayerPrefs.GetString(SaveKeys.MatchesTodayDate, "") != Today)
        {
            PlayerPrefs.SetString(SaveKeys.MatchesTodayDate, Today);
            PlayerPrefs.SetInt(SaveKeys.MatchesToday, 0);
        }
        PlayerPrefs.SetInt(SaveKeys.MatchesToday, PlayerPrefs.GetInt(SaveKeys.MatchesToday, 0) + 1);
    }

    private static string Today => Clock().ToString("yyyyMMdd");

    // A match left running (app killed) counts as a loss on the next boot, with the usual rules for stars.
    public bool ApplyPendingMatch()
    {
        if (PlayerPrefs.GetInt(SaveKeys.PendingMatch, 0) != 1)
            return false;

        ApplyMatchResult(false, new MatchStats());
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

    private void HandleObjectiveClaimRequested(string id)
    {
        if (!TryClaimObjective(id, out ObjectiveClaim claim))
            return;
        GameEvents.RaiseToastRequested(new ToastMessage($"+{claim.Reward} coins", ToastStyle.Good));
        if (claim.SetDivision >= 0)
        {
            DivisionDefinition division = _divisions.At(claim.SetDivision);
            GameEvents.RaiseRankCelebration(RankCelebration.ObjectiveSetComplete);
            GameEvents.RaiseObjectiveSetCompleted(new CelebrationData
            {
                Kind = CelebrationKind.SetComplete,
                Title = $"{division.Name.ToUpperInvariant()} OBJECTIVES COMPLETE",
                Color = division.Color,
                Reward = claim.SetBonus
            });
        }
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
