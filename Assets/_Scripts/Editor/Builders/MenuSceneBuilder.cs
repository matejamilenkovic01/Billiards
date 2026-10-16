using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuSceneBuilder
{
    private const string Generated = UIPrefabBuilder.Generated;

    [MenuItem("Tools/Billiards/Builders/Menu Scene")]
    public static void Build()
    {
        // Load assets only after NewScene: a Single-mode new scene unloads unused assets loaded before it.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);

        MatchSceneBuilder.ApplyRoomLighting();
        Camera camera = MatchSceneBuilder.BuildCamera(config);
        MatchSceneBuilder.BuildVolume();

        var table = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.TablePath));
        table.transform.position = new Vector3(0f, config.TableHeight, 0f);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabBuilder.RoomPath));

        var orbit = camera.gameObject.AddComponent<MenuTableOrbit>();
        UIPrefabBuilder.SetRef(orbit, "_table", table.GetComponent<TableView>());
        UIPrefabBuilder.SetRef(orbit, "_config", config);

        BuildUI();
        new GameObject("MenuController").AddComponent<MenuController>();

        EditorSceneManager.SaveScene(scene, ProjectSettingsBuilder.MenuScenePath);
        Debug.Log("MenuSceneBuilder: done");
    }

    private static void BuildUI()
    {
        Canvas canvas = MatchSceneBuilder.CreateSceneCanvas("MenuUI", 0, out RectTransform safe);

        // Darkens the 3D backdrop so the menu reads clearly.
        RectTransform dim = UIPrefabBuilder.CreateRect("Dim", canvas.transform);
        dim.SetAsFirstSibling();
        UIPrefabBuilder.Stretch(dim, Vector4.zero);
        UIPrefabBuilder.AddImage(dim.gameObject, null, new Color(0.02f, 0.03f, 0.025f, 0.5f), false);

        UIPrefabBuilder.InstantiateCoinCounter(safe, new Vector2(1f, 1f), new Vector2(-175f, -95f));

        RectTransform title = UIPrefabBuilder.CreateRect("Title", safe);
        UIPrefabBuilder.Anchor(title, new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(1000f, 320f));
        TMP_Text main = UIPrefabBuilder.CreateText(title, "Main", "BILLIARDS", 150, UIPrefabBuilder.Gold, TextAlignmentOptions.Center);
        main.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(main.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1000f, 180f));
        TMP_Text sub = UIPrefabBuilder.CreateText(title, "Sub", "8-BALL DUEL", 60, UIPrefabBuilder.TextLight, TextAlignmentOptions.Center);
        sub.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1000f, 80f));
        AddIdleMotion(title.gameObject, 0, 16f, 1.3f);

        BuildSelectedCue(safe);

        // The pulse lives on a wrapper so it never fights the button's own press scale tween.
        // Primary: play the computer (it's what earns coins). Secondary: two players on one phone.
        RectTransform playPulse = UIPrefabBuilder.CreateRect("PlayPulse", safe);
        UIPrefabBuilder.Anchor(playPulse, new Vector2(0.5f, 1f), new Vector2(0f, -1060f), Vector2.zero);
        AddIdleMotion(playPulse.gameObject, 1, 1.05f, 0.7f);
        UIButton play = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.Play, playPulse, Vector2.zero);
        Caption(safe, "PlayCaption", "VS COMPUTER  ·  WIN +10 COINS", -1182f);

        UIButton twoPlayers = UIPrefabBuilder.InstantiateButton(UIPrefabBuilder.ButtonVariant.TwoPlayers, safe, Vector2.zero);
        UIPrefabBuilder.Anchor((RectTransform)twoPlayers.transform, new Vector2(0.5f, 1f), new Vector2(0f, -1305f), new Vector2(580f, 150f));
        Caption(safe, "TwoPlayersCaption", "PASS & PLAY ON ONE PHONE  ·  JUST FOR FUN", -1408f);

        GameObject settingsObject = UIPrefabBuilder.CreateButtonInstance(safe, "SETTINGS", UIPrefabBuilder.Icons + "/icon_gear.png", new Vector2(400f, 130f), 40, UIPrefabBuilder.ButtonColor.Grey);
        UIPrefabBuilder.Anchor((RectTransform)settingsObject.transform, new Vector2(0.5f, 1f), new Vector2(-215f, -1565f), new Vector2(400f, 130f));
        GameObject cuesObject = UIPrefabBuilder.CreateButtonInstance(safe, "CUES", UIPrefabBuilder.Icons + "/icon_star.png", new Vector2(400f, 130f), 44, UIPrefabBuilder.ButtonColor.Yellow);
        UIPrefabBuilder.Anchor((RectTransform)cuesObject.transform, new Vector2(0.5f, 1f), new Vector2(215f, -1565f), new Vector2(400f, 130f));
        UIButton settings = settingsObject.GetComponent<UIButton>();
        UIButton cues = cuesObject.GetComponent<UIButton>();

        TMP_Text version = UIPrefabBuilder.CreateText(safe, "Version", "v0.0.0", 34, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.BottomRight);
        version.rectTransform.anchorMin = version.rectTransform.anchorMax = new Vector2(1f, 0f);
        version.rectTransform.pivot = new Vector2(1f, 0f);
        version.rectTransform.anchoredPosition = new Vector2(-32f, 26f);
        version.rectTransform.sizeDelta = new Vector2(300f, 50f);

        MatchSceneBuilder.BuildToast();

        var view = canvas.gameObject.AddComponent<MainMenuView>();
        UIPrefabBuilder.SetRef(view, "_playButton", play);
        UIPrefabBuilder.SetRef(view, "_twoPlayersButton", twoPlayers);
        UIPrefabBuilder.SetRef(view, "_settingsButton", settings);
        UIPrefabBuilder.SetRef(view, "_cuesButton", cues);
        UIPrefabBuilder.SetRef(view, "_versionText", version);
    }

    private static void BuildSelectedCue(RectTransform parent)
    {
        Image card = UIPrefabBuilder.Panel(parent, "SelectedCue", new Vector2(0.5f, 1f), new Vector2(0f, -800f), new Vector2(820f, 190f), new Color(0f, 0f, 0f, 0.5f));
        card.raycastTarget = true;

        TMP_Text caption = UIPrefabBuilder.CreateText(card.transform, "Caption", "YOUR CUE", 28, UIPrefabBuilder.TextMuted, TextAlignmentOptions.MidlineLeft);
        UIPrefabBuilder.Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(170f, -36f), new Vector2(300f, 40f));
        TMP_Text name = UIPrefabBuilder.CreateText(card.transform, "Name", "Rookie Cue", 46, UIPrefabBuilder.TextLight, TextAlignmentOptions.MidlineLeft);
        name.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -80f), new Vector2(560f, 60f));

        RectTransform cue = UIPrefabBuilder.CreateRect("Cue", card.transform);
        UIPrefabBuilder.Anchor(cue, new Vector2(0.5f, 0f), new Vector2(-70f, 45f), new Vector2(620f, 40f));
        Image cueImage = UIPrefabBuilder.AddImage(cue.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/cue_rookie.png"), Color.white, false);
        cueImage.preserveAspect = false;

        RectTransform bolt = UIPrefabBuilder.CreateRect("Bolt", card.transform);
        UIPrefabBuilder.Anchor(bolt, new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(54f, 54f));
        UIPrefabBuilder.AddImage(bolt.gameObject, UIPrefabBuilder.LoadSprite(Generated + "/icon_bolt.png"), Color.white, false);
        TMP_Text energy = UIPrefabBuilder.CreateText(card.transform, "Energy", "∞", 46, Color.white, TextAlignmentOptions.MidlineLeft);
        energy.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(energy.rectTransform, new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(120f, 60f));

        var view = card.gameObject.AddComponent<SelectedCueView>();
        UIPrefabBuilder.SetRef(view, "_cueImage", cueImage);
        UIPrefabBuilder.SetRef(view, "_name", name);
        UIPrefabBuilder.SetRef(view, "_energy", energy);
    }

    private static void Caption(RectTransform parent, string name, string text, float y)
    {
        TMP_Text caption = UIPrefabBuilder.CreateText(parent, name, text, 28, UIPrefabBuilder.TextMuted, TextAlignmentOptions.Center);
        caption.fontSharedMaterial = UIPrefabBuilder.OutlineMaterial;
        UIPrefabBuilder.Anchor(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1000f, 40f));
    }

    // mode: 0 = bob, 1 = pulse (IdleMotion.Mode)
    private static void AddIdleMotion(GameObject go, int mode, float amount, float halfPeriod)
    {
        var motion = go.AddComponent<IdleMotion>();
        var serialized = new SerializedObject(motion);
        serialized.FindProperty("_mode").enumValueIndex = mode;
        serialized.FindProperty("_amount").floatValue = amount;
        serialized.FindProperty("_halfPeriod").floatValue = halfPeriod;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
