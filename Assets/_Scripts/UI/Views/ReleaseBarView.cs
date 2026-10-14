using DG.Tweening;
using UnityEngine;

// Vertical release bar: green in the middle, yellow around it, red at the ends. Zone sizes come from the
// selected cue, so a better cue visibly widens green and yellow. The indicator follows the oscillation.
public class ReleaseBarView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _bar;
    [SerializeField] private RectTransform _yellow;
    [SerializeField] private RectTransform _green;
    [SerializeField] private RectTransform _indicator;

    private float _greenHalf = 0.1f;
    private float _yellowWidth = 0.3f;
    private bool _visible;
    private bool _inGreen;

    private float HalfHeight => _bar.rect.height * 0.5f;

    private void Awake()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        GameEvents.OnReleaseMeterSetup += HandleSetup;
        GameEvents.OnReleaseMeterChanged += HandleChanged;
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnReleaseMeterSetup -= HandleSetup;
        GameEvents.OnReleaseMeterChanged -= HandleChanged;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
    }

    private void HandleSetup(ReleaseMeterSettings settings)
    {
        _greenHalf = settings.GreenHalf;
        _yellowWidth = settings.YellowWidth;
        Layout();
    }

    private void Layout()
    {
        float half = HalfHeight;
        _green.sizeDelta = new Vector2(_green.sizeDelta.x, _greenHalf * 2f * half);
        _yellow.sizeDelta = new Vector2(_yellow.sizeDelta.x, Mathf.Min(1f, _greenHalf + _yellowWidth) * 2f * half);
    }

    private void HandleChanged(float position, float power01)
    {
        if (!_visible)
            SetVisible(true);
        _indicator.anchoredPosition = new Vector2(_indicator.anchoredPosition.x, position * HalfHeight);

        // The green zone breathes while the indicator is inside it.
        bool inGreen = Mathf.Abs(position) <= _greenHalf;
        if (inGreen != _inGreen)
        {
            _inGreen = inGreen;
            _green.DOKill();
            _green.localScale = Vector3.one;
            if (inGreen)
                _green.DOScale(new Vector3(1.18f, 1f, 1f), 0.12f).SetLoops(2, LoopType.Yoyo).SetUpdate(true).SetLink(_green.gameObject);
        }
    }

    private void HandleTurnPhaseChanged(TurnPhase phase)
    {
        if (phase != TurnPhase.Pulling)
            SetVisible(false);
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
