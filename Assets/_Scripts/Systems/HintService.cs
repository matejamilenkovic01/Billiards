using DG.Tweening;
using UnityEngine;

// One-time, in-context hints (gameplay redesign G10, v3 V9): decides when a hint is due, asks the UI for a coach
// mark (or a toast, over the results) through GameEvents and remembers it in the profile. Real matches only, never
// in the tutorial.
public class HintService : SingletonPersistent<HintService>
{
    private const string FreeLookText = "Drag to look around · Pinch to zoom";
    private const int ShotsBeforeFineAimHint = 5;
    private const int RedsBeforeGreenHint = 2;
    private const float HardShot = 0.85f;
    private const float ResultsHintDelay = 2.6f;

    private int _shotsWithoutFineAim;
    private bool _usedFineAim;
    private int _redsInARow;
    private bool _hardShotPending;

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnLookModeToggled += HandleLookModeToggled;
        GameEvents.OnGroupsAssigned += HandleGroupsAssigned;
        GameEvents.OnShotFired += HandleShotFired;
        GameEvents.OnMatchEnded += HandleMatchEnded;
        GameEvents.OnOpponentEightPocketRequested += HandleOpponentEightPocketRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnLookModeToggled -= HandleLookModeToggled;
        GameEvents.OnGroupsAssigned -= HandleGroupsAssigned;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
        GameEvents.OnOpponentEightPocketRequested -= HandleOpponentEightPocketRequested;
    }

    private static bool InRealMatch => MatchSetup.Mode == MatchMode.VsBot;

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _redsInARow = 0;
        _hardShotPending = false;
        _shotsWithoutFineAim = 0;
        _usedFineAim = false;
    }

    private void HandleTurnStarted(TurnInfo info)
    {
        if (!InRealMatch)
            return;
        if (!info.IsHuman)
        {
            // A hard shot that ended the visit missed.
            if (_hardShotPending)
                ShowOnce(HintId.PowerMiss, CoachTarget.Balloon, "Big power makes the green smaller. Try less.");
            _hardShotPending = false;
            if (!info.IsPlayer)
                ShowOnce(HintId.FreeLook, CoachTarget.FreeLook, FreeLookText);
            return;
        }

        _hardShotPending = false;
        if (info.NeedsCall && info.CalledPocket < 0)
            ShowOnce(HintId.PocketCall, CoachTarget.PocketMap, "Call your pocket: tap it on the map or on the table.");
        else if (info.BallInHand && !info.IsBreak)
            ShowOnce(HintId.BallInHand, CoachTarget.Balloon, "BALL IN HAND: drag the cue ball anywhere, then shoot.");
        else if (info.PlayerOnEight)
            ShowOnce(HintId.EightOnly, CoachTarget.Balloon, "Only the 8 is left. Pocket it to win!");
        else if (_redsInARow >= RedsBeforeGreenHint)
            ShowOnce(HintId.GreenRelease, CoachTarget.Balloon, "Let go on GREEN for a straight shot.");
        else if (!_usedFineAim && _shotsWithoutFineAim >= ShotsBeforeFineAimHint)
            ShowOnce(HintId.FineAim, CoachTarget.FineAim, "Use the wheel for tiny adjustments.");
    }

    // v4: the first time the player chooses the pocket for the opponent's 8.
    private void HandleOpponentEightPocketRequested(PocketChoiceKind kind)
    {
        if (InRealMatch)
            ShowOnce(HintId.PocketChoice, CoachTarget.Balloon, kind == PocketChoiceKind.Block
                ? "Your opponent is on the 8. Block the pocket they'd like most."
                : "Your opponent is on the 8. Pick the hardest pocket for them.");
    }

    private void HandleLookModeToggled(bool on)
    {
        if (on && InRealMatch)
            ShowOnce(HintId.LookMode, CoachTarget.FreeLook, FreeLookText);
    }

    private void HandleGroupsAssigned(BallGroup player, BallGroup bot)
    {
        if (!InRealMatch || player == BallGroup.Open)
            return;
        string group = player == BallGroup.Solids ? "SOLIDS (1-7)" : "STRIPES (9-15)";
        ShowOnce(HintId.Groups, CoachTarget.Balloon, $"You are {group}: pocket them all, then the 8.");
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (!feedback.IsPlayer || !InRealMatch)
            return;
        _usedFineAim |= feedback.UsedFineAim;
        _shotsWithoutFineAim = feedback.UsedFineAim ? 0 : _shotsWithoutFineAim + 1;
        _redsInARow = feedback.Zone == ReleaseZone.Red ? _redsInARow + 1 : 0;
        _hardShotPending = feedback.Power01 > HardShot && !feedback.IsBreak;
    }

    // Over the results screen: toasts (they sit above popups).
    private void HandleMatchEnded(MatchResult result)
    {
        if (!InRealMatch)
            return;
        if (result.StarsGained < 0)
            ToastOnce(HintId.StarLost, "Lost a star. You can't drop below a checkpoint.");
        else if (result.ObjectivesCompleted.Count > 0)
            ToastOnce(HintId.ClaimObjective, "Objective done! Tap CLAIM for your coins.");
    }

    private void ToastOnce(HintId hint, string text)
    {
        if (!MarkOnce(hint))
            return;
        DOVirtual.DelayedCall(ResultsHintDelay, () => GameEvents.RaiseToastRequested(new ToastMessage(text, ToastStyle.Info)), true)
            .SetLink(gameObject);
    }

    public static void ShowOnce(HintId hint, CoachTarget target, string text)
    {
        if (MarkOnce(hint))
            GameEvents.RaiseCoachMarkRequested(target, text);
    }

    private static bool MarkOnce(HintId hint)
    {
        ProfileService profile = ProfileService.Instance;
        if (profile == null || profile.WasHintShown(hint))
            return false;
        profile.MarkHintShown(hint);
        return true;
    }
}
