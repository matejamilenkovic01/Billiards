using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MatchSceneBuilder
{
    private const string Generated = UIPrefabBuilder.Generated;

    [MenuItem("Tools/Billiards/Builders/Match Scene")]
    public static void Build()
    {
        // Load assets only after NewScene: a Single-mode new scene unloads unused assets loaded before it.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);

        ApplyRoomLighting();
        Camera camera = BuildCamera(config);
        BuildVolume();

        var table = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.TablePath));
        table.transform.position = new Vector3(0f, config.TableHeight, 0f);
        var tableView = table.GetComponent<TableView>();
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabBuilder.RoomPath));
        SetVenues(table.GetComponent<VenueView>());
        BakeReflections(config);
        var cue = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.CuePath));

        CameraRig rig = camera.gameObject.AddComponent<CameraRig>();
        UIPrefabBuilder.SetRef(rig, "_config", config);
        UIPrefabBuilder.SetRef(rig, "_table", tableView);

        var guide = new GameObject("AimGuide").AddComponent<AimGuideView>();
        UIPrefabBuilder.SetRef(guide, "_config", config);
        UIPrefabBuilder.SetRef(guide, "_table", tableView);
        UIPrefabBuilder.SetRef(guide, "_solidMaterial", AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("GuideSolid")));
        UIPrefabBuilder.SetRef(guide, "_handSprite", UIPrefabBuilder.LoadSprite(Generated + "/icon_hand.png"));
        UIPrefabBuilder.SetRef(guide, "_dashMaterial", AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("GuideDash")));
        UIPrefabBuilder.SetRef(guide, "_ghostMaterial", AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("GhostBall")));
        var bandObject = new GameObject("GuideBands", typeof(MeshFilter), typeof(MeshRenderer));
        bandObject.transform.SetParent(guide.transform, false);
        bandObject.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("GuideBand"));
        var bands = bandObject.AddComponent<GuideBandRenderer>();
        UIPrefabBuilder.SetRef(bands, "_table", tableView);
        UIPrefabBuilder.SetRef(guide, "_bands", bands);
        var lineObject = new GameObject("GuideLine", typeof(MeshFilter), typeof(MeshRenderer));
        lineObject.transform.SetParent(guide.transform, false);
        lineObject.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("GuideThinLine"));
        var line = lineObject.AddComponent<GuideBandRenderer>();
        UIPrefabBuilder.SetRef(line, "_table", tableView);
        UIPrefabBuilder.SetRef(guide, "_line", line);
        UIPrefabBuilder.SetRef(guide, "_tipMaterial", AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("GuideTip")));

        var controllers = new GameObject("Controllers");
        BotController bot = controllers.AddComponent<BotController>();
        PlayerShotController player = controllers.AddComponent<PlayerShotController>();
        UIPrefabBuilder.SetRef(player, "_guide", guide);
        MatchController match = controllers.AddComponent<MatchController>();
        UIPrefabBuilder.SetRef(match, "_config", config);
        UIPrefabBuilder.SetRef(match, "_botProfile", AssetDatabase.LoadAssetAtPath<BotProfile>(DataAssetBuilder.BotProfilePath));
        UIPrefabBuilder.SetRef(match, "_catalog", AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath));
        UIPrefabBuilder.SetRef(match, "_table", tableView);
        UIPrefabBuilder.SetRef(match, "_cue", cue.GetComponent<CueView>());
        UIPrefabBuilder.SetRef(match, "_bot", bot);
        UIPrefabBuilder.SetRef(match, "_cameraRig", rig);

        BuildHud();

        EditorSceneManager.SaveScene(scene, ProjectSettingsBuilder.MatchScenePath);
        Debug.Log("MatchSceneBuilder: done");
    }

    public const string ReflectionCubemapPath = "Assets/_Art/Generated/ReflectionProbe_Match.exr";

    // The venue themes the table can wear (v3 V6; one so far).
    public static void SetVenues(VenueView venue)
    {
        if (venue == null)
            return;
        var serialized = new SerializedObject(venue);
        SerializedProperty themes = serialized.FindProperty("_themes");
        themes.arraySize = 1;
        themes.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<VenueTheme>(DataAssetBuilder.VenuePath(MatchSetup.DefaultVenueId));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // One reflection probe over the table, baked here at 128 px: the balls reflect the lamp and the room (v3 V6).
    public static void BakeReflections(MatchConfig config)
    {
        var probeObject = new GameObject("ReflectionProbe");
        probeObject.transform.position = new Vector3(0f, config.TableHeight + 0.5f, 0f);
        var probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.resolution = 128;
        probe.size = new Vector3(8.6f, 3.2f, 11.2f);
        probe.hdr = true;
        Lightmapping.BakeReflectionProbe(probe, ReflectionCubemapPath);
        probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(ReflectionCubemapPath);
    }

    // The room is lit by ambient and emission (no realtime lights); VenueView sets the venue's ambient at start.
    public static void ApplyRoomLighting()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(1.1f, 1.0f, 0.88f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 0.35f;
        RenderSettings.fog = false;
    }

    public static Camera BuildCamera(MatchConfig config)
    {
        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = config.FieldOfView;
        camera.nearClipPlane = 0.02f;
        camera.farClipPlane = 30f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.03f, 0.028f);
        var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        // Broadcast view from behind the head rail, the whole table in a portrait frame.
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, config.TableHeight + 2.9f, -1.35f), Quaternion.Euler(68f, 0f, 0f));
        return camera;
    }

    public static void BuildVolume()
    {
        var volumeObject = new GameObject("Global Volume");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TablePrefabBuilder.VolumeProfilePath);
    }

    #region HUD
    public static Canvas CreateSceneCanvas(string name, int sortingOrder, out RectTransform safeArea)
    {
        var canvasObject = new GameObject(name);
        Canvas canvas = SystemsPrefabBuilder.CreateCanvas(canvasObject, sortingOrder, true);
        safeArea = UIPrefabBuilder.CreateRect("SafeArea", canvasObject.transform);
        UIPrefabBuilder.Stretch(safeArea, Vector4.zero);
        safeArea.gameObject.AddComponent<SafeArea>();
        return canvas;
    }

    private static void BuildHud()
    {
        Canvas canvas = CreateSceneCanvas("MatchHud", 0, out RectTransform safe);

        // Behind everything else: drags that hit no control turn the aim / look around / move the cue ball.
        RectTransform touch = UIPrefabBuilder.CreateRect("TableTouchSurface", canvas.transform);
        touch.SetAsFirstSibling();
        UIPrefabBuilder.Stretch(touch, Vector4.zero);
        UIPrefabBuilder.AddImage(touch.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;
        touch.gameObject.AddComponent<TableTouchSurface>();

        BuildShotControls(safe);
        BuildTopBar(safe);
        BuildFreeLookChip(safe);
        BuildCoachMark(canvas.transform);
        BuildBanner(canvas.transform);
        BuildToast();
    }

    // Under your panel while you look around on the opponent's turn (v3 V2): drag and pinch icons, TABLE VIEW.
    private static void BuildFreeLookChip(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("FreeLookChip", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(0f, 1f), new Vector2(255f, -262f), new Vector2(470f, 84f));
        var group = root.gameObject.AddComponent<CanvasGroup>();

        Image icons = UIPrefabBuilder.Panel(root, "Gestures", new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(140f, 76f), new Color(0f, 0f, 0f, 0.55f));
        string[] sprites = { Generated + "/icon_hand.png", Generated + "/icon_pinch.png" };
        for (int i = 0; i < sprites.Length; i++)
        {
            RectTransform icon = UIPrefabBuilder.CreateRect("Icon" + i, icons.transform);
            UIPrefabBuilder.Anchor(icon, new Vector2(0.5f, 0.5f), new Vector2(-32f + 64f * i, 0f), new Vector2(54f, 54f));
            UIPrefabBuilder.AddImage(icon.gameObject, UIPrefabBuilder.LoadSprite(sprites[i]), Color.white, false).raycastTarget = false;
        }

        GameObject button = UIPrefabBuilder.CreateButtonInstance(root, "TABLE VIEW", UIPrefabBuilder.Icons + "/icon_return.png", new Vector2(310f, 84f), 30f, UIPrefabBuilder.ButtonColor.Blue);
        button.name = "TableViewButton";
        UIPrefabBuilder.Anchor((RectTransform)button.transform, new Vector2(0f, 0.5f), new Vector2(315f, 0f), new Vector2(310f, 84f));

        var view = root.gameObject.AddComponent<FreeLookChipView>();
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_tableViewButton", button.GetComponent<UIButton>());
    }

    // Coach marks: a balloon and a ghost hand over the table (v3 V2, G10). Not a raycast target.
    private static void BuildCoachMark(Transform canvas)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("CoachMark", canvas);
        UIPrefabBuilder.Stretch(root, Vector4.zero);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Image balloon = UIPrefabBuilder.Panel(root, "Balloon", new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(980f, 120f), new Color(0.06f, 0.08f, 0.07f, 0.88f));
        balloon.raycastTarget = false;
        TMP_Text text = UIPrefabBuilder.CreateText(balloon.transform, "Text", "", 40, UIPrefabBuilder.TextLight, TextAlignmentOptions.Center);
        text.enableAutoSizing = true;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 38f;
        text.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Stretch(text.rectTransform, new Vector4(24f, 10f, 24f, 10f));
        text.raycastTarget = false;

        RectTransform hand = UIPrefabBuilder.CreateRect("Hand", root);
        UIPrefabBuilder.Anchor(hand, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(130f, 130f));
        UIPrefabBuilder.AddImage(hand.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/icon_hand.png"), new Color(1f, 1f, 1f, 0.9f), false).raycastTarget = false;
        RectTransform second = UIPrefabBuilder.CreateRect("SecondFinger", root);
        UIPrefabBuilder.Anchor(second, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(130f, 130f));
        UIPrefabBuilder.AddImage(second.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/icon_hand.png"), new Color(1f, 1f, 1f, 0.9f), false).raycastTarget = false;
        second.localScale = new Vector3(-1f, -1f, 1f);

        var view = root.gameObject.AddComponent<CoachMarkView>();
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_text", text);
        UIPrefabBuilder.SetRef(view, "_hand", hand);
        UIPrefabBuilder.SetRef(view, "_secondFinger", second);
    }

    private static Sprite[] BallSprites()
    {
        var sprites = new Sprite[BallGroups.BallCount];
        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.BallSpritePath(i));
        return sprites;
    }

    private static void SetSprites(Object target, string field, Sprite[] sprites)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty array = serialized.FindProperty(field);
        array.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetImages(Object target, string field, Image[] images)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty array = serialized.FindProperty(field);
        array.arraySize = images.Length;
        for (int i = 0; i < images.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Top: YOU and OPPONENT panels, the centre 8-ball, the pause button.
    private static void BuildTopBar(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("MatchHud", parent);
        UIPrefabBuilder.Stretch(root, Vector4.zero);

        BuildPlayerPanel(root, true, new Vector2(-250f, -110f));
        BuildPlayerPanel(root, false, new Vector2(250f, -110f));

        RectTransform eight = UIPrefabBuilder.CreateRect("CenterEight", root);
        UIPrefabBuilder.Anchor(eight, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(60f, 60f));
        Image eightImage = UIPrefabBuilder.AddImage(eight.gameObject, UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.BallSpritePath(8)), new Color(1f, 1f, 1f, 0.35f), false);

        UIButton pause = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.Pause, root, Vector2.zero);
        UIPrefabBuilder.Anchor((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-80f, -285f), new Vector2(120f, 120f));

        var view = root.gameObject.AddComponent<MatchHudView>();
        UIPrefabBuilder.SetRef(view, "_pauseButton", pause);
        UIPrefabBuilder.SetRef(view, "_centerEight", eightImage);
    }

    private static void BuildPlayerPanel(RectTransform parent, bool isPlayer, Vector2 position)
    {
        var size = new Vector2(470f, 190f);
        RectTransform root = UIPrefabBuilder.CreateRect(isPlayer ? "PlayerPanel" : "OpponentPanel", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(0.5f, 1f), position, size);

        Image glow = UIPrefabBuilder.Panel(root, "Glow", new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(14f, 14f), new Color(0.3f, 0.9f, 0.45f, 0f));
        UIPrefabBuilder.Panel(root, "Background", new Vector2(0.5f, 0.5f), Vector2.zero, size, new Color(0.04f, 0.06f, 0.05f, 0.82f));

        RectTransform avatar = UIPrefabBuilder.CreateRect("Avatar", root);
        UIPrefabBuilder.Anchor(avatar, new Vector2(0f, 1f), new Vector2(46f, -44f), new Vector2(54f, 54f));
        UIPrefabBuilder.AddImage(avatar.gameObject, UIPrefabBuilder.LoadSprite(UIPrefabBuilder.Icons + "/icon_singleplayer.png"),
            isPlayer ? UIPrefabBuilder.Hex("7FE39A") : UIPrefabBuilder.Hex("F5A94C"), false);

        TMP_Text name = UIPrefabBuilder.CreateText(root, "Name", isPlayer ? "YOU" : "OPPONENT", isPlayer ? 38 : 30, Color.white, TextAlignmentOptions.MidlineLeft);
        name.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(205f, -44f), new Vector2(240f, 54f));

        RectTransform eight = UIPrefabBuilder.CreateRect("Eight", root);
        UIPrefabBuilder.Anchor(eight, new Vector2(1f, 1f), new Vector2(-38f, -44f), new Vector2(46f, 46f));
        Image eightImage = UIPrefabBuilder.AddImage(eight.gameObject, UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.BallSpritePath(8)), Color.white, false);

        TMP_Text group = UIPrefabBuilder.CreateText(root, "Group", "OPEN TABLE", 22, UIPrefabBuilder.TextMuted, TextAlignmentOptions.MidlineRight);
        UIPrefabBuilder.Anchor(group.rectTransform, new Vector2(1f, 0f), new Vector2(-90f, 26f), new Vector2(160f, 36f));

        RectTransform row = UIPrefabBuilder.CreateRect("Slots", root);
        UIPrefabBuilder.Anchor(row, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(434f, 56f));
        var slots = new Image[7];
        var checks = new Image[7];
        for (int i = 0; i < 7; i++)
        {
            RectTransform slot = UIPrefabBuilder.CreateRect("Slot" + i, row);
            UIPrefabBuilder.Anchor(slot, new Vector2(0f, 0.5f), new Vector2(31f + i * 62f, 0f), new Vector2(54f, 54f));
            slots[i] = UIPrefabBuilder.AddImage(slot.gameObject, UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.UnknownBallSpritePath), Color.white, false);
            RectTransform check = UIPrefabBuilder.CreateRect("Check", slot);
            UIPrefabBuilder.Anchor(check, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
            checks[i] = UIPrefabBuilder.AddImage(check.gameObject, UIPrefabBuilder.LoadSprite(UIPrefabBuilder.Icons + "/icon_checkmark.png"), UIPrefabBuilder.Hex("7FE39A"), false);
        }

        TMP_Text count = UIPrefabBuilder.CreateText(root, "Count", "", 24, Color.white, TextAlignmentOptions.MidlineLeft);
        UIPrefabBuilder.Anchor(count.rectTransform, new Vector2(0f, 0f), new Vector2(110f, 26f), new Vector2(200f, 36f));
        TMP_Text thinking = UIPrefabBuilder.CreateText(root, "Thinking", "Thinking", 26, UIPrefabBuilder.Hex("F5A94C"), TextAlignmentOptions.Center);
        thinking.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(thinking.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -62f), new Vector2(260f, 36f));

        RectTransform arrow = UIPrefabBuilder.CreateRect("Arrow", root);
        UIPrefabBuilder.Anchor(arrow, new Vector2(0.5f, 0f), new Vector2(0f, -26f), new Vector2(48f, 24f));
        UIPrefabBuilder.AddImage(arrow.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/tooltip_arrow.png"),
            isPlayer ? UIPrefabBuilder.Hex("7FE39A") : UIPrefabBuilder.Hex("F5A94C"), false);

        var view = root.gameObject.AddComponent<PlayerPanelView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("_isPlayer").boolValue = isPlayer;
        serialized.FindProperty("_activeColor").colorValue = isPlayer ? UIPrefabBuilder.Hex("4CD97A") : UIPrefabBuilder.Hex("F59A3A");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        UIPrefabBuilder.SetRef(view, "_glow", glow);
        UIPrefabBuilder.SetRef(view, "_name", name);
        UIPrefabBuilder.SetRef(view, "_arrow", arrow);
        UIPrefabBuilder.SetRef(view, "_groupText", group);
        UIPrefabBuilder.SetRef(view, "_countText", count);
        UIPrefabBuilder.SetRef(view, "_thinking", thinking);
        UIPrefabBuilder.SetRef(view, "_slotRow", row);
        SetImages(view, "_slots", slots);
        SetImages(view, "_checks", checks);
        UIPrefabBuilder.SetRef(view, "_eightIcon", eightImage);
        SetSprites(view, "_ballSprites", BallSprites());
        UIPrefabBuilder.SetRef(view, "_unknownSprite", UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.UnknownBallSpritePath));
    }

    private static void BuildBanner(Transform canvas)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("TurnBanner", canvas);
        UIPrefabBuilder.Stretch(root, Vector4.zero);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        RectTransform band = UIPrefabBuilder.CreateRect("Band", root);
        band.anchorMin = new Vector2(0f, 0.5f);
        band.anchorMax = new Vector2(1f, 0.5f);
        band.anchoredPosition = new Vector2(0f, 140f);
        band.sizeDelta = new Vector2(0f, 190f);
        Image background = UIPrefabBuilder.AddImage(band.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), new Color(0.18f, 0.62f, 0.32f, 0.95f), false);
        background.raycastTarget = false;
        TMP_Text text = UIPrefabBuilder.CreateText(band, "Text", "YOUR TURN", 76, Color.white, TextAlignmentOptions.Center);
        text.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(text.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 96f));
        // "FOUL — No rail after contact" is long: shrink to fit.
        text.enableAutoSizing = true;
        text.fontSizeMin = 40f;
        text.fontSizeMax = 76f;
        text.raycastTarget = false;
        TMP_Text subText = UIPrefabBuilder.CreateText(band, "SubText", "", 38, Color.white, TextAlignmentOptions.Center);
        subText.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(subText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(900f, 56f));
        subText.enableAutoSizing = true;
        subText.fontSizeMin = 20f;
        subText.fontSizeMax = 38f;
        subText.raycastTarget = false;

        RectTransform iconRect = UIPrefabBuilder.CreateRect("Icon", band);
        UIPrefabBuilder.Anchor(iconRect, new Vector2(0.5f, 0.5f), new Vector2(-400f, 0f), new Vector2(110f, 110f));
        Image icon = UIPrefabBuilder.AddImage(iconRect.gameObject, null, Color.white, false);
        icon.raycastTarget = false;

        // "?" on a foul banner: opens the House Rules card at the broken rule.
        Image help = UIPrefabBuilder.Panel(band, "Help", new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(100f, 100f), new Color(0f, 0f, 0f, 0.45f));
        help.raycastTarget = true;
        RectTransform helpIcon = UIPrefabBuilder.CreateRect("Icon", help.transform);
        UIPrefabBuilder.Anchor(helpIcon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
        UIPrefabBuilder.AddImage(helpIcon.gameObject, UIPrefabBuilder.LoadSprite(UIPrefabBuilder.Icons + "/icon_question.png"), Color.white, false).raycastTarget = false;
        UIButton helpButton = help.gameObject.AddComponent<UIButton>();
        UIPrefabBuilder.SetRef(helpButton, "_normalSprite", help.sprite);
        UIPrefabBuilder.SetRef(helpButton, "_pressedSprite", help.sprite);

        var view = root.gameObject.AddComponent<TurnBannerView>();
        UIPrefabBuilder.SetRef(view, "_band", band);
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_background", background);
        UIPrefabBuilder.SetRef(view, "_text", text);
        UIPrefabBuilder.SetRef(view, "_subText", subText);
        UIPrefabBuilder.SetRef(view, "_icon", icon);
        UIPrefabBuilder.SetRef(view, "_helpButton", helpButton);
        UIPrefabBuilder.SetRef(view, "_solidsSprite", UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.BallSpritePath(1)));
        UIPrefabBuilder.SetRef(view, "_stripesSprite", UIPrefabBuilder.LoadSprite(GeneratedArtBuilder.BallSpritePath(9)));
    }

    // Screen layout (gameplay redesign 3.1), reference 1080 x 1920 inside the safe area: power column on the right
    // (78-98% of the width, 22-80% of the height), LOOK / MOVE BALL at 82-88%, fine aim wheel or release bar at
    // the bottom (89-97%). Public so ControlChecks can test the pull range against the real strip.
    public static readonly Vector2 PowerStripPosition = new(-128f, -20f);   // anchored right-middle
    public static readonly Vector2 PowerStripSize = new(216f, 1110f);
    public const float BottomRowY = 125f;
    public const float ButtonRowY = 300f;
    public const float HintRowY = 410f;

    // Strip's bottom edge above the safe area's bottom, reference pixels.
    public static float PowerStripBottom => 960f + PowerStripPosition.y - PowerStripSize.y * 0.5f;

    private static void BuildShotControls(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("ShotControls", parent);
        UIPrefabBuilder.Stretch(root, Vector4.zero);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var view = root.gameObject.AddComponent<ShotControlsView>();

        GameObject fine = BuildFineAim(root);
        GameObject power = BuildPowerStrip(root);
        // Outside the controls group: it holds the marker for a moment after the shot, when the controls are gone.
        BuildReleaseBar(parent);

        UIButton look = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.Look, root, Vector2.zero);
        UIPrefabBuilder.Anchor((RectTransform)look.transform, new Vector2(0f, 0f), new Vector2(225f, ButtonRowY), new Vector2(390f, 110f));
        UIButton move = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.MoveBall, root, Vector2.zero);
        UIPrefabBuilder.Anchor((RectTransform)move.transform, new Vector2(0f, 0f), new Vector2(635f, ButtonRowY), new Vector2(390f, 110f));
        UIButton done = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.Done, root, Vector2.zero);
        UIPrefabBuilder.Anchor((RectTransform)done.transform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(380f, 150f));

        TMP_Text hint = UIPrefabBuilder.CreateText(root, "Hint", "Place the cue ball behind the line", 38, UIPrefabBuilder.TextLight, TextAlignmentOptions.Center);
        hint.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        // Its own row above the buttons (v3 V2): never under LOOK / BACK TO SHOT or the power column; one line that fits.
        UIPrefabBuilder.Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, HintRowY), new Vector2(1000f, 70f));
        hint.enableAutoSizing = true;
        hint.fontSizeMin = 22f;
        hint.fontSizeMax = 38f;

        Transform lookContent = look.transform.Find("Content");
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_lookButton", look);
        UIPrefabBuilder.SetRef(view, "_lookLabel", lookContent.Find("Label").GetComponent<TMP_Text>());
        UIPrefabBuilder.SetRef(view, "_lookIcon", lookContent.Find("Icon").GetComponent<Image>());
        UIPrefabBuilder.SetRef(view, "_lookSprite", UIPrefabBuilder.LoadSprite(Generated + "/icon_eye.png"));
        UIPrefabBuilder.SetRef(view, "_backSprite", UIPrefabBuilder.LoadSprite(UIPrefabBuilder.Icons + "/icon_return.png"));
        UIPrefabBuilder.SetRef(view, "_moveBallButton", move);
        UIPrefabBuilder.SetRef(view, "_doneButton", done);
        UIPrefabBuilder.SetRef(view, "_hint", hint);
        UIPrefabBuilder.SetRef(view, "_powerStrip", power);
        UIPrefabBuilder.SetRef(view, "_fineAim", fine);
    }

    // Bottom: FINE AIM label, a horizontal roller of ticks, step arrows at both ends.
    private static GameObject BuildFineAim(RectTransform parent)
    {
        Sprite white = UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png");
        RectTransform root = UIPrefabBuilder.CreateRect("FineAim", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(0.5f, 0f), new Vector2(0f, BottomRowY), new Vector2(1040f, 190f));

        TMP_Text label = UIPrefabBuilder.CreateText(root, "Label", "FINE AIM", 30, UIPrefabBuilder.TextLight, TextAlignmentOptions.Center);
        label.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(400f, 44f));

        Image wheel = UIPrefabBuilder.Panel(root, "Wheel", new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(700f, 92f), new Color(0f, 0f, 0f, 0.45f));
        wheel.raycastTarget = true;
        wheel.gameObject.AddComponent<RectMask2D>();
        var ticks = new RectTransform[18];
        for (int i = 0; i < ticks.Length; i++)
        {
            RectTransform tick = UIPrefabBuilder.CreateRect("Tick", wheel.transform);
            UIPrefabBuilder.Anchor(tick, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 62f));
            UIPrefabBuilder.AddImage(tick.gameObject, white, new Color(1f, 1f, 1f, 0.8f), false);
            ticks[i] = tick;
        }
        RectTransform shade = UIPrefabBuilder.CreateRect("Shade", wheel.transform);
        UIPrefabBuilder.Stretch(shade, Vector4.zero);
        UIPrefabBuilder.AddImage(shade.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_wheel_shade.png"), Color.white, false).raycastTarget = false;

        var view = wheel.gameObject.AddComponent<FineAimWheelView>();
        UIPrefabBuilder.SetRef(view, "_background", wheel);
        var serialized = new SerializedObject(view);
        SerializedProperty array = serialized.FindProperty("_ticks");
        array.arraySize = ticks.Length;
        for (int i = 0; i < ticks.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = ticks[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();

        foreach (int direction in new[] { -1, 1 })
        {
            Image button = UIPrefabBuilder.Panel(root, direction < 0 ? "StepLeft" : "StepRight", new Vector2(0.5f, 0.5f),
                new Vector2(direction * 455f, -15f), new Vector2(110f, 110f), new Color(0f, 0f, 0f, 0.5f));
            button.raycastTarget = true;
            RectTransform icon = UIPrefabBuilder.CreateRect("Icon", button.transform);
            UIPrefabBuilder.Anchor(icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            UIPrefabBuilder.AddImage(icon.gameObject, UIPrefabBuilder.LoadSprite(UIPrefabBuilder.Icons + (direction < 0 ? "/icon_left.png" : "/icon_right.png")), Color.white, false).raycastTarget = false;
            var step = button.gameObject.AddComponent<FineAimStepButton>();
            var stepSerialized = new SerializedObject(step);
            stepSerialized.FindProperty("_direction").intValue = direction;
            stepSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
        return root.gameObject;
    }

    // Right: the cue strip you pull down (v3 V5). POWER and an arrow down at the top; the cue points up with a grip
    // ring at its top, which travels MatchConfig.PullTravelPixels down to 20 px above the bottom; SOFT / MED / HARD
    // marks at the grip's position for MatchConfig.PowerMarks, and a pointer that rides with the grip.
    public static float PowerGripRestY(MatchConfig config) =>
        -PowerStripSize.y * 0.5f + PowerStripView.BottomMarginPixels + config.PullTravelPixels;

    private static GameObject BuildPowerStrip(RectTransform parent)
    {
        var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);
        Sprite white = UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png");
        Image panel = UIPrefabBuilder.Panel(parent, "PowerStrip", new Vector2(1f, 0.5f), PowerStripPosition, PowerStripSize, new Color(0f, 0f, 0f, 0.4f));
        panel.raycastTarget = true;
        const float cueX = 30f;
        float gripY = PowerGripRestY(config);
        float travel = config.PullTravelPixels;

        RectTransform bolt = UIPrefabBuilder.CreateRect("PowerIcon", panel.transform);
        UIPrefabBuilder.Anchor(bolt, new Vector2(0.5f, 0.5f), new Vector2(-74f, 505f), new Vector2(42f, 42f));
        UIPrefabBuilder.AddImage(bolt.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/icon_bolt.png"), Color.white, false).raycastTarget = false;
        TMP_Text title = UIPrefabBuilder.CreateText(panel.transform, "PowerLabel", "POWER", 40, Color.white, TextAlignmentOptions.Center);
        title.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(18f, 505f), new Vector2(170f, 50f));
        RectTransform arrow = UIPrefabBuilder.CreateRect("Arrow", panel.transform);
        UIPrefabBuilder.Anchor(arrow, new Vector2(0.5f, 0.5f), new Vector2(cueX, 448f), new Vector2(54f, 54f));
        UIPrefabBuilder.AddImage(arrow.gameObject, UIPrefabBuilder.LoadSprite(UIPrefabBuilder.Icons + "/icon_right.png"), new Color(1f, 1f, 1f, 0.9f), false).raycastTarget = false;
        arrow.localRotation = Quaternion.Euler(0f, 0f, -90f);

        var clipPadding = new Vector4(8f, 10f, 8f, 150f);
        RectTransform clip = UIPrefabBuilder.CreateRect("Clip", panel.transform);
        UIPrefabBuilder.Stretch(clip, clipPadding);
        clip.gameObject.AddComponent<RectMask2D>();
        // Children of the clip are placed relative to its centre; these positions are in the panel's.
        var clipCenter = new Vector2((clipPadding.x - clipPadding.z) * 0.5f, (clipPadding.y - clipPadding.w) * 0.5f);

        // Warm fill from the grip's rest down to the grip.
        RectTransform fillRect = UIPrefabBuilder.CreateRect("Fill", clip);
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.pivot = new Vector2(0.5f, 1f);
        fillRect.sizeDelta = new Vector2(76f, travel);
        fillRect.anchoredPosition = new Vector2(cueX, gripY) - clipCenter;
        Image fill = UIPrefabBuilder.AddImage(fillRect.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_power_fill.png"), Color.white, false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Top;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;

        // The cue sprite is horizontal (tip on the right); turned 90 degrees it points up, its tip at the grip.
        RectTransform cue = UIPrefabBuilder.CreateRect("Cue", clip);
        UIPrefabBuilder.Anchor(cue, new Vector2(0.5f, 0.5f), new Vector2(cueX, gripY + 20f - 440f) - clipCenter, new Vector2(880f, 58f));
        cue.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Image cueImage = UIPrefabBuilder.AddImage(cue.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/cue_rookie.png"), Color.white, false);
        cueImage.preserveAspect = false;
        cueImage.raycastTarget = false;

        Vector3 marks = config.PowerMarks;
        (string text, float pull)[] labels = { ("SOFT", marks.x), ("MED", marks.y), ("HARD", marks.z) };
        foreach (var (text, pull) in labels)
        {
            float y = gripY - pull * travel;
            TMP_Text mark = UIPrefabBuilder.CreateText(panel.transform, text, text, 26, UIPrefabBuilder.TextLight, TextAlignmentOptions.Left);
            mark.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
            UIPrefabBuilder.Anchor(mark.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-62f, y), new Vector2(96f, 40f));
            RectTransform notch = UIPrefabBuilder.CreateRect(text + "Notch", panel.transform);
            UIPrefabBuilder.Anchor(notch, new Vector2(0.5f, 0.5f), new Vector2(-14f, y), new Vector2(24f, 3f));
            UIPrefabBuilder.AddImage(notch.gameObject, white, new Color(1f, 1f, 1f, 0.5f), false).raycastTarget = false;
        }

        // The grip: a rounded ring with a sheen across the top of the cue; it lights up when taken.
        RectTransform grip = UIPrefabBuilder.CreateRect("Grip", panel.transform);
        UIPrefabBuilder.Anchor(grip, new Vector2(0.5f, 0.5f), new Vector2(cueX, gripY), new Vector2(104f, 44f));
        RectTransform glowRect = UIPrefabBuilder.CreateRect("GrabGlow", grip);
        UIPrefabBuilder.Anchor(glowRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 150f));
        Image glow = UIPrefabBuilder.AddImage(glowRect.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_glow.png"), new Color(1f, 1f, 1f, 0f), false);
        glow.preserveAspect = false;
        glow.raycastTarget = false;
        RectTransform ring = UIPrefabBuilder.CreateRect("Ring", grip);
        UIPrefabBuilder.Stretch(ring, Vector4.zero);
        UIPrefabBuilder.AddImage(ring.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_grip.png"), Color.white, false).raycastTarget = false;

        // The pointer next to the cue: shows which mark the grip is at.
        RectTransform pointer = UIPrefabBuilder.CreateRect("Pointer", panel.transform);
        UIPrefabBuilder.Anchor(pointer, new Vector2(0.5f, 0.5f), new Vector2(-14f, gripY), new Vector2(30f, 18f));
        UIPrefabBuilder.AddImage(pointer.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/tooltip_arrow.png"), new Color(1f, 0.82f, 0.4f, 1f), false).raycastTarget = false;
        pointer.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // Ghost hand: takes the grip and pulls it down.
        RectTransform hand = UIPrefabBuilder.CreateRect("HintHand", panel.transform);
        UIPrefabBuilder.Anchor(hand, new Vector2(0.5f, 0.5f), new Vector2(cueX + 18f, gripY - 30f), new Vector2(110f, 110f));
        var handGroup = hand.gameObject.AddComponent<CanvasGroup>();
        handGroup.blocksRaycasts = false;
        UIPrefabBuilder.AddImage(hand.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/icon_hand.png"), Color.white, false).raycastTarget = false;

        TMP_Text percent = UIPrefabBuilder.CreateText(panel.transform, "PowerPercent", "", 54, Color.white, TextAlignmentOptions.Center);
        percent.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(percent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 45f), new Vector2(220f, 80f));

        var view = panel.gameObject.AddComponent<PowerStripView>();
        UIPrefabBuilder.SetRef(view, "_cue", cue);
        UIPrefabBuilder.SetRef(view, "_cueImage", cueImage);
        UIPrefabBuilder.SetRef(view, "_grip", grip);
        UIPrefabBuilder.SetRef(view, "_pointer", pointer);
        UIPrefabBuilder.SetRef(view, "_percent", percent);
        UIPrefabBuilder.SetRef(view, "_fill", fill);
        UIPrefabBuilder.SetRef(view, "_grabGlow", glow);
        UIPrefabBuilder.SetRef(view, "_arrow", arrow);
        UIPrefabBuilder.SetRef(view, "_hintHand", hand);
        UIPrefabBuilder.SetRef(view, "_hintGroup", handGroup);
        return panel.gameObject;
    }

    // Bottom, in the fine aim wheel's place while pulling: a framed horizontal bar (red / yellow / green / yellow /
    // red) with a marker (a line with a triangle above and below).
    private static void BuildReleaseBar(RectTransform parent)
    {
        Sprite white = UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png");
        Sprite triangle = UIPrefabBuilder.LoadSprite(Generated + "/tooltip_arrow.png");
        RectTransform root = UIPrefabBuilder.CreateRect("ReleaseBar", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(0.5f, 0f), new Vector2(0f, BottomRowY), new Vector2(820f, 190f));
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        TMP_Text label = UIPrefabBuilder.CreateText(root, "Label", "LET GO ON GREEN", 28, UIPrefabBuilder.TextLight, TextAlignmentOptions.Center);
        label.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(600f, 44f));

        UIPrefabBuilder.Panel(root, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(6f, -23f), new Vector2(772f, 86f), new Color(0f, 0f, 0f, 0.45f));
        UIPrefabBuilder.Panel(root, "Border", new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(776f, 90f), UIPrefabBuilder.Hex("4C6B55"));
        UIPrefabBuilder.Panel(root, "Frame", new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(768f, 82f), UIPrefabBuilder.Hex("0E1511"));

        Image red = UIPrefabBuilder.Panel(root, "Bar", new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(756f, 70f), UIPrefabBuilder.Hex("C9352C"));
        Image yellow = UIPrefabBuilder.Panel(red.transform, "Yellow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 70f), UIPrefabBuilder.Hex("F2C230"));
        Image green = UIPrefabBuilder.Panel(red.transform, "Green", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 70f), UIPrefabBuilder.Hex("3DCB5B"));
        red.pixelsPerUnitMultiplier = yellow.pixelsPerUnitMultiplier = green.pixelsPerUnitMultiplier = 2.2f;

        RectTransform center = UIPrefabBuilder.CreateRect("Center", red.transform);
        UIPrefabBuilder.Anchor(center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 70f));
        UIPrefabBuilder.AddImage(center.gameObject, white, new Color(0f, 0f, 0f, 0.35f), false);

        RectTransform gloss = UIPrefabBuilder.CreateRect("Gloss", red.transform);
        UIPrefabBuilder.Stretch(gloss, new Vector4(4f, 0f, 4f, 0f));
        Image glossImage = UIPrefabBuilder.AddImage(gloss.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_gloss_h.png"), Color.white, false);
        glossImage.preserveAspect = false;

        RectTransform flashRect = UIPrefabBuilder.CreateRect("Flash", red.transform);
        UIPrefabBuilder.Stretch(flashRect, new Vector4(-6f, -6f, -6f, -6f));
        Image flash = UIPrefabBuilder.AddImage(flashRect.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_round_24.png"), Color.clear, true);

        RectTransform indicator = UIPrefabBuilder.CreateRect("Indicator", red.transform);
        UIPrefabBuilder.Anchor(indicator, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 130f));
        RectTransform glowRect = UIPrefabBuilder.CreateRect("Glow", indicator);
        UIPrefabBuilder.Anchor(glowRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 200f));
        Image glow = UIPrefabBuilder.AddImage(glowRect.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_glow.png"), new Color(0.35f, 1f, 0.45f, 0.8f), false);
        glow.preserveAspect = false;
        RectTransform shadow = UIPrefabBuilder.CreateRect("LineShadow", indicator);
        UIPrefabBuilder.Anchor(shadow, new Vector2(0.5f, 0.5f), new Vector2(3f, -3f), new Vector2(12f, 92f));
        UIPrefabBuilder.AddImage(shadow.gameObject, white, new Color(0f, 0f, 0f, 0.6f), false);
        RectTransform line = UIPrefabBuilder.CreateRect("Line", indicator);
        UIPrefabBuilder.Anchor(line, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 92f));
        UIPrefabBuilder.AddImage(line.gameObject, white, Color.white, false);
        foreach (float side in new[] { 1f, -1f })
        {
            RectTransform arrowRect = UIPrefabBuilder.CreateRect(side > 0f ? "TriangleTop" : "TriangleBottom", indicator);
            UIPrefabBuilder.Anchor(arrowRect, new Vector2(0.5f, 0.5f), new Vector2(0f, side * 56f), new Vector2(32f, 20f));
            // The source triangle points down: the top one points down at the bar, the bottom one up.
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, side > 0f ? 0f : 180f);
            UIPrefabBuilder.AddImage(arrowRect.gameObject, triangle, Color.white, false).preserveAspect = false;
        }

        var view = root.gameObject.AddComponent<ReleaseBarView>();
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_bar", red.rectTransform);
        UIPrefabBuilder.SetRef(view, "_yellow", yellow.rectTransform);
        UIPrefabBuilder.SetRef(view, "_green", green.rectTransform);
        UIPrefabBuilder.SetRef(view, "_indicator", indicator);
        UIPrefabBuilder.SetRef(view, "_glow", glow);
        UIPrefabBuilder.SetRef(view, "_flash", flash);
    }

    // Toasts get their own canvas between popups (100) and tooltips (200), so a "purchased!" or foul message
    // is never hidden behind an open popup.
    public static ToastView BuildToast()
    {
        CreateSceneCanvas("ToastCanvas", 150, out RectTransform parent);
        Object.DestroyImmediate(parent.GetComponentInParent<GraphicRaycaster>());
        Image panel = UIPrefabBuilder.Panel(parent, "Toast", new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(760f, 120f), new Color(0.1f, 0.12f, 0.14f, 0.85f));
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        TMP_Text text = UIPrefabBuilder.CreateText(panel.transform, "Text", "", 52, Color.white, TextAlignmentOptions.Center);
        text.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Stretch(text.rectTransform, new Vector4(24f, 8f, 24f, 8f));
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 30f;
        text.fontSizeMax = 56f;

        var view = panel.gameObject.AddComponent<ToastView>();
        UIPrefabBuilder.SetRef(view, "_panel", panel.rectTransform);
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_background", panel);
        UIPrefabBuilder.SetRef(view, "_text", text);
        return view;
    }
    #endregion
}
