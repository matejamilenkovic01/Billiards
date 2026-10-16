using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuilder
{
    public const string OutputPath = "Builds/Android/Billiards.apk";

    [MenuItem("Tools/Billiards/Build Android APK")]
    public static string Build()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        // Plain APK for sideloading; signed with Unity's debug keystore.
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.development = false;

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        BuildSummary summary = report.summary;
        // summary.totalSize counts uncompressed content; the APK on disk is what gets installed.
        float apkMegabytes = File.Exists(OutputPath) ? new FileInfo(OutputPath).Length / (1024f * 1024f) : 0f;
        string result = $"{summary.result}: {OutputPath} ({apkMegabytes:F1} MB, " +
                        $"{summary.totalTime.TotalSeconds:F0}s, {summary.totalErrors} errors, {summary.totalWarnings} warnings)";
        Debug.Log($"AndroidBuilder: {result}");

        if (summary.result != BuildResult.Succeeded)
            throw new Exception($"Android build failed: {result}");

        return result;
    }
}
