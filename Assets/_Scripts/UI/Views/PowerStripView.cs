using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Right-hand column showing the cue pointing up, labelled POWER with an arrow down and SOFT / MED / HARD
// along it (gameplay redesign G7). Put a finger anywhere on it and drag down to pull the cue back; lift to
// shoot. The pull range adapts to where you touch, so 100% is always within reach. Dragging back above the
// start (pull under the threshold) and letting go cancels. A ghost hand shows the gesture for new players.
public class PowerStripView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // The range stops this far short of the strip's bottom edge.
    public const float BottomMarginPixels = 20f;

    [SerializeField] private RectTransform _cue;
    [SerializeField] private Image _cueImage;
    [SerializeField] private float _cueTravel = 300f;
    [SerializeField] private TMP_Text _percent;
    [SerializeField] private Image _fill;
    [SerializeField] private Image _grabGlow;
    [SerializeField] private RectTransform _arrow;
    [SerializeField] private RectTransform _hintHand;
    [SerializeField] private CanvasGroup _hintGroup;
    [SerializeField] private float _hintTravel = 260f;

    private Canvas _canvas;
    private RectTransform _rect;
    private Vector2 _cueRest;
    private Vector2 _arrowRest;
    private Vector2 _handRest;
    private float _pullRange = 800f;
    private float _pullMinRange = 320f;
    private float _range = 800f;
    private float _startY;
    private bool _dragging;
    private float _pull;
    private int _hintShotsLeft;
    private float _idleHintSeconds = 8f;
    private float _idle;
    private bool _aiming;
    private Sequence _hintLoop;

    private float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

    // Pull range for a press `distanceToBottom` reference pixels above the strip's bottom edge.
    public static float PullRange(float distanceToBottom, float minRange, float maxRange) =>
        Mathf.Clamp(distanceToBottom - BottomMarginPixels, minRange, maxRange);

    public static float PullFromDrag(float dragDown, float range) => Mathf.Clamp01(dragDown / Mathf.Max(1f, range));

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _rect = (RectTransform)transform;
        _cueRest = _cue.anchoredPosition;
        _arrowRest = _arrow.anchoredPosition;
        _handRest = _hintHand.anchoredPosition;
        _grabGlow.color = new Color(1f, 1f, 1f, 0f);
        _hintGroup.alpha = 0f;
        Show(0f);
        // The arrow keeps nudging downwards: this way.
        _arrow.DOAnchorPosY(_arrowRest.y - 22f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
    }

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnReleaseMeterChanged += HandleReleaseMeterChanged;
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        GameEvents.OnShotFired += HandleShotFired;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnReleaseMeterChanged -= HandleReleaseMeterChanged;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        GameEvents.OnShotFired -= HandleShotFired;
        _dragging = false;
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _pullRange = info.PullRangePixels;
        _pullMinRange = info.PullMinRangePixels;
        _hintShotsLeft = info.PowerHintShotsLeft;
        _idleHintSeconds = info.PowerIdleHintSeconds;
        if (info.CueSprite != null)
            _cueImage.sprite = info.CueSprite;
    }

    private void Update()
    {
        if (!_aiming || _dragging)
            return;
        _idle += Time.unscaledDeltaTime;
        if (_idle >= _idleHintSeconds)
            SetHint(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _dragging = true;
        _startY = eventData.position.y;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out Vector2 local);
        _range = PullRange(local.y - _rect.rect.yMin, _pullMinRange, _pullRange);
        _cue.DOKill();
        _idle = 0f;
        SetHint(false);
        // Got hold of the cue: it lights up before anything moves.
        _grabGlow.DOKill();
        _grabGlow.DOFade(0.9f, 0.08f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaisePullStarted();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;
        _pull = PullFromDrag((_startY - eventData.position.y) / Scale, _range);
        Show(_pull);
        GameEvents.RaisePullChanged(_pull);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_dragging)
            return;
        _dragging = false;
        _idle = 0f;
        GameEvents.RaisePullReleased();
        _pull = 0f;
        _grabGlow.DOKill();
        _grabGlow.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
        // Snap the strip's cue forward, like the stroke.
        DOTween.To(() => _cue.anchoredPosition.y, y => _cue.anchoredPosition = new Vector2(_cueRest.x, y), _cueRest.y, 0.12f)
            .SetEase(Ease.InQuad).SetUpdate(true).SetLink(gameObject).SetTarget(_cue);
        _percent.text = "";
        _fill.fillAmount = 0f;
    }

    private void HandleReleaseMeterChanged(float position, float power01) => Show(power01);

    private void HandleTurnPhaseChanged(TurnPhase phase)
    {
        if (phase != TurnPhase.Pulling && !_dragging)
            Show(0f);
        bool aiming = phase == TurnPhase.Aim;
        if (aiming && !_aiming)
            _idle = 0f;
        _aiming = aiming;
        // New players see the gesture whenever it's time to shoot.
        SetHint(aiming && _hintShotsLeft > 0);
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (feedback.IsPlayer && _hintShotsLeft > 0)
            _hintShotsLeft--;
    }

    private void Show(float pull)
    {
        _cue.anchoredPosition = _cueRest + Vector2.down * pull * _cueTravel;
        _percent.text = pull > 0.001f ? $"{Mathf.RoundToInt(pull * 100f)}%" : "";
        _fill.fillAmount = pull;
    }

    // Ghost hand: presses the top of the cue and pulls it down, every 3 s.
    private void SetHint(bool on)
    {
        bool running = _hintLoop != null && _hintLoop.IsActive();
        if (on == running)
            return;
        _hintLoop?.Kill();
        _hintLoop = null;
        if (!on)
        {
            _hintGroup.DOKill();
            _hintGroup.alpha = 0f;
            return;
        }

        _hintHand.anchoredPosition = _handRest;
        _hintGroup.alpha = 0f;
        _hintLoop = DOTween.Sequence()
            .AppendCallback(() => _hintHand.anchoredPosition = _handRest)
            .Append(_hintGroup.DOFade(0.85f, 0.3f))
            .Append(_hintHand.DOScale(0.85f, 0.15f))
            .Append(_hintHand.DOAnchorPosY(_handRest.y - _hintTravel, 1.2f).SetEase(Ease.InOutSine))
            .Append(_hintGroup.DOFade(0f, 0.35f))
            .Join(_hintHand.DOScale(1f, 0.35f))
            .AppendInterval(1f)
            .SetLoops(-1)
            .SetUpdate(true)
            .SetLink(gameObject);
    }
}
