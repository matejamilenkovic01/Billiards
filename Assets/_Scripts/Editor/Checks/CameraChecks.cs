using UnityEngine;

// Gameplay redesign G1 / G5: what the watch and ball-in-hand views must show, on several phone aspects.
public static class CameraChecks
{
    private static readonly (string name, float aspect)[] Aspects = { ("9:16", 9f / 16f), ("9:19.5", 9f / 19.5f), ("9:21", 9f / 21f) };

    public static string All() => WatchFitsAllPockets() + "\n" + PlaceFitsArea();

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
