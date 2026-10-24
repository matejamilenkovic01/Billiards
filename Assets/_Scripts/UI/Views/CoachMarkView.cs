using DG.Tweening;
using TMPro;
using UnityEngine;

// Coach marks (gameplay redesign G10, v3 V2/V9): a ghost hand shows a gesture next to a short balloon. FreeLook:
// over the whole table without darkening it, one finger circles, then two fingers pinch apart. Power: the hand
// drags the grip down. FineAim: it drags the wheel sideways. Other targets: it taps the control. Balloon: text only.
// It goes after a few seconds or at the first touch. Never blocks input.
public class CoachMarkView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private RectTransform _hand;
    [SerializeField] private RectTransform _secondFinger;
    [SerializeField] private float _duration = 3.2f;
    [SerializeField] private float _circleRadius = 110f;
    [SerializeField] private RectTransform _balloon;
    [Tooltip("Controls a coach mark can point at (same order as _targetRects).")]
    [SerializeField] private CoachTarget[] _targets = new CoachTarget[0];
    [SerializeField] private RectTransform[] _targetRects = new RectTransform[0];

    private Sequence _loop;
    private Vector2 _handRest;
    private Vector2 _balloonRest;

    private void Awake()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _handRest = _hand.anchoredPosition;
        _balloonRest = _balloon != null ? _balloon.anchoredPosition : Vector2.zero;
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
        RectTransform rect = RectOf(target);
        if (target == CoachTarget.FreeLook || rect == null)
        {
            Place(_handRest, _balloonRest);
            if (target == CoachTarget.FreeLook)
                PlayGesture();
            else
                HideHand();
        }
        else
        {
            // Hand on the control, the balloon just above it (below when there is no room above).
            Vector2 local = LocalOf(rect);
            float offset = rect.rect.height * 0.5f + 90f;
            float top = ((RectTransform)_hand.parent).rect.height * 0.5f - 160f;
            Place(local, new Vector2(0f, local.y + offset <= top ? local.y + offset : local.y - offset));
            PlayPointer(target, rect);
        }
        DOVirtual.DelayedCall(_duration * (target == CoachTarget.FreeLook ? 1f : 1.5f), Hide, true).SetLink(gameObject).SetTarget(_group);
    }

    private RectTransform RectOf(CoachTarget target)
    {
        int index = System.Array.IndexOf(_targets, target);
        return index >= 0 && index < _targetRects.Length ? _targetRects[index] : null;
    }

    // The target's centre in the coach mark's own space (its anchors sit in the middle).
    private Vector2 LocalOf(RectTransform rect)
    {
        var parent = (RectTransform)_hand.parent;
        Vector3 world = rect.TransformPoint(rect.rect.center);
        return (Vector2)parent.InverseTransformPoint(world) - parent.rect.center;
    }

    private void Place(Vector2 hand, Vector2 balloon)
    {
        _hand.anchoredPosition = hand;
        if (_balloon != null)
            _balloon.anchoredPosition = balloon;
    }

    private void HideHand()
    {
        _loop?.Kill();
        _loop = null;
        _hand.gameObject.SetActive(false);
        _secondFinger.gameObject.SetActive(false);
    }

    // Power: drag down the strip. FineAim: sideways on the wheel. Else: two taps.
    private void PlayPointer(CoachTarget target, RectTransform rect)
    {
        _loop?.Kill();
        _hand.gameObject.SetActive(true);
        _hand.localScale = Vector3.one;
        _secondFinger.gameObject.SetActive(false);
        Vector2 start = _hand.anchoredPosition;
        Sequence loop = DOTween.Sequence();
        if (target == CoachTarget.Power)
        {
            start += new Vector2(0f, rect.rect.height * 0.35f);
            loop.AppendCallback(() => _hand.anchoredPosition = start)
                .Append(_hand.DOAnchorPos(start - new Vector2(0f, rect.rect.height * 0.55f), 1.1f).SetEase(Ease.InOutSine))
                .AppendInterval(0.35f);
        }
        else if (target == CoachTarget.FineAim)
        {
            float reach = rect.rect.width * 0.25f;
            loop.AppendCallback(() => _hand.anchoredPosition = start)
                .Append(_hand.DOAnchorPos(start + new Vector2(reach, 0f), 0.6f).SetEase(Ease.InOutSine))
                .Append(_hand.DOAnchorPos(start - new Vector2(reach, 0f), 1.0f).SetEase(Ease.InOutSine))
                .Append(_hand.DOAnchorPos(start, 0.5f).SetEase(Ease.InOutSine));
        }
        else
        {
            loop.AppendCallback(() => _hand.anchoredPosition = start)
                .Append(_hand.DOScale(0.85f, 0.15f)).Append(_hand.DOScale(1f, 0.15f))
                .Append(_hand.DOScale(0.85f, 0.15f)).Append(_hand.DOScale(1f, 0.15f))
                .AppendInterval(0.6f);
        }
        _loop = loop.SetLoops(-1).SetUpdate(true).SetLink(gameObject);
    }

    // One finger draws a circle (orbit), then two fingers spread apart (zoom); repeats while shown.
    private void PlayGesture()
    {
        _loop?.Kill();
        _hand.gameObject.SetActive(true);
        _hand.localScale = Vector3.one;
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
        _hand.localScale = Vector3.one;
        _group.DOKill();
        _group.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
    }
}
