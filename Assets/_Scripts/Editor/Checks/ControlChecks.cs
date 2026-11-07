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
    // v4 V2: the spin picker. (a) the marker dragged to the ring's edge gives |Tip| = SpinMaxOffset, (b) further out it
    // stays there, (c) a double tap puts it back in the centre, (d) the shot puts it back (gameplay raises OnSpinReset,
    // checked in Play mode by SpinResetInPlay).
    public static string SpinMapping()
    {
        var report = new CheckReport("ControlChecks.SpinMapping");
        MatchConfig config = PhysicsChecks.Config;
        float max = config.SpinMaxOffset;
        float radius = MatchSceneBuilder.SpinBallSize * 0.5f;
        var picker = new SpinPicker { MaxOffset = max };

        float worstEdge = 0f, worstOutside = 0f;
        for (int i = 0; i < 36; i++)
        {
            var direction = new Vector2(Mathf.Cos(i * 10f * Mathf.Deg2Rad), Mathf.Sin(i * 10f * Mathf.Deg2Rad));
            picker.Drag(direction * radius * max, radius);
            worstEdge = Mathf.Max(worstEdge, Mathf.Abs(picker.Tip.magnitude - max));
            picker.Drag(direction * radius * 3f, radius);
            worstOutside = Mathf.Max(worstOutside, picker.Tip.magnitude - max);
        }
        report.Info($"edge: worst |Tip| error {worstEdge:F4}; outside: worst overshoot {worstOutside:F4} (limit {max})");
        report.Check(worstEdge <= 0.005f, "(a) dragged to the edge: |Tip| = SpinMaxOffset +- 0.005");
        report.Check(worstOutside <= 1e-5f, "(b) dragged outside: never past SpinMaxOffset");

        picker.Reset();
        picker.Press(new Vector2(0f, -0.4f * radius), radius, 10f);
        Vector2 first = picker.Tip;
        bool doubleTap = picker.Press(new Vector2(5f, -0.4f * radius), radius, 10.2f);
        Vector2 afterDouble = picker.Tip;
        bool slowNotDouble = !picker.Press(new Vector2(0f, -0.4f * radius), radius, 11f);
        report.Check(first.y < -0.39f && doubleTap && afterDouble == Vector2.zero, $"(c) double tap -> centre (first tap {first})");
        report.Check(slowNotDouble && picker.Tip.y < -0.39f, "(c) two taps 0.8 s apart are two placements, not a double tap");

        picker.Reset();
        report.Check(picker.Tip == Vector2.zero, "(d) reset -> centre (the view resets on OnSpinReset)");
        return report.ToString();
    }

    // Play mode, Match scene, the player aiming: the picker's tip reaches the shot controller; a shot puts both back.
    public static string SpinResetInPlay()
    {
        var report = new CheckReport("ControlChecks.SpinResetInPlay");
        var view = Object.FindFirstObjectByType<SpinControlView>(FindObjectsInactive.Include);
        var player = Object.FindFirstObjectByType<PlayerShotController>();
        if (view == null || player == null || !Application.isPlaying)
        {
            report.Check(false, "needs Play mode in the Match scene with the player to shoot");
            return report.ToString();
        }
        view.DebugSetTip(new Vector2(0f, -0.5f));
        Vector2 before = player.Tip;
        // v4 V4: in a division with called pockets the shot waits for a call; any pocket will do here.
        GameEvents.RaisePocketCallRequested(0);
        player.DebugReleaseAt(0f, 0.3f);
        report.Info($"tip before the shot {before}, after: controller {player.Tip}, view {view.Tip}");
        report.Check(before == new Vector2(0f, -0.5f), "the picker's tip reaches the shot controller");
        report.Check(player.Tip == Vector2.zero && view.Tip == Vector2.zero, "(d) after the shot both are back in the centre");
        return report.ToString();
    }

    // v5 V3, Play mode, Match scene: the ghost hand on the power strip shows only in the tutorial. PowerHandStart(false)
    // in a ranked match (or (true) right after the tutorial started): 15 s without a touch, then a tap below the grip;
    // PowerHandResult after ~18 s reports the most the hand showed.
    private static float _handPeak = -1f;
    private static bool _handSampling;

    public static string PowerHandStart(bool tutorial)
    {
        var view = Object.FindFirstObjectByType<PowerStripView>();
        if (view == null || !Application.isPlaying)
            return "needs Play mode in the Match scene";
        var group = (CanvasGroup)typeof(PowerStripView).GetField("_hintGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(view);
        _handPeak = 0f;
        _handSampling = true;
        _handTutorial = tutorial;
        DG.Tweening.TweenSettingsExtensions.SetUpdate(DG.Tweening.DOVirtual.Float(0f, 1f, 18f, _ =>
        {
            if (_handSampling)
                _handPeak = Mathf.Max(_handPeak, group.alpha);
        }), true);
        // The tap: low on the strip, where nothing is grabbed.
        DG.Tweening.DOVirtual.DelayedCall(15f, () =>
        {
            var rect = (RectTransform)view.transform;
            Vector3 low = rect.TransformPoint(new Vector3(rect.rect.center.x, rect.rect.yMin + rect.rect.height * 0.15f, 0f));
            var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, low) };
            view.OnPointerDown(data);
        }, true);
        DG.Tweening.DOVirtual.DelayedCall(17.5f, () => _handSampling = false, true);
        return "started";
    }

    private static bool _handTutorial;

    public static string PowerHandResult()
    {
        if (_handSampling || _handPeak < 0f)
            return "not finished yet";
        var report = new CheckReport("ControlChecks.PowerHand");
        report.Info($"{(_handTutorial ? "tutorial" : "ranked match")}: the hand's alpha peaked at {_handPeak:F2} over 15 s idle and a tap below the grip");
        if (_handTutorial)
            report.Check(_handPeak > 0.5f, "in the tutorial the hand shows");
        else
            report.Check(_handPeak < 0.01f, "outside the tutorial the hand never shows (idle, tap below the grip)");
        return report.ToString();
    }

    // v5 V3, Play mode, Match scene, the player aiming: a slow drag (0.083 deg a frame, 5 deg/s) towards and past a
    // full-ball line on a legal ball: the magnet takes it, then lets it go (it used to hold it for good).
    public static string MagnetLetsGo()
    {
        var report = new CheckReport("ControlChecks.MagnetLetsGo");
        var player = Object.FindFirstObjectByType<PlayerShotController>();
        MatchController match = MatchController.Instance;
        if (player == null || match == null || !Application.isPlaying)
        {
            report.Check(false, "needs Play mode in the Match scene with the player to shoot");
            return report.ToString();
        }
        var field = typeof(PlayerShotController).GetField("_aim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        MatchController.DebugSetupTable(BallGroup.Solids, new[] { 0, 1 }, new[] { new Vector2(0f, -0.5f), new Vector2(0f, 0.3f) });
        Vector2 full = Vector2.up;
        player.DebugSetAim(ShotAccuracy.Rotate(full, -1f));
        float step = 0.0833f, config = match.Config.AimMagnetDegrees;
        bool snapped = false;
        float last = 0f;
        for (int i = 0; i < 40; i++)
        {
            GameEvents.RaiseLookDrag(new Vector2(-step / match.Config.AimDegreesPerPixel, 0f));
            last = Vector2.SignedAngle(full, (Vector2)field.GetValue(player));
            snapped |= Mathf.Abs(last) < 1e-3f;
        }
        report.Info($"40 drag steps of {step:F3} deg from -1 deg: snapped {snapped}, ends {last:+0.00;-0.00} deg off the full ball (finger moved {40 * step - 1f:+0.00} deg past it)");
        report.Check(snapped, "the magnet takes the slow drag onto the full ball");
        report.Check(last > config, $"and lets it go past (> {config:F2} deg)");
        return report.ToString();
    }
}
