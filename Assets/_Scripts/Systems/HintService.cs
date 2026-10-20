using UnityEngine;

// One-time, in-context hints (gameplay redesign G10, v3 V9). Decides when a hint is due, asks the UI for a coach
// mark through GameEvents and remembers it in the profile. So far: free look on the first opponent turn and on the
// first LOOK (v3 V2).
public class HintService : SingletonPersistent<HintService>
{
    private const string FreeLookText = "Drag to look around \u00b7 Pinch to zoom";

    private void OnEnable()
    {
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnLookModeToggled += HandleLookModeToggled;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnLookModeToggled -= HandleLookModeToggled;
    }

    private void HandleTurnStarted(TurnInfo info)
    {
        if (!info.IsHuman)
            ShowOnce(HintId.FreeLook, CoachTarget.FreeLook, FreeLookText);
    }

    private void HandleLookModeToggled(bool on)
    {
        if (on)
            ShowOnce(HintId.LookMode, CoachTarget.FreeLook, FreeLookText);
    }

    private static void ShowOnce(HintId hint, CoachTarget target, string text)
    {
        ProfileService profile = ProfileService.Instance;
        if (profile == null || profile.WasHintShown(hint))
            return;
        profile.MarkHintShown(hint);
        GameEvents.RaiseCoachMarkRequested(target, text);
    }
}
