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

    // What the player did, for the objectives (v3 V12).
    private MatchStats _stats = new();
    private int _perfectStreak;
    private bool _shotByPlayer;
    private bool _shotUsedFineAim;
    private Vector2 _shotDirection;
    private readonly Vector2[] _shotStart = new Vector2[BallGroups.BallCount];
    private int _visitPots;
    private bool _visitRunOutCounted;

    public MatchPhase Phase => _phase;
    public RulesState Rules => _rules;
    public BallState[] Balls => _physics.Balls;
    public TableGeometry Geometry => _table.Geometry;
    public ShotPredictor Predictor => _predictor;
    public MatchConfig Config => _config;
    public BotProfile BotProfile => _botProfile;
    public DivisionDefinition Division => _division;
    public OpponentDefinition Opponent => _opponent;
    public MatchStats Stats => _stats;
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
        _stats = new MatchStats();
        _perfectStreak = 0;
        _visitPots = 0;
        _visitRunOutCounted = false;
        _table.ShowState(_physics.Balls);
        _cue.SetVisible(false);

        _phase = MatchPhase.Intro;
        GameEvents.RaiseMatchStarted(new MatchStartInfo
        {
            PlayerBreaks = _rules.Turn == Side.Player,
            PlayerName = PlayerName,
            OpponentName = _opponent != null ? _opponent.Name : OpponentName,
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
        });
        Debug.Log($"Match: started in {_division.Name} vs {(_opponent != null ? _opponent.Name : OpponentName)}, " +
                  $"{(_rules.Turn == Side.Player ? "player" : "bot")} breaks");
        StartCoroutine(AfterDelay(_config.CoinFlipDuration, BeginTurn));
    }

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
    private PocketFxKind PocketKind(int number)
    {
        if (number == 0)
            return PocketFxKind.Foul;
        Side shooter = _rules.Turn;
        if (number == BallGroups.EightBall)
            return _rules.IsOnEight(shooter) ? PocketFxKind.Legal : PocketFxKind.Foul;
        BallGroup group = _rules.GroupOf(shooter);
        return group == BallGroup.Open || BallGroups.Contains(group, number) ? PocketFxKind.Legal : PocketFxKind.Opponent;
    }

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
            RecordPlayerShot(outcome);
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
    private void RecordShotStart(Vector2 direction, ShotFeedback feedback)
    {
        // The player side's shots (the human; in the Editor's bot-vs-bot test, the bot playing for them).
        _shotByPlayer = _rules.Turn == Side.Player;
        if (!_shotByPlayer)
            return;
        _shotDirection = direction;
        _shotUsedFineAim = feedback.UsedFineAim;
        for (int i = 0; i < _shotStart.Length; i++)
            _shotStart[i] = _physics.Balls[i].Pos;

        if (feedback.Zone == ReleaseZone.Green)
        {
            _stats.PerfectReleases++;
            _perfectStreak++;
            _stats.MaxPerfectStreak = Mathf.Max(_stats.MaxPerfectStreak, _perfectStreak);
        }
        else
        {
            _perfectStreak = 0;
        }
    }

    // Called before the rules move on, so PocketKind still reads the groups as they stood for this shot. Only a
    // clean shot counts its pots.
    private void RecordPlayerShot(RulesOutcome outcome)
    {
        ShotResult result = _physics.Result;
        bool foul = outcome.Foul != FoulType.None;
        int own = 0;
        if (foul)
        {
            _stats.Fouls++;
        }
        else
        {
            foreach (PocketEvent pocket in result.Pocketed)
            {
                if (PocketKind(pocket.Number) != PocketFxKind.Legal)
                    continue;
                own++;
                _stats.Pots.Add(new PotRecord
                {
                    Distance = (Geometry.Pockets[pocket.PocketIndex].Center - _shotStart[pocket.Number]).magnitude,
                    Cut = pocket.Number == result.FirstContactBall ? Vector2.Angle(_shotDirection, result.FirstContactObjectDir) : 0f,
                    Bank = TouchedCushionBefore(result, pocket.Number, pocket.Time),
                    FineAim = _shotUsedFineAim
                });
            }
        }
        if (own >= 2)
            _stats.TwoBallShots++;

        // A run out: the whole group (3+ balls) cleared in one visit, counted once per visit.
        _visitPots += own;
        bool cleared = outcome.Next.IsOnEight(Side.Player) || (outcome.Next.IsOver && outcome.Next.Winner == Side.Player);
        if (!foul && cleared && !_visitRunOutCounted && _visitPots >= 3)
        {
            _stats.RunOuts++;
            _visitRunOutCounted = true;
        }
        if (foul || !outcome.ShooterContinues)
        {
            _visitPots = 0;
            _visitRunOutCounted = false;
        }
    }

    private bool TouchedCushionBefore(ShotResult result, int number, float time)
    {
        foreach (CollisionEvent collision in result.Collisions)
        {
            if (collision.Time > time - _config.BankIgnoreBeforeDrop)
                break;
            if (collision.Kind == CollisionKind.Cushion && collision.BallA == number)
                return true;
        }
        return false;
    }

    private int BallsLeft(Side side)
    {
        BallGroup group = _rules.GroupOf(side);
        if (group == BallGroup.Open)
            return 7;
        int left = 0;
        for (int n = 1; n < BallGroups.BallCount; n++)
        {
            if (n != BallGroups.EightBall && BallGroups.Contains(group, n) && !_rules.Pocketed[n])
                left++;
        }
        return left;
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
        _stats.Won = won;
        _stats.OpponentLeft = won ? BallsLeft(Side.Bot) : 0;
        ProfileService profile = ProfileService.Instance;
        int previousBalance = profile != null ? profile.Coins : 0;
        var result = new MatchResult
        {
            Won = won,
            Title = won ? "YOU WIN!" : "YOU LOSE",
            Reason = EightBallRules.ReasonText(_rules),
            NewBalance = previousBalance
        };
        if (profile != null)
        {
            MatchOutcome outcome = profile.ApplyMatchResult(won, _stats);
            result.CoinDelta = outcome.CoinsFromMatch;
            result.NewBalance = outcome.CoinsAfter;
            result.RankBefore = outcome.RankBefore;
            result.RankAfter = outcome.RankAfter;
            result.StarsGained = outcome.Rank.StarsGained;
            result.StreakBonus = outcome.Rank.StreakBonus;
            result.NewDivision = outcome.Rank.NewDivision;
            result.ObjectivesCompleted.AddRange(outcome.ObjectivesCompleted);
        }
        Debug.Log($"Match: ended, {(won ? "player" : "bot")} wins - {result.Reason} ({result.CoinDelta:+0;-0;0} coins, balance {result.NewBalance}, " +
                  $"{result.RankBefore} -> {result.RankAfter}, stars {result.StarsGained:+0;-0;0}, pots {_stats.Pots.Count}, perfect {_stats.PerfectReleases}, fouls {_stats.Fouls})");
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
        if (_phase == MatchPhase.Ended)
            return;
        _rules = EightBallRules.Forfeit(_rules, Side.Player);
        EndMatch();
    }

    private void HandleSettingsRequested()
    {
        AudioManager audio = AudioManager.Instance;
        SettingsPopup popup = PopupManager.Instance.Show<SettingsPopup>(new SettingsPopupData(audio.SfxVolume, audio.MusicVolume));
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

#if UNITY_EDITOR
    // Editor testing from `eval`.
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
        MatchController match = Instance;
        match.StopAllCoroutines();
        match._bot.CancelTurn();
        RulesState rules = RulesState.NewRack(Side.Player);
        rules.IsBreakShot = false;
        rules.BallInHand = ballInHand;
        rules.KitchenOnly = false;
        rules.Groups[(int)Side.Player] = playerGroup;
        rules.Groups[(int)Side.Bot] = playerGroup == BallGroup.Solids ? BallGroup.Stripes : playerGroup == BallGroup.Stripes ? BallGroup.Solids : BallGroup.Open;

        BallState[] balls = match._physics.Balls;
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
        match._rules = rules;
        match._table.ShowState(balls);
        match.BeginTurn();
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
