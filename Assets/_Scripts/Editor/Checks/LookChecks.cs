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
            shot.Caps[p] = camera.WorldToScreenPoint(table.TableToWorld(pockets[p].Center, match.Config.RailTop));
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
}
