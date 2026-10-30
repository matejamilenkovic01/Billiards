using DG.Tweening;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    private void OnEnable()
    {
        GameEvents.OnPlayRequested += HandlePlayRequested;
        GameEvents.OnSettingsRequested += HandleSettingsRequested;
        GameEvents.OnCuesRequested += HandleCuesRequested;
        GameEvents.OnCueTryRequested += HandleCueTryRequested;
        GameEvents.OnDivisionsRequested += HandleDivisionsRequested;
        GameEvents.OnObjectivesRequested += HandleObjectivesRequested;
        GameEvents.OnObjectiveSetCompleted += HandleSetCompleted;
        GameEvents.OnProfileChanged += HandleProfileChanged;
        GameEvents.OnHowToPlayRequested += HandleHowToPlayRequested;
        GameEvents.OnRulesRequested += HandleRulesRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayRequested -= HandlePlayRequested;
        GameEvents.OnSettingsRequested -= HandleSettingsRequested;
        GameEvents.OnCuesRequested -= HandleCuesRequested;
        GameEvents.OnCueTryRequested -= HandleCueTryRequested;
        GameEvents.OnDivisionsRequested -= HandleDivisionsRequested;
        GameEvents.OnObjectivesRequested -= HandleObjectivesRequested;
        GameEvents.OnObjectiveSetCompleted -= HandleSetCompleted;
        GameEvents.OnProfileChanged -= HandleProfileChanged;
        GameEvents.OnHowToPlayRequested -= HandleHowToPlayRequested;
        GameEvents.OnRulesRequested -= HandleRulesRequested;
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
            HintService.ShowOnce(HintId.MenuRank, CoachTarget.RankBadge, "Win to earn ★. Each division brings a new 8-ball rule.");
            DOVirtual.DelayedCall(5.5f, () =>
                HintService.ShowOnce(HintId.MenuObjectives, CoachTarget.Objectives, "Objectives pay coins — some count even when you lose."), true).SetLink(gameObject);
        }

        // Back from a cue try: the cues again, where the player left off (v4 V9).
        if (MatchSetup.OpenCuesOnMenu)
        {
            MatchSetup.OpenCuesOnMenu = false;
            DOVirtual.DelayedCall(0.4f, HandleCuesRequested, true).SetLink(gameObject);
            return;
        }
    }

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
        PopupManager.Instance.Show<RulesPopup>(new RulesPopupData(focus, ProfileService.Instance.CurrentRuleText, ProfileService.Instance.CurrentRules));

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

    // TRY (v4 V9): three practice shots with that cue in the current division's venue; nothing is counted.
    private void HandleCueTryRequested(string cueId)
    {
        ProfileService profile = ProfileService.Instance;
        MatchSetup.Mode = MatchMode.Practice;
        MatchSetup.PracticeCueId = cueId;
        MatchSetup.DivisionId = profile.CurrentDivision.Id;
        MatchSetup.VenueId = profile.CurrentDivision.VenueId;
        PopupManager.Instance.HideAll();
        SceneLoader.Instance.Load(InputRouter.MatchSceneName);
    }

    // DIVISIONS (v4 V11): the whole journey; looking at it clears the NEW chip.
    private void HandleDivisionsRequested()
    {
        ProfileService profile = ProfileService.Instance;
        PopupManager.Instance.Show<DivisionsPopup>(new DivisionsPopupData(profile.DivisionsView()));
        profile.MarkDivisionsSeen();
    }

    // ALL OBJECTIVES (v4 V12).
    private ObjectivesPopup _objectivesPopup;

    private void HandleObjectivesRequested() =>
        _objectivesPopup = PopupManager.Instance.Show<ObjectivesPopup>(new ObjectivesPopupData(ProfileService.Instance.ObjectivesView()));

    // A CLAIM inside ALL OBJECTIVES: the set's counter and bonus move on.
    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        if (_objectivesPopup != null && _objectivesPopup.IsShown)
            _objectivesPopup.Refresh(ProfileService.Instance.ObjectivesView());
    }

    // A set was completed by a CLAIM on the menu: the bonus gets its moment.
    private void HandleSetCompleted(CelebrationData data) =>
        DOVirtual.DelayedCall(0.3f, () => PopupManager.Instance.Show<CelebrationPopup>(new CelebrationPopupData(data)), true).SetLink(gameObject);
}
