using DG.Tweening;
using UnityEngine.UI;
using UnityEngine;

// Horizontal release bar at the bottom (gameplay redesign G11), where the fine aim wheel is while aiming:
// green in the middle, yellow around it, red at the ends; zone sizes come from the selected cue. The marker
// sweeps left and right; letting go left of green sends the ball left of the aim, right of green right, so
// the side on the bar is the side of the miss. The harder you pull, the smaller the zones and the faster the
// marker (v3 V8): the zones follow the pull live, eased over ~0.1 s. After the shot the marker holds where you let go for a moment
// and the bar flashes in the zone's colour.
public class ReleaseBarView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _bar;
    [SerializeField] private RectTransform _yellow;
    [SerializeField] private RectTransform _green;
    [SerializeField] private RectTransform _indicator;
    [SerializeField] private Image _glow;
    [SerializeField] private Image _flash;
    [SerializeField] private Color _greenGlow = new(0.35f, 1f, 0.45f, 0.85f);
    [SerializeField] private Color _yellowGlow = new(1f, 0.85f, 0.25f, 0.55f);
    [SerializeField] private Color _redGlow = new(1f, 0.3f, 0.25f, 0.45f);

    private const float ZoneEaseTime = 0.035f;   // exponential: ~95% of a change within 0.1 s

    private float _greenHalf = 0.1f;
    private float _yellowEnd = 0.4f;
    private float _shownGreenHalf = 0.1f;
    private float _shownYellowEnd = 0.4f;
    private float _freeze = 0.5f;
    private bool _visible;
    private bool _inGreen;
    private bool _pulling;

    private float HalfWidth => _bar.rect.width * 0.5f;

    // ShotAccuracy turns a positive bar position to the shooter's left, so positive is drawn on the left.
    public static float DisplaySide(float position) => -position;

    private void Awake()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _flash.color = Color.clear;
    }

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnReleaseMeterSetup += HandleSetup;
        GameEvents.OnReleaseMeterChanged += HandleChanged;
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        GameEvents.OnShotFired += HandleShotFired;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnReleaseMeterSetup -= HandleSetup;
        GameEvents.OnReleaseMeterChanged -= HandleChanged;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        GameEvents.OnShotFired -= HandleShotFired;
    }

    private void HandleMatchStarted(MatchStartInfo info) => _freeze = info.ReleaseFreezeDuration;

    private void HandleSetup(ReleaseMeterSettings settings)
    {
        _greenHalf = _shownGreenHalf = settings.GreenHalf;
        _yellowEnd = _shownYellowEnd = settings.GreenHalf + settings.YellowWidth;
        Layout();
    }

    private void Layout()
    {
        float half = HalfWidth;
        _green.sizeDelta = new Vector2(_shownGreenHalf * 2f * half, _green.sizeDelta.y);
        _yellow.sizeDelta = new Vector2(Mathf.Min(1f, _shownYellowEnd) * 2f * half, _yellow.sizeDelta.y);
    }

    private void HandleChanged(float position, float power01, float greenHalf, float yellowEnd, float period)
    {
        if (!_pulling)
            return;
        _greenHalf = greenHalf;
        _yellowEnd = yellowEnd;
        if (!_visible)
        {
            _shownGreenHalf = greenHalf;
            _shownYellowEnd = yellowEnd;
            SetVisible(true);
        }
        else
        {
            float ease = 1f - Mathf.Exp(-Time.unscaledDeltaTime / ZoneEaseTime);
            _shownGreenHalf = Mathf.Lerp(_shownGreenHalf, greenHalf, ease);
            _shownYellowEnd = Mathf.Lerp(_shownYellowEnd, yellowEnd, ease);
            Layout();
        }
        _indicator.anchoredPosition = new Vector2(DisplaySide(position) * HalfWidth, _indicator.anchoredPosition.y);

        float distance = Mathf.Abs(position);
        _glow.color = distance <= _greenHalf ? _greenGlow : distance <= _yellowEnd ? _yellowGlow : _redGlow;

        // The green zone breathes while the marker is inside it.
        bool inGreen = distance <= _greenHalf;
        if (inGreen != _inGreen)
        {
            _inGreen = inGreen;
            _green.DOKill();
            _green.localScale = Vector3.one;
            _indicator.DOKill();
            _indicator.localScale = Vector3.one;
            if (inGreen)
            {
                _green.DOScale(new Vector3(1f, 1.25f, 1f), 0.12f).SetLoops(2, LoopType.Yoyo).SetUpdate(true).SetLink(_green.gameObject);
                _indicator.DOScale(1.12f, 0.1f).SetLoops(2, LoopType.Yoyo).SetUpdate(true).SetLink(_indicator.gameObject);
            }
        }
    }

    private void HandleTurnPhaseChanged(TurnPhase phase)
    {
        bool wasPulling = _pulling;
        _pulling = phase == TurnPhase.Pulling;
        if (_pulling)
            return;
        // A shot: hold the marker where the finger let go, then fade. A cancel: just go.
        if (wasPulling && phase == TurnPhase.Shooting && _visible)
        {
            _visible = false;
            _group.DOKill();
            _group.DOFade(0f, 0.2f).SetDelay(_freeze).SetUpdate(true).SetLink(gameObject);
            return;
        }
        SetVisible(false);
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (!feedback.IsPlayer)
            return;
        Color color = feedback.Zone == ReleaseZone.Green ? _greenGlow : feedback.Zone == ReleaseZone.Yellow ? _yellowGlow : _redGlow;
        color.a = 0.9f;
        _flash.DOKill();
        _flash.color = color;
        _flash.DOFade(0f, _freeze).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        if (visible)
            Layout();
        _group.DOKill();
        _group.DOFade(visible ? 1f : 0f, 0.1f).SetUpdate(true).SetLink(gameObject);
    }
}
