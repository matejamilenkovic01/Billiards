using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Fake door (v3 V12): "Pro Cue + 1,000 coins + 3 Star Shields, $1.99". BUY charges nothing and grants nothing: the
// card turns into "The shop opens soon!" with a Notify me switch. Every step is an OnFakeDoor event.
public class StarterPackPopup : PopupBase
{
    [SerializeField] private GameObject _offer;
    [SerializeField] private GameObject _thanks;
    [SerializeField] private Image _cueImage;
    [SerializeField] private TMP_Text _overall;
    [SerializeField] private TMP_Text _vsHouse;
    [SerializeField] private TMP_Text _recommended;
    [SerializeField] private TMP_Text _priceLabel;
    [SerializeField] private UIButton _buyButton;
    [SerializeField] private UIButton _closeButton;
    [SerializeField] private UIButton _notifyButton;
    [SerializeField] private TMP_Text _notifyLabel;
    [SerializeField] private UIButton _okButton;

    private bool _notify;

    private void OnEnable()
    {
        _buyButton.Clicked += HandleBuy;
        _closeButton.Clicked += HandleClose;
        _okButton.Clicked += HandleClose;
        _notifyButton.Clicked += HandleNotify;
    }

    private void OnDisable()
    {
        _buyButton.Clicked -= HandleBuy;
        _closeButton.Clicked -= HandleClose;
        _okButton.Clicked -= HandleClose;
        _notifyButton.Clicked -= HandleNotify;
    }

    protected override void OnShow(object payload)
    {
        if (payload is not FakeDoorPopupData data)
            return;
        _notify = data.NotifyOn;
        _offer.SetActive(true);
        _thanks.SetActive(false);
        if (data.Cue != null)
        {
            if (data.Cue.Sprite != null)
                _cueImage.sprite = data.Cue.Sprite;
            _overall.text = $"OVR {data.Cue.Overall}";
            _recommended.text = string.IsNullOrEmpty(data.Cue.RecommendedFor) ? "" : $"Recommended for {data.Cue.RecommendedFor}";
        }
        _vsHouse.text = $"+{data.OverallVsHouse} OVR vs House";
        _priceLabel.text = $"BUY · {data.Price}";
        ShowNotify();
        GameEvents.RaiseFakeDoor(FakeDoorId.StarterPack, FakeDoorAction.Shown);
    }

    private void HandleBuy()
    {
        GameEvents.RaiseFakeDoor(FakeDoorId.StarterPack, FakeDoorAction.Tapped);
        _offer.SetActive(false);
        _thanks.SetActive(true);
        Transform card = _thanks.transform;
        card.DOKill();
        card.localScale = Vector3.one * 0.9f;
        card.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(card.gameObject);
    }

    private void HandleNotify()
    {
        _notify = !_notify;
        ShowNotify();
        GameEvents.RaiseFakeDoor(FakeDoorId.StarterPack, _notify ? FakeDoorAction.NotifyOn : FakeDoorAction.NotifyOff);
    }

    private void ShowNotify() => _notifyLabel.text = _notify ? "NOTIFY ME: ON" : "NOTIFY ME: OFF";

    private void HandleClose()
    {
        GameEvents.RaiseFakeDoor(FakeDoorId.StarterPack, FakeDoorAction.Closed);
        Hide();
    }
}
