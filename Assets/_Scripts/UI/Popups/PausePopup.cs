using UnityEngine;

public class PausePopup : PopupBase
{
    [SerializeField] private UIButton _resumeButton;
    [SerializeField] private UIButton _settingsButton;
    [SerializeField] private UIButton _forfeitButton;
    [SerializeField] private TMPro.TMP_Text _forfeitLabel;

    private bool _twoPlayers;

    private void OnEnable()
    {
        _resumeButton.Clicked += GameEvents.RaiseResumeRequested;
        _settingsButton.Clicked += GameEvents.RaiseSettingsRequested;
        _forfeitButton.Clicked += HandleForfeit;
    }

    private void OnDisable()
    {
        _resumeButton.Clicked -= GameEvents.RaiseResumeRequested;
        _settingsButton.Clicked -= GameEvents.RaiseSettingsRequested;
        _forfeitButton.Clicked -= HandleForfeit;
    }

    // Back means "resume": the controller unpauses and closes this popup.
    public override void HandleBack() => GameEvents.RaiseResumeRequested();

    protected override void OnShow(object payload)
    {
        _twoPlayers = payload is PausePopupData { IsTwoPlayers: true };
        _forfeitLabel.text = _twoPlayers ? "QUIT MATCH" : "FORFEIT";
    }

    private void HandleForfeit() =>
        PopupManager.Instance.Show<ConfirmPopup>(_twoPlayers
            ? new ConfirmPopupData("QUIT", "Quit this match?\nNothing is won or lost.", "QUIT", GameEvents.RaiseForfeitRequested, isDanger: true)
            : new ConfirmPopupData("FORFEIT", "Forfeit the match?\nThis counts as a loss (-1 coin).", "FORFEIT", GameEvents.RaiseForfeitRequested, isDanger: true));
}
