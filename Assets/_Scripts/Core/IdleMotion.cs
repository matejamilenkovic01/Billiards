using DG.Tweening;
using UnityEngine;

// Endless ambient motion for menu elements (works on UI and world objects alike).
public class IdleMotion : MonoBehaviour
{
    private enum Mode { Bob, Pulse }

    [SerializeField] private Mode _mode = Mode.Bob;
    [Tooltip("Bob: vertical distance in local units. Pulse: target scale.")]
    [SerializeField] private float _amount = 20f;
    [SerializeField] private float _halfPeriod = 0.9f;

    private Vector3 _restPosition;
    private Vector2 _restAnchored;
    private Vector3 _restScale;

    // UI bobs in anchored space: a parent resized after Awake (the safe area) doesn't move the rest point.
    private RectTransform Rect => transform as RectTransform;

    private void Awake()
    {
        _restPosition = transform.localPosition;
        if (Rect != null)
            _restAnchored = Rect.anchoredPosition;
        _restScale = transform.localScale;
    }

    private void OnEnable()
    {
        Tween tween = _mode == Mode.Pulse ? transform.DOScale(_restScale * _amount, _halfPeriod)
            : Rect != null ? Rect.DOAnchorPosY(_restAnchored.y + _amount, _halfPeriod)
            : transform.DOLocalMoveY(_restPosition.y + _amount, _halfPeriod);

        tween.SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
    }

    private void OnDisable()
    {
        transform.DOKill();
        if (Rect != null)
            Rect.anchoredPosition = _restAnchored;
        else
            transform.localPosition = _restPosition;
        transform.localScale = _restScale;
    }
}
