using System;
using DG.Tweening;
using UnityEngine;

// The cue stick. Its mesh has the tip at the local origin and the butt along -Z, so the transform's
// forward is the shot direction (tilted down by the elevation) and its position is the tip.
public class CueView : MonoBehaviour
{
    [SerializeField] private MeshRenderer _renderer;
    [SerializeField] private MatchConfig _config;

    private Vector3 _ballCenter;
    private Vector3 _aimDirection = Vector3.forward;
    private float _pull01;
    private float _tipOffset;

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
        ApplyPose();
    }

    private void ApplyPose()
    {
        Quaternion rotation = Quaternion.LookRotation(_aimDirection, Vector3.up) * Quaternion.Euler(_config.CueElevation, 0f, 0f);
        Vector3 forward = rotation * Vector3.forward;
        transform.SetPositionAndRotation(_ballCenter - forward * _tipOffset, rotation);
    }

    // Quick stroke through the ball, then the cue gets out of the way while balls roll.
    public void Strike(Action onContact = null)
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
            .OnComplete(() => onContact?.Invoke())
            .SetTarget(transform)
            .SetLink(gameObject);

        DOVirtual.DelayedCall(_config.CueStrikeDuration + 0.25f, () => SetVisible(false), false)
            .SetTarget(transform)
            .SetLink(gameObject);
    }
}
