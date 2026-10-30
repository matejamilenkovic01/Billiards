using UnityEngine;

// TRY a cue (v4 V9): three practice shots with the cue in hand, in the current division's venue. A long straight shot at
// HARD (the bar), a 30 degree cut from 1.2 m (the aim line), the break (the power). After every shot the player picks
// RETRY or NEXT; after the third comes the comparison screen. Unlimited and free; nothing reaches the profile.
public class PracticeController : MonoBehaviour
{
    [SerializeField] private PracticeConfig _config;

    private MatchController _match;
    private CueDefinition _tried;
    private CueDefinition _selected;
    private int _step;
    private int _shots;
    private int _perfect;
    private int _good;
    private bool _active;

    public bool IsActive => _active;
    public int Step => _step;
    public int Shots => _shots;
    public int Perfect => _perfect;
    public PracticeConfig Config => _config;

    private void OnEnable()
    {
        GameEvents.OnPracticeRetryRequested += Retry;
        GameEvents.OnPracticeNextRequested += Next;
        GameEvents.OnPracticeLeaveRequested += Leave;
    }

    private void OnDisable()
    {
        GameEvents.OnPracticeRetryRequested -= Retry;
        GameEvents.OnPracticeNextRequested -= Next;
        GameEvents.OnPracticeLeaveRequested -= Leave;
    }

    public void Begin()
    {
        _match = MatchController.Instance;
        _tried = _match.PlayerCue;
        _selected = ProfileService.Instance != null ? ProfileService.Instance.SelectedCue : _tried;
        _shots = _perfect = _good = 0;
        _active = true;
        StartStep(0);
    }

    private void StartStep(int index)
    {
        _step = index;
        TutorialStep step = _config.Steps[index];
        GameEvents.RaisePracticeStepShown(new PracticeStepInfo
        {
            Index = index,
            Count = _config.Steps.Count,
            Header = string.Format(_config.Header, _tried.Name.ToUpperInvariant()),
            Diff = Difference(_tried, _selected),
            Title = step.Title,
            Instruction = step.Instruction
        });
        SetUp(step);
    }

    // "Green +40% · Bar 19% slower · Aim line ×2 · Power +0.9 m/s", against the cue the player has selected.
    private string Difference(CueDefinition tried, CueDefinition selected)
    {
        if (tried.Id == selected.Id)
            return _config.SameCue;
        string Percent(float ratio) => $"{(ratio >= 1f ? "+" : "-")}{Mathf.Abs(ratio - 1f) * 100f:0}%";
        float bar = tried.OscillationPeriod / selected.OscillationPeriod - 1f;
        float guide = tried.ObjectGuideDiameters / selected.ObjectGuideDiameters;
        float power = tried.MaxSpeed - selected.MaxSpeed;
        return $"Green {Percent(tried.GreenHalf / selected.GreenHalf)} · Bar {Mathf.Abs(bar) * 100f:0}% {(bar >= 0f ? "slower" : "faster")} · " +
               $"Aim line ×{guide:0.#} · Power {(power >= 0f ? "+" : "-")}{Mathf.Abs(power):0.0} m/s";
    }

    // The step's table: the object ball by its pocket and the cue ball behind the ghost ball (TutorialStep layout), or
    // a full rack for the break.
    private void SetUp(TutorialStep step)
    {
        _match.ReleaseAssist = ReleaseAssist.None;
        if (step.Break)
        {
            _match.SetupBreak();
            return;
        }
        TableGeometry geometry = _match.Geometry;
        PocketInfo pocket = geometry.Pockets[step.Pocket];
        Vector2 toPocket = step.Approach.sqrMagnitude > 0f ? step.Approach.normalized : pocket.Inward.normalized;
        Vector2 objectBall = pocket.Center - toPocket * step.ObjectDistance;
        Vector2 ghost = objectBall - toPocket * (2f * geometry.BallRadius);
        Vector2 cueBall = ghost - ShotAccuracy.Rotate(toPocket, step.CutDegrees) * step.CueDistance;
        _match.SetupTable(BallGroup.Open, new[] { 0, step.ObjectBall }, new[] { cueBall, objectBall }, false);
    }

    // MatchController hands over every shot: it is counted, then the player chooses RETRY or NEXT.
    public void HandleShot(ShotResult result, ShotFeedback feedback)
    {
        if (!_active)
            return;
        _shots++;
        if (feedback.Zone == ReleaseZone.Green)
            _perfect++;
        else if (feedback.Zone == ReleaseZone.Yellow)
            _good++;
    }

    private void Retry()
    {
        if (_active)
            SetUp(_config.Steps[_step]);
    }

    private void Next()
    {
        if (!_active)
            return;
        if (_step + 1 < _config.Steps.Count)
        {
            StartStep(_step + 1);
            return;
        }
        Finish();
    }

    private void Finish()
    {
        _active = false;
        GameEvents.RaiseCueTryFinished(_tried.Id, _shots, _perfect);
        ProfileSnapshot snapshot = ProfileService.Instance.GetSnapshot();
        PopupManager.Instance.Show<CueTryResultPopup>(new CueTryResultPopupData(new CueTryResultData
        {
            Tried = snapshot.Find(_tried.Id),
            Selected = snapshot.SelectedCue,
            Shots = _shots,
            Perfect = _perfect,
            Good = _good
        }));
    }

    // Back to the menu (PLAY), or to the menu with the cues open (BACK TO CUES, and leaving from the pause menu).
    private void Leave(bool openCues)
    {
        _active = false;
        Time.timeScale = 1f;
        MatchSetup.Mode = MatchMode.VsBot;
        MatchSetup.PracticeCueId = "";
        MatchSetup.OpenCuesOnMenu = openCues;
        SceneLoader.Instance.Load(InputRouter.MenuSceneName);
    }
}
