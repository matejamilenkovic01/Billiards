using UnityEngine;
using UnityEngine.UI;

public class SettingsPopup : PopupBase
{
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private UIButton _closeButton;

    private void OnEnable()
    {
        _sfxSlider.onValueChanged.AddListener(GameEvents.RaiseSfxVolumeChanged);
        _musicSlider.onValueChanged.AddListener(GameEvents.RaiseMusicVolumeChanged);
        _closeButton.Clicked += Hide;
    }

    private void OnDisable()
    {
        _sfxSlider.onValueChanged.RemoveListener(GameEvents.RaiseSfxVolumeChanged);
        _musicSlider.onValueChanged.RemoveListener(GameEvents.RaiseMusicVolumeChanged);
        _closeButton.Clicked -= Hide;
    }

    protected override void OnShow(object payload)
    {
        if (payload is not SettingsPopupData data)
            return;

        // Initial values must not echo back as "changed" events.
        _sfxSlider.SetValueWithoutNotify(data.SfxVolume);
        _musicSlider.SetValueWithoutNotify(data.MusicVolume);
    }
}
