using System.Text;
using UnityEditor;
using UnityEngine;

// v4 V10 in Play mode (Match scene open): RunThrough plays every lesson with a perfect release on the right line
// (placing the cue ball in hand first) and expects OnTutorialCompleted(false) with the profile flag set; Skip expects
// OnTutorialCompleted(true). Both run over frames: call Status() until it says done.
public static class TutorialChecks
{
    private static readonly float[] Pulls = { 0.4f, 0.4f, 0.6f };
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
    private static bool _triedCentre;
    private static int _blocked;

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
        _triedCentre = false;
        _blocked = 0;
        _started = EditorApplication.timeSinceStartup;
        _waitUntil = _started + 1.0;
        PlayerPrefs.SetInt(SaveKeys.TutorialDone, 0);
        GameEvents.OnTutorialStep += HandleStep;
        GameEvents.OnTutorialCompleted += HandleCompleted;
        GameEvents.OnTutorialShotBlocked += HandleBlocked;
        EditorApplication.update += Tick;
        _log.AppendLine(MatchController.DebugStartTutorial());
        if (skip)
            GameEvents.RaiseTutorialSkipRequested();
        return "started";
    }

    private static void HandleBlocked() => _blocked++;

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
        {
            // The first try is in the centre: the lesson must hold the shot back.
            if (!_triedCentre)
            {
                _triedCentre = true;
                player.DebugSetSpin(Vector2.zero);
                player.DebugReleaseAt(0f, pull);
                _waitUntil = now + 1.0;
                return;
            }
            player.DebugSetSpin(new Vector2(0f, -0.4f));
        }
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
        if (!_expectSkip)
            report.Check(_blocked == 1, $"the spin lesson held back a shot from the centre ({_blocked} time)");
        if (_expectMoveOn)
            report.Check(_shots == 5, $"lesson 1 moved on after 3 misses, lessons 2 and 3 passed first try ({_shots} shots, want 5)");
        else if (!_expectSkip)
            report.Check(_shots == 3, $"every lesson passed first try ({_shots} shots for 3 lessons)");
        _log.Insert(0, report + "\n");
        Stop();
        _done = true;
    }

    // The lessons' tables headless (v4 V10.1): every cue ball stands on the cloth and a perfect shot at the lesson's
    // power drops the ball; in the Spin lesson a centre hit lets the cue ball follow in (the reason for the lesson),
    // a low hit keeps it out, and any off-centre hit mostly does (average player: sigma 45 ms, the easy bar, 0.15 deg).
    public static string Layouts(int samples = 300)
    {
        var report = new CheckReport("TutorialChecks.Layouts");
        MatchConfig config = PhysicsChecks.Config;
        var tutorial = AssetDatabase.LoadAssetAtPath<TutorialConfig>(DataAssetBuilder.TutorialConfigPath);
        PoolSettings settings = config.ToPoolSettings();
        var geometry = new TableGeometry(settings);
        var physics = new PoolPhysics(geometry, settings) { RecordCollisions = true };
        CueDefinition house = PhysicsChecks.Cues.Starter;
        PowerCurve curve = config.PowerCurve(house.MaxSpeed);
        ShotAccuracy.PowerScaling scaling = config.ReleaseScaling();
        var easy = config.AccuracySettings(house.GreenHalf * tutorial.EasyGreenScale, house.YellowWidth, house.OscillationPeriod * tutorial.EasyPeriodScale);
        report.Check(tutorial.Steps.Count == 3 && tutorial.Steps[0].AimFirst && tutorial.Steps[2].Kind == TutorialStepKind.Spin && tutorial.Steps[2].RequireSpin, "three lessons: aim first, release, spin that holds the shot");
        for (int i = 0; i < tutorial.Steps.Count; i++)
        {
            TutorialStep step = tutorial.Steps[i];
            TutorialController.Layout(step, geometry, out Vector2 objectBall, out Vector2 cueBall, out Vector2 ghost);
            report.Check(geometry.IsInsidePlayArea(cueBall), $"lesson {i + 1}: the cue ball stands on the cloth ({cueBall.x:F2}, {cueBall.y:F2})");
            var balls = new BallState[BallGroups.BallCount];
            for (int b = 0; b < balls.Length; b++)
            {
                balls[b].Number = b;
                balls[b].Pocketed = b > 1;
            }
            balls[0].Pos = cueBall;
            balls[1].Pos = objectBall;
            Vector2 aim = (ghost - cueBall).normalized;
            var random = new System.Random(10 + i);
            int[] ok = new int[3], scratch = new int[3];
            for (int mode = 0; mode < 3; mode++)
            for (int n = 0; n < samples; n++)
            {
                float pull = 0.5f + 0.2f * (float)random.NextDouble();
                if (step.Kind != TutorialStepKind.Spin)
                    pull = 0.4f;
                // 0: the centre, 1: low, 2: off the centre any way
                Vector2 tip = mode == 0 ? Vector2.zero : mode == 1 ? new Vector2(0f, -0.25f - 0.25f * (float)random.NextDouble())
                    : new Vector2(0.15f + 0.35f * (float)random.NextDouble(), 0.3f * ((float)random.NextDouble() - 0.5f));
                ShotAccuracy.Settings window = ShotAccuracy.AtPower(step.Assist == ReleaseAssistMode.Easy ? easy : config.AccuracySettings(house.GreenHalf, house.YellowWidth, house.OscillationPeriod), scaling, pull);
                float position = step.Assist == ReleaseAssistMode.AlwaysGreen ? 0f : ShotAccuracy.PositionFromTimingError(ShotAccuracy.Gaussian(random) * 0.045f, window.Period);
                ShotAccuracy.Outcome release = ShotAccuracy.Evaluate(position, window, random);
                Vector2 direction = ShotAccuracy.Rotate(aim, release.AngleErrorDeg + ShotAccuracy.Gaussian(random) * 0.15f);
                physics.SetBalls(balls);
                physics.BeginShot(new ShotParams(direction, Mathf.Min(curve.SpeedFromPull(pull) * release.PowerFactor, curve.MaxSpeed), tip, 0f));
                for (int k = 0; k < 20000 && physics.IsMoving; k++)
                    physics.Step();
                bool pot = physics.Balls[1].Pocketed && physics.Balls[1].PocketIndex == step.Pocket;
                if (physics.Balls[0].Pocketed)
                    scratch[mode]++;
                if (pot && !physics.Balls[0].Pocketed)
                    ok[mode]++;
                if (step.Kind != TutorialStepKind.Spin)
                    break;
            }
            if (step.Kind != TutorialStepKind.Spin)
            {
                report.Check(ok[0] == 1, $"lesson {i + 1}: a perfect shot at the lesson's power drops the ball");
                continue;
            }
            report.Info($"spin lesson: centre -> dropped, cue ball safe {ok[0] / (float)samples:P0}, cue ball in {scratch[0] / (float)samples:P0}; low {ok[1] / (float)samples:P0} (in {scratch[1] / (float)samples:P0}); off centre {ok[2] / (float)samples:P0} (in {scratch[2] / (float)samples:P0})");
            report.Check(scratch[0] >= 0.7f * samples, $"centre hit: the cue ball follows it in in >= 70% ({scratch[0] / (float)samples:P0})");
            report.Check(ok[1] >= 0.9f * samples, $"low hit: dropped and the cue ball stays out in >= 90% ({ok[1] / (float)samples:P0})");
            report.Check(ok[2] >= 0.5f * samples, $"any off-centre hit: dropped and the cue ball stays out in >= 50% ({ok[2] / (float)samples:P0})");
        }
        return report.ToString();
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
        GameEvents.OnTutorialShotBlocked -= HandleBlocked;
        EditorApplication.update -= Tick;
    }
}
