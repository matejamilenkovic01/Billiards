using UnityEngine;

// The billiard lamp over the table (implementation_definition_iteration_v4 V6). It hides smoothly (a 0.25 s dither
// dissolve, no transparency sorting) whenever the camera rises above its lower edge less HideMargin, or when it
// stands between the camera and the table's centre; it comes back the same way.
public class LampView : MonoBehaviour
{
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

    [SerializeField] private Renderer[] _renderers;
    [Tooltip("World height of the shades' lower edge.")]
    [SerializeField] private float _bottom;
    [SerializeField] private float _hideMargin = 0.1f;
    [Tooltip("Once hidden, the camera must come this much lower again before the lamp shows (no flicker at the edge).")]
    [SerializeField] private float _hysteresis = 0.05f;
    [SerializeField] private float _fadeTime = 0.25f;
    [Tooltip("The table's centre on the cloth (world).")]
    [SerializeField] private Vector3 _tableCenter;

    private MaterialPropertyBlock _block;
    private Bounds _bounds;
    private float _shown = 1f;

    public bool Hidden { get; private set; }
    public float Bottom => _bottom;
    public Bounds Bounds => _bounds;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        _bounds = _renderers.Length > 0 ? _renderers[0].bounds : new Bounds(transform.position, Vector3.zero);
        foreach (Renderer renderer in _renderers)
            _bounds.Encapsulate(renderer.bounds);
        Apply();
    }

    // Would the lamp be hidden for a camera at `eye`?
    public bool HidesFor(Vector3 eye)
    {
        if (eye.y > _bottom - _hideMargin - (Hidden ? _hysteresis : 0f))
            return true;
        Vector3 toCenter = _tableCenter - eye;
        return _bounds.IntersectRay(new Ray(eye, toCenter), out float distance) && distance < toCenter.magnitude;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;
        Hidden = HidesFor(camera.transform.position);
        float target = Hidden ? 0f : 1f;
        if (Mathf.Approximately(_shown, target))
            return;
        _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, _fadeTime));
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
    public void Configure(Renderer[] renderers, float bottom, Vector3 tableCenter)
    {
        _renderers = renderers;
        _bottom = bottom;
        _tableCenter = tableCenter;
    }
#endif
}
