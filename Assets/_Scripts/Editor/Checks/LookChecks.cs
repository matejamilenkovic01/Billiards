using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// The table's look in numbers (implementation_definition_iteration_v4 V6), from real frames: Play mode, Match scene.
// Start() sets up a table (the bot to shoot, so the camera is in Watch; or the player, for Aim), captures frames and
// remembers where the cloth samples, the cue ball and the pocket caps are on screen at that moment; Result() reads
// the files back: cloth luma (centre, edges / centre), the cue ball's luma and its share of blown-out pixels, and
// the caps' shimmer (pixel differences between three frames 0.1 s apart while the head sways).
public static class LookChecks
{
    private struct Shot
    {
        public string Path;
        public Vector2[] Cloth;      // screen points: [0] centre, then the edges
        public Vector2 Ball;
        public float BallRadius;
        public Vector2[] Caps;
        public int DrawCalls;
        public int Triangles;
    }

    private static readonly List<Shot> _shots = new();
    private static string _tag;

    private static readonly Vector2[] ClothPoints =
    {
        new(0f, 0.15f), new(0.45f, 0.15f), new(-0.45f, 0.15f), new(0f, 0.95f), new(0f, -0.95f)
    };

    // watch = the bot shoots (Watch camera); else the player aims. Captures three frames 0.1 s apart.
    public static string Start(string tag, bool watch, string venueId = null)
    {
        MatchController match = MatchController.Instance;
        if (match == null || !Application.isPlaying)
            return "needs Play mode in the Match scene";
        Application.runInBackground = true;
        PopupManager.Instance.HideAllImmediate();
        if (!string.IsNullOrEmpty(venueId))
            Object.FindFirstObjectByType<VenueView>().Apply(venueId);
        _shots.Clear();
        _tag = tag;
        match.SetupTable(BallGroup.Solids, new[] { 0, 1, 9, 8 },
            new[] { new Vector2(-0.18f, -0.35f), new Vector2(0.3f, 0.55f), new Vector2(-0.35f, 0.75f), new Vector2(0.25f, -0.8f) },
            false, watch ? Side.Bot : Side.Player);
        if (!watch)
        {
            var player = Object.FindFirstObjectByType<PlayerShotController>();
            player.DebugSetAim(new Vector2(0.4f, 1f));
            // The ball alone: no cue tip or guide line over it (the aim pose stays).
            DG.Tweening.DOVirtual.DelayedCall(1.3f, () =>
            {
                player.enabled = false;
                Object.FindFirstObjectByType<AimGuideView>().HideAim();
                match.Cue.SetVisible(false);
            }, true);
            DG.Tweening.DOVirtual.DelayedCall(2.3f, () => player.enabled = true, true);
        }
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            DG.Tweening.DOVirtual.DelayedCall(1.6f + 0.1f * index, () => Capture(index), true);
        }
        return $"{tag}: capturing";
    }

    private static void Capture(int index)
    {
        MatchController match = MatchController.Instance;
        Camera camera = match.CameraRig.Camera;
        TableView table = match.Table;
        var shot = new Shot { Path = Path.GetFullPath($"Screenshots/v4_5_measure_{_tag}_{index}.png") };
        shot.Cloth = new Vector2[ClothPoints.Length];
        for (int i = 0; i < ClothPoints.Length; i++)
            shot.Cloth[i] = camera.WorldToScreenPoint(table.TableToWorld(ClothPoints[i] * new Vector2(1f, 1f), 0f));
        Vector3 ball = table.BallCenterWorld(match.Balls[0].Pos);
        shot.Ball = camera.WorldToScreenPoint(ball);
        Vector2 edge = camera.WorldToScreenPoint(ball + camera.transform.right * match.Config.BallRadius);
        shot.BallRadius = Vector2.Distance(edge, shot.Ball);
        PocketInfo[] pockets = match.Geometry.Pockets;
        shot.Caps = new Vector2[pockets.Length];
        for (int p = 0; p < pockets.Length; p++)
            shot.Caps[p] = camera.WorldToScreenPoint(table.TableToWorld(PocketLook.Of(match.Geometry, match.Config, p).Center, match.Config.RailTop));
        shot.DrawCalls = UnityEditor.UnityStats.drawCalls;
        shot.Triangles = UnityEditor.UnityStats.triangles;
        _shots.Add(shot);
        ScreenCapture.CaptureScreenshot(shot.Path);
    }

    public static string Result()
    {
        var report = new StringBuilder();
        if (_shots.Count < 3)
            return $"{_tag}: {_shots.Count} frames so far";
        var textures = new List<Texture2D>();
        foreach (Shot shot in _shots)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(shot.Path));
            textures.Add(texture);
        }
        Shot first = _shots[0];
        Texture2D frame = textures[0];

        float centre = Patch(frame, first.Cloth[0], 6);
        float worstEdge = float.MaxValue;
        for (int i = 1; i < first.Cloth.Length; i++)
        {
            if (Inside(frame, first.Cloth[i]))
                worstEdge = Mathf.Min(worstEdge, Patch(frame, first.Cloth[i], 6));
        }

        double ballSum = 0;
        int ballPixels = 0, blown = 0;
        float r = first.BallRadius * 0.85f;
        for (int y = Mathf.FloorToInt(first.Ball.y - r); y <= first.Ball.y + r; y++)
        for (int x = Mathf.FloorToInt(first.Ball.x - r); x <= first.Ball.x + r; x++)
        {
            if ((new Vector2(x, y) - first.Ball).sqrMagnitude > r * r || x < 0 || y < 0 || x >= frame.width || y >= frame.height)
                continue;
            float luma = frame.GetPixel(x, y).grayscale;
            ballSum += luma;
            ballPixels++;
            if (luma >= 0.98f)
                blown++;
        }

        // Shimmer: the caps' boxes, largest difference over the three frames and the share of pixels moving > 10/255.
        int capPixels = 0, moving = 0, flickering = 0;
        float maxDiff = 0f, sumDiff = 0f, maxFlicker = 0f;
        const int box = 40;
        foreach (Vector2 cap in first.Caps)
        {
            if (!Inside(frame, cap))
                continue;
            for (int y = (int)cap.y - box; y < cap.y + box; y++)
            for (int x = (int)cap.x - box; x < cap.x + box; x++)
            {
                if (x < 0 || y < 0 || x >= frame.width || y >= frame.height)
                    continue;
                float diff = 0f;
                for (int f = 1; f < textures.Count; f++)
                    diff = Mathf.Max(diff, Mathf.Abs(textures[f].GetPixel(x, y).grayscale - textures[f - 1].GetPixel(x, y).grayscale) * 255f);
                // Flicker: the middle frame against the mean of its neighbours. The slow head sway moves edges almost
                // linearly over 0.2 s and cancels out; specular sparkle does not.
                float flicker = Mathf.Abs(textures[1].GetPixel(x, y).grayscale - 0.5f * (textures[0].GetPixel(x, y).grayscale + textures[2].GetPixel(x, y).grayscale)) * 255f;
                maxFlicker = Mathf.Max(maxFlicker, flicker);
                if (flicker > 10f)
                    flickering++;
                capPixels++;
                sumDiff += diff;
                maxDiff = Mathf.Max(maxDiff, diff);
                if (diff > 10f)
                    moving++;
            }
        }
        foreach (Texture2D texture in textures)
            Object.DestroyImmediate(texture);

        report.Append($"{_tag}: cloth centre {centre:F3}, edges / centre {(worstEdge < float.MaxValue ? worstEdge / centre : 0f):P0}; ");
        report.Append($"cue ball luma {(ballPixels > 0 ? ballSum / ballPixels : 0):F3} ({ballPixels} px, {(ballPixels > 0 ? blown / (float)ballPixels : 0f):P1} >= 0.98); ");
        report.Append($"caps shimmer: mean {(capPixels > 0 ? sumDiff / capPixels : 0f):F2}, max {maxDiff:F0}, {(capPixels > 0 ? moving / (float)capPixels : 0f):P2} > 10 ({capPixels} px); ");
        report.Append($"flicker max {maxFlicker:F0}, {(capPixels > 0 ? flickering / (float)capPixels : 0f):P2} > 10");
        report.Append($"; {first.DrawCalls} draw calls, {first.Triangles / 1000f:F1}k triangles");
        return report.ToString();
    }

    private static bool Inside(Texture2D texture, Vector2 point) => point.x > 8 && point.y > 8 && point.x < texture.width - 8 && point.y < texture.height - 8;

    private static float Patch(Texture2D texture, Vector2 center, int half)
    {
        double sum = 0;
        int n = 0;
        for (int y = (int)center.y - half; y <= center.y + half; y++)
        for (int x = (int)center.x - half; x <= center.x + half; x++)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                continue;
            sum += texture.GetPixel(x, y).grayscale;
            n++;
        }
        return n > 0 ? (float)(sum / n) : 0f;
    }

    // v5 V4 (edit mode, the Table prefab's meshes): no point of the cloth, cushions, rails or caps lies inside a
    // visible hole (radius - 1 mm) below the cloth; the cloth's lip and the liner are the only things down there. Every
    // vertex and every triangle's centre and edge midpoints are sampled.
    public static string PocketIntersections()
    {
        var report = new CheckReport("LookChecks.PocketIntersections");
        MatchConfig config = PhysicsChecks.Config;
        var geometry = new TableGeometry(config.ToPoolSettings());
        var table = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.TablePath);
        var holes = new PocketLook.Hole[geometry.Pockets.Length];
        for (int i = 0; i < holes.Length; i++)
            holes[i] = PocketLook.Of(geometry, config, i);
        foreach (string part in new[] { "Felt", "Cushions", "Rails", "PocketCaps" })
        {
            Mesh mesh = table.transform.Find(part).GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            int inside = 0;
            float worst = float.MaxValue;
            void Test(Vector3 q)
            {
                if (q.y >= -1e-4f)
                    return;
                foreach (PocketLook.Hole hole in holes)
                {
                    float d = (new Vector2(q.x, q.z) - hole.Center).magnitude - hole.Radius;
                    worst = Mathf.Min(worst, d);
                    if (d < -0.001f)
                        inside++;
                }
            }
            foreach (Vector3 q in v)
                Test(q);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Test((a + b + c) / 3f);
                Test((a + b) * 0.5f);
                Test((b + c) * 0.5f);
                Test((c + a) * 0.5f);
            }
            report.Info($"{part}: {v.Length} vertices, {t.Length / 3} triangles; below the cloth, closest to a hole's axis: {(worst == float.MaxValue ? "nothing below the cloth" : $"{worst * 1000f:+0.0;-0.0} mm from the rim")}");
            report.Check(inside == 0, $"{part}: nothing inside a hole below the cloth ({inside} points)");
        }
        return report.ToString();
    }

    // v5 V4 (edit mode): seen from above, every cap and every hole stays inside the frame's outer outline (a margin of
    // 2 mm), so nothing sticks out of the table.
    public static string PocketsInsideFrame()
    {
        var report = new CheckReport("LookChecks.PocketsInsideFrame");
        MatchConfig config = PhysicsChecks.Config;
        var geometry = new TableGeometry(config.ToPoolSettings());
        float outerX = geometry.HalfWidth + config.CushionDepth + config.RailWidth;
        float outerZ = geometry.HalfLength + config.CushionDepth + config.RailWidth;
        var table = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.TablePath);
        Mesh caps = table.transform.Find("PocketCaps").GetComponent<MeshFilter>().sharedMesh;
        float capRoom = float.MaxValue;
        foreach (Vector3 q in caps.vertices)
            capRoom = Mathf.Min(capRoom, outerX - Mathf.Abs(q.x), outerZ - Mathf.Abs(q.z));
        float holeRoom = float.MaxValue;
        for (int i = 0; i < geometry.Pockets.Length; i++)
        {
            PocketLook.Hole hole = PocketLook.Of(geometry, config, i);
            for (int k = 0; k < 72; k++)
            {
                float a = k / 72f * Mathf.PI * 2f;
                Vector2 q = hole.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * hole.Radius;
                holeRoom = Mathf.Min(holeRoom, outerX - Mathf.Abs(q.x), outerZ - Mathf.Abs(q.y));
            }
        }
        report.Info($"room to the frame's outer edge: caps {capRoom * 1000f:F1} mm, holes {holeRoom * 1000f:F1} mm");
        report.Check(capRoom >= 0.002f, $"every cap inside the frame ({capRoom * 1000f:F1} mm)");
        report.Check(holeRoom >= 0.002f, $"every hole inside the frame ({holeRoom * 1000f:F1} mm)");
        return report.ToString();
    }

    // v5 V5 (edit mode, every venue's room): LOOK round the table, yaw 0-360 deg in 2 deg steps (~90 deg/s) at pitch
    // 12-85 deg and two distances, through LampView's rule with its hysteresis and minimum time. The truth is a ray from
    // the eyes to a 4x finer table grid and every pocket against the lamp's parts: it is never shown in front of a pocket,
    // never hidden once the view has been clear for the show delay, and it changes state at most twice for each window
    // in which it is really in the way (no flicker).
    public static string LampVisibleWhenClear()
    {
        var report = new CheckReport("LookChecks.LampVisibleWhenClear");
        MatchConfig config = PhysicsChecks.Config;
        var geometry = new TableGeometry(config.ToPoolSettings());
        var origin = new Vector3(0f, config.TableHeight, 0f);
        float frame = config.CushionDepth + config.RailWidth;
        var half = new Vector2(geometry.HalfWidth + frame, geometry.HalfLength + frame);
        var keys = new List<Vector3>();
        for (int p = 0; p < geometry.Pockets.Length; p++)
        {
            Vector2 c = PocketLook.Of(geometry, config, p).Center;
            keys.Add(origin + new Vector3(c.x, 0f, c.y));
        }
        using var scope = new TempObjectScope();
        var camera = new GameObject("CheckCamera").AddComponent<Camera>();
        camera.fieldOfView = config.FieldOfView;
        camera.aspect = 9f / 19.5f;
        camera.enabled = false;
        const float step = 1f / 45f;
        foreach (DataAssetBuilder.VenueSpec venue in DataAssetBuilder.VenueSpecs)
        {
            var room = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabBuilder.RoomPath(venue.Id)));
            var parts = new List<Bounds>();
            foreach (Renderer renderer in room.GetComponentInChildren<LampView>().GetComponentsInChildren<Renderer>())
            {
                if (!renderer.name.StartsWith("Hanger"))
                    parts.Add(renderer.bounds);
            }
            int hiddenClear = 0, shownOnPocket = 0, worstChanges = 0, hiddenFrames = 0, frames = 0;
            string worstPass = "";
            foreach (float distance in new[] { 1.6f, 3.0f })
            foreach (float pitch in new[] { 12f, 20f, 30f, 40f, 50f, 60f, 70f, 80f, 85f })
            {
                bool hidden = false, first = true, wasBlocked = false;
                float since = 0f, clearFor = 10f;
                int changes = 0, windows = 0;
                for (float yaw = 0f; yaw < 360f; yaw += 2f)
                {
                    var state = new OrbitState { Pivot = origin, Yaw = yaw, Pitch = pitch, Distance = distance };
                    CameraRig.ConstrainOrbit(ref state, config, geometry, origin);
                    Vector3 eye = CameraRig.OrbitPosition(state);
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(state.Pivot - eye, Vector3.up));
                    float coverage = LampView.Coverage(camera, parts, origin, half, keys, out bool coversKey);
                    // since: how long the rule's view has been clear.
                    since = LampView.IsClear(coverage, coversKey, config) ? since + step : 0f;
                    bool next = first ? coverage > config.LampOccludeThreshold || coversKey : LampView.NextHidden(hidden, coverage, coversKey, since, config);
                    bool settled = since >= config.LampShowDelay + step;
                    if (!first && next != hidden)
                        changes++;
                    hidden = next;
                    first = false;

                    // The truth, on a finer grid (4x) than the rule's, rays from the eyes against the lamp's parts.
                    bool Blocked(Vector3 point)
                    {
                        Vector3 to = point - eye;
                        foreach (Bounds part in parts)
                        {
                            if (part.IntersectRay(new Ray(eye, to), out float hit) && hit < to.magnitude)
                                return true;
                        }
                        return false;
                    }
                    bool OnScreen(Vector3 point)
                    {
                        Vector3 v = camera.WorldToViewportPoint(point);
                        return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
                    }
                    bool anything = false, pocket = false;
                    int seen = 0, covered = 0;
                    for (int ix = 0; ix < 33; ix++)
                    for (int iz = 0; iz < 65; iz++)
                    {
                        var point = origin + new Vector3(Mathf.Lerp(-half.x, half.x, ix / 32f), 0f, Mathf.Lerp(-half.y, half.y, iz / 64f));
                        if (!OnScreen(point))
                            continue;
                        seen++;
                        if (Blocked(point))
                            covered++;
                    }
                    anything = covered > 0;
                    float trueCoverage = seen > 0 ? covered / (float)seen : 0f;
                    foreach (Vector3 key in keys)
                        pocket |= OnScreen(key) && Blocked(key);
                    anything |= pocket;
                    frames++;
                    if (hidden)
                        hiddenFrames++;
                    // Past the minimum time in a state the rule must have caught up.
                    if (hidden && !anything && settled)
                        hiddenClear++;
                    if (!hidden && pocket)
                        shownOnPocket++;
                    // A new window where the lamp really needs to go (the rule's own thresholds on the true coverage: in over
                    // the hide threshold or on a pocket, out under the show threshold; a gap shorter than the show delay
                    // joins two).
                    bool blocking = wasBlocked ? trueCoverage >= config.LampShowThreshold || pocket : trueCoverage > config.LampOccludeThreshold || pocket;
                    if (blocking && !wasBlocked && clearFor >= config.LampShowDelay)
                        windows++;
                    clearFor = blocking ? 0f : clearFor + step;
                    wasBlocked = blocking;
                }
                // Flicker would change state more often than twice (hide, show) for each window.
                if (changes - 2 * Mathf.Max(1, windows) > worstChanges)
                    worstPass = $" (distance {distance} m, pitch {pitch} deg: {changes} changes, {windows} windows)";
                worstChanges = Mathf.Max(worstChanges, changes - 2 * Mathf.Max(1, windows));
            }
            Object.DestroyImmediate(room);
            report.Info($"{venue.Id}: hidden in {hiddenFrames}/{frames} poses; hidden with nothing behind it {hiddenClear}; shown on a pocket {shownOnPocket}; worst changes beyond 2 a window {worstChanges}{worstPass}");
            report.Check(hiddenClear == 0, $"{venue.Id}: never hidden while it covers nothing ({hiddenClear})");
            report.Check(worstChanges <= 0, $"{venue.Id}: at most 2 changes for each window it is in the way (worst excess {worstChanges})");
            report.Check(shownOnPocket == 0, $"{venue.Id}: never shown in front of a pocket ({shownOnPocket})");
        }
        return report.ToString();
    }
}
