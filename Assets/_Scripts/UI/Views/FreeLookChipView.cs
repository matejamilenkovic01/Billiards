using DG.Tweening;
using UnityEngine;

// While you look around on the opponent's turn (v3 V2): a small chip under your panel, the drag and pinch icons
// and TABLE VIEW, which gives the camera back (as does a double tap).
public class FreeLookChipView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private UIButton _tableViewButton;

    private void Awake() => SetVisible(false, true);

    private void OnEnable()
    {
        GameEvents.OnFreeLookChanged += HandleFreeLookChanged;
        _tableViewButton.Clicked += GameEvents.RaiseTableViewRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnFreeLookChanged -= HandleFreeLookChanged;
        _tableViewButton.Clicked -= GameEvents.RaiseTableViewRequested;
    }

    private void HandleFreeLookChanged(bool on) => SetVisible(on, false);

    private void SetVisible(bool visible, bool instant)
    {
        _group.DOKill();
        _group.blocksRaycasts = visible;
        _group.interactable = visible;
        if (instant)
            _group.alpha = visible ? 1f : 0f;
        else
            _group.DOFade(visible ? 1f : 0f, 0.2f).SetUpdate(true).SetLink(gameObject);
    }
}
