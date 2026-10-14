using UnityEngine;
using UnityEngine.EventSystems;

// Left-hand "wheel": a strip of ticks that scroll with the finger. Vertical drag = very fine aim correction.
public class FineAimWheelView : MonoBehaviour, IDragHandler
{
    [SerializeField] private RectTransform[] _ticks;
    [SerializeField] private float _spacing = 50f;

    private Canvas _canvas;
    private float _offset;

    private float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        LayoutTicks();
    }

    public void OnDrag(PointerEventData eventData)
    {
        float delta = eventData.delta.y / Scale;
        _offset = Mathf.Repeat(_offset + delta, _spacing);
        LayoutTicks();
        GameEvents.RaiseFineAimDrag(delta);
    }

    private void LayoutTicks()
    {
        float span = _ticks.Length * _spacing;
        for (int i = 0; i < _ticks.Length; i++)
        {
            float y = i * _spacing + _offset - span * 0.5f;
            _ticks[i].anchoredPosition = new Vector2(0f, y);
            // Ticks fade towards the ends, like a wheel turning away.
            float edge = 1f - Mathf.Abs(y) / (span * 0.5f);
            _ticks[i].localScale = new Vector3(Mathf.Lerp(0.4f, 1f, edge), 1f, 1f);
        }
    }
}
