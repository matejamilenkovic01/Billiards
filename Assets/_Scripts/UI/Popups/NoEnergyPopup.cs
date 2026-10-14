using TMPro;
using UnityEngine;

// Play with a drained cue: refill it right here, or pick another one.
public class NoEnergyPopup : PopupBase
{
    [SerializeField] private TMP_Text _body;
    [SerializeField] private UIButton _refillButton;
    [SerializeField] private TMP_Text _refillLabel;
    [SerializeField] private TMP_Text _shortText;
    [SerializeField] private UIButton _changeCueButton;
    [SerializeField] private UIButton _closeButton;

    private CueViewData _cue;

    private void OnEnable()
    {
        _refillButton.Clicked += HandleRefill;
        _changeCueButton.Clicked += HandleChangeCue;
        _closeButton.Clicked += Hide;
        GameEvents.OnProfileChanged += HandleProfileChanged;
    }

    private void OnDisable()
    {
        _refillButton.Clicked -= HandleRefill;
        _changeCueButton.Clicked -= HandleChangeCue;
        _closeButton.Clicked -= Hide;
        GameEvents.OnProfileChanged -= HandleProfileChanged;
    }

    protected override void OnShow(object payload)
    {
        if (payload is not NoEnergyPopupData data)
            return;

        _cue = data.Cue;
        _body.text = $"Your {_cue.Name} is out of energy.";
        _refillLabel.text = $"REFILL · {_cue.RefillPrice}";
        bool canPay = data.Coins >= _cue.RefillPrice;
        _refillButton.Interactable = canPay;
        _shortText.gameObject.SetActive(!canPay);
        _shortText.text = $"Need {_cue.RefillPrice - data.Coins} more coins";
    }

    private void HandleRefill()
    {
        CueViewData cue = _cue;
        PopupManager.Instance.Show<ConfirmPopup>(new ConfirmPopupData(
            "REFILL", $"Refill {cue.Name} to {cue.MaxEnergy} energy?", "REFILL",
            () => GameEvents.RaiseCueRefillRequested(cue.Id), cue.RefillPrice));
    }

    private void HandleChangeCue()
    {
        Hide();
        GameEvents.RaiseCuesRequested();
    }

    // Refilled (or switched cue elsewhere): nothing left to do here.
    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        if (_cue != null && IsShown && snapshot.SelectedCue != null && snapshot.SelectedCue.HasEnergy)
            Hide();
    }
}
