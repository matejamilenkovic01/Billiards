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
    public static event Action<int> OnObjectiveClaimRequested;
    public static event Action OnRankedRoadRequested;
    public static event Action OnHowToPlayRequested;
    public static event Action<RewardedPlacement> OnRewardedAdRequested;
    public static event Action<RewardedPlacement, bool> OnRewardedAdFinished;   // rewarded
    public static event Action<FakeDoorId, FakeDoorAction> OnFakeDoor;
    public static event Action OnOnlineRankedRequested;
    public static event Action<int, int> OnSurveyAnswered;                      // score (1-3), SurveyMissing mask
    public static event Action OnSurveySkipped;
    public static event Action<ProfileSnapshot> OnProfileChanged;
    public static event Action<string> OnPurchaseFailed;

    public static void RaisePlayRequested() => OnPlayRequested?.Invoke();
    public static void RaiseSettingsRequested() => OnSettingsRequested?.Invoke();
    public static void RaiseCuesRequested() => OnCuesRequested?.Invoke();
    public static void RaiseCueSelectRequested(string cueId) => OnCueSelectRequested?.Invoke(cueId);
    public static void RaiseCueBuyRequested(string cueId) => OnCueBuyRequested?.Invoke(cueId);
    public static void RaiseObjectiveClaimRequested(int slot) => OnObjectiveClaimRequested?.Invoke(slot);
    public static void RaiseRankedRoadRequested() => OnRankedRoadRequested?.Invoke();
    public static void RaiseHowToPlayRequested() => OnHowToPlayRequested?.Invoke();
    public static void RaiseRewardedAdRequested(RewardedPlacement placement) => OnRewardedAdRequested?.Invoke(placement);
    public static void RaiseRewardedAdFinished(RewardedPlacement placement, bool rewarded) => OnRewardedAdFinished?.Invoke(placement, rewarded);
    public static void RaiseFakeDoor(FakeDoorId id, FakeDoorAction action) => OnFakeDoor?.Invoke(id, action);
    public static void RaiseOnlineRankedRequested() => OnOnlineRankedRequested?.Invoke();
    public static void RaiseSurveyAnswered(int score, int missingMask) => OnSurveyAnswered?.Invoke(score, missingMask);
    public static void RaiseSurveySkipped() => OnSurveySkipped?.Invoke();
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
    // The results screen celebrates a rank step (v3 V12): sound and haptics follow.
    public static event Action<RankCelebration> OnRankCelebration;
    public static event Action<bool> OnHapticsToggled;

    public static void RaiseSfxVolumeChanged(float volume) => OnSfxVolumeChanged?.Invoke(volume);
    public static void RaiseMusicVolumeChanged(float volume) => OnMusicVolumeChanged?.Invoke(volume);
    public static void RaiseButtonClicked() => OnButtonClicked?.Invoke();
    public static void RaiseDisabledButtonClicked() => OnDisabledButtonClicked?.Invoke();
    public static void RaisePopupShown() => OnPopupShown?.Invoke();
    public static void RaiseToastRequested(ToastMessage message) => OnToastRequested?.Invoke(message);
    public static void RaiseCoinTick() => OnCoinTick?.Invoke();
    public static void RaiseRankCelebration(RankCelebration kind) => OnRankCelebration?.Invoke(kind);
    public static void RaiseHapticsToggled(bool on) => OnHapticsToggled?.Invoke(on);
    #endregion

    #region Tutorial (v3 V9)
    public static event Action<int, TutorialStepResult> OnTutorialStep;
    public static event Action<TutorialStepInfo> OnTutorialStepShown;
    public static event Action<bool> OnTutorialCompleted;      // skipped
    public static event Action OnTutorialSkipRequested;

    public static void RaiseTutorialStep(int step, TutorialStepResult result) => OnTutorialStep?.Invoke(step, result);
    public static void RaiseTutorialStepShown(TutorialStepInfo info) => OnTutorialStepShown?.Invoke(info);
    public static void RaiseTutorialCompleted(bool skipped) => OnTutorialCompleted?.Invoke(skipped);
    public static void RaiseTutorialSkipRequested() => OnTutorialSkipRequested?.Invoke();
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
    // v4 V2: where the tip will meet the cue ball (ball radii), from the spin picker; back to the centre after a shot.
    public static event Action<Vector2> OnSpinChanged;
    public static event Action OnSpinReset;
    // v4 V4/V5: called pockets and the opponent's choice for an 8 (gameplay <-> the pocket map and the table).
    public static event Action<int> OnPocketCallRequested;
    public static event Action<int, PocketCallKind> OnPocketCallChanged;
    public static event Action<PocketChoiceKind> OnOpponentEightPocketRequested;
    public static event Action<int> OnOpponentEightPocketChosen;
    public static event Action<PocketMapInfo> OnPocketMapChanged;
    public static event Action OnPocketCallNudged;
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
    public static void RaiseSpinChanged(Vector2 tip) => OnSpinChanged?.Invoke(tip);
    public static void RaiseSpinReset() => OnSpinReset?.Invoke();
    public static void RaisePocketCallRequested(int pocket) => OnPocketCallRequested?.Invoke(pocket);
    public static void RaisePocketCallChanged(int pocket, PocketCallKind kind) => OnPocketCallChanged?.Invoke(pocket, kind);
    public static void RaiseOpponentEightPocketRequested(PocketChoiceKind kind) => OnOpponentEightPocketRequested?.Invoke(kind);
    public static void RaiseOpponentEightPocketChosen(int pocket) => OnOpponentEightPocketChosen?.Invoke(pocket);
    public static void RaisePocketMapChanged(PocketMapInfo info) => OnPocketMapChanged?.Invoke(info);
    public static void RaisePocketCallNudged() => OnPocketCallNudged?.Invoke();
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
        OnObjectiveClaimRequested = null;
        OnRankedRoadRequested = null;
        OnHowToPlayRequested = null;
        OnRewardedAdRequested = null;
        OnRewardedAdFinished = null;
        OnFakeDoor = null;
        OnOnlineRankedRequested = null;
        OnSurveyAnswered = null;
        OnSurveySkipped = null;
        OnTutorialStep = null;
        OnTutorialStepShown = null;
        OnTutorialCompleted = null;
        OnTutorialSkipRequested = null;
        OnProfileChanged = null;
        OnPurchaseFailed = null;

        OnSfxVolumeChanged = null;
        OnMusicVolumeChanged = null;
        OnButtonClicked = null;
        OnDisabledButtonClicked = null;
        OnPopupShown = null;
        OnToastRequested = null;
        OnCoinTick = null;
        OnRankCelebration = null;
        OnHapticsToggled = null;

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
        OnSpinChanged = null;
        OnSpinReset = null;
        OnPocketCallRequested = null;
        OnPocketCallChanged = null;
        OnOpponentEightPocketRequested = null;
        OnOpponentEightPocketChosen = null;
        OnPocketMapChanged = null;
        OnPocketCallNudged = null;
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
