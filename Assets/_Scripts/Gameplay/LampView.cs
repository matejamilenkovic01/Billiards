using System.Collections.Generic;
using UnityEngine;

// The billiard lamp over the table (implementation_definition_iteration_v4 V6). v5 V5: it hides only when, on screen, it
// would cover the table the player is looking at: more than MatchConfig.LampOccludeThreshold of the table's visible top
// (sampled on a grid), or any ball or pocket, with the lamp nearer than what it covers. It hides at once and shows
// again only after the view has been clear (under LampShowThreshold, nothing key covered) for LampShowDelay, so passing
// a short gap never flickers it. It dissolves in and out over LampFadeTime (a dither, no transparency sorting). The
// hangers up to the ceiling are thin and never count.
public class LampView : MonoBehaviour
{
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private const int GridX = 17, GridZ = 33;

    [SerializeField] private Renderer[] _renderers;
    [Tooltip("World height of the shades' lower edge.")]
    [SerializeField] private float _bottom;
    [Tooltip("The table's centre on the cloth (world).")]
    [SerializeField] private Vector3 _tableCenter;
    [Tooltip("Half the table's outer size (frame included), x and z.")]
    [SerializeField] private Vector2 _tableHalfSize = new(0.715f, 1.275f);
    [SerializeField] private MatchConfig _config;

    private MaterialPropertyBlock _block;
    private Bounds _bounds;
    private readonly List<Bounds> _parts = new();
    private readonly List<Vector3> _keys = new();
    private TableView _table;
    private float _shown = 1f;
    private float _clearSince = -10f;
    private bool _clear;

    public bool Hidden { get; private set; }
    public float Bottom => _bottom;
    public Bounds Bounds => _bounds;
    // What the lamp covered in the last frame (checks).
    public float LastCoverage { get; private set; }
    public bool LastCoversKey { get; private set; }

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        _bounds = _renderers.Length > 0 ? _renderers[0].bounds : new Bounds(transform.position, Vector3.zero);
        foreach (Renderer renderer in _renderers)
        {
            _bounds.Encapsulate(renderer.bounds);
            if (!renderer.name.StartsWith("Hanger"))
                _parts.Add(renderer.bounds);
        }
        Apply();
    }

    private void Start() => _table = FindFirstObjectByType<TableView>();

    // The share of the table's visible top the lamp's parts cover on this camera's screen (0..1), and whether they
    // cover any of `keys` (balls, pockets): a ray from the eyes to each point of a grid on the table (and to each key)
    // against the parts' boxes; only points on screen count.
    public static float Coverage(Camera camera, IReadOnlyList<Bounds> parts, Vector3 tableCenter, Vector2 halfSize, IReadOnlyList<Vector3> keys, out bool coversKey)
    {
        Vector3 eye = camera.transform.position;
        bool Covered(Vector3 point, out bool visible)
        {
            Vector3 v = camera.WorldToViewportPoint(point);
            visible = v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
            if (!visible)
                return false;
            Vector3 to = point - eye;
            float length = to.magnitude;
            var ray = new Ray(eye, to);
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].IntersectRay(ray, out float hit) && hit < length)
                    return true;
            }
            return false;
        }

        int seen = 0, covered = 0;
        for (int ix = 0; ix < GridX; ix++)
        for (int iz = 0; iz < GridZ; iz++)
        {
            var point = tableCenter + new Vector3(Mathf.Lerp(-halfSize.x, halfSize.x, ix / (GridX - 1f)), 0f, Mathf.Lerp(-halfSize.y, halfSize.y, iz / (GridZ - 1f)));
            bool hit = Covered(point, out bool visible);
            if (!visible)
                continue;
            seen++;
            if (hit)
                covered++;
        }
        coversKey = false;
        if (keys != null)
        {
            foreach (Vector3 key in keys)
                coversKey |= Covered(key, out _);
        }
        return seen > 0 ? covered / (float)seen : 0f;
    }

    // Clear: under the show threshold with nothing key covered.
    public static bool IsClear(float coverage, bool coversKey, MatchConfig config) => coverage < config.LampShowThreshold && !coversKey;

    // The next hidden state: hide at once over the threshold or on a ball / pocket; show once the view has been clear
    // for LampShowDelay.
    public static bool NextHidden(bool hidden, float coverage, bool coversKey, float clearFor, MatchConfig config)
    {
        if (!hidden)
            return coverage > config.LampOccludeThreshold || coversKey;
        return !(IsClear(coverage, coversKey, config) && clearFor >= config.LampShowDelay);
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null || _config == null)
            return;
        _keys.Clear();
        if (_table != null)
        {
            for (int i = 0; i < BallGroups.BallCount; i++)
            {
                BallView ball = _table.Ball(i);
                if (ball != null && ball.gameObject.activeInHierarchy && !ball.InPocket)
                    _keys.Add(ball.transform.position);
            }
            for (int p = 0; p < _table.Geometry.Pockets.Length; p++)
                _keys.Add(_table.TableToWorld(PocketLook.Of(_table.Geometry, _config, p).Center, 0f));
        }
        LastCoverage = Coverage(camera, _parts, _tableCenter, _tableHalfSize, _keys, out bool coversKey);
        LastCoversKey = coversKey;
        bool clear = IsClear(LastCoverage, coversKey, _config);
        if (clear && !_clear)
            _clearSince = Time.unscaledTime;
        _clear = clear;
        Hidden = NextHidden(Hidden, LastCoverage, coversKey, clear ? Time.unscaledTime - _clearSince : 0f, _config);
        float target = Hidden ? 0f : 1f;
        if (Mathf.Approximately(_shown, target))
            return;
        _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, _config.LampFadeTime));
        Apply();
    }

    private void Apply()
    {
        // Cutoff 0 keeps every pixel of the dither; just above 1 drops them all.
        _block.SetFloat(CutoffId, (1f - _shown) * 1.01f);
        foreach (Renderer renderer in _renderers)
        {
            renderer.SetPropertyBlock(_block);
            renderer.enabled = _shown > 0f;
        }
    }

#if UNITY_EDITOR
    public void Configure(Renderer[] renderers, float bottom, Vector3 tableCenter, Vector2 tableHalfSize, MatchConfig config)
    {
        _renderers = renderers;
        _bottom = bottom;
        _tableCenter = tableCenter;
        _tableHalfSize = tableHalfSize;
        _config = config;
    }
#endif
}
