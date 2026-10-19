using UnityEngine;

// Gameplay redesign G7: 100% power is reachable from wherever the finger lands on the power strip.
public static class ControlChecks
{
    public static string PullRangeReachable()
    {
        var report = new CheckReport("ControlChecks.PullRangeReachable");
        MatchConfig config = PhysicsChecks.Config;
        float bottom = MatchSceneBuilder.PowerStripBottom;
        float height = MatchSceneBuilder.PowerStripSize.y;
        float min = config.PullMinRangePixels, max = config.PullRangePixels;

        int points = 0, reachable = 0;
        float shortestRoom = float.MaxValue;
        for (float h = 0f; h <= height; h += 1f)
        {
            points++;
            float range = PowerStripView.PullRange(h, min, max);
            // Full pull = dragging `range` down from the touch; there must be screen (safe area) left for it.
            float room = bottom + h - range;
            shortestRoom = Mathf.Min(shortestRoom, room);
            if (room >= 0f && Mathf.Approximately(PowerStripView.PullFromDrag(range, range), 1f))
                reachable++;
        }
        report.Check(reachable == points, $"100% reachable from {reachable}/{points} touch heights (least room below: {shortestRoom:F0} ref px)");

        float high = 0.85f * height;
        float fromHigh = PowerStripView.PullFromDrag(high, PowerStripView.PullRange(high, min, max));
        report.Check(Mathf.Approximately(fromHigh, 1f), $"touch at 85% of the strip, drag to its bottom: pull {fromHigh:F2} (want 1.00)");
        float fromTop = PowerStripView.PullFromDrag(400f, PowerStripView.PullRange(height, min, max));
        report.Check(Mathf.Abs(fromTop - 0.5f) < 1e-4f, $"touch at the top, drag 400 px: pull {fromTop:F2} (want 0.50)");
        report.Info($"strip bottom {bottom:F0} ref px above the safe area, height {height:F0}; range {min:F0}-{max:F0} px");
        return report.ToString();
    }
}
