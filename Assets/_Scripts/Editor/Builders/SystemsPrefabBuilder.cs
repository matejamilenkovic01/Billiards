using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class SystemsPrefabBuilder
{
    public const string PrefabPath = "Assets/Resources/Systems.prefab";

    private const string KenneyAudio = "Assets/_Audio/Kenney";
    private const string MusicPath = "Assets/_Audio/Music/coffee_house_bump.wav";
    private const int SfxSourceCount = 8;

    private static readonly (SoundId id, string path, float volume, float pitch)[] Sounds =
    {
        (SoundId.Click, KenneyAudio + "/UIAudio/click1.ogg", 0.8f, 1f),
        (SoundId.PopupOpen, KenneyAudio + "/InterfaceSounds/maximize_006.ogg", 0.6f, 1f),
        (SoundId.Whoosh, KenneyAudio + "/InterfaceSounds/minimize_006.ogg", 0.7f, 1f),
        (SoundId.Toast, KenneyAudio + "/InterfaceSounds/select_002.ogg", 0.5f, 1f),
        (SoundId.Error, KenneyAudio + "/InterfaceSounds/error_004.ogg", 0.6f, 1f),
        (SoundId.CueHit, KenneyAudio + "/ImpactSounds/impactWood_light_000.ogg", 0.9f, 1.15f),
        (SoundId.BallClick, KenneyAudio + "/ImpactSounds/impactPlate_light_000.ogg", 1f, 1.1f),
        (SoundId.Cushion, KenneyAudio + "/ImpactSounds/impactSoft_medium_000.ogg", 0.8f, 1f),
        (SoundId.Pocket, KenneyAudio + "/ImpactSounds/impactWood_medium_000.ogg", 0.9f, 0.75f),
        (SoundId.Foul, KenneyAudio + "/DigitalAudio/lowDown.ogg", 0.55f, 1f),
        (SoundId.Perfect, KenneyAudio + "/InterfaceSounds/glass_002.ogg", 0.7f, 1f),
        (SoundId.CoinTick, KenneyAudio + "/InterfaceSounds/tick_002.ogg", 0.5f, 1f),
        (SoundId.Purchase, KenneyAudio + "/InterfaceSounds/confirmation_002.ogg", 0.8f, 1f),
        (SoundId.Win, KenneyAudio + "/MusicJingles/jingle_win.ogg", 0.9f, 1f),
        (SoundId.Lose, KenneyAudio + "/MusicJingles/jingle_lose.ogg", 0.9f, 1f),
        (SoundId.TurnStart, KenneyAudio + "/InterfaceSounds/pluck_002.ogg", 0.6f, 1f)
    };

    [MenuItem("Tools/Billiards/Builders/Systems Prefab")]
    public static void Build()
    {
        using var scope = new TempObjectScope();
        var root = new GameObject("Systems");

        BuildAudio(root.transform);
        CreateChild<HapticsService>(root.transform, "HapticsService");
        CreateChild<InputRouter>(root.transform, "InputRouter");
        BuildProfileService(root.transform);
        BuildEventSystem(root.transform);
        BuildPopupManager(root.transform);
        BuildTooltipManager(root.transform);
        BuildSceneLoader(root.transform);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("SystemsPrefabBuilder: done");
    }

    private static void BuildAudio(Transform parent)
    {
        AudioManager audioManager = CreateChild<AudioManager>(parent, "AudioManager");
        GameObject go = audioManager.gameObject;
        // The only listener in the game: it persists across scenes, so scene cameras don't carry one.
        go.AddComponent<AudioListener>();

        var sfxSources = new AudioSource[SfxSourceCount];
        for (int i = 0; i < SfxSourceCount; i++)
        {
            sfxSources[i] = go.AddComponent<AudioSource>();
            sfxSources[i].playOnAwake = false;
        }

        AudioSource musicSource = go.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;

        var serialized = new SerializedObject(audioManager);
        SerializedProperty sources = serialized.FindProperty("_sfxSources");
        sources.arraySize = SfxSourceCount;
        for (int i = 0; i < SfxSourceCount; i++)
            sources.GetArrayElementAtIndex(i).objectReferenceValue = sfxSources[i];
        serialized.FindProperty("_musicSource").objectReferenceValue = musicSource;
        serialized.FindProperty("_musicClip").objectReferenceValue = LoadRequired<AudioClip>(MusicPath);

        SerializedProperty sounds = serialized.FindProperty("_sounds");
        sounds.arraySize = Sounds.Length;
        for (int i = 0; i < Sounds.Length; i++)
        {
            SerializedProperty element = sounds.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Id").enumValueIndex = (int)Sounds[i].id;
            element.FindPropertyRelative("Clip").objectReferenceValue = LoadRequired<AudioClip>(Sounds[i].path);
            element.FindPropertyRelative("Volume").floatValue = Sounds[i].volume;
            element.FindPropertyRelative("Pitch").floatValue = Sounds[i].pitch;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildProfileService(Transform parent)
    {
        ProfileService profile = CreateChild<ProfileService>(parent, "ProfileService");
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("_catalog").objectReferenceValue = LoadRequired<CueCatalog>(DataAssetBuilder.CueCatalogPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildEventSystem(Transform parent)
    {
        var go = new GameObject("EventSystem");
        go.transform.SetParent(parent, false);
        go.AddComponent<EventSystem>();
        // Actions left unassigned on purpose: the module falls back to the Input System's default UI
        // actions at runtime. Assigning them here would only store in-memory references that don't serialize.
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static readonly string[] PopupPaths =
    {
        UIPrefabBuilder.SettingsPopupPath, UIPrefabBuilder.CuesPopupPath, UIPrefabBuilder.ConfirmPopupPath, UIPrefabBuilder.NoEnergyPopupPath
    };

    private static void BuildPopupManager(Transform parent)
    {
        PopupManager manager = CreateChild<PopupManager>(parent, "PopupManager");
        CreateCanvas(manager.gameObject, 100, true);

        var serialized = new SerializedObject(manager);
        serialized.FindProperty("_container").objectReferenceValue = manager.transform;
        SerializedProperty prefabs = serialized.FindProperty("_popupPrefabs");
        prefabs.arraySize = PopupPaths.Length;
        for (int i = 0; i < PopupPaths.Length; i++)
            prefabs.GetArrayElementAtIndex(i).objectReferenceValue = LoadRequired<GameObject>(PopupPaths[i]).GetComponent<PopupBase>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildTooltipManager(Transform parent)
    {
        TooltipManager manager = CreateChild<TooltipManager>(parent, "TooltipManager");
        // No raycaster: the tooltip never blocks taps meant for the game.
        CreateCanvas(manager.gameObject, 200, false);

        var serialized = new SerializedObject(manager);
        serialized.FindProperty("_canvasRect").objectReferenceValue = manager.transform;
        serialized.FindProperty("_viewPrefab").objectReferenceValue =
            LoadRequired<GameObject>(UIPrefabBuilder.TooltipViewPath).GetComponent<TooltipView>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildSceneLoader(Transform parent)
    {
        SceneLoader loader = CreateChild<SceneLoader>(parent, "SceneLoader");
        CreateCanvas(loader.gameObject, 1000, true);
        var blocker = loader.gameObject.AddComponent<CanvasGroup>();
        blocker.blocksRaycasts = false;

        RectTransform top = BuildTransitionPanel(loader.transform, "TopPanel", true);
        RectTransform bottom = BuildTransitionPanel(loader.transform, "BottomPanel", false);

        var serialized = new SerializedObject(loader);
        serialized.FindProperty("_blocker").objectReferenceValue = blocker;
        serialized.FindProperty("_topPanel").objectReferenceValue = top;
        serialized.FindProperty("_bottomPanel").objectReferenceValue = bottom;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Each panel covers half the screen: dark cloth green, a wood-coloured edge and a soft shadow at the seam.
    private static RectTransform BuildTransitionPanel(Transform parent, string name, bool isTop)
    {
        Color panelColor = UIPrefabBuilder.Hex("123A26");
        Color edgeColor = UIPrefabBuilder.Hex("5A3219");
        const float overlap = 6f;
        const float edgeHeight = 18f;
        const float shadowHeight = 28f;

        RectTransform panel = UIPrefabBuilder.CreateRect(name, parent);
        // Start fully open (off screen); SceneLoader drives the anchors from here.
        panel.anchorMin = isTop ? new Vector2(0f, 1.04f) : new Vector2(0f, -0.54f);
        panel.anchorMax = isTop ? new Vector2(1f, 1.54f) : new Vector2(1f, -0.04f);
        panel.offsetMin = new Vector2(0f, isTop ? -overlap : 0f);
        panel.offsetMax = new Vector2(0f, isTop ? 0f : overlap);
        Image image = UIPrefabBuilder.AddImage(panel.gameObject, null, panelColor, false);
        image.raycastTarget = true;

        float seamY = isTop ? 0f : 1f;
        RectTransform shadow = UIPrefabBuilder.CreateRect("Shadow", panel);
        shadow.anchorMin = new Vector2(0f, seamY);
        shadow.anchorMax = new Vector2(1f, seamY);
        shadow.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
        shadow.sizeDelta = new Vector2(0f, shadowHeight);
        UIPrefabBuilder.AddImage(shadow.gameObject, null, new Color(0f, 0f, 0f, 0.25f), false);

        RectTransform edge = UIPrefabBuilder.CreateRect("Edge", panel);
        edge.anchorMin = new Vector2(0f, seamY);
        edge.anchorMax = new Vector2(1f, seamY);
        edge.pivot = new Vector2(0.5f, isTop ? 0f : 1f);
        edge.sizeDelta = new Vector2(0f, edgeHeight);
        UIPrefabBuilder.AddImage(edge.gameObject, null, edgeColor, false);

        return panel;
    }

    public static Canvas CreateCanvas(GameObject go, int sortingOrder, bool raycaster)
    {
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (raycaster)
            go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static T CreateChild<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    public static T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            Debug.LogError($"SystemsPrefabBuilder: missing asset {path}");
        return asset;
    }
}
