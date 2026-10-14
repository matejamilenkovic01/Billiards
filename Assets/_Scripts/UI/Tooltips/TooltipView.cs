using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class TooltipView : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private RectTransform _arrow;
    [SerializeField] private float _maxTextWidth = 600f;
    [SerializeField] private Vector2 _padding = new(44f, 32f);
    [Tooltip("Keeps the arrow away from the rounded corners.")]
    [SerializeField] private float _arrowEdgeMargin = 40f;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Tween _tween;

    public RectTransform RectTransform => _rectTransform;
    public float ArrowHeight => _arrow.rect.height;

    private void Awake()
    {
        _rectTransform = (RectTransform)transform;
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    public Vector2 SetText(string text)
    {
        _text.text = text;
        Vector2 preferred = _text.GetPreferredValues(text, _maxTextWidth, 0f);
        float textWidth = Mathf.Min(preferred.x, _maxTextWidth);
        Vector2 textSize = _text.GetPreferredValues(text, textWidth, 0f);

        _text.rectTransform.sizeDelta = new Vector2(textWidth, textSize.y);
        Vector2 size = new(textWidth + _padding.x * 2f, textSize.y + _padding.y * 2f);
        _rectTransform.sizeDelta = size;
        return size;
    }

    // side: Below means the tooltip hangs under its anchor, so the arrow sits on its top edge pointing up.
    public void Place(Vector2 position, TooltipSide side, float arrowLocalX)
    {
        _rectTransform.pivot = new Vector2(0.5f, side == TooltipSide.Below ? 1f : 0f);
        _rectTransform.anchoredPosition = position;

        float halfWidth = _rectTransform.sizeDelta.x * 0.5f - _arrowEdgeMargin;
        float arrowX = Mathf.Clamp(arrowLocalX, -halfWidth, halfWidth);
        bool below = side == TooltipSide.Below;
        _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, below ? 1f : 0f);
        _arrow.pivot = new Vector2(0.5f, 1f);
        _arrow.localRotation = Quaternion.Euler(0f, 0f, below ? 180f : 0f);
        _arrow.anchoredPosition = new Vector2(arrowX, 0f);
    }

    public void AnimateIn()
    {
        _tween?.Kill();
        gameObject.SetActive(true);
        _canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * 0.8f;
        _tween = DOTween.Sequence()
            .Join(transform.DOScale(1f, 0.15f).SetEase(Ease.OutBack))
            .Join(_canvasGroup.DOFade(1f, 0.15f))
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    public void AnimateOut()
    {
        if (!gameObject.activeSelf)
            return;

        _tween?.Kill();
        _tween = DOTween.Sequence()
            .Join(transform.DOScale(0.9f, 0.1f))
            .Join(_canvasGroup.DOFade(0f, 0.1f))
            .OnComplete(() => gameObject.SetActive(false))
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    public void HideImmediate()
    {
        _tween?.Kill();
        gameObject.SetActive(false);
    }
}
