using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static partial class UIPrefabBuilder
{
    public const string UIFolder = "Assets/_Prefabs/UI";
    public const string PopupFolder = "Assets/_Prefabs/Popups";
    public const string FontFolder = "Assets/_Art/Fonts";
    // v4 V8: Rubik (OFL). Bold for titles, buttons, badges and banners (written in capitals), Medium for anything
    // with lower case; Noto Sans Symbols 2 (OFL) behind both for the stars, checks, arrows and the no-entry sign.
    public const string FontAssetPath = FontFolder + "/Rubik/Rubik-Bold SDF.asset";
    public const string BodyFontPath = FontFolder + "/Rubik/Rubik-Medium SDF.asset";
    public const string SymbolsFontPath = FontFolder + "/NotoSymbols2/NotoSymbols2 SDF.asset";
    public const string OutlineMaterialPath = FontFolder + "/Rubik/Rubik-Bold SDF Outline.mat";
    public const string BodyOutlineMaterialPath = FontFolder + "/Rubik/Rubik-Medium SDF Outline.mat";
    // The static atlases: ASCII, the punctuation the texts use, and (symbols font) the signs Rubik lacks.
    public const string ExtraCharacters = "·—–“”‘’…›°×";
    public const string SymbolCharacters = "★☆✓✕▶◀⛔";
    public const string ButtonPath = UIFolder + "/UIButton.prefab";
    public const string SliderPath = UIFolder + "/UISlider.prefab";
    public const string TooltipViewPath = UIFolder + "/TooltipView.prefab";
    public const string CoinCounterPath = UIFolder + "/CoinCounter.prefab";
    public const string CueCardPath = UIFolder + "/CueCard.prefab";
    public const string SettingsPopupPath = PopupFolder + "/SettingsPopup.prefab";
    public const string CuesPopupPath = PopupFolder + "/CuesPopup.prefab";
    public const string ConfirmPopupPath = PopupFolder + "/ConfirmPopup.prefab";
    public const string PausePopupPath = PopupFolder + "/PausePopup.prefab";
    public const string CueTryResultPopupPath = PopupFolder + "/CueTryResultPopup.prefab";
    public const string RulesPopupPath = PopupFolder + "/RulesPopup.prefab";
    public const string MatchResultPopupPath = PopupFolder + "/MatchResultPopup.prefab";
    public const string DivisionsPopupPath = PopupFolder + "/DivisionsPopup.prefab";
    public const string ObjectivesPopupPath = PopupFolder + "/ObjectivesPopup.prefab";
    public const string CelebrationPopupPath = PopupFolder + "/CelebrationPopup.prefab";

    public const string Kenney = "Assets/_Art/Kenney";
    public const string Icons = Kenney + "/GameIcons";
    public const string Generated = "Assets/_Art/Generated";

    public static readonly Color OutlineColor = Hex("1B140E");
    public static readonly Color PanelColor = Hex("15201A");
    public static readonly Color TextLight = Hex("F3EBDD");
    public static readonly Color TextMuted = Hex("A9B5AA");
    public static readonly Color Gold = Hex("F6C445");

    public enum ButtonColor { Green, Yellow, Blue, Red, Grey, Square }

    public enum ButtonVariant { Look, MoveBall, Done, Play, Settings, Cues, Close, Confirm, Cancel, Pause, Resume, Forfeit, PlayAgain, Menu }

    private static readonly Dictionary<ButtonVariant, (string label, string icon, Vector2 size, float fontSize, ButtonColor color)> Variants = new()
    {
        { ButtonVariant.Look, ("LOOK", Generated + "/icon_eye.png", new Vector2(420, 130), 40, ButtonColor.Blue) },
        { ButtonVariant.MoveBall, ("MOVE BALL", Generated + "/icon_hand.png", new Vector2(420, 120), 38, ButtonColor.Blue) },
        { ButtonVariant.Done, ("DONE", Icons + "/icon_checkmark.png", new Vector2(380, 150), 60, ButtonColor.Green) },
        { ButtonVariant.Play, ("PLAY", Icons + "/icon_right.png", new Vector2(640, 190), 84, ButtonColor.Green) },
        { ButtonVariant.Settings, ("SETTINGS", Icons + "/icon_gear.png", new Vector2(580, 150), 56, ButtonColor.Blue) },
        { ButtonVariant.Cues, ("CUES", Icons + "/icon_star.png", new Vector2(580, 150), 56, ButtonColor.Yellow) },
        { ButtonVariant.Close, ("CLOSE", null, new Vector2(420, 140), 54, ButtonColor.Grey) },
        { ButtonVariant.Confirm, ("CONFIRM", null, new Vector2(340, 140), 48, ButtonColor.Green) },
        { ButtonVariant.Cancel, ("CANCEL", null, new Vector2(340, 140), 48, ButtonColor.Grey) },
        { ButtonVariant.Pause, (null, Icons + "/icon_pause.png", new Vector2(120, 120), 40, ButtonColor.Square) },
        { ButtonVariant.Resume, ("RESUME", Icons + "/icon_right.png", new Vector2(600, 150), 60, ButtonColor.Green) },
        { ButtonVariant.Forfeit, ("FORFEIT", Icons + "/icon_exit.png", new Vector2(600, 150), 60, ButtonColor.Red) },
        { ButtonVariant.PlayAgain, ("PLAY AGAIN", Icons + "/icon_return.png", new Vector2(620, 160), 60, ButtonColor.Green) },
        { ButtonVariant.Menu, ("MENU", Icons + "/icon_home.png", new Vector2(620, 140), 56, ButtonColor.Blue) }
    };

    [MenuItem("Tools/Billiards/Builders/UI Prefabs")]
    // v4 V14: the prefabs of the removed probes (rewarded ad overlay, Starter Pack, ONLINE RANKED, survey).
    private static void DeleteRetiredPrefabs()
    {
        foreach (string name in new[] { "RankedRoadPopup", "SimulatedAdOverlay", "Starter" + "PackPopup", "Coming" + "SoonPopup", "Sur" + "veyPopup" })
            AssetDatabase.DeleteAsset($"{PopupFolder}/{name}.prefab");
    }

    public static void BuildAll()
    {
        using var scope = new TempObjectScope();
        Directory.CreateDirectory(UIFolder);
        Directory.CreateDirectory(FontFolder);
        AssetDatabase.Refresh();

        Directory.CreateDirectory(PopupFolder);
        AssetDatabase.Refresh();

        EnsureFont();
        BuildButton();
        foreach (ButtonVariant variant in Variants.Keys)
            BuildButtonVariant(variant);
        BuildSlider();
        BuildTooltipView();
        BuildCoinCounter();
        BuildCueCard();
        BuildSettingsPopup();
        BuildConfirmPopup();
        BuildCuesPopup();
        BuildPausePopup();
        BuildCueTryResultPopup();
        BuildRulesPopup();
        BuildMatchResultPopup();
        BuildDivisionsPopup();
        BuildObjectivesPopup();
        BuildCelebrationPopup();
        DeleteRetiredPrefabs();

        AssetDatabase.SaveAssets();
        Debug.Log("UIPrefabBuilder: done");
    }

    public static string VariantPath(ButtonVariant variant) => $"{UIFolder}/UIButton_{variant}.prefab";

    #region Font
    public static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    public static TMP_FontAsset BodyFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
    public static Material OutlineMaterial => AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
    public static Material BodyOutlineMaterial => AssetDatabase.LoadAssetAtPath<Material>(BodyOutlineMaterialPath);

    // The outlined material for a text's own font (its atlas must match).
    public static Material OutlineFor(TMP_Text text) => text.font == BodyFont ? BodyOutlineMaterial : OutlineMaterial;

    // Lower case anywhere: a description (Medium); else a title, button or badge (Bold).
    public static TMP_FontAsset FontFor(string text)
    {
        foreach (char c in text ?? "")
        {
            if (char.IsLower(c))
                return BodyFont;
        }
        return Font;
    }

    // For texts filled at run time with sentences (hints, toasts, reasons): Medium, keeping an outline if it has one.
    public static void UseBodyFont(TMP_Text text)
    {
        bool outlined = text.fontSharedMaterial == OutlineMaterial || text.fontSharedMaterial == BodyOutlineMaterial;
        text.font = BodyFont;
        text.fontSharedMaterial = outlined ? BodyOutlineMaterial : BodyFont.material;
    }

    // Created once and kept, so their GUIDs (referenced by every text) stay stable across rebuilds.
    private static void EnsureFont()
    {
        TMP_FontAsset symbols = EnsureFontAsset(SymbolsFontPath, FontFolder + "/NotoSymbols2/NotoSansSymbols2-Regular.ttf", "NotoSymbols2 SDF", SymbolCharacters, 512);
        TMP_FontAsset bold = EnsureFontAsset(FontAssetPath, FontFolder + "/Rubik/Rubik-Bold.ttf", "Rubik-Bold SDF", Ascii() + ExtraCharacters, 1024);
        TMP_FontAsset body = EnsureFontAsset(BodyFontPath, FontFolder + "/Rubik/Rubik-Medium.ttf", "Rubik-Medium SDF", Ascii() + ExtraCharacters, 1024);
        foreach (TMP_FontAsset font in new[] { bold, body })
        {
            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            font.fallbackFontAssetTable.Clear();
            // Only the static symbols font: a dynamic fallback would rewrite its atlas while the game runs.
            font.fallbackFontAssetTable.Add(symbols);
            EditorUtility.SetDirty(font);
        }
        EnsureOutline(OutlineMaterialPath, bold);
        EnsureOutline(BodyOutlineMaterialPath, body);
    }

    private static string Ascii()
    {
        var characters = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++)
            characters.Append(c);
        return characters.ToString();
    }

    private static TMP_FontAsset EnsureFontAsset(string path, string ttf, string name, string characters, int atlas)
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (fontAsset != null)
            return fontAsset;
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, atlas, atlas, AtlasPopulationMode.Dynamic, false);
        fontAsset.name = name;
        fontAsset.TryAddCharacters(characters);
        // Static atlas: playing the game never rewrites the asset (no git noise).
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(fontAsset, path);
        fontAsset.material.name = name + " Material";
        fontAsset.atlasTexture.name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        return fontAsset;
    }

    private static void EnsureOutline(string path, TMP_FontAsset fontAsset)
    {
        var outline = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (outline == null)
        {
            outline = new Material(fontAsset.material) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(outline, path);
        }
        // The full SDF shader: the mobile one can't draw an outline and an underlay together.
        outline.shader = Shader.Find("TextMeshPro/Distance Field");
        outline.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
        outline.EnableKeyword(ShaderUtilities.Keyword_Outline);
        outline.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        outline.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
        outline.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
        // A soft drop shadow under every outlined label (v3 V13 contrast).
        outline.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        outline.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.45f));
        outline.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
        outline.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
        outline.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
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
        label.fontSharedMaterial = OutlineFor(label);

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
        ApplyMinTextSizes(instance);
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
        Image image = instance.GetComponent<Image>();
        image.sprite = ButtonSprite(color, false);
        // Contrast (v3 V13): saturated buttons ~15% darker, light ones get dark text.
        image.color = ButtonTint(color);

        Transform content = instance.transform.Find("Content");
        var labelText = content.Find("Label").GetComponent<TMP_Text>();
        labelText.gameObject.SetActive(label != null);
        labelText.color = ButtonTextColor(color);
        if (label != null)
        {
            labelText.text = label;
            labelText.fontSize = fontSize;
        }

        var iconImage = content.Find("Icon").GetComponent<Image>();
        iconImage.color = ButtonTextColor(color);
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

    public static bool HasDarkText(ButtonColor color) => color is ButtonColor.Yellow or ButtonColor.Grey or ButtonColor.Square;

    public static Color ButtonTextColor(ButtonColor color) => HasDarkText(color) ? OutlineColor : Color.white;

    // ~15-20% darker: enough for white labels to reach 3:1 (WCAG large text) on every saturated face (ContrastChecks).
    public static Color ButtonTint(ButtonColor color) => color switch
    {
        // v4 V8: green and blue darker, so white labels reach 4.5:1 on the fill.
        ButtonColor.Green => new Color(0.64f, 0.64f, 0.64f, 1f),
        ButtonColor.Red => new Color(0.82f, 0.82f, 0.82f, 1f),
        ButtonColor.Blue => new Color(0.71f, 0.71f, 0.71f, 1f),
        _ => Color.white
    };

    public static UIButton InstantiateButton(ButtonVariant variant, Transform parent, Vector2 anchoredPosition)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath(variant));
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        ((RectTransform)instance.transform).anchoredPosition = anchoredPosition;
        return instance.GetComponent<UIButton>();
    }
    #endregion

    #region Slider and tooltip
    private static void BuildSlider()
    {
        RectTransform root = CreateRect("UISlider", null);
        root.sizeDelta = new Vector2(680, 80);

        RectTransform background = CreateRect("Background", root);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.sizeDelta = new Vector2(0f, 36f);
        Image backgroundImage = AddImage(background.gameObject, LoadSprite(Kenney + "/UIPack/slider_track.png"), Color.white, true);
        // The whole bar is draggable, not only the handle.
        backgroundImage.raycastTarget = true;

        RectTransform fillArea = CreateRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 36f);
        RectTransform fill = CreateRect("Fill", fillArea);
        Stretch(fill, Vector4.zero);
        AddImage(fill.gameObject, LoadSprite(Kenney + "/UIPack/slider_fill.png"), Color.white, true);

        RectTransform handleArea = CreateRect("Handle Slide Area", root);
        Stretch(handleArea, new Vector4(28, 0, 28, 0));
        RectTransform handle = CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(60f, 80f);
        Image handleImage = AddImage(handle.gameObject, LoadSprite(Kenney + "/UIPack/slider_handle.png"), Color.white, false);
        handleImage.raycastTarget = true;

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = slider.colors;
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        slider.colors = colors;

        SavePrefab(root.gameObject, SliderPath);
    }

    private static void BuildTooltipView()
    {
        RectTransform root = CreateRect("TooltipView", null);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(500, 160);
        Image background = AddImage(root.gameObject, LoadSprite(Generated + "/ui_round_24.png"), TooltipColor, true);
        background.pixelsPerUnitMultiplier = 1.5f;
        root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

        TMP_Text text = CreateText(root, "Text", "Tooltip", 36, Color.white, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.lineSpacing = 8f;

        RectTransform arrow = CreateRect("Arrow", root);
        arrow.sizeDelta = new Vector2(48, 24);
        AddImage(arrow.gameObject, LoadSprite(Generated + "/tooltip_arrow.png"), TooltipColor, false);

        TooltipView view = root.gameObject.AddComponent<TooltipView>();
        SetRef(view, "_text", text);
        SetRef(view, "_arrow", arrow);
        SavePrefab(root.gameObject, TooltipViewPath);
    }

    private static readonly Color TooltipColor = Hex("2B3A33");
    #endregion

    #region Coin counter and cue card
    private static void BuildCoinCounter()
    {
        RectTransform root = CreateRect("CoinCounter", null);
        root.sizeDelta = new Vector2(280, 100);
        Image background = AddImage(root.gameObject, LoadSprite(Generated + "/ui_round_24.png"), new Color(0f, 0f, 0f, 0.5f), true);
        background.pixelsPerUnitMultiplier = 1f;

        RectTransform icon = CreateRect("Coin", root);
        Anchor(icon, new Vector2(0f, 0.5f), new Vector2(55f, 0f), new Vector2(76f, 76f));
        AddImage(icon.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);

        TMP_Text amount = CreateText(root, "Amount", "10", 56, Gold, TextAlignmentOptions.MidlineLeft);
        amount.fontSharedMaterial = OutlineFor(amount);
        Anchor(amount.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(170f, 80f));

        var view = root.gameObject.AddComponent<CoinCounterView>();
        SetRef(view, "_amount", amount);
        SetRef(view, "_icon", icon);
        SavePrefab(root.gameObject, CoinCounterPath);
    }

    public static CoinCounterView InstantiateCoinCounter(Transform parent, Vector2 anchor, Vector2 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CoinCounterPath), parent);
        var rect = (RectTransform)instance.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        return instance.GetComponent<CoinCounterView>();
    }

    private static readonly (string label, string tooltip, string icon)[] Ratings =
    {
        ("AIM", "AIM: a longer aim line past the hit ball.", "/icon_stat_aim.png"),
        ("CONTROL", "CONTROL: wider green and yellow zones.", "/icon_stat_control.png"),
        ("TIMING", "TIMING: a slower release bar.", "/icon_stat_timing.png"),
        ("POWER", "POWER: harder breaks and power shots.", "/icon_bolt.png")
    };
    public const float CueCardHeight = 470f;

    // v3 V11: OVR badge, name, cue, "Recommended for", AIM / CONTROL / TIMING bars; BUY / SELECT on the right.
    private static void BuildCueCard()
    {
        const float width = 960f, height = CueCardHeight;
        RectTransform root = CreateRect("CueCard", null);
        root.sizeDelta = new Vector2(width, height);
        root.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        Image frame = AddImage(root.gameObject, LoadSprite(Generated + "/ui_round_24.png"), new Color(1f, 1f, 1f, 0.12f), true);

        RectTransform inner = CreateRect("Inner", root);
        Stretch(inner, new Vector4(6, 6, 6, 6));
        AddImage(inner.gameObject, LoadSprite(Generated + "/ui_round_24.png"), Hex("1C2A22"), true);

        RectTransform content = CreateRect("Content", root);
        Stretch(content, Vector4.zero);
        var contentGroup = content.gameObject.AddComponent<CanvasGroup>();

        // Big OVR badge.
        RectTransform ovr = CreateRect("Overall", content);
        Anchor(ovr, new Vector2(0f, 1f), new Vector2(90f, -92f), new Vector2(128f, 128f));
        AddImage(ovr.gameObject, LoadSprite(Generated + "/ui_circle.png"), Hex("2E4A3A"), false);
        RectTransform ovrRing = CreateRect("Ring", ovr);
        Stretch(ovrRing, Vector4.zero);
        AddImage(ovrRing.gameObject, LoadSprite(Generated + "/ui_ring.png"), Gold, false);
        TMP_Text overall = CreateText(ovr, "Value", "44", 44, Color.white, TextAlignmentOptions.Center);
        overall.fontSharedMaterial = OutlineFor(overall);
        Anchor(overall.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 17f), new Vector2(100f, 50f));
        TMP_Text ovrLabel = CreateText(ovr, "Label", "OVR", 28, Gold, TextAlignmentOptions.Center);
        Anchor(ovrLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -33f), new Vector2(100f, 32f));

        TMP_Text name = CreateText(content, "Name", "House Cue", 48, TextLight, TextAlignmentOptions.MidlineLeft);
        name.fontSharedMaterial = OutlineFor(name);
        TopLeft(name.rectTransform, new Vector2(180f, -48f), new Vector2(460f, 60f));

        RectTransform cue = CreateRect("CueImage", content);
        TopLeft(cue, new Vector2(176f, -104f), new Vector2(480f, 34f));
        Image cueImage = AddImage(cue.gameObject, LoadSprite(GeneratedArtBuilder.CueSpritePath(DataAssetBuilder.CueSpecs[0].Id)), Color.white, false);
        cueImage.preserveAspect = false;

        RectTransform recommended = CreateRect("Recommended", content);
        TopLeft(recommended, new Vector2(180f, -160f), new Vector2(500f, 44f));
        AddImage(recommended.gameObject, LoadSprite(Generated + "/ui_round_24.png"), new Color(0.97f, 0.77f, 0.27f, 0.2f), true).pixelsPerUnitMultiplier = 3f;
        TMP_Text recommendedText = CreateText(recommended, "Text", "Platinum opponents use this cue", 28, Gold, TextAlignmentOptions.Center);
        recommendedText.enableAutoSizing = true;
        recommendedText.fontSizeMin = 28f;
        recommendedText.fontSizeMax = 28f;
        Stretch(recommendedText.rectTransform, new Vector4(12f, 0f, 12f, 0f));

        var fills = new Image[Ratings.Length];
        var values = new TMP_Text[Ratings.Length];
        var diffs = new TMP_Text[Ratings.Length];
        for (int i = 0; i < Ratings.Length; i++)
            (fills[i], values[i], diffs[i]) = RatingRow(content, Ratings[i].label, -220f - i * 56f, Ratings[i].tooltip, Ratings[i].icon);

        // Action area on the right: TRY, then BUY / SELECT, what is missing, the OVR difference.
        GameObject tryButton = CreateButtonInstance(content, "TRY", null, new Vector2(240f, 92f), 38, ButtonColor.Yellow);
        Anchor((RectTransform)tryButton.transform, new Vector2(1f, 1f), new Vector2(-150f, -92f), new Vector2(240f, 92f));
        GameObject action = CreateButtonInstance(content, "SELECT", Generated + "/icon_coin.png", new Vector2(240f, 92f), 38, ButtonColor.Blue);
        Anchor((RectTransform)action.transform, new Vector2(1f, 1f), new Vector2(-150f, -200f), new Vector2(240f, 92f));
        Transform actionContent = action.transform.Find("Content");
        GameObject actionCoin = actionContent.Find("Icon").gameObject;
        TMP_Text actionLabel = actionContent.Find("Label").GetComponent<TMP_Text>();
        actionLabel.enableAutoSizing = true;
        actionLabel.fontSizeMin = 28f;
        actionLabel.fontSizeMax = 38f;
        actionCoin.GetComponent<Image>().color = Color.white;

        TMP_Text note = CreateText(content, "Note", "12 more coins", 28, Hex("FF8A7E"), TextAlignmentOptions.Center);
        Anchor(note.rectTransform, new Vector2(1f, 1f), new Vector2(-150f, -272f), new Vector2(250f, 40f));

        TMP_Text vsSelected = CreateText(content, "VsSelected", "+20 OVR vs your cue", 28, Hex("7FE39A"), TextAlignmentOptions.Center);
        vsSelected.fontSharedMaterial = OutlineFor(vsSelected);
        vsSelected.enableAutoSizing = true;
        vsSelected.fontSizeMin = 28f;
        vsSelected.fontSizeMax = 28f;
        Anchor(vsSelected.rectTransform, new Vector2(1f, 1f), new Vector2(-150f, -330f), new Vector2(280f, 40f));

        RectTransform badge = CreateRect("SelectedBadge", content);
        Anchor(badge, new Vector2(1f, 1f), new Vector2(-150f, -200f), new Vector2(240f, 76f));
        AddImage(badge.gameObject, LoadSprite(Generated + "/ui_round_24.png"), Hex("2E9E4F"), true);
        TMP_Text badgeText = CreateText(badge, "Text", "SELECTED", 38, Color.white, TextAlignmentOptions.Center);
        badgeText.fontSharedMaterial = OutlineFor(badgeText);
        Stretch(badgeText.rectTransform, Vector4.zero);

        var view = root.gameObject.AddComponent<CueCardView>();
        SetRef(view, "_frame", frame);
        SetRef(view, "_cueImage", cueImage);
        SetRef(view, "_name", name);
        SetRef(view, "_selectedBadge", badge.gameObject);
        SetRef(view, "_overall", overall);
        SetRef(view, "_tryButton", tryButton.GetComponent<UIButton>());
        SetRef(view, "_vsSelected", vsSelected);
        SetRef(view, "_recommended", recommended.gameObject);
        SetRef(view, "_recommendedText", recommendedText);
        var serialized = new SerializedObject(view);
        SerializedProperty fillArray = serialized.FindProperty("_ratingFills");
        SerializedProperty valueArray = serialized.FindProperty("_ratingValues");
        SerializedProperty diffArray = serialized.FindProperty("_ratingDiffs");
        fillArray.arraySize = valueArray.arraySize = diffArray.arraySize = Ratings.Length;
        for (int i = 0; i < Ratings.Length; i++)
        {
            fillArray.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
            valueArray.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            diffArray.GetArrayElementAtIndex(i).objectReferenceValue = diffs[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetRef(view, "_actionButton", action.GetComponent<UIButton>());
        SetRef(view, "_actionLabel", actionContent.Find("Label").GetComponent<TMP_Text>());
        SetRef(view, "_actionCoin", actionCoin);
        SetRef(view, "_note", note);
        SetRef(view, "_content", contentGroup);
        SavePrefab(root.gameObject, CueCardPath);
    }

    // Icon, label, bar, number and the difference to the selected cue; the whole row is a tooltip trigger.
    private static (Image fill, TMP_Text value, TMP_Text diff) RatingRow(Transform parent, string label, float y, string tooltip, string icon)
    {
        RectTransform row = CreateRect(label, parent);
        TopLeft(row, new Vector2(30f, y), new Vector2(620f, 48f));
        AddImage(row.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;
        var trigger = row.gameObject.AddComponent<TooltipTrigger>();
        var serialized = new SerializedObject(trigger);
        serialized.FindProperty("_text").stringValue = tooltip;
        serialized.FindProperty("_preferredSide").enumValueIndex = (int)TooltipSide.Above;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        RectTransform iconRect = CreateRect("Icon", row);
        Anchor(iconRect, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(40f, 40f));
        AddImage(iconRect.gameObject, LoadSprite(Generated + icon), Gold, false);
        TMP_Text text = CreateText(row, "Label", label, 28, TextMuted, TextAlignmentOptions.MidlineLeft);
        Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(170f, 40f));
        Image fill = Bar(row, "Bar", new Vector2(240f, 0f), new Vector2(210f, 16f), Gold);
        TMP_Text value = CreateText(row, "Value", "44", 30, Color.white, TextAlignmentOptions.MidlineLeft);
        value.fontSharedMaterial = OutlineFor(value);
        Anchor(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(490f, 0f), new Vector2(60f, 40f));
        TMP_Text diff = CreateText(row, "Diff", "+12", 28, Hex("7FE39A"), TextAlignmentOptions.MidlineLeft);
        diff.fontSharedMaterial = OutlineFor(diff);
        Anchor(diff.rectTransform, new Vector2(0f, 0.5f), new Vector2(575f, 0f), new Vector2(80f, 40f));
        return (fill, value, diff);
    }

    // Dark track with a horizontal fill; returns the fill image.
    private static Image Bar(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        RectTransform track = CreateRect(name, parent);
        track.anchorMin = track.anchorMax = new Vector2(0f, 0.5f);
        track.pivot = new Vector2(0f, 0.5f);
        track.anchoredPosition = position;
        track.sizeDelta = size;
        AddImage(track.gameObject, LoadSprite(Generated + "/ui_white.png"), new Color(0f, 0f, 0f, 0.55f), false);
        RectTransform fillRect = CreateRect("Fill", track);
        Stretch(fillRect, Vector4.zero);
        Image fill = AddImage(fillRect.gameObject, LoadSprite(Generated + "/ui_white.png"), color, false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0.5f;
        return fill;
    }

    private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
    #endregion

    #region Popups
    // v3 V13: sound, music, haptics, HOW TO PLAY (menu only), RULES, the version.
    private static void BuildSettingsPopup()
    {
        RectTransform panel = CreatePopupShell("SettingsPopup", new Vector2(900, 1360), "SETTINGS", out GameObject root);

        CreateIconLabel(panel, Icons + "/icon_audioOn.png", "SOUND EFFECTS", new Vector2(0, -230));
        Slider sfx = InstantiateSlider(panel, new Vector2(0, -320));
        CreateIconLabel(panel, Icons + "/icon_musicOn.png", "MUSIC", new Vector2(0, -450));
        Slider music = InstantiateSlider(panel, new Vector2(0, -540));

        GameObject haptics = CreateButtonInstance(panel, "HAPTICS: ON", null, new Vector2(600, 120), 44, ButtonColor.Grey);
        Anchor((RectTransform)haptics.transform, new Vector2(0.5f, 1f), new Vector2(0, -680), new Vector2(600, 120));
        GameObject howTo = CreateButtonInstance(panel, "HOW TO PLAY", Icons + "/icon_question.png", new Vector2(600, 120), 44, ButtonColor.Blue);
        Anchor((RectTransform)howTo.transform, new Vector2(0.5f, 1f), new Vector2(0, -820), new Vector2(600, 120));
        GameObject rules = CreateButtonInstance(panel, "RULES", Icons + "/icon_information.png", new Vector2(600, 120), 44, ButtonColor.Blue);
        Anchor((RectTransform)rules.transform, new Vector2(0.5f, 1f), new Vector2(0, -960), new Vector2(600, 120));

        TMP_Text version = CreateText(panel, "Version", "v0.0.0", 28, TextMuted, TextAlignmentOptions.Center);
        Anchor(version.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 225), new Vector2(400, 40));

        UIButton close = InstantiateButton(ButtonVariant.Close, panel, Vector2.zero);
        Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(420, 140));

        var popup = root.AddComponent<SettingsPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: true);
        SetRef(popup, "_sfxSlider", sfx);
        SetRef(popup, "_musicSlider", music);
        SetRef(popup, "_hapticsButton", haptics.GetComponent<UIButton>());
        SetRef(popup, "_hapticsLabel", haptics.transform.Find("Content/Label").GetComponent<TMP_Text>());
        SetRef(popup, "_howToPlayButton", howTo.GetComponent<UIButton>());
        SetRef(popup, "_rulesButton", rules.GetComponent<UIButton>());
        SetRef(popup, "_version", version);
        SetRef(popup, "_closeButton", close);
        SavePrefab(root, SettingsPopupPath);
    }

    private static void BuildConfirmPopup()
    {
        RectTransform panel = CreatePopupShell("ConfirmPopup", new Vector2(860, 760), "CONFIRM", out GameObject root);
        TMP_Text title = panel.Find("Title").GetComponent<TMP_Text>();

        TMP_Text body = CreateTopText(panel, "Body", "Are you sure?", 44, TextLight, -250);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.rectTransform.sizeDelta = new Vector2(760, 160);

        RectTransform priceRow = CreateRect("Price", panel);
        Anchor(priceRow, new Vector2(0.5f, 1f), new Vector2(0, -410), new Vector2(400, 100));
        RectTransform coin = CreateRect("Coin", priceRow);
        Anchor(coin, new Vector2(0.5f, 0.5f), new Vector2(-70, 0), new Vector2(84, 84));
        AddImage(coin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text price = CreateText(priceRow, "Amount", "70", 68, Gold, TextAlignmentOptions.MidlineLeft);
        price.fontSharedMaterial = OutlineFor(price);
        Anchor(price.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60, 0), new Vector2(180, 90));

        UIButton confirm = InstantiateButton(ButtonVariant.Confirm, panel, Vector2.zero);
        Anchor((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(-190, 120), new Vector2(340, 140));
        UIButton cancel = InstantiateButton(ButtonVariant.Cancel, panel, Vector2.zero);
        Anchor((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(190, 120), new Vector2(340, 140));

        var popup = root.AddComponent<ConfirmPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: true);
        SetRef(popup, "_title", title);
        SetRef(popup, "_body", body);
        SetRef(popup, "_priceRow", priceRow.gameObject);
        SetRef(popup, "_price", price);
        SetRef(popup, "_confirmButton", confirm);
        SetRef(popup, "_confirmLabel", confirm.transform.Find("Content/Label").GetComponent<TMP_Text>());
        SetRef(popup, "_confirmSprite", ButtonSprite(ButtonColor.Green, false));
        SetRef(popup, "_confirmPressedSprite", ButtonSprite(ButtonColor.Green, true));
        SetRef(popup, "_dangerSprite", ButtonSprite(ButtonColor.Red, false));
        SetRef(popup, "_dangerPressedSprite", ButtonSprite(ButtonColor.Red, true));
        SetRef(popup, "_cancelButton", cancel);
        SavePrefab(root, ConfirmPopupPath);
    }

    private static void BuildCuesPopup()
    {
        RectTransform panel = CreatePopupShell("CuesPopup", new Vector2(1020, 1720), "CUES", out GameObject root);
        CoinCounterView coins = InstantiateCoinCounter(panel, new Vector2(1f, 1f), new Vector2(-170, -100));

        RectTransform viewport = CreateRect("Viewport", panel);
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.offsetMin = new Vector2(20, 280);
        viewport.offsetMax = new Vector2(-20, -200);
        AddImage(viewport.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 22f;
        layout.padding = new RectOffset(0, 0, 10, 10);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CueCardPath);
        var cards = new CueCardView[DataAssetBuilder.CueSpecs.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            var card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, content);
            card.name = "CueCard_" + DataAssetBuilder.CueSpecs[i].Id;
            cards[i] = card.GetComponent<CueCardView>();
        }

        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        TMP_Text footer = CreateText(panel, "Footer", "Better cues: longer aim line, wider green, slower bar.", 26, TextMuted, TextAlignmentOptions.Center);
        footer.enableAutoSizing = true;
        footer.fontSizeMin = 18f;
        footer.fontSizeMax = 26f;
        Anchor(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 235), new Vector2(940, 44));

        UIButton close = InstantiateButton(ButtonVariant.Close, panel, Vector2.zero);
        Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 115), new Vector2(420, 140));

        var popup = root.AddComponent<CuesPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: false);
        var serialized = new SerializedObject(popup);
        SerializedProperty array = serialized.FindProperty("_cards");
        array.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetRef(popup, "_scroll", scroll);
        SetRef(popup, "_coins", coins);
        SetRef(popup, "_closeButton", close);
        SavePrefab(root, CuesPopupPath);
    }

    // v4 V9: after TRY. Two columns, the selected cue and the tried one, the four stats side by side, how the releases
    // went, then BUY (or what is missing), PLAY and BACK TO CUES.
    private static void BuildCueTryResultPopup()
    {
        RectTransform panel = CreatePopupShell("CueTryResultPopup", new Vector2(1040, 1480), "TRY RESULT", out GameObject root);

        (TMP_Text name, Image cue, TMP_Text overall) Column(string caption, float x)
        {
            TMP_Text captionText = CreateText(panel, caption + "Caption", caption, 28, TextMuted, TextAlignmentOptions.Center);
            Anchor(captionText.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -200f), new Vector2(420f, 40f));
            TMP_Text nameText = CreateText(panel, caption + "Name", "House Cue", 40, TextLight, TextAlignmentOptions.Center);
            nameText.fontSharedMaterial = OutlineFor(nameText);
            Anchor(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -252f), new Vector2(440f, 52f));
            RectTransform cueRect = CreateRect(caption + "Cue", panel);
            Anchor(cueRect, new Vector2(0.5f, 1f), new Vector2(x, -312f), new Vector2(440f, 28f));
            Image cueImage = AddImage(cueRect.gameObject, LoadSprite(GeneratedArtBuilder.CueSpritePath(DataAssetBuilder.CueSpecs[0].Id)), Color.white, false);
            cueImage.preserveAspect = false;
            TMP_Text overallText = CreateText(panel, caption + "Overall", "OVR 44", 44, Gold, TextAlignmentOptions.Center);
            overallText.fontSharedMaterial = OutlineFor(overallText);
            Anchor(overallText.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -372f), new Vector2(300f, 56f));
            return (nameText, cueImage, overallText);
        }
        var selected = Column("SELECTED", -250f);
        var tried = Column("TRIED", 250f);

        var triedFills = new Image[Ratings.Length];
        var selectedFills = new Image[Ratings.Length];
        var triedValues = new TMP_Text[Ratings.Length];
        var selectedValues = new TMP_Text[Ratings.Length];
        for (int i = 0; i < Ratings.Length; i++)
        {
            float y = -460f - i * 84f;
            TMP_Text label = CreateText(panel, "StatLabel" + i, Ratings[i].label, 28, TextMuted, TextAlignmentOptions.Center);
            Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(190f, 40f));
            // Left: the selected cue, growing from the middle outwards; right: the tried one.
            (selectedFills[i], selectedValues[i]) = ResultBar(panel, new Vector2(-380f, y), TextAlignmentOptions.Center, -135f, true);
            (triedFills[i], triedValues[i]) = ResultBar(panel, new Vector2(190f, y), TextAlignmentOptions.Center, 135f, false);
        }

        TMP_Text releases = CreateTopText(panel, "Releases", "PERFECT 2 · GOOD 1 · 3 shots", 36, TextLight, -840);
        releases.fontSharedMaterial = OutlineFor(releases);

        GameObject buy = CreateButtonInstance(panel, "BUY · 400", Generated + "/icon_coin.png", new Vector2(560, 110), 44, ButtonColor.Green);
        Anchor((RectTransform)buy.transform, new Vector2(0.5f, 1f), new Vector2(0, -960), new Vector2(560, 110));
        Transform buyContent = buy.transform.Find("Content");
        buyContent.Find("Icon").GetComponent<Image>().color = Color.white;
        TMP_Text note = CreateTopText(panel, "Note", "120 more coins to buy it", 32, Hex("FF8A7E"), -945);
        GameObject play = CreateButtonInstance(panel, "PLAY", Icons + "/icon_right.png", new Vector2(560, 130), 56, ButtonColor.Green);
        Anchor((RectTransform)play.transform, new Vector2(0.5f, 1f), new Vector2(0, -1120), new Vector2(560, 130));
        GameObject cues = CreateButtonInstance(panel, "BACK TO CUES", Icons + "/icon_left.png", new Vector2(560, 120), 44, ButtonColor.Blue);
        Anchor((RectTransform)cues.transform, new Vector2(0.5f, 1f), new Vector2(0, -1290), new Vector2(560, 120));

        var popup = root.AddComponent<CueTryResultPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: false);
        SetRef(popup, "_triedName", tried.name);
        SetRef(popup, "_selectedName", selected.name);
        SetRef(popup, "_triedCue", tried.cue);
        SetRef(popup, "_selectedCue", selected.cue);
        SetRef(popup, "_triedOverall", tried.overall);
        SetRef(popup, "_selectedOverall", selected.overall);
        SetArray(popup, "_triedFills", triedFills);
        SetArray(popup, "_selectedFills", selectedFills);
        SetArray(popup, "_triedValues", triedValues);
        SetArray(popup, "_selectedValues", selectedValues);
        SetRef(popup, "_releases", releases);
        SetRef(popup, "_buyButton", buy.GetComponent<UIButton>());
        SetRef(popup, "_buyLabel", buyContent.Find("Label").GetComponent<TMP_Text>());
        SetRef(popup, "_buyCoin", buyContent.Find("Icon").gameObject);
        SetRef(popup, "_note", note);
        SetRef(popup, "_playButton", play.GetComponent<UIButton>());
        SetRef(popup, "_cuesButton", cues.GetComponent<UIButton>());
        SavePrefab(root, CueTryResultPopupPath);
    }

    // A bar (left end at `position`, 190 wide) with its number at `valueX`; `fromRight`: it fills from the right.
    private static (Image fill, TMP_Text value) ResultBar(Transform parent, Vector2 position, TextAlignmentOptions align, float valueX, bool fromRight)
    {
        RectTransform track = CreateRect("Bar", parent);
        track.anchorMin = track.anchorMax = new Vector2(0.5f, 1f);
        track.pivot = new Vector2(0f, 0.5f);
        track.anchoredPosition = position;
        track.sizeDelta = new Vector2(190f, 18f);
        AddImage(track.gameObject, LoadSprite(Generated + "/ui_white.png"), new Color(0f, 0f, 0f, 0.55f), false);
        RectTransform fillRect = CreateRect("Fill", track);
        Stretch(fillRect, Vector4.zero);
        Image fill = AddImage(fillRect.gameObject, LoadSprite(Generated + "/ui_white.png"), Gold, false);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = fromRight ? 1 : 0;
        TMP_Text value = CreateText(parent, "Value", "44", 32, Color.white, align);
        value.fontSharedMaterial = OutlineFor(value);
        Anchor(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(valueX, position.y), new Vector2(70f, 40f));
        return (fill, value);
    }

    private static void BuildPausePopup()
    {
        RectTransform panel = CreatePopupShell("PausePopup", new Vector2(860, 1080), "PAUSED", out GameObject root);
        UIButton resume = InstantiateButton(ButtonVariant.Resume, panel, Vector2.zero);
        Anchor((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(600, 150));
        UIButton settings = InstantiateButton(ButtonVariant.Settings, panel, Vector2.zero);
        Anchor((RectTransform)settings.transform, new Vector2(0.5f, 1f), new Vector2(0, -470), new Vector2(600, 150));
        GameObject rules = CreateButtonInstance(panel, "RULES", Icons + "/icon_question.png", new Vector2(600, 150), 56, ButtonColor.Blue);
        Anchor((RectTransform)rules.transform, new Vector2(0.5f, 1f), new Vector2(0, -650), new Vector2(600, 150));
        UIButton forfeit = InstantiateButton(ButtonVariant.Forfeit, panel, Vector2.zero);
        Anchor((RectTransform)forfeit.transform, new Vector2(0.5f, 1f), new Vector2(0, -870), new Vector2(600, 150));

        var popup = root.AddComponent<PausePopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: false);
        SetRef(popup, "_resumeButton", resume);
        SetRef(popup, "_settingsButton", settings);
        SetRef(popup, "_rulesButton", rules.GetComponent<UIButton>());
        SetRef(popup, "_forfeitButton", forfeit);
        SetRef(popup, "_forfeitLabel", forfeit.transform.Find("Content/Label").GetComponent<TMP_Text>());
        SavePrefab(root, PausePopupPath);
    }

    // House Rules card (gameplay redesign G5): six rules, each with an icon; one row can be highlighted.
    private static void BuildRulesPopup()
    {
        (string icon, string text)[] rows =
        {
            (GeneratedArtBuilder.BallSpritePath(1), "Pocket all of your group — solids (1–7) or stripes (9–15) — then the 8-ball."),
            (GeneratedArtBuilder.BallSpritePath(9), "Your group is the first ball you pocket after the break."),
            (GeneratedArtBuilder.BallSpritePath(0), "Hit one of your own balls first."),
            (Icons + "/icon_right.png", "After contact, a ball must reach a rail or drop in."),
            (Generated + "/icon_hand.png", "Foul: your opponent gets <b>ball in hand</b> and places the cue ball anywhere."),
            (GeneratedArtBuilder.BallSpritePath(8), "The 8-ball goes in last, where your division's rule says. Early, or with a foul, and you lose.")
        };
        const float rowHeight = 170f;
        RectTransform panel = CreatePopupShell("RulesPopup", new Vector2(960, 1700), "HOUSE RULES", out GameObject root);

        // v4 V4: the division's 8-ball rule, on top.
        Image division = Panel(panel, "DivisionRule", new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(880, 190), Hex("2A3A2E"));
        RectTransform divisionIcon = CreateRect("Icon", division.transform);
        Anchor(divisionIcon, new Vector2(0f, 0.5f), new Vector2(70, 0), new Vector2(100, 100));
        Image divisionIconImage = AddImage(divisionIcon.gameObject, LoadSprite(Generated + "/icon_pocket_called.png"), Color.white, false);
        divisionIconImage.preserveAspect = true;
        TMP_Text divisionLabel = CreateText(division.transform, "Label", "DIVISION RULE", 28, Gold, TextAlignmentOptions.TopLeft);
        Anchor(divisionLabel.rectTransform, new Vector2(0f, 1f), new Vector2(490, -36), new Vector2(700, 40));
        TMP_Text divisionText = CreateText(division.transform, "Text", "Call your pocket for the 8-ball.", 36, TextLight, TextAlignmentOptions.MidlineLeft);
        divisionText.textWrappingMode = TextWrappingModes.Normal;
        divisionText.enableAutoSizing = true;
        divisionText.fontSizeMin = 28f;
        divisionText.fontSizeMax = 36f;
        Anchor(divisionText.rectTransform, new Vector2(0f, 0.5f), new Vector2(490, -18), new Vector2(700, 110));

        var highlights = new Image[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            Image row = Panel(panel, "Rule" + (i + 1), new Vector2(0.5f, 1f), new Vector2(0, -450 - i * rowHeight), new Vector2(880, rowHeight - 14), Color.clear);
            highlights[i] = row;
            RectTransform icon = CreateRect("Icon", row.transform);
            Anchor(icon, new Vector2(0f, 0.5f), new Vector2(70, 0), new Vector2(92, 92));
            AddImage(icon.gameObject, LoadSprite(rows[i].icon), Color.white, false).preserveAspect = true;
            TMP_Text text = CreateText(row.transform, "Text", rows[i].text, 36, TextLight, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 36f;
            Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(490, 0), new Vector2(700, rowHeight - 24));
        }
        GameObject close = CreateButtonInstance(panel, "GOT IT", Icons + "/icon_checkmark.png", new Vector2(520, 140), 56, ButtonColor.Green);
        Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(520, 140));

        var popup = root.AddComponent<RulesPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: true);
        var serialized = new SerializedObject(popup);
        SerializedProperty array = serialized.FindProperty("_rowHighlights");
        array.arraySize = highlights.Length;
        for (int i = 0; i < highlights.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = highlights[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetRef(popup, "_closeButton", close.GetComponent<UIButton>());
        SetRef(popup, "_divisionRow", division.gameObject);
        SetRef(popup, "_divisionText", divisionText);
        SetRef(popup, "_divisionIcon", divisionIconImage);
        SetRef(popup, "_anyPocketIcon", LoadSprite(Generated + "/icon_pocket_any.png"));
        SetRef(popup, "_calledPocketIcon", LoadSprite(Generated + "/icon_pocket_called.png"));
        SetRef(popup, "_blockedPocketIcon", LoadSprite(Generated + "/icon_pocket_blocked.png"));
        SavePrefab(root, RulesPopupPath);
    }

    // v4 V13: title, reason, the rank badge with its stars, the coins (the match, and a stage / division reward as a
    // row of its own), finished objectives, an affordable cue, NEXT MATCH with the next opponent, MENU underneath.
    private static void BuildMatchResultPopup()
    {
        RectTransform panel = CreatePopupShell("MatchResultPopup", new Vector2(940, 1780), "", out GameObject root);
        Object.DestroyImmediate(panel.Find("Title").gameObject);

        RectTransform confetti = CreateRect("Confetti", panel);
        Anchor(confetti, new Vector2(0.5f, 1f), new Vector2(0, -150), Vector2.zero);

        TMP_Text title = CreateTopText(panel, "Title", "YOU WIN!", 110, Gold, -135);
        title.fontSharedMaterial = OutlineFor(title);
        title.enableAutoSizing = true;
        title.fontSizeMin = 56f;
        title.fontSizeMax = 110f;
        TMP_Text reason = CreateTopText(panel, "Reason", "You pocketed the 8-ball!", 40, TextLight, -250);
        reason.textWrappingMode = TextWrappingModes.Normal;
        reason.rectTransform.sizeDelta = new Vector2(820, 100);

        Image rankBack = Panel(panel, "Rank", new Vector2(0.5f, 1f), new Vector2(0, -410), new Vector2(840, 200), new Color(0f, 0f, 0f, 0.35f));
        RankBadgeView rank = BuildRankBadge(rankBack.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), 1f, false, false, false);
        TMP_Text rankChange = CreateText(rankBack.transform, "Change", "+1 ★", 40, Hex("7FE39A"), TextAlignmentOptions.Center);
        rankChange.fontSharedMaterial = OutlineFor(rankChange);
        Anchor(rankChange.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(800f, 52f));

        RectTransform coinRow = CreateRect("Coins", panel);
        Anchor(coinRow, new Vector2(0.5f, 1f), new Vector2(0, -555), new Vector2(500, 90));
        RectTransform coin = CreateRect("Coin", coinRow);
        Anchor(coin, new Vector2(0.5f, 0.5f), new Vector2(-90, 0), new Vector2(80, 80));
        AddImage(coin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text delta = CreateText(coinRow, "Delta", "+20", 76, Gold, TextAlignmentOptions.MidlineLeft);
        delta.fontSharedMaterial = OutlineFor(delta);
        Anchor(delta.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80, 0), new Vector2(240, 90));

        // A stage, division or milestone reward is a row of its own (never folded into the match's coins).
        Image rewardRow = Panel(panel, "RewardRow", new Vector2(0.5f, 1f), new Vector2(0, -640), new Vector2(760, 64), Hex("3B3114"));
        TMP_Text rewardText = CreateText(rewardRow.transform, "Text", "STAGE REWARD  +75", 34, Gold, TextAlignmentOptions.Center);
        rewardText.fontSharedMaterial = OutlineFor(rewardText);
        rewardText.enableAutoSizing = true;
        rewardText.fontSizeMin = 28f;
        rewardText.fontSizeMax = 34f;
        Stretch(rewardText.rectTransform, new Vector4(10f, 0f, 10f, 0f));
        rewardRow.gameObject.SetActive(false);

        TMP_Text balance = CreateTopText(panel, "Balance", "Balance 20", 38, TextMuted, -715);

        TMP_Text objectivesHeader = CreateTopText(panel, "ObjectivesHeader", "OBJECTIVE DONE", 30, Gold, -765);
        objectivesHeader.fontSharedMaterial = OutlineFor(objectivesHeader);
        var rows = new ObjectiveRowView[ObjectiveCatalog.Slots];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = BuildObjectiveRow(panel, new Vector2(0.5f, 1f), new Vector2(0, -860 - i * 152), new Vector2(840, 148));

        Image cueCard = Panel(panel, "Cue", new Vector2(0.5f, 1f), new Vector2(0, -1285), new Vector2(840, 80), Hex("2C3B22"));
        TMP_Text cueText = CreateText(cueCard.transform, "Text", "You can buy the Club Cue!", 34, TextLight, TextAlignmentOptions.MidlineLeft);
        cueText.textWrappingMode = TextWrappingModes.Normal;
        Anchor(cueText.rectTransform, new Vector2(0f, 0.5f), new Vector2(300, 0), new Vector2(540, 76));
        GameObject seeCues = CreateButtonInstance(cueCard.transform, "CUES", Icons + "/icon_star.png", new Vector2(230, 70), 36, ButtonColor.Yellow);
        Anchor((RectTransform)seeCues.transform, new Vector2(1f, 0.5f), new Vector2(-135, 0), new Vector2(230, 70));

        // The next match: who and where, then the big green button.
        Image nextCard = Panel(panel, "NextCard", new Vector2(0.5f, 1f), new Vector2(0, -1385), new Vector2(840, 110), new Color(0f, 0f, 0f, 0.35f));
        PortraitView nextPortrait = BuildPortrait(nextCard.transform, new Vector2(0f, 0.5f), new Vector2(66f, 0f), 88f);
        TMP_Text nextName = CreateText(nextCard.transform, "Name", "vs Dina Drift", 40, TextLight, TextAlignmentOptions.MidlineLeft);
        nextName.fontSharedMaterial = OutlineFor(nextName);
        Anchor(nextName.rectTransform, new Vector2(0f, 0.5f), new Vector2(530f, 20f), new Vector2(840f - 136f, 50f));
        nextName.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        TMP_Text nextInfo = CreateText(nextCard.transform, "Info", "Downtown Hall", 30, TextMuted, TextAlignmentOptions.MidlineLeft);
        Anchor(nextInfo.rectTransform, new Vector2(0f, 0.5f), new Vector2(530f, -26f), new Vector2(840f - 136f, 40f));
        GameObject next = CreateButtonInstance(panel, "NEXT MATCH ▶", null, new Vector2(840, 150), 64, ButtonColor.Green);
        Anchor((RectTransform)next.transform, new Vector2(0.5f, 1f), new Vector2(0, -1520), new Vector2(840, 150));
        TMP_Text nextLabel = next.transform.Find("Content/Label").GetComponent<TMP_Text>();
        nextLabel.enableAutoSizing = true;
        nextLabel.fontSizeMin = 40f;
        nextLabel.fontSizeMax = 64f;
        GameObject menu = CreateButtonInstance(panel, "MENU", Icons + "/icon_home.png", new Vector2(360, 84), 38, ButtonColor.Grey);
        Anchor((RectTransform)menu.transform, new Vector2(0.5f, 1f), new Vector2(0, -1665), new Vector2(360, 84));
        confetti.SetAsLastSibling();

        var popup = root.AddComponent<MatchResultPopup>();
        WirePopup(popup, closeOnBack: false, closeOnDimClick: false);
        SetRef(popup, "_title", title);
        SetRef(popup, "_reason", reason);
        SetRef(popup, "_rank", rank);
        SetRef(popup, "_rankChange", rankChange);
        SetRef(popup, "_delta", delta);
        SetRef(popup, "_balance", balance);
        SetRef(popup, "_coinRow", coinRow.gameObject);
        SetRef(popup, "_rewardRow", rewardRow.gameObject);
        SetRef(popup, "_rewardText", rewardText);
        SetRef(popup, "_objectivesHeader", objectivesHeader.gameObject);
        SetArray(popup, "_objectives", rows);
        SetRef(popup, "_cueCard", cueCard.gameObject);
        SetRef(popup, "_cueText", cueText);
        SetRef(popup, "_seeCuesButton", seeCues.GetComponent<UIButton>());
        SetRef(popup, "_nextButton", next.GetComponent<UIButton>());
        SetRef(popup, "_nextPortrait", nextPortrait);
        SetRef(popup, "_nextName", nextName);
        SetRef(popup, "_nextInfo", nextInfo);
        SetRef(popup, "_menuButton", menu.GetComponent<UIButton>());
        SetRef(popup, "_confettiRoot", confetti);
        SetRef(popup, "_confettiSprite", LoadSprite(Generated + "/ui_white.png"));
        SavePrefab(root, MatchResultPopupPath);
    }

    // The rank badge (shield, label, stars, next step), used on the menu, the VS intro and the results.
    public static RankBadgeView BuildRankBadge(Transform parent, Vector2 anchor, Vector2 position, float scale, bool withNext, bool followProfile, bool opensRoad)
    {
        RectTransform root = CreateRect("RankBadge", parent);
        Anchor(root, anchor, position, new Vector2(620f, withNext ? 200f : 140f));
        root.localScale = Vector3.one * scale;
        AddImage(root.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = opensRoad;

        float top = withNext ? 30f : 0f;
        RectTransform badge = CreateRect("Badge", root);
        Anchor(badge, new Vector2(0f, 0.5f), new Vector2(70f, top), new Vector2(124f, 124f));
        Image badgeImage = AddImage(badge.gameObject, LoadSprite(Generated + "/rank_badge.png"), Hex("CD7F32"), false);

        TMP_Text label = CreateText(root, "Label", "BRONZE III", 58, Color.white, TextAlignmentOptions.MidlineLeft);
        label.fontSharedMaterial = OutlineFor(label);
        label.enableAutoSizing = true;
        label.fontSizeMin = 34f;
        label.fontSizeMax = 58f;
        Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(380f, top + 30f), new Vector2(460f, 70f));

        var stars = new Image[3];
        for (int i = 0; i < stars.Length; i++)
        {
            RectTransform star = CreateRect("Star" + i, root);
            Anchor(star, new Vector2(0f, 0.5f), new Vector2(180f + i * 66f, top - 34f), new Vector2(58f, 58f));
            stars[i] = AddImage(star.gameObject, LoadSprite(Icons + "/icon_star.png"), Color.white, false);
        }
        TMP_Text championStars = CreateText(root, "ChampionStars", "★ 0", 46, Gold, TextAlignmentOptions.MidlineLeft);
        championStars.fontSharedMaterial = OutlineFor(championStars);
        Anchor(championStars.rectTransform, new Vector2(0f, 0.5f), new Vector2(260f, top - 34f), new Vector2(220f, 60f));

        TMP_Text next = null;
        if (withNext)
        {
            next = CreateText(root, "Next", "2 stars to BRONZE II", 34, TextLight, TextAlignmentOptions.Center);
            next.fontSharedMaterial = OutlineFor(next);
            Anchor(next.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(620f, 46f));
        }

        // NEW: a division the player has not looked at on DIVISIONS yet (menu badge only).
        GameObject newChip = null;
        if (opensRoad)
        {
            Image chip = Panel(root, "NewChip", new Vector2(1f, 1f), new Vector2(-50f, -22f), new Vector2(92f, 42f), Gold);
            TMP_Text chipText = CreateText(chip.transform, "Text", "NEW", 28, OutlineColor, TextAlignmentOptions.Center);
            Stretch(chipText.rectTransform, Vector4.zero);
            newChip = chip.gameObject;
            newChip.SetActive(false);
        }

        var view = root.gameObject.AddComponent<RankBadgeView>();
        if (newChip != null)
            SetRef(view, "_newChip", newChip);
        SetRef(view, "_badge", badgeImage);
        SetRef(view, "_label", label);
        SetArray(view, "_stars", stars);
        SetRef(view, "_championStars", championStars);
        if (next != null)
            SetRef(view, "_next", next);
        var serialized = new SerializedObject(view);
        serialized.FindProperty("_followProfile").boolValue = followProfile;
        serialized.FindProperty("_opensRoad").boolValue = opensRoad;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // One objective row (v4 V8, F19), two lines: the text on top (36 px, up to two lines where the row is tall
    // enough), then the bar, "3/5" and the coin reward. Done: CLAIM with the reward takes the reward's place.
    public const float ObjectiveTextSize = 36f;

    public static ObjectiveRowView BuildObjectiveRow(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image background = Panel(parent, "Objective", anchor, position, size, new Color(0f, 0f, 0f, 0.45f));
        Transform row = background.transform;
        const float bottom = 50f;   // the lower line: bar, count, reward / CLAIM

        TMP_Text text = CreateText(row, "Text", "Release on PERFECT 5 times", ObjectiveTextSize, TextLight, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        // Two lines of 36 px need ~86 px: rows are 148+ tall.
        text.rectTransform.offsetMin = new Vector2(26f, -(size.y - bottom - 4f));
        text.rectTransform.offsetMax = new Vector2(-90f, -6f);

        // A coin at the top right: this objective pays "cue coins" in a loss too (shown while the cue gap card is up).
        RectTransform tag = CreateRect("CueCoinTag", row);
        Anchor(tag, new Vector2(1f, 1f), new Vector2(-44f, -40f), new Vector2(56f, 56f));
        AddImage(tag.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        tag.gameObject.SetActive(false);

        float lineY = -size.y * 0.5f + bottom * 0.5f + 4f;
        Image fill = Bar(row, "Bar", new Vector2(26f, lineY), new Vector2(size.x - 470f, 14f), Hex("7FE39A"));
        TMP_Text progress = CreateText(row, "Progress", "3/5", 30, TextMuted, TextAlignmentOptions.MidlineLeft);
        Anchor(progress.rectTransform, new Vector2(1f, 0.5f), new Vector2(-340f, lineY), new Vector2(140f, 44f));

        RectTransform coin = CreateRect("Coin", row);
        Anchor(coin, new Vector2(1f, 0.5f), new Vector2(-170f, lineY), new Vector2(44f, 44f));
        AddImage(coin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text reward = CreateText(row, "Reward", "50", 34, Gold, TextAlignmentOptions.MidlineLeft);
        reward.fontSharedMaterial = OutlineFor(reward);
        Anchor(reward.rectTransform, new Vector2(1f, 0.5f), new Vector2(-82f, lineY), new Vector2(110f, 44f));

        // CLAIM with the coin and the reward on it, over the reward's place.
        GameObject claim = CreateButtonInstance(row, "CLAIM 50", Generated + "/icon_coin.png", new Vector2(250f, bottom), 30, ButtonColor.Green);
        Anchor((RectTransform)claim.transform, new Vector2(1f, 0.5f), new Vector2(-145f, lineY), new Vector2(250f, bottom));

        // Done: a gold frame round the whole row.
        Image frame = AddImage(CreateRect("Frame", row).gameObject, LoadSprite(Generated + "/ui_round_frame_24.png"), Gold, true);
        frame.pixelsPerUnitMultiplier = 1.5f;
        Stretch(frame.rectTransform, Vector4.zero);
        frame.gameObject.SetActive(false);

        var view = background.gameObject.AddComponent<ObjectiveRowView>();
        SetRef(view, "_frame", frame.gameObject);
        SetRef(view, "_cueCoinTag", tag.gameObject);
        SetRef(view, "_text", text);
        SetRef(view, "_fill", fill);
        SetRef(view, "_progress", progress);
        SetRef(view, "_reward", reward);
        SetRef(view, "_rewardIcon", coin.gameObject);
        SetRef(view, "_claimButton", claim.GetComponent<UIButton>());
        SetRef(view, "_claimLabel", claim.transform.Find("Content/Label").GetComponent<TMP_Text>());
        SetRef(view, "_background", background);
        return view;
    }

    // A CPU character's portrait with initials over a generated avatar.
    public static PortraitView BuildPortrait(Transform parent, Vector2 anchor, Vector2 position, float size)
    {
        RectTransform root = CreateRect("Portrait", parent);
        Anchor(root, anchor, position, new Vector2(size, size));
        Image image = AddImage(root.gameObject, LoadSprite(GeneratedArtBuilder.PortraitPath("ray")), Color.white, false);
        TMP_Text initials = CreateText(root, "Initials", "RR", size * 0.3f, Color.white, TextAlignmentOptions.Center);
        initials.fontSharedMaterial = OutlineFor(initials);
        Anchor(initials.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, size * 0.24f), new Vector2(size, size * 0.36f));
        var view = root.gameObject.AddComponent<PortraitView>();
        SetRef(view, "_image", image);
        SetRef(view, "_initials", initials);
        return view;
    }

    public static void SetArray(Object target, string field, Object[] values)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty array = serialized.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static RectTransform CreatePopupShell(string name, Vector2 panelSize, string title, out GameObject root)
    {
        RectTransform rootRect = CreateRect(name, null);
        Stretch(rootRect, Vector4.zero);
        rootRect.gameObject.AddComponent<CanvasGroup>();

        RectTransform dim = CreateRect("Dim", rootRect);
        Stretch(dim, Vector4.zero);
        Image dimImage = AddImage(dim.gameObject, null, new Color(0f, 0f, 0f, 0.65f), false);
        dimImage.raycastTarget = true;
        dim.gameObject.AddComponent<CanvasGroup>();

        RectTransform panel = CreateRect("Panel", rootRect);
        panel.sizeDelta = panelSize;
        Image border = AddImage(panel.gameObject, LoadSprite(Generated + "/ui_round_24.png"), Hex("3E5C48"), true);
        border.raycastTarget = true;
        panel.gameObject.AddComponent<CanvasGroup>();
        RectTransform face = CreateRect("Face", panel);
        Stretch(face, new Vector4(6, 6, 6, 6));
        AddImage(face.gameObject, LoadSprite(Generated + "/ui_round_24.png"), PanelColor, true);

        TMP_Text titleText = CreateTopText(panel, "Title", title, 76, Gold, -100);
        titleText.fontSharedMaterial = OutlineFor(titleText);

        root = rootRect.gameObject;
        return panel;
    }

    public static void WirePopup(PopupBase popup, bool closeOnBack, bool closeOnDimClick)
    {
        var serialized = new SerializedObject(popup);
        serialized.FindProperty("_dim").objectReferenceValue = popup.transform.Find("Dim").GetComponent<CanvasGroup>();
        serialized.FindProperty("_panel").objectReferenceValue = popup.transform.Find("Panel");
        serialized.FindProperty("_closeOnBack").boolValue = closeOnBack;
        serialized.FindProperty("_closeOnDimClick").boolValue = closeOnDimClick;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Slider InstantiateSlider(Transform parent, Vector2 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SliderPath), parent);
        var rect = (RectTransform)instance.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        return instance.GetComponent<Slider>();
    }

    private static void CreateIconLabel(RectTransform parent, string icon, string label, Vector2 position)
    {
        RectTransform row = CreateRect(label + "Row", parent);
        Anchor(row, new Vector2(0.5f, 1f), position, new Vector2(680, 80));
        RectTransform iconRect = CreateRect("Icon", row);
        Anchor(iconRect, new Vector2(0f, 0.5f), new Vector2(35, 0), new Vector2(64, 64));
        AddImage(iconRect.gameObject, LoadSprite(icon), TextMuted, false);
        TMP_Text text = CreateText(row, "Label", label, 46, TextLight, TextAlignmentOptions.MidlineLeft);
        Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(390, 0), new Vector2(600, 70));
    }

    public static TMP_Text CreateTopText(RectTransform parent, string name, string text, float size, Color color, float y)
    {
        TMP_Text tmp = CreateText(parent, name, text, size, color, TextAlignmentOptions.Center);
        Anchor(tmp.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(820, size * 1.3f));
        return tmp;
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
        tmp.font = FontFor(text);
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

    // v4 V8 (F19): no UI text below 28 ref px (small labels under a control 26, objective text 36), autosize included.
    public const float MinTextSize = 28f;
    public const float MinSmallTextSize = 26f;
    public static readonly HashSet<string> SmallLabels = new() { "Caption", "Count", "Thinking", "Group" };

    public static float MinSizeFor(TMP_Text text)
    {
        if (text.name == "Text" && text.GetComponentInParent<ObjectiveRowView>(true) != null)
            return ObjectiveTextSize;
        return SmallLabels.Contains(text.name) ? MinSmallTextSize : MinTextSize;
    }

    public static void ApplyMinTextSizes(GameObject root)
    {
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            float min = MinSizeFor(text);
            text.fontSize = Mathf.Max(text.fontSize, min);
            if (text.enableAutoSizing)
            {
                text.fontSizeMin = Mathf.Max(text.fontSizeMin, min);
                text.fontSizeMax = Mathf.Max(text.fontSizeMax, text.fontSizeMin);
            }
        }
    }

    public static void ApplyMinTextSizes(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            ApplyMinTextSizes(root);
    }

    public static void SavePrefab(GameObject root, string path)
    {
        ApplyMinTextSizes(root);
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
