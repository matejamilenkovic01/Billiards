using UnityEngine;

public class MenuController : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.OnPlayRequested += HandlePlayRequested;
        GameEvents.OnSettingsRequested += HandleSettingsRequested;
        GameEvents.OnCuesRequested += HandleCuesRequested;
        GameEvents.OnRankedRoadRequested += HandleRankedRoadRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayRequested -= HandlePlayRequested;
        GameEvents.OnSettingsRequested -= HandleSettingsRequested;
        GameEvents.OnCuesRequested -= HandleCuesRequested;
        GameEvents.OnRankedRoadRequested -= HandleRankedRoadRequested;
    }

    private void Start()
    {
        ProfileService profile = ProfileService.Instance;
        // Views fill themselves from the initial snapshot.
        GameEvents.RaiseProfileChanged(profile.GetSnapshot());
        if (profile.ConsumeAbandonedNotice())
            GameEvents.RaiseToastRequested(new ToastMessage("Last match was abandoned (counted as a loss)", ToastStyle.Bad));
    }

    // No energy, no entry fee (v3 V11): the match is in the player's current division, against a random character
    // of it, in its venue.
    private void HandlePlayRequested()
    {
        MatchSetup.Ranked(ProfileService.Instance.CurrentDivision);
        SceneLoader.Instance.Load(InputRouter.MatchSceneName);
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

    private void HandleRankedRoadRequested() =>
        PopupManager.Instance.Show<RankedRoadPopup>(new RankedRoadPopupData(ProfileService.Instance.RoadView()));
}
