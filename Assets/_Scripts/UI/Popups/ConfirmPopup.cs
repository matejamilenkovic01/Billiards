using TMPro;
using UnityEngine;

// Generic yes/no: Buy, Refill, Forfeit.
public class ConfirmPopup : PopupBase
{
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _body;
    [SerializeField] private GameObject _priceRow;
    [SerializeField] private TMP_Text _price;
    [SerializeField] private UIButton _confirmButton;
    [SerializeField] private TMP_Text _confirmLabel;
    [SerializeField] private Sprite _confirmSprite;
    [SerializeField] private Sprite _confirmPressedSprite;
    [SerializeField] private Sprite _dangerSprite;
    [SerializeField] private Sprite _dangerPressedSprite;
    [SerializeField] private UIButton _cancelButton;

    private System.Action _onConfirm;

    private void OnEnable()
    {
        _confirmButton.Clicked += HandleConfirm;
        _cancelButton.Clicked += Hide;
    }

    private void OnDisable()
    {
        _confirmButton.Clicked -= HandleConfirm;
        _cancelButton.Clicked -= Hide;
    }

    protected override void OnShow(object payload)
    {
        if (payload is not ConfirmPopupData data)
            return;

        _title.text = data.Title;
        _body.text = data.Body;
        _confirmLabel.text = data.ConfirmLabel;
        _priceRow.SetActive(data.Price > 0);
        _price.text = data.Price.ToString();
        _onConfirm = data.OnConfirm;
        _confirmButton.SetSprites(data.IsDanger ? _dangerSprite : _confirmSprite, data.IsDanger ? _dangerPressedSprite : _confirmPressedSprite);
    }

    private void HandleConfirm()
    {
        System.Action action = _onConfirm;
        _onConfirm = null;
        Hide();
        action?.Invoke();
    }
}
