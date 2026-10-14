using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Right-hand strip showing the cue pointing up. Put a finger anywhere on it and drag down to pull the cue back;
// lift to shoot. Dragging back above the start (pull under the threshold) and letting go cancels.
public class PowerStripView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform _cue;
    [SerializeField] private Image _cueImage;
    [SerializeField] private float _cueTravel = 300f;
    [SerializeField] private TMP_Text _percent;
    [SerializeField] private GameObject _hint;
    [SerializeField] private Image _fill;

    private Canvas _canvas;
    private Vector2 _cueRest;
    private float _pullRange = 800f;
    private float _startY;
    private bool _dragging;
    private float _pull;

    private float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _cueRest = _cue.anchoredPosition;
        Show(0f);
    }

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnReleaseMeterChanged += HandleReleaseMeterChanged;
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnReleaseMeterChanged -= HandleReleaseMeterChanged;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        _dragging = false;
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _pullRange = info.PullRangePixels;
        if (info.CueSprite != null)
            _cueImage.sprite = info.CueSprite;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _dragging = true;
        _startY = eventData.position.y;
        _cue.DOKill();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging)
            return;
        float dragDown = (_startY - eventData.position.y) / Scale;
        _pull = Mathf.Clamp01(dragDown / _pullRange);
        Show(_pull);
        GameEvents.RaisePullChanged(_pull);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_dragging)
            return;
        _dragging = false;
        GameEvents.RaisePullReleased();
        _pull = 0f;
        // Snap the strip's cue forward, like the stroke.
        DOTween.To(() => _cue.anchoredPosition.y, y => _cue.anchoredPosition = new Vector2(_cueRest.x, y), _cueRest.y, 0.12f)
            .SetEase(Ease.InQuad).SetUpdate(true).SetLink(gameObject).SetTarget(_cue);
        _percent.text = "";
        _fill.fillAmount = 0f;
        _hint.SetActive(true);
    }

    private void HandleReleaseMeterChanged(float position, float power01) => Show(power01);

    private void HandleTurnPhaseChanged(TurnPhase phase)
    {
        if (phase != TurnPhase.Pulling && !_dragging)
            Show(0f);
    }

    private void Show(float pull)
    {
        _cue.anchoredPosition = _cueRest + Vector2.down * pull * _cueTravel;
        _percent.text = pull > 0.001f ? $"{Mathf.RoundToInt(pull * 100f)}%" : "";
        _fill.fillAmount = pull;
        _hint.SetActive(pull <= 0.001f);
    }
}
