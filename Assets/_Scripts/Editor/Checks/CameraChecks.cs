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
            int passed = 0, total = 0;
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
                    float distance = Mathf.Abs(position.z - origin.z);
                    minDistance = Mathf.Min(minDistance, distance);
                    maxDistance = Mathf.Max(maxDistance, distance);
                    maxHeight = Mathf.Max(maxHeight, position.y - origin.y);
                }
            }
            report.Check(passed == total, $"{name}: {passed}/{total} watch poses keep all 6 pockets in WatchFitArea");
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
