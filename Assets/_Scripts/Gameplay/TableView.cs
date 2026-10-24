using UnityEngine;

// The 3D table. The transform sits on the cloth surface: local (x, 0, z) = table plane (x, y).
public class TableView : MonoBehaviour
{
    [SerializeField] private MatchConfig _config;
    [SerializeField] private BallView _ballPrefab;
    [SerializeField] private Transform _ballRoot;
    [SerializeField] private Material[] _ballMaterials;
    [SerializeField] private BallShadowRenderer _shadows;
    [SerializeField] private Material _glowMaterial;

    private const int GlowSegments = 40;
    private readonly LineRenderer[] _glows = new LineRenderer[6];
    private readonly float[] _glowTime = new float[6];
    private readonly Color[] _glowColor = new Color[6];
    private Camera _camera;

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

    // Contact shadows follow the ball views (also while held in hand or animating); pocket rims fade out.
    private void LateUpdate()
    {
        if (_shadows != null)
            _shadows.UpdateShadows(_balls, transform, _config.BallRadius);
        for (int i = 0; i < _glows.Length; i++)
        {
            if (_glows[i] == null || !_glows[i].enabled)
                continue;
            _glowTime[i] += Time.deltaTime;
            float t = Mathf.Clamp01(_glowTime[i] / Mathf.Max(0.01f, _config.PocketGlowTime));
            Color color = _glowColor[i];
            color.a *= 1f - t * t;
            _glows[i].startColor = _glows[i].endColor = color;
            _glows[i].widthMultiplier = Mathf.Lerp(0.025f, 0.008f, t);
            if (t >= 1f)
                _glows[i].enabled = false;
        }
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

    // A ball dropped (v3 V7): it falls into the liner and stays there until the turn ends, the rim glows in the
    // colour of how it counts, and the UI hears where it happened on screen.
    public void PlayPocket(int number, int pocketIndex, Vector2 velocity, PocketFxKind kind)
    {
        PocketInfo pocket = Geometry.Pockets[pocketIndex];
        float holeRadius = pocket.CaptureRadius + _config.PocketHoleExtra;
        _balls[number].PlayPocket(TableToWorld(pocket.Center, -_config.PocketDepth), TableDirectionToWorld(velocity) * velocity.magnitude,
            holeRadius, _config.BallRadius, _config.PocketFallTime, _config.PocketBounce);
        GlowRim(pocketIndex, holeRadius, kind == PocketFxKind.Legal ? _config.PocketGlowLegal : kind == PocketFxKind.Foul ? _config.PocketGlowFoul : _config.PocketGlowOpponent);

        if (_camera == null)
            _camera = Camera.main;
        Vector2 screen = _camera != null ? (Vector2)_camera.WorldToScreenPoint(PocketWorld(pocketIndex)) : Vector2.zero;
        GameEvents.RaiseBallPocketedFx(number, screen, kind, velocity.magnitude);
    }

    // Balls that dropped this turn leave the pockets.
    public void HidePocketed(BallState[] balls)
    {
        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i].Pocketed && _balls[i].InPocket)
                _balls[i].Hide();
        }
    }

    private void GlowRim(int pocketIndex, float radius, Color color)
    {
        LineRenderer ring = _glows[pocketIndex];
        if (ring == null)
        {
            var go = new GameObject("PocketGlow" + pocketIndex);
            go.transform.SetParent(transform, false);
            ring = go.AddComponent<LineRenderer>();
            ring.sharedMaterial = _glowMaterial;
            ring.loop = true;
            ring.useWorldSpace = true;
            ring.positionCount = GlowSegments;
            ring.alignment = LineAlignment.TransformZ;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            go.transform.rotation = transform.rotation * Quaternion.Euler(90f, 0f, 0f);
            Vector2 center = Geometry.Pockets[pocketIndex].Center;
            for (int i = 0; i < GlowSegments; i++)
            {
                float angle = i / (float)GlowSegments * Mathf.PI * 2f;
                ring.SetPosition(i, TableToWorld(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + 0.008f), 0.002f));
            }
            _glows[pocketIndex] = ring;
        }
        _glowTime[pocketIndex] = 0f;
        _glowColor[pocketIndex] = color;
        ring.enabled = true;
    }
}
