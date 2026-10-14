using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One cue in the inventory: Locked (wins progress), Unlocked (buy), Owned (select) or Selected,
// plus energy and refill for owned cues. Purchases go through the confirm popup.
public class CueCardView : MonoBehaviour
{
    private const float StatAnimDuration = 0.35f;

    [SerializeField] private Image _frame;
    [SerializeField] private Image _cueImage;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private GameObject _selectedBadge;
    [SerializeField] private Image _aimBar;
    [SerializeField] private Image _releaseBar;
    [SerializeField] private GameObject _energyRow;
    [SerializeField] private Image _energyBar;
    [SerializeField] private TMP_Text _energyText;
    [SerializeField] private UIButton _refillButton;
    [SerializeField] private TMP_Text _refillLabel;
    [SerializeField] private UIButton _actionButton;
    [SerializeField] private TMP_Text _actionLabel;
    [SerializeField] private GameObject _actionCoin;
    [SerializeField] private TMP_Text _note;
    [SerializeField] private GameObject _lockedOverlay;
    [SerializeField] private TMP_Text _lockedText;
    [SerializeField] private Image _unlockProgress;
    [SerializeField] private CanvasGroup _content;
    [SerializeField] private Color _frameNormal = new(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color _frameSelected = new(0.25f, 0.85f, 0.4f, 1f);
    [SerializeField] private Color _shortColor = new(1f, 0.4f, 0.35f, 1f);

    private CueViewData _data;
    private bool _hasData;

    private void OnEnable()
    {
        _actionButton.Clicked += HandleAction;
        _refillButton.Clicked += HandleRefill;
    }

    private void OnDisable()
    {
        _actionButton.Clicked -= HandleAction;
        _refillButton.Clicked -= HandleRefill;
    }

    public void Bind(CueViewData data, bool justPurchased)
    {
        bool animate = _hasData && _data.Id == data.Id;
        _data = data;
        _hasData = true;

        _name.text = data.Name;
        if (data.Sprite != null)
            _cueImage.sprite = data.Sprite;
        SetFill(_aimBar, data.AimLine01, animate);
        SetFill(_releaseBar, data.ReleaseWindow01, animate);

        bool locked = data.State == CueState.Locked;
        bool owned = data.State == CueState.Owned;
        _lockedOverlay.SetActive(locked);
        _content.alpha = locked ? 0.45f : 1f;
        _frame.color = data.IsSelected ? _frameSelected : _frameNormal;
        _selectedBadge.SetActive(data.IsSelected);

        // Energy (owned cues only).
        _energyRow.SetActive(owned);
        if (owned)
        {
            if (data.InfiniteEnergy)
            {
                _energyText.text = "∞";
                SetFill(_energyBar, 1f, animate);
                _refillButton.gameObject.SetActive(false);
            }
            else
            {
                _energyText.text = $"{data.Energy}/{data.MaxEnergy}";
                SetFill(_energyBar, data.MaxEnergy > 0 ? data.Energy / (float)data.MaxEnergy : 0f, animate);
                _refillButton.gameObject.SetActive(true);
                _refillLabel.text = $"REFILL · {data.RefillPrice}";
                _refillButton.Interactable = data.CanRefill;
            }
        }

        // Main action per state.
        _note.gameObject.SetActive(false);
        _actionCoin.SetActive(false);
        _actionButton.gameObject.SetActive(!locked && !data.IsSelected);
        if (locked)
        {
            int have = Mathf.Min(data.Wins, data.WinsToUnlock);
            _lockedText.text = $"Win {data.WinsToUnlock} {(data.WinsToUnlock == 1 ? "match" : "matches")} to unlock ({have}/{data.WinsToUnlock})";
            _unlockProgress.fillAmount = data.WinsToUnlock > 0 ? have / (float)data.WinsToUnlock : 1f;
        }
        else if (data.State == CueState.Unlocked)
        {
            _actionLabel.text = $"BUY · {data.Price}";
            _actionCoin.SetActive(true);
            _actionButton.Interactable = data.CanAfford;
            _actionLabel.color = data.CanAfford ? Color.white : _shortColor;
            if (!data.CanAfford)
            {
                _note.gameObject.SetActive(true);
                _note.text = $"Need {data.CoinsShort} more coins";
            }
        }
        else if (!data.IsSelected)
        {
            _actionLabel.text = "SELECT";
            _actionLabel.color = Color.white;
            _actionButton.Interactable = true;
        }

        if (justPurchased)
        {
            Transform button = _actionButton.transform;
            button.DOKill();
            button.localScale = Vector3.one;
            button.DOPunchScale(Vector3.one * 0.18f, 0.5f, 6, 0.6f).SetLoops(2).SetUpdate(true).SetLink(button.gameObject);
        }
    }

    private void SetFill(Image bar, float value, bool animate)
    {
        bar.DOKill();
        if (animate)
            bar.DOFillAmount(value, StatAnimDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(bar.gameObject);
        else
            bar.fillAmount = value;
    }

    private void HandleAction()
    {
        if (!_hasData)
            return;

        CueViewData cue = _data;
        if (cue.State == CueState.Owned)
        {
            GameEvents.RaiseCueSelectRequested(cue.Id);
            return;
        }

        PopupManager.Instance.Show<ConfirmPopup>(new ConfirmPopupData(
            "BUY CUE", $"Buy the {cue.Name}?", "BUY", () => GameEvents.RaiseCueBuyRequested(cue.Id), cue.Price));
    }

    private void HandleRefill()
    {
        if (!_hasData)
            return;

        CueViewData cue = _data;
        PopupManager.Instance.Show<ConfirmPopup>(new ConfirmPopupData(
            "REFILL", $"Refill {cue.Name} to {cue.MaxEnergy} energy?", "REFILL",
            () => GameEvents.RaiseCueRefillRequested(cue.Id), cue.RefillPrice));
    }
}
