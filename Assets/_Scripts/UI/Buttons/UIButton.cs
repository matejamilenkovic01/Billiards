using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The one button used everywhere. Variants only change label, icon and size; behaviour lives here.
// Disabled: grey sprite, no press animation, no click; a tap only raises OnDisabledButtonClicked (soft error sound).
[RequireComponent(typeof(Image))]
public class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
{
    [SerializeField] private Sprite _normalSprite;
    [SerializeField] private Sprite _pressedSprite;
    [SerializeField] private Sprite _disabledSprite;
    [SerializeField] private float _pressedScale = 0.92f;
    [SerializeField] private bool _interactable = true;
    [Tooltip("Label and icon container, nudged down while pressed to match the Kenney depth sprites.")]
    [SerializeField] private RectTransform _content;
    [SerializeField] private float _pressedContentOffset = 8f;
    [SerializeField] private CanvasGroup _contentGroup;

    private Image _image;
    private Vector2 _contentRestPosition;
    private bool _isPressed;

    public event Action Clicked;

    public bool Interactable
    {
        get => _interactable;
        set
        {
            _interactable = value;
            if (!value)
                Release();
            ApplyVisualState();
        }
    }

    private void Awake()
    {
        _image = GetComponent<Image>();
        if (_content != null)
            _contentRestPosition = _content.anchoredPosition;
        ApplyVisualState();
    }

    private void OnDisable()
    {
        // A button hidden mid-press (e.g. its popup closed) must come back in its rest state.
        transform.DOKill();
        transform.localScale = Vector3.one;
        _isPressed = false;
        if (_content != null)
            _content.anchoredPosition = _contentRestPosition;
        ApplyVisualState();
    }

    public void SetSprites(Sprite normal, Sprite pressed)
    {
        _normalSprite = normal;
        _pressedSprite = pressed;
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable)
            return;

        _isPressed = true;
        _image.sprite = _pressedSprite;
        if (_content != null)
            _content.anchoredPosition = _contentRestPosition + Vector2.down * _pressedContentOffset;

        transform.DOKill();
        transform.DOScale(_pressedScale, 0.08f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    public void OnPointerExit(PointerEventData eventData) => Release();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_interactable)
        {
            GameEvents.RaiseDisabledButtonClicked();
            return;
        }

        GameEvents.RaiseButtonClicked();
        Clicked?.Invoke();
    }

    private void Release()
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        _image.sprite = _normalSprite;
        if (_content != null)
            _content.anchoredPosition = _contentRestPosition;

        transform.DOKill();
        transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
    }

    private void ApplyVisualState()
    {
        if (_image == null)
            return;
        _image.sprite = _interactable || _disabledSprite == null ? (_isPressed ? _pressedSprite : _normalSprite) : _disabledSprite;
        if (_contentGroup != null)
            _contentGroup.alpha = _interactable ? 1f : 0.55f;
    }
}
