using DG.Tweening;
using TMPro;
using UnityEngine;

// After your release (v3 V7, G4): a stamp above the cue ball instead of a toast. PERFECT green, GOOD yellow,
// MISS red; it punches in, holds a moment and fades. The bot's release is only heard, not stamped.
public class ReleaseStampView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _stamp;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private float _above = 170f;
    [SerializeField] private Color _perfect = new(0.4f, 1f, 0.5f);
    [SerializeField] private Color _good = new(1f, 0.85f, 0.3f);
    [SerializeField] private Color _miss = new(1f, 0.35f, 0.3f);

    private void Awake() => _group.alpha = 0f;

    private void OnEnable() => GameEvents.OnShotFired += HandleShotFired;

    private void OnDisable() => GameEvents.OnShotFired -= HandleShotFired;

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (!feedback.IsPlayer)
            return;
        _label.text = feedback.Zone == ReleaseZone.Green ? "PERFECT" : feedback.Zone == ReleaseZone.Yellow ? "GOOD" : "MISS";
        _label.color = feedback.Zone == ReleaseZone.Green ? _perfect : feedback.Zone == ReleaseZone.Yellow ? _good : _miss;
        // Above the cue ball, kept on screen.
        var parent = (RectTransform)_stamp.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, feedback.CueBallScreen, null, out Vector2 local);
        Rect bounds = parent.rect;
        local.y += _above;
        local.x = Mathf.Clamp(local.x, bounds.xMin + 220f, bounds.xMax - 220f);
        local.y = Mathf.Clamp(local.y, bounds.yMin + 300f, bounds.yMax - 420f);
        _stamp.anchoredPosition = local;

        _stamp.DOKill();
        _group.DOKill();
        _stamp.localScale = Vector3.one * 1.8f;
        _stamp.localRotation = Quaternion.Euler(0f, 0f, feedback.Zone == ReleaseZone.Green ? -6f : 4f);
        _group.alpha = 1f;
        DOTween.Sequence()
            .Append(_stamp.DOScale(1f, 0.18f).SetEase(Ease.OutBack))
            .AppendInterval(0.35f)
            .Append(_group.DOFade(0f, 0.3f))
            .SetUpdate(true)
            .SetLink(gameObject);
    }
}
