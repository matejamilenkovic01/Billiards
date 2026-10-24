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

    public float Elevation => _elevation;

    // The balls and table the cue has to clear (the simulation's live state).
    public void Bind(BallState[] balls, TableView table)
    {
        _balls = balls;
        _table = table;
        _clearance = _config.CueClearanceSettings();
        _elevation = _targetElevation = _config.CueElevation;
    }

    public bool IsStriking { get; private set; }

    public void SetCue(CueDefinition cue)
    {
        if (cue != null && cue.Material != null)
            _renderer.sharedMaterial = cue.Material;
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
