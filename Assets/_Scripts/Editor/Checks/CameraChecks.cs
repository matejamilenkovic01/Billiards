using DG.Tweening;
using UnityEngine;

// Gameplay redesign G1 / G5: what the watch and ball-in-hand views must show, on several phone aspects.
public static class CameraChecks
{
    private static readonly (string name, float aspect)[] Aspects = { ("9:16", 9f / 16f), ("9:19.5", 9f / 19.5f), ("9:21", 9f / 21f) };

    public static string All() => WatchFitsAllPockets() + "\n" + PlaceFitsArea() + "\n" + OrbitBounds();

    // v3 V2: random gesture sequences (orbit, pinch, pan, with extreme sizes) through the same functions the rig
    // uses; after every gesture the camera is above the rail, out of the table and every ball, the pivot in bounds.
    // Also the corner view: from a corner along the short rail, both of its pockets in frame.
    public static string OrbitBounds(int sequences = 1000)
    {
        var report = new CheckReport("CameraChecks.OrbitBounds");
        MatchConfig config = PhysicsChecks.Config;
        var geometry = new TableGeometry(config.ToPoolSettings());
        var origin = new Vector3(0f, config.TableHeight, 0f);
        var random = new System.Random(77);
        float rail = config.RailTop + config.OrbitRailClearance;
        float frameX = geometry.HalfWidth + config.CushionDepth + config.RailWidth;
        float frameZ = geometry.HalfLength + config.CushionDepth + config.RailWidth;
        int gestures = 0, low = 0, inTable = 0, inBall = 0, pivotOut = 0;
        float lowest = float.MaxValue;
        BallState[] balls = RackBuilder.Build(random, geometry, config.RackGap);
        float R() => (float)random.NextDouble();

        for (int sequence = 0; sequence < sequences; sequence++)
        {
            var state = new OrbitState
            {
                Pivot = origin + new Vector3((R() * 2f - 1f) * geometry.HalfWidth, 0f, (R() * 2f - 1f) * geometry.HalfLength),
                Yaw = R() * 360f - 180f,
                Pitch = Mathf.Lerp(-20f, 100f, R()),
                Distance = Mathf.Lerp(0.05f, 6f, R())
            };
            CameraRig.ConstrainOrbit(ref state, config, geometry, origin);
            for (int g = 0; g < 20; g++)
            {
                float extreme = R() < 0.2f ? 6f : 1f;
                switch (random.Next(3))
                {
                    case 0: CameraRig.ApplyOrbitDrag(ref state, new Vector2(R() * 2f - 1f, R() * 2f - 1f) * 400f * extreme, config); break;
                    case 1: CameraRig.ApplyOrbitPinch(ref state, Mathf.Lerp(0.3f, 3f, R()) * (extreme > 1f ? 2f : 1f)); break;
                    default: CameraRig.ApplyOrbitPan(ref state, new Vector2(R() * 2f - 1f, R() * 2f - 1f) * 600f * extreme, config); break;
                }
                CameraRig.ConstrainOrbit(ref state, config, geometry, origin);
                gestures++;

                Vector3 local = CameraRig.OrbitPosition(state) - origin;
                lowest = Mathf.Min(lowest, local.y);
                bool overFrame = Mathf.Abs(local.x) <= frameX && Mathf.Abs(local.z) <= frameZ;
                if (local.y < rail - 1e-4f)
                    low++;
                if (overFrame && local.y < 2f * config.BallRadius + config.OrbitBallClearance - 1e-4f)
                    inTable++;
                for (int b = 0; b < balls.Length; b++)
                {
                    if (!balls[b].Pocketed && (local - new Vector3(balls[b].Pos.x, config.BallRadius, balls[b].Pos.y)).magnitude < config.BallRadius * 1.5f)
                        inBall++;
                }
                Vector3 pivot = state.Pivot - origin;
                if (Mathf.Abs(pivot.x) > geometry.HalfWidth + config.OrbitPivotMargin + 1e-4f || Mathf.Abs(pivot.z) > geometry.HalfLength + config.OrbitPivotMargin + 1e-4f || Mathf.Abs(pivot.y) > 1e-4f)
                    pivotOut++;
            }
        }
        report.Info($"{sequences} sequences, {gestures} gestures: lowest eye {lowest * 1000f:F0} mm above the cloth (rail + clearance {rail * 1000f:F0} mm)");
        report.Check(low == 0, $"never below the rail top + {config.OrbitRailClearance * 1000f:F0} mm ({low})");
        report.Check(inTable == 0, $"never in the table / over the frame below ball height + {config.OrbitBallClearance * 100f:F0} cm ({inTable})");
        report.Check(inBall == 0, $"never inside a ball ({inBall})");
        report.Check(pivotOut == 0, $"pivot on the cloth within the table + {config.OrbitPivotMargin:F1} m ({pivotOut})");

        // Corner view: pivot at pocket 1, looking along the short (head) rail from beyond pocket 0, pitch 15 degrees.
        using var scope = new TempObjectScope();
        Camera camera = NewCamera(config);
        camera.aspect = 9f / 19.5f;
        Vector3 pocket0 = CameraRig.PocketRim(config, geometry, origin, 0);
        Vector3 pocket1 = CameraRig.PocketRim(config, geometry, origin, 1);
        var corner = new OrbitState { Pivot = origin + new Vector3(geometry.Pockets[1].Center.x, 0f, geometry.Pockets[1].Center.y), Yaw = 90f, Pitch = 15f, Distance = 2.3f };
        CameraRig.ConstrainOrbit(ref corner, config, geometry, origin);
        Vector3 eye = CameraRig.OrbitPosition(corner);
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(corner.Pivot - eye, Vector3.up));
        Vector3 v0 = camera.WorldToViewportPoint(pocket0), v1 = camera.WorldToViewportPoint(pocket1);
        var inner = new Rect(0.05f, 0.05f, 0.9f, 0.9f);
        bool both = v0.z > 0f && v1.z > 0f && inner.Contains(v0) && inner.Contains(v1);
        report.Info($"corner view: pitch {corner.Pitch:F1}, eye {(eye - origin).y * 100f:F0} cm above the cloth; pocket 0 at {(Vector2)v0}, pocket 1 at {(Vector2)v1}");
        report.Check(both, "corner view along the short rail: pockets 0 and 1 both in frame (5% margin)");
        return report.ToString();
    }

    // Random cue ball positions and shot directions, for both shooters, on three aspects: the final watch pose
    // (no blend; eyes as far towards the action as allowed) keeps all six pocket rims inside WatchFitArea.
    // The largest billiard lamp (v4 V6, RoomPrefabBuilder: 4 shades, body 1.96 m) as a box, and LampView's rule.
    private static Bounds LampBox(Vector3 origin)
    {
        float bottom = RoomPrefabBuilder.LampBottomAboveCloth;
        return new Bounds(origin + new Vector3(0f, bottom + 0.6f, 0f), new Vector3(0.42f, 1.2f, 2.0f));
    }

    private static bool LampHides(Vector3 eye, Vector3 origin)
    {
        Bounds box = LampBox(origin);
        if (eye.y > origin.y + RoomPrefabBuilder.LampBottomAboveCloth - 0.1f)
            return true;
        Vector3 toCenter = origin - eye;
        return box.IntersectRay(new Ray(eye, toCenter), out float hit) && hit < toCenter.magnitude;
    }

    // v4 V7, Play mode, Match scene, the player aiming: a shot is fired and the camera's pose sampled every frame.
    // Start, then read ShotRiseResult after ~3 s: the time to the Watch pose and the fastest turn.
    private static readonly System.Collections.Generic.List<(float time, Quaternion rotation, bool blending)> _rise = new();
    private static float _riseStart;

    public static string ShotRiseStart(bool screenshots = true)
    {
        MatchController match = MatchController.Instance;
        _rise.Clear();
        _riseStart = Time.unscaledTime;
        var player = Object.FindFirstObjectByType<PlayerShotController>();
        player.DebugReleaseAt(0f, 0.4f);
        CameraRig rig = match.CameraRig;
        DG.Tweening.DOVirtual.Float(0f, 1f, 3f, _ => _rise.Add((Time.unscaledTime - _riseStart, rig.transform.rotation, rig.IsBlending || rig.Mode != CameraMode.Watch))).SetUpdate(true);
        foreach (float at in screenshots ? new[] { 0f, 0.4f, 0.8f, 1.4f, 2.0f } : new float[0])
        {
            int index = System.Array.IndexOf(new[] { 0f, 0.4f, 0.8f, 1.4f, 2.0f }, at);
            DG.Tweening.DOVirtual.DelayedCall(at, () => ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath($"Screenshots/v4_5_rise_{index}.png")), true);
        }
        return "shot fired";
    }

    public static string ShotRiseResult()
    {
        var report = new CheckReport("CameraChecks.ShotRise");
        MatchConfig config = PhysicsChecks.Config;
        float reached = -1f, worstExcess = 0f, worstAt = 0f;
        float frame = _rise.Count > 1 ? _rise[_rise.Count - 1].time / (_rise.Count - 1) : 0f;
        var turns = new System.Text.StringBuilder();
        for (int i = 1; i < _rise.Count; i++)
        {
            // In the Editor (~10 fps, hitches right after an eval) the samples' spacing and the camera's own frame
            // time differ: the allowance uses the larger of the two.
            float dt = Mathf.Max(_rise[i].time - _rise[i - 1].time, frame);
            float turn = Quaternion.Angle(_rise[i - 1].rotation, _rise[i].rotation);
            turns.Append($"{_rise[i].time:F2}:{turn:F1}{(_rise[i].blending ? "" : "w")} ");
            // Head sway adds a little; the allowance is the doc's 1 degree.
            float excess = turn - (config.MaxCameraTurnDegPerSec * dt + 1f);
            if (excess > worstExcess)
            {
                worstExcess = excess;
                worstAt = _rise[i].time;
            }
            if (reached < 0f && !_rise[i].blending)
                reached = _rise[i].time;
        }
        float expected = config.ShotHoldTime + config.ShotRiseTime;
        report.Info($"{_rise.Count} frames ({frame * 1000f:F0} ms each): Watch pose reached after {reached:F2} s (expected {expected:F2}), worst turn over the limit {worstExcess:F2} deg at {worstAt:F2} s");
        report.Info("turn per frame (s:deg, w = in Watch): " + turns);
        // Sampled in the tween update, before the camera's LateUpdate: up to two frames late.
        report.Check(reached > 0f && Mathf.Abs(reached - expected) <= 0.15f + 2f * frame, $"Watch pose reached at {expected:F2} +- 0.15 s (+ 2 frames of sampling, {2f * frame:F2} s)");
        report.Check(worstExcess <= 0f, "no turn step faster than MaxCameraTurnDegPerSec x dt + 1 deg");
        return report.ToString();
    }

    public static string WatchFitsAllPockets(int samples = 300)
    {
        var report = new CheckReport("CameraChecks.WatchFitsAllPockets");
        MatchConfig config = PhysicsChecks.Config;
        var geometry = new TableGeometry(config.ToPoolSettings());
        var origin = new Vector3(0f, config.TableHeight, 0f);
        using var scope = new TempObjectScope();
        Camera camera = NewCamera(config);

        foreach (var (name, aspect) in Aspects)
        {
            camera.aspect = aspect;
            var random = new System.Random(11);
            int passed = 0, total = 0, lampShown = 0, lampBlocks = 0;
            float minDistance = float.MaxValue, maxDistance = 0f, maxHeight = 0f;
            for (int i = 0; i < samples; i++)
            {
                Vector2 cue = new(Mathf.Lerp(-geometry.MaxX, geometry.MaxX, (float)random.NextDouble()),
                    Mathf.Lerp(-geometry.MaxY, geometry.MaxY, (float)random.NextDouble()));
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                Vector2 aim = new(Mathf.Cos(angle), Mathf.Sin(angle));
                foreach (bool player in new[] { true, false })
                {
                    // The player watches from the end their aim camera was at; the bot's end is behind its ball.
                    Vector2 eye = cue - aim * config.AimDistance;
                    float end = player ? (eye.y > 0f ? 1f : -1f) : Mathf.Abs(aim.y) > 0.25f ? -Mathf.Sign(aim.y) : (cue.y > 0f ? 1f : -1f);
                    Vector3 position = CameraRig.WatchPosition(camera, config, geometry, origin, end);
                    // The player's eyes follow the cue ball (anywhere it rolls); the bot's, its shot line.
                    Vector2 focusPoint = player
                        ? new Vector2(Mathf.Lerp(-geometry.MaxX, geometry.MaxX, (float)random.NextDouble()), Mathf.Lerp(-geometry.MaxY, geometry.MaxY, (float)random.NextDouble()))
                        : cue + aim * 0.5f;
                    Vector3 focus = origin + new Vector3(focusPoint.x, 0f, focusPoint.y);
                    Quaternion rotation = CameraRig.WatchRotation(camera, config, geometry, origin, position, focus);
                    camera.transform.SetPositionAndRotation(position, rotation);
                    total++;
                    if (CameraRig.PocketsFit(camera, config, geometry, origin, config.WatchFitArea))
                        passed++;
                    // v4 V6: where the lamp is still shown (not faded out), no pocket hides behind it.
                    if (!LampHides(position, origin))
                    {
                        lampShown++;
                        for (int p = 0; p < geometry.Pockets.Length; p++)
                        {
                            Vector3 pocket = origin + new Vector3(geometry.Pockets[p].Center.x, 0f, geometry.Pockets[p].Center.y);
                            if (LampBox(origin).IntersectRay(new Ray(position, pocket - position), out float hit) && hit < (pocket - position).magnitude)
                                lampBlocks++;
                        }
                    }
                    float distance = Mathf.Abs(position.z - origin.z);
                    minDistance = Mathf.Min(minDistance, distance);
                    maxDistance = Mathf.Max(maxDistance, distance);
                    maxHeight = Mathf.Max(maxHeight, position.y - origin.y);
                }
            }
            report.Check(passed == total, $"{name}: {passed}/{total} watch poses keep all 6 pockets in WatchFitArea");
            report.Check(lampBlocks == 0, $"{name}: the lamp never hides a pocket ({lampShown} poses with the lamp shown, {lampBlocks} pockets behind it)");
            report.Info($"{name}: {passed}/{total} ({100f * passed / total:F1} %), stand {minDistance:F2}-{maxDistance:F2} m from the table centre, eyes up to {maxHeight:F2} m above the cloth");
        }
        return report.ToString();
    }

    // Ball in hand from the end of the table: the kitchen (break) or the whole table, plus the cue ball, inside
    // PlaceFitArea, looking down at PlacePitchMin..Max (perspective, not top-down).
    public static string PlaceFitsArea(int samples = 100)
    {
        var report = new CheckReport("CameraChecks.PlaceFitsArea");
        MatchConfig config = PhysicsChecks.Config;
        var geometry = new TableGeometry(config.ToPoolSettings());
        var origin = new Vector3(0f, config.TableHeight, 0f);
        using var scope = new TempObjectScope();
        Camera camera = NewCamera(config);

        foreach (var (name, aspect) in Aspects)
        {
            camera.aspect = aspect;
            var random = new System.Random(5);
            foreach (bool kitchen in new[] { true, false })
            {
                int passed = 0;
                float minPitch = 90f, maxPitch = 0f, maxHeight = 0f;
                for (int i = 0; i < samples; i++)
                {
                    float maxY = kitchen ? geometry.HeadStringZ : geometry.MaxY;
                    Vector2 cue = new(Mathf.Lerp(-geometry.MaxX, geometry.MaxX, (float)random.NextDouble()),
                        Mathf.Lerp(-geometry.MaxY, maxY, (float)random.NextDouble()));
                    float end = kitchen || Mathf.Abs(cue.y) < 0.05f ? -1f : Mathf.Sign(cue.y);
                    Vector3 cueWorld = origin + new Vector3(cue.x, config.BallRadius, cue.y);
                    CameraRig.PlacePose(camera, config, geometry, origin, end, kitchen, cueWorld, out Vector3 position, out Quaternion rotation);
                    camera.transform.SetPositionAndRotation(position, rotation);
                    float pitch = rotation.eulerAngles.x;
                    minPitch = Mathf.Min(minPitch, pitch);
                    maxPitch = Mathf.Max(maxPitch, pitch);
                    maxHeight = Mathf.Max(maxHeight, position.y - origin.y);

                    float zFar = kitchen ? geometry.HeadStringZ : geometry.HalfLength;
                    bool fits = Inside(camera, config, cueWorld)
                                && Inside(camera, config, origin + new Vector3(-geometry.HalfWidth, 0f, -geometry.HalfLength))
                                && Inside(camera, config, origin + new Vector3(geometry.HalfWidth, 0f, -geometry.HalfLength))
                                && Inside(camera, config, origin + new Vector3(-geometry.HalfWidth, 0f, zFar))
                                && Inside(camera, config, origin + new Vector3(geometry.HalfWidth, 0f, zFar));
                    bool pitchOk = pitch >= config.PlacePitchMin - 0.01f && pitch <= config.PlacePitchMax + 0.01f;
                    if (fits && pitchOk)
                        passed++;
                }
                string area = kitchen ? "kitchen" : "whole table";
                report.Check(passed == samples, $"{name} {area}: {passed}/{samples} poses fit with pitch {config.PlacePitchMin:F0}-{config.PlacePitchMax:F0} deg");
                report.Info($"{name} {area}: {passed}/{samples}, pitch {minPitch:F1}-{maxPitch:F1} deg, eyes up to {maxHeight:F2} m above the cloth");
            }
        }
        return report.ToString();
    }

    private static bool Inside(Camera camera, MatchConfig config, Vector3 world)
    {
        Vector3 viewport = camera.WorldToViewportPoint(world);
        return viewport.z > 0f && config.PlaceFitArea.Contains(new Vector2(viewport.x, viewport.y));
    }

    private static Camera NewCamera(MatchConfig config)
    {
        var camera = new GameObject("CheckCamera").AddComponent<Camera>();
        camera.fieldOfView = config.FieldOfView;
        camera.enabled = false;
        return camera;
    }
}
