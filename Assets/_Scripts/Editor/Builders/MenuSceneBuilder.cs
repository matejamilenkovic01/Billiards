using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuSceneBuilder
{
    private const string Generated = UIPrefabBuilder.Generated;

    [MenuItem("Tools/Billiards/Builders/Menu Scene")]
    public static void Build()
    {
        // Load assets only after NewScene: a Single-mode new scene unloads unused assets loaded before it.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);

        MatchSceneBuilder.ApplyRoomLighting();
        Camera camera = MatchSceneBuilder.BuildCamera(config);
        MatchSceneBuilder.BuildVolume();

        var table = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.TablePath));
        table.transform.position = new Vector3(0f, config.TableHeight, 0f);
        // The rooms and baked reflections of all venues: MenuController shows the current division's (v3 V13).
        MatchSceneBuilder.SetVenues(table.GetComponent<VenueView>(), MatchSceneBuilder.BuildRooms(), MatchSceneBuilder.CreateProbe(config));

        var orbit = camera.gameObject.AddComponent<MenuTableOrbit>();
        UIPrefabBuilder.SetRef(orbit, "_table", table.GetComponent<TableView>());
        UIPrefabBuilder.SetRef(orbit, "_config", config);

        BuildUI();
        new GameObject("MenuController").AddComponent<MenuController>();

        UIPrefabBuilder.ApplyMinTextSizes(scene);
        EditorSceneManager.SaveScene(scene, ProjectSettingsBuilder.MenuScenePath);
        Debug.Log("MenuSceneBuilder: done");
    }

    // v4 V13 menu, in the order of what the player does: settings and coins on top, the rank badge (opens DIVISIONS),
    // the three objectives (ALL ›), PLAY across the width in the division's venue, CUES with the selected cue, and
    // under them the cue gap card or one line about the next cue.
    private static void BuildUI()
    {
        Canvas canvas = MatchSceneBuilder.CreateSceneCanvas("MenuUI", 0, out RectTransform safe);

        // Darkens the 3D backdrop so the menu reads clearly.
        RectTransform dim = UIPrefabBuilder.CreateRect("Dim", canvas.transform);
        dim.SetAsFirstSibling();
        UIPrefabBuilder.Stretch(dim, Vector4.zero);
        UIPrefabBuilder.AddImage(dim.gameObject, null, new Color(0.02f, 0.03f, 0.025f, 0.6f), false);

        GameObject settingsObject = UIPrefabBuilder.CreateButtonInstance(safe, null, UIPrefabBuilder.Icons + "/icon_gear.png", new Vector2(110f, 110f), 40, UIPrefabBuilder.ButtonColor.Square);
        UIPrefabBuilder.Anchor((RectTransform)settingsObject.transform, new Vector2(0f, 1f), new Vector2(90f, -95f), new Vector2(110f, 110f));
        UIPrefabBuilder.InstantiateCoinCounter(safe, new Vector2(1f, 1f), new Vector2(-175f, -95f));

        RectTransform title = UIPrefabBuilder.CreateRect("Title", safe);
        UIPrefabBuilder.Anchor(title, new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(1000f, 150f));
        TMP_Text main = UIPrefabBuilder.CreateText(title, "Main", "BILLIARDS", 118, UIPrefabBuilder.Gold, TextAlignmentOptions.Center);
        main.fontSharedMaterial = UIPrefabBuilder.OutlineFor(main);
        UIPrefabBuilder.Stretch(main.rectTransform, Vector4.zero);
        // A calm bob (v3 V7, F11): 5 px over a 2 s cycle.
        AddIdleMotion(title.gameObject, 0, 5f, 1.0f);

        Image rankPanel = UIPrefabBuilder.Panel(safe, "RankPanel", new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(984f, 250f), new Color(0f, 0f, 0f, 0.5f));
        RankBadgeView rank = UIPrefabBuilder.BuildRankBadge(rankPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-40f, 0f), 1f, true, true, true);
        ((RectTransform)rank.transform).sizeDelta = new Vector2(860f, 240f);
        TMP_Text road = UIPrefabBuilder.CreateText(rankPanel.transform, "Divisions", "DIVISIONS ›", 30, UIPrefabBuilder.TextMuted, TextAlignmentOptions.MidlineRight);
        road.fontSharedMaterial = UIPrefabBuilder.OutlineFor(road);
        UIPrefabBuilder.Anchor(road.rectTransform, new Vector2(1f, 1f), new Vector2(-110f, -208f), new Vector2(220f, 44f));

        RectTransform objectives = BuildObjectives(safe);

        // The pulse lives on a wrapper so it never fights the button's own press scale tween.
        RectTransform playPulse = UIPrefabBuilder.CreateRect("PlayPulse", safe);
        UIPrefabBuilder.Anchor(playPulse, new Vector2(0.5f, 1f), new Vector2(0f, -1262f), Vector2.zero);
        AddIdleMotion(playPulse.gameObject, 1, 1.03f, 0.7f);
        UIButton play = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.Play, playPulse, Vector2.zero);
        ((RectTransform)play.transform).sizeDelta = new Vector2(984f, 170f);
        TMP_Text playCaption = Caption(safe, "PlayCaption", "RANKED  ·  BASEMENT BAR  ·  VS CPU", -1378f);

        GameObject cuesObject = UIPrefabBuilder.CreateButtonInstance(safe, "CUES", UIPrefabBuilder.Icons + "/icon_star.png", new Vector2(290f, 110f), 40, UIPrefabBuilder.ButtonColor.Yellow);
        UIPrefabBuilder.Anchor((RectTransform)cuesObject.transform, new Vector2(0.5f, 1f), new Vector2(-347f, -1480f), new Vector2(290f, 110f));
        UIButton settings = settingsObject.GetComponent<UIButton>();
        UIButton cues = cuesObject.GetComponent<UIButton>();
        BuildSelectedCue(safe);
        BuildCueGap(safe);

        TMP_Text version = UIPrefabBuilder.CreateText(safe, "Version", "v0.0.0", 30, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.BottomRight);
        version.rectTransform.anchorMin = version.rectTransform.anchorMax = new Vector2(1f, 0f);
        version.rectTransform.pivot = new Vector2(1f, 0f);
        version.rectTransform.anchoredPosition = new Vector2(-32f, 22f);
        version.rectTransform.sizeDelta = new Vector2(300f, 46f);

        // FTUE coach marks on the badge and the objectives (v3 V13).
        // The balloon takes the nearest free spot: not over the title, PLAY, the other cards or the buttons.
        MatchSceneBuilder.BuildCoachMark(canvas.transform, new[] { CoachTarget.RankBadge, CoachTarget.Objectives, CoachTarget.Cues },
            new[] { rankPanel.rectTransform, objectives, (RectTransform)cuesObject.transform },
            new[]
            {
                title, rankPanel.rectTransform, (RectTransform)playPulse.GetChild(0), playCaption.rectTransform, objectives,
                (RectTransform)safe.Find("CueGap"), (RectTransform)cuesObject.transform,
                (RectTransform)safe.Find("SelectedCue"), (RectTransform)settingsObject.transform
            });
        MatchSceneBuilder.BuildToast();

        var view = canvas.gameObject.AddComponent<MainMenuView>();
        UIPrefabBuilder.SetRef(view, "_playButton", play);
        UIPrefabBuilder.SetRef(view, "_settingsButton", settings);
        UIPrefabBuilder.SetRef(view, "_cuesButton", cues);
        UIPrefabBuilder.SetRef(view, "_versionText", version);
        UIPrefabBuilder.SetRef(view, "_playCaption", playCaption);
    }

    private static RectTransform BuildObjectives(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("Objectives", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(0.5f, 1f), new Vector2(0f, -886f), new Vector2(984f, 532f));
        TMP_Text header = UIPrefabBuilder.CreateText(root, "Header", "OBJECTIVES", 34, UIPrefabBuilder.Gold, TextAlignmentOptions.MidlineLeft);
        header.fontSharedMaterial = UIPrefabBuilder.OutlineFor(header);
        UIPrefabBuilder.Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(190f, -28f), new Vector2(320f, 44f));
        GameObject all = UIPrefabBuilder.CreateButtonInstance(root, "ALL ›", null, new Vector2(190f, 56f), 30, UIPrefabBuilder.ButtonColor.Blue);
        UIPrefabBuilder.Anchor((RectTransform)all.transform, new Vector2(1f, 1f), new Vector2(-110f, -28f), new Vector2(190f, 56f));
        var rows = new ObjectiveRowView[ObjectiveCatalog.Slots];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = UIPrefabBuilder.BuildObjectiveRow(root, new Vector2(0.5f, 1f), new Vector2(0f, -139f - i * 156f), new Vector2(984f, 150f));
        TMP_Text empty = UIPrefabBuilder.CreateText(root, "Empty", "All objectives done!", 32, UIPrefabBuilder.TextMuted, TextAlignmentOptions.Center);
        UIPrefabBuilder.Anchor(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 50f));

        var view = root.gameObject.AddComponent<ObjectivesPanelView>();
        UIPrefabBuilder.SetArray(view, "_rows", rows);
        UIPrefabBuilder.SetRef(view, "_empty", empty);
        UIPrefabBuilder.SetRef(view, "_allButton", all.GetComponent<UIButton>());
        return root;
    }

    // The cue gap card (the division's cue is better than yours: coins, TRY, a pointer to the objectives that pay in
    // a loss) or, when there is no gap, one line about the next cue.
    private static void BuildCueGap(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("CueGap", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(0.5f, 1f), new Vector2(0f, -1655f), new Vector2(984f, 210f));
        UIPrefabBuilder.AddImage(root.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;

        Image card = UIPrefabBuilder.Panel(root, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(984f, 210f), new Color(0f, 0f, 0f, 0.5f));
        TMP_Text title = UIPrefabBuilder.CreateText(card.transform, "Title", "Gold opponents use the Pro Cue", 34, UIPrefabBuilder.Gold, TextAlignmentOptions.MidlineLeft);
        title.fontSharedMaterial = UIPrefabBuilder.OutlineFor(title);
        title.enableAutoSizing = true;
        title.fontSizeMin = 28f;
        title.fontSizeMax = 34f;
        UIPrefabBuilder.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(30f + 330f, -36f), new Vector2(660f, 48f));
        RectTransform coin = UIPrefabBuilder.CreateRect("Coin", card.transform);
        UIPrefabBuilder.Anchor(coin, new Vector2(0f, 1f), new Vector2(30f + 20f, -100f), new Vector2(40f, 40f));
        UIPrefabBuilder.AddImage(coin.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text amount = UIPrefabBuilder.CreateText(card.transform, "Amount", "412 / 1000", 34, UIPrefabBuilder.TextLight, TextAlignmentOptions.MidlineLeft);
        amount.fontSharedMaterial = UIPrefabBuilder.OutlineFor(amount);
        UIPrefabBuilder.Anchor(amount.rectTransform, new Vector2(0f, 1f), new Vector2(90f + 100f, -100f), new Vector2(200f, 44f));
        RectTransform track = UIPrefabBuilder.CreateRect("Bar", card.transform);
        track.anchorMin = track.anchorMax = new Vector2(0f, 1f);
        track.pivot = new Vector2(0f, 0.5f);
        track.anchoredPosition = new Vector2(300f, -100f);
        track.sizeDelta = new Vector2(380f, 16f);
        UIPrefabBuilder.AddImage(track.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), new Color(0f, 0f, 0f, 0.55f), false);
        RectTransform fillRect = UIPrefabBuilder.CreateRect("Fill", track);
        UIPrefabBuilder.Stretch(fillRect, Vector4.zero);
        Image fill = UIPrefabBuilder.AddImage(fillRect.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), UIPrefabBuilder.Gold, false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        TMP_Text hint = UIPrefabBuilder.CreateText(card.transform, "Hint", "Objectives with a coin pay even when you lose", 28, UIPrefabBuilder.TextMuted, TextAlignmentOptions.MidlineLeft);
        hint.fontSharedMaterial = UIPrefabBuilder.OutlineFor(hint);
        UIPrefabBuilder.Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(30f + 460f, 42f), new Vector2(920f, 40f));
        GameObject tryObject = UIPrefabBuilder.CreateButtonInstance(card.transform, "TRY", null, new Vector2(220f, 84f), 40, UIPrefabBuilder.ButtonColor.Green);
        UIPrefabBuilder.Anchor((RectTransform)tryObject.transform, new Vector2(1f, 1f), new Vector2(-135f, -80f), new Vector2(220f, 84f));

        Image line = UIPrefabBuilder.Panel(root, "Line", new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(984f, 70f), new Color(0f, 0f, 0f, 0.45f));
        TMP_Text lineText = UIPrefabBuilder.CreateText(line.transform, "Text", "Next cue: Pro Cue  ·  412 / 1000", 32, UIPrefabBuilder.TextLight, TextAlignmentOptions.Center);
        lineText.fontSharedMaterial = UIPrefabBuilder.OutlineFor(lineText);
        UIPrefabBuilder.Stretch(lineText.rectTransform, new Vector4(20f, 0f, 20f, 0f));

        var view = root.gameObject.AddComponent<CueGapView>();
        UIPrefabBuilder.SetRef(view, "_card", card.gameObject);
        UIPrefabBuilder.SetRef(view, "_title", title);
        UIPrefabBuilder.SetRef(view, "_fill", fill);
        UIPrefabBuilder.SetRef(view, "_amount", amount);
        UIPrefabBuilder.SetRef(view, "_hint", hint);
        UIPrefabBuilder.SetRef(view, "_tryButton", tryObject.GetComponent<UIButton>());
        UIPrefabBuilder.SetRef(view, "_line", line.gameObject);
        UIPrefabBuilder.SetRef(view, "_lineText", lineText);
    }

    // "YOUR CUE  [cue]  House Cue  OVR 44" next to CUES; tapping opens the cues.
    private static void BuildSelectedCue(RectTransform parent)
    {
        Image card = UIPrefabBuilder.Panel(parent, "SelectedCue", new Vector2(0.5f, 1f), new Vector2(144f, -1480f), new Vector2(680f, 110f), new Color(0f, 0f, 0f, 0.45f));
        card.raycastTarget = true;

        TMP_Text caption = UIPrefabBuilder.CreateText(card.transform, "Caption", "YOUR CUE", 26, UIPrefabBuilder.TextMuted, TextAlignmentOptions.MidlineLeft);
        UIPrefabBuilder.Anchor(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f + 90f, 28f), new Vector2(180f, 34f));
        TMP_Text name = UIPrefabBuilder.CreateText(card.transform, "Name", "House Cue", 38, UIPrefabBuilder.TextLight, TextAlignmentOptions.MidlineLeft);
        name.fontSharedMaterial = UIPrefabBuilder.OutlineFor(name);
        UIPrefabBuilder.Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f + 150f, -16f), new Vector2(300f, 48f));

        RectTransform cue = UIPrefabBuilder.CreateRect("Cue", card.transform);
        UIPrefabBuilder.Anchor(cue, new Vector2(0.5f, 0.5f), new Vector2(70f, 0f), new Vector2(230f, 24f));
        Image cueImage = UIPrefabBuilder.AddImage(cue.gameObject, UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.CueSpritePath(DataAssetBuilder.CueSpecs[0].Id)), Color.white, false);
        cueImage.preserveAspect = false;

        TMP_Text rating = UIPrefabBuilder.CreateText(card.transform, "Rating", "OVR 44", 36, Color.white, TextAlignmentOptions.MidlineRight);
        rating.fontSharedMaterial = UIPrefabBuilder.OutlineFor(rating);
        UIPrefabBuilder.Anchor(rating.rectTransform, new Vector2(1f, 0.5f), new Vector2(-95f, 0f), new Vector2(170f, 50f));

        var view = card.gameObject.AddComponent<SelectedCueView>();
        UIPrefabBuilder.SetRef(view, "_cueImage", cueImage);
        UIPrefabBuilder.SetRef(view, "_name", name);
        UIPrefabBuilder.SetRef(view, "_rating", rating);
    }

    private static TMP_Text Caption(RectTransform parent, string name, string text, float y)
    {
        TMP_Text caption = UIPrefabBuilder.CreateText(parent, name, text, 28, UIPrefabBuilder.TextMuted, TextAlignmentOptions.Center);
        caption.fontSharedMaterial = UIPrefabBuilder.OutlineFor(caption);
        UIPrefabBuilder.Anchor(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1000f, 40f));
        return caption;
    }

    // mode: 0 = bob, 1 = pulse (IdleMotion.Mode)
    private static void AddIdleMotion(GameObject go, int mode, float amount, float halfPeriod)
    {
        var motion = go.AddComponent<IdleMotion>();
        var serialized = new SerializedObject(motion);
        serialized.FindProperty("_mode").enumValueIndex = mode;
        serialized.FindProperty("_amount").floatValue = amount;
        serialized.FindProperty("_halfPeriod").floatValue = halfPeriod;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
