using TMPro;
using UnityEngine;

public class MainMenuView : MonoBehaviour
{
    [SerializeField] private UIButton _playButton;
    [SerializeField] private UIButton _settingsButton;
    [SerializeField] private UIButton _cuesButton;
    [Tooltip("ONLINE RANKED · soon: a fake door (v3 V12).")]
    [SerializeField] private UIButton _onlineButton;
    [SerializeField] private TMP_Text _versionText;
    [Tooltip("Under PLAY: the venue of the current division.")]
    [SerializeField] private TMP_Text _playCaption;

    private void Awake() => _versionText.text = $"v{Application.version}";

    private void OnEnable()
    {
        _playButton.Clicked += GameEvents.RaisePlayRequested;
        _settingsButton.Clicked += GameEvents.RaiseSettingsRequested;
        _cuesButton.Clicked += GameEvents.RaiseCuesRequested;
        _onlineButton.Clicked += HandleOnline;
        GameEvents.OnProfileChanged += HandleProfileChanged;
    }

    private void OnDisable()
    {
        _playButton.Clicked -= GameEvents.RaisePlayRequested;
        _settingsButton.Clicked -= GameEvents.RaiseSettingsRequested;
        _cuesButton.Clicked -= GameEvents.RaiseCuesRequested;
        _onlineButton.Clicked -= HandleOnline;
        GameEvents.OnProfileChanged -= HandleProfileChanged;
    }

    private void HandleOnline()
    {
        GameEvents.RaiseFakeDoor(FakeDoorId.OnlineRanked, FakeDoorAction.Tapped);
        GameEvents.RaiseOnlineRankedRequested();
    }

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        if (snapshot.Rank != null)
            _playCaption.text = $"RANKED  ·  {snapshot.Rank.VenueName.ToUpperInvariant()}  ·  VS CPU";
    }
}
