using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// v4 V9, TRY: from the Menu scene in Play mode, asks for a try of a cue (the real event), then plays the three shots
// through DebugReleaseAt (RETRY / NEXT through their events) until the comparison screen, and checks the profile is
// exactly as it was (every ProfileKeys value, ObjectiveProgress and ShotsTaken too). Run Start(), then call Status().
public static class CueTryChecks
{
    private static StringBuilder _log;
    private static bool _running, _done;
    private static string _cueId, _before;
    private static double _started, _waitUntil;
    private static int _stage, _shotsFired, _placedFor = -1, _finishedShots = -1, _finishedPerfect;
    private static string _screenshotPrefix;
    private static bool _captured;

    public static string Start(string cueId, string screenshotPrefix = "")
    {
        if (!EditorApplication.isPlaying || ProfileService.Instance == null)
            return "needs Play mode in the Menu scene";
        _log = new StringBuilder();
        _cueId = cueId;
        _screenshotPrefix = screenshotPrefix;
        _before = Snapshot();
        _stage = 0;
        _shotsFired = 0;
        _placedFor = -1;
        _captured = false;
        _finishedShots = -1;
        _running = true;
        _done = false;
        _started = EditorApplication.timeSinceStartup;
        _waitUntil = _started + 0.5;
        GameEvents.OnCueTryFinished += HandleFinished;
        EditorApplication.update += Tick;
        GameEvents.RaiseCueTryRequested(cueId);
        return $"try of {cueId} requested";
    }

    public static string Status() => _log == null ? "not started" : (_done ? "DONE\n" : "running\n") + _log;

    private static string Snapshot()
    {
        ProfileService profile = ProfileService.Instance;
        var sb = new StringBuilder();
        foreach (string key in profile.ProfileKeys().OrderBy(k => k))
            sb.Append($"{key}={PlayerPrefs.GetString(key, PlayerPrefs.GetInt(key, int.MinValue).ToString())};");
        return sb.ToString();
    }

    private static void HandleFinished(string cueId, int shots, int perfect)
    {
        _finishedShots = shots;
        _finishedPerfect = perfect;
        _log.AppendLine($"OnCueTryFinished({cueId}, {shots}, {perfect})");
    }

    private static void Capture(string name)
    {
        if (!string.IsNullOrEmpty(_screenshotPrefix))
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath($"Screenshots/{_screenshotPrefix}{name}.png"));
    }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (!_running || now < _waitUntil)
            return;
        if (now - _started > 180 || !EditorApplication.isPlaying)
        {
            Finish();
            return;
        }

        MatchController match = MatchController.Instance;
        var practice = Object.FindFirstObjectByType<PracticeController>();
        if (match == null || practice == null || !practice.IsActive && _stage < 3)
            return;
        var player = Object.FindFirstObjectByType<PlayerShotController>();

        switch (_stage)
        {
            case 0:   // the match is in practice, with that cue, and the player to aim
                if (match.Phase != MatchPhase.Turn)
                    return;
                _log.AppendLine($"practice: cue {match.PlayerCue.Id}, mode {MatchSetup.Mode}, step {practice.Step + 1}");
                _stage = 1;
                _waitUntil = now + 1.5;
                return;
            case 1:   // shoot the current step
                if (match.Phase != MatchPhase.Turn)
                    return;
                if (_shotsFired == 0 && !_captured)
                {
                    _captured = true;
                    Capture("try_shot1");
                    _waitUntil = now + 0.8;
                    return;
                }
                if (match.Rules.BallInHand && _placedFor != _shotsFired)
                {
                    _placedFor = _shotsFired;
                    match.PlaceCueBall(RackBuilder.DefaultCueBallPosition(match.Geometry));
                    GameEvents.RaisePlaceCueBallConfirmed();
                    _waitUntil = now + 0.6;
                    return;
                }
                player.DebugReleaseAt(0f, 0.55f);
                _shotsFired++;
                _log.AppendLine($"  shot {_shotsFired}");
                _stage = 2;
                _waitUntil = now + 1.0;
                return;
            case 2:   // the shot is over (Resolving): NEXT; the last NEXT ends the try
                if (match.Phase != MatchPhase.Resolving)
                    return;
                _stage = _shotsFired >= 3 ? 3 : 1;
                _waitUntil = now + 0.8;
                GameEvents.RaisePracticeNextRequested();
                return;
            case 3:   // the comparison screen
                var popup = Object.FindFirstObjectByType<CueTryResultPopup>();
                if (popup == null || !popup.IsShown)
                    return;
                _log.AppendLine("comparison screen shown");
                _stage = 4;
                _waitUntil = now + 1.2;
                return;
            case 4:
                Capture("try_compare");
                _stage = 5;
                _waitUntil = now + 1.0;
                return;
            default:
                Finish();
                return;
        }
    }

    private static void Finish()
    {
        _running = false;
        EditorApplication.update -= Tick;
        GameEvents.OnCueTryFinished -= HandleFinished;
        var report = new CheckReport("CueTryChecks");
        report.Check(_shotsFired == 3, $"three try shots played ({_shotsFired})");
        report.Check(_finishedShots == 3, $"OnCueTryFinished with 3 shots ({_finishedShots}, {_finishedPerfect} perfect)");
        string after = Snapshot();
        report.Check(after == _before, "the profile is exactly as before" + (after == _before ? "" : $"\n  before {_before}\n  after  {after}"));
        _log.Insert(0, report + "\n");
        _done = true;
    }
}
