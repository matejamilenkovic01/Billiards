using System.Collections.Generic;
using System.Text;
using UnityEngine;

// v4 V9.1, Play mode, Match scene, the player aiming: the camera holds still while the cue is pulled and while the spin
// changes, the pinch stays inside its range, the rise after the shot is smooth. Start(), then Result() after ~14 s.
public static class AimFeelChecks
{
    private struct Pose
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private static readonly Dictionary<string, Pose> _poses = new();
    private static readonly List<(float time, Quaternion rotation, Vector3 position, CameraMode mode)> _rise = new();
    private static readonly StringBuilder _notes = new();
    private static float _zoomMin = float.MaxValue, _zoomMax = float.MinValue, _panMax;
    private static float _riseStart = -1f;
    private static bool _sampling;
    private static float _zoomAfterShot = -1f;

    public static string Start()
    {
        MatchController match = MatchController.Instance;
        if (match == null || !Application.isPlaying)
            return "needs Play mode in the Match scene";
        Application.runInBackground = true;
        PopupManager.Instance.HideAllImmediate();
        _poses.Clear();
        _rise.Clear();
        _notes.Clear();
        _zoomMin = float.MaxValue;
        _zoomMax = float.MinValue;
        _panMax = 0f;
        _riseStart = -1f;
        _zoomAfterShot = -1f;

        MatchController.DebugSetRules(RuleSetId.Standard);
        MatchController.DebugStart(true, null, 8);
        // The VS intro and the coin toss come first: the table is set up once the turn is on.
        const float Base = 4.5f;
        void At(float t, System.Action action) => DG.Tweening.DOVirtual.DelayedCall(Base + t, () => action(), true);
        var player = Object.FindFirstObjectByType<PlayerShotController>();
        CameraRig rig = match.CameraRig;
        DG.Tweening.DOVirtual.DelayedCall(Base - 1f, () =>
        {
            MatchController.DebugSetupTable(BallGroup.Solids, new[] { 0, 1, 11, 12 }, SetupPositions(match, out Vector2 aimPoint, out Vector2 cuePos));
            player.DebugSetAim((aimPoint - cuePos).normalized);
        }, true);
        void Snap(string name) => _poses[name] = new Pose { Position = rig.transform.position, Rotation = rig.transform.rotation };

        At(2.0f, () => Snap("rest"));
        // The spin changes (and goes back): the camera must not move.
        At(2.1f, () => player.DebugSetSpin(new Vector2(0.45f, 0.3f)));
        At(2.7f, () => Snap("spin"));
        At(2.8f, () => player.DebugSetSpin(Vector2.zero));
        At(3.4f, () => Snap("spin_reset"));
        // The pinch: further and closer than any sane framing, then a big slide.
        for (int i = 0; i < 12; i++)
            At(3.5f + i * 0.05f, () => GameEvents.RaiseOrbitPinch(2f));
        At(4.3f, () => { _zoomMin = Mathf.Min(_zoomMin, rig.AimDistanceNow + rig.AimZoom); });
        for (int i = 0; i < 20; i++)
            At(4.4f + i * 0.05f, () => GameEvents.RaiseOrbitPinch(0.4f));
        At(5.7f, () => { _zoomMax = Mathf.Max(_zoomMax, rig.AimDistanceNow + rig.AimZoom); });
        for (int i = 0; i < 10; i++)
            At(5.8f + i * 0.05f, () => GameEvents.RaiseOrbitPan(new Vector2(-900f, 0f)));
        At(6.5f, () => { _panMax = Mathf.Max(_panMax, Mathf.Abs(rig.AimPan)); });
        for (int i = 0; i < 10; i++)
            At(6.6f + i * 0.05f, () => GameEvents.RaiseOrbitPan(new Vector2(900f, 0f)));
        At(7.3f, () => { _panMax = Mathf.Max(_panMax, Mathf.Abs(rig.AimPan)); });
        // Back to the framing, then the cue is pulled: weak, medium, hard. The camera stays exactly where it was.
        for (int i = 0; i < 40; i++)
            At(7.4f + i * 0.02f, () => GameEvents.RaiseOrbitPinch(1.05f));
        At(8.6f, () => { GameEvents.RaisePullChanged(0.12f); });
        At(9.2f, () => Snap("pull_weak"));
        At(9.25f, () => GameEvents.RaisePullChanged(0.5f));
        At(9.8f, () => Snap("pull_medium"));
        At(9.85f, () => GameEvents.RaisePullChanged(1f));
        At(10.4f, () => Snap("pull_hard"));
        // Fire and sample the rise.
        At(10.5f, () =>
        {
            _riseStart = Time.unscaledTime;
            _sampling = true;
            player.DebugReleaseAt(0f, 0.5f);
        });
        At(10.5f + 6f, () =>
        {
            _sampling = false;
            _zoomAfterShot = Mathf.Abs(rig.AimZoom) + Mathf.Abs(rig.AimPan);
        });
        DG.Tweening.TweenSettingsExtensions.SetUpdate(DG.Tweening.DOVirtual.Float(0f, 1f, 40f, _ =>
        {
            if (_sampling)
                _rise.Add((Time.unscaledTime - _riseStart, rig.transform.rotation, rig.transform.position, rig.Mode));
        }), true);
        return "started";
    }

    private static Vector2[] SetupPositions(MatchController match, out Vector2 aimPoint, out Vector2 cue)
    {
        Vector2 pocket = match.Geometry.Pockets[4].Center;
        Vector2 toTable = (-pocket).normalized;
        Vector2 ball = pocket + toTable * 0.9f;
        cue = ball + toTable * 0.5f + new Vector2(0.12f, 0f);
        aimPoint = ball;
        return new[] { cue, ball, new Vector2(-0.35f, -0.7f), new Vector2(0.3f, -0.7f) };
    }

    private static string Delta(string a, string b, out float mm, out float deg)
    {
        mm = Vector3.Distance(_poses[a].Position, _poses[b].Position) * 1000f;
        deg = Quaternion.Angle(_poses[a].Rotation, _poses[b].Rotation);
        return $"{a} -> {b}: {mm:F2} mm, {deg:F3} deg";
    }

    public static string Result()
    {
        var report = new CheckReport("AimFeelChecks");
        MatchConfig config = PhysicsChecks.Config;
        if (!_poses.ContainsKey("pull_hard") || _zoomAfterShot < 0f)
            return "not finished yet";

        Delta("pull_weak", "pull_medium", out float m1, out float d1);
        Delta("pull_medium", "pull_hard", out float m2, out float d2);
        report.Info($"pull weak -> medium: {m1:F2} mm, {d1:F3} deg; medium -> hard: {m2:F2} mm, {d2:F3} deg");
        report.Check(m1 < 0.5f && d1 < 0.05f && m2 < 0.5f && d2 < 0.05f, "the camera does not move while the cue is pulled harder (< 0.5 mm, < 0.05 deg)");
        Delta("rest", "spin", out float m3, out float d3);
        Delta("rest", "spin_reset", out float m4, out float d4);
        report.Info($"spin on: {m3:F2} mm, {d3:F3} deg; spin reset: {m4:F2} mm, {d4:F3} deg (the head's sway is a little)");
        report.Check(m3 < 4f && d3 < 0.3f && m4 < 4f && d4 < 0.3f, "changing or resetting the spin does not move the camera (only the breathing sway, < 4 mm)");

        report.Info($"zoom: closest {_zoomMin:F2} m, furthest {_zoomMax:F2} m behind the cue ball, slide up to {_panMax:F2} m");
        report.Check(_zoomMin >= config.AimZoomMinDistance - 0.01f && _zoomMax <= config.AimZoomMaxDistance + 0.01f, $"the pinch stays in {config.AimZoomMinDistance:F2}..{config.AimZoomMaxDistance:F2} m");
        report.Check(_zoomMax - _zoomMin > 0.5f, $"the pinch has a real range ({_zoomMax - _zoomMin:F2} m)");
        report.Check(_panMax > 0.05f && _panMax <= config.AimPanMax + 0.01f, $"the slide stays within {config.AimPanMax:F2} m ({_panMax:F2} m)");
        report.Check(_zoomAfterShot < 0.001f, $"zoom and slide are reset after the shot ({_zoomAfterShot:F3})");

        // The rise: the fastest turn, the fastest change of turn, the way home.
        float peak = 0f, peakAccel = 0f, previousRate = 0f, reached = -1f;
        float frame = _rise.Count > 1 ? _rise[_rise.Count - 1].time / (_rise.Count - 1) : 0f;
        for (int i = 1; i < _rise.Count; i++)
        {
            float dt = Mathf.Max(1e-3f, _rise[i].time - _rise[i - 1].time);
            float rate = Quaternion.Angle(_rise[i - 1].rotation, _rise[i].rotation) / dt;
            if (_rise[i].time > config.ShotHoldTime + 0.3f && _rise[i].mode == CameraMode.Watch && reached < 0f && rate < 3f)
                reached = _rise[i].time;
            if (_rise[i].time > config.ShotHoldTime + 0.1f && (reached < 0f || _rise[i].time < reached))
            {
                peak = Mathf.Max(peak, rate);
                peakAccel = Mathf.Max(peakAccel, Mathf.Abs(rate - previousRate) / dt);
            }
            previousRate = rate;
        }
        report.Info($"rise: {_rise.Count} samples ({frame * 1000f:F0} ms each), turn peak {peak:F1} deg/s, peak change {peakAccel:F0} deg/s^2, settled at {reached:F2} s");
        report.Check(peak <= config.MaxCameraTurnDegPerSec * 1.3f + 5f, $"the rise never turns faster than ~{config.MaxCameraTurnDegPerSec:F0} deg/s ({peak:F1})");
        report.Check(reached > 0f && reached <= config.ShotHoldTime + config.ShotRiseMaxTime + 0.8f, $"the camera settles in Watch ({reached:F2} s)");
        return report.ToString();
    }
}
