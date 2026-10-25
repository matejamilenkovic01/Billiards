using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Right-hand column showing the cue pointing up with a grip ring at its top, labelled POWER with an arrow down
// (implementation_definition_v3 V5). A touch in the top part of the column grabs the grip; it then moves exactly
// with the finger (1 px = 1 px), down to 100% at the bottom of the strip; lift to shoot. SOFT / MED / HARD are
// fixed marks at 15 / 50 / 85%, and a small pointer rides with the grip. A touch lower down grabs nothing: the
// grip lights up and the ghost hand shows where to take it. Dragging back under the threshold and letting go
// cancels. The ghost hand also shows the gesture for new players.
public class PowerStripView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // 100% leaves the grip this far above the strip's bottom edge.
    public const float BottomMarginPixels = 20f;

    [SerializeField] private RectTransform _cue;
    [SerializeField] private Image _cueImage;
    [SerializeField] private RectTransform _grip;
    [SerializeField] private RectTransform _pointer;
    [SerializeField] private TMP_Text _percent;
    [SerializeField] private Image _fill;
    [SerializeField] private Image _grabGlow;
    [SerializeField] private RectTransform _arrow;
    [SerializeField] private RectTransform _hintHand;
    [SerializeField] private CanvasGroup _hintGroup;
    [Tooltip("v4 V5: shown over the strip while the shot must be called and no pocket is: the power is locked.")]
    [SerializeField] private GameObject _lock;

    private Canvas _canvas;
    private RectTransform _rect;
    private Vector2 _cueRest;
    private Vector2 _arrowRest;
    private Vector2 _handRest;
    private Vector2 _gripRest;
    private Vector2 _pointerRest;
    private float _travel = 895f;
    private float _grabZone = 0.4f;
    private float _startY;
    private bool _dragging;
    private float _pull;
    private int _hintShotsLeft;
    private float _idleHintSeconds = 8f;
    private float _idle;
    private bool _aiming;
    private Sequence _hintLoop;
    private bool _needsCall;
    private bool _called;

    private float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

    // 1:1: dragging `dragDown` reference pixels moves the grip as far, and the pull is that over the travel.
    public static float PullFromDrag(float dragDown, float travel) => Mathf.Clamp01(dragDown / Mathf.Max(1f, travel));

    // A press `fromTop` reference pixels below the strip's top edge grabs the grip.
    public static bool Grabs(float fromTop, float height, float grabZone) => fromTop >= 0f && fromTop <= height * grabZone;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _rect = (RectTransform)transform;
        _cueRest = _cue.anchoredPosition;
        _arrowRest = _arrow.anchoredPosition;
        _handRest = _hintHand.anchoredPosition;
        _gripRest = _grip.anchoredPosition;
        _pointerRest = _pointer.anchoredPosition;
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
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnPocketCallChanged += HandlePocketCallChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnReleaseMeterChanged -= HandleReleaseMeterChanged;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnPocketCallChanged -= HandlePocketCallChanged;
        _dragging = false;
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _travel = info.PullTravelPixels;
        _grabZone = info.PullGrabZone;
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

    private bool Locked => _needsCall && !_called;

    private void HandleTurnStarted(TurnInfo info)
    {
        _needsCall = info.IsHuman && info.NeedsCall;
        _called = info.CalledPocket >= 0;
        ApplyLock();
    }

    private void HandlePocketCallChanged(int pocket, PocketCallKind kind)
    {
        if (kind != PocketCallKind.Called)
            return;
        _called = pocket >= 0;
        ApplyLock();
    }

    private void ApplyLock()
    {
        if (_lock != null)
            _lock.SetActive(Locked);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (Locked)
        {
            // Not before a pocket is called: the pocket map lights up instead.
            _dragging = false;
            GameEvents.RaisePocketCallNudged();
            return;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out Vector2 local);
        _idle = 0f;
        if (!Grabs(_rect.rect.yMax - local.y, _rect.rect.height, _grabZone))
        {
            // Too low: nothing moves; the grip lights up and the hand shows where to take it.
            _dragging = false;
            FlashGrip(0.6f, 0.35f);
            SetHint(true);
            return;
        }
        _dragging = true;
        _startY = eventData.position.y;
        _cue.DOKill();
        SetHint(false);
        // Got hold of the grip: it lights up before anything moves.
        _grabGlow.DOKill();
        _grabGlow.DOFade(0.9f, 0.08f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaisePullStarted();
    }

    private void FlashGrip(float alpha, float duration)
    {
        _grabGlow.DOKill();
        _grabGlow.color = new Color(1f, 1f, 1f, alpha);
        _grabGlow.DOFade(0f, duration).SetUpdate(true).SetLink(gameObject);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;
        _pull = PullFromDrag((_startY - eventData.position.y) / Scale, _travel);
        Show(_pull);
        GameEvents.RaisePullChanged(_pull);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_dragging)
            return;
        _dragging = false;
        _idle = 0f;
        float from = _pull;
        GameEvents.RaisePullReleased();
        _pull = 0f;
        _grabGlow.DOKill();
        _grabGlow.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
        // Snap the strip's cue forward, like the stroke.
        DOTween.To(() => from, value => { from = value; Place(value); }, 0f, 0.12f)
            .SetEase(Ease.InQuad).SetUpdate(true).SetLink(gameObject).SetTarget(_cue);
        _percent.text = "";
        _fill.fillAmount = 0f;
    }

    private void HandleReleaseMeterChanged(float position, float power01, float greenHalf, float yellowEnd, float period) => Show(power01);

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
        Place(pull);
        _percent.text = pull > 0.001f ? $"{Mathf.RoundToInt(pull * 100f)}%" : "";
        _fill.fillAmount = pull;
    }

    // Cue, grip and pointer move down by exactly pull x travel reference pixels.
    private void Place(float pull)
    {
        Vector2 offset = Vector2.down * pull * _travel;
        _cue.anchoredPosition = _cueRest + offset;
        _grip.anchoredPosition = _gripRest + offset;
        _pointer.anchoredPosition = _pointerRest + offset;
    }

    // For checks: where the grip sits now, relative to its rest position (reference pixels, down = positive).
    public float GripOffset => _gripRest.y - _grip.anchoredPosition.y;

    // Ghost hand: takes the grip and pulls it down to MED, every 3 s.
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
            .Append(_hintHand.DOAnchorPosY(_handRest.y - 0.5f * _travel, 1.2f).SetEase(Ease.InOutSine))
            .Append(_hintGroup.DOFade(0f, 0.35f))
            .Join(_hintHand.DOScale(1f, 0.35f))
            .AppendInterval(1f)
            .SetLoops(-1)
            .SetUpdate(true)
            .SetLink(gameObject);
    }
}
