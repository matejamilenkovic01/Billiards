using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class UIPrefabBuilder
{
    public const string UIFolder = "Assets/_Prefabs/UI";
    public const string PopupFolder = "Assets/_Prefabs/Popups";
    public const string FontFolder = "Assets/_Art/Fonts";
    public const string FontAssetPath = FontFolder + "/KenneyFuture SDF.asset";
    public const string OutlineMaterialPath = FontFolder + "/KenneyFuture SDF Outline.mat";
    public const string ButtonPath = UIFolder + "/UIButton.prefab";

    public const string Kenney = "Assets/_Art/Kenney";
    public const string Icons = Kenney + "/GameIcons";
    public const string Generated = "Assets/_Art/Generated";

    public static readonly Color OutlineColor = Hex("1B140E");
    public static readonly Color PanelColor = Hex("15201A");
    public static readonly Color TextLight = Hex("F3EBDD");
    public static readonly Color TextMuted = Hex("A9B5AA");
    public static readonly Color Gold = Hex("F6C445");

    public enum ButtonColor { Green, Yellow, Blue, Red, Grey }

    public enum ButtonVariant { Look, MoveBall, Done }

    private static readonly Dictionary<ButtonVariant, (string label, string icon, Vector2 size, float fontSize, ButtonColor color)> Variants = new()
    {
        { ButtonVariant.Look, ("LOOK", Generated + "/icon_eye.png", new Vector2(420, 130), 40, ButtonColor.Blue) },
        { ButtonVariant.MoveBall, ("MOVE BALL", Generated + "/icon_hand.png", new Vector2(420, 120), 38, ButtonColor.Blue) },
        { ButtonVariant.Done, ("DONE", Icons + "/icon_checkmark.png", new Vector2(380, 150), 60, ButtonColor.Green) }
    };

    [MenuItem("Tools/Billiards/Builders/UI Prefabs")]
    public static void BuildAll()
    {
        using var scope = new TempObjectScope();
        Directory.CreateDirectory(UIFolder);
        Directory.CreateDirectory(FontFolder);
        AssetDatabase.Refresh();

        EnsureFont();
        BuildButton();
        foreach (ButtonVariant variant in Variants.Keys)
            BuildButtonVariant(variant);

        AssetDatabase.SaveAssets();
        Debug.Log("UIPrefabBuilder: done");
    }

    public static string VariantPath(ButtonVariant variant) => $"{UIFolder}/UIButton_{variant}.prefab";

    #region Font
    public static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    public static Material OutlineMaterial => AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);

    // Created once and kept, so its GUID (referenced by every text) stays stable across rebuilds.
    private static void EnsureFont()
    {
        TMP_FontAsset fontAsset = Font;
        if (fontAsset == null)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Kenney + "/UIPack/KenneyFuture.ttf");
            fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            fontAsset.name = "KenneyFuture SDF";

            var characters = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++)
                characters.Append(c);
            fontAsset.TryAddCharacters(characters.ToString());
            // Static atlas: playing the game never rewrites the asset (no git noise).
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            fontAsset.material.name = "KenneyFuture SDF Material";
            fontAsset.atlasTexture.name = "KenneyFuture SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }

        // Symbols the Kenney font lacks (arrows, multiplication sign...) come from TMP's default font.
        fontAsset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        if (TMP_Settings.defaultFontAsset != null && !fontAsset.fallbackFontAssetTable.Contains(TMP_Settings.defaultFontAsset))
            fontAsset.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
        EditorUtility.SetDirty(fontAsset);

        Material outline = OutlineMaterial;
        if (outline == null)
        {
            outline = new Material(fontAsset.material) { name = "KenneyFuture SDF Outline" };
            AssetDatabase.CreateAsset(outline, OutlineMaterialPath);
        }
        outline.shader = fontAsset.material.shader;
        outline.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
        outline.EnableKeyword(ShaderUtilities.Keyword_Outline);
        outline.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        outline.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
        outline.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
        EditorUtility.SetDirty(outline);
    }
    #endregion

    #region Button
    public static Sprite ButtonSprite(ButtonColor color, bool pressed) =>
        LoadSprite($"{Kenney}/UIPack/button_{color.ToString().ToLowerInvariant()}_{(pressed ? "pressed" : "normal")}.png");

    private static void BuildButton()
    {
        RectTransform root = CreateRect("UIButton", null);
        root.sizeDelta = new Vector2(480, 150);
        Image image = AddImage(root.gameObject, ButtonSprite(ButtonColor.Yellow, false), Color.white, true);
        image.raycastTarget = true;

        RectTransform content = CreateRect("Content", root);
        Stretch(content, new Vector4(30, 22, 30, 10));
        var group = content.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform icon = CreateRect("Icon", content);
        AddImage(icon.gameObject, null, Color.white, false);
        var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 64f;
        iconLayout.preferredHeight = 64f;
        icon.gameObject.SetActive(false);

        TMP_Text label = CreateText(content, "Label", "BUTTON", 56, Color.white, TextAlignmentOptions.Center);
        label.fontSharedMaterial = OutlineMaterial;

        UIButton button = root.gameObject.AddComponent<UIButton>();
        var serialized = new SerializedObject(button);
        serialized.FindProperty("_normalSprite").objectReferenceValue = image.sprite;
        serialized.FindProperty("_pressedSprite").objectReferenceValue = ButtonSprite(ButtonColor.Yellow, true);
        serialized.FindProperty("_disabledSprite").objectReferenceValue = ButtonSprite(ButtonColor.Grey, false);
        serialized.FindProperty("_content").objectReferenceValue = content;
        serialized.FindProperty("_contentGroup").objectReferenceValue = group;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root.gameObject, ButtonPath);
    }

    private static void BuildButtonVariant(ButtonVariant variant)
    {
        var (label, icon, size, fontSize, color) = Variants[variant];
        GameObject instance = CreateButtonInstance(null, label, icon, size, fontSize, color);
        instance.name = $"UIButton_{variant}";
        PrefabUtility.SaveAsPrefabAsset(instance, VariantPath(variant));
        Object.DestroyImmediate(instance);
    }

    // A configured instance of the base button prefab (used for variants and one-off buttons).
    public static GameObject CreateButtonInstance(Transform parent, string label, string iconPath, Vector2 size, float fontSize, ButtonColor color)
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, parent);
        ((RectTransform)instance.transform).sizeDelta = size;

        var button = instance.GetComponent<UIButton>();
        var serialized = new SerializedObject(button);
        serialized.FindProperty("_normalSprite").objectReferenceValue = ButtonSprite(color, false);
        serialized.FindProperty("_pressedSprite").objectReferenceValue = ButtonSprite(color, true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        instance.GetComponent<Image>().sprite = ButtonSprite(color, false);

        Transform content = instance.transform.Find("Content");
        var labelText = content.Find("Label").GetComponent<TMP_Text>();
        labelText.gameObject.SetActive(label != null);
        if (label != null)
        {
            labelText.text = label;
            labelText.fontSize = fontSize;
        }

        var iconImage = content.Find("Icon").GetComponent<Image>();
        iconImage.gameObject.SetActive(iconPath != null);
        if (iconPath != null)
        {
            iconImage.sprite = LoadSprite(iconPath);
            float iconSize = label == null ? Mathf.Min(size.x, size.y) * 0.55f : fontSize * 1.2f;
            var iconLayout = iconImage.GetComponent<LayoutElement>();
            iconLayout.preferredWidth = iconSize;
            iconLayout.preferredHeight = iconSize;
        }
        return instance;
    }

    public static UIButton InstantiateButton(ButtonVariant variant, Transform parent, Vector2 anchoredPosition)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath(variant));
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        ((RectTransform)instance.transform).anchoredPosition = anchoredPosition;
        return instance.GetComponent<UIButton>();
    }
    #endregion

    #region Helpers
    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // padding: left, bottom, right, top
    public static void Stretch(RectTransform rect, Vector4 padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding.x, padding.y);
        rect.offsetMax = new Vector2(-padding.z, -padding.w);
    }

    public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static Image AddImage(GameObject go, Sprite sprite, Color color, bool sliced)
    {
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        // The 4px white sprite is stretched into bars and lines, never kept square.
        image.preserveAspect = !sliced && sprite != null && sprite.name != "ui_white";
        image.raycastTarget = false;
        return image;
    }

    public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Anchor(rect, anchor, position, size);
        Image image = AddImage(rect.gameObject, LoadSprite(Generated + "/ui_round_24.png"), color, true);
        image.pixelsPerUnitMultiplier = 1.5f;
        return image;
    }

    public static TMP_Text CreateText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = Font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    public static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"UIPrefabBuilder: missing sprite {path}");
        return sprite;
    }

    public static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    public static void SetRef(Object target, string field, Object value) => TablePrefabBuilder.SetField(target, field, value);

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
    #endregion
}
