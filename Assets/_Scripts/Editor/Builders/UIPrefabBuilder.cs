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
    public const string SliderPath = UIFolder + "/UISlider.prefab";
    public const string TooltipViewPath = UIFolder + "/TooltipView.prefab";
    public const string CoinCounterPath = UIFolder + "/CoinCounter.prefab";
    public const string CueCardPath = UIFolder + "/CueCard.prefab";
    public const string SettingsPopupPath = PopupFolder + "/SettingsPopup.prefab";
    public const string CuesPopupPath = PopupFolder + "/CuesPopup.prefab";
    public const string ConfirmPopupPath = PopupFolder + "/ConfirmPopup.prefab";
    public const string PausePopupPath = PopupFolder + "/PausePopup.prefab";
    public const string RulesPopupPath = PopupFolder + "/RulesPopup.prefab";
    public const string MatchResultPopupPath = PopupFolder + "/MatchResultPopup.prefab";
    public const string RankedRoadPopupPath = PopupFolder + "/RankedRoadPopup.prefab";

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
        BuildRulesPopup();
        BuildMatchResultPopup();
        BuildRankedRoadPopup();

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
        ButtonColor.Green => new Color(0.8f, 0.8f, 0.8f, 1f),
        ButtonColor.Red => new Color(0.82f, 0.82f, 0.82f, 1f),
        ButtonColor.Blue => new Color(0.85f, 0.85f, 0.85f, 1f),
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
        amount.fontSharedMaterial = OutlineMaterial;
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

    private static readonly (string label, string tooltip)[] Ratings =
    {
        ("AIM", "AIM: a longer aim line past the hit ball."),
        ("CONTROL", "CONTROL: wider green and yellow zones."),
        ("TIMING", "TIMING: a slower release bar.")
    };

    // v3 V11: OVR badge, name, cue, "Recommended for", AIM / CONTROL / TIMING bars; BUY / SELECT on the right.
    private static void BuildCueCard()
    {
        const float width = 960f, height = 360f;
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
        TMP_Text overall = CreateText(ovr, "Value", "44", 48, Color.white, TextAlignmentOptions.Center);
        overall.fontSharedMaterial = OutlineMaterial;
        Anchor(overall.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(100f, 56f));
        TMP_Text ovrLabel = CreateText(ovr, "Label", "OVR", 20, Gold, TextAlignmentOptions.Center);
        Anchor(ovrLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(100f, 26f));

        TMP_Text name = CreateText(content, "Name", "House Cue", 48, TextLight, TextAlignmentOptions.MidlineLeft);
        name.fontSharedMaterial = OutlineMaterial;
        TopLeft(name.rectTransform, new Vector2(180f, -48f), new Vector2(460f, 60f));

        RectTransform cue = CreateRect("CueImage", content);
        TopLeft(cue, new Vector2(176f, -104f), new Vector2(460f, 34f));
        Image cueImage = AddImage(cue.gameObject, LoadSprite(GeneratedArtBuilder.CueSpritePath(DataAssetBuilder.CueSpecs[0].Id)), Color.white, false);
        cueImage.preserveAspect = false;

        RectTransform recommended = CreateRect("Recommended", content);
        TopLeft(recommended, new Vector2(180f, -152f), new Vector2(400f, 42f));
        AddImage(recommended.gameObject, LoadSprite(Generated + "/ui_round_24.png"), new Color(0.97f, 0.77f, 0.27f, 0.2f), true).pixelsPerUnitMultiplier = 3f;
        TMP_Text recommendedText = CreateText(recommended, "Text", "Recommended for Gold", 24, Gold, TextAlignmentOptions.Center);
        recommendedText.enableAutoSizing = true;
        recommendedText.fontSizeMin = 16f;
        recommendedText.fontSizeMax = 22f;
        Stretch(recommendedText.rectTransform, new Vector4(12f, 0f, 12f, 0f));

        var fills = new Image[Ratings.Length];
        var values = new TMP_Text[Ratings.Length];
        for (int i = 0; i < Ratings.Length; i++)
            (fills[i], values[i]) = RatingRow(content, Ratings[i].label, -212f - i * 46f, Ratings[i].tooltip);

        // Action area on the right.
        GameObject action = CreateButtonInstance(content, "SELECT", Generated + "/icon_coin.png", new Vector2(290f, 112f), 40, ButtonColor.Blue);
        Anchor((RectTransform)action.transform, new Vector2(1f, 1f), new Vector2(-175f, -110f), new Vector2(290f, 112f));
        Transform actionContent = action.transform.Find("Content");
        GameObject actionCoin = actionContent.Find("Icon").gameObject;
        TMP_Text actionLabel = actionContent.Find("Label").GetComponent<TMP_Text>();
        actionLabel.enableAutoSizing = true;
        actionLabel.fontSizeMin = 24f;
        actionLabel.fontSizeMax = 40f;
        actionCoin.GetComponent<Image>().color = Color.white;

        TMP_Text note = CreateText(content, "Note", "Need 12 more coins", 28, Hex("FF8A7E"), TextAlignmentOptions.Center);
        Anchor(note.rectTransform, new Vector2(1f, 1f), new Vector2(-175f, -192f), new Vector2(320f, 40f));

        TMP_Text vsSelected = CreateText(content, "VsSelected", "+20 vs your cue", 28, Hex("7FE39A"), TextAlignmentOptions.Center);
        vsSelected.fontSharedMaterial = OutlineMaterial;
        Anchor(vsSelected.rectTransform, new Vector2(1f, 1f), new Vector2(-175f, -250f), new Vector2(320f, 40f));

        RectTransform badge = CreateRect("SelectedBadge", content);
        Anchor(badge, new Vector2(1f, 1f), new Vector2(-175f, -110f), new Vector2(270f, 76f));
        AddImage(badge.gameObject, LoadSprite(Generated + "/ui_round_24.png"), Hex("2E9E4F"), true);
        TMP_Text badgeText = CreateText(badge, "Text", "SELECTED", 38, Color.white, TextAlignmentOptions.Center);
        badgeText.fontSharedMaterial = OutlineMaterial;
        Stretch(badgeText.rectTransform, Vector4.zero);

        var view = root.gameObject.AddComponent<CueCardView>();
        SetRef(view, "_frame", frame);
        SetRef(view, "_cueImage", cueImage);
        SetRef(view, "_name", name);
        SetRef(view, "_selectedBadge", badge.gameObject);
        SetRef(view, "_overall", overall);
        SetRef(view, "_vsSelected", vsSelected);
        SetRef(view, "_recommended", recommended.gameObject);
        SetRef(view, "_recommendedText", recommendedText);
        var serialized = new SerializedObject(view);
        SerializedProperty fillArray = serialized.FindProperty("_ratingFills");
        SerializedProperty valueArray = serialized.FindProperty("_ratingValues");
        fillArray.arraySize = valueArray.arraySize = Ratings.Length;
        for (int i = 0; i < Ratings.Length; i++)
        {
            fillArray.GetArrayElementAtIndex(i).objectReferenceValue = fills[i];
            valueArray.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SetRef(view, "_actionButton", action.GetComponent<UIButton>());
        SetRef(view, "_actionLabel", actionContent.Find("Label").GetComponent<TMP_Text>());
        SetRef(view, "_actionCoin", actionCoin);
        SetRef(view, "_note", note);
        SetRef(view, "_content", contentGroup);
        SavePrefab(root.gameObject, CueCardPath);
    }

    // Label + bar + number; the whole row is a tooltip trigger.
    private static (Image fill, TMP_Text value) RatingRow(Transform parent, string label, float y, string tooltip)
    {
        RectTransform row = CreateRect(label, parent);
        TopLeft(row, new Vector2(30f, y), new Vector2(560f, 42f));
        AddImage(row.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;
        var trigger = row.gameObject.AddComponent<TooltipTrigger>();
        var serialized = new SerializedObject(trigger);
        serialized.FindProperty("_text").stringValue = tooltip;
        serialized.FindProperty("_preferredSide").enumValueIndex = (int)TooltipSide.Above;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        TMP_Text text = CreateText(row, "Label", label, 26, TextMuted, TextAlignmentOptions.MidlineLeft);
        Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(160f, 36f));
        Image fill = Bar(row, "Bar", new Vector2(170f, 0f), new Vector2(300f, 16f), Gold);
        TMP_Text value = CreateText(row, "Value", "44", 30, Color.white, TextAlignmentOptions.MidlineLeft);
        value.fontSharedMaterial = OutlineMaterial;
        Anchor(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(530f, 0f), new Vector2(80f, 36f));
        return (fill, value);
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
        price.fontSharedMaterial = OutlineMaterial;
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
            (Generated + "/icon_hand.png", "Foul → your opponent gets <b>ball in hand</b>: they place the cue ball anywhere."),
            (GeneratedArtBuilder.BallSpritePath(8), "The 8-ball can go in any pocket. Pocket it early, or with a foul, and you lose.")
        };
        const float rowHeight = 170f;
        RectTransform panel = CreatePopupShell("RulesPopup", new Vector2(960, 1500), "HOUSE RULES", out GameObject root);
        var highlights = new Image[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            Image row = Panel(panel, "Rule" + (i + 1), new Vector2(0.5f, 1f), new Vector2(0, -235 - i * rowHeight), new Vector2(880, rowHeight - 14), Color.clear);
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
        SavePrefab(root, RulesPopupPath);
    }

    // v3 V13: title, reason, the rank badge with its stars, the CHECKPOINT stamp, coins, finished objectives,
    // an affordable cue, PLAY AGAIN and MENU.
    private static void BuildMatchResultPopup()
    {
        RectTransform panel = CreatePopupShell("MatchResultPopup", new Vector2(940, 1720), "", out GameObject root);
        Object.DestroyImmediate(panel.Find("Title").gameObject);

        RectTransform confetti = CreateRect("Confetti", panel);
        Anchor(confetti, new Vector2(0.5f, 1f), new Vector2(0, -150), Vector2.zero);

        TMP_Text title = CreateTopText(panel, "Title", "YOU WIN!", 110, Gold, -140);
        title.fontSharedMaterial = OutlineMaterial;
        title.enableAutoSizing = true;
        title.fontSizeMin = 56f;
        title.fontSizeMax = 110f;
        TMP_Text reason = CreateTopText(panel, "Reason", "You pocketed the 8-ball!", 40, TextLight, -265);
        reason.textWrappingMode = TextWrappingModes.Normal;
        reason.rectTransform.sizeDelta = new Vector2(820, 100);

        Image rankBack = Panel(panel, "Rank", new Vector2(0.5f, 1f), new Vector2(0, -440), new Vector2(840, 200), new Color(0f, 0f, 0f, 0.35f));
        RankBadgeView rank = BuildRankBadge(rankBack.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), 1f, false, false, false);
        TMP_Text rankChange = CreateText(rankBack.transform, "Change", "+1 STAR", 40, Hex("7FE39A"), TextAlignmentOptions.Center);
        rankChange.fontSharedMaterial = OutlineMaterial;
        Anchor(rankChange.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(800f, 52f));

        Image stamp = Panel(panel, "Stamp", new Vector2(0.5f, 1f), new Vector2(0, -440), new Vector2(780, 200), Hex("7A1F1A"));
        stamp.gameObject.AddComponent<CanvasGroup>();
        Image stampFace = Panel(stamp.transform, "Face", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(756, 176), Hex("B8322A"));
        TMP_Text stampTitle = CreateText(stampFace.transform, "Title", "CHECKPOINT · SILVER", 58, Color.white, TextAlignmentOptions.Center);
        stampTitle.fontSharedMaterial = OutlineMaterial;
        stampTitle.enableAutoSizing = true;
        stampTitle.fontSizeMin = 36f;
        stampTitle.fontSizeMax = 58f;
        Anchor(stampTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(720f, 76f));
        TMP_Text stampSub = CreateText(stampFace.transform, "Sub", "New venue: Downtown Hall", 34, Hex("FFE7A8"), TextAlignmentOptions.Center);
        Anchor(stampSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -44f), new Vector2(720f, 50f));

        RectTransform coinRow = CreateRect("Coins", panel);
        Anchor(coinRow, new Vector2(0.5f, 1f), new Vector2(0, -630), new Vector2(500, 100));
        RectTransform coin = CreateRect("Coin", coinRow);
        Anchor(coin, new Vector2(0.5f, 0.5f), new Vector2(-90, 0), new Vector2(88, 88));
        AddImage(coin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text delta = CreateText(coinRow, "Delta", "+20", 80, Gold, TextAlignmentOptions.MidlineLeft);
        delta.fontSharedMaterial = OutlineMaterial;
        Anchor(delta.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80, 0), new Vector2(240, 100));
        TMP_Text balance = CreateTopText(panel, "Balance", "Balance 20", 38, TextMuted, -715);

        TMP_Text objectivesHeader = CreateTopText(panel, "ObjectivesHeader", "OBJECTIVE DONE", 34, Gold, -800);
        objectivesHeader.fontSharedMaterial = OutlineMaterial;
        var rows = new ObjectiveRowView[ObjectiveCatalog.Slots];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = BuildObjectiveRow(panel, new Vector2(0.5f, 1f), new Vector2(0, -870 - i * 96), new Vector2(840, 86));

        Image cueCard = Panel(panel, "Cue", new Vector2(0.5f, 1f), new Vector2(0, -1210), new Vector2(840, 110), Hex("2C3B22"));
        TMP_Text cueText = CreateText(cueCard.transform, "Text", "You can buy the Club Cue!", 34, TextLight, TextAlignmentOptions.MidlineLeft);
        cueText.textWrappingMode = TextWrappingModes.Normal;
        Anchor(cueText.rectTransform, new Vector2(0f, 0.5f), new Vector2(300, 0), new Vector2(540, 96));
        GameObject seeCues = CreateButtonInstance(cueCard.transform, "CUES", Icons + "/icon_star.png", new Vector2(230, 90), 36, ButtonColor.Yellow);
        Anchor((RectTransform)seeCues.transform, new Vector2(1f, 0.5f), new Vector2(-135, 0), new Vector2(230, 90));

        UIButton playAgain = InstantiateButton(ButtonVariant.PlayAgain, panel, Vector2.zero);
        Anchor((RectTransform)playAgain.transform, new Vector2(0.5f, 0f), new Vector2(0, 290), new Vector2(620, 160));
        UIButton menu = InstantiateButton(ButtonVariant.Menu, panel, Vector2.zero);
        Anchor((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0, 115), new Vector2(620, 140));
        confetti.SetAsLastSibling();

        var popup = root.AddComponent<MatchResultPopup>();
        WirePopup(popup, closeOnBack: false, closeOnDimClick: false);
        SetRef(popup, "_title", title);
        SetRef(popup, "_reason", reason);
        SetRef(popup, "_rank", rank);
        SetRef(popup, "_rankChange", rankChange);
        SetRef(popup, "_stamp", stamp.rectTransform);
        SetRef(popup, "_stampTitle", stampTitle);
        SetRef(popup, "_stampSub", stampSub);
        SetRef(popup, "_delta", delta);
        SetRef(popup, "_balance", balance);
        SetRef(popup, "_coinRow", coinRow.gameObject);
        SetRef(popup, "_objectivesHeader", objectivesHeader.gameObject);
        SetArray(popup, "_objectives", rows);
        SetRef(popup, "_cueCard", cueCard.gameObject);
        SetRef(popup, "_cueText", cueText);
        SetRef(popup, "_seeCuesButton", seeCues.GetComponent<UIButton>());
        SetRef(popup, "_playAgainLabel", playAgain.transform.Find("Content/Label").GetComponent<TMP_Text>());
        SetRef(popup, "_playAgainButton", playAgain);
        SetRef(popup, "_menuButton", menu);
        SetRef(popup, "_confettiRoot", confetti);
        SetRef(popup, "_confettiSprite", LoadSprite(Generated + "/ui_white.png"));
        SavePrefab(root, MatchResultPopupPath);
    }

    // v3 V12: 15 stages and Grand Champion, Bronze III at the bottom; rows[0] is Bronze III.
    private static void BuildRankedRoadPopup()
    {
        RectTransform panel = CreatePopupShell("RankedRoadPopup", new Vector2(1020, 1760), "RANKED ROAD", out GameObject root);

        RectTransform viewport = CreateRect("Viewport", panel);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(20, 230);
        viewport.offsetMax = new Vector2(-20, -190);
        AddImage(viewport.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(0, 0, 10, 10);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int count = DataAssetBuilder.DivisionSpecs.Length * DivisionCatalog.StagesPerDivision + 1;
        var rows = new RoadRowView[count];
        for (int i = count - 1; i >= 0; i--)
            rows[i] = BuildRoadRow(content, i);

        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        UIButton close = InstantiateButton(ButtonVariant.Close, panel, Vector2.zero);
        Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 115), new Vector2(420, 140));

        var popup = root.AddComponent<RankedRoadPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: false);
        SetArray(popup, "_rows", rows);
        SetRef(popup, "_scroll", scroll);
        SetRef(popup, "_closeButton", close);
        SavePrefab(root, RankedRoadPopupPath);
    }

    private static RoadRowView BuildRoadRow(Transform parent, int index)
    {
        const float width = 940f, height = 150f;
        RectTransform row = CreateRect("Stage" + index, parent);
        row.sizeDelta = new Vector2(width, height);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        Image background = AddImage(row.gameObject, LoadSprite(Generated + "/ui_round_24.png"), new Color(0f, 0f, 0f, 0.55f), true);
        background.pixelsPerUnitMultiplier = 1.5f;

        // The venue's thumbnail on checkpoint rows, faded behind the text, masked to the row.
        RectTransform thumbMask = CreateRect("Thumbnail", row);
        Stretch(thumbMask, new Vector4(4f, 4f, 4f, 4f));
        thumbMask.gameObject.AddComponent<RectMask2D>();
        RectTransform thumb = CreateRect("Image", thumbMask);
        thumb.anchorMin = new Vector2(1f, 0.5f);
        thumb.anchorMax = new Vector2(1f, 0.5f);
        thumb.pivot = new Vector2(1f, 0.5f);
        thumb.anchoredPosition = Vector2.zero;
        thumb.sizeDelta = new Vector2(600f, 300f);
        Image thumbnail = AddImage(thumb.gameObject, null, new Color(1f, 1f, 1f, 0.38f), false);

        RectTransform badge = CreateRect("Badge", row);
        Anchor(badge, new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(96f, 96f));
        Image badgeImage = AddImage(badge.gameObject, LoadSprite(Generated + "/rank_badge.png"), Color.white, false);

        TMP_Text label = CreateText(row, "Label", "BRONZE III", 44, Color.white, TextAlignmentOptions.MidlineLeft);
        label.fontSharedMaterial = OutlineMaterial;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 44f;
        Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(340f, 26f), new Vector2(370f, 56f));

        RectTransform checkpoint = CreateRect("Checkpoint", row);
        Anchor(checkpoint, new Vector2(0f, 0.5f), new Vector2(450f, -34f), new Vector2(600f, 40f));
        RectTransform flag = CreateRect("Flag", checkpoint);
        Anchor(flag, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(36f, 36f));
        AddImage(flag.gameObject, LoadSprite(Generated + "/icon_flag.png"), Hex("FF6B5E"), false);
        TMP_Text checkpointText = CreateText(checkpoint, "Text", "CHECKPOINT · Basement Bar · bot: House Cue", 24, TextMuted, TextAlignmentOptions.MidlineLeft);
        checkpointText.enableAutoSizing = true;
        checkpointText.fontSizeMin = 18f;
        checkpointText.fontSizeMax = 24f;
        Anchor(checkpointText.rectTransform, new Vector2(0f, 0.5f), new Vector2(315f, 0f), new Vector2(560f, 36f));

        var stars = new Image[3];
        for (int i = 0; i < stars.Length; i++)
        {
            RectTransform star = CreateRect("Star" + i, row);
            Anchor(star, new Vector2(1f, 0.5f), new Vector2(-370f + i * 54f, 24f), new Vector2(48f, 48f));
            stars[i] = AddImage(star.gameObject, LoadSprite(Icons + "/icon_star.png"), Color.white, false);
        }
        TMP_Text championStars = CreateText(row, "ChampionStars", "ENDLESS", 40, Gold, TextAlignmentOptions.Center);
        championStars.fontSharedMaterial = OutlineMaterial;
        championStars.enableAutoSizing = true;
        championStars.fontSizeMin = 20f;
        championStars.fontSizeMax = 36f;
        Anchor(championStars.rectTransform, new Vector2(1f, 0.5f), new Vector2(-318f, 24f), new Vector2(146f, 54f));

        Image here = Panel(row, "Here", new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(210f, 70f), Hex("2E9E4F"));
        TMP_Text hereText = CreateText(here.transform, "Text", "YOU ARE HERE", 22, Color.white, TextAlignmentOptions.Center);
        hereText.fontSharedMaterial = OutlineMaterial;
        hereText.enableAutoSizing = true;
        hereText.fontSizeMin = 14f;
        hereText.fontSizeMax = 22f;
        Stretch(hereText.rectTransform, new Vector4(10f, 0f, 10f, 0f));
        RectTransform done = CreateRect("Done", row);
        Anchor(done, new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(56f, 56f));
        AddImage(done.gameObject, LoadSprite(Icons + "/icon_checkmark.png"), Hex("7FE39A"), false);

        var view = row.gameObject.AddComponent<RoadRowView>();
        SetRef(view, "_background", background);
        SetRef(view, "_thumbnail", thumbnail);
        SetRef(view, "_badge", badgeImage);
        SetRef(view, "_label", label);
        SetArray(view, "_stars", stars);
        SetRef(view, "_championStars", championStars);
        SetRef(view, "_checkpoint", checkpoint.gameObject);
        SetRef(view, "_checkpointText", checkpointText);
        SetRef(view, "_here", here.gameObject);
        SetRef(view, "_done", done.gameObject);
        return view;
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
        label.fontSharedMaterial = OutlineMaterial;
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
        TMP_Text championStars = CreateText(root, "ChampionStars", "0 STARS", 46, Gold, TextAlignmentOptions.MidlineLeft);
        championStars.fontSharedMaterial = OutlineMaterial;
        Anchor(championStars.rectTransform, new Vector2(0f, 0.5f), new Vector2(260f, top - 34f), new Vector2(220f, 60f));

        TMP_Text next = null;
        if (withNext)
        {
            next = CreateText(root, "Next", "2 stars to BRONZE II", 34, TextLight, TextAlignmentOptions.Center);
            next.fontSharedMaterial = OutlineMaterial;
            Anchor(next.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(620f, 46f));
        }

        var view = root.gameObject.AddComponent<RankBadgeView>();
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

    // One objective row: text, progress bar and count, coin reward, CLAIM.
    public static ObjectiveRowView BuildObjectiveRow(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image background = Panel(parent, "Objective", anchor, position, size, new Color(0f, 0f, 0f, 0.45f));
        Transform row = background.transform;

        TMP_Text text = CreateText(row, "Text", "Release on PERFECT 5 times", 30, TextLight, TextAlignmentOptions.MidlineLeft);
        text.enableAutoSizing = true;
        text.fontSizeMin = 15f;
        text.fontSizeMax = 30f;
        Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f + (size.x - 440f) * 0.5f, 12f), new Vector2(size.x - 440f, 40f));
        Image fill = Bar(row, "Bar", new Vector2(26f, -22f), new Vector2(size.x - 450f, 12f), Hex("7FE39A"));
        TMP_Text progress = CreateText(row, "Progress", "3/5", 26, TextMuted, TextAlignmentOptions.MidlineRight);
        Anchor(progress.rectTransform, new Vector2(1f, 0.5f), new Vector2(-245f, -2f), new Vector2(140f, 40f));

        RectTransform coin = CreateRect("Coin", row);
        Anchor(coin, new Vector2(1f, 0.5f), new Vector2(-138f, 0f), new Vector2(44f, 44f));
        AddImage(coin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text reward = CreateText(row, "Reward", "50", 32, Gold, TextAlignmentOptions.MidlineLeft);
        reward.fontSharedMaterial = OutlineMaterial;
        Anchor(reward.rectTransform, new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(110f, 44f));

        GameObject claim = CreateButtonInstance(row, "CLAIM", null, new Vector2(190f, size.y - 14f), 32, ButtonColor.Green);
        Anchor((RectTransform)claim.transform, new Vector2(1f, 0.5f), new Vector2(-305f, 0f), new Vector2(190f, size.y - 14f));

        var view = background.gameObject.AddComponent<ObjectiveRowView>();
        SetRef(view, "_text", text);
        SetRef(view, "_fill", fill);
        SetRef(view, "_progress", progress);
        SetRef(view, "_reward", reward);
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
        initials.fontSharedMaterial = OutlineMaterial;
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
        titleText.fontSharedMaterial = OutlineMaterial;

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
