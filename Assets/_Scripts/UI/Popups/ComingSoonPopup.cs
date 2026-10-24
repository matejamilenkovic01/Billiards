using TMPro;
using UnityEngine;

// Fake door (v3 V12): ONLINE RANKED is coming soon; Notify me is a switch kept in the profile.
public class ComingSoonPopup : PopupBase
{
    [SerializeField] private UIButton _notifyButton;
    [SerializeField] private TMP_Text _notifyLabel;
    [SerializeField] private UIButton _okButton;

    private bool _notify;

    private void OnEnable()
    {
        _notifyButton.Clicked += HandleNotify;
        _okButton.Clicked += HandleClose;
    }

    private void OnDisable()
    {
        _notifyButton.Clicked -= HandleNotify;
        _okButton.Clicked -= HandleClose;
    }

    protected override void OnShow(object payload)
    {
        if (payload is not FakeDoorPopupData data)
            return;
        _notify = data.NotifyOn;
        ShowNotify();
        GameEvents.RaiseFakeDoor(FakeDoorId.OnlineRanked, FakeDoorAction.Shown);
    }

    private void HandleNotify()
    {
        _notify = !_notify;
        ShowNotify();
        GameEvents.RaiseFakeDoor(FakeDoorId.OnlineRanked, _notify ? FakeDoorAction.NotifyOn : FakeDoorAction.NotifyOff);
    }

    private void ShowNotify() => _notifyLabel.text = _notify ? "NOTIFY ME: ON" : "NOTIFY ME: OFF";

    private void HandleClose()
    {
        GameEvents.RaiseFakeDoor(FakeDoorId.OnlineRanked, FakeDoorAction.Closed);
        Hide();
    }
}
