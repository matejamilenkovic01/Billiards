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

    // v3 V13 menu: settings and coins on top, the rank badge (opens the Ranked Road), PLAY in the division's venue,
    // the three objectives, the next cue, CUES and the selected cue.
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

        Image rankPanel = UIPrefabBuilder.Panel(safe, "RankPanel", new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(960f, 250f), new Color(0f, 0f, 0f, 0.5f));
        RankBadgeView rank = UIPrefabBuilder.BuildRankBadge(rankPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-40f, 0f), 1f, true, true, true);
        ((RectTransform)rank.transform).sizeDelta = new Vector2(860f, 240f);
        TMP_Text road = UIPrefabBuilder.CreateText(rankPanel.transform, "Road", "ROAD ›", 30, UIPrefabBuilder.TextMuted, TextAlignmentOptions.MidlineRight);
        road.fontSharedMaterial = UIPrefabBuilder.OutlineFor(road);
        UIPrefabBuilder.Anchor(road.rectTransform, new Vector2(1f, 1f), new Vector2(-110f, -40f), new Vector2(200f, 44f));

        // The pulse lives on a wrapper so it never fights the button's own press scale tween.
        RectTransform playPulse = UIPrefabBuilder.CreateRect("PlayPulse", safe);
        UIPrefabBuilder.Anchor(playPulse, new Vector2(0.5f, 1f), new Vector2(0f, -730f), Vector2.zero);
        AddIdleMotion(playPulse.gameObject, 1, 1.05f, 0.7f);
        UIButton play = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.Play, playPulse, Vector2.zero);
        TMP_Text playCaption = Caption(safe, "PlayCaption", "RANKED  ·  BASEMENT BAR  ·  VS CPU", -850f);

        RectTransform objectives = BuildObjectives(safe);
        BuildNextCue(safe);

        GameObject cuesObject = UIPrefabBuilder.CreateButtonInstance(safe, "CUES", UIPrefabBuilder.Icons + "/icon_star.png", new Vector2(440f, 130f), 44, UIPrefabBuilder.ButtonColor.Yellow);
        UIPrefabBuilder.Anchor((RectTransform)cuesObject.transform, new Vector2(0.5f, 1f), new Vector2(-235f, -1607f), new Vector2(440f, 110f));
        // Fake door (v3 V12): ONLINE RANKED is coming.
        GameObject onlineObject = UIPrefabBuilder.CreateButtonInstance(safe, "ONLINE · SOON", UIPrefabBuilder.Icons + "/icon_multiplayer.png", new Vector2(440f, 130f), 38, UIPrefabBuilder.ButtonColor.Blue);
        UIPrefabBuilder.Anchor((RectTransform)onlineObject.transform, new Vector2(0.5f, 1f), new Vector2(235f, -1607f), new Vector2(440f, 110f));
        UIButton settings = settingsObject.GetComponent<UIButton>();
        UIButton cues = cuesObject.GetComponent<UIButton>();

        BuildSelectedCue(safe);

        TMP_Text version = UIPrefabBuilder.CreateText(safe, "Version", "v0.0.0", 30, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.BottomRight);
        version.rectTransform.anchorMin = version.rectTransform.anchorMax = new Vector2(1f, 0f);
        version.rectTransform.pivot = new Vector2(1f, 0f);
        version.rectTransform.anchoredPosition = new Vector2(-32f, 22f);
        version.rectTransform.sizeDelta = new Vector2(300f, 46f);

        // FTUE coach marks on the badge and the objectives (v3 V13).
        MatchSceneBuilder.BuildCoachMark(canvas.transform, new[] { CoachTarget.RankBadge, CoachTarget.Objectives },
            new[] { rankPanel.rectTransform, objectives });
        MatchSceneBuilder.BuildToast();

        var view = canvas.gameObject.AddComponent<MainMenuView>();
        UIPrefabBuilder.SetRef(view, "_playButton", play);
        UIPrefabBuilder.SetRef(view, "_settingsButton", settings);
        UIPrefabBuilder.SetRef(view, "_cuesButton", cues);
        UIPrefabBuilder.SetRef(view, "_onlineButton", onlineObject.GetComponent<UIButton>());
        UIPrefabBuilder.SetRef(view, "_versionText", version);
        UIPrefabBuilder.SetRef(view, "_playCaption", playCaption);
    }

    private static RectTransform BuildObjectives(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("Objectives", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(0.5f, 1f), new Vector2(0f, -1166f), new Vector2(960f, 532f));
        TMP_Text header = UIPrefabBuilder.CreateText(root, "Header", "OBJECTIVES", 34, UIPrefabBuilder.Gold, TextAlignmentOptions.MidlineLeft);
        header.fontSharedMaterial = UIPrefabBuilder.OutlineFor(header);
        UIPrefabBuilder.Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(170f, -28f), new Vector2(320f, 44f));
        var rows = new ObjectiveRowView[ObjectiveCatalog.Slots];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = UIPrefabBuilder.BuildObjectiveRow(root, new Vector2(0.5f, 1f), new Vector2(0f, -139f - i * 156f), new Vector2(960f, 150f));
        TMP_Text empty = UIPrefabBuilder.CreateText(root, "Empty", "All objectives done!", 32, UIPrefabBuilder.TextMuted, TextAlignmentOptions.Center);
        UIPrefabBuilder.Anchor(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 50f));

        var view = root.gameObject.AddComponent<ObjectivesPanelView>();
        UIPrefabBuilder.SetArray(view, "_rows", rows);
        UIPrefabBuilder.SetRef(view, "_empty", empty);
        return root;
    }

    private static void BuildNextCue(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("NextCue", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(0.5f, 1f), new Vector2(0f, -1487f), new Vector2(960f, 100f));
        UIPrefabBuilder.AddImage(root.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;
        RectTransform content = UIPrefabBuilder.CreateRect("Content", root);
        UIPrefabBuilder.Stretch(content, Vector4.zero);
        Image background = UIPrefabBuilder.AddImage(content.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_round_24.png"), new Color(0f, 0f, 0f, 0.45f), true);
        background.pixelsPerUnitMultiplier = 1.5f;

        TMP_Text label = UIPrefabBuilder.CreateText(content, "Label", "NEXT CUE · Club Cue", 32, UIPrefabBuilder.TextLight, TextAlignmentOptions.MidlineLeft);
        label.fontSharedMaterial = UIPrefabBuilder.OutlineFor(label);
        UIPrefabBuilder.Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(330f, 18f), new Vector2(600f, 44f));
        RectTransform track = UIPrefabBuilder.CreateRect("Bar", content);
        track.anchorMin = track.anchorMax = new Vector2(0f, 0.5f);
        track.pivot = new Vector2(0f, 0.5f);
        track.anchoredPosition = new Vector2(30f, -24f);
        track.sizeDelta = new Vector2(620f, 16f);
        UIPrefabBuilder.AddImage(track.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), new Color(0f, 0f, 0f, 0.55f), false);
        RectTransform fillRect = UIPrefabBuilder.CreateRect("Fill", track);
        UIPrefabBuilder.Stretch(fillRect, Vector4.zero);
        Image fill = UIPrefabBuilder.AddImage(fillRect.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), UIPrefabBuilder.Gold, false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;

        RectTransform coin = UIPrefabBuilder.CreateRect("Coin", content);
        UIPrefabBuilder.Anchor(coin, new Vector2(1f, 0.5f), new Vector2(-235f, 0f), new Vector2(46f, 46f));
        UIPrefabBuilder.AddImage(coin.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text amount = UIPrefabBuilder.CreateText(content, "Amount", "0 / 300", 32, UIPrefabBuilder.Gold, TextAlignmentOptions.MidlineRight);
        amount.fontSharedMaterial = UIPrefabBuilder.OutlineFor(amount);
        amount.enableAutoSizing = true;
        amount.fontSizeMin = 22f;
        amount.fontSizeMax = 32f;
        UIPrefabBuilder.Anchor(amount.rectTransform, new Vector2(1f, 0.5f), new Vector2(-114f, 0f), new Vector2(190f, 50f));

        var view = root.gameObject.AddComponent<NextCueView>();
        UIPrefabBuilder.SetRef(view, "_content", content.gameObject);
        UIPrefabBuilder.SetRef(view, "_label", label);
        UIPrefabBuilder.SetRef(view, "_fill", fill);
        UIPrefabBuilder.SetRef(view, "_amount", amount);
        UIPrefabBuilder.SetRef(view, "_background", background);
    }

    // "YOUR CUE  [cue]  House Cue  OVR 44"; tapping opens the cues.
    private static void BuildSelectedCue(RectTransform parent)
    {
        Image card = UIPrefabBuilder.Panel(parent, "SelectedCue", new Vector2(0.5f, 1f), new Vector2(0f, -1725f), new Vector2(960f, 100f), new Color(0f, 0f, 0f, 0.45f));
        card.raycastTarget = true;

        TMP_Text caption = UIPrefabBuilder.CreateText(card.transform, "Caption", "YOUR CUE", 26, UIPrefabBuilder.TextMuted, TextAlignmentOptions.MidlineLeft);
        UIPrefabBuilder.Anchor(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f, 18f), new Vector2(180f, 36f));
        TMP_Text name = UIPrefabBuilder.CreateText(card.transform, "Name", "House Cue", 38, UIPrefabBuilder.TextLight, TextAlignmentOptions.MidlineLeft);
        name.fontSharedMaterial = UIPrefabBuilder.OutlineFor(name);
        UIPrefabBuilder.Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(170f, -20f), new Vector2(300f, 48f));

        RectTransform cue = UIPrefabBuilder.CreateRect("Cue", card.transform);
        UIPrefabBuilder.Anchor(cue, new Vector2(0.5f, 0.5f), new Vector2(60f, 0f), new Vector2(340f, 26f));
        Image cueImage = UIPrefabBuilder.AddImage(cue.gameObject, UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.CueSpritePath(DataAssetBuilder.CueSpecs[0].Id)), Color.white, false);
        cueImage.preserveAspect = false;

        TMP_Text rating = UIPrefabBuilder.CreateText(card.transform, "Rating", "OVR 44", 36, Color.white, TextAlignmentOptions.MidlineRight);
        rating.fontSharedMaterial = UIPrefabBuilder.OutlineFor(rating);
        UIPrefabBuilder.Anchor(rating.rectTransform, new Vector2(1f, 0.5f), new Vector2(-100f, 0f), new Vector2(170f, 50f));

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
