using DG.Tweening;
using TMPro;
using UnityEngine;

// Coach marks (gameplay redesign G10, v3 V2/V9): a ghost hand shows a gesture next to a short balloon. FreeLook:
// over the whole table without darkening it, one finger circles, then two fingers pinch apart; it goes after a few
// seconds or at the first touch. Never blocks input.
public class CoachMarkView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private RectTransform _hand;
    [SerializeField] private RectTransform _secondFinger;
    [SerializeField] private float _duration = 3.2f;
    [SerializeField] private float _circleRadius = 110f;

    private Sequence _loop;
    private Vector2 _handRest;

    private void Awake()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _handRest = _hand.anchoredPosition;
    }

    private void OnEnable()
    {
        GameEvents.OnCoachMarkRequested += HandleRequested;
        GameEvents.OnCoachMarkCleared += Hide;
        GameEvents.OnTablePressed += HandleTouched;
        GameEvents.OnOrbitDrag += HandleOrbitDrag;
        GameEvents.OnOrbitPinch += HandleOrbitPinch;
    }

    private void OnDisable()
    {
        GameEvents.OnCoachMarkRequested -= HandleRequested;
        GameEvents.OnCoachMarkCleared -= Hide;
        GameEvents.OnTablePressed -= HandleTouched;
        GameEvents.OnOrbitDrag -= HandleOrbitDrag;
        GameEvents.OnOrbitPinch -= HandleOrbitPinch;
        _loop?.Kill();
    }

    private void HandleRequested(CoachTarget target, string text)
    {
        _text.text = text;
        _group.DOKill();
        _group.DOFade(1f, 0.25f).SetUpdate(true).SetLink(gameObject);
        PlayGesture();
        DOVirtual.DelayedCall(_duration, Hide, true).SetLink(gameObject).SetTarget(_group);
    }

    // One finger draws a circle (orbit), then two fingers spread apart (zoom); repeats while shown.
    private void PlayGesture()
    {
        _loop?.Kill();
        _hand.anchoredPosition = _handRest + Vector2.right * _circleRadius;
        _secondFinger.gameObject.SetActive(false);
        float angle = 0f;
        _loop = DOTween.Sequence()
            .AppendCallback(() => { _secondFinger.gameObject.SetActive(false); angle = 0f; })
            .Append(DOTween.To(() => angle, a =>
            {
                angle = a;
                _hand.anchoredPosition = _handRest + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f) * _circleRadius;
            }, Mathf.PI * 2f, 1.3f).SetEase(Ease.InOutSine))
            .AppendCallback(() =>
            {
                _secondFinger.gameObject.SetActive(true);
                _hand.anchoredPosition = _handRest + new Vector2(30f, 30f);
                _secondFinger.anchoredPosition = _handRest - new Vector2(30f, 30f);
            })
            .Append(_hand.DOAnchorPos(_handRest + new Vector2(120f, 120f), 0.9f).SetEase(Ease.InOutSine))
            .Join(_secondFinger.DOAnchorPos(_handRest - new Vector2(120f, 120f), 0.9f).SetEase(Ease.InOutSine))
            .AppendInterval(0.3f)
            .SetLoops(-1)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void HandleTouched(Vector2 screenPosition) => Hide();

    private void HandleOrbitDrag(Vector2 delta) => Hide();

    private void HandleOrbitPinch(float scale) => Hide();

    private void Hide()
    {
        if (_group.alpha <= 0f && _loop == null)
            return;
        _loop?.Kill();
        _loop = null;
        _group.DOKill();
        _group.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
    }
}
