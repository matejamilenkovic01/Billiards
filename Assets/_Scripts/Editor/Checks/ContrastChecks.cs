using System.IO;
using UnityEngine;

// v3 V13 contrast: button label against the button face (WCAG 2 contrast ratio), per button colour, from the
// sprites themselves (the face's middle band, with the builder's tint). Two numbers: the label fill, and the dark
// outline every label carries. Labels are 32 px and up, so the fill must reach WCAG large-text 3:1 and the fill or
// the outline 4.5:1; the weakest fill is named against 4.5:1.
public static class ContrastChecks
{
    public static string Buttons(float minimum = 4.5f, float largeText = 3f)
    {
        var report = new CheckReport("ContrastChecks.Buttons");
        string weakest = null;
        float weakestRatio = float.MaxValue;
        foreach (UIPrefabBuilder.ButtonColor color in System.Enum.GetValues(typeof(UIPrefabBuilder.ButtonColor)))
        {
            Color face = FaceColor($"{UIPrefabBuilder.Kenney}/UIPack/button_{color.ToString().ToLowerInvariant()}_normal.png") * UIPrefabBuilder.ButtonTint(color);
            Color text = UIPrefabBuilder.ButtonTextColor(color);
            float fill = Ratio(text, face);
            float edge = Ratio(UIPrefabBuilder.OutlineColor, face);
            float best = Mathf.Max(fill, edge);
            report.Info($"{color}: face #{ColorUtility.ToHtmlStringRGB(face)}, {(UIPrefabBuilder.HasDarkText(color) ? "dark" : "white")} text {fill:F1}:1, outline {edge:F1}:1");
            report.Check(best >= minimum - 0.005f, $"{color}: fill or outline {best:F1}:1 >= {minimum}:1");
            report.Check(fill >= largeText, $"{color}: fill {fill:F1}:1 >= {largeText}:1 (large text)");
            if (fill < weakestRatio)
            {
                weakestRatio = fill;
                weakest = color.ToString();
            }
        }

        // Disabled: grey face, the label dimmed to 80%.
        Color grey = FaceColor($"{UIPrefabBuilder.Kenney}/UIPack/button_grey_normal.png");
        Color dimmed = Color.Lerp(grey, UIPrefabBuilder.OutlineColor, 0.8f);
        float disabled = Ratio(dimmed, grey);
        report.Info($"Disabled (grey, dark label at 80%): {disabled:F1}:1");
        report.Check(disabled >= 3f, $"disabled label still readable ({disabled:F1}:1 >= 3:1)");
        report.Info($"weakest label fill: {weakest} {weakestRatio:F1}:1 ({(weakestRatio >= minimum ? "meets" : "below")} {minimum}:1; the outline carries it)");
        return report.ToString();
    }

    // Mean of the opaque pixels in the middle band of the sprite (no rounded corners, no bottom depth edge).
    private static Color FaceColor(string assetPath)
    {
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath)));
        Color sum = Color.clear;
        int count = 0;
        for (int y = (int)(texture.height * 0.35f); y < texture.height * 0.8f; y++)
        {
            for (int x = (int)(texture.width * 0.15f); x < texture.width * 0.85f; x++)
            {
                Color c = texture.GetPixel(x, y);
                if (c.a < 0.9f)
                    continue;
                sum += c;
                count++;
            }
        }
        Object.DestroyImmediate(texture);
        Color mean = count > 0 ? sum / count : Color.grey;
        mean.a = 1f;
        return mean;
    }

    public static float Ratio(Color a, Color b)
    {
        float la = Luminance(a), lb = Luminance(b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    private static float Luminance(Color c) => 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

    private static float Linear(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
}
