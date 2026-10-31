using System.Text;
using UnityEditor;
using UnityEngine;

// v4 V10 in Play mode (Match scene open): RunThrough plays every lesson with a perfect release on the right line
// (placing the cue ball in hand first) and expects OnTutorialCompleted(false) with the profile flag set; Skip expects
// OnTutorialCompleted(true). Both run over frames: call Status() until it says done.
public static class TutorialChecks
{
    private static readonly float[] Pulls = { 0.4f, 0.4f, 0.68f };
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
    private static bool _expectMoveOn;

    public static string RunThrough() => Start(false);

    public static string Skip() => Start(true);

    // Lesson 1 shot too softly every time: it must go on after MoveOnAfter (3) tries, and the lessons after it still run.
    public static string MoveOn() => Start(false, true);

    public static string Status()
    {
        if (_log == null)
            return "not started";
        return (_done ? "DONE\n" : "running\n") + _log;
    }

    private static string Start(bool skip, bool moveOn = false)
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
        _expectMoveOn = moveOn;
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
        // MoveOn: the first lesson is shot too softly every time; the check expects it to give up after three tries.
        if (_expectMoveOn && step == 0)
            pull = 0.1f;
        if (lesson.Kind == TutorialStepKind.Spin)
            player.DebugSetSpin(new Vector2(0f, -0.5f));
        player.DebugReleaseAt(0f, pull);
        _shots++;
        _lastShotStep = step;
        _log.AppendLine($"  shot {_shots}: lesson {step + 1} pull {pull:P0}");
        _waitUntil = now + 1.0;
    }

    private static void Finish()
    {
        var report = new CheckReport(_expectSkip ? "TutorialChecks.Skip" : _expectMoveOn ? "TutorialChecks.MoveOn" : "TutorialChecks.RunThrough");
        report.Check(_completedSkipped.HasValue, "OnTutorialCompleted raised");
        report.Check(_completedSkipped == _expectSkip, $"skipped = {_expectSkip}");
        report.Check(PlayerPrefs.GetInt(SaveKeys.TutorialDone, 0) == 1, "TutorialDone set");
        if (_expectMoveOn)
            report.Check(_shots == 5, $"lesson 1 moved on after 3 misses, lessons 2 and 3 passed first try ({_shots} shots, want 5)");
        else if (!_expectSkip)
            report.Check(_shots == 3, $"every lesson passed first try ({_shots} shots for 3 lessons)");
        _log.Insert(0, report + "\n");
        Stop();
        _done = true;
    }

    // T3 headless (v4 V10): the cue ball comes to rest in the gold circle in >= 70% of shots with the marker in the lower
    // fifth (b -0.4..-0.5), an average player (sigma 45 ms, the lesson's easy bar) who pulls harder (62-74%), 0.15 deg aim noise;
    // and a shot with no spin does not (so the lesson teaches something). Static physics, no Play mode needed.
    public static string DrawReach(int samples = 400)
    {
        var report = new CheckReport("TutorialChecks.DrawReach");
        MatchConfig config = PhysicsChecks.Config;
        var tutorial = AssetDatabase.LoadAssetAtPath<TutorialConfig>(DataAssetBuilder.TutorialConfigPath);
        PoolSettings settings = config.ToPoolSettings();
        var geometry = new TableGeometry(settings);
        var physics = new PoolPhysics(geometry, settings) { RecordCollisions = true };
        TutorialStep step = tutorial.Steps[tutorial.Steps.Count - 1];
        report.Check(step.Kind == TutorialStepKind.Spin && step.CircleBehind > 0f, "the last lesson is the spin lesson with a circle");
        TutorialController.Layout(step, geometry, out Vector2 objectBall, out Vector2 cueBall, out Vector2 ghost);
        Vector2 aim = (ghost - cueBall).normalized;
        Vector2 circle = cueBall - aim * step.CircleBehind;
        CueDefinition house = Cues(config);
        var balls = new BallState[BallGroups.BallCount];
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].Number = i;
            balls[i].Pocketed = i > 1;
        }
        balls[0].Pos = cueBall;
        balls[1].Pos = objectBall;
        ShotAccuracy.PowerScaling scaling = config.ReleaseScaling();
        PowerCurve curve = config.PowerCurve(house.MaxSpeed);
        var cue = config.AccuracySettings(house.GreenHalf * tutorial.EasyGreenScale, house.YellowWidth, house.OscillationPeriod * tutorial.EasyPeriodScale);
        var random = new System.Random(10);
        int[] ok = new int[2], potted = new int[2];
        var distances = new System.Collections.Generic.List<float>();
        for (int mode = 0; mode < 2; mode++)
        for (int s = 0; s < samples; s++)
        {
            float pull = 0.62f + 0.12f * (float)random.NextDouble();
            float tipY = mode == 0 ? -0.4f - 0.1f * (float)random.NextDouble() : 0f;
            ShotAccuracy.Settings window = ShotAccuracy.AtPower(cue, scaling, pull);
            float position = ShotAccuracy.PositionFromTimingError(ShotAccuracy.Gaussian(random) * 0.045f, window.Period);
            ShotAccuracy.Outcome release = ShotAccuracy.Evaluate(position, window, random);
            Vector2 direction = ShotAccuracy.Rotate(aim, release.AngleErrorDeg + ShotAccuracy.Gaussian(random) * 0.15f);
            float speed = Mathf.Min(curve.SpeedFromPull(pull) * release.PowerFactor, curve.MaxSpeed);
            physics.SetBalls(balls);
            physics.BeginShot(new ShotParams(direction, speed, new Vector2(0f, tipY), 0f));
            for (int step2 = 0; step2 < 20000 && physics.IsMoving; step2++)
                physics.Step();
            bool pot = physics.Balls[1].Pocketed && physics.Balls[1].PocketIndex == step.Pocket;
            float distance = (physics.Balls[0].Pos - circle).magnitude;
            if (pot)
                potted[mode]++;
            if (pot && !physics.Balls[0].Pocketed && distance <= tutorial.CircleRadius)
                ok[mode]++;
            if (mode == 0 && pot && !physics.Balls[0].Pocketed)
                distances.Add(distance);
        }
        distances.Sort();
        report.Info($"circle {step.CircleBehind:F2} m behind the start, r {tutorial.CircleRadius:F2}; with draw: potted {potted[0] / (float)samples:P0}, in the circle {ok[0] / (float)samples:P0}; "
                    + (distances.Count > 0 ? $"distance to the centre: median {distances[distances.Count / 2]:F2} m, p10 {distances[distances.Count / 10]:F2}, p90 {distances[distances.Count * 9 / 10]:F2}" : "no pots"));
        report.Info($"without spin: potted {potted[1] / (float)samples:P0}, in the circle {ok[1] / (float)samples:P0}");
        report.Check(ok[0] >= 0.7f * samples, $"draw reaches the circle in >= 70% ({ok[0] / (float)samples:P0})");
        report.Check(ok[1] <= 0.1f * samples, $"no spin does not land in the circle ({ok[1] / (float)samples:P0})");
        return report.ToString();
    }

    private static CueDefinition Cues(MatchConfig config) => PhysicsChecks.Cues.Starter;

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
                (HintId.ScratchFollow, () =>
                {
                    GameEvents.RaiseShotFired(new ShotFeedback { IsPlayer = true, Zone = ReleaseZone.Green, Power01 = 0.4f, Tip = new Vector2(0f, 0.5f) });
                    GameEvents.RaiseFoul(new FoulInfo { Foul = FoulType.CueBallPocketed, ByPlayer = true });
                }),
                (HintId.CueGap, () => HintService.ShowOnce(HintId.CueGap, CoachTarget.Cues, "test")),
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
