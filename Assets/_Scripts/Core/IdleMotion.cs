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
    private Vector3 _restScale;

    private void Awake()
    {
        _restPosition = transform.localPosition;
        _restScale = transform.localScale;
    }

    private void OnEnable()
    {
        Tween tween = _mode == Mode.Bob
            ? transform.DOLocalMoveY(_restPosition.y + _amount, _halfPeriod)
            : transform.DOScale(_restScale * _amount, _halfPeriod);

        tween.SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
    }

    private void OnDisable()
    {
        transform.DOKill();
        transform.localPosition = _restPosition;
        transform.localScale = _restScale;
    }
}
