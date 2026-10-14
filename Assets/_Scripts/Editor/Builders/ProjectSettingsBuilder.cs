using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ProjectSettingsBuilder
{
    public const string ScenesFolder = "Assets/_Scenes";
    public const string MenuScenePath = ScenesFolder + "/Menu.unity";
    public const string MatchScenePath = ScenesFolder + "/Match.unity";
    public const string MobileRenderPipelinePath = "Assets/Settings/Mobile_RPAsset.asset";

    // Template leftovers removed on the first run.
    private static readonly string[] TemplateLeftovers = { "Assets/Scenes", "Assets/TutorialInfo", "Assets/Readme.asset" };

    [MenuItem("Tools/Billiards/Builders/Project Settings")]
    public static void Apply()
    {
        PlayerSettings.companyName = "Mateja Milenkovic";
        PlayerSettings.productName = "Billiards";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.matejamilenkovic.billiards");
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.Android.bundleVersionCode = 1;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        ApplyMobileRenderPipeline();
        EnsureScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("ProjectSettingsBuilder: done");
    }

    // Slow (reimports for the platform); run on its own with --detach.
    [MenuItem("Tools/Billiards/Builders/Switch To Android")]
    public static string SwitchToAndroid()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            return "already Android";

        bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        return ok ? "switched to Android" : "switch failed";
    }

    private static void ApplyMobileRenderPipeline()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileRenderPipelinePath);
        if (asset == null)
        {
            Debug.LogError($"ProjectSettingsBuilder: missing {MobileRenderPipelinePath}");
            return;
        }

        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_MSAA").intValue = 2;
        serialized.FindProperty("m_RenderScale").floatValue = 1f;
        serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
        // The table's only shadow caster is the spot above it, which URP treats as an additional light.
        serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
        serialized.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 1024;
        // The room is small: a short shadow distance keeps the shadow map sharp.
        serialized.FindProperty("m_ShadowDistance").floatValue = 6f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureScenes()
    {
        if (!AssetDatabase.IsValidFolder(ScenesFolder))
            AssetDatabase.CreateFolder("Assets", "_Scenes");

        CreateEmptySceneIfMissing(MenuScenePath);
        CreateEmptySceneIfMissing(MatchScenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(MatchScenePath, true)
        };

        string activePath = EditorSceneManager.GetActiveScene().path;
        foreach (string leftover in TemplateLeftovers)
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(leftover)))
                continue;

            if (!string.IsNullOrEmpty(activePath) && activePath.StartsWith(leftover))
                EditorSceneManager.OpenScene(MatchScenePath);

            AssetDatabase.DeleteAsset(leftover);
        }
    }

    private static void CreateEmptySceneIfMissing(string path)
    {
        if (File.Exists(path))
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 55f;
        camera.nearClipPlane = 0.02f;
        camera.farClipPlane = 30f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.035f, 0.03f);
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2.2f, -1.6f), Quaternion.Euler(50f, 0f, 0f));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);

        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.CloseScene(scene, true);
    }
}
