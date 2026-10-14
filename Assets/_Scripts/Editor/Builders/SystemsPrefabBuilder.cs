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
