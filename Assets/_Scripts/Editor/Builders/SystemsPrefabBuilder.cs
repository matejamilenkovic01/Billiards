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

    // SoundId -> its variants (v3 V7). Kenney impact sounds are placeholders, pitched to sound like billiards
    // (click shorter and higher, cushion dull and lower, pocket wooden and lower). When real billiard files are in
    // _kenney_downloads/billiard_sfx/ (names from the gameplay doc P2: ball_click_01.wav ...), RealPrefix picks them
    // up instead: only this table changes.
    private static readonly (SoundId id, string[] paths, string realPrefix, float volume, float pitch)[] Sounds =
    {
        (SoundId.Click, new[] { KenneyAudio + "/UIAudio/click1.ogg" }, null, 0.8f, 1f),
        (SoundId.PopupOpen, new[] { KenneyAudio + "/InterfaceSounds/maximize_006.ogg" }, null, 0.6f, 1f),
        (SoundId.Whoosh, new[] { KenneyAudio + "/InterfaceSounds/minimize_006.ogg" }, null, 0.7f, 1f),
        (SoundId.Toast, new[] { KenneyAudio + "/InterfaceSounds/select_002.ogg" }, null, 0.5f, 1f),
        (SoundId.Error, new[] { KenneyAudio + "/InterfaceSounds/error_004.ogg" }, null, 0.6f, 1f),
        (SoundId.CueHitSoft, new[] { KenneyAudio + "/ImpactSounds/impactWood_light_000.ogg", KenneyAudio + "/ImpactSounds/impactWood_light_001.ogg", KenneyAudio + "/ImpactSounds/impactWood_light_002.ogg", KenneyAudio + "/ImpactSounds/impactWood_light_003.ogg", KenneyAudio + "/ImpactSounds/impactWood_light_004.ogg" }, "cue_hit_soft", 0.85f, 1.2f),
        (SoundId.CueHitHard, new[] { KenneyAudio + "/ImpactSounds/impactWood_medium_000.ogg", KenneyAudio + "/ImpactSounds/impactWood_medium_001.ogg", KenneyAudio + "/ImpactSounds/impactWood_medium_002.ogg", KenneyAudio + "/ImpactSounds/impactWood_medium_003.ogg", KenneyAudio + "/ImpactSounds/impactWood_medium_004.ogg" }, "cue_hit_hard", 0.95f, 1.12f),
        (SoundId.Break, new[] { KenneyAudio + "/ImpactSounds/impactPlate_heavy_000.ogg", KenneyAudio + "/ImpactSounds/impactPlate_heavy_001.ogg", KenneyAudio + "/ImpactSounds/impactPlate_heavy_002.ogg" }, "break", 1f, 1.05f),
        (SoundId.BallClick, new[] { KenneyAudio + "/ImpactSounds/impactPlate_light_000.ogg", KenneyAudio + "/ImpactSounds/impactPlate_light_001.ogg", KenneyAudio + "/ImpactSounds/impactPlate_light_002.ogg", KenneyAudio + "/ImpactSounds/impactPlate_light_003.ogg", KenneyAudio + "/ImpactSounds/impactPlate_light_004.ogg" }, "ball_click", 1f, 1.22f),
        (SoundId.Cushion, new[] { KenneyAudio + "/ImpactSounds/impactSoft_medium_000.ogg", KenneyAudio + "/ImpactSounds/impactSoft_medium_001.ogg", KenneyAudio + "/ImpactSounds/impactSoft_medium_002.ogg", KenneyAudio + "/ImpactSounds/impactSoft_medium_003.ogg", KenneyAudio + "/ImpactSounds/impactSoft_medium_004.ogg" }, "cushion", 0.8f, 0.85f),
        (SoundId.PocketDrop, new[] { KenneyAudio + "/ImpactSounds/impactWood_heavy_000.ogg", KenneyAudio + "/ImpactSounds/impactWood_heavy_001.ogg", KenneyAudio + "/ImpactSounds/impactWood_heavy_002.ogg" }, "pocket_drop", 0.9f, 0.72f),
        (SoundId.PocketRattle, new[] { KenneyAudio + "/ImpactSounds/impactPlank_medium_000.ogg", KenneyAudio + "/ImpactSounds/impactPlank_medium_001.ogg", KenneyAudio + "/ImpactSounds/impactPlank_medium_002.ogg" }, "pocket_rattle", 0.5f, 0.85f),
        (SoundId.BallRollLoop, new[] { GeneratedAudioFolder + "/ball_roll_loop.wav" }, "ball_roll_loop", 1f, 1f),
        (SoundId.RoomAmbience, new[] { GeneratedAudioFolder + "/room_ambience_loop.wav" }, "room_ambience_loop", 1f, 1f),
        (SoundId.ComboChime, new[] { KenneyAudio + "/InterfaceSounds/bong_001.ogg" }, null, 0.5f, 1.2f),
        (SoundId.Foul, new[] { KenneyAudio + "/DigitalAudio/lowDown.ogg" }, null, 0.55f, 1f),
        (SoundId.Perfect, new[] { KenneyAudio + "/InterfaceSounds/glass_002.ogg" }, null, 0.7f, 1f),
        (SoundId.CoinTick, new[] { KenneyAudio + "/InterfaceSounds/tick_002.ogg" }, null, 0.5f, 1f),
        (SoundId.Purchase, new[] { KenneyAudio + "/InterfaceSounds/confirmation_002.ogg" }, null, 0.8f, 1f),
        (SoundId.Win, new[] { KenneyAudio + "/MusicJingles/jingle_win.ogg" }, null, 0.9f, 1f),
        (SoundId.Lose, new[] { KenneyAudio + "/MusicJingles/jingle_lose.ogg" }, null, 0.9f, 1f),
        (SoundId.TurnStart, new[] { KenneyAudio + "/InterfaceSounds/pluck_002.ogg" }, null, 0.6f, 1f)
    };

    private const string GeneratedAudioFolder = "Assets/_Audio/Generated";
    private const string BilliardAudioFolder = "Assets/_Audio/Billiard";
    private static string BilliardDownloads => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../_kenney_downloads/billiard_sfx"));

    [MenuItem("Tools/Billiards/Builders/Systems Prefab")]
    public static void Build()
    {
        using var scope = new TempObjectScope();
        var root = new GameObject("Systems");

        BuildAudio(root.transform);
        CreateChild<HapticsService>(root.transform, "HapticsService");
        CreateChild<HintService>(root.transform, "HintService");
        CreateChild<RewardedAdService>(root.transform, "RewardedAdService");
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

        AudioSource rollSource = go.AddComponent<AudioSource>();
        rollSource.playOnAwake = false;
        rollSource.loop = true;
        AudioSource ambienceSource = go.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        serialized.FindProperty("_rollSource").objectReferenceValue = rollSource;
        serialized.FindProperty("_ambienceSource").objectReferenceValue = ambienceSource;

        GeneratedAudio.WriteLoops(GeneratedAudioFolder);
        SerializedProperty sounds = serialized.FindProperty("_sounds");
        sounds.arraySize = Sounds.Length;
        for (int i = 0; i < Sounds.Length; i++)
        {
            SerializedProperty element = sounds.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Id").enumValueIndex = (int)Sounds[i].id;
            string[] paths = RealBilliardFiles(Sounds[i].realPrefix) ?? Sounds[i].paths;
            SerializedProperty clips = element.FindPropertyRelative("Clips");
            clips.arraySize = paths.Length;
            for (int c = 0; c < paths.Length; c++)
                clips.GetArrayElementAtIndex(c).objectReferenceValue = LoadRequired<AudioClip>(paths[c]);
            element.FindPropertyRelative("Volume").floatValue = Sounds[i].volume;
            // Real recordings play at their own pitch; the placeholders are pitched to sound like billiards.
            element.FindPropertyRelative("Pitch").floatValue = paths == Sounds[i].paths ? Sounds[i].pitch : 1f;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // Real billiard files (prefix_01.wav, prefix_02.ogg ...) from the download folder, copied into the project; null if none.
    private static string[] RealBilliardFiles(string prefix)
    {
        if (string.IsNullOrEmpty(prefix) || !System.IO.Directory.Exists(BilliardDownloads))
            return null;
        var found = new System.Collections.Generic.List<string>();
        foreach (string file in System.IO.Directory.GetFiles(BilliardDownloads))
        {
            string name = System.IO.Path.GetFileName(file);
            string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            if (!name.StartsWith(prefix) || (ext != ".wav" && ext != ".ogg" && ext != ".mp3"))
                continue;
            System.IO.Directory.CreateDirectory(BilliardAudioFolder);
            string target = BilliardAudioFolder + "/" + name;
            System.IO.File.Copy(file, System.IO.Path.GetFullPath(target), true);
            found.Add(target);
        }
        if (found.Count == 0)
            return null;
        AssetDatabase.Refresh();
        found.Sort(System.StringComparer.Ordinal);
        return found.ToArray();
    }

    private static void BuildProfileService(Transform parent)
    {
        ProfileService profile = CreateChild<ProfileService>(parent, "ProfileService");
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("_catalog").objectReferenceValue = LoadRequired<CueCatalog>(DataAssetBuilder.CueCatalogPath);
        serialized.FindProperty("_divisions").objectReferenceValue = LoadRequired<DivisionCatalog>(DataAssetBuilder.DivisionCatalogPath);
        serialized.FindProperty("_objectives").objectReferenceValue = LoadRequired<ObjectiveCatalog>(DataAssetBuilder.ObjectiveCatalogPath);
        serialized.FindProperty("_economy").objectReferenceValue = LoadRequired<EconomyConfig>(DataAssetBuilder.EconomyConfigPath);
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
        UIPrefabBuilder.SettingsPopupPath, UIPrefabBuilder.CuesPopupPath, UIPrefabBuilder.ConfirmPopupPath,
        UIPrefabBuilder.PausePopupPath, UIPrefabBuilder.MatchResultPopupPath, UIPrefabBuilder.RulesPopupPath,
        UIPrefabBuilder.RankedRoadPopupPath, UIPrefabBuilder.SimulatedAdPath, UIPrefabBuilder.StarterPackPopupPath,
        UIPrefabBuilder.ComingSoonPopupPath, UIPrefabBuilder.SurveyPopupPath
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
