using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// A WebGL build to open in a browser (also on a phone): a folder with index.html and Build/, uncompressed so any plain
// static host serves it (itch.io, Netlify, GitHub Pages, `python3 -m http.server`). Portrait page from
// Assets/WebGLTemplates/Billiards.
public static class WebGLBuilder
{
    public const string OutputFolder = "Builds/WebGL";

    [MenuItem("Tools/Billiards/Build WebGL")]
    public static string Build()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (Directory.Exists(OutputFolder))
            Directory.Delete(OutputFolder, true);
        Directory.CreateDirectory(OutputFolder);

        PlayerSettings.WebGL.template = "PROJECT:Billiards";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.initialMemorySize = 256;
        EditorUserBuildSettings.development = false;

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputFolder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });

        BuildSummary summary = report.summary;
        long bytes = Directory.Exists(OutputFolder)
            ? new DirectoryInfo(OutputFolder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
        string result = $"{summary.result}: {OutputFolder} ({bytes / (1024f * 1024f):F1} MB, {summary.totalTime.TotalSeconds:F0}s, " +
                        $"{summary.totalErrors} errors, {summary.totalWarnings} warnings)";
        Debug.Log($"WebGLBuilder: {result}");
        if (summary.result != BuildResult.Succeeded)
            throw new Exception($"WebGL build failed: {result}");
        return result;
    }
}
