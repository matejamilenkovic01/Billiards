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
        DG.Tweening.DOVirtual.Float(0f, 1f, 5.5f, _ => _rise.Add((Time.unscaledTime - _riseStart, rig.transform.rotation, rig.IsBlending || rig.Mode != CameraMode.Watch))).SetUpdate(true);
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
            float turn = AimFeelChecks.Angle(_rise[i - 1].rotation, _rise[i].rotation);
            turns.Append($"{_rise[i].time:F2}:{turn:F1}{(_rise[i].blending ? "" : "w")} ");
            // Head sway adds a little; the allowance is the doc's 1 degree.
            float excess = turn - (config.MaxCameraTurnDegPerSec * 1.25f * dt + 1f);
            if (excess > worstExcess)
            {
                worstExcess = excess;
                worstAt = _rise[i].time;
            }
            if (reached < 0f && !_rise[i].blending)
                reached = _rise[i].time;
        }
        float expected = config.ShotHoldTime + config.ShotRiseTime;
        float latest = config.ShotHoldTime + config.ShotRiseMaxTime;
        report.Info($"{_rise.Count} frames ({frame * 1000f:F0} ms each): Watch pose reached after {reached:F2} s (expected {expected:F2}), worst turn over the limit {worstExcess:F2} deg at {worstAt:F2} s");
        report.Info("turn per frame (s:deg, w = in Watch): " + turns);
        // Sampled in the tween update, before the camera's LateUpdate: up to two frames late.
        // v4 V9.1: a bigger turn takes longer (up to ShotRiseMaxTime); the peak of the eased turn stays near the limit.
        report.Check(reached > 0f && reached >= expected - 0.15f - 2f * frame && reached <= latest + 0.15f + 2f * frame, $"Watch pose reached between {expected:F2} and {latest:F2} s (+- 0.15 s and 2 frames of sampling)");
        report.Check(worstExcess <= 0f, "no turn step faster than MaxCameraTurnDegPerSec x dt x 1.25 + 1 deg");
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
    // v5 V1, Play mode, Match scene: the aim swept across balls, the camera sampled every frame at a fixed 60 fps
    // (Time.captureDeltaTime). scenario 0: the break (cue ball in the kitchen, the full rack) at 20 deg/s, 5 deg/s and
    // with the held fine-aim arrow; 1: 20 random layouts, 20 deg/s across every ball (at most 170 deg) and back to the middle, 5 deg/s over
    // 30 degrees round the middle, the fine-aim arrow over 10 degrees (the whole arc at 5 deg/s would take ~30 min). AimSweepStart, then poll AimSweepSteady until it stops saying "running".
    private struct SweepFrame
    {
        public float Time;
        public int Leg;
        public bool Sweep;
        public float Height, Pitch, Yaw, Distance, Aim;
        public CameraRig.AimParts Parts;
    }

    private sealed class SweepLeg
    {
        public string Name;
        public int Kind;          // 0 setup, 1 wait for the aim view, 2 settle, 3 drag, 4 fine-aim arrow
        public System.Action Setup;
        public float Seconds, Rate, To;
        public float Done, Next;
        public int Direction;
        public bool Started;
    }

    private static readonly System.Collections.Generic.List<SweepFrame> _sweep = new();
    private static readonly System.Collections.Generic.List<SweepLeg> _legs = new();
    private static int _leg;
    private static bool _sweepOn;
    private static float _sweepClock;
    private static DG.Tweening.Tween _sweepTicker;

    private static float AimAngle(PlayerShotController player)
    {
        var field = typeof(PlayerShotController).GetField("_aim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Vector2 aim = (Vector2)field.GetValue(player);
        return Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
    }

    private static Vector2 Dir(float degrees) => new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    // The arc (degrees, unwrapped round the first ball) that holds every listed ball seen from the cue ball, widened by
    // each ball's own half-width and `margin`.
    private static void Arc(Vector2 cue, Vector2[] balls, float radius, float margin, out float from, out float to)
    {
        float first = Mathf.Atan2(balls[0].y - cue.y, balls[0].x - cue.x) * Mathf.Rad2Deg;
        from = float.MaxValue;
        to = float.MinValue;
        foreach (Vector2 ball in balls)
        {
            Vector2 d = ball - cue;
            float a = first + Mathf.DeltaAngle(first, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            float half = Mathf.Asin(Mathf.Clamp01(2f * radius / Mathf.Max(d.magnitude, 2f * radius))) * Mathf.Rad2Deg;
            from = Mathf.Min(from, a - half - margin);
            to = Mathf.Max(to, a + half + margin);
        }
    }

    public static string AimSweepStart(int scenario)
    {
        MatchController match = MatchController.Instance;
        if (match == null || !Application.isPlaying)
            return "needs Play mode in the Match scene";
        Application.runInBackground = true;
        PopupManager.Instance.HideAllImmediate();
        var player = Object.FindFirstObjectByType<PlayerShotController>();
        MatchConfig config = match.Config;
        _sweep.Clear();
        _legs.Clear();
        _leg = 0;
        _sweepClock = 0f;
        MatchController.DebugSetRules(RuleSetId.Standard);
        MatchController.DebugStart(true, null, 8);

        void Wait(string name) => _legs.Add(new SweepLeg { Name = name, Kind = 1, Seconds = 8f });
        void Settle(float seconds) => _legs.Add(new SweepLeg { Name = "settle", Kind = 2, Seconds = seconds });
        void Drag(string name, float to, float rate) => _legs.Add(new SweepLeg { Name = name, Kind = 3, To = to, Rate = rate });
        void Fine(string name, float to) => _legs.Add(new SweepLeg { Name = name, Kind = 4, To = to });
        void Setup(string name, System.Action action) => _legs.Add(new SweepLeg { Name = name, Kind = 0, Setup = action });

        if (scenario == 0)
        {
            Wait("break: aim view");
            float from = 0f, to = 0f;
            Setup("break: arc", () =>
            {
                BallState[] balls = match.Balls;
                var rack = new System.Collections.Generic.List<Vector2>();
                for (int n = 1; n < balls.Length; n++)
                    if (!balls[n].Pocketed)
                        rack.Add(balls[n].Pos);
                Arc(balls[0].Pos, rack.ToArray(), config.BallRadius, 2f, out from, out to);
                player.DebugSetAim(Dir(from));
                _legs[_leg + 2].To = to;
                _legs[_leg + 3].To = from;
                _legs[_leg + 4].To = to;
                _legs[_leg + 6].To = from;
            });
            Settle(1.5f);
            Drag("break 20 deg/s ->", 0f, 20f);
            Drag("break 20 deg/s <-", 0f, 20f);
            Drag("break 5 deg/s ->", 0f, 5f);
            Settle(0.8f);
            Fine("break fine arrow <-", 0f);
            Settle(0.8f);
        }
        else
        {
            var random = new System.Random(2026);
            for (int layout = 0; layout < 20; layout++)
            {
                int index = layout;
                int[] numbers = { 0, 1, 2, 3, 9, 10, 11, 8 };
                Vector2[] positions = RandomLayout(random, match.Geometry, config.BallRadius, numbers.Length);
                var others = new Vector2[positions.Length - 1];
                System.Array.Copy(positions, 1, others, 0, others.Length);
                Arc(positions[0], others, config.BallRadius, 2f, out float from, out float to);
                if (to - from > 170f)
                    to = from + 170f;
                float middle = (from + to) * 0.5f;
                Setup($"layout {index}", () =>
                {
                    MatchController.DebugSetupTable(BallGroup.Solids, numbers, positions);
                    player.DebugSetAim(Dir(from));
                });
                Wait($"layout {index}: aim view");
                Settle(1.0f);
                Drag($"layout {index} 20 deg/s ->", to, 20f);
                Drag($"layout {index} 20 deg/s <-", Mathf.Min(to, middle + 15f), 20f);
                Drag($"layout {index} 5 deg/s <-", Mathf.Max(from, middle - 15f), 5f);
                Fine($"layout {index} fine arrow ->", Mathf.Max(from, middle - 15f) + 10f);
                Settle(0.5f);
            }
        }

        _sweepOn = true;
        CameraRig.DebugManual = false;
        _sweepTicker?.Kill();
        _sweepTicker = DG.Tweening.TweenSettingsExtensions.SetUpdate(DG.Tweening.DOVirtual.Float(0f, 1f, 3600f, _ => TickSweep(player, match)), true);
        return $"started, {_legs.Count} legs";
    }

    private static Vector2[] RandomLayout(System.Random random, TableGeometry geometry, float radius, int count)
    {
        var positions = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var p = new Vector2(Mathf.Lerp(-geometry.MaxX, geometry.MaxX, (float)random.NextDouble()),
                    Mathf.Lerp(-geometry.MaxY, geometry.MaxY, (float)random.NextDouble()));
                bool free = true;
                for (int j = 0; j < i && free; j++)
                    free = (positions[j] - p).magnitude > 4f * radius;
                if (!free)
                    continue;
                positions[i] = p;
                break;
            }
        }
        return positions;
    }

    // Every step is one 1/60 s frame of the game's aim and the camera (PlayerShotController.Update, then the camera),
    // SweepSubSteps of them per Editor frame once the sweep runs (the unfocused Editor draws only ~10 frames a second).
    private const int SweepSubSteps = 6;
    private static readonly System.Reflection.MethodInfo PlayerUpdate =
        typeof(PlayerShotController).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    private static void TickSweep(PlayerShotController player, MatchController match)
    {
        int steps = CameraRig.DebugManual ? SweepSubSteps : 1;
        for (int i = 0; i < steps && _sweepOn; i++)
        {
            bool manual = CameraRig.DebugManual;
            SweepStep(player, match);
            if (manual && _sweepOn)
            {
                PlayerUpdate.Invoke(player, null);
                match.CameraRig.DebugStep();
                SampleSweep(player, match);
            }
        }
    }

    private static void SweepStep(PlayerShotController player, MatchController match)
    {
        if (!_sweepOn)
            return;
        if (_leg >= _legs.Count)
        {
            _sweepOn = false;
            Time.captureDeltaTime = 0f;
            CameraRig.DebugFrameTime = 0f;
            CameraRig.DebugManual = false;
            _sweepTicker?.Kill();
            return;
        }
        float dt = CameraRig.DebugFrameTime > 0f ? CameraRig.DebugFrameTime : Time.unscaledDeltaTime;
        SweepLeg leg = _legs[_leg];
        MatchConfig config = match.Config;
        switch (leg.Kind)
        {
            case 0:
                leg.Setup();
                if (leg.Name.StartsWith("layout") && !leg.Name.Contains("fine"))
                    CameraRig.DebugManual = false;
                _leg++;
                return;
            case 1:
            {
                CameraRig rig = match.CameraRig;
                leg.Done += dt;
                // Ball in hand (the break): the cue ball stays where it was put, DONE.
                if (rig.Mode == CameraMode.Place && !rig.IsBlending)
                    GameEvents.RaisePlaceCueBallConfirmed();
                if (rig.Mode == CameraMode.Aim && !rig.IsBlending && match.IsHumanTurn)
                {
                    // From here on every frame is 1/60 s, for the game and the camera.
                    Time.captureDeltaTime = 1f / 60f;
                    CameraRig.DebugFrameTime = 1f / 60f;
                    CameraRig.DebugManual = true;
                    _leg++;
                }
                else if (leg.Done > leg.Seconds)
                {
                    leg.Name += " (never reached)";
                    _leg++;
                }
                return;
            }
            case 2:
                leg.Done += dt;
                if (leg.Done >= leg.Seconds)
                    _leg++;
                return;
            case 3:
            {
                // A finger moving at a steady speed: pixels per frame, through the same handler as a real drag
                // (the full-ball magnet included).
                if (!leg.Started)
                {
                    leg.Started = true;
                    float turn = Mathf.DeltaAngle(AimAngle(player), leg.To);
                    leg.Direction = turn > 0f ? 1 : -1;
                    leg.Seconds = Mathf.Abs(turn);
                }
                float step = leg.Rate * dt;
                GameEvents.RaiseLookDrag(new Vector2(-leg.Direction * step / config.AimDegreesPerPixel, 0f));
                leg.Done += step;
                if (leg.Done >= leg.Seconds)
                    _leg++;
                return;
            }
            default:
            {
                // The held arrow: one step, then repeats at FineAimRepeatRate after FineAimRepeatDelay.
                float angle = AimAngle(player);
                if (!leg.Started)
                {
                    leg.Started = true;
                    leg.Direction = Mathf.DeltaAngle(angle, leg.To) > 0f ? -1 : 1;
                    GameEvents.RaiseFineAimStep(leg.Direction, false);
                    leg.Next = config.FineAimRepeatDelay;
                    return;
                }
                leg.Done += dt;
                while (leg.Done >= leg.Next)
                {
                    GameEvents.RaiseFineAimStep(leg.Direction, true);
                    leg.Next += 1f / Mathf.Max(1f, config.FineAimRepeatRate);
                }
                float left = Mathf.DeltaAngle(AimAngle(player), leg.To);
                if (left * -leg.Direction <= 0f || leg.Done > 30f)
                    _leg++;
                return;
            }
        }
    }

    private static void SampleSweep(PlayerShotController player, MatchController match)
    {
        if (!_sweepOn || _leg >= _legs.Count)
            return;
        _sweepClock += CameraRig.DebugFrameTime;
        CameraRig rig = match.CameraRig;
        Vector3 euler = rig.transform.rotation.eulerAngles;
        int kind = _legs[_leg].Kind;
        _sweep.Add(new SweepFrame
        {
            Time = _sweepClock,
            Leg = _leg,
            Sweep = kind == 3 || kind == 4,
            Height = rig.transform.position.y - match.Table.FeltHeight,
            Pitch = euler.x > 180f ? euler.x - 360f : euler.x,
            Yaw = euler.y,
            Distance = rig.AimDistanceNow + rig.AimZoom,
            Aim = AimAngle(player),
            Parts = rig.DebugAimParts
        });
    }

    public static string AimSweepSteady()
    {
        if (_sweepOn)
            return $"running: leg {_leg}/{_legs.Count} ({(_leg < _legs.Count ? _legs[_leg].Name : "")}), {_sweep.Count} frames";
        var report = new CheckReport("CameraChecks.AimSweepSteady");
        var text = new System.Text.StringBuilder();
        float worstDh = 0f, worstPitch = 0f, worstWobble = 0f;
        int worstFlips = 0;
        string worstDhAt = "", worstPitchAt = "", worstWobbleAt = "", worstFlipsAt = "";
        // Per part: the biggest change in one frame anywhere in a sweep.
        float dAlong = 0f, dFit = 0f, dDistance = 0f, dBase = 0f, dClear = 0f, dCue = 0f, dElevation = 0f;
        var blame = new System.Collections.Generic.Dictionary<string, int>();
        for (int leg = 0; leg < _legs.Count; leg++)
        {
            var frames = _sweep.FindAll(f => f.Leg == leg && f.Sweep);
            if (frames.Count < 3)
                continue;
            float maxDh = 0f, maxPitch = 0f, travel = 0f;
            int flips = 0;
            var signs = new System.Collections.Generic.List<(float time, int sign)>();
            for (int i = 1; i < frames.Count; i++)
            {
                SweepFrame a = frames[i - 1], b = frames[i];
                float dh = b.Height - a.Height;
                maxDh = Mathf.Max(maxDh, Mathf.Abs(dh));
                maxPitch = Mathf.Max(maxPitch, Mathf.Abs(b.Pitch - a.Pitch));
                travel += Mathf.Abs(dh);
                if (Mathf.Abs(dh) > 0.0001f)
                    signs.Add((b.Time, dh > 0f ? 1 : -1));
                float da = Mathf.Abs(b.Parts.Along - a.Parts.Along), df = Mathf.Abs(b.Parts.Fit - a.Parts.Fit), dd = Mathf.Abs(b.Parts.Distance - a.Parts.Distance);
                float db = Mathf.Abs(b.Parts.Base - a.Parts.Base), dc = Mathf.Abs(b.Parts.Clear - a.Parts.Clear), dq = Mathf.Abs(b.Parts.Cue - a.Parts.Cue);
                dAlong = Mathf.Max(dAlong, da);
                dFit = Mathf.Max(dFit, df);
                dDistance = Mathf.Max(dDistance, dd);
                dBase = Mathf.Max(dBase, db);
                dClear = Mathf.Max(dClear, dc);
                dCue = Mathf.Max(dCue, dq);
                dElevation = Mathf.Max(dElevation, Mathf.Abs(b.Parts.Elevation - a.Parts.Elevation));
                // A frame over a limit: which part of the pose moved the most (height parts only count while they win the max).
                if (Mathf.Abs(dh) > 0.002f || Mathf.Abs(b.Pitch - a.Pitch) > 0.15f)
                {
                    string who = Mathf.Abs(b.Pitch - a.Pitch) > 0.15f && da > 0.002f ? "along"
                        : b.Parts.Height <= b.Parts.Base + 1e-4f ? (df > 0.01f ? "fit distance" : "distance (smoothing)")
                        : b.Parts.Height <= b.Parts.Clear + 1e-4f ? "MinEyeHeight (cue clearance)"
                        : "CueElevation";
                    if (Mathf.Abs(dh) <= 0.002f && Mathf.Abs(b.Pitch - a.Pitch) > 0.15f && da <= 0.002f)
                        who = "pitch from height";
                    blame[who] = blame.TryGetValue(who, out int n) ? n + 1 : 1;
                }
            }
            for (int i = 0; i < signs.Count; i++)
            {
                int count = 0;
                for (int j = i + 1; j < signs.Count && signs[j].time - signs[i].time <= 0.5f; j++)
                    if (signs[j].sign != signs[j - 1].sign)
                        count++;
                flips = Mathf.Max(flips, count);
            }
            float wobble = (travel - Mathf.Abs(frames[frames.Count - 1].Height - frames[0].Height)) * 0.5f;
            string name = _legs[leg].Name;
            float turned = 0f;
            for (int i = 1; i < frames.Count; i++)
                turned += Mathf.Abs(Mathf.DeltaAngle(frames[i - 1].Aim, frames[i].Aim));
            text.Append($"\n  {name}: {frames.Count} frames, aim turned {turned:F1} deg, height {frames[0].Height * 100f:F1}->{frames[frames.Count - 1].Height * 100f:F1} cm, max {maxDh * 1000f:F2} mm/frame, back-and-forth {wobble * 100f:F2} cm, pitch max {maxPitch:F3} deg/frame, height direction flips in 0.5 s {flips}");
            if (maxDh > worstDh) { worstDh = maxDh; worstDhAt = name; }
            if (maxPitch > worstPitch) { worstPitch = maxPitch; worstPitchAt = name; }
            if (wobble > worstWobble) { worstWobble = wobble; worstWobbleAt = name; }
            if (flips > worstFlips) { worstFlips = flips; worstFlipsAt = name; }
        }
        report.Check(_sweep.Count > 60 && _legs.TrueForAll(l => !l.Name.Contains("never")), $"the sweep ran ({_sweep.Count} frames)");
        report.Info($"{_sweep.Count} frames at 60 fps; legs:{text}");
        report.Info($"largest change in one frame: along {dAlong * 100f:F1} cm, fit distance (target) {dFit * 100f:F1} cm, distance {dDistance * 1000f:F1} mm, base height {dBase * 1000f:F2} mm, cue-clearance height {dClear * 1000f:F2} mm, cue-elevation height {dCue * 1000f:F2} mm, cue elevation {dElevation:F2} deg");
        var blames = new System.Text.StringBuilder();
        foreach (var pair in blame)
            blames.Append($"{pair.Key} {pair.Value}, ");
        report.Info($"frames over a limit, by the part that made them: {(blames.Length > 0 ? blames.ToString().TrimEnd(',', ' ') : "none")}");
        report.Check(worstDh <= 0.002f, $"eye height changes at most 2 mm per frame ({worstDh * 1000f:F2} mm, {worstDhAt})");
        report.Check(worstWobble <= 0.015f, $"at most 1.5 cm of up-and-down beyond the trend ({worstWobble * 100f:F2} cm, {worstWobbleAt})");
        report.Check(worstPitch <= 0.15f, $"pitch changes at most 0.15 deg per frame ({worstPitch:F3}, {worstPitchAt})");
        report.Check(worstFlips <= 1, $"the height turns round at most once in 0.5 s ({worstFlips}, {worstFlipsAt})");
        return report.ToString();
    }
}
