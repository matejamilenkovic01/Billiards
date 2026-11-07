using System.Collections.Generic;
using System.Text;
using UnityEngine;

// v5 V6, Play mode, Match scene (a ranked match): the shot clock running out while aiming, while pulling, with the cue
// ball in hand and during the player's own pocket pick (a foul, the bot gets ball in hand, the pull is cancelled); the
// pause stopping it; the bot's never running out; no clock in the tutorial and the TRY practice. Start(), then Status()
// until it says DONE (~60 s).
public static class ShotClockChecks
{
    private static readonly StringBuilder _log = new();
    private static CheckReport _report;
    private static bool _running;
    private static int _clockFouls, _shots, _botClockEvents;
    private static float _t;

    public static string Status() => _running ? "running\n" + _log : _report == null ? "not started" : "DONE\n" + _report;

    public static string Start()
    {
        MatchController match = MatchController.Instance;
        if (match == null || !Application.isPlaying)
            return "needs Play mode in the Match scene";
        Application.runInBackground = true;
        PopupManager.Instance.HideAllImmediate();
        GameEvents.RaiseResumeRequested();
        _log.Clear();
        _report = new CheckReport("ShotClockChecks");
        _running = true;
        _t = 0f;
        GameEvents.OnFoul -= CountFoul;
        GameEvents.OnFoul += CountFoul;
        GameEvents.OnShotFired -= CountShot;
        GameEvents.OnShotFired += CountShot;
        GameEvents.OnShotClockChanged -= CountClock;
        GameEvents.OnShotClockChanged += CountClock;

        var player = Object.FindFirstObjectByType<PlayerShotController>();
        var numbers = new[] { 0, 1, 11, 9 };
        var positions = new[] { new Vector2(0.05f, -0.55f), new Vector2(0.05f, 0.25f), new Vector2(0.4f, 0.8f), new Vector2(-0.45f, -0.9f) };
        MatchController.DebugSetRules(RuleSetId.Standard);

        void Expire(string name, System.Action setup, System.Func<bool> during, float settle = 2.5f)
        {
            int fouls = 0, shots = 0;
            At(0f, () =>
            {
                MatchController.DebugSetRules(RuleSetId.Standard);
                setup();
            });
            At(settle, () =>
            {
                fouls = _clockFouls;
                shots = _shots;
                bool running = MatchController.DebugShotClock > 0f;
                bool ready = during();
                _log.AppendLine($"{name}: clock {MatchController.DebugShotClock:F1} s, set-up state {(ready ? "ok" : "NOT reached")}");
                _report.Check(running && ready, $"{name}: the clock runs in this state");
                MatchController.DebugSetShotClock(0.3f);
            });
            At(settle + 1.5f, () =>
            {
                bool fouled = _clockFouls == fouls + 1;
                bool noShot = _shots == shots;
                bool controlsGone = PlayerPhase(player) == TurnPhase.None;
                _report.Check(fouled && noShot && controlsGone, $"{name}: out -> a foul, no shot, the controls go (foul {fouled}, no shot {noShot}, controls gone {controlsGone})");
            });
            At(settle + 1.5f + match.Config.FoulBannerDuration + 0.8f, () =>
            {
                RulesState rules = match.Rules;
                bool botInHand = rules.Turn == Side.Bot && (rules.BallInHand || match.Phase != MatchPhase.Turn);
                _report.Check(botInHand && !rules.IsOver, $"{name}: the bot has ball in hand, the match goes on (turn {rules.Turn}, in hand {rules.BallInHand})");
                _report.Check(!match.CameraRig.IsPocketPick, $"{name}: the camera has left any bird's-eye pick");
            });
            _t += settle + 1.5f + match.Config.FoulBannerDuration + 1.6f;
        }

        Expire("aim", () => match.SetupTable(BallGroup.Solids, numbers, positions), () => PlayerPhase(player) == TurnPhase.Aim);
        Expire("pulling", () =>
        {
            match.SetupTable(BallGroup.Solids, numbers, positions);
            DG.Tweening.DOVirtual.DelayedCall(1.5f, () => GameEvents.RaisePullChanged(0.6f), true);
        }, () => PlayerPhase(player) == TurnPhase.Pulling);
        Expire("ball in hand", () => match.SetupTable(BallGroup.Solids, numbers, positions, true), () => PlayerPhase(player) == TurnPhase.PlaceCueBall);
        Expire("own pocket pick", () =>
        {
            MatchController.DebugSetRules(RuleSetId.CallAll);
            match.SetupTable(BallGroup.Solids, numbers, positions);
        }, () => match.CameraRig.IsPocketPick);

        // The pause stops it (+-0.1 s over 2 s).
        float before = 0f;
        At(0f, () => match.SetupTable(BallGroup.Solids, numbers, positions));
        At(2f, () => MatchController.DebugSetShotClock(20f));
        At(2.5f, () =>
        {
            before = MatchController.DebugShotClock;
            GameEvents.RaisePauseRequested();
        });
        At(4.5f, () =>
        {
            float after = MatchController.DebugShotClock;
            _log.AppendLine($"pause: {before:F2} s -> {after:F2} s over 2 s paused");
            _report.Check(Mathf.Abs(after - before) <= 0.1f, $"the pause stops the clock ({before:F2} -> {after:F2} s)");
            PopupManager.Instance.HideAllImmediate();
            GameEvents.RaiseResumeRequested();
        });
        _t += 5f;

        // The bot's clock runs (its panel shows it) and never runs out.
        int botFouls = 0, botEvents = 0;
        At(0f, () =>
        {
            botEvents = _botClockEvents;
            match.SetupTable(BallGroup.Solids, numbers, positions, false, Side.Bot);
        });
        At(1.2f, () =>
        {
            botFouls = _clockFouls;
            MatchController.DebugSetShotClock(0.05f);
        });
        At(2.5f, () =>
        {
            _report.Check(_botClockEvents > botEvents, $"the bot's panel gets its clock ({_botClockEvents - botEvents} updates)");
            _report.Check(_clockFouls == botFouls, "the bot's clock never runs out");
        });
        _t += 3f;

        // No clock in the tutorial or the TRY practice.
        At(0f, () => MatchController.DebugStartTutorial());
        At(3f, () => _report.Check(MatchController.DebugShotClock < 0f, $"tutorial: no shot clock ({MatchController.DebugShotClock:F1})"));
        At(3.2f, () =>
        {
            MatchSetup.Mode = MatchMode.Practice;
            MatchSetup.PracticeCueId = "pro";
            typeof(MatchController).GetMethod("StartPractice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(match, null);
        });
        At(6.5f, () => _report.Check(MatchController.DebugShotClock < 0f, $"TRY practice: no shot clock ({MatchController.DebugShotClock:F1})"));
        At(6.6f, () =>
        {
            MatchSetup.Mode = MatchMode.VsBot;
            GameEvents.OnFoul -= CountFoul;
            GameEvents.OnShotFired -= CountShot;
            GameEvents.OnShotClockChanged -= CountClock;
            _running = false;
        });
        return "started";
    }

    private static void At(float delay, System.Action action) => DG.Tweening.DOVirtual.DelayedCall(_t + delay, () =>
    {
        try
        {
            action();
        }
        catch (System.Exception e)
        {
            _log.AppendLine("ERROR " + e.Message);
            _report.Check(false, "step threw: " + e.Message);
        }
    }, true);

    private static TurnPhase PlayerPhase(PlayerShotController player) =>
        (TurnPhase)typeof(PlayerShotController).GetField("_phase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(player);

    private static void CountFoul(FoulInfo foul)
    {
        if (foul.Foul == FoulType.ShotClock)
            _clockFouls++;
    }

    private static void CountShot(ShotFeedback feedback) => _shots++;

    private static void CountClock(Side side, float remaining01, int seconds)
    {
        if (side == Side.Bot && remaining01 >= 0f)
            _botClockEvents++;
    }
}
