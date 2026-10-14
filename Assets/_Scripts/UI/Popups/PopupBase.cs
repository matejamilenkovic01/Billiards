using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public abstract class PopupBase : MonoBehaviour
{
    private const float DimDuration = 0.2f;
    private const float ShowDuration = 0.3f;
    private const float HideDuration = 0.2f;
    private const float ShowStartScale = 0.85f;
    private const float HideEndScale = 0.9f;

    [SerializeField] private CanvasGroup _dim;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private bool _closeOnBack = true;
    [SerializeField] private bool _closeOnDimClick;

    private CanvasGroup _canvasGroup;
    private CanvasGroup _panelGroup;
    private Sequence _sequence;

    public bool IsShown { get; private set; }
    public bool CloseOnBack => _closeOnBack;

    public event Action<PopupBase> Hidden;

    protected virtual void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _panelGroup = _panel.GetComponent<CanvasGroup>();

        if (_closeOnDimClick)
        {
            var trigger = _dim.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => Hide());
            trigger.triggers.Add(entry);
        }
    }

    public void Show(object payload = null)
    {
        bool wasHidden = !gameObject.activeSelf;
        IsShown = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        OnShow(payload);

        _sequence?.Kill();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = true;

        // Restart from zero only when fully hidden; a Show during Hide continues from the current frame.
        if (wasHidden)
        {
            _dim.alpha = 0f;
            _panelGroup.alpha = 0f;
            _panel.localScale = Vector3.one * ShowStartScale;
        }

        _sequence = DOTween.Sequence()
            .Join(_dim.DOFade(1f, DimDuration))
            .Join(_panel.DOScale(1f, ShowDuration).SetEase(Ease.OutBack))
            .Join(_panelGroup.DOFade(1f, ShowDuration * 0.6f))
            .OnComplete(() => _canvasGroup.interactable = true)
            .SetUpdate(true)
            .SetLink(gameObject);

        GameEvents.RaisePopupShown();
    }

    public void Hide()
    {
        if (!IsShown)
            return;

        IsShown = false;
        _sequence?.Kill();
        _canvasGroup.interactable = false;

        _sequence = DOTween.Sequence()
            .Join(_panel.DOScale(HideEndScale, HideDuration).SetEase(Ease.InBack))
            .Join(_panelGroup.DOFade(0f, HideDuration))
            .Join(_dim.DOFade(0f, HideDuration))
            .OnComplete(CompleteHide)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    public void HideImmediate()
    {
        if (!gameObject.activeSelf)
            return;

        IsShown = false;
        _sequence?.Kill();
        CompleteHide();
    }

    // Back/Esc while this popup is on top. Pause overrides it to resume the game instead.
    public virtual void HandleBack() => Hide();

    protected virtual void OnShow(object payload) { }

    protected virtual void OnHidden() { }

    private void CompleteHide()
    {
        _canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
        OnHidden();
        Hidden?.Invoke(this);
    }
}
