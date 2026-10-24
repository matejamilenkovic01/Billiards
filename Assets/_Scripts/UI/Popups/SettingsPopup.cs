using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sound and music, haptics on/off, HOW TO PLAY (the tutorial again, from the menu), RULES, the version (v3 V13).
public class SettingsPopup : PopupBase
{
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private UIButton _hapticsButton;
    [SerializeField] private TMP_Text _hapticsLabel;
    [SerializeField] private UIButton _howToPlayButton;
    [SerializeField] private UIButton _rulesButton;
    [SerializeField] private TMP_Text _version;
    [SerializeField] private UIButton _closeButton;

    private bool _hapticsOn;

    private void OnEnable()
    {
        _sfxSlider.onValueChanged.AddListener(GameEvents.RaiseSfxVolumeChanged);
        _musicSlider.onValueChanged.AddListener(GameEvents.RaiseMusicVolumeChanged);
        _hapticsButton.Clicked += HandleHaptics;
        _howToPlayButton.Clicked += GameEvents.RaiseHowToPlayRequested;
        _rulesButton.Clicked += HandleRules;
        _closeButton.Clicked += Hide;
    }

    private void OnDisable()
    {
        _sfxSlider.onValueChanged.RemoveListener(GameEvents.RaiseSfxVolumeChanged);
        _musicSlider.onValueChanged.RemoveListener(GameEvents.RaiseMusicVolumeChanged);
        _hapticsButton.Clicked -= HandleHaptics;
        _howToPlayButton.Clicked -= GameEvents.RaiseHowToPlayRequested;
        _rulesButton.Clicked -= HandleRules;
        _closeButton.Clicked -= Hide;
    }

    protected override void OnShow(object payload)
    {
        if (payload is not SettingsPopupData data)
            return;

        // Initial values must not echo back as "changed" events.
        _sfxSlider.SetValueWithoutNotify(data.SfxVolume);
        _musicSlider.SetValueWithoutNotify(data.MusicVolume);
        _hapticsOn = data.HapticsOn;
        ShowHaptics();
        _howToPlayButton.gameObject.SetActive(data.ShowHowToPlay);
        _version.text = $"v{Application.version}";
    }

    private void HandleHaptics()
    {
        _hapticsOn = !_hapticsOn;
        ShowHaptics();
        GameEvents.RaiseHapticsToggled(_hapticsOn);
    }

    private void ShowHaptics() => _hapticsLabel.text = _hapticsOn ? "HAPTICS: ON" : "HAPTICS: OFF";

    private void HandleRules() => GameEvents.RaiseRulesRequested(RuleId.None);
}
