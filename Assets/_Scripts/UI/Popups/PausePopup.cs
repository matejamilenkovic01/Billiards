using UnityEngine;

public class PausePopup : PopupBase
{
    [SerializeField] private UIButton _resumeButton;
    [SerializeField] private UIButton _settingsButton;
    [SerializeField] private UIButton _rulesButton;
    [SerializeField] private UIButton _forfeitButton;
    [SerializeField] private TMPro.TMP_Text _forfeitLabel;

    private void OnEnable()
    {
        _resumeButton.Clicked += GameEvents.RaiseResumeRequested;
        _settingsButton.Clicked += GameEvents.RaiseSettingsRequested;
        _rulesButton.Clicked += HandleRules;
        _forfeitButton.Clicked += HandleForfeit;
    }

    private void OnDisable()
    {
        _resumeButton.Clicked -= GameEvents.RaiseResumeRequested;
        _settingsButton.Clicked -= GameEvents.RaiseSettingsRequested;
        _rulesButton.Clicked -= HandleRules;
        _forfeitButton.Clicked -= HandleForfeit;
    }

    // Back means "resume": the controller unpauses and closes this popup.
    public override void HandleBack() => GameEvents.RaiseResumeRequested();

    protected override void OnShow(object payload)
    {
        _forfeitLabel.text = "FORFEIT";
    }

    private void HandleRules() => GameEvents.RaiseRulesRequested(RuleId.None);

    private void HandleForfeit() =>
        PopupManager.Instance.Show<ConfirmPopup>(
            new ConfirmPopupData("FORFEIT", "Forfeit the match?\nThis counts as a loss (-1 coin).", "FORFEIT", GameEvents.RaiseForfeitRequested, isDanger: true));
}
