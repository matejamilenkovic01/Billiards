using System.Collections;
using UnityEngine;

// The tutorial (v3 V9, gameplay G10): five short lessons on a table set by hand (TutorialConfig), each retried until
// the ball drops; the bar is eased per lesson. Then the House Rules card, and the first ranked match (first time)
// or the menu (HOW TO PLAY). SKIP (or FORFEIT in the pause menu) ends it the same way.
public class TutorialController : MonoBehaviour
{
    [SerializeField] private TutorialConfig _config;
    [SerializeField] private PlayerShotController _player;

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

    private void OnEnable() => GameEvents.OnTutorialSkipRequested += Skip;

    private void OnDisable() => GameEvents.OnTutorialSkipRequested -= Skip;

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
        if (step.Coach != CoachTarget.None)
            GameEvents.RaiseCoachMarkRequested(step.Coach, step.CoachText);
    }

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
        PocketInfo pocket = geometry.Pockets[step.Pocket];
        Vector2 toPocket = step.Approach.sqrMagnitude > 0f ? step.Approach.normalized : pocket.Inward.normalized;
        Vector2 objectBall = pocket.Center - toPocket * step.ObjectDistance;
        Vector2 ghost = objectBall - toPocket * (2f * geometry.BallRadius);
        Vector2 cueBall = ghost - ShotAccuracy.Rotate(toPocket, step.CutDegrees) * step.CueDistance;
        _rightAim = (ghost - cueBall).normalized;
        _plannedCueBall = cueBall;

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
            message = _config.Scratch;
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
