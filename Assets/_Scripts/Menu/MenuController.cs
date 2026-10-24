using DG.Tweening;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.OnPlayRequested += HandlePlayRequested;
        GameEvents.OnSettingsRequested += HandleSettingsRequested;
        GameEvents.OnCuesRequested += HandleCuesRequested;
        GameEvents.OnRankedRoadRequested += HandleRankedRoadRequested;
        GameEvents.OnHowToPlayRequested += HandleHowToPlayRequested;
        GameEvents.OnRulesRequested += HandleRulesRequested;
        GameEvents.OnOnlineRankedRequested += HandleOnlineRankedRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayRequested -= HandlePlayRequested;
        GameEvents.OnSettingsRequested -= HandleSettingsRequested;
        GameEvents.OnCuesRequested -= HandleCuesRequested;
        GameEvents.OnRankedRoadRequested -= HandleRankedRoadRequested;
        GameEvents.OnHowToPlayRequested -= HandleHowToPlayRequested;
        GameEvents.OnRulesRequested -= HandleRulesRequested;
        GameEvents.OnOnlineRankedRequested -= HandleOnlineRankedRequested;
    }

    private void Start()
    {
        ProfileService profile = ProfileService.Instance;
        // FTUE (v3 V13): a fresh install goes straight into the tutorial.
        if (!profile.TutorialDone)
        {
            StartTutorial();
            return;
        }
        // The table behind the menu stands in the current division's venue (v3 V13).
        VenueView venue = FindFirstObjectByType<VenueView>();
        if (venue != null)
            venue.Apply(profile.CurrentDivision.VenueId);
        // Views fill themselves from the initial snapshot.
        GameEvents.RaiseProfileChanged(profile.GetSnapshot());
        if (profile.ConsumeAbandonedNotice())
            GameEvents.RaiseToastRequested(new ToastMessage("Last match was abandoned (counted as a loss)", ToastStyle.Bad));

        // Back from the first match: what the badge and the objectives are for (once each).
        if (profile.MatchesPlayed > 0)
        {
            HintService.ShowOnce(HintId.MenuRank, CoachTarget.RankBadge, "Win to earn stars. Reach Silver to unlock a checkpoint.");
            DOVirtual.DelayedCall(5.5f, () =>
                HintService.ShowOnce(HintId.MenuObjectives, CoachTarget.Objectives, "Finish objectives for coins. Coins buy better cues."), true).SetLink(gameObject);
        }

        // v3 V12 probes on the way back to the menu: the survey (once), else the Starter Pack offer.
        if (profile.SurveyDue)
            DOVirtual.DelayedCall(0.8f, () => PopupManager.Instance.Show<SurveyPopup>(), true).SetLink(gameObject);
        else if (profile.StarterPackDue)
            DOVirtual.DelayedCall(0.8f, ShowStarterPack, true).SetLink(gameObject);
    }

    private static void ShowStarterPack()
    {
        ProfileService profile = ProfileService.Instance;
        ProfileSnapshot snapshot = profile.GetSnapshot();
        CueViewData pro = snapshot.Find("pro");
        CueViewData house = snapshot.Find("house");
        PopupManager.Instance.Show<StarterPackPopup>(new FakeDoorPopupData(FakeDoorId.StarterPack, profile.NotifyOn(FakeDoorId.StarterPack),
            pro, pro != null && house != null ? pro.Overall - house.Overall : 0, profile.Economy.StarterPackPrice));
    }

    private void HandleOnlineRankedRequested() =>
        PopupManager.Instance.Show<ComingSoonPopup>(new FakeDoorPopupData(FakeDoorId.OnlineRanked, ProfileService.Instance.NotifyOn(FakeDoorId.OnlineRanked)));

    private static void StartTutorial()
    {
        MatchSetup.Mode = MatchMode.Tutorial;
        MatchSetup.VenueId = MatchSetup.DefaultVenueId;
        SceneLoader.Instance.Load(InputRouter.MatchSceneName);
    }

    // Settings -> HOW TO PLAY: the tutorial again; it comes back to the menu.
    private void HandleHowToPlayRequested()
    {
        PopupManager.Instance.HideAll();
        StartTutorial();
    }

    private void HandleRulesRequested(RuleId focus) =>
        PopupManager.Instance.Show<RulesPopup>(new RulesPopupData(focus));

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
        SettingsPopup popup = PopupManager.Instance.Show<SettingsPopup>(new SettingsPopupData(audio.SfxVolume, audio.MusicVolume, HapticsService.Instance.Enabled, true));
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
