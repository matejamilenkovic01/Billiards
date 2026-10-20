using UnityEditor;
using UnityEngine;

// implementation_definition_v3 V5: the power strip's grip moves 1:1 with the finger, 100% is reachable from
// anywhere it can be grabbed, and SOFT / MED / HARD sit at 15 / 50 / 85%.
public static class ControlChecks
{
    public static string PullMapping()
    {
        var report = new CheckReport("ControlChecks.PullMapping");
        MatchConfig config = PhysicsChecks.Config;
        float bottom = MatchSceneBuilder.PowerStripBottom;
        float height = MatchSceneBuilder.PowerStripSize.y;
        float travel = config.PullTravelPixels;

        // (a) From every grabbing touch, the finger has screen (safe area) below for the full travel.
        int points = 0, reachable = 0, grabbing = 0;
        float leastRoom = float.MaxValue;
        for (float fromTop = 0f; fromTop <= height; fromTop += 1f)
        {
            if (!PowerStripView.Grabs(fromTop, height, config.PullGrabZone))
                continue;
            grabbing++;
            points++;
            float touchY = bottom + height - fromTop;
            float room = touchY - travel;
            leastRoom = Mathf.Min(leastRoom, room);
            if (room >= 0f)
                reachable++;
        }
        report.Check(grabbing > 0 && reachable == points, $"(a) 100% reachable from {reachable}/{points} grabbing touch heights (least room below: {leastRoom:F0} ref px)");
        report.Check(!PowerStripView.Grabs(height * 0.6f, height, config.PullGrabZone), "a touch in the lower part of the strip grabs nothing");

        // (b) 1:1: a drag of X px moves the grip X px.
        float worst = 0f;
        for (float drag = 0f; drag <= travel; drag += 5f)
            worst = Mathf.Max(worst, Mathf.Abs(PowerStripView.PullFromDrag(drag, travel) * travel - drag));
        report.Check(worst < 0.01f, $"(b) grip moves exactly with the finger (worst {worst:F3} px)");
        float gripRest = MatchSceneBuilder.PowerGripRestY(config);
        report.Check(gripRest - travel >= -height * 0.5f + PowerStripView.BottomMarginPixels - 0.5f && gripRest <= 421f,
            $"grip rests at {gripRest:F0} (under the arrow) and reaches the strip's bottom at 100%");

        // (c) The built strip (open Match scene): marks where the grip is at 15 / 50 / 85%.
        PowerStripView view = Object.FindFirstObjectByType<PowerStripView>(FindObjectsInactive.Include);
        if (view == null)
        {
            report.Check(false, "(c) open the Match scene to check the marks");
            return report.ToString();
        }
        var grip = (RectTransform)new SerializedObject(view).FindProperty("_grip").objectReferenceValue;
        Vector3 marks = config.PowerMarks;
        (string name, float pull)[] wanted = { ("SOFT", marks.x), ("MED", marks.y), ("HARD", marks.z) };
        int good = 0;
        foreach (var (name, pull) in wanted)
        {
            Transform mark = view.transform.Find(name);
            float expected = grip.anchoredPosition.y - pull * travel;
            if (mark != null && Mathf.Abs(((RectTransform)mark).anchoredPosition.y - expected) < 0.5f)
                good++;
            else
                report.Info($"{name}: at {(mark != null ? ((RectTransform)mark).anchoredPosition.y : float.NaN):F1}, want {expected:F1}");
        }
        report.Check(good == 3 && Mathf.Approximately(marks.x, 0.15f) && Mathf.Approximately(marks.y, 0.5f) && Mathf.Approximately(marks.z, 0.85f),
            "(c) SOFT / MED / HARD at the grip's 15 / 50 / 85% positions");
        report.Info($"strip bottom {bottom:F0} ref px above the safe area, height {height:F0}, grab zone top {config.PullGrabZone:P0}, travel {travel:F0} px");
        return report.ToString();
    }
}
