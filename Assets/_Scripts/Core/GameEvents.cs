using System;
using UnityEngine;

// Single hub for every cross-layer event. UI only talks to the game through these.
public static class GameEvents
{
    #region Menu / profile
    public static event Action OnPlayRequested;
    public static event Action OnSettingsRequested;
    public static event Action OnCuesRequested;
    public static event Action<string> OnCueSelectRequested;
    public static event Action<string> OnCueBuyRequested;
    public static event Action<string> OnCueRefillRequested;
    public static event Action<ProfileSnapshot> OnProfileChanged;
    public static event Action<string> OnPurchaseFailed;

    public static void RaisePlayRequested() => OnPlayRequested?.Invoke();
    public static void RaiseSettingsRequested() => OnSettingsRequested?.Invoke();
    public static void RaiseCuesRequested() => OnCuesRequested?.Invoke();
    public static void RaiseCueSelectRequested(string cueId) => OnCueSelectRequested?.Invoke(cueId);
    public static void RaiseCueBuyRequested(string cueId) => OnCueBuyRequested?.Invoke(cueId);
    public static void RaiseCueRefillRequested(string cueId) => OnCueRefillRequested?.Invoke(cueId);
    public static void RaiseProfileChanged(ProfileSnapshot snapshot) => OnProfileChanged?.Invoke(snapshot);
    public static void RaisePurchaseFailed(string reason) => OnPurchaseFailed?.Invoke(reason);
    #endregion

    #region Settings / UI
    public static event Action<float> OnSfxVolumeChanged;
    public static event Action<float> OnMusicVolumeChanged;
    public static event Action OnButtonClicked;
    public static event Action OnDisabledButtonClicked;
    public static event Action OnPopupShown;
    public static event Action<ToastMessage> OnToastRequested;
    public static event Action OnCoinTick;

    public static void RaiseSfxVolumeChanged(float volume) => OnSfxVolumeChanged?.Invoke(volume);
    public static void RaiseMusicVolumeChanged(float volume) => OnMusicVolumeChanged?.Invoke(volume);
    public static void RaiseButtonClicked() => OnButtonClicked?.Invoke();
    public static void RaiseDisabledButtonClicked() => OnDisabledButtonClicked?.Invoke();
    public static void RaisePopupShown() => OnPopupShown?.Invoke();
    public static void RaiseToastRequested(ToastMessage message) => OnToastRequested?.Invoke(message);
    public static void RaiseCoinTick() => OnCoinTick?.Invoke();
    #endregion

    #region Match flow
    public static event Action OnPauseRequested;
    public static event Action OnResumeRequested;
    public static event Action<bool> OnPauseChanged;
    public static event Action OnForfeitRequested;
    public static event Action OnMenuRequested;
    public static event Action<RuleId> OnRulesRequested;
    public static event Action<MatchStartInfo> OnMatchStarted;
    public static event Action<TurnInfo> OnTurnStarted;
    public static event Action<TurnPhase> OnTurnPhaseChanged;
    public static event Action<BallGroup, BallGroup> OnGroupsAssigned;
    public static event Action<int, int> OnBallPocketed;
    // number, the pocket's screen position (pixels), how it counts, entry speed (m/s): drop glow, flyer, sound (V7).
    public static event Action<int, Vector2, PocketFxKind, float> OnBallPocketedFx;
    // Sum of the balls' speeds while they roll (m/s), every simulated frame; 0 when they stop (roll sound).
    public static event Action<float> OnRollingSpeed;
    public static event Action<FoulInfo> OnFoul;
    public static event Action<MatchResult> OnMatchEnded;

    public static void RaisePauseRequested() => OnPauseRequested?.Invoke();
    public static void RaiseResumeRequested() => OnResumeRequested?.Invoke();
    public static void RaisePauseChanged(bool paused) => OnPauseChanged?.Invoke(paused);
    public static void RaiseForfeitRequested() => OnForfeitRequested?.Invoke();
    public static void RaiseMenuRequested() => OnMenuRequested?.Invoke();
    public static void RaiseRulesRequested(RuleId focus) => OnRulesRequested?.Invoke(focus);
    public static void RaiseMatchStarted(MatchStartInfo info) => OnMatchStarted?.Invoke(info);
    public static void RaiseTurnStarted(TurnInfo info) => OnTurnStarted?.Invoke(info);
    public static void RaiseTurnPhaseChanged(TurnPhase phase) => OnTurnPhaseChanged?.Invoke(phase);
    public static void RaiseGroupsAssigned(BallGroup player, BallGroup bot) => OnGroupsAssigned?.Invoke(player, bot);
    public static void RaiseBallPocketed(int number, int pocketIndex) => OnBallPocketed?.Invoke(number, pocketIndex);
    public static void RaiseBallPocketedFx(int number, Vector2 screenPosition, PocketFxKind kind, float speed) =>
        OnBallPocketedFx?.Invoke(number, screenPosition, kind, speed);
    public static void RaiseRollingSpeed(float totalSpeed) => OnRollingSpeed?.Invoke(totalSpeed);
    public static void RaiseFoul(FoulInfo foul) => OnFoul?.Invoke(foul);
    public static void RaiseMatchEnded(MatchResult result) => OnMatchEnded?.Invoke(result);
    #endregion

    #region Shot controls (UI -> gameplay)
    public static event Action<Vector2> OnLookDrag;
    public static event Action<bool> OnLookModeToggled;
    public static event Action<float> OnFineAimDrag;
    public static event Action<Vector2> OnCueBallDragged;
    public static event Action OnCueBallDragEnded;
    public static event Action<Vector2> OnTableTapped;
    public static event Action<Vector2> OnTablePressed;
    public static event Action OnPlaceCueBallConfirmed;
    public static event Action OnMoveCueBallRequested;
    public static event Action<float> OnPullChanged;
    public static event Action OnPullStarted;
    public static event Action<int, bool> OnFineAimStep;
    public static event Action OnPullReleased;
    // Orbit camera (v3 V2): 1-finger drag, 2-finger pinch (scale > 1 = fingers apart) and pan, all in reference pixels.
    public static event Action<Vector2> OnOrbitDrag;
    public static event Action<float> OnOrbitPinch;
    public static event Action<Vector2> OnOrbitPan;
    public static event Action OnOrbitDragEnded;
    public static event Action OnTableViewRequested;

    public static void RaiseLookDrag(Vector2 deltaPx) => OnLookDrag?.Invoke(deltaPx);
    public static void RaiseLookModeToggled(bool on) => OnLookModeToggled?.Invoke(on);
    public static void RaiseFineAimDrag(float deltaPx) => OnFineAimDrag?.Invoke(deltaPx);
    public static void RaiseCueBallDragged(Vector2 screenPos) => OnCueBallDragged?.Invoke(screenPos);
    public static void RaiseCueBallDragEnded() => OnCueBallDragEnded?.Invoke();
    public static void RaiseTableTapped(Vector2 screenPos) => OnTableTapped?.Invoke(screenPos);
    public static void RaiseTablePressed(Vector2 screenPos) => OnTablePressed?.Invoke(screenPos);
    public static void RaisePlaceCueBallConfirmed() => OnPlaceCueBallConfirmed?.Invoke();
    public static void RaiseMoveCueBallRequested() => OnMoveCueBallRequested?.Invoke();
    public static void RaisePullChanged(float pull01) => OnPullChanged?.Invoke(pull01);
    public static void RaisePullStarted() => OnPullStarted?.Invoke();
    // direction: +1 = right (the cue turns clockwise seen from above), -1 = left.
    public static void RaiseFineAimStep(int direction, bool repeating) => OnFineAimStep?.Invoke(direction, repeating);
    public static void RaisePullReleased() => OnPullReleased?.Invoke();
    public static void RaiseOrbitDrag(Vector2 deltaPx) => OnOrbitDrag?.Invoke(deltaPx);
    public static void RaiseOrbitPinch(float scaleDelta) => OnOrbitPinch?.Invoke(scaleDelta);
    public static void RaiseOrbitPan(Vector2 deltaPx) => OnOrbitPan?.Invoke(deltaPx);
    public static void RaiseOrbitDragEnded() => OnOrbitDragEnded?.Invoke();
    public static void RaiseTableViewRequested() => OnTableViewRequested?.Invoke();
    #endregion

    #region Shot (gameplay -> UI)
    public static event Action<ReleaseMeterSettings> OnReleaseMeterSetup;
    // position, power01, greenHalf, yellowEnd, period: the window shrinks and the bar speeds up as you pull (V8).
    public static event Action<float, float, float, float, float> OnReleaseMeterChanged;
    public static event Action<ShotFeedback> OnShotFired;
    public static event Action<float, CollisionKind> OnBallCollision;
    public static event Action OnAimSnapped;
    public static event Action OnCueBallPlaced;
    public static event Action OnFineAimTick;
    // The opponent's turn: the player has taken the camera (free look) or given it back.
    public static event Action<bool> OnFreeLookChanged;
    public static event Action<CoachTarget, string> OnCoachMarkRequested;
    public static event Action OnCoachMarkCleared;

    public static void RaiseReleaseMeterSetup(ReleaseMeterSettings settings) => OnReleaseMeterSetup?.Invoke(settings);
    public static void RaiseReleaseMeterChanged(float position, float power01, float greenHalf, float yellowEnd, float period) =>
        OnReleaseMeterChanged?.Invoke(position, power01, greenHalf, yellowEnd, period);
    public static void RaiseShotFired(ShotFeedback feedback) => OnShotFired?.Invoke(feedback);
    public static void RaiseBallCollision(float impactSpeed, CollisionKind kind) => OnBallCollision?.Invoke(impactSpeed, kind);
    public static void RaiseAimSnapped() => OnAimSnapped?.Invoke();
    public static void RaiseCueBallPlaced() => OnCueBallPlaced?.Invoke();
    public static void RaiseFineAimTick() => OnFineAimTick?.Invoke();
    public static void RaiseFreeLookChanged(bool on) => OnFreeLookChanged?.Invoke(on);
    public static void RaiseCoachMarkRequested(CoachTarget target, string text) => OnCoachMarkRequested?.Invoke(target, text);
    public static void RaiseCoachMarkCleared() => OnCoachMarkCleared?.Invoke();
    #endregion

    // Static events survive Play mode when domain reload is disabled, so stale subscribers are dropped here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAll()
    {
        OnPlayRequested = null;
        OnSettingsRequested = null;
        OnCuesRequested = null;
        OnCueSelectRequested = null;
        OnCueBuyRequested = null;
        OnCueRefillRequested = null;
        OnProfileChanged = null;
        OnPurchaseFailed = null;

        OnSfxVolumeChanged = null;
        OnMusicVolumeChanged = null;
        OnButtonClicked = null;
        OnDisabledButtonClicked = null;
        OnPopupShown = null;
        OnToastRequested = null;
        OnCoinTick = null;

        OnPauseRequested = null;
        OnResumeRequested = null;
        OnPauseChanged = null;
        OnForfeitRequested = null;
        OnMenuRequested = null;
        OnRulesRequested = null;
        OnMatchStarted = null;
        OnTurnStarted = null;
        OnTurnPhaseChanged = null;
        OnGroupsAssigned = null;
        OnBallPocketed = null;
        OnBallPocketedFx = null;
        OnRollingSpeed = null;
        OnFoul = null;
        OnMatchEnded = null;

        OnLookDrag = null;
        OnLookModeToggled = null;
        OnFineAimDrag = null;
        OnCueBallDragged = null;
        OnCueBallDragEnded = null;
        OnTableTapped = null;
        OnTablePressed = null;
        OnPlaceCueBallConfirmed = null;
        OnMoveCueBallRequested = null;
        OnPullChanged = null;
        OnPullStarted = null;
        OnFineAimStep = null;
        OnPullReleased = null;
        OnOrbitDrag = null;
        OnOrbitPinch = null;
        OnOrbitPan = null;
        OnOrbitDragEnded = null;
        OnTableViewRequested = null;

        OnReleaseMeterSetup = null;
        OnReleaseMeterChanged = null;
        OnShotFired = null;
        OnBallCollision = null;
        OnAimSnapped = null;
        OnCueBallPlaced = null;
        OnFineAimTick = null;
        OnFreeLookChanged = null;
        OnCoachMarkRequested = null;
        OnCoachMarkCleared = null;
    }
}
