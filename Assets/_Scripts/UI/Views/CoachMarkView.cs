using DG.Tweening;
using TMPro;
using UnityEngine;

// Coach marks (gameplay redesign G10, v3 V2/V9): a ghost hand shows a gesture next to a short balloon. FreeLook:
// over the whole table without darkening it, one finger circles, then two fingers pinch apart. Power: the hand
// drags the grip down. FineAim: it drags the wheel sideways. Other targets: it taps the control. Balloon: text only.
// It goes after a few seconds or at the first touch. Never blocks input. v4 V8: the hand is a skin-tone emoji whose
// fingertip is its centre; drags tilt it and leave a fading trail.
public class CoachMarkView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private RectTransform _hand;
    [SerializeField] private RectTransform _secondFinger;
    [SerializeField] private RectTransform _trail;
    [SerializeField] private float _dragTilt = 18f;
    [SerializeField] private float _duration = 3.2f;
    [SerializeField] private float _circleRadius = 110f;
    [SerializeField] private RectTransform _balloon;
    [Tooltip("Controls a coach mark can point at (same order as _targetRects).")]
    [SerializeField] private CoachTarget[] _targets = new CoachTarget[0];
    [SerializeField] private RectTransform[] _targetRects = new RectTransform[0];
    [Tooltip("Shown areas the balloon must not cover (the lesson card).")]
    [SerializeField] private RectTransform[] _avoid = new RectTransform[0];
    [Tooltip("Areas the balloon never covers, shown or not (the toast slot: a message can appear later).")]
    [SerializeField] private RectTransform[] _reserved = new RectTransform[0];
    [SerializeField] private float _balloonGap = 40f;

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
            // Hand on the control; the balloon on the first side (above, below, left, right) where it stays on screen
            // and covers neither the control nor the lesson card.
            Vector2 local = LocalOf(rect);
            Place(local, BalloonSpot(rect));
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

    private Vector2 BalloonSpot(RectTransform target)
    {
        if (_balloon == null)
            return Vector2.zero;
        Rect area = ((RectTransform)_hand.parent).rect;
        Rect control = LocalRect(target);
        Vector2 half = _balloon.rect.size * 0.5f;
        float x = Mathf.Clamp(control.center.x, area.xMin + half.x, area.xMax - half.x);
        float y = Mathf.Clamp(control.center.y, area.yMin + half.y, area.yMax - half.y);
        // Each side starts next to the control and moves outwards until nothing shown is under the balloon.
        (Vector2 start, Vector2 step)[] sides =
        {
            (new Vector2(x, control.yMax + _balloonGap + half.y), Vector2.up),
            (new Vector2(x, control.yMin - _balloonGap - half.y), Vector2.down),
            (new Vector2(control.xMin - _balloonGap - half.x, y), Vector2.left),
            (new Vector2(control.xMax + _balloonGap + half.x, y), Vector2.right)
        };
        foreach ((Vector2 start, Vector2 step) in sides)
        {
            for (Vector2 spot = start; ; spot += step * 20f)
            {
                var balloon = new Rect(spot - half, _balloon.rect.size);
                if (balloon.xMin < area.xMin || balloon.xMax > area.xMax || balloon.yMin < area.yMin || balloon.yMax > area.yMax)
                    break;
                if (IsFree(balloon, control))
                    return spot;
            }
        }
        return sides[0].start;
    }

    private bool IsFree(Rect balloon, Rect control)
    {
        if (balloon.Overlaps(control))
            return false;
        foreach (RectTransform reserved in _reserved)
        {
            if (reserved != null && balloon.Overlaps(LocalRect(reserved)))
                return false;
        }
        foreach (RectTransform avoid in _avoid)
        {
            if (avoid != null && avoid.gameObject.activeInHierarchy && IsShown(avoid) && balloon.Overlaps(LocalRect(avoid)))
                return false;
        }
        return true;
    }

    private static bool IsShown(RectTransform rect)
    {
        CanvasGroup group = rect.GetComponentInParent<CanvasGroup>();
        return group == null || group.alpha > 0.01f;
    }

    // A rect in the coach mark's own space (centred, like the anchored positions).
    private Rect LocalRect(RectTransform rect)
    {
        var parent = (RectTransform)_hand.parent;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector2 local = (Vector2)parent.InverseTransformPoint(corner) - parent.rect.center;
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
        SetTrail(false, Vector2.zero);
    }

    // The trail: a thin strip from where the drag started to the fingertip.
    private void SetTrail(bool on, Vector2 from)
    {
        if (_trail == null)
            return;
        _trail.gameObject.SetActive(on);
        if (!on)
            return;
        Vector2 delta = _hand.anchoredPosition - from;
        _trail.anchoredPosition = from + delta * 0.5f;
        _trail.sizeDelta = new Vector2(delta.magnitude, _trail.sizeDelta.y);
        _trail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // Power: drag down the strip. FineAim: sideways on the wheel. Else: two taps.
    private void PlayPointer(CoachTarget target, RectTransform rect)
    {
        _loop?.Kill();
        _hand.gameObject.SetActive(true);
        _hand.localScale = Vector3.one;
        _hand.localRotation = Quaternion.identity;
        _secondFinger.gameObject.SetActive(false);
        SetTrail(false, Vector2.zero);
        Vector2 start = _hand.anchoredPosition;
        Sequence loop = DOTween.Sequence();
        if (target == CoachTarget.Power)
        {
            start += new Vector2(0f, rect.rect.height * 0.35f);
            loop.AppendCallback(() => { _hand.anchoredPosition = start; _hand.localRotation = Quaternion.Euler(0f, 0f, _dragTilt); })
                .Append(_hand.DOAnchorPos(start - new Vector2(0f, rect.rect.height * 0.55f), 1.1f).SetEase(Ease.InOutSine)
                    .OnUpdate(() => SetTrail(true, start)))
                .AppendInterval(0.35f)
                .AppendCallback(() => SetTrail(false, start));
        }
        else if (target == CoachTarget.FineAim)
        {
            float reach = rect.rect.width * 0.25f;
            loop.AppendCallback(() => { _hand.anchoredPosition = start; _hand.localRotation = Quaternion.Euler(0f, 0f, _dragTilt); })
                .Append(_hand.DOAnchorPos(start + new Vector2(reach, 0f), 0.6f).SetEase(Ease.InOutSine).OnUpdate(() => SetTrail(true, start)))
                .Append(_hand.DOAnchorPos(start - new Vector2(reach, 0f), 1.0f).SetEase(Ease.InOutSine).OnUpdate(() => SetTrail(true, start)))
                .Append(_hand.DOAnchorPos(start, 0.5f).SetEase(Ease.InOutSine))
                .AppendCallback(() => SetTrail(false, start));
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
        _hand.localRotation = Quaternion.identity;
        _hand.anchoredPosition = _handRest + Vector2.right * _circleRadius;
        _secondFinger.gameObject.SetActive(false);
        SetTrail(false, Vector2.zero);
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
