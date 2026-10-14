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
    [SerializeField] private BotProfile _botProfile;
    [SerializeField] private CueCatalog _catalog;
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
    private readonly bool[] _legalTargets = new bool[BallGroups.BallCount];

    public MatchPhase Phase => _phase;
    public RulesState Rules => _rules;
    public BallState[] Balls => _physics.Balls;
    public TableGeometry Geometry => _table.Geometry;
    public ShotPredictor Predictor => _predictor;
    public MatchConfig Config => _config;
    public BotProfile BotProfile => _botProfile;
    public TableView Table => _table;
    public CueView Cue => _cue;
    public CameraRig CameraRig => _cameraRig;
    public System.Random Random => _random;
    public CueDefinition PlayerCue => _playerCue;
    public CueDefinition BotCue => _botCue;
    public bool BotVsBot => _botVsBot;
    public bool IsHumanTurn => _phase == MatchPhase.Turn && !_botVsBot && _rules.Turn == Side.Player;
    public bool[] LegalTargets => _legalTargets;

    protected override void Awake()
    {
        base.Awake();
        PoolSettings settings = _config.ToPoolSettings();
        _physics = new PoolPhysics(_table.Geometry, settings);
        _predictor = new ShotPredictor(_table.Geometry, settings.CushionRestitution, settings.CushionFriction);
        _botCue = _catalog.Get("club") ?? _catalog.Starter;
        _playerCue = ProfileService.Instance != null ? ProfileService.Instance.SelectedCue : _catalog.Starter;
    }

    private void Start() => StartMatch(System.Environment.TickCount);

    public void StartMatch(int seed)
    {
        StopAllCoroutines();
        _bot.CancelTurn();
        _random = new System.Random(seed);
        _physics.SetBalls(RackBuilder.Build(_random, Geometry, _config.RackGap));
        _rules = RulesState.NewRack(_random.NextDouble() < 0.5 ? Side.Player : Side.Bot);
        _shotInFlight = false;
        _table.ShowState(_physics.Balls);
        _cue.SetVisible(false);

        _phase = MatchPhase.Intro;
        GameEvents.RaiseMatchStarted(new MatchStartInfo
        {
            PlayerBreaks = _rules.Turn == Side.Player,
            CueName = _playerCue.Name,
            CueSprite = _playerCue.Sprite,
            PullRangePixels = _config.PullRangePixels,
            PullStartThreshold = _config.PullStartThreshold,
            GreenHalf = _playerCue.GreenHalf,
            YellowWidth = _playerCue.YellowWidth,
            OscillationPeriod = _playerCue.OscillationPeriod
        });
        Debug.Log($"Match: started, {(_rules.Turn == Side.Player ? "player" : "bot")} breaks");
        StartCoroutine(AfterDelay(_config.CoinFlipDuration, BeginTurn));
    }

    private void Update()
    {
        if (_phase != MatchPhase.Simulating || !_shotInFlight)
            return;

        _physics.Advance(Time.deltaTime, _config.MaxStepsPerFrame);
        DispatchSimulationEvents();
        _table.Sync(_physics.Balls);

        if (!_physics.IsMoving)
        {
            _shotInFlight = false;
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
            _physics.Balls[0].Vel = Vector2.zero;
            _physics.Balls[0].Pos = position;
            _table.Ball(0).Show(_table.BallCenterWorld(position));
        }

        _cue.SetCue(_rules.Turn == Side.Player && !_botVsBot ? _playerCue : _botCue);
        GameEvents.RaiseTurnStarted(BuildTurnInfo());

        if (_botVsBot || _rules.Turn == Side.Bot)
            _bot.TakeTurn();
    }

    public TurnInfo BuildTurnInfo()
    {
        var info = new TurnInfo
        {
            IsPlayer = _rules.Turn == Side.Player,
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
        GameEvents.RaiseTurnPhaseChanged(TurnPhase.Shooting);
        GameEvents.RaiseShotFired(feedback);
        _cue.Strike(() =>
        {
            _physics.BeginShot(shot);
            _shotInFlight = true;
        });
    }
    #endregion

    #region Simulation
    private void DispatchSimulationEvents()
    {
        ShotResult result = _physics.Result;
        while (_pocketCursor < result.Pocketed.Count)
        {
            PocketEvent pocketed = result.Pocketed[_pocketCursor++];
            _table.PlayPocket(pocketed.Number, pocketed.PocketIndex);
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

    private void Resolve()
    {
        _phase = MatchPhase.Resolving;
        RulesOutcome outcome = EightBallRules.Evaluate(_rules, _physics.Result);

        if (outcome.RespotEight)
        {
            ref BallState eight = ref _physics.Balls[BallGroups.EightBall];
            eight.Pocketed = false;
            eight.Vel = Vector2.zero;
            eight.Pos = RackBuilder.SpotPosition(_physics.Balls, Geometry, BallGroups.EightBall);
            _table.Ball(BallGroups.EightBall).Show(_table.BallCenterWorld(eight.Pos));
            GameEvents.RaiseToastRequested(new ToastMessage("8-ball re-spotted", ToastStyle.Info));
        }

        if (outcome.Foul != FoulType.None)
            GameEvents.RaiseFoul(outcome.Foul);
        if (outcome.GroupsAssigned)
            GameEvents.RaiseGroupsAssigned(outcome.Next.GroupOf(Side.Player), outcome.Next.GroupOf(Side.Bot));

        Debug.Log($"Match: {outcome.Shooter} shot -> foul={outcome.Foul} continues={outcome.ShooterContinues} " +
                  $"pocketed={_physics.Result.Pocketed.Count} groups={outcome.Next.GroupOf(Side.Player)}/{outcome.Next.GroupOf(Side.Bot)}");

        _rules = outcome.Next;
        if (_rules.IsOver)
            StartCoroutine(AfterDelay(_config.ResolvePause, EndMatch));
        else
            StartCoroutine(AfterDelay(_config.ResolvePause, BeginTurn));
    }
    #endregion

    #region End
    private void EndMatch()
    {
        _phase = MatchPhase.Ended;
        _cue.SetVisible(false);
        bool won = _rules.Winner == Side.Player;
        var result = new MatchResult { Won = won, Reason = EightBallRules.ReasonText(_rules) };
        Debug.Log($"Match: ended, {(won ? "player" : "bot")} wins - {result.Reason}");
        GameEvents.RaiseMatchEnded(result);
    }
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
