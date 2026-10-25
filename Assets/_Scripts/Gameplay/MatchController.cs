using System.Collections;
using UnityEngine;

public enum MatchPhase
{
    Intro = 0,
    Turn = 1,
    Simulating = 2,
    Resolving = 3,
    Ended = 4
}

// Match flow: Intro -> Turn (player | bot) -> Simulating -> Resolving -> Turn | Ended.
// Owns the simulation and the rules state; shot controllers call PlaceCueBall / Shoot.
public class MatchController : Singleton<MatchController>
{
    [SerializeField] private MatchConfig _config;
    [Tooltip("Fallback when the division has no bot profile.")]
    [SerializeField] private BotProfile _botProfile;
    [SerializeField] private CueCatalog _catalog;
    [SerializeField] private DivisionCatalog _divisions;
    [SerializeField] private TableView _table;
    [SerializeField] private CueView _cue;
    [SerializeField] private BotController _bot;
    [SerializeField] private CameraRig _cameraRig;
    [Tooltip("Runs the lessons when MatchSetup.Mode is Tutorial (v3 V9).")]
    [SerializeField] private TutorialController _tutorial;
    [Tooltip("Editor testing: the bot plays both sides.")]
    [SerializeField] private bool _botVsBot;

    private PoolPhysics _physics;
    private RulesState _rules;
    private ShotPredictor _predictor;
    private System.Random _random;
    private MatchPhase _phase;
    private bool _shotInFlight;
    private int _pocketCursor;
    private int _collisionCursor;
    private CueDefinition _playerCue;
    private CueDefinition _botCue;
    private bool _paused;
    private readonly bool[] _legalTargets = new bool[BallGroups.BallCount];
    private DivisionDefinition _division;
    private OpponentDefinition _opponent;

    // What the player did, for the objectives (v3 V12): the same recorder ProgressionSim uses.
    private readonly MatchStatsRecorder _recorder = new();
    private bool _shotByPlayer;

    public MatchPhase Phase => _phase;
    public RulesState Rules => _rules;
    public BallState[] Balls => _physics.Balls;
    public TableGeometry Geometry => _table.Geometry;
    public ShotPredictor Predictor => _predictor;
    public MatchConfig Config => _config;
    public BotProfile BotProfile => _botProfile;
    public DivisionDefinition Division => _division;
    public OpponentDefinition Opponent => _opponent;
    public MatchStats Stats => _recorder.Stats;
    public bool IsTutorial => MatchSetup.Mode == MatchMode.Tutorial && _tutorial != null;
    // Eases the release bar for the player's shots (set by the tutorial per lesson).
    public ReleaseAssist ReleaseAssist { get; set; } = ReleaseAssist.None;
    private ShotFeedback _lastFeedback;
    public TableView Table => _table;
    public CueView Cue => _cue;
    public CameraRig CameraRig => _cameraRig;
    public System.Random Random => _random;
    public CueDefinition PlayerCue => _playerCue;
    public CueDefinition BotCue => _botCue;
    public bool BotVsBot => _botVsBot;
    public bool IsHumanTurn => _phase == MatchPhase.Turn && IsHumanSide(_rules.Turn);
    private bool IsHumanSide(Side side) => !_botVsBot && side == Side.Player;
    private const string PlayerName = "YOU";
    private const string OpponentName = "OPPONENT";
    public bool[] LegalTargets => _legalTargets;
    public bool IsPaused => _paused;

    protected override void Awake()
    {
        base.Awake();
        PoolSettings settings = _config.ToPoolSettings();
        _physics = new PoolPhysics(_table.Geometry, settings);
        _predictor = new ShotPredictor(_table.Geometry, settings);
        _cue.Bind(_physics.Balls, _table);
        // The division (v3 V10): set by the menu; started from the Match scene in the Editor, the player's current one.
        _division = !string.IsNullOrEmpty(MatchSetup.DivisionId) ? _divisions.Get(MatchSetup.DivisionId) : null;
        _division ??= ProfileService.Instance != null ? ProfileService.Instance.CurrentDivision : _divisions.At(0);
        _opponent = _division.Opponents.Find(o => o.Id == MatchSetup.OpponentId);
        if (_opponent == null && _division.Opponents.Count > 0)
            _opponent = _division.Opponents[0];
        if (_division.Bot != null)
            _botProfile = _division.Bot;
        _botCue = _catalog.Get(_division.BotCueId) ?? _catalog.Starter;
        _playerCue = ProfileService.Instance != null ? ProfileService.Instance.SelectedCue : _catalog.Starter;
    }

    private void OnEnable()
    {
        GameEvents.OnPauseRequested += HandlePauseRequested;
        GameEvents.OnResumeRequested += HandleResumeRequested;
        GameEvents.OnForfeitRequested += HandleForfeitRequested;
        GameEvents.OnSettingsRequested += HandleSettingsRequested;
        GameEvents.OnMenuRequested += HandleMenuRequested;
        GameEvents.OnPlayRequested += HandlePlayAgainRequested;
        GameEvents.OnCuesRequested += HandleCuesRequested;
        GameEvents.OnRulesRequested += HandleRulesRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnPauseRequested -= HandlePauseRequested;
        GameEvents.OnResumeRequested -= HandleResumeRequested;
        GameEvents.OnForfeitRequested -= HandleForfeitRequested;
        GameEvents.OnSettingsRequested -= HandleSettingsRequested;
        GameEvents.OnMenuRequested -= HandleMenuRequested;
        GameEvents.OnPlayRequested -= HandlePlayAgainRequested;
        GameEvents.OnCuesRequested -= HandleCuesRequested;
        GameEvents.OnRulesRequested -= HandleRulesRequested;
        if (_paused)
            Time.timeScale = 1f;
    }

    // Leaving the app mid-match: pause right away (a kill from the background counts as a loss on the next boot).
    private void OnApplicationPause(bool paused)
    {
        if (paused)
            HandlePauseRequested();
    }

    private void Start()
    {
        if (IsTutorial)
        {
            StartTutorial();
            return;
        }

        ProfileService profile = ProfileService.Instance;
        // No energy (v3 V11); a match left running counts as a loss on the next boot.
        if (profile != null)
            profile.SetPendingMatch(true);

        // The very first match opens with the House Rules card; the coin toss waits for it.
        if (profile != null && !profile.RulesSeen && PopupManager.Instance != null)
        {
            RulesPopup rules = PopupManager.Instance.Show<RulesPopup>(new RulesPopupData(RuleId.None));
            if (rules != null)
            {
                rules.Hidden += HandleFirstRulesHidden;
                return;
            }
        }
        StartMatch(System.Environment.TickCount);
    }

    private void HandleFirstRulesHidden(PopupBase popup)
    {
        popup.Hidden -= HandleFirstRulesHidden;
        ProfileService.Instance.MarkRulesSeen();
        StartMatch(System.Environment.TickCount);
    }

    public void StartMatch(int seed)
    {
        StopAllCoroutines();
        _bot.CancelTurn();
        _random = new System.Random(seed);
        _physics.SetBalls(RackBuilder.Build(_random, Geometry, _config.RackGap));
        _rules = RulesState.NewRack(_random.NextDouble() < 0.5 ? Side.Player : Side.Bot);
        _shotInFlight = false;
        _recorder.Reset();
        _table.ShowState(_physics.Balls);
        _cue.SetVisible(false);

        _phase = MatchPhase.Intro;
        GameEvents.RaiseMatchStarted(StartInfo(false));
        Debug.Log($"Match: started in {_division.Name} vs {(_opponent != null ? _opponent.Name : OpponentName)}, " +
                  $"{(_rules.Turn == Side.Player ? "player" : "bot")} breaks");
        StartCoroutine(AfterDelay(_config.VsIntroDuration + _config.CoinFlipDuration, BeginTurn));
    }

    // The tutorial (v3 V9): no VS intro, no coin toss, no profile; the lessons set up the table themselves.
    private void StartTutorial()
    {
        _random = new System.Random(System.Environment.TickCount);
        _rules = RulesState.NewRack(Side.Player);
        _shotInFlight = false;
        _cue.SetVisible(false);
        _phase = MatchPhase.Intro;
        GameEvents.RaiseMatchStarted(StartInfo(true));
        Debug.Log("Match: tutorial");
        _tutorial.Begin();
    }

    private MatchStartInfo StartInfo(bool tutorial) => new()
    {
            IsTutorial = tutorial,
            PlayerBreaks = _rules.Turn == Side.Player,
            PlayerName = PlayerName,
            OpponentName = tutorial ? "COACH" : _opponent != null ? _opponent.Name : OpponentName,
            Opponent = tutorial ? null : _opponent?.ToView(_division),
            Rank = !tutorial && ProfileService.Instance != null ? ProfileService.Instance.RankView(ProfileService.Instance.Rank) : null,
            VenueName = _division.VenueName,
            VsIntroDuration = tutorial ? 0f : _config.VsIntroDuration,
            CueName = _playerCue.Name,
            CueSprite = _playerCue.Sprite,
            PullTravelPixels = _config.PullTravelPixels,
            PullGrabZone = _config.PullGrabZone,
            PullStartThreshold = _config.PullStartThreshold,
            PowerHintShotsLeft = Mathf.Max(0, _config.PowerHintShots - (ProfileService.Instance != null ? ProfileService.Instance.ShotsTaken : 0)),
            PowerIdleHintSeconds = _config.PowerIdleHintSeconds,
            FineAimRepeatRate = _config.FineAimRepeatRate,
            FineAimRepeatDelay = _config.FineAimRepeatDelay,
            ReleaseFreezeDuration = _config.ReleaseFreezeDuration,
            FoulBannerDuration = _config.FoulBannerDuration,
            GroupBannerDuration = _config.GroupBannerDuration,
            GreenHalf = _playerCue.GreenHalf,
            YellowWidth = _playerCue.YellowWidth,
            OscillationPeriod = _playerCue.OscillationPeriod
    };

    private void Update()
    {
        if (_phase != MatchPhase.Simulating || !_shotInFlight)
            return;

        _physics.Advance(Time.deltaTime, _config.MaxStepsPerFrame);
        DispatchSimulationEvents();
        _table.Sync(_physics.Balls);
        GameEvents.RaiseRollingSpeed(TotalSpeed());

        if (!_physics.IsMoving)
        {
            _shotInFlight = false;
            GameEvents.RaiseRollingSpeed(0f);
            Resolve();
        }
    }

    #region Turn
    private void BeginTurn()
    {
        _phase = MatchPhase.Turn;
        _rules.FillLegalTargets(_rules.Turn, _legalTargets);

        if (_rules.BallInHand || _physics.Balls[0].Pocketed)
        {
            Vector2 start = _rules.KitchenOnly ? RackBuilder.DefaultCueBallPosition(Geometry) : new Vector2(0f, Geometry.HeadStringZ);
            Vector2 position = RackBuilder.FindFreeSpot(_physics.Balls, Geometry, start, _rules.KitchenOnly);
            _physics.Balls[0].Pocketed = false;
            _physics.Balls[0].Vel = _physics.Balls[0].Roll = Vector2.zero;
            _physics.Balls[0].Pos = position;
            _table.Ball(0).Show(_table.BallCenterWorld(position));
        }

        _cue.SetCue(IsHumanSide(_rules.Turn) ? _playerCue : _botCue);
        GameEvents.RaiseTurnStarted(BuildTurnInfo());

        if (!IsHumanSide(_rules.Turn))
            _bot.TakeTurn();
    }

    public TurnInfo BuildTurnInfo()
    {
        var info = new TurnInfo
        {
            IsPlayer = _rules.Turn == Side.Player,
            IsHuman = IsHumanSide(_rules.Turn),
            BallInHand = _rules.BallInHand,
            KitchenOnly = _rules.KitchenOnly,
            IsBreak = _rules.IsBreakShot,
            PlayerGroup = _rules.GroupOf(Side.Player),
            BotGroup = _rules.GroupOf(Side.Bot),
            PlayerOnEight = _rules.IsOnEight(Side.Player),
            BotOnEight = _rules.IsOnEight(Side.Bot)
        };
        System.Array.Copy(_rules.Pocketed, info.Pocketed, info.Pocketed.Length);
        for (int i = 1; i < _legalTargets.Length; i++)
        {
            if (_legalTargets[i])
                info.LegalTargets.Add(i);
        }
        return info;
    }

    public bool IsValidCueBallPosition(Vector2 position)
    {
        if (!Geometry.IsInsidePlayArea(position) || (_rules.KitchenOnly && !Geometry.IsInKitchen(position)))
            return false;
        return RackBuilder.IsFree(_physics.Balls, position, Geometry.BallRadius + 0.0005f, 0);
    }

    public void PlaceCueBall(Vector2 position)
    {
        if (_phase != MatchPhase.Turn)
            return;
        _physics.Balls[0].Pos = position;
        _table.Ball(0).Show(_table.BallCenterWorld(position));
    }

    // Called by the player or bot controller at the moment of release; the balls start when the cue connects.
    public void Shoot(Vector2 direction, float speed, ShotFeedback feedback)
    {
        if (_phase != MatchPhase.Turn)
            return;

        _phase = MatchPhase.Simulating;
        _pocketCursor = 0;
        _collisionCursor = 0;
        var shot = new ShotParams(direction, speed);
        _lastFeedback = feedback;
        RecordShotStart(direction, feedback);
        GameEvents.RaiseTurnPhaseChanged(TurnPhase.Shooting);
        GameEvents.RaiseShotFired(feedback);
        // A PERFECT release puffs chalk where the tip meets the ball (v3 V7).
        _cue.Strike(() =>
        {
            _physics.BeginShot(shot);
            _shotInFlight = true;
        }, feedback.Zone == ReleaseZone.Green);
    }
    #endregion

    #region Simulation
    private void DispatchSimulationEvents()
    {
        ShotResult result = _physics.Result;
        while (_pocketCursor < result.Pocketed.Count)
        {
            PocketEvent pocketed = result.Pocketed[_pocketCursor++];
            _table.PlayPocket(pocketed.Number, pocketed.PocketIndex, pocketed.Velocity, PocketKind(pocketed.Number));
            GameEvents.RaiseBallPocketed(pocketed.Number, pocketed.PocketIndex);
        }

        // At most one sound-worthy contact of each kind per frame: the strongest.
        float strongestBall = 0f, strongestCushion = 0f;
        while (_collisionCursor < result.Collisions.Count)
        {
            CollisionEvent collision = result.Collisions[_collisionCursor++];
            if (collision.Kind == CollisionKind.Ball)
                strongestBall = Mathf.Max(strongestBall, collision.Speed);
            else
                strongestCushion = Mathf.Max(strongestCushion, collision.Speed);
        }
        if (strongestBall > 0f)
            GameEvents.RaiseBallCollision(strongestBall, CollisionKind.Ball);
        if (strongestCushion > 0f)
            GameEvents.RaiseBallCollision(strongestCushion, CollisionKind.Cushion);
    }

    // How a ball dropped in this shot counts for the shooter, by the rules as they stood before the shot.
    private PocketFxKind PocketKind(int number) => MatchStatsRecorder.PocketKind(_rules, number);

    private float TotalSpeed()
    {
        float sum = 0f;
        BallState[] balls = _physics.Balls;
        for (int i = 0; i < balls.Length; i++)
        {
            if (!balls[i].Pocketed)
                sum += balls[i].Vel.magnitude;
        }
        return sum;
    }

    private void Resolve()
    {
        _phase = MatchPhase.Resolving;
        // The dropped balls stayed in the pockets for the shot; now they go (v3 V7).
        _table.HidePocketed(_physics.Balls);
        if (IsTutorial)
        {
            _tutorial.HandleShot(_physics.Result, _lastFeedback);
            return;
        }
        RulesOutcome outcome = EightBallRules.Evaluate(_rules, _physics.Result);

        if (outcome.RespotEight)
        {
            ref BallState eight = ref _physics.Balls[BallGroups.EightBall];
            eight.Pocketed = false;
            eight.Vel = eight.Roll = Vector2.zero;
            eight.Pos = RackBuilder.SpotPosition(_physics.Balls, Geometry, BallGroups.EightBall);
            _table.Ball(BallGroups.EightBall).Show(_table.BallCenterWorld(eight.Pos));
            GameEvents.RaiseToastRequested(new ToastMessage("8-ball re-spotted", ToastStyle.Info));
        }

        // Say what happened and why (G5): a foul banner with its consequence, the group, or how the turn goes on.
        float wait = _config.ResolvePause;
        if (outcome.Foul != FoulType.None)
        {
            GameEvents.RaiseFoul(new FoulInfo
            {
                Foul = outcome.Foul,
                ByPlayer = outcome.Shooter == Side.Player,
                MatchOver = outcome.MatchOver,
                Rule = RuleOf(outcome.Foul)
            });
            if (!outcome.MatchOver)
                wait = Mathf.Max(wait, _config.FoulBannerDuration);
        }
        if (outcome.GroupsAssigned)
        {
            GameEvents.RaiseGroupsAssigned(outcome.Next.GroupOf(Side.Player), outcome.Next.GroupOf(Side.Bot));
            wait = Mathf.Max(wait, _config.GroupBannerDuration);
        }
        else if (_rules.IsBreakShot && !outcome.MatchOver)
        {
            GameEvents.RaiseToastRequested(new ToastMessage("Table is open", ToastStyle.Info));
        }
        if (outcome.Foul == FoulType.None && !outcome.MatchOver && outcome.Shooter == Side.Player)
        {
            if (outcome.ShooterContinues)
                GameEvents.RaiseToastRequested(new ToastMessage("Nice! Shoot again", ToastStyle.Good));
            else
                GameEvents.RaiseToastRequested(new ToastMessage(_physics.Result.Pocketed.Count == 0
                    ? "No ball pocketed — opponent's turn" : "None of yours went in — opponent's turn", ToastStyle.Info));
        }

        Debug.Log($"Match: {outcome.Shooter} shot -> foul={outcome.Foul} continues={outcome.ShooterContinues} " +
                  $"pocketed={_physics.Result.Pocketed.Count} groups={outcome.Next.GroupOf(Side.Player)}/{outcome.Next.GroupOf(Side.Bot)}");

        if (_shotByPlayer)
            _recorder.ShotEnd(_rules, outcome, _physics.Result, Geometry, _config.BankIgnoreBeforeDrop);
        _rules = outcome.Next;
        if (_rules.IsOver)
            StartCoroutine(AfterDelay(_config.ResolvePause, EndMatch));
        else
            StartCoroutine(AfterDelay(wait, BeginTurn));
    }

    private static RuleId RuleOf(FoulType foul) => foul switch
    {
        FoulType.WrongBallFirst or FoulType.NoBallHit => RuleId.OwnBallFirst,
        FoulType.NoRailAfterContact => RuleId.RailAfterContact,
        FoulType.CueBallPocketed => RuleId.BallInHand,
        _ => RuleId.None
    };
    #endregion

    #region Stats
    // The player side's shots (the human; in the Editor's bot-vs-bot test, the bot playing for them).
    private void RecordShotStart(Vector2 direction, ShotFeedback feedback)
    {
        _shotByPlayer = _rules.Turn == Side.Player;
        if (_shotByPlayer)
            _recorder.ShotStart(_physics.Balls, direction, feedback.Zone, feedback.UsedFineAim);
    }
    #endregion

    #region End
    private void EndMatch()
    {
        StopAllCoroutines();
        _bot.CancelTurn();
        _shotInFlight = false;
        _phase = MatchPhase.Ended;
        _cue.SetVisible(false);
        if (_paused)
            SetPaused(false);
        PopupManager.Instance.Hide<PausePopup>();

        bool won = _rules.Winner == Side.Player;
        _recorder.Finish(won, _rules);
        ProfileService profile = ProfileService.Instance;
        int previousBalance = profile != null ? profile.Coins : 0;
        var result = new MatchResult
        {
            Won = won,
            Title = won ? "YOU WIN!" : "YOU LOSE",
            Reason = EightBallRules.ReasonText(_rules, "You", _opponent != null ? _opponent.ShortName : "Opponent"),
            NewBalance = previousBalance
        };
        if (profile != null)
        {
            MatchOutcome outcome = profile.ApplyMatchResult(won, _recorder.Stats);
            result.CoinDelta = outcome.CoinsFromMatch;
            result.NewBalance = outcome.CoinsAfter;
            result.RankBefore = outcome.RankBefore;
            result.RankAfter = outcome.RankAfter;
            result.RankBeforeView = profile.RankView(outcome.Rank.Before);
            result.RankAfterView = profile.RankView(outcome.Rank.After);
            result.StarsGained = outcome.Rank.StarsGained;
            result.StreakBonus = outcome.Rank.StreakBonus;
            result.StageUp = outcome.Rank.StageUp;
            result.StageDown = outcome.Rank.StageDown;
            result.NewDivision = outcome.Rank.NewDivision;
            result.LossCostNothing = outcome.Rank.LossCostNothing;
            result.NewVenueName = outcome.Rank.NewDivision ? result.RankAfterView.VenueName : null;
            result.CueAffordable = outcome.CueAffordable;
            result.StarShieldAvailable = outcome.StarShieldAvailable;
            result.DoubleCoinsAvailable = outcome.DoubleCoinsAvailable;
            result.ObjectivesCompleted.AddRange(outcome.ObjectivesCompleted);
        }
        Debug.Log($"Match: ended, {(won ? "player" : "bot")} wins - {result.Reason} ({result.CoinDelta:+0;-0;0} coins, balance {result.NewBalance}, " +
                  $"{result.RankBefore} -> {result.RankAfter}, stars {result.StarsGained:+0;-0;0}, pots {Stats.Pots.Count}, perfect {Stats.PerfectReleases}, fouls {Stats.Fouls})");
        GameEvents.RaiseMatchEnded(result);
        StartCoroutine(ShowResultAfterDelay(new MatchResultPopupData(result, previousBalance)));
    }

    private IEnumerator ShowResultAfterDelay(MatchResultPopupData data)
    {
        yield return new WaitForSecondsRealtime(_config.EndPopupDelay);
        PopupManager.Instance.Show<MatchResultPopup>(data);
    }
    #endregion

    #region Pause, forfeit, navigation
    private void HandlePauseRequested()
    {
        if (_paused || _phase == MatchPhase.Ended || PopupManager.Instance == null)
            return;
        SetPaused(true);
        PopupManager.Instance.Show<PausePopup>();
    }

    private void HandleResumeRequested()
    {
        if (!_paused)
            return;
        PopupManager.Instance.Hide<PausePopup>();
        SetPaused(false);
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        Time.timeScale = paused ? 0f : 1f;
        GameEvents.RaisePauseChanged(paused);
    }

    private void HandleForfeitRequested()
    {
        if (IsTutorial)
        {
            PopupManager.Instance.Hide<PausePopup>();
            if (_paused)
                SetPaused(false);
            GameEvents.RaiseTutorialSkipRequested();
            return;
        }
        if (_phase == MatchPhase.Ended)
            return;
        _rules = EightBallRules.Forfeit(_rules, Side.Player);
        EndMatch();
    }

    private void HandleSettingsRequested()
    {
        AudioManager audio = AudioManager.Instance;
        SettingsPopup popup = PopupManager.Instance.Show<SettingsPopup>(new SettingsPopupData(audio.SfxVolume, audio.MusicVolume, HapticsService.Instance.Enabled, false));
        if (popup == null)
            return;
        popup.Hidden -= HandleSettingsHidden;
        popup.Hidden += HandleSettingsHidden;
    }

    private void HandleSettingsHidden(PopupBase popup)
    {
        popup.Hidden -= HandleSettingsHidden;
        AudioManager.Instance.SaveVolumes();
    }

    private void HandleMenuRequested()
    {
        if (_phase != MatchPhase.Ended)
            return;
        SceneLoader.Instance.Load(InputRouter.MenuSceneName);
    }

    // "Play again" from the result popup: like PLAY on the menu, in the division the player is in now.
    private void HandlePlayAgainRequested()
    {
        if (_phase != MatchPhase.Ended)
            return;
        MatchSetup.Ranked(ProfileService.Instance.CurrentDivision);
        SceneLoader.Instance.Load(InputRouter.MatchSceneName);
    }

    // From the pause menu or a foul banner's "?": the House Rules card, open at that rule. The match holds
    // still while it is open.
    private void HandleRulesRequested(RuleId focus)
    {
        if (PopupManager.Instance == null)
            return;
        RulesPopup popup = PopupManager.Instance.Show<RulesPopup>(new RulesPopupData(focus));
        if (popup == null || _paused || _phase == MatchPhase.Ended)
            return;
        SetPaused(true);
        popup.Hidden -= HandleRulesHidden;
        popup.Hidden += HandleRulesHidden;
    }

    private void HandleRulesHidden(PopupBase popup)
    {
        popup.Hidden -= HandleRulesHidden;
        if (_paused && !PopupManager.Instance.IsAnyOpen)
            SetPaused(false);
    }

    private void HandleCuesRequested() =>
        PopupManager.Instance.Show<CuesPopup>(new CuesPopupData(ProfileService.Instance.GetSnapshot()));
    #endregion

    private IEnumerator AfterDelay(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
    }

    // A table set by hand, the player to shoot: the tutorial's lessons and Editor tests. `numbers` are the balls on
    // the table (0 = the cue ball) at `positions`; the rest are pocketed.
    public void SetupTable(BallGroup playerGroup, int[] numbers, Vector2[] positions, bool ballInHand = false)
    {
        StopAllCoroutines();
        _bot.CancelTurn();
        RulesState rules = RulesState.NewRack(Side.Player);
        rules.IsBreakShot = false;
        rules.BallInHand = ballInHand;
        rules.KitchenOnly = false;
        rules.Groups[(int)Side.Player] = playerGroup;
        rules.Groups[(int)Side.Bot] = playerGroup == BallGroup.Solids ? BallGroup.Stripes : playerGroup == BallGroup.Stripes ? BallGroup.Solids : BallGroup.Open;

        BallState[] balls = _physics.Balls;
        for (int n = 1; n < balls.Length; n++)
        {
            rules.Pocketed[n] = System.Array.IndexOf(numbers, n) < 0;
            balls[n].Pocketed = rules.Pocketed[n];
            balls[n].Vel = balls[n].Roll = Vector2.zero;
        }
        balls[0].Pocketed = false;
        balls[0].Vel = balls[0].Roll = Vector2.zero;
        for (int i = 0; i < numbers.Length; i++)
            balls[numbers[i]].Pos = positions[i];
        _rules = rules;
        _shotInFlight = false;
        _table.ShowState(balls);
        BeginTurn();
    }

#if UNITY_EDITOR
    // Editor testing from `eval`.
    // Starts the tutorial in the open Match scene (TutorialChecks).
    public static string DebugStartTutorial()
    {
        MatchSetup.Mode = MatchMode.Tutorial;
        Instance.StartTutorial();
        return "tutorial started";
    }

    // Ends the match now through the real path (profile, results popup): the player wins or forfeits.
    public static string DebugEndMatch(bool won)
    {
        MatchController match = Instance;
        RulesState rules = EightBallRules.Forfeit(match._rules, won ? Side.Bot : Side.Player);
        rules.Reason = EndReason.EightPocketed;
        rules.ReasonSide = rules.Winner;
        match._rules = rules;
        match.EndMatch();
        return $"ended, player {(won ? "won" : "lost")}";
    }

    public static string DebugBotVsBot(bool on, int seed = 1)
    {
        Instance._botVsBot = on;
        Instance.StartMatch(seed);
        return $"bot vs bot {(on ? "on" : "off")}, seed {seed}";
    }

    // Restart with a chosen breaker (and optionally a different player cue).
    public static string DebugStart(bool playerBreaks, string playerCueId = null, int seed = 1)
    {
        MatchController match = Instance;
        if (!string.IsNullOrEmpty(playerCueId) && match._catalog.Get(playerCueId) != null)
            match._playerCue = match._catalog.Get(playerCueId);
        match._botVsBot = false;
        match.StartMatch(seed);
        match._rules = RulesState.NewRack(playerBreaks ? Side.Player : Side.Bot);
        return $"match restarted, {(playerBreaks ? "player" : "bot")} breaks with {match._playerCue.Name}";
    }

    public static string DebugMoveBall(int number, Vector2 position)
    {
        MatchController match = Instance;
        match._physics.Balls[number].Pos = position;
        match._physics.Balls[number].Pocketed = false;
        match._table.ShowState(match._physics.Balls);
        return $"ball {number} at {position}";
    }

    public static Vector2 DebugBallScreen(int number)
    {
        MatchController match = Instance;
        Vector3 world = match._table.BallCenterWorld(match._physics.Balls[number].Pos);
        return match._cameraRig.Camera.WorldToScreenPoint(world);
    }

    // Sets up a won position: player on SOLIDS with the group cleared, the 8 a short straight shot from a
    // corner pocket, player to shoot. A green release then wins the match through the normal flow.
    public static string DebugSetupWin()
    {
        MatchController match = Instance;
        match.StopAllCoroutines();
        match._bot.CancelTurn();
        RulesState rules = RulesState.NewRack(Side.Player);
        rules.IsBreakShot = false;
        rules.BallInHand = false;
        rules.KitchenOnly = false;
        rules.Groups[(int)Side.Player] = BallGroup.Solids;
        rules.Groups[(int)Side.Bot] = BallGroup.Stripes;
        for (int n = 1; n <= 7; n++)
            rules.Pocketed[n] = true;
        rules.Pocketed[9] = rules.Pocketed[10] = true;
        match._rules = rules;

        BallState[] balls = match._physics.Balls;
        for (int n = 1; n <= 15; n++)
        {
            balls[n].Pocketed = rules.Pocketed[n];
            balls[n].Vel = balls[n].Roll = Vector2.zero;
        }
        Vector2 pocket = match.Geometry.Pockets[5].Center;
        Vector2 toTable = (-pocket).normalized;
        balls[8].Pos = pocket + toTable * 0.35f;
        balls[0].Pos = balls[8].Pos + toTable * 0.4f;
        balls[0].Pocketed = false;
        float[] stripes = { -0.35f, -0.2f, -0.05f, 0.1f, 0.25f };
        for (int i = 0; i < 5; i++)
            balls[11 + i].Pos = new Vector2(stripes[i], -0.7f);
        match._table.ShowState(balls);
        match.BeginTurn();
        return "win position ready: " + DebugStatus();
    }

    // Any table for screenshots and checks: only the listed balls (and the cue ball) stay on the table,
    // groups decided, the player to shoot. Ball 0 in the list places the cue ball.
    public static string DebugSetupTable(BallGroup playerGroup, int[] numbers, Vector2[] positions, bool ballInHand = false)
    {
        Instance.SetupTable(playerGroup, numbers, positions, ballInHand);
        return DebugStatus();
    }

    public static string DebugStatus()
    {
        MatchController match = Instance;
        RulesState rules = match._rules;
        return $"phase={match._phase} turn={rules.Turn} break={rules.IsBreakShot} ballInHand={rules.BallInHand} kitchen={rules.KitchenOnly} " +
               $"groups={rules.GroupOf(Side.Player)}/{rules.GroupOf(Side.Bot)} left={rules.Remaining(rules.GroupOf(Side.Player))}/{rules.Remaining(rules.GroupOf(Side.Bot))} " +
               $"over={rules.IsOver} winner={rules.Winner} reason={rules.Reason}";
    }
#endif
}
