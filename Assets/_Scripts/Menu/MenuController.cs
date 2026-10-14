using UnityEngine;

public class MenuController : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.OnPlayRequested += HandlePlayRequested;
        GameEvents.OnSettingsRequested += HandleSettingsRequested;
        GameEvents.OnCuesRequested += HandleCuesRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayRequested -= HandlePlayRequested;
        GameEvents.OnSettingsRequested -= HandleSettingsRequested;
        GameEvents.OnCuesRequested -= HandleCuesRequested;
    }

    private void Start()
    {
        ProfileService profile = ProfileService.Instance;
        // Views fill themselves from the initial snapshot.
        GameEvents.RaiseProfileChanged(profile.GetSnapshot());
        if (profile.ConsumeAbandonedNotice())
            GameEvents.RaiseToastRequested(new ToastMessage("Last match was abandoned (-1 coin)", ToastStyle.Bad));
    }

    private void HandlePlayRequested()
    {
        ProfileService profile = ProfileService.Instance;
        if (profile.CanStartMatch(out _))
        {
            SceneLoader.Instance.Load(InputRouter.MatchSceneName);
            return;
        }

        ProfileSnapshot snapshot = profile.GetSnapshot();
        PopupManager.Instance.Show<NoEnergyPopup>(new NoEnergyPopupData(snapshot.SelectedCue, snapshot.Coins));
    }

    private void HandleSettingsRequested()
    {
        AudioManager audio = AudioManager.Instance;
        SettingsPopup popup = PopupManager.Instance.Show<SettingsPopup>(new SettingsPopupData(audio.SfxVolume, audio.MusicVolume));
        if (popup == null)
            return;

        popup.Hidden -= HandleSettingsHidden;
        popup.Hidden += HandleSettingsHidden;
    }

    private void HandleSettingsHidden(PopupBase popup)
    {
        popup.Hidden -= HandleSettingsHidden;
        AudioManager.Instance.SaveVolumes();
    }

    private void HandleCuesRequested() =>
        PopupManager.Instance.Show<CuesPopup>(new CuesPopupData(ProfileService.Instance.GetSnapshot()));
}
