using System.IO;
using UnityEditor;
using UnityEngine;

// Poly Haven (CC0) PBR textures for the table (implementation_definition_v3 V6): downloaded once into
// _kenney_downloads/polyhaven/<id>/ (1K jpg: diffuse, OpenGL normal, roughness); only what is used is copied into
// Assets/_Art/PolyHaven/<id>/. Roughness becomes a URP metallic/smoothness map (A = 1 - roughness). The cloth
// albedo is made neutral grey so the venue's felt colour tints it; the pocket liner gets a vertical gradient
// (brown at the lip, near black at the bottom) baked over the leather. Missing downloads: the generated
// textures stay in use (BuildMaterials falls back), and the build says so.
public static class PolyHavenBuilder
{
    public const string Folder = "Assets/_Art/PolyHaven";
    public const string Wood = "lacquered_cherry_wood";
    public const string Cloth = "scuba_suede";
    public const string Leather = "brown_leather";

    public static string DownloadFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../../_kenney_downloads/polyhaven"));
    public static string AlbedoPath(string id) => $"{Folder}/{id}/{id}_albedo.png";
    public static string NormalPath(string id) => $"{Folder}/{id}/{id}_normal.jpg";
    public static string MaskPath(string id) => $"{Folder}/{id}/{id}_smoothness.png";
    public static string LinerAlbedoPath => $"{Folder}/{Leather}/pocket_liner_albedo.png";

    [MenuItem("Tools/Billiards/Builders/Poly Haven Textures")]
    public static string Build()
    {
        string report = "";
        foreach (string id in new[] { Wood, Cloth, Leather })
        {
            string source = Path.Combine(DownloadFolder, id);
            string diffuse = FindFile(source, "_diff_1k.jpg") ?? FindFile(source, "_albedo_1k.jpg");
            string normal = FindFile(source, "_nor_gl_1k.jpg");
            string rough = FindFile(source, "_rough_1k.jpg");
            if (diffuse == null || normal == null || rough == null)
            {
                report += $"{id}: missing download in {source}, generated textures stay\n";
                continue;
            }

            Directory.CreateDirectory($"{Folder}/{id}");
            Texture2D albedo = LoadJpg(diffuse);
            if (id == Cloth)
                Neutralise(albedo, 0.82f);
            WritePng(AlbedoPath(id), albedo);
            if (id == Leather)
                WritePng(LinerAlbedoPath, Liner(albedo));
            Object.DestroyImmediate(albedo);

            File.Copy(normal, Path.GetFullPath(NormalPath(id)), true);
            Texture2D roughness = LoadJpg(rough);
            WritePng(MaskPath(id), SmoothnessMask(roughness));
            Object.DestroyImmediate(roughness);
            report += $"{id}: albedo, normal, smoothness\n";
        }

        AssetDatabase.Refresh();
        foreach (string id in new[] { Wood, Cloth, Leather })
            ConfigureImports(id);
        Debug.Log("PolyHavenBuilder: " + report);
        return report.TrimEnd();
    }

    public static Texture2D LoadAlbedo(string id) => AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath(id));
    public static Texture2D LoadNormal(string id) => AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath(id));
    public static Texture2D LoadMask(string id) => AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath(id));

    private static void ConfigureImports(string id)
    {
        Configure(AlbedoPath(id), true, false);
        Configure(NormalPath(id), false, true);
        Configure(MaskPath(id), false, false);
        if (id == Leather)
            Configure(LinerAlbedoPath, true, false);
    }

    // Mipmaps, trilinear and anisotropic filtering against shimmer at a distance (V6).
    private static void Configure(string path, bool srgb, bool normalMap)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = 1024;
        importer.alphaSource = path.EndsWith("_smoothness.png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static string FindFile(string folder, string suffix)
    {
        if (!Directory.Exists(folder))
            return null;
        foreach (string file in Directory.GetFiles(folder))
        {
            if (file.EndsWith(suffix))
                return file;
        }
        return null;
    }

    private static Texture2D LoadJpg(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        texture.LoadImage(File.ReadAllBytes(path));
        return texture;
    }

    // Grey with the texture's light and dark detail, around `mean`: the material colour gives the hue.
    private static void Neutralise(Texture2D texture, float mean)
    {
        Color[] pixels = texture.GetPixels();
        double sum = 0;
        foreach (Color c in pixels)
            sum += c.grayscale;
        float scale = mean / Mathf.Max(0.01f, (float)(sum / pixels.Length));
        for (int i = 0; i < pixels.Length; i++)
        {
            float g = Mathf.Clamp01(pixels[i].grayscale * scale);
            pixels[i] = new Color(g, g, g, 1f);
        }
        texture.SetPixels(pixels);
        texture.Apply();
    }

    // URP metallic map: R = metallic (none), A = smoothness.
    private static Texture2D SmoothnessMask(Texture2D roughness)
    {
        Color[] pixels = roughness.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0f, 0f, 0f, 1f - pixels[i].r);
        var mask = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
        mask.SetPixels(pixels);
        mask.Apply();
        return mask;
    }

    // u around the pocket, v = 1 at the lip and 0 at the bottom: leather, darkening to almost black at the bottom.
    private static Texture2D Liner(Texture2D leather)
    {
        const int width = 512;
        const int height = 256;
        var liner = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        Color top = GeneratedArtBuilder.Hex("3B2618");
        Color bottom = GeneratedArtBuilder.Hex("070504");
        for (int y = 0; y < height; y++)
        {
            float depth = 1f - (y + 0.5f) / height;   // texture v = 1 at the top row
            Color tint = Color.Lerp(top, bottom, Mathf.SmoothStep(0f, 1f, depth));
            for (int x = 0; x < width; x++)
            {
                float grain = leather.GetPixelBilinear(x / (float)width * 2f, y / (float)height).grayscale;
                Color c = tint * Mathf.Lerp(0.75f, 1.25f, grain);
                c.a = 1f;
                liner.SetPixel(x, y, c);
            }
        }
        liner.Apply();
        return liner;
    }

    private static void WritePng(string path, Texture2D texture) => File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
}
