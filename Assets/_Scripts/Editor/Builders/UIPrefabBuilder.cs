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
    public const string NoEnergyPopupPath = PopupFolder + "/NoEnergyPopup.prefab";
    public const string PausePopupPath = PopupFolder + "/PausePopup.prefab";
    public const string MatchResultPopupPath = PopupFolder + "/MatchResultPopup.prefab";

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
        BuildNoEnergyPopup();
        BuildCuesPopup();
        BuildPausePopup();
        BuildMatchResultPopup();

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

    private const string AimLineTooltip = "How far the guide shows where the hit ball goes.\nLonger line = easier aiming.";
    private const string ReleaseTooltip = "Size of the green and yellow zones and how slowly the bar moves.\nBigger = easier PERFECT shots.";

    private static void BuildCueCard()
    {
        const float width = 960f, height = 330f;
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

        TMP_Text name = CreateText(content, "Name", "Rookie Cue", 50, TextLight, TextAlignmentOptions.MidlineLeft);
        name.fontSharedMaterial = OutlineMaterial;
        TopLeft(name.rectTransform, new Vector2(34f, -42f), new Vector2(560f, 64f));

        RectTransform cue = CreateRect("CueImage", content);
        TopLeft(cue, new Vector2(30f, -98f), new Vector2(590f, 40f));
        Image cueImage = AddImage(cue.gameObject, LoadSprite(Generated + "/cue_rookie.png"), Color.white, false);
        cueImage.preserveAspect = false;

        Image aimBar = StatRow(content, "AimLine", Generated + "/icon_eye.png", "AIM LINE", -160f, AimLineTooltip);
        Image releaseBar = StatRow(content, "ReleaseWindow", Icons + "/icon_star.png", "RELEASE WINDOW", -214f, ReleaseTooltip);

        // Energy row: bolt, bar, value, refill.
        RectTransform energy = CreateRect("Energy", content);
        TopLeft(energy, new Vector2(30f, -272f), new Vector2(900f, 70f));
        RectTransform bolt = CreateRect("Bolt", energy);
        Anchor(bolt, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(46f, 46f));
        AddImage(bolt.gameObject, LoadSprite(Generated + "/icon_bolt.png"), Color.white, false);
        Image energyBar = Bar(energy, "EnergyBar", new Vector2(70f, 0f), new Vector2(220f, 18f), Hex("F6C445"));
        TMP_Text energyText = CreateText(energy, "Value", "∞", 40, TextLight, TextAlignmentOptions.MidlineLeft);
        Anchor(energyText.rectTransform, new Vector2(0f, 0.5f), new Vector2(370f, 0f), new Vector2(120f, 60f));
        GameObject refill = CreateButtonInstance(energy, "REFILL · 5", Generated + "/icon_coin.png", new Vector2(300f, 84f), 32, ButtonColor.Green);
        Anchor((RectTransform)refill.transform, new Vector2(0f, 0.5f), new Vector2(600f, 0f), new Vector2(300f, 84f));

        // Action area on the right.
        GameObject action = CreateButtonInstance(content, "SELECT", Generated + "/icon_coin.png", new Vector2(290f, 112f), 40, ButtonColor.Blue);
        Anchor((RectTransform)action.transform, new Vector2(1f, 1f), new Vector2(-175f, -110f), new Vector2(290f, 112f));
        Transform actionContent = action.transform.Find("Content");
        GameObject actionCoin = actionContent.Find("Icon").gameObject;

        TMP_Text note = CreateText(content, "Note", "Need 12 more coins", 28, Hex("FF6B5E"), TextAlignmentOptions.Center);
        Anchor(note.rectTransform, new Vector2(1f, 1f), new Vector2(-175f, -192f), new Vector2(320f, 40f));

        RectTransform badge = CreateRect("SelectedBadge", content);
        Anchor(badge, new Vector2(1f, 1f), new Vector2(-175f, -110f), new Vector2(270f, 76f));
        AddImage(badge.gameObject, LoadSprite(Generated + "/ui_round_24.png"), Hex("2E9E4F"), true);
        TMP_Text badgeText = CreateText(badge, "Text", "SELECTED", 38, Color.white, TextAlignmentOptions.Center);
        badgeText.fontSharedMaterial = OutlineMaterial;
        Stretch(badgeText.rectTransform, Vector4.zero);

        // Locked overlay sits above the dimmed content.
        RectTransform locked = CreateRect("Locked", root);
        Stretch(locked, Vector4.zero);
        RectTransform lockIcon = CreateRect("Lock", locked);
        Anchor(lockIcon, new Vector2(1f, 1f), new Vector2(-175f, -95f), new Vector2(90f, 90f));
        AddImage(lockIcon.gameObject, LoadSprite(Icons + "/icon_locked.png"), Color.white, false);
        TMP_Text lockedText = CreateText(locked, "Text", "Win 3 matches to unlock (1/3)", 30, TextLight, TextAlignmentOptions.Center);
        lockedText.textWrappingMode = TextWrappingModes.Normal;
        Anchor(lockedText.rectTransform, new Vector2(1f, 1f), new Vector2(-175f, -190f), new Vector2(320f, 80f));
        Image progress = Bar(locked, "Progress", Vector2.zero, new Vector2(300f, 18f), Hex("39C455"));
        RectTransform progressRoot = (RectTransform)progress.transform.parent;
        progressRoot.anchorMin = progressRoot.anchorMax = new Vector2(1f, 1f);
        progressRoot.pivot = new Vector2(0.5f, 0.5f);
        progressRoot.anchoredPosition = new Vector2(-175f, -252f);

        var view = root.gameObject.AddComponent<CueCardView>();
        SetRef(view, "_frame", frame);
        SetRef(view, "_cueImage", cueImage);
        SetRef(view, "_name", name);
        SetRef(view, "_selectedBadge", badge.gameObject);
        SetRef(view, "_aimBar", aimBar);
        SetRef(view, "_releaseBar", releaseBar);
        SetRef(view, "_energyRow", energy.gameObject);
        SetRef(view, "_energyBar", energyBar);
        SetRef(view, "_energyText", energyText);
        SetRef(view, "_refillButton", refill.GetComponent<UIButton>());
        SetRef(view, "_refillLabel", refill.transform.Find("Content/Label").GetComponent<TMP_Text>());
        SetRef(view, "_actionButton", action.GetComponent<UIButton>());
        SetRef(view, "_actionLabel", actionContent.Find("Label").GetComponent<TMP_Text>());
        SetRef(view, "_actionCoin", actionCoin);
        SetRef(view, "_note", note);
        SetRef(view, "_lockedOverlay", locked.gameObject);
        SetRef(view, "_lockedText", lockedText);
        SetRef(view, "_unlockProgress", progress);
        SetRef(view, "_content", contentGroup);
        SavePrefab(root.gameObject, CueCardPath);
    }

    // Icon + label + bar; the whole row is a tooltip trigger.
    private static Image StatRow(Transform parent, string name, string icon, string label, float y, string tooltip)
    {
        RectTransform row = CreateRect(name, parent);
        TopLeft(row, new Vector2(30f, y), new Vector2(560f, 50f));
        AddImage(row.gameObject, null, new Color(0f, 0f, 0f, 0f), false).raycastTarget = true;
        var trigger = row.gameObject.AddComponent<TooltipTrigger>();
        var serialized = new SerializedObject(trigger);
        serialized.FindProperty("_text").stringValue = tooltip;
        serialized.FindProperty("_preferredSide").enumValueIndex = (int)TooltipSide.Above;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        RectTransform iconRect = CreateRect("Icon", row);
        Anchor(iconRect, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(40f, 40f));
        AddImage(iconRect.gameObject, LoadSprite(icon), TextMuted, false);
        TMP_Text text = CreateText(row, "Label", label, 24, TextMuted, TextAlignmentOptions.MidlineLeft);
        Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(175f, 0f), new Vector2(260f, 40f));
        RectTransform info = CreateRect("Info", row);
        Anchor(info, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(34f, 34f));
        AddImage(info.gameObject, LoadSprite(Icons + "/icon_information.png"), TextMuted, false);
        return Bar(row, "Bar", new Vector2(320f, 0f), new Vector2(190f, 16f), Gold);
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
    private static void BuildSettingsPopup()
    {
        RectTransform panel = CreatePopupShell("SettingsPopup", new Vector2(900, 900), "SETTINGS", out GameObject root);

        CreateIconLabel(panel, Icons + "/icon_audioOn.png", "SOUND EFFECTS", new Vector2(0, -230));
        Slider sfx = InstantiateSlider(panel, new Vector2(0, -320));
        CreateIconLabel(panel, Icons + "/icon_musicOn.png", "MUSIC", new Vector2(0, -450));
        Slider music = InstantiateSlider(panel, new Vector2(0, -540));

        UIButton close = InstantiateButton(ButtonVariant.Close, panel, Vector2.zero);
        Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(420, 140));

        var popup = root.AddComponent<SettingsPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: true);
        SetRef(popup, "_sfxSlider", sfx);
        SetRef(popup, "_musicSlider", music);
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

    private static void BuildNoEnergyPopup()
    {
        RectTransform panel = CreatePopupShell("NoEnergyPopup", new Vector2(900, 1080), "OUT OF ENERGY", out GameObject root);

        RectTransform bolt = CreateRect("Bolt", panel);
        Anchor(bolt, new Vector2(0.5f, 1f), new Vector2(0, -270), new Vector2(150, 150));
        AddImage(bolt.gameObject, LoadSprite(Generated + "/icon_bolt.png"), Color.white, false);

        TMP_Text body = CreateTopText(panel, "Body", "Your Club Cue is out of energy.", 42, TextLight, -420);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.rectTransform.sizeDelta = new Vector2(780, 120);

        GameObject refill = CreateButtonInstance(panel, "REFILL · 5", Generated + "/icon_coin.png", new Vector2(560, 140), 50, ButtonColor.Green);
        Anchor((RectTransform)refill.transform, new Vector2(0.5f, 1f), new Vector2(0, -560), new Vector2(560, 140));
        TMP_Text shortText = CreateTopText(panel, "Short", "Need 3 more coins", 32, Hex("FF6B5E"), -650);
        GameObject change = CreateButtonInstance(panel, "CHANGE CUE", Icons + "/icon_star.png", new Vector2(560, 140), 50, ButtonColor.Yellow);
        Anchor((RectTransform)change.transform, new Vector2(0.5f, 1f), new Vector2(0, -760), new Vector2(560, 140));

        UIButton close = InstantiateButton(ButtonVariant.Close, panel, Vector2.zero);
        Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(420, 130));

        var popup = root.AddComponent<NoEnergyPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: true);
        SetRef(popup, "_body", body);
        SetRef(popup, "_refillButton", refill.GetComponent<UIButton>());
        SetRef(popup, "_refillLabel", refill.transform.Find("Content/Label").GetComponent<TMP_Text>());
        SetRef(popup, "_shortText", shortText);
        SetRef(popup, "_changeCueButton", change.GetComponent<UIButton>());
        SetRef(popup, "_closeButton", close);
        SavePrefab(root, NoEnergyPopupPath);
    }

    private static void BuildCuesPopup()
    {
        RectTransform panel = CreatePopupShell("CuesPopup", new Vector2(1020, 1720), "CUES", out GameObject root);
        CoinCounterView coins = InstantiateCoinCounter(panel, new Vector2(1f, 1f), new Vector2(-170, -100));

        RectTransform viewport = CreateRect("Viewport", panel);
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.offsetMin = new Vector2(20, 230);
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
        RectTransform panel = CreatePopupShell("PausePopup", new Vector2(860, 900), "PAUSED", out GameObject root);
        UIButton resume = InstantiateButton(ButtonVariant.Resume, panel, Vector2.zero);
        Anchor((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(600, 150));
        UIButton settings = InstantiateButton(ButtonVariant.Settings, panel, Vector2.zero);
        Anchor((RectTransform)settings.transform, new Vector2(0.5f, 1f), new Vector2(0, -470), new Vector2(600, 150));
        UIButton forfeit = InstantiateButton(ButtonVariant.Forfeit, panel, Vector2.zero);
        Anchor((RectTransform)forfeit.transform, new Vector2(0.5f, 1f), new Vector2(0, -690), new Vector2(600, 150));

        var popup = root.AddComponent<PausePopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: false);
        SetRef(popup, "_resumeButton", resume);
        SetRef(popup, "_settingsButton", settings);
        SetRef(popup, "_forfeitButton", forfeit);
        SavePrefab(root, PausePopupPath);
    }

    private static void BuildMatchResultPopup()
    {
        RectTransform panel = CreatePopupShell("MatchResultPopup", new Vector2(920, 1300), "", out GameObject root);
        Object.DestroyImmediate(panel.Find("Title").gameObject);

        RectTransform confetti = CreateRect("Confetti", panel);
        Anchor(confetti, new Vector2(0.5f, 1f), new Vector2(0, -150), Vector2.zero);

        TMP_Text title = CreateTopText(panel, "Title", "YOU WIN!", 110, Gold, -150);
        title.fontSharedMaterial = OutlineMaterial;
        TMP_Text reason = CreateTopText(panel, "Reason", "You pocketed the 8-ball!", 40, TextLight, -290);
        reason.textWrappingMode = TextWrappingModes.Normal;
        reason.rectTransform.sizeDelta = new Vector2(800, 110);

        RectTransform coinRow = CreateRect("Coins", panel);
        Anchor(coinRow, new Vector2(0.5f, 1f), new Vector2(0, -430), new Vector2(500, 110));
        RectTransform coin = CreateRect("Coin", coinRow);
        Anchor(coin, new Vector2(0.5f, 0.5f), new Vector2(-90, 0), new Vector2(96, 96));
        AddImage(coin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text delta = CreateText(coinRow, "Delta", "+10", 88, Gold, TextAlignmentOptions.MidlineLeft);
        delta.fontSharedMaterial = OutlineMaterial;
        Anchor(delta.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80, 0), new Vector2(240, 110));
        TMP_Text balance = CreateTopText(panel, "Balance", "Balance 20", 42, TextMuted, -530);

        Image unlock = Panel(panel, "Unlock", new Vector2(0.5f, 1f), new Vector2(0, -690), new Vector2(800, 170), Hex("2C3B22"));
        RectTransform star = CreateRect("Star", unlock.transform);
        Anchor(star, new Vector2(0f, 0.5f), new Vector2(70, 0), new Vector2(80, 80));
        AddImage(star.gameObject, LoadSprite(Icons + "/icon_star.png"), Gold, false);
        TMP_Text unlockText = CreateText(unlock.transform, "Text", "New cue unlocked: Pro Cue!", 36, TextLight, TextAlignmentOptions.MidlineLeft);
        unlockText.textWrappingMode = TextWrappingModes.Normal;
        Anchor(unlockText.rectTransform, new Vector2(0f, 0.5f), new Vector2(330, 0), new Vector2(420, 140));
        GameObject seeCues = CreateButtonInstance(unlock.transform, "CUES", Icons + "/icon_star.png", new Vector2(220, 100), 36, ButtonColor.Yellow);
        Anchor((RectTransform)seeCues.transform, new Vector2(1f, 0.5f), new Vector2(-130, 0), new Vector2(220, 100));

        UIButton playAgain = InstantiateButton(ButtonVariant.PlayAgain, panel, Vector2.zero);
        Anchor((RectTransform)playAgain.transform, new Vector2(0.5f, 0f), new Vector2(0, 300), new Vector2(620, 160));
        UIButton menu = InstantiateButton(ButtonVariant.Menu, panel, Vector2.zero);
        Anchor((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0, 125), new Vector2(620, 140));
        confetti.SetAsLastSibling();

        var popup = root.AddComponent<MatchResultPopup>();
        WirePopup(popup, closeOnBack: false, closeOnDimClick: false);
        SetRef(popup, "_title", title);
        SetRef(popup, "_reason", reason);
        SetRef(popup, "_delta", delta);
        SetRef(popup, "_balance", balance);
        SetRef(popup, "_unlockCard", unlock.gameObject);
        SetRef(popup, "_unlockText", unlockText);
        SetRef(popup, "_seeCuesButton", seeCues.GetComponent<UIButton>());
        SetRef(popup, "_playAgainButton", playAgain);
        SetRef(popup, "_menuButton", menu);
        SetRef(popup, "_confettiRoot", confetti);
        SetRef(popup, "_confettiSprite", LoadSprite(Generated + "/ui_white.png"));
        SavePrefab(root, MatchResultPopupPath);
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
