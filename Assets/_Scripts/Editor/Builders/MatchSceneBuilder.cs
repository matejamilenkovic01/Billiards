using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class MatchSceneBuilder
{
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
        var cue = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.CuePath));

        var debug = new GameObject("DebugShot").AddComponent<DebugShot>();
        TablePrefabBuilder.SetField(debug, "_config", config);
        TablePrefabBuilder.SetField(debug, "_table", table.GetComponent<TableView>());
        TablePrefabBuilder.SetField(debug, "_cue", cue.GetComponent<CueView>());
        TablePrefabBuilder.SetField(debug, "_catalog", AssetDatabase.LoadAssetAtPath<CueCatalog>(DataAssetBuilder.CueCatalogPath));

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
}
