using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ArtImportBuilder
{
    public const string KenneyArt = "Assets/_Art/Kenney";
    public const string GeneratedArt = "Assets/_Art/Generated";
    public const string KenneyAudio = "Assets/_Audio/Kenney";
    public const string MusicAudio = "Assets/_Audio/Music";
    public const string GeneratedAudio = "Assets/_Audio/Generated";

    private const float UIPixelsPerUnit = 100f;

    // 9-slice borders (left, bottom, right, top) in source pixels.
    private static readonly Dictionary<string, Vector4> Borders = new()
    {
        { "_normal.png", new Vector4(20, 26, 20, 20) },
        { "_pressed.png", new Vector4(20, 20, 20, 20) },
        { "UIPack/slider_track.png", new Vector4(14, 14, 14, 14) },
        { "UIPack/slider_fill.png", new Vector4(14, 14, 14, 14) },
        { "UIPack/panel_bg.png", new Vector4(20, 20, 20, 20) },
        { "ui_round_", new Vector4(24, 24, 24, 24) },
        { "ui_white.png", new Vector4(1, 1, 1, 1) }
    };

    // Textures used on 3D materials rather than as UI sprites.
    private static readonly string[] WorldTextureKeys = { "Generated/ball_", "Generated/felt", "Generated/wood", "_albedo", "Generated/guide", "Generated/inlay", "Generated/carpet", "Generated/wallpaper" };

    // Tileable world textures.
    private static readonly string[] RepeatKeys = { "Generated/ball_", "Generated/felt", "Generated/wood", "Generated/guide", "Generated/inlay", "Generated/carpet", "Generated/wallpaper" };

    [MenuItem("Tools/Billiards/Builders/Art Import")]
    public static void Apply()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { KenneyArt, GeneratedArt }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (MatchesAny(path, WorldTextureKeys))
                ApplyWorldTexture(path);
            else
                ApplySprite(path);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { KenneyAudio }))
            ApplySfx(AssetDatabase.GUIDToAssetPath(guid));

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", ExistingFolders(MusicAudio, GeneratedAudio)))
            ApplyMusic(AssetDatabase.GUIDToAssetPath(guid));

        Debug.Log("ArtImportBuilder: done");
    }

    private static void ApplySprite(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = UIPixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        // Full rect so sliced and filled images never get clipped to a tight outline.
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);

        importer.spriteBorder = FindValue(path, Borders, Vector4.zero);
        importer.SaveAndReimport();
    }

    private static void ApplyWorldTexture(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        // Guide textures are drawn transparent: keep their alpha. Everything else is opaque.
        // The cue albedo keeps its alpha too: it is the smoothness (wood / ferrule / tip).
        importer.alphaSource = path.Contains("Generated/guide") || path.Contains("Generated/inlay") || path.EndsWith("_albedo.png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.wrapMode = MatchesAny(path, RepeatKeys) && !path.Contains("guide_tip") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void ApplySfx(string path)
    {
        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
            return;

        importer.forceToMono = true;
        importer.defaultSampleSettings = new AudioImporterSampleSettings
        {
            loadType = AudioClipLoadType.DecompressOnLoad,
            compressionFormat = AudioCompressionFormat.Vorbis,
            quality = 0.7f,
            sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
        };
        importer.SaveAndReimport();
    }

    private static void ApplyMusic(string path)
    {
        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
            return;

        importer.forceToMono = false;
        importer.loadInBackground = true;
        importer.defaultSampleSettings = new AudioImporterSampleSettings
        {
            loadType = AudioClipLoadType.CompressedInMemory,
            compressionFormat = AudioCompressionFormat.Vorbis,
            quality = 0.5f,
            sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
        };
        importer.SaveAndReimport();
    }

    private static string[] ExistingFolders(params string[] folders) =>
        System.Array.FindAll(folders, AssetDatabase.IsValidFolder);

    private static bool MatchesAny(string path, string[] keys)
    {
        foreach (string key in keys)
        {
            if (path.Contains(key))
                return true;
        }
        return false;
    }

    private static T FindValue<T>(string path, Dictionary<string, T> table, T fallback)
    {
        foreach (var pair in table)
        {
            if (path.Contains(pair.Key))
                return pair.Value;
        }
        return fallback;
    }
}
