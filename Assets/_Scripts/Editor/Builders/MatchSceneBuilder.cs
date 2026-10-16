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
        var cue = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.CuePath));

        CameraRig rig = camera.gameObject.AddComponent<CameraRig>();
        UIPrefabBuilder.SetRef(rig, "_config", config);
        UIPrefabBuilder.SetRef(rig, "_table", tableView);

        var guide = new GameObject("AimGuide").AddComponent<AimGuideView>();
        UIPrefabBuilder.SetRef(guide, "_config", config);
        UIPrefabBuilder.SetRef(guide, "_table", tableView);
        UIPrefabBuilder.SetRef(guide, "_lineMaterial", AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("GuideLine")));
        UIPrefabBuilder.SetRef(guide, "_solidMaterial", AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("GuideSolid")));
        UIPrefabBuilder.SetRef(guide, "_handSprite", UIPrefabBuilder.LoadSprite(Generated + "/icon_hand.png"));

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

    // A dark room: almost no ambient, the lamp does the work.
    public static void ApplyRoomLighting()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.27f, 0.22f);
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
        BuildBanner(canvas.transform);
        BuildToast();
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
        band.sizeDelta = new Vector2(0f, 150f);
        Image background = UIPrefabBuilder.AddImage(band.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), new Color(0.18f, 0.62f, 0.32f, 0.95f), false);
        TMP_Text text = UIPrefabBuilder.CreateText(band, "Text", "YOUR TURN", 84, Color.white, TextAlignmentOptions.Center);
        text.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Stretch(text.rectTransform, new Vector4(40f, 0f, 40f, 0f));
        // Two-player names ("PLAYER 2 · BALL IN HAND") are longer: shrink to fit.
        text.enableAutoSizing = true;
        text.fontSizeMin = 40f;
        text.fontSizeMax = 84f;

        var view = root.gameObject.AddComponent<TurnBannerView>();
        UIPrefabBuilder.SetRef(view, "_band", band);
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_background", background);
        UIPrefabBuilder.SetRef(view, "_text", text);
    }

    private static void BuildShotControls(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("ShotControls", parent);
        UIPrefabBuilder.Stretch(root, Vector4.zero);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var view = root.gameObject.AddComponent<ShotControlsView>();

        GameObject fine = BuildFineAim(root);
        GameObject power = BuildPowerStrip(root);
        BuildReleaseBar(root);

        UIButton look = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.Look, root, Vector2.zero);
        UIPrefabBuilder.Anchor((RectTransform)look.transform, new Vector2(0f, 0f), new Vector2(240f, 110f), new Vector2(420f, 130f));
        UIButton move = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.MoveBall, root, Vector2.zero);
        UIPrefabBuilder.Anchor((RectTransform)move.transform, new Vector2(0f, 0f), new Vector2(240f, 250f), new Vector2(420f, 120f));
        UIButton done = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.Done, root, Vector2.zero);
        UIPrefabBuilder.Anchor((RectTransform)done.transform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(380f, 150f));

        TMP_Text hint = UIPrefabBuilder.CreateText(root, "Hint", "Place the cue ball behind the line", 38, UIPrefabBuilder.TextLight, TextAlignmentOptions.Center);
        hint.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 290f), new Vector2(980f, 70f));

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

    // Left: vertical "wheel" for fine aim.
    private static GameObject BuildFineAim(RectTransform parent)
    {
        Image panel = UIPrefabBuilder.Panel(parent, "FineAimWheel", new Vector2(0f, 0.5f), new Vector2(85f, -60f), new Vector2(120f, 640f), new Color(0f, 0f, 0f, 0.4f));
        panel.raycastTarget = true;
        var mask = panel.gameObject.AddComponent<RectMask2D>();
        mask.padding = new Vector4(0f, 12f, 0f, 12f);

        var ticks = new RectTransform[14];
        for (int i = 0; i < ticks.Length; i++)
        {
            RectTransform tick = UIPrefabBuilder.CreateRect("Tick", panel.transform);
            UIPrefabBuilder.Anchor(tick, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 6f));
            UIPrefabBuilder.AddImage(tick.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), new Color(1f, 1f, 1f, 0.75f), false);
            ticks[i] = tick;
        }

        TMP_Text label = UIPrefabBuilder.CreateText(parent, "FineAimLabel", "FINE\nAIM", 30, UIPrefabBuilder.TextMuted, TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.Normal;
        UIPrefabBuilder.Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(85f, 310f), new Vector2(160f, 80f));

        var view = panel.gameObject.AddComponent<FineAimWheelView>();
        var serialized = new SerializedObject(view);
        SerializedProperty array = serialized.FindProperty("_ticks");
        array.arraySize = ticks.Length;
        for (int i = 0; i < ticks.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = ticks[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // The label belongs to the wheel so it hides with it.
        label.transform.SetParent(panel.transform, true);
        return panel.gameObject;
    }

    // Right: the cue strip you pull down.
    private static GameObject BuildPowerStrip(RectTransform parent)
    {
        Image panel = UIPrefabBuilder.Panel(parent, "PowerStrip", new Vector2(1f, 0.5f), new Vector2(-105f, -40f), new Vector2(170f, 1000f), new Color(0f, 0f, 0f, 0.4f));
        panel.raycastTarget = true;

        RectTransform clip = UIPrefabBuilder.CreateRect("Clip", panel.transform);
        UIPrefabBuilder.Stretch(clip, new Vector4(10f, 10f, 10f, 10f));
        clip.gameObject.AddComponent<RectMask2D>();

        RectTransform fillRect = UIPrefabBuilder.CreateRect("Fill", clip);
        UIPrefabBuilder.Stretch(fillRect, Vector4.zero);
        Image fill = UIPrefabBuilder.AddImage(fillRect.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), new Color(1f, 0.62f, 0.2f, 0.28f), false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Top;
        fill.fillAmount = 0f;

        // The cue sprite is horizontal (tip on the right); turned 90 degrees it points up.
        RectTransform cue = UIPrefabBuilder.CreateRect("Cue", clip);
        UIPrefabBuilder.Anchor(cue, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(880f, 58f));
        cue.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Image cueImage = UIPrefabBuilder.AddImage(cue.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/cue_rookie.png"), Color.white, false);
        cueImage.preserveAspect = false;

        RectTransform hint = UIPrefabBuilder.CreateRect("PullHint", panel.transform);
        UIPrefabBuilder.Anchor(hint, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(150f, 180f));
        RectTransform arrow = UIPrefabBuilder.CreateRect("Arrow", hint);
        UIPrefabBuilder.Anchor(arrow, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(70f, 70f));
        UIPrefabBuilder.AddImage(arrow.gameObject, UIPrefabBuilder.LoadSprite(UIPrefabBuilder.Icons + "/icon_right.png"), new Color(1f, 1f, 1f, 0.8f), false);
        arrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
        TMP_Text pullText = UIPrefabBuilder.CreateText(hint, "Label", "PULL", 36, Color.white, TextAlignmentOptions.Center);
        pullText.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(pullText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(150f, 50f));

        TMP_Text percent = UIPrefabBuilder.CreateText(panel.transform, "PowerPercent", "", 54, Color.white, TextAlignmentOptions.Center);
        percent.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(percent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 45f), new Vector2(220f, 80f));

        var view = panel.gameObject.AddComponent<PowerStripView>();
        UIPrefabBuilder.SetRef(view, "_cue", cue);
        UIPrefabBuilder.SetRef(view, "_cueImage", cueImage);
        UIPrefabBuilder.SetRef(view, "_percent", percent);
        UIPrefabBuilder.SetRef(view, "_hint", hint.gameObject);
        UIPrefabBuilder.SetRef(view, "_fill", fill);
        return panel.gameObject;
    }

    // Left of the power strip: a framed, glossy bar (red / yellow / green) with a glowing double-arrow indicator.
    private static void BuildReleaseBar(RectTransform parent)
    {
        Sprite round = UIPrefabBuilder.LoadSprite(Generated + "/ui_round_24.png");
        Sprite white = UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png");
        RectTransform root = UIPrefabBuilder.CreateRect("ReleaseBar", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(1f, 0.5f), new Vector2(-268f, -40f), new Vector2(150f, 800f));
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        TMP_Text label = UIPrefabBuilder.CreateText(root, "Label", "RELEASE", 26, UIPrefabBuilder.TextMuted, TextAlignmentOptions.Center);
        label.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 392f), new Vector2(200f, 40f));

        UIPrefabBuilder.Panel(root, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(6f, -8f), new Vector2(100f, 744f), new Color(0f, 0f, 0f, 0.45f));
        UIPrefabBuilder.Panel(root, "Border", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 748f), UIPrefabBuilder.Hex("4C6B55"));
        UIPrefabBuilder.Panel(root, "Frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 740f), UIPrefabBuilder.Hex("0E1511"));

        Image red = UIPrefabBuilder.Panel(root, "Bar", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 700f), UIPrefabBuilder.Hex("C9352C"));
        Image yellow = UIPrefabBuilder.Panel(red.transform, "Yellow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 280f), UIPrefabBuilder.Hex("F2C230"));
        Image green = UIPrefabBuilder.Panel(red.transform, "Green", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 70f), UIPrefabBuilder.Hex("3DCB5B"));
        red.pixelsPerUnitMultiplier = yellow.pixelsPerUnitMultiplier = green.pixelsPerUnitMultiplier = 2.2f;

        RectTransform center = UIPrefabBuilder.CreateRect("Center", red.transform);
        UIPrefabBuilder.Anchor(center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 3f));
        UIPrefabBuilder.AddImage(center.gameObject, white, new Color(0f, 0f, 0f, 0.35f), false);

        RectTransform gloss = UIPrefabBuilder.CreateRect("Gloss", red.transform);
        UIPrefabBuilder.Stretch(gloss, new Vector4(0f, 6f, 0f, 6f));
        Image glossImage = UIPrefabBuilder.AddImage(gloss.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_gloss.png"), Color.white, false);
        glossImage.preserveAspect = false;

        RectTransform indicator = UIPrefabBuilder.CreateRect("Indicator", red.transform);
        UIPrefabBuilder.Anchor(indicator, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 40f));
        RectTransform glowRect = UIPrefabBuilder.CreateRect("Glow", indicator);
        UIPrefabBuilder.Anchor(glowRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 110f));
        Image glow = UIPrefabBuilder.AddImage(glowRect.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_glow.png"), new Color(0.35f, 1f, 0.45f, 0.8f), false);
        glow.preserveAspect = false;
        RectTransform outline = UIPrefabBuilder.CreateRect("Outline", indicator);
        UIPrefabBuilder.Anchor(outline, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(98f, 16f));
        UIPrefabBuilder.AddImage(outline.gameObject, round, new Color(0f, 0f, 0f, 0.8f), true).pixelsPerUnitMultiplier = 6f;
        RectTransform line = UIPrefabBuilder.CreateRect("Line", indicator);
        UIPrefabBuilder.Anchor(line, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 10f));
        UIPrefabBuilder.AddImage(line.gameObject, round, Color.white, true).pixelsPerUnitMultiplier = 8f;
        foreach (float side in new[] { -1f, 1f })
        {
            RectTransform arrow = UIPrefabBuilder.CreateRect(side < 0f ? "ArrowLeft" : "ArrowRight", indicator);
            UIPrefabBuilder.Anchor(arrow, new Vector2(0.5f, 0.5f), new Vector2(side * 64f, 0f), new Vector2(30f, 18f));
            // The source triangle points down; turned, it points at the bar from each side.
            arrow.localRotation = Quaternion.Euler(0f, 0f, side < 0f ? 90f : -90f);
            UIPrefabBuilder.AddImage(arrow.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/tooltip_arrow.png"), Color.white, false).preserveAspect = false;
        }

        var view = root.gameObject.AddComponent<ReleaseBarView>();
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_bar", red.rectTransform);
        UIPrefabBuilder.SetRef(view, "_yellow", yellow.rectTransform);
        UIPrefabBuilder.SetRef(view, "_green", green.rectTransform);
        UIPrefabBuilder.SetRef(view, "_indicator", indicator);
        UIPrefabBuilder.SetRef(view, "_glow", glow);
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
