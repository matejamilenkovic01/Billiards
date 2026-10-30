using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The meta screens of v4 (V11, V12): DIVISIONS, ALL OBJECTIVES and the full-screen celebration.
public static partial class UIPrefabBuilder
{
    private const float BlockWidth = 940f, BlockHeader = 260f, StageRow = 100f;

    // DIVISIONS: a scrolling column of division blocks, Grand Champion on top, Bronze at the bottom (blocks[0]).
    private static void BuildDivisionsPopup()
    {
        RectTransform panel = CreatePopupShell("DivisionsPopup", new Vector2(1020, 1760), "DIVISIONS", out GameObject root);

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
        layout.spacing = 26f;
        layout.padding = new RectOffset(0, 0, 10, 10);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int count = DataAssetBuilder.DivisionSpecs.Length + 1;
        var blocks = new DivisionBlockView[count];
        for (int i = count - 1; i >= 0; i--)
            blocks[i] = BuildDivisionBlock(content, i, i == count - 1);

        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        UIButton close = InstantiateButton(ButtonVariant.Close, panel, Vector2.zero);
        Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 115), new Vector2(420, 140));

        var popup = root.AddComponent<DivisionsPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: false);
        SetArray(popup, "_blocks", blocks);
        SetRef(popup, "_scroll", scroll);
        SetRef(popup, "_closeButton", close);
        SavePrefab(root, DivisionsPopupPath);
    }

    private static DivisionBlockView BuildDivisionBlock(Transform parent, int index, bool champion)
    {
        float height = champion ? BlockHeader : BlockHeader + 3 * StageRow + 20f;
        RectTransform block = CreateRect(champion ? "GrandChampion" : "Division" + index, parent);
        block.sizeDelta = new Vector2(BlockWidth, height);
        block.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        Image background = AddImage(block.gameObject, LoadSprite(Generated + "/ui_round_24.png"), new Color(0f, 0f, 0f, 0.55f), true);
        background.pixelsPerUnitMultiplier = 1.5f;

        // The header in the division's colour with its venue behind the right side.
        Image header = Panel(block, "Header", new Vector2(0.5f, 1f), new Vector2(0f, -BlockHeader * 0.5f), new Vector2(BlockWidth, BlockHeader), Color.gray);
        RectTransform thumbMask = CreateRect("Thumbnail", header.transform);
        Stretch(thumbMask, new Vector4(4f, 4f, 4f, 4f));
        thumbMask.gameObject.AddComponent<RectMask2D>();
        RectTransform thumb = CreateRect("Image", thumbMask);
        thumb.anchorMin = thumb.anchorMax = new Vector2(1f, 0.5f);
        thumb.pivot = new Vector2(1f, 0.5f);
        thumb.sizeDelta = new Vector2(560f, 280f);
        Image thumbnail = AddImage(thumb.gameObject, null, new Color(1f, 1f, 1f, 0.45f), false);
        thumbnail.preserveAspect = false;

        TMP_Text name = CreateText(header.transform, "Name", "GOLD DIVISION", 56, Color.white, TextAlignmentOptions.MidlineLeft);
        name.fontSharedMaterial = OutlineFor(name);
        name.enableAutoSizing = true;
        name.fontSizeMin = 36f;
        name.fontSizeMax = 56f;
        TopLeft(name.rectTransform, new Vector2(30f, -52f), new Vector2(620f, 70f));
        TMP_Text venue = CreateText(header.transform, "Venue", "Riverside Club  ·  Pro Cue", 32, TextLight, TextAlignmentOptions.MidlineLeft);
        venue.fontSharedMaterial = OutlineFor(venue);
        TopLeft(venue.rectTransform, new Vector2(30f, -112f), new Vector2(640f, 44f));

        Image ruleIcon = AddImage(CreateRect("RuleIcon", header.transform).gameObject, LoadSprite(Generated + "/icon_pocket_any.png"), Color.white, false);
        TopLeft(ruleIcon.rectTransform, new Vector2(30f, -184f), new Vector2(64f, 64f));
        TMP_Text rule = CreateText(header.transform, "Rule", "Your opponent blocks one pocket for your 8-ball.", 30, Color.white, TextAlignmentOptions.MidlineLeft);
        rule.fontSharedMaterial = OutlineFor(rule);
        rule.textWrappingMode = TextWrappingModes.Normal;
        rule.overflowMode = TextOverflowModes.Ellipsis;
        TopLeft(rule.rectTransform, new Vector2(110f, -184f), new Vector2(560f, 84f));

        // What entering pays, top right.
        RectTransform entryCoin = CreateRect("EntryCoin", header.transform);
        entryCoin.anchorMin = entryCoin.anchorMax = new Vector2(1f, 1f);
        entryCoin.anchoredPosition = new Vector2(-250f, -48f);
        entryCoin.sizeDelta = new Vector2(46f, 46f);
        AddImage(entryCoin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text entry = CreateText(header.transform, "Entry", "300", 38, Gold, TextAlignmentOptions.MidlineRight);
        entry.fontSharedMaterial = OutlineFor(entry);
        entry.rectTransform.anchorMin = entry.rectTransform.anchorMax = new Vector2(1f, 1f);
        entry.rectTransform.anchoredPosition = new Vector2(-135f, -48f);
        entry.rectTransform.sizeDelta = new Vector2(200f, 50f);

        // Locked (later) or passed (earlier), bottom right of the header.
        Image lockBack = Panel(header.transform, "Lock", new Vector2(1f, 0f), new Vector2(-190f, 40f), new Vector2(330f, 52f), new Color(0f, 0f, 0f, 0.6f));
        TMP_Text lockText = CreateText(lockBack.transform, "Text", "Reach SILVER I", 30, Gold, TextAlignmentOptions.Center);
        lockText.fontSharedMaterial = OutlineFor(lockText);
        lockText.enableAutoSizing = true;
        lockText.fontSizeMin = 26f;
        lockText.fontSizeMax = 30f;
        Stretch(lockText.rectTransform, new Vector4(8f, 0f, 8f, 0f));
        Image doneBack = Panel(header.transform, "Done", new Vector2(1f, 0f), new Vector2(-150f, 40f), new Vector2(250f, 52f), new Color(0f, 0f, 0f, 0.6f));
        TMP_Text doneText = CreateText(doneBack.transform, "Text", "✓ PASSED", 30, Hex("7FE39A"), TextAlignmentOptions.Center);
        doneText.fontSharedMaterial = OutlineFor(doneText);
        Stretch(doneText.rectTransform, Vector4.zero);

        // The stages: III at the bottom, a thin line through the nodes.
        var stages = new DivisionStageView[champion ? 0 : DivisionCatalog.StagesPerDivision];
        if (!champion)
        {
            float lineTop = -(BlockHeader + 10f + StageRow * 0.5f);
            Image line = AddImage(CreateRect("Line", block).gameObject, LoadSprite(Generated + "/ui_white.png"), new Color(1f, 1f, 1f, 0.25f), false);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.anchoredPosition = new Vector2(60f, lineTop);
            line.rectTransform.sizeDelta = new Vector2(6f, StageRow * 2f);
            for (int s = 0; s < stages.Length; s++)
                stages[s] = BuildDivisionStage(block, new Vector2(0f, -(BlockHeader + 10f + StageRow * 0.5f + (stages.Length - 1 - s) * StageRow)), s);
        }

        var view = block.gameObject.AddComponent<DivisionBlockView>();
        SetRef(view, "_background", background);
        SetRef(view, "_header", header);
        SetRef(view, "_thumbnail", thumbnail);
        SetRef(view, "_name", name);
        SetRef(view, "_venue", venue);
        SetRef(view, "_ruleIcon", ruleIcon);
        SetRef(view, "_rule", rule);
        SetRef(view, "_entry", entry);
        SetRef(view, "_entryIcon", entryCoin.gameObject);
        SetRef(view, "_lock", lockBack.gameObject);
        SetRef(view, "_lockText", lockText);
        SetRef(view, "_done", doneBack.gameObject);
        SetArray(view, "_stages", stages);
        SetRef(view, "_anyPocketIcon", LoadSprite(Generated + "/icon_pocket_any.png"));
        SetRef(view, "_calledPocketIcon", LoadSprite(Generated + "/icon_pocket_called.png"));
        SetRef(view, "_blockedPocketIcon", LoadSprite(Generated + "/icon_pocket_blocked.png"));
        return view;
    }

    private static DivisionStageView BuildDivisionStage(Transform parent, Vector2 position, int stage)
    {
        RectTransform row = CreateRect("Stage" + stage, parent);
        row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 0.5f);
        row.anchoredPosition = new Vector2(0f, position.y);
        row.sizeDelta = new Vector2(BlockWidth, StageRow);

        RectTransform node = CreateRect("Node", row);
        Anchor(node, new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(40f, 40f));
        Image nodeImage = AddImage(node.gameObject, LoadSprite(Generated + "/ui_circle.png"), Color.white, false);

        TMP_Text label = CreateText(row, "Label", "GOLD III", 40, Color.white, TextAlignmentOptions.MidlineLeft);
        label.fontSharedMaterial = OutlineFor(label);
        Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(125f + 130f, 14f), new Vector2(260f, 48f));
        TMP_Text flag = CreateText(row, "Flag", "CHECKPOINT · can't drop below", 26, Gold, TextAlignmentOptions.MidlineLeft);
        flag.fontSharedMaterial = OutlineFor(flag);
        Anchor(flag.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f + 190f, -28f), new Vector2(380f, 34f));

        var stars = new Image[3];
        for (int i = 0; i < stars.Length; i++)
        {
            RectTransform star = CreateRect("Star" + i, row);
            Anchor(star, new Vector2(0f, 0.5f), new Vector2(480f + i * 52f, 14f), new Vector2(46f, 46f));
            stars[i] = AddImage(star.gameObject, LoadSprite(Icons + "/icon_star.png"), Color.white, false);
        }
        TMP_Text here = CreateText(row, "Here", "◀ YOU", 30, Gold, TextAlignmentOptions.MidlineLeft);
        here.fontSharedMaterial = OutlineFor(here);
        Anchor(here.rectTransform, new Vector2(0f, 0.5f), new Vector2(480f + 70f, -28f), new Vector2(200f, 36f));

        RectTransform coin = CreateRect("Coin", row);
        Anchor(coin, new Vector2(1f, 0.5f), new Vector2(-190f, 0f), new Vector2(44f, 44f));
        AddImage(coin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text reward = CreateText(row, "Reward", "100", 36, Gold, TextAlignmentOptions.MidlineRight);
        reward.fontSharedMaterial = OutlineFor(reward);
        Anchor(reward.rectTransform, new Vector2(1f, 0.5f), new Vector2(-105f, 0f), new Vector2(130f, 48f));
        TMP_Text check = CreateText(row, "Check", "✓", 40, Hex("7FE39A"), TextAlignmentOptions.Center);
        Anchor(check.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(50f, 50f));

        var view = row.gameObject.AddComponent<DivisionStageView>();
        SetRef(view, "_node", nodeImage);
        SetRef(view, "_nodeRect", node);
        SetRef(view, "_label", label);
        SetArray(view, "_stars", stars);
        SetRef(view, "_reward", reward);
        SetRef(view, "_rewardIcon", coin.gameObject);
        SetRef(view, "_check", check.gameObject);
        SetRef(view, "_here", here);
        SetRef(view, "_flag", flag);
        return view;
    }

    // ALL OBJECTIVES: a tab per division, six rows, the set bonus.
    private static void BuildObjectivesPopup()
    {
        RectTransform panel = CreatePopupShell("ObjectivesPopup", new Vector2(1020, 1760), "OBJECTIVES", out GameObject root);

        int tabCount = DataAssetBuilder.DivisionSpecs.Length;
        var tabs = new UIButton[tabCount];
        var labels = new TMP_Text[tabCount];
        var frames = new Image[tabCount];
        const float tabWidth = 184f, tabHeight = 84f;
        for (int i = 0; i < tabCount; i++)
        {
            float x = (i - (tabCount - 1) * 0.5f) * (tabWidth + 4f);
            Image frame = AddImage(CreateRect("TabFrame" + i, panel).gameObject, LoadSprite(Generated + "/ui_round_frame_24.png"), Gold, true);
            frame.pixelsPerUnitMultiplier = 1.5f;
            Anchor(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -215f), new Vector2(tabWidth + 8f, tabHeight + 8f));
            frame.gameObject.SetActive(false);
            GameObject tab = CreateButtonInstance(panel, DataAssetBuilder.DivisionSpecs[i].Name.ToUpperInvariant(), null, new Vector2(tabWidth, tabHeight), 30, ButtonColor.Blue);
            Anchor((RectTransform)tab.transform, new Vector2(0.5f, 1f), new Vector2(x, -215f), new Vector2(tabWidth, tabHeight));
            TMP_Text label = tab.transform.Find("Content/Label").GetComponent<TMP_Text>();
            label.enableAutoSizing = true;
            label.fontSizeMin = 26f;
            label.fontSizeMax = 30f;
            tabs[i] = tab.GetComponent<UIButton>();
            labels[i] = label;
            frames[i] = frame;
        }

        TMP_Text lockText = CreateTopText(panel, "Locked", "Reach GOLD to unlock", 36, Gold, -305);
        lockText.fontSharedMaterial = OutlineFor(lockText);
        lockText.gameObject.SetActive(false);

        var rows = new ObjectiveRowView[6];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = BuildObjectiveRow(panel, new Vector2(0.5f, 1f), new Vector2(0, -435 - i * 156), new Vector2(940, 148));

        // The set bonus and how close it is.
        Image bonusBack = Panel(panel, "Bonus", new Vector2(0.5f, 1f), new Vector2(0, -1385), new Vector2(940, 110), new Color(0f, 0f, 0f, 0.45f));
        RectTransform bonusCoin = CreateRect("Coin", bonusBack.transform);
        Anchor(bonusCoin, new Vector2(0f, 0.5f), new Vector2(60f, 14f), new Vector2(48f, 48f));
        AddImage(bonusCoin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text bonus = CreateText(bonusBack.transform, "Text", "SET BONUS  100  ·  4/6", 38, Gold, TextAlignmentOptions.MidlineLeft);
        bonus.fontSharedMaterial = OutlineFor(bonus);
        Anchor(bonus.rectTransform, new Vector2(0f, 0.5f), new Vector2(120f + 380f, 14f), new Vector2(760f, 52f));
        Image fill = Bar(bonusBack.transform, "Bar", new Vector2(30f, -28f), new Vector2(880f, 16f), Gold);

        UIButton close = InstantiateButton(ButtonVariant.Close, panel, Vector2.zero);
        Anchor((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0, 115), new Vector2(420, 140));

        var popup = root.AddComponent<ObjectivesPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: false);
        SetArray(popup, "_tabs", tabs);
        SetArray(popup, "_tabLabels", labels);
        SetArray(popup, "_tabFrames", frames);
        SetArray(popup, "_rows", rows);
        SetRef(popup, "_lockText", lockText);
        SetRef(popup, "_bonus", bonus);
        SetRef(popup, "_bonusFill", fill);
        SetRef(popup, "_closeButton", close);
        SavePrefab(root, ObjectivesPopupPath);
    }

    // The big moment, full screen: rays, the badges, the title, NEW RULE, the venue, the reward.
    private static void BuildCelebrationPopup()
    {
        RectTransform panel = CreatePopupShell("CelebrationPopup", new Vector2(1080, 1920), "", out GameObject root);
        Object.DestroyImmediate(panel.Find("Title").gameObject);
        panel.GetComponent<Image>().color = Color.clear;
        panel.GetComponent<Image>().raycastTarget = false;
        panel.Find("Face").gameObject.SetActive(false);
        root.transform.Find("Dim").GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 0.9f);
        var tapCatcher = root.transform.Find("Dim").gameObject.AddComponent<PointerRelay>();

        RectTransform rays = CreateRect("Rays", panel);
        Anchor(rays, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(10f, 10f));
        for (int i = 0; i < 16; i++)
        {
            RectTransform ray = CreateRect("Ray" + i, rays);
            ray.anchorMin = ray.anchorMax = new Vector2(0.5f, 0.5f);
            ray.pivot = new Vector2(0.5f, 0f);
            ray.sizeDelta = new Vector2(34f, 760f);
            ray.localRotation = Quaternion.Euler(0f, 0f, i * 22.5f);
            AddImage(ray.gameObject, LoadSprite(Generated + "/ui_white.png"), new Color(1f, 1f, 1f, 0.3f), false);
        }
        Image glow = AddImage(CreateRect("Glow", panel).gameObject, LoadSprite(Generated + "/ui_glow.png"), new Color(1f, 1f, 1f, 0.5f), false);
        Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1200f, 1200f));
        glow.rectTransform.SetSiblingIndex(rays.GetSiblingIndex() + 1);

        RankBadgeView oldBadge = BuildRankBadge(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), 1f, false, false, false);
        RankBadgeView newBadge = BuildRankBadge(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), 1f, false, false, false);
        RectTransform trophy = CreateRect("Trophy", panel);
        Anchor(trophy, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(360f, 360f));
        AddImage(trophy.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);

        TMP_Text title = CreateText(panel, "Title", "SILVER DIVISION", 88, Gold, TextAlignmentOptions.Center);
        title.fontSharedMaterial = OutlineFor(title);
        title.enableAutoSizing = true;
        title.fontSizeMin = 44f;
        title.fontSizeMax = 88f;
        title.textWrappingMode = TextWrappingModes.Normal;
        Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(960f, 200f));

        // NEW RULE.
        CanvasGroup ruleCard = Card(panel, "RuleCard", new Vector2(0f, -170f), new Vector2(900f, 190f), Hex("2A3B2F"));
        TMP_Text ruleHeader = CreateText(ruleCard.transform, "Header", "NEW RULE", 34, Gold, TextAlignmentOptions.MidlineLeft);
        ruleHeader.fontSharedMaterial = OutlineFor(ruleHeader);
        Anchor(ruleHeader.rectTransform, new Vector2(0f, 1f), new Vector2(200f, -34f), new Vector2(300f, 44f));
        Image ruleIcon = AddImage(CreateRect("Icon", ruleCard.transform).gameObject, LoadSprite(Generated + "/icon_pocket_any.png"), Color.white, false);
        Anchor(ruleIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(110f, 110f));
        TMP_Text ruleText = CreateText(ruleCard.transform, "Text", "Call your pocket for the 8-ball.", 38, Color.white, TextAlignmentOptions.TopLeft);
        ruleText.fontSharedMaterial = OutlineFor(ruleText);
        ruleText.textWrappingMode = TextWrappingModes.Normal;
        ruleText.rectTransform.anchorMin = ruleText.rectTransform.anchorMax = new Vector2(0f, 1f);
        ruleText.rectTransform.pivot = new Vector2(0f, 1f);
        ruleText.rectTransform.anchoredPosition = new Vector2(150f, -66f);
        ruleText.rectTransform.sizeDelta = new Vector2(720f, 110f);

        // The new venue.
        CanvasGroup venueCard = Card(panel, "VenueCard", new Vector2(0f, -390f), new Vector2(900f, 190f), Hex("2A3B2F"));
        RectTransform vMask = CreateRect("Mask", venueCard.transform);
        Anchor(vMask, new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(260f, 150f));
        vMask.gameObject.AddComponent<RectMask2D>();
        Image venueImage = AddImage(CreateRect("Image", vMask).gameObject, null, Color.white, false);
        venueImage.preserveAspect = false;
        Stretch(venueImage.rectTransform, Vector4.zero);
        TMP_Text venueHeader = CreateText(venueCard.transform, "Header", "NEW VENUE", 34, Gold, TextAlignmentOptions.MidlineLeft);
        venueHeader.fontSharedMaterial = OutlineFor(venueHeader);
        Anchor(venueHeader.rectTransform, new Vector2(0f, 0.5f), new Vector2(290f + 220f, 30f), new Vector2(440f, 44f));
        TMP_Text venueText = CreateText(venueCard.transform, "Text", "Downtown Hall", 44, Color.white, TextAlignmentOptions.MidlineLeft);
        venueText.fontSharedMaterial = OutlineFor(venueText);
        venueText.enableAutoSizing = true;
        venueText.fontSizeMin = 32f;
        venueText.fontSizeMax = 44f;
        Anchor(venueText.rectTransform, new Vector2(0f, 0.5f), new Vector2(290f + 280f, -30f), new Vector2(560f, 56f));

        // The reward counting up.
        CanvasGroup rewardRow = Card(panel, "RewardRow", new Vector2(0f, -580f), new Vector2(560f, 120f), Hex("3B3114"));
        RectTransform coin = CreateRect("Coin", rewardRow.transform);
        Anchor(coin, new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(84f, 84f));
        AddImage(coin.gameObject, LoadSprite(Generated + "/icon_coin.png"), Color.white, false);
        TMP_Text rewardText = CreateText(rewardRow.transform, "Text", "+150", 76, Gold, TextAlignmentOptions.MidlineLeft);
        rewardText.fontSharedMaterial = OutlineFor(rewardText);
        Anchor(rewardText.rectTransform, new Vector2(0f, 0.5f), new Vector2(150f + 190f, 0f), new Vector2(380f, 100f));

        TMP_Text tap = CreateText(panel, "TapHint", "TAP TO CONTINUE", 32, TextMuted, TextAlignmentOptions.Center);
        Anchor(tap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(800f, 44f));

        RectTransform confetti = CreateRect("Confetti", panel);
        Anchor(confetti, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), Vector2.zero);

        var popup = root.AddComponent<CelebrationPopup>();
        WirePopup(popup, closeOnBack: true, closeOnDimClick: false);
        SetRef(popup, "_rays", rays);
        SetRef(popup, "_glow", glow);
        SetRef(popup, "_oldBadge", oldBadge);
        SetRef(popup, "_newBadge", newBadge);
        SetRef(popup, "_trophy", trophy);
        SetRef(popup, "_title", title);
        SetRef(popup, "_ruleCard", ruleCard);
        SetRef(popup, "_ruleIcon", ruleIcon);
        SetRef(popup, "_ruleText", ruleText);
        SetRef(popup, "_venueCard", venueCard);
        SetRef(popup, "_venueImage", venueImage);
        SetRef(popup, "_venueText", venueText);
        SetRef(popup, "_rewardRow", rewardRow);
        SetRef(popup, "_rewardText", rewardText);
        SetRef(popup, "_tapHint", tap);
        SetRef(popup, "_tapCatcher", tapCatcher);
        SetRef(popup, "_confettiRoot", confetti);
        SetRef(popup, "_confettiSprite", LoadSprite(Generated + "/ui_white.png"));
        SetRef(popup, "_anyPocketIcon", LoadSprite(Generated + "/icon_pocket_any.png"));
        SetRef(popup, "_calledPocketIcon", LoadSprite(Generated + "/icon_pocket_called.png"));
        SetRef(popup, "_blockedPocketIcon", LoadSprite(Generated + "/icon_pocket_blocked.png"));
        SavePrefab(root, CelebrationPopupPath);
    }

    private static CanvasGroup Card(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        Image card = Panel(parent, name, new Vector2(0.5f, 0.5f), position, size, color);
        return card.gameObject.AddComponent<CanvasGroup>();
    }
}
