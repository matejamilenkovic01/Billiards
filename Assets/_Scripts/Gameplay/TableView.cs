using UnityEngine;

// The 3D table. The transform sits on the cloth surface: local (x, 0, z) = table plane (x, y).
public class TableView : MonoBehaviour
{
    [SerializeField] private MatchConfig _config;
    [SerializeField] private BallView _ballPrefab;
    [SerializeField] private Transform _ballRoot;
    [SerializeField] private Material[] _ballMaterials;
    [SerializeField] private BallShadowRenderer _shadows;

    private readonly BallView[] _balls = new BallView[BallGroups.BallCount];
    private TableGeometry _geometry;

    // Lazy: other components may ask for it in their Awake, before this one's ran.
    public TableGeometry Geometry => _geometry ??= new TableGeometry(_config.ToPoolSettings());
    public float FeltHeight => transform.position.y;
    public BallView Ball(int number) => _balls[number];

    private void Awake()
    {
        for (int i = 0; i < _balls.Length; i++)
        {
            _balls[i] = Instantiate(_ballPrefab, _ballRoot);
            _balls[i].Setup(i, _ballMaterials[i]);
        }
    }

    // Contact shadows follow the ball views (also while held in hand or animating).
    private void LateUpdate()
    {
        if (_shadows != null)
            _shadows.UpdateShadows(_balls, transform, _config.BallRadius);
    }

    public Vector3 TableToWorld(Vector2 point, float height) => transform.TransformPoint(new Vector3(point.x, height, point.y));

    public Vector3 BallCenterWorld(Vector2 point) => TableToWorld(point, _config.BallRadius);

    public Vector2 WorldToTable(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        return new Vector2(local.x, local.z);
    }

    public Vector3 TableDirectionToWorld(Vector2 direction) =>
        transform.TransformDirection(new Vector3(direction.x, 0f, direction.y)).normalized;

    public Vector2 WorldDirectionToTable(Vector3 direction)
    {
        Vector3 local = transform.InverseTransformDirection(direction);
        return new Vector2(local.x, local.z).normalized;
    }

    // Screen point -> table plane at ball-centre height. False if the ray misses the plane.
    public bool ScreenToTable(Camera camera, Vector2 screenPoint, out Vector2 tablePoint)
    {
        Ray ray = camera.ScreenPointToRay(screenPoint);
        var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0f, _config.BallRadius, 0f)));
        if (plane.Raycast(ray, out float distance))
        {
            tablePoint = WorldToTable(ray.GetPoint(distance));
            return true;
        }
        tablePoint = default;
        return false;
    }

    public Vector3 PocketWorld(int pocketIndex) => TableToWorld(Geometry.Pockets[pocketIndex].Center, _config.BallRadius);

    // Puts every ball where the state says, without animation (new rack, re-spot, scene start).
    public void ShowState(BallState[] balls)
    {
        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i].Pocketed)
                _balls[i].Hide();
            else
                _balls[i].Show(BallCenterWorld(balls[i].Pos));
            _balls[i].SyncRollAngle(balls[i].RollAngle);
        }
    }

    public void Sync(BallState[] balls)
    {
        for (int i = 0; i < balls.Length; i++)
        {
            if (!balls[i].Pocketed)
                _balls[i].Apply(balls[i], BallCenterWorld(balls[i].Pos));
        }
    }

    public void PlayPocket(int number, int pocketIndex) =>
        _balls[number].PlayPocket(PocketWorld(pocketIndex), _config.PocketDrop, _config.PocketShrink, _config.PocketAnimDuration);
}
