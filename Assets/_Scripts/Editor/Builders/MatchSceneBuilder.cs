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
        RenderSettings.ambientLight = new Color(0.16f, 0.14f, 0.12f);
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
        BuildToast(safe);
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

    // Left of the power strip: red / yellow / green with the oscillating indicator.
    private static void BuildReleaseBar(RectTransform parent)
    {
        RectTransform root = UIPrefabBuilder.CreateRect("ReleaseBar", parent);
        UIPrefabBuilder.Anchor(root, new Vector2(1f, 0.5f), new Vector2(-265f, -40f), new Vector2(84f, 720f));
        var group = root.gameObject.AddComponent<CanvasGroup>();

        Image red = UIPrefabBuilder.Panel(root, "Bar", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 700f), UIPrefabBuilder.Hex("D8382E"));
        Image yellow = UIPrefabBuilder.Panel(red.transform, "Yellow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 280f), UIPrefabBuilder.Hex("F2C230"));
        Image green = UIPrefabBuilder.Panel(red.transform, "Green", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 70f), UIPrefabBuilder.Hex("39C455"));
        yellow.pixelsPerUnitMultiplier = green.pixelsPerUnitMultiplier = 3f;

        RectTransform indicator = UIPrefabBuilder.CreateRect("Indicator", red.transform);
        UIPrefabBuilder.Anchor(indicator, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 12f));
        RectTransform shadow = UIPrefabBuilder.CreateRect("Shadow", indicator);
        UIPrefabBuilder.Anchor(shadow, new Vector2(0.5f, 0.5f), new Vector2(3f, -4f), new Vector2(110f, 12f));
        UIPrefabBuilder.AddImage(shadow.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), new Color(0f, 0f, 0f, 0.5f), false);
        RectTransform line = UIPrefabBuilder.CreateRect("Line", indicator);
        UIPrefabBuilder.Stretch(line, Vector4.zero);
        UIPrefabBuilder.AddImage(line.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/ui_white.png"), Color.white, false);

        var view = root.gameObject.AddComponent<ReleaseBarView>();
        UIPrefabBuilder.SetRef(view, "_group", group);
        UIPrefabBuilder.SetRef(view, "_bar", red.rectTransform);
        UIPrefabBuilder.SetRef(view, "_yellow", yellow.rectTransform);
        UIPrefabBuilder.SetRef(view, "_green", green.rectTransform);
        UIPrefabBuilder.SetRef(view, "_indicator", indicator);
    }

    public static ToastView BuildToast(RectTransform parent)
    {
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
