using System.Text;
using UnityEditor;
using UnityEngine;

// v3 V9 in Play mode (Match scene open): RunThrough plays every lesson with a perfect release on the right line
// (placing the cue ball in hand first) and expects OnTutorialCompleted(false) with the profile flag set; Skip expects
// OnTutorialCompleted(true). Both run over frames: call Status() until it says done.
public static class TutorialChecks
{
    private static readonly float[] Pulls = { 0.36f, 0.4f, 0.38f, 0.55f, 0.4f };
    private const double TimeoutSeconds = 120.0;

    private static StringBuilder _log;
    private static bool _running;
    private static bool _done;
    private static bool? _completedSkipped;
    private static int _lastShotStep = -1;
    private static int _shots;
    private static int _placedBeforeShot = -1;
    private static double _started;
    private static double _waitUntil;
    private static bool _expectSkip;

    public static string RunThrough() => Start(false);

    public static string Skip() => Start(true);

    public static string Status()
    {
        if (_log == null)
            return "not started";
        return (_done ? "DONE\n" : "running\n") + _log;
    }

    private static string Start(bool skip)
    {
        if (!EditorApplication.isPlaying || MatchController.Instance == null)
            return "TutorialChecks: enter Play mode in the Match scene first";
        Stop();
        _log = new StringBuilder();
        _running = true;
        _done = false;
        _completedSkipped = null;
        _lastShotStep = -1;
        _shots = 0;
        _placedBeforeShot = -1;
        _expectSkip = skip;
        _started = EditorApplication.timeSinceStartup;
        _waitUntil = _started + 1.0;
        PlayerPrefs.SetInt(SaveKeys.TutorialDone, 0);
        GameEvents.OnTutorialStep += HandleStep;
        GameEvents.OnTutorialCompleted += HandleCompleted;
        EditorApplication.update += Tick;
        _log.AppendLine(MatchController.DebugStartTutorial());
        if (skip)
            GameEvents.RaiseTutorialSkipRequested();
        return "started";
    }

    private static void HandleStep(int step, TutorialStepResult result) => _log.AppendLine($"step {step + 1}: {result}");

    private static void HandleCompleted(bool skipped)
    {
        _completedSkipped = skipped;
        _log.AppendLine($"completed (skipped {skipped}), TutorialDone {PlayerPrefs.GetInt(SaveKeys.TutorialDone, 0)}");
    }

    private static void Tick()
    {
        if (!_running)
            return;
        double now = EditorApplication.timeSinceStartup;
        if (_completedSkipped.HasValue || now - _started > TimeoutSeconds || !EditorApplication.isPlaying)
        {
            Finish();
            return;
        }
        if (now < _waitUntil)
            return;

        MatchController match = MatchController.Instance;
        var tutorial = Object.FindFirstObjectByType<TutorialController>();
        var player = Object.FindFirstObjectByType<PlayerShotController>();
        if (match == null || tutorial == null || player == null || !tutorial.IsActive || match.Phase != MatchPhase.Turn)
            return;

        int step = tutorial.Step;
        TutorialStep lesson = tutorial.Config.Steps[step];
        if (match.Rules.BallInHand && _placedBeforeShot != _shots)
        {
            _placedBeforeShot = _shots;
            match.PlaceCueBall(tutorial.PlannedCueBall);
            GameEvents.RaisePlaceCueBallConfirmed();
            _waitUntil = now + 0.5;
            return;
        }
        player.SetAim(tutorial.RightAim);
        float pull = Mathf.Min(Pulls[Mathf.Min(step, Pulls.Length - 1)], lesson.MaxPull);
        player.DebugReleaseAt(0f, pull);
        _shots++;
        _lastShotStep = step;
        _log.AppendLine($"  shot {_shots}: lesson {step + 1} pull {pull:P0}");
        _waitUntil = now + 1.0;
    }

    private static void Finish()
    {
        var report = new CheckReport(_expectSkip ? "TutorialChecks.Skip" : "TutorialChecks.RunThrough");
        report.Check(_completedSkipped.HasValue, "OnTutorialCompleted raised");
        report.Check(_completedSkipped == _expectSkip, $"skipped = {_expectSkip}");
        report.Check(PlayerPrefs.GetInt(SaveKeys.TutorialDone, 0) == 1, "TutorialDone set");
        if (!_expectSkip)
            report.Check(_shots == 5, $"every lesson passed first try ({_shots} shots for 5 lessons)");
        _log.Insert(0, report + "\n");
        Stop();
        _done = true;
    }

    // Every one-time hint (v3 V9) fires on its trigger, once: raised through the real events (run it in the Menu
    // scene, where no match views listen to the fake match events).
    public static string Hints()
    {
        var report = new CheckReport("TutorialChecks.Hints");
        if (!EditorApplication.isPlaying || HintService.Instance == null)
            return "TutorialChecks.Hints: enter Play mode first";
        MatchSetup.Mode = MatchMode.VsBot;
        int marks = 0, toasts = 0;
        System.Action<CoachTarget, string> onMark = (_, _) => marks++;
        System.Action<ToastMessage> onToast = _ => toasts++;
        GameEvents.OnCoachMarkRequested += onMark;
        GameEvents.OnToastRequested += onToast;
        int backup = PlayerPrefs.GetInt(SaveKeys.HintsShown, 0);
        try
        {
            var human = new TurnInfo { IsPlayer = true, IsHuman = true };
            var cases = new (HintId hint, System.Action trigger)[]
            {
                (HintId.BallInHand, () => GameEvents.RaiseTurnStarted(new TurnInfo { IsPlayer = true, IsHuman = true, BallInHand = true })),
                (HintId.EightOnly, () => GameEvents.RaiseTurnStarted(new TurnInfo { IsPlayer = true, IsHuman = true, PlayerOnEight = true })),
                (HintId.Groups, () => GameEvents.RaiseGroupsAssigned(BallGroup.Solids, BallGroup.Stripes)),
                (HintId.GreenRelease, () =>
                {
                    GameEvents.RaiseShotFired(new ShotFeedback { IsPlayer = true, Zone = ReleaseZone.Red, Power01 = 0.4f });
                    GameEvents.RaiseShotFired(new ShotFeedback { IsPlayer = true, Zone = ReleaseZone.Red, Power01 = 0.4f });
                    GameEvents.RaiseTurnStarted(human);
                }),
                (HintId.FineAim, () =>
                {
                    for (int i = 0; i < 5; i++)
                        GameEvents.RaiseShotFired(new ShotFeedback { IsPlayer = true, Zone = ReleaseZone.Green, Power01 = 0.4f });
                    GameEvents.RaiseTurnStarted(human);
                }),
                (HintId.PowerMiss, () =>
                {
                    GameEvents.RaiseShotFired(new ShotFeedback { IsPlayer = true, Zone = ReleaseZone.Green, Power01 = 0.95f });
                    GameEvents.RaiseTurnStarted(new TurnInfo { IsPlayer = false, IsHuman = false });
                }),
                (HintId.FreeLook, () => GameEvents.RaiseTurnStarted(new TurnInfo { IsPlayer = false, IsHuman = false })),
                (HintId.LookMode, () => GameEvents.RaiseLookModeToggled(true)),
                (HintId.StarLost, () => GameEvents.RaiseMatchEnded(new MatchResult { StarsGained = -1 })),
                (HintId.ClaimObjective, () =>
                {
                    var result = new MatchResult { Won = true, StarsGained = 1 };
                    result.ObjectivesCompleted.Add(new ObjectiveViewData { Text = "Win a match", Target = 1, Progress = 1 });
                    GameEvents.RaiseMatchEnded(result);
                }),
                (HintId.MenuRank, () => HintService.ShowOnce(HintId.MenuRank, CoachTarget.RankBadge, "test")),
                (HintId.MenuObjectives, () => HintService.ShowOnce(HintId.MenuObjectives, CoachTarget.Objectives, "test"))
            };
            foreach (var (hint, trigger) in cases)
            {
                PlayerPrefs.SetInt(SaveKeys.HintsShown, 0);
                GameEvents.RaiseMatchStarted(new MatchStartInfo());
                marks = toasts = 0;
                trigger();
                bool shown = ProfileService.Instance.WasHintShown(hint);
                PlayerPrefs.SetInt(SaveKeys.HintsShown, -1);
                GameEvents.RaiseMatchStarted(new MatchStartInfo());
                int before = marks;
                trigger();
                // Over the results the hint is a toast a moment later; the profile bit says it was due.
                bool delayed = hint is HintId.StarLost or HintId.ClaimObjective;
                report.Check(shown && (delayed || marks + toasts >= 1), $"{hint}: shown on its trigger");
                report.Check(marks == before, $"{hint}: not shown again");
            }
            // Never in the tutorial.
            PlayerPrefs.SetInt(SaveKeys.HintsShown, 0);
            MatchSetup.Mode = MatchMode.Tutorial;
            GameEvents.RaiseTurnStarted(new TurnInfo { IsPlayer = true, IsHuman = true, BallInHand = true });
            report.Check(!ProfileService.Instance.WasHintShown(HintId.BallInHand), "no hints in the tutorial");
        }
        finally
        {
            MatchSetup.Mode = MatchMode.VsBot;
            GameEvents.OnCoachMarkRequested -= onMark;
            GameEvents.OnToastRequested -= onToast;
            PlayerPrefs.SetInt(SaveKeys.HintsShown, backup);
        }
        return report.ToString();
    }

    private static void Stop()
    {
        _running = false;
        GameEvents.OnTutorialStep -= HandleStep;
        GameEvents.OnTutorialCompleted -= HandleCompleted;
        EditorApplication.update -= Tick;
    }
}
