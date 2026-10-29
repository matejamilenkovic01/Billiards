using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Where the cue hits the cue ball (implementation_definition_iteration_v4 V2). A small cue ball above POWER shows
// the tip as a red dot (and pulses while it is off centre); a tap opens a big cue ball in the lower half of the
// screen: drag the marker (it stops at the SpinMaxOffset ring), double tap for the centre, tap outside or the
// check to close. The aim guide follows live. Gameplay puts it back in the centre after every shot.
public class SpinControlView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform _iconDot;
    [SerializeField] private Image _iconFrame;
    [SerializeField] private CanvasGroup _overlay;
    [SerializeField] private RectTransform _ball;
    [SerializeField] private RectTransform _marker;
    [SerializeField] private RectTransform _limitRing;
    [SerializeField] private PointerRelay _ballInput;
    [SerializeField] private PointerRelay _backdrop;
    [SerializeField] private UIButton _closeButton;
    [Tooltip("The big ball's drawn radius in its own pixels (one ball radius).")]
    [SerializeField] private float _ballRadius = 256f;
    [Tooltip("The icon's ball radius in its own pixels.")]
    [SerializeField] private float _iconRadius = 46f;

    private readonly SpinPicker _picker = new();
    private Canvas _canvas;
    private Tween _pulse;

    public Vector2 Tip => _picker.Tip;
    public bool IsOpen => _overlay != null && _overlay.blocksRaycasts;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        SetOpen(false);
        Apply();
        // Also while hidden (the intro, placing the cue ball, the bot's turn): the match start (the offset limit)
        // and a reset then must not be missed.
        GameEvents.OnSpinReset += HandleSpinReset;
        GameEvents.OnMatchStarted += HandleMatchStarted;
    }

    private void OnDestroy()
    {
        GameEvents.OnSpinReset -= HandleSpinReset;
        GameEvents.OnMatchStarted -= HandleMatchStarted;
    }

    private void OnEnable()
    {
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        _ballInput.Down += HandleBallDown;
        _ballInput.Dragged += HandleBallDragged;
        _backdrop.Clicked += HandleBackdropClicked;
        _closeButton.Clicked += Close;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        _ballInput.Down -= HandleBallDown;
        _ballInput.Dragged -= HandleBallDragged;
        _backdrop.Clicked -= HandleBackdropClicked;
        _closeButton.Clicked -= Close;
        SetOpen(false);
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _picker.MaxOffset = info.SpinMaxOffset;
        _limitRing.sizeDelta = Vector2.one * (2f * _ballRadius * info.SpinMaxOffset + 8f);
    }

    // Only while aiming: a pull, LOOK, ball in hand or the shot close it.
    private void HandleTurnPhaseChanged(TurnPhase phase)
    {
        if (phase != TurnPhase.Aim)
            SetOpen(false);
    }

    private void HandleSpinReset()
    {
        _picker.Reset();
        Apply();
    }

    public void OnPointerClick(PointerEventData eventData) => SetOpen(true);

    public void Close() => SetOpen(false);

    private void HandleBackdropClicked(PointerEventData eventData) => Close();

    private void HandleBallDown(PointerEventData eventData)
    {
        if (_picker.Press(Offset(eventData), _ballRadius, Time.unscaledTime))
            GameEvents.RaiseButtonClicked();
        Raise();
    }

    private void HandleBallDragged(PointerEventData eventData)
    {
        _picker.Drag(Offset(eventData), _ballRadius);
        Raise();
    }

    private Vector2 Offset(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_ball, eventData.position, eventData.pressEventCamera, out Vector2 local);
        return local;
    }

    private void Raise()
    {
        GameEvents.RaiseSpinChanged(_picker.Tip);
        Apply();
    }

    private void SetOpen(bool open)
    {
        if (_overlay == null)
            return;
        _overlay.DOKill();
        _overlay.blocksRaycasts = open;
        _overlay.interactable = open;
        if (open)
        {
            _overlay.gameObject.SetActive(true);
            _overlay.DOFade(1f, 0.15f).SetUpdate(true).SetLink(_overlay.gameObject);
        }
        else
        {
            _overlay.alpha = 0f;
            _overlay.gameObject.SetActive(false);
        }
    }

    // The marker and the icon's dot at the tip; the icon's frame pulses while the tip is off centre.
    private void Apply()
    {
        Vector2 tip = _picker.Tip;
        _marker.anchoredPosition = tip * _ballRadius;
        _iconDot.anchoredPosition = tip * _iconRadius;
        bool off = tip != Vector2.zero;
        if (off && _pulse == null)
        {
            _iconFrame.color = new Color(1f, 0.84f, 0.29f, 1f);
            _pulse = _iconFrame.transform.DOScale(1.12f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject);
        }
        else if (!off && _pulse != null)
        {
            _pulse.Kill();
            _pulse = null;
            _iconFrame.transform.localScale = Vector3.one;
            _iconFrame.color = new Color(1f, 1f, 1f, 0.55f);
        }
        else if (!off)
        {
            _iconFrame.color = new Color(1f, 1f, 1f, 0.55f);
        }
    }

#if UNITY_EDITOR
    // Editor testing: the tip as if the marker had been dragged there.
    public void DebugSetTip(Vector2 tip)
    {
        _picker.Drag(tip * _ballRadius, _ballRadius);
        Raise();
    }

    public void DebugOpen(bool open) => SetOpen(open);
#endif
}
