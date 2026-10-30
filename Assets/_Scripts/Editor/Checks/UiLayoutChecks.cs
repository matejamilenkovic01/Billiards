using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// v4 V8 (F15, F19): the screen layout in numbers, Play mode, from what is really drawn. Analyse(state) looks at the
// screen now: every visible UGUI text (its rendered text bounds) and every visible button, on canvases not covered by
// a dim above them. NoOverlap: two of them (neither inside the other) overlapping by more than 2 px. MinTextSize: the
// rendered size after autosize and scale below UIPrefabBuilder.MinSizeFor. MissingGlyphs: a character neither Rubik
// nor its symbols fallback has (it would show as a box), or a text not on the Rubik fonts; also texts cut off by
// their box. RunMatch() / RunMenu() step through the screen states (Match / Menu scene) and analyse each; Status()
// returns the report when they are done.
public static class UiLayoutChecks
{
    private const float OverlapTolerance = 2f;

    private struct Item
    {
        public string Name;
        public Transform Transform;
        public Rect Rect;
    }

    private static readonly List<string> _overlaps = new();
    private static readonly List<string> _small = new();
    private static readonly List<string> _glyphs = new();
    private static readonly List<string> _states = new();
    private static int _textCount;
    private static float _smallest = float.MaxValue;
    private static string _smallestName;
    private static bool _running;
    private static string _capturePrefix;

    public static void Clear()
    {
        _overlaps.Clear();
        _small.Clear();
        _glyphs.Clear();
        _states.Clear();
        _textCount = 0;
        _smallest = float.MaxValue;
        _smallestName = null;
    }

    public static string NoOverlap() => Summary("UiLayoutChecks.NoOverlap", _overlaps);

    public static string MinTextSize() => Summary("UiLayoutChecks.MinTextSize", _small) + $" (smallest {_smallest:F1} px: {_smallestName})";

    public static string MissingGlyphs() => Summary("UiLayoutChecks.MissingGlyphs", _glyphs);

    public static string Report() =>
        $"{_states.Count} states, {_textCount} texts: {string.Join(", ", _states)}\n{NoOverlap()}\n{MinTextSize()}\n{MissingGlyphs()}";

    public static string Status() => _running ? $"running ({_states.Count} states so far)" : "DONE\n" + Report();

    private static string Summary(string name, List<string> failures)
    {
        var report = new StringBuilder($"{name}: {(failures.Count == 0 ? "PASS" : "FAIL")} ({failures.Count})");
        foreach (string failure in failures.Distinct().Take(60))
            report.Append("\n  ").Append(failure);
        return report.ToString();
    }

    // The current screen. Also saves Screenshots/<prefix><state>.png when RunMatch/RunMenu got a prefix.
    public static string Analyse(string state)
    {
        _states.Add(state);
        var texts = new List<Item>();
        var buttons = new List<Item>();
        Collect(texts, buttons);
        int before = _overlaps.Count + _small.Count + _glyphs.Count;

        var items = texts.Concat(buttons).ToList();
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
        {
            Item a = items[i], b = items[j];
            if (a.Transform.IsChildOf(b.Transform) || b.Transform.IsChildOf(a.Transform))
                continue;
            float w = Mathf.Min(a.Rect.xMax, b.Rect.xMax) - Mathf.Max(a.Rect.xMin, b.Rect.xMin);
            float h = Mathf.Min(a.Rect.yMax, b.Rect.yMax) - Mathf.Max(a.Rect.yMin, b.Rect.yMin);
            if (w > OverlapTolerance && h > OverlapTolerance)
                _overlaps.Add($"[{state}] {a.Name} × {b.Name} ({w:F0}×{h:F0} px)");
        }
        if (!string.IsNullOrEmpty(_capturePrefix))
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath($"Screenshots/{_capturePrefix}{state}.png"));
        int found = _overlaps.Count + _small.Count + _glyphs.Count - before;
        return $"{state}: {texts.Count} texts, {buttons.Count} buttons, {found} problems";
    }

    private static void Collect(List<Item> texts, List<Item> buttons)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas && c.isActiveAndEnabled).OrderByDescending(c => c.sortingOrder).ToArray();
        // Canvases under a visible full-screen dim of a higher canvas are covered.
        int coveredBelow = int.MinValue;
        foreach (Canvas canvas in canvases)
        {
            if (canvas.sortingOrder < coveredBelow)
                continue;
            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(false);
            // Inside this canvas, what is drawn before its last visible dim is covered too.
            int firstVisible = 0;
            for (int g = graphics.Length - 1; g >= 0; g--)
            {
                if (IsDim(canvas, graphics[g]))
                {
                    firstVisible = g;
                    coveredBelow = Mathf.Max(coveredBelow, canvas.sortingOrder);
                    break;
                }
            }
            for (int g = firstVisible; g < graphics.Length; g++)
            {
                if (graphics[g] is not TextMeshProUGUI text || Alpha(text) < 0.02f)
                    continue;
                text.ForceMeshUpdate();
                string parsed = text.GetParsedText();
                if (string.IsNullOrWhiteSpace(parsed) || text.textInfo.characterCount == 0)
                    continue;
                if (!TryScreenRect(canvas, text.rectTransform, text.textBounds, out Rect rect))
                    continue;
                _textCount++;
                string name = PathOf(text.transform);
                texts.Add(new Item { Name = $"\"{Short(parsed)}\" ({name})", Transform = text.transform, Rect = rect });
                CheckText(canvas, text, parsed, name);
            }
            // The floating message panels (F15): a toast, the coach-mark balloon, the lesson card.
            var panels = new List<RectTransform>();
            panels.AddRange(canvas.GetComponentsInChildren<ToastView>(false).Select(v => (RectTransform)v.transform));
            panels.AddRange(canvas.GetComponentsInChildren<CoachMarkView>(false).Select(v => (RectTransform)v.transform.Find("Balloon")));
            panels.AddRange(canvas.GetComponentsInChildren<TutorialView>(false).Select(v => (RectTransform)v.transform.Find("Card")));
            foreach (RectTransform panel in panels)
            {
                if (panel == null || Alpha(panel.GetComponent<Graphic>()) < 0.02f)
                    continue;
                if (TryScreenRect(canvas, panel, new Bounds(panel.rect.center, panel.rect.size), out Rect rect))
                    buttons.Add(new Item { Name = $"[panel {PathOf(panel)}]", Transform = panel, Rect = rect });
            }
            foreach (UIButton button in canvas.GetComponentsInChildren<UIButton>(false))
            {
                var rectTransform = (RectTransform)button.transform;
                if (Alpha(button.GetComponent<Graphic>() ?? button.GetComponentInChildren<Graphic>()) < 0.02f || !IsAfter(graphics, firstVisible, button))
                    continue;
                if (TryScreenRect(canvas, rectTransform, new Bounds(rectTransform.rect.center, rectTransform.rect.size), out Rect rect))
                    buttons.Add(new Item { Name = $"[button {PathOf(button.transform)}]", Transform = button.transform, Rect = rect });
            }
        }
    }

    private static void CheckText(Canvas canvas, TextMeshProUGUI text, string parsed, string name)
    {
        float size = text.fontSize * text.transform.lossyScale.y / canvas.transform.lossyScale.y;
        float min = UIPrefabBuilder.MinSizeFor(text);
        if (size < _smallest)
        {
            _smallest = size;
            _smallestName = $"\"{Short(parsed)}\" ({name})";
        }
        if (size < min - 0.5f)
            _small.Add($"\"{Short(parsed)}\" ({name}): {size:F1} < {min:F0}");

        if (text.font != UIPrefabBuilder.Font && text.font != UIPrefabBuilder.BodyFont)
            _glyphs.Add($"\"{Short(parsed)}\" ({name}): font {(text.font != null ? text.font.name : "none")}");
        foreach (char c in parsed)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c) || text.font == null)
                continue;
            if (!text.font.HasCharacter(c, true, false))
                _glyphs.Add($"\"{Short(parsed)}\" ({name}): U+{(int)c:X4} '{c}'");
        }
        if (text.overflowMode != TextOverflowModes.Overflow && text.isTextOverflowing)
            _glyphs.Add($"\"{Short(parsed)}\" ({name}): cut off by its box");
    }

    private static bool IsDim(Canvas canvas, Graphic graphic)
    {
        if (graphic is not Image || Alpha(graphic) < 0.4f || graphic.color.a < 0.4f)
            return false;
        if (!TryScreenRect(canvas, graphic.rectTransform, new Bounds(graphic.rectTransform.rect.center, graphic.rectTransform.rect.size), out Rect rect, false))
            return false;
        Vector2 screen = ScreenSize(canvas);
        return rect.width >= screen.x * 0.95f && rect.height >= screen.y * 0.9f;
    }

    private static bool IsAfter(Graphic[] graphics, int firstVisible, UIButton button)
    {
        if (firstVisible == 0)
            return true;
        for (int g = firstVisible; g < graphics.Length; g++)
        {
            if (graphics[g].transform.IsChildOf(button.transform))
                return true;
        }
        return false;
    }

    // The effective alpha: the graphic's colour times every CanvasGroup above it.
    private static float Alpha(Graphic graphic)
    {
        if (graphic == null || !graphic.isActiveAndEnabled)
            return 0f;
        float alpha = graphic.color.a;
        for (Transform t = graphic.transform; t != null; t = t.parent)
        {
            if (t.TryGetComponent(out CanvasGroup group))
            {
                alpha *= group.alpha;
                if (group.ignoreParentGroups)
                    break;
            }
            if (t.localScale.x * t.localScale.y < 0.01f)
                return 0f;
        }
        return alpha;
    }

    private static Vector2 ScreenSize(Canvas canvas) => new(Screen.width / canvas.scaleFactor, Screen.height / canvas.scaleFactor);

    // Local bounds -> reference pixels on screen, clipped by any RectMask2D above (false when fully clipped).
    private static bool TryScreenRect(Canvas canvas, RectTransform rectTransform, Bounds local, out Rect rect, bool clip = true)
    {
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        rect = ToScreen(canvas, camera, rectTransform, local.min, local.max);
        if (!clip)
            return true;
        for (Transform t = rectTransform.parent; t != null; t = t.parent)
        {
            if (!t.TryGetComponent(out RectMask2D mask) || !mask.enabled)
                continue;
            var maskRect = (RectTransform)t;
            Rect clipRect = ToScreen(canvas, camera, maskRect, maskRect.rect.min, maskRect.rect.max);
            float xMin = Mathf.Max(rect.xMin, clipRect.xMin), yMin = Mathf.Max(rect.yMin, clipRect.yMin);
            float xMax = Mathf.Min(rect.xMax, clipRect.xMax), yMax = Mathf.Min(rect.yMax, clipRect.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return false;
            rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
        return true;
    }

    private static Rect ToScreen(Canvas canvas, Camera camera, RectTransform rectTransform, Vector2 min, Vector2 max)
    {
        Vector2 a = new(float.MaxValue, float.MaxValue), b = new(float.MinValue, float.MinValue);
        foreach (Vector2 corner in new[] { min, max, new Vector2(min.x, max.y), new Vector2(max.x, min.y) })
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(corner)) / canvas.scaleFactor;
            a = Vector2.Min(a, screen);
            b = Vector2.Max(b, screen);
        }
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        for (int i = 0; i < 2 && t.parent != null; i++)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static string Short(string text)
    {
        text = text.Replace('\n', ' ');
        return text.Length > 28 ? text.Substring(0, 28) + "…" : text;
    }

    #region State drivers
    private static readonly List<(float Wait, string State, Action Setup)> _steps = new();

    private static void Step(string state, float wait, Action setup) => _steps.Add((wait, state, setup));

    // Time passes, nothing is analysed.
    private static void Wait(float seconds) => _steps.Add((seconds, null, () => { }));

    private static string Run(string prefix)
    {
        Application.runInBackground = true;
        Clear();
        _capturePrefix = prefix;
        _running = true;
        float time = 0.3f;
        var steps = _steps.ToArray();
        _steps.Clear();
        foreach (var (wait, state, setup) in steps)
        {
            float at = time;
            DG.Tweening.DOVirtual.DelayedCall(at, () =>
            {
                try { setup(); }
                catch (Exception e) { _glyphs.Add($"[{state}] setup failed: {e.Message}"); }
            }, true);
            time += wait;
            if (state == null)
                continue;
            DG.Tweening.DOVirtual.DelayedCall(time, () =>
            {
                try { Analyse(state); }
                catch (Exception e) { _glyphs.Add($"[{state}] analyse failed: {e.Message}"); }
            }, true);
            time += 0.15f;
        }
        DG.Tweening.DOVirtual.DelayedCall(time + 0.2f, () => _running = false, true);
        return $"queued {steps.Length} states, ~{time:F0} s";
    }

    // A celebration for the screens: Silver's, Grand Champion's or a finished set's.
    private static CelebrationData Celebration(ProfileService profile, CelebrationKind kind)
    {
        DivisionDefinition silver = profile.Divisions.At(1);
        RankViewData Rank(int division, int stage) => profile.RankView(new RankState { Division = division, Stage = stage });
        return kind switch
        {
            CelebrationKind.DivisionUp => new CelebrationData
            {
                Kind = kind, Title = "SILVER DIVISION", Color = silver.Color, Before = Rank(0, 2), After = Rank(1, 0), Rules = silver.Rules, RuleText = silver.RuleText,
                VenueName = silver.VenueName, VenueThumbnail = silver.Venue != null ? silver.Venue.Thumbnail : null, Reward = 150
            },
            CelebrationKind.ChampionUp => new CelebrationData
            {
                Kind = kind, Title = "GRAND CHAMPION", Color = new Color(1f, 0.84f, 0.3f), Before = Rank(4, 2),
                After = profile.RankView(new RankState { Division = 4, Stage = 2, IsChampion = true }), Rules = RuleSetId.CallAllOpponentEight,
                RuleText = profile.Divisions.ChampionRuleText, VenueName = profile.Divisions.At(4).VenueName,
                VenueThumbnail = profile.Divisions.At(4).Venue != null ? profile.Divisions.At(4).Venue.Thumbnail : null, Reward = 1000
            },
            _ => new CelebrationData { Kind = kind, Title = "SILVER OBJECTIVES COMPLETE", Color = silver.Color, Reward = 150 }
        };
    }

    private static void Popup<T>(object data = null) where T : PopupBase
    {
        PopupManager.Instance.HideAllImmediate();
        PopupManager.Instance.Show<T>(data);
    }

    // Match scene, Play mode. prefix: screenshot name prefix ("" = no screenshots).
    public static string RunMatch(string prefix = "")
    {
        MatchController match = MatchController.Instance;
        if (match == null || !Application.isPlaying)
            return "needs Play mode in the Match scene";
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerShotController>();
        var spin = UnityEngine.Object.FindFirstObjectByType<SpinControlView>(FindObjectsInactive.Include);
        int[] numbers = { 0, 1, 9, 8 };
        Vector2[] positions = { new(-0.18f, -0.35f), new(0.3f, 0.55f), new(-0.35f, 0.75f), new(0.25f, -0.8f) };

        Step("vs_intro", 1.2f, () => { MatchController.DebugSetRules(RuleSetId.CallEight); MatchController.DebugStart(true); });
        // The intro and the coin flip run out and the first turn begins, as in a real match.
        Wait(match.Config.VsIntroDuration + match.Config.CoinFlipDuration);
        Step("hud_aim", 1.6f, () =>
        {
            // In a real match the intro is gone (1.9 s) long before the first turn (3.2 s); the Editor's uneven frame
            // times can stretch it here, so end it as the match would.
            var intro = UnityEngine.Object.FindFirstObjectByType<VsIntroView>();
            var sequence = (DG.Tweening.Tween)typeof(VsIntroView).GetField("_sequence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(intro);
            if (sequence != null)
                DG.Tweening.TweenExtensions.Kill(sequence);
            intro.GetComponentInChildren<CanvasGroup>().alpha = 0f;
            PopupManager.Instance.HideAllImmediate();
            MatchController.DebugSetRules(RuleSetId.Standard);
            match.SetupTable(BallGroup.Solids, numbers, positions);
            player.DebugSetAim(new Vector2(0.4f, 1f));
        });
        Step("hud_spin_overlay", 0.8f, () => spin.DebugOpen(true));
        Step("hud_place", 1.4f, () => { spin.DebugOpen(false); match.SetupTable(BallGroup.Solids, numbers, positions, true); });
        Step("hud_call", 1.8f, () =>
        {
            MatchController.DebugSetRules(RuleSetId.CallEight);
            MatchController.DebugSetupOnEight(true, new Vector2(-0.15f, -0.45f), new Vector2(0.25f, 0.55f));
            player.DebugSetAim((match.Balls[8].Pos - match.Balls[0].Pos).normalized);
        });
        Step("hud_called", 0.8f, () => GameEvents.RaisePocketCallRequested(5));
        Step("hud_bot_choice", 1.8f, () =>
        {
            MatchController.DebugSetRules(RuleSetId.BlockedPocket);
            MatchController.DebugSetupOnEight(false, new Vector2(-0.2f, -0.3f), new Vector2(0.3f, 0.6f));
        });
        Step("hud_watch", 0.6f, () =>
        {
            GameEvents.RaiseOpponentEightPocketChosen(5);
            MatchController.DebugSetRules(RuleSetId.Standard);
            match.SetupTable(BallGroup.Solids, numbers, positions, false, Side.Bot);
        });
        Step("foul_banner", 0.9f, () =>
        {
            match.SetupTable(BallGroup.Solids, numbers, positions);
            GameEvents.RaiseFoul(new FoulInfo { Foul = FoulType.NoRailAfterContact, ByPlayer = true });
        });
        Step("toast", 0.6f, () => GameEvents.RaiseToastRequested(new ToastMessage("Lost a star. You can't drop below a checkpoint.")));
        // After the toast has gone.
        Step("pause", 2.4f, () => Popup<PausePopup>());
        Step("settings", 0.9f, () => Popup<SettingsPopup>(new SettingsPopupData(1f, 1f, true, false)));
        foreach (RuleSetId rules in Enum.GetValues(typeof(RuleSetId)))
            Step("rules_" + rules, 0.9f, () => Popup<RulesPopup>(new RulesPopupData(RuleId.None, RuleTextOf(rules), rules)));
        foreach ((string name, MatchResult result) in Results())
            Step("result_" + name, 3.6f, () => Popup<MatchResultPopup>(new MatchResultPopupData(result, result.NewBalance - result.CoinDelta)));

        // v4 V9: the practice card (the longest difference line) and the comparison screen.
        Step("practice_card", 1.2f, () =>
        {
            PopupManager.Instance.HideAllImmediate();
            match.SetupTable(BallGroup.Solids, numbers, positions);
            GameEvents.RaisePracticeStepShown(new PracticeStepInfo
            {
                Index = 0, Count = 3, Header = "TRYING: CHAMPION CUE",
                Diff = "Green +100% · Bar 42% slower · Aim line ×4 · Power +1.7 m/s",
                Title = "LONG STRAIGHT", Instruction = "Feel the bar: see how wide the green stays at full power. Pull to HARD."
            });
            GameEvents.RaiseToastRequested(new ToastMessage("PERFECT release!", ToastStyle.Perfect));
        });
        Step("practice_off", 0.4f, () => GameEvents.RaiseCueTryFinished("x", 0, 0));
        Step("try_compare", 1.4f, () =>
        {
            ProfileSnapshot snapshot = ProfileService.Instance.GetSnapshot();
            Popup<CueTryResultPopup>(new CueTryResultPopupData(new CueTryResultData
            {
                Tried = snapshot.Find("champion"), Selected = snapshot.SelectedCue, Shots = 3, Perfect = 2, Good = 1
            }));
        });

        // The tutorial's lesson card, coach mark and messages, from its config.
        var config = UnityEditor.AssetDatabase.LoadAssetAtPath<TutorialConfig>(DataAssetBuilder.TutorialConfigPath);
        for (int i = 0; i < config.Steps.Count; i++)
        {
            int index = i;
            TutorialStep step = config.Steps[i];
            Step($"tutorial_{i + 1}", 1.2f, () =>
            {
                PopupManager.Instance.HideAllImmediate();
                match.SetupTable(BallGroup.Solids, numbers, positions, step.BallInHand);
                GameEvents.RaiseTutorialStepShown(new TutorialStepInfo { Index = index, Count = config.Steps.Count, Title = step.Title, Instruction = step.Instruction });
                if (step.Coach != CoachTarget.None)
                    GameEvents.RaiseCoachMarkRequested(step.Coach, step.CoachText);
            });
            Step($"tutorial_{i + 1}_message", 0.6f, () => GameEvents.RaiseToastRequested(new ToastMessage(step.Success, ToastStyle.Good)));
        }
        Step("end", 0.3f, () => MatchController.DebugSetRules(RuleSetId.Standard));
        return Run(prefix);
    }

    private static string RuleTextOf(RuleSetId rules)
    {
        foreach (DataAssetBuilder.DivisionSpec division in DataAssetBuilder.DivisionSpecs)
        {
            if (division.Rules == rules)
                return division.RuleText;
        }
        return DataAssetBuilder.ChampionRuleText;
    }

    private static IEnumerable<(string, MatchResult)> Results()
    {
        ProfileService profile = ProfileService.Instance;
        RankViewData Rank(int division, int stage, int stars) => profile.RankView(new RankState { Division = division, Stage = stage, Stars = stars });
        DivisionDefinition silver = profile.Divisions.At(1);
        OpponentViewData next = silver.Opponents[0].ToView(silver);
        var objectives = new List<ObjectiveViewData>
        {
            new() { Id = "a", Text = "Use FINE AIM and pot the ball (3 times)", Progress = 3, Target = 3, Reward = 50 },
            new() { Id = "b", Text = "Win with 4+ opponent balls still on the table", Progress = 1, Target = 1, Reward = 140 },
            new() { Id = "c", Text = "Bank shot: pot a ball after it hits a cushion", Progress = 1, Target = 1, Reward = 120 }
        };
        MatchResult Make(bool won, int delta, int balance, RankViewData before, RankViewData after, int stars) => new()
        {
            Won = won, Title = won ? "YOU WIN!" : "YOU LOSE", Reason = won ? "You pocketed the 8-ball!" : "The 8-ball went in before your group was cleared",
            CoinDelta = delta, NewBalance = balance, RankBeforeView = before, RankAfterView = after, StarsGained = stars,
            NextOpponent = next, NextDivisionName = silver.Name.ToUpperInvariant() + " DIVISION", NextVenueName = silver.VenueName
        };
        yield return ("win", Make(true, 40, 340, Rank(0, 0, 0), Rank(0, 0, 1), 1));
        yield return ("loss", Make(false, 8, 340, Rank(1, 1, 1), Rank(1, 1, 0), -1));
        MatchResult stage = Make(true, 40, 420, Rank(0, 0, 1), Rank(0, 1, 0), 1);
        stage.StageUp = true;
        stage.StageRewardCoins = 50;
        stage.RankRewardCoins = 50;
        yield return ("stage_up", stage);
        MatchResult division = Make(true, 30, 600, Rank(0, 2, 1), Rank(1, 0, 0), 1);
        division.StageUp = division.NewDivision = true;
        division.NewVenueName = "Downtown Hall";
        division.DivisionRewardCoins = division.RankRewardCoins = 150;
        division.Celebration = Celebration(profile, CelebrationKind.DivisionUp);
        yield return ("new_division", division);
        MatchResult many = Make(true, 40, 420, Rank(0, 1, 0), Rank(0, 1, 1), 1);
        many.CueAffordable = "Club Cue";
        many.ObjectivesCompleted = objectives;
        yield return ("objectives", many);
        MatchResult both = Make(true, 40, 520, Rank(0, 0, 1), Rank(0, 1, 0), 1);
        both.StageUp = true;
        both.StageRewardCoins = both.RankRewardCoins = 50;
        both.ObjectivesCompleted = objectives;
        yield return ("objectives_stage", both);
    }

    // Menu scene, Play mode: new player, CLAIM, every objective done, Grand Champion, the popups. Changes the profile:
    // back it up first (EconomyChecks.BackupProfileToFile) and restore it after.
    public static string RunMenu(string prefix = "")
    {
        ProfileService profile = ProfileService.Instance;
        if (profile == null || !Application.isPlaying || UnityEngine.Object.FindFirstObjectByType<MainMenuView>() == null)
            return "needs Play mode in the Menu scene";
        // `done` of the objectives on the menu are finished, the others at 0.
        void Objectives(int done)
        {
            int slot = 0;
            foreach (ObjectiveDefinition objective in profile.VisibleObjectives())
                PlayerPrefs.SetInt(SaveKeys.ObjectiveProgress(objective.Id), slot++ < done ? objective.Target : 0);
            profile.RaiseChanged();
        }

        Step("menu_new", 1.2f, () =>
        {
            PopupManager.Instance.HideAllImmediate();
            profile.ResetProfile();
            PlayerPrefs.SetInt(SaveKeys.TutorialDone, 1);
            PlayerPrefs.SetInt(SaveKeys.RulesSeen, 1);
            PlayerPrefs.SetInt(SaveKeys.HintsShown, -1);
            profile.RaiseChanged();
        });
        Step("menu_claim", 1.0f, () => Objectives(1));
        Step("menu_all_done", 1.0f, () => Objectives(ObjectiveCatalog.Slots));
        Step("menu_coach_objectives", 0.8f, () => GameEvents.RaiseCoachMarkRequested(CoachTarget.Objectives, "Finish objectives for coins"));
        Step("menu_cue_gap", 1.0f, () =>
        {
            profile.ResetProfile();
            PlayerPrefs.SetInt(SaveKeys.TutorialDone, 1);
            profile.SetRank(new RankState { Division = 2, Stage = 0, Stars = 0 });
            PlayerPrefs.SetInt(SaveKeys.Coins, 412);
            profile.RaiseChanged();
        });
        Step("menu_cue_gap_claim", 1.0f, () => Objectives(1));
        Step("menu_champion", 1.0f, () =>
        {
            GameEvents.RaiseCoachMarkCleared();
            Objectives(0);
            profile.SetRank(new RankState { Division = DataAssetBuilder.DivisionSpecs.Length - 1, Stage = DivisionCatalog.StagesPerDivision - 1, IsChampion = true, ChampionStars = 12 });
            profile.RaiseChanged();
        });
        Step("cues", 1.0f, () => Popup<CuesPopup>(new CuesPopupData(profile.GetSnapshot())));
        Step("settings_menu", 0.9f, () => Popup<SettingsPopup>(new SettingsPopupData(1f, 1f, true, true)));
        Step("divisions_bronze", 1.2f, () =>
        {
            profile.ResetProfile();
            Popup<DivisionsPopup>(new DivisionsPopupData(profile.DivisionsView()));
        });
        Step("divisions_gold", 1.2f, () =>
        {
            profile.SetRank(new RankState { Division = 2, Stage = 1, Stars = 1 });
            Popup<DivisionsPopup>(new DivisionsPopupData(profile.DivisionsView()));
        });
        Step("objectives_silver", 1.0f, () =>
        {
            profile.SetRank(new RankState { Division = 1, Stage = 1, Stars = 1 });
            Popup<ObjectivesPopup>(new ObjectivesPopupData(profile.ObjectivesView()));
        });
        Step("objectives_locked", 1.0f, () =>
        {
            ObjectivesViewData view = profile.ObjectivesView();
            view.CurrentTab = view.Sets.Count - 1;
            Popup<ObjectivesPopup>(new ObjectivesPopupData(view));
        });
        Step("celebration_division", 4.0f, () => Popup<CelebrationPopup>(new CelebrationPopupData(Celebration(profile, CelebrationKind.DivisionUp))));
        Step("celebration_champion", 4.0f, () => Popup<CelebrationPopup>(new CelebrationPopupData(Celebration(profile, CelebrationKind.ChampionUp))));
        Step("celebration_set", 3.0f, () => Popup<CelebrationPopup>(new CelebrationPopupData(Celebration(profile, CelebrationKind.SetComplete))));
        Step("end", 0.3f, () => PopupManager.Instance.HideAllImmediate());
        return Run(prefix);
    }
    #endregion
}
