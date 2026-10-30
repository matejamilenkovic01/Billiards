using System;
using DG.Tweening;
using UnityEngine;

// The cue stick. Its mesh has the tip at the local origin and the butt along -Z, so the transform's
// forward is the shot direction (tilted down by the elevation) and its position is the tip.
// The butt rises just enough to clear cushions, rails and balls behind the cue ball (G6, display only),
// smoothly, and holds still while the cue is pulled back.
public class CueView : MonoBehaviour
{
    [SerializeField] private MeshRenderer _renderer;
    [SerializeField] private MeshFilter _filter;
    [SerializeField] private MatchConfig _config;
    [SerializeField] private ParticleSystem _chalkPuff;

    private Vector3 _ballCenter;
    private Vector3 _aimDirection = Vector3.forward;
    private float _pull01;
    private float _tipOffset;
    private BallState[] _balls;
    private TableView _table;
    private CueClearanceSettings _clearance;
    private float _elevation;
    private float _targetElevation;
    private float _elevationVelocity;
    private bool _snapElevation = true;
    private CueProfile _profile = CueProfile.Default;
    private PerfectEffect _effect = PerfectEffect.Default;
    private float _pulse;
    private float _flash;
    private MaterialPropertyBlock _block;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public float Elevation => _elevation;

    // The balls and table the cue has to clear (the simulation's live state).
    public void Bind(BallState[] balls, TableView table)
    {
        _balls = balls;
        _table = table;
        _clearance = _config.CueClearanceSettings();
        _clearance.Profile = _profile;
        _elevation = _targetElevation = _config.CueElevation;
    }

    public bool IsStriking { get; private set; }

    // The cue in hand (v4 V9): its mesh and material, the silhouette the elevation clears, its PERFECT effect.
    public void SetCue(CueDefinition cue)
    {
        if (cue == null)
            return;
        if (cue.Material != null)
            _renderer.sharedMaterial = cue.Material;
        if (cue.Mesh != null && _filter != null)
            _filter.sharedMesh = cue.Mesh;
        _profile = cue.Profile;
        _clearance.Profile = _profile;
        _snapElevation = true;
        _pulse = cue.EmissionPulse;
        _flash = 0f;
        _effect = cue.Perfect;
        ApplyEffect(_effect);
        if (_pulse <= 0f)
            _renderer.SetPropertyBlock(null);
    }

    private void ApplyEffect(PerfectEffect effect)
    {
        if (_chalkPuff == null)
            return;
        ParticleSystem.MainModule main = _chalkPuff.main;
        main.startColor = new ParticleSystem.MinMaxGradient(effect.Color, effect.Accent);
        main.startSize = new ParticleSystem.MinMaxCurve(effect.Size * 0.6f, effect.Size * 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(effect.Speed * 0.5f, effect.Speed);
        main.maxParticles = Mathf.Max(16, effect.Count + 4);
        ParticleSystem.EmissionModule emission = _chalkPuff.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, effect.Count - 2), (short)(effect.Count + 2)) });
        ParticleSystem.ShapeModule shape = _chalkPuff.shape;
        switch (effect.Shape)
        {
            case PerfectShape.Sparks:
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 55f;
                shape.radius = 0.002f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.gravityModifier = 0.35f;
                break;
            case PerfectShape.Ring:
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.003f;
                shape.radiusThickness = 0f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
                main.gravityModifier = 0f;
                break;
            default:
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.004f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.gravityModifier = 0.05f;
                break;
        }
    }

    // Legend: the glow breathes; a PERFECT makes it flare.
    private void Update()
    {
        if (_pulse <= 0f && _flash <= 0f)
            return;
        _block ??= new MaterialPropertyBlock();
        float level = 1f - _pulse * 0.5f * (1f + Mathf.Sin(Time.time * 2.2f)) + _flash;
        _renderer.GetPropertyBlock(_block);
        _block.SetColor(EmissionColor, Color.white * Mathf.Max(0f, level));
        _renderer.SetPropertyBlock(_block);
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
            transform.DOKill();
        if (visible && !gameObject.activeSelf)
            _snapElevation = true;
        IsStriking = false;
        gameObject.SetActive(visible);
    }

    // ballCenter in world space, aimDirection horizontal (world), pull 0..1.
    public void SetPose(Vector3 ballCenter, Vector3 aimDirection, float pull01)
    {
        if (IsStriking)
            return;

        _ballCenter = ballCenter;
        _aimDirection = new Vector3(aimDirection.x, 0f, aimDirection.z).normalized;
        _pull01 = Mathf.Clamp01(pull01);
        _tipOffset = _config.BallRadius + _config.CueTipGap + _pull01 * _config.CuePullDistance;
        UpdateElevation();
        ApplyPose();
    }

    private void UpdateElevation()
    {
        if (_balls == null || _table == null)
        {
            _elevation = _config.CueElevation;
            return;
        }
        // Held while pulling: the clearance already covers the butt's full pull back.
        if (_pull01 <= 0.001f || _snapElevation)
        {
            float needed = CueClearance.Elevation(_clearance, _balls, _table.WorldToTable(_ballCenter),
                _table.WorldDirectionToTable(_aimDirection), _config.BallRadius + _config.CueTipGap);
            if (needed > _targetElevation || _targetElevation - needed >= _config.CueElevationMinChange)
                _targetElevation = needed;
        }
        if (_snapElevation)
        {
            _elevation = _targetElevation;
            _elevationVelocity = 0f;
            _snapElevation = false;
            return;
        }
        _elevation = Mathf.SmoothDamp(_elevation, _targetElevation, ref _elevationVelocity, _config.CueElevationSmooth);
    }

    // The pose the cue settles into for this aim (no smoothing), for checks.
    public static Quaternion PoseRotation(Vector3 aimDirection, float elevation) =>
        Quaternion.LookRotation(aimDirection, Vector3.up) * Quaternion.Euler(elevation, 0f, 0f);

    private void ApplyPose()
    {
        Quaternion rotation = PoseRotation(_aimDirection, _elevation);
        Vector3 forward = rotation * Vector3.forward;
        transform.SetPositionAndRotation(_ballCenter - forward * _tipOffset, rotation);
    }

    // Quick stroke through the ball, then the cue gets out of the way while balls roll.
    public void Strike(Action onContact = null, bool chalkPuff = false)
    {
        if (!gameObject.activeSelf)
        {
            onContact?.Invoke();
            return;
        }

        IsStriking = true;
        transform.DOKill();
        DOTween.To(() => _tipOffset, value =>
            {
                _tipOffset = value;
                ApplyPose();
            }, _config.BallRadius, _config.CueStrikeDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                if (chalkPuff && _chalkPuff != null)
                {
                    _chalkPuff.transform.position = transform.position;
                    _chalkPuff.Play();
                    if (_effect.CueFlash > 0f)
                        DOTween.To(() => _flash, v => _flash = v, 0f, 0.5f).From(_effect.CueFlash).SetTarget(transform).SetLink(gameObject);
                }
                onContact?.Invoke();
            })
            .SetTarget(transform)
            .SetLink(gameObject);

        DOVirtual.DelayedCall(_config.CueStrikeDuration + 0.25f, () => SetVisible(false), false)
            .SetTarget(transform)
            .SetLink(gameObject);
    }
}
