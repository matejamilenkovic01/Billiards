using TMPro;
using UnityEngine;

public class MainMenuView : MonoBehaviour
{
    [SerializeField] private UIButton _playButton;
    [SerializeField] private UIButton _twoPlayersButton;
    [SerializeField] private UIButton _settingsButton;
    [SerializeField] private UIButton _cuesButton;
    [SerializeField] private TMP_Text _versionText;

    private void Awake() => _versionText.text = $"v{Application.version}";

    private void OnEnable()
    {
        _playButton.Clicked += GameEvents.RaisePlayRequested;
        _twoPlayersButton.Clicked += GameEvents.RaiseTwoPlayersRequested;
        _settingsButton.Clicked += GameEvents.RaiseSettingsRequested;
        _cuesButton.Clicked += GameEvents.RaiseCuesRequested;
    }

    private void OnDisable()
    {
        _playButton.Clicked -= GameEvents.RaisePlayRequested;
        _twoPlayersButton.Clicked -= GameEvents.RaiseTwoPlayersRequested;
        _settingsButton.Clicked -= GameEvents.RaiseSettingsRequested;
        _cuesButton.Clicked -= GameEvents.RaiseCuesRequested;
    }
}
