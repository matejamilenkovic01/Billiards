using UnityEngine;

// TEMPORARY (commit 2): drives the simulation without rules or UI so shots can be fired from `eval`.
// Replaced by MatchController in the next commit.
public class DebugShot : MonoBehaviour
{
    [SerializeField] private MatchConfig _config;
    [SerializeField] private TableView _table;
    [SerializeField] private CueView _cue;
    [SerializeField] private CueCatalog _catalog;

    private PoolPhysics _physics;
    private int _pocketCursor;

    public static DebugShot Instance { get; private set; }
    public PoolPhysics Physics => _physics;

    private void Awake() => Instance = this;

    private void Start()
    {
        _physics = new PoolPhysics(_table.Geometry, _config.ToPoolSettings());
        _cue.SetCue(_catalog.Starter);
        Rerack(1);
    }

    private void Update()
    {
        if (!_physics.IsMoving)
            return;

        _physics.Advance(Time.deltaTime, _config.MaxStepsPerFrame);
        while (_pocketCursor < _physics.Result.Pocketed.Count)
        {
            PocketEvent pocketed = _physics.Result.Pocketed[_pocketCursor++];
            _table.PlayPocket(pocketed.Number, pocketed.PocketIndex);
        }
        _table.Sync(_physics.Balls);
    }

    public static string Rerack(int seed)
    {
        DebugShot shot = Instance;
        shot._physics.SetBalls(RackBuilder.Build(new System.Random(seed), shot._table.Geometry, shot._config.RackGap));
        shot._table.ShowState(shot._physics.Balls);
        shot.AimCue(shot._table.Geometry.FootSpot - shot._physics.Balls[0].Pos, 0f);
        return "racked";
    }

    public static string Break(float power = 1f)
    {
        DebugShot shot = Instance;
        return Fire(shot._table.Geometry.FootSpot - shot._physics.Balls[0].Pos, power);
    }

    public static string FireAngle(float degrees, float power) =>
        Fire(new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad)), power);

    public static string Fire(Vector2 direction, float power)
    {
        DebugShot shot = Instance;
        if (shot._physics.IsMoving)
            return "balls still moving";

        shot.AimCue(direction, 0f);
        shot._pocketCursor = 0;
        float speed = shot._config.SpeedFromPull(power);
        shot._cue.Strike(() => shot._physics.BeginShot(new ShotParams(direction, speed)));
        return $"fired {speed:F2} m/s";
    }

    public static string Status()
    {
        PoolPhysics physics = Instance._physics;
        string pocketed = "";
        foreach (PocketEvent pocketEvent in physics.Result.Pocketed)
            pocketed += pocketEvent.Number + "@" + pocketEvent.PocketIndex + " ";
        return $"moving={physics.IsMoving} t={physics.Time:F2}s firstContact={physics.Result.FirstContactBall} pocketed=[{pocketed.Trim()}]";
    }

    private void AimCue(Vector2 direction, float pull)
    {
        _cue.SetVisible(true);
        _cue.SetPose(_table.BallCenterWorld(_physics.Balls[0].Pos), _table.TableDirectionToWorld(direction), pull);
    }
}
