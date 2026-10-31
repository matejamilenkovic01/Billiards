using System.Collections;
using UnityEngine;

// The tutorial (v3 V9, v4 V10): three short lessons on a table set by hand (TutorialConfig) - aim and power (the aim
// first, wheel too, then the pull), release, spin (the cue ball would follow the ball in: move the hit point) - each
// retried until it works or the third miss; the bar is eased per lesson. Then the House Rules card, and the first
// ranked match (first time) or the menu (HOW TO PLAY). SKIP (or FORFEIT in the pause menu) ends it the same way.
public class TutorialController : MonoBehaviour
{
    [SerializeField] private TutorialConfig _config;
    [SerializeField] private PlayerShotController _player;

    private const float CoachDelay = 1.0f;
    private const float AimedHold = 0.4f;
    private const float WrongAimDegrees = 8f;

    private MatchController _match;
    private int _step;
    private int _tries;
    private int _misses;
    private bool _active;
    private bool _firstTime;
    private Vector2 _rightAim;
    private Vector2 _plannedCueBall;

    public bool IsActive => _active;
    public int Step => _step;
    public TutorialConfig Config => _config;
    public Vector2 RightAim => _rightAim;
    // Where the cue ball would stand for the lesson's shot (in hand: a good place to put it).
    public Vector2 PlannedCueBall => _plannedCueBall;

    private void OnEnable()
    {
        GameEvents.OnTutorialSkipRequested += Skip;
        GameEvents.OnTutorialShotBlocked += HandleShotBlocked;
    }

    private void OnDisable()
    {
        GameEvents.OnTutorialSkipRequested -= Skip;
        GameEvents.OnTutorialShotBlocked -= HandleShotBlocked;
    }

    // The Spin lesson holds the shot until the hit point is off the centre: say what to do, point at the icon again.
    private void HandleShotBlocked()
    {
        if (!_active)
            return;
        GameEvents.RaiseToastRequested(new ToastMessage(_config.MoveHitPoint, ToastStyle.Warning));
        GameEvents.RaiseCoachMarkRequested(Current.Coach, Current.CoachText);
    }

    public void Begin()
    {
        _match = MatchController.Instance;
        _firstTime = ProfileService.Instance == null || !ProfileService.Instance.TutorialDone;
        _active = true;
        StartStep(0);
    }

    private TutorialStep Current => _config.Steps[_step];

    private void StartStep(int index)
    {
        StopAllCoroutines();
        _step = index;
        _tries = 0;
        _misses = 0;
        TutorialStep step = Current;
        GameEvents.RaiseTutorialStep(index, TutorialStepResult.Started);
        GameEvents.RaiseTutorialStepShown(new TutorialStepInfo
        {
            Index = index,
            Count = _config.Steps.Count,
            Title = step.Title,
            Instruction = step.Instruction
        });
        SetUp(step);
        StartCoroutine(Coach(step));
    }

    // Once the controls are on screen, so the balloon finds a free spot around them. A two-part lesson (Aim & Power)
    // teaches the aim first - the wheel too - and the pull only when the line is right.
    private IEnumerator Coach(TutorialStep step)
    {
        yield return new WaitForSecondsRealtime(CoachDelay);
        if (step.Coach != CoachTarget.None)
            GameEvents.RaiseCoachMarkRequested(step.Coach, step.CoachText);
        if (!step.AimFirst)
            yield break;
        float aimedFor = 0f;
        while (aimedFor < AimedHold)
        {
            bool aimed = _match.Phase == MatchPhase.Turn && Vector2.Angle(_player.Aim, _rightAim) <= _config.AimTolerance;
            aimedFor = aimed ? aimedFor + Time.unscaledDeltaTime : 0f;
            yield return null;
        }
        ShowInstruction(step, step.InstructionPull);
        if (step.PullCoach != CoachTarget.None)
            GameEvents.RaiseCoachMarkRequested(step.PullCoach, step.PullCoachText);
    }

    private void ShowInstruction(TutorialStep step, string text) => GameEvents.RaiseTutorialStepShown(new TutorialStepInfo
    {
        Index = _step,
        Count = _config.Steps.Count,
        Title = step.Title,
        Instruction = text
    });

    // The lesson's table: the object ball by its pocket, the cue ball behind the ghost ball (or in hand).
    private void SetUp(TutorialStep step)
    {
        _match.ReleaseAssist = step.Assist switch
        {
            ReleaseAssistMode.AlwaysGreen => new ReleaseAssist { AlwaysGreen = true, PeriodScale = 1f, GreenScale = 1f },
            ReleaseAssistMode.Easy => new ReleaseAssist { PeriodScale = _config.EasyPeriodScale, GreenScale = _config.EasyGreenScale },
            _ => ReleaseAssist.None
        };

        TableGeometry geometry = _match.Geometry;
        Layout(step, geometry, out Vector2 objectBall, out Vector2 cueBall, out Vector2 ghost);
        _rightAim = (ghost - cueBall).normalized;
        _plannedCueBall = cueBall;
        _match.RequireSpin = step.RequireSpin;

        int count = 2 + step.ExtraBalls.Length;
        var numbers = new int[count];
        var positions = new Vector2[count];
        numbers[0] = 0;
        positions[0] = step.BallInHand ? RackBuilder.DefaultCueBallPosition(geometry) : cueBall;
        numbers[1] = step.ObjectBall;
        positions[1] = objectBall;
        for (int i = 0; i < step.ExtraBalls.Length; i++)
        {
            numbers[2 + i] = step.ExtraBalls[i];
            positions[2 + i] = step.ExtraPositions[i];
        }
        _match.SetupTable(BallGroup.Open, numbers, positions, step.BallInHand);
        if (step.PresetAim && !step.BallInHand)
            _player.SetAim(_rightAim);
        // The aim lesson starts with the line clearly off: it has to be moved (the default aim is almost the right one).
        else if (step.AimFirst && !step.BallInHand)
            _player.SetAim(ShotAccuracy.Rotate(_rightAim, WrongAimDegrees));
    }

    // The lesson's balls: the object ball ObjectDistance from the pocket along the approach, the ghost ball touching it
    // on the pocket's line, the cue ball CueDistance behind the ghost ball (turned by CutDegrees). Static so the
    // checks can set up the same table headless.
    public static void Layout(TutorialStep step, TableGeometry geometry, out Vector2 objectBall, out Vector2 cueBall, out Vector2 ghost)
    {
        PocketInfo pocket = geometry.Pockets[step.Pocket];
        Vector2 toPocket = step.Approach.sqrMagnitude > 0f ? step.Approach.normalized : pocket.Inward.normalized;
        objectBall = pocket.Center - toPocket * step.ObjectDistance;
        ghost = objectBall - toPocket * (2f * geometry.BallRadius);
        cueBall = ghost - ShotAccuracy.Rotate(toPocket, step.CutDegrees) * step.CueDistance;
    }

    // MatchController hands over every shot of the tutorial instead of the rules.
    public void HandleShot(ShotResult result, ShotFeedback feedback)
    {
        if (!_active)
            return;
        TutorialStep step = Current;
        _tries++;
        bool potted = false;
        foreach (PocketEvent pocketed in result.Pocketed)
            potted |= pocketed.Number != 0;

        string message = null;
        bool success = false;
        if (result.CueBallPocketed)
            message = step.Kind != TutorialStepKind.Spin ? _config.Scratch : feedback.Tip.y > -0.1f ? _config.ScratchFollow : _config.ScratchAgain;
        else if (!potted)
            message = feedback.Power01 < _config.TooSoftPull ? _config.TooSoft : feedback.Power01 > _config.TooHardPull ? _config.TooHard
                : feedback.Zone == ReleaseZone.Red ? _config.RedRelease : _config.Missed;
        else if (feedback.Zone == ReleaseZone.Red || (!step.YellowCounts && feedback.Zone == ReleaseZone.Yellow))
            message = _config.RedRelease;
        else if (feedback.Power01 > step.MaxPull + 0.001f)
            message = _config.PowerTooHigh;
        else
            success = true;

        bool moveOn = success || (step.MoveOnAfter > 0 && _tries >= step.MoveOnAfter);
        if (moveOn && !success)
            message = _config.KeepGoing;
        Debug.Log($"Tutorial: step {_step + 1} try {_tries}: {(success ? "success" : message)}");
        GameEvents.RaiseTutorialStep(_step, success ? TutorialStepResult.Succeeded : TutorialStepResult.Failed);
        GameEvents.RaiseToastRequested(new ToastMessage(success ? step.Success : message, success ? ToastStyle.Good : ToastStyle.Warning));
        if (!success)
            _misses++;
        StartCoroutine(Next(moveOn));
    }

    private IEnumerator Next(bool moveOn)
    {
        yield return new WaitForSeconds(_config.NextDelay);
        if (!moveOn)
        {
            SetUp(Current);
            TutorialStep step = Current;
            if (step.AimFirst)
            {
                // The line is taught again from the start.
                ShowInstruction(step, step.Instruction);
                StartCoroutine(Coach(step));
            }
            // After a few misses the right line is shown (and kept).
            if (step.ShowAimAfter > 0 && _misses >= step.ShowAimAfter && !step.BallInHand)
            {
                _player.SetAim(_rightAim);
                GameEvents.RaiseToastRequested(new ToastMessage(_config.ShowAim, ToastStyle.Info));
            }
            yield break;
        }
        if (_step + 1 < _config.Steps.Count)
            StartStep(_step + 1);
        else
            Complete(false);
    }

    private void Skip()
    {
        if (!_active)
            return;
        GameEvents.RaiseTutorialStep(_step, TutorialStepResult.Skipped);
        Complete(true);
    }

    // "You're ready!" -> House Rules -> the first ranked match (first time) or back to the menu.
    private void Complete(bool skipped)
    {
        StopAllCoroutines();
        _active = false;
        _match.RequireSpin = false;
        _match.ReleaseAssist = ReleaseAssist.None;
        Debug.Log($"Tutorial: completed (skipped {skipped})");
        GameEvents.RaiseTutorialCompleted(skipped);
        GameEvents.RaiseToastRequested(new ToastMessage(_config.Ready, ToastStyle.Good));
        StartCoroutine(ShowRules());
    }

    // The rules card after the "ready" toast has had its moment.
    private IEnumerator ShowRules()
    {
        yield return new WaitForSecondsRealtime(_config.NextDelay);
        RulesPopup rules = PopupManager.Instance != null ? PopupManager.Instance.Show<RulesPopup>(new RulesPopupData(RuleId.None,
            ProfileService.Instance != null ? ProfileService.Instance.CurrentRuleText : null, ProfileService.Instance != null ? ProfileService.Instance.CurrentRules : RuleSetId.Standard)) : null;
        if (rules != null)
            rules.Hidden += HandleRulesHidden;
        else
            Leave();
    }

    private void HandleRulesHidden(PopupBase popup)
    {
        popup.Hidden -= HandleRulesHidden;
        ProfileService.Instance?.MarkRulesSeen();
        Leave();
    }

    private void Leave()
    {
        if (_firstTime && ProfileService.Instance != null)
        {
            // The first ranked match: Bronze III against the friendliest character (v3 V13 FTUE).
            DivisionDefinition division = ProfileService.Instance.CurrentDivision;
            MatchSetup.Ranked(division);
            if (division.Opponents.Count > 0)
                MatchSetup.OpponentId = division.Opponents[0].Id;
            SceneLoader.Instance.Load(InputRouter.MatchSceneName);
        }
        else
        {
            MatchSetup.Mode = MatchMode.VsBot;
            SceneLoader.Instance.Load(InputRouter.MenuSceneName);
        }
    }
}
