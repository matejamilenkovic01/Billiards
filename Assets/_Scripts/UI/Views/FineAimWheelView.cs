using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Bottom "wheel" for fine aim (gameplay redesign G2): a horizontal roller of ticks that slide with the finger,
// shaded at the ends. Drag left = the cue turns left, drag right = right: finger and aim move the same way.
// It lights up while held.
public class FineAimWheelView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform[] _ticks;
    [SerializeField] private float _spacing = 40f;
    [SerializeField] private Image _background;
    [SerializeField] private Color _idleColor = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color _heldColor = new(0.2f, 0.3f, 0.24f, 0.75f);

    private Canvas _canvas;
    private float _offset;

    private float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _background.color = _idleColor;
        LayoutTicks();
    }

    public void OnPointerDown(PointerEventData eventData) => SetHeld(true);

    public void OnPointerUp(PointerEventData eventData) => SetHeld(false);

    public void OnDrag(PointerEventData eventData)
    {
        float delta = eventData.delta.x / Scale;
        _offset = Mathf.Repeat(_offset + delta, _spacing);
        LayoutTicks();
        GameEvents.RaiseFineAimDrag(delta);
    }

    private void SetHeld(bool held)
    {
        _background.DOKill();
        _background.DOColor(held ? _heldColor : _idleColor, 0.1f).SetUpdate(true).SetLink(gameObject);
    }

    private void LayoutTicks()
    {
        float span = _ticks.Length * _spacing;
        for (int i = 0; i < _ticks.Length; i++)
        {
            float x = i * _spacing + _offset - span * 0.5f;
            _ticks[i].anchoredPosition = new Vector2(x, 0f);
            // Ticks squeeze and fade towards the ends, like a roller turning away.
            float edge = Mathf.Clamp01(1f - Mathf.Abs(x) / (span * 0.5f));
            float curve = Mathf.Sin(edge * Mathf.PI * 0.5f);
            _ticks[i].localScale = new Vector3(Mathf.Lerp(0.35f, 1f, curve), Mathf.Lerp(0.6f, 1f, curve), 1f);
        }
    }
}
