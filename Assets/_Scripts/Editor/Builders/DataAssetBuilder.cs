using System.IO;
using UnityEditor;
using UnityEngine;

public static class DataAssetBuilder
{
    public const string Folder = "Assets/_ScriptableObjects";
    public const string CueCatalogPath = Folder + "/CueCatalog.asset";
    public const string MatchConfigPath = Folder + "/MatchConfig.asset";
    public const string BotProfilePath = Folder + "/BotProfile.asset";
    public const string VenueFolder = Folder + "/Venues";
    public static string VenuePath(string id) => $"{VenueFolder}/{id}.asset";

    public const string DivisionCatalogPath = Folder + "/DivisionCatalog.asset";
    public const string ObjectiveCatalogPath = Folder + "/ObjectiveCatalog.asset";
    public const string EconomyConfigPath = Folder + "/EconomyConfig.asset";
    public const string DivisionFolder = Folder + "/Divisions";
    public static string DivisionBotPath(string id) => $"{DivisionFolder}/Bot_{id}.asset";

    public struct CueSpec
    {
        public string Id;
        public string Name;
        public int Price;
        public float GreenHalf;
        public float YellowWidth;
        public float Period;
        public float GuideDiameters;
        public Color Shaft;
        public Color Butt;
        public Color Band;
        public float Glow;
    }

    // Source of truth for the catalog (implementation_definition_v3 V11); also drives the generated cue art.
    public static readonly CueSpec[] CueSpecs =
    {
        new() { Id = "house", Name = "House Cue", Price = 0,
                GreenHalf = 0.10f, YellowWidth = 0.30f, Period = 1.30f, GuideDiameters = 2f,
                Shaft = Hex("EBCFA2"), Butt = Hex("C99358"), Band = Hex("8A5A2B") },
        new() { Id = "club", Name = "Club Cue", Price = 300,
                GreenHalf = 0.12f, YellowWidth = 0.30f, Period = 1.40f, GuideDiameters = 3f,
                Shaft = Hex("E3C696"), Butt = Hex("4E2A17"), Band = Hex("C62828") },
        new() { Id = "pro", Name = "Pro Cue", Price = 900,
                GreenHalf = 0.14f, YellowWidth = 0.30f, Period = 1.55f, GuideDiameters = 4f,
                Shaft = Hex("E8D2A8"), Butt = Hex("151515"), Band = Hex("D4A62A") },
        new() { Id = "master", Name = "Master Cue", Price = 2200,
                GreenHalf = 0.16f, YellowWidth = 0.29f, Period = 1.70f, GuideDiameters = 6f,
                Shaft = Hex("E9D8B8"), Butt = Hex("1D4AA8"), Band = Hex("C9D1D9") },
        new() { Id = "champion", Name = "Champion Cue", Price = 5000,
                GreenHalf = 0.18f, YellowWidth = 0.28f, Period = 1.85f, GuideDiameters = 8f,
                Shaft = Hex("EAD6AE"), Butt = Hex("6E1423"), Band = Hex("D9A92E") },
        new() { Id = "legend", Name = "Legend Cue", Price = 10000,
                GreenHalf = 0.20f, YellowWidth = 0.27f, Period = 2.00f, GuideDiameters = 10f,
                Shaft = Hex("F1DEB0"), Butt = Hex("D9A92E"), Band = Hex("FFF0B0"), Glow = 0.55f }
    };

    public struct OpponentSpec
    {
        public string Id;
        public string Name;
        public string ShortName;
        public string Line;
        public float ThinkExtra;
        public bool FullBreak;

        public OpponentSpec(string id, string name, string shortName, string line, float thinkExtra = 0f, bool fullBreak = false)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            Line = line;
            ThinkExtra = thinkExtra;
            FullBreak = fullBreak;
        }
    }

    public struct DivisionSpec
    {
        public string Id;
        public string Name;
        public string VenueId;
        public string VenueName;
        public int StarsPerStage;
        public LossRule Loss;
        public int WinCoins;
        public string BotCueId;
        public float SigmaMs;
        public float MaxCut;
        public int Candidates;
        public Color Color;
        public OpponentSpec[] Opponents;
    }

    // v3 V10 starting values (ProgressionSim tunes them in commit 12; the asset keeps tuned values).
    // "Thinks slower" = +1.5 s, "thinks fast" = -0.5 s; Big Sal always breaks at full power.
    public static readonly DivisionSpec[] DivisionSpecs =
    {
        new() { Id = "bronze", Name = "Bronze", VenueId = "basement", VenueName = "Basement Bar", StarsPerStage = 2,
                Loss = LossRule.NoLoss, WinCoins = 20, BotCueId = "house", SigmaMs = 75f, MaxCut = 55f, Candidates = 2,
                Color = Hex("CD7F32"), Opponents = new[]
                {
                    new OpponentSpec("ray", "Rookie Ray", "Ray", "Just learned what a bridge is.", -0.5f),
                    new OpponentSpec("lin", "Lucky Lin", "Lin", "Swears every shot was planned."),
                    new OpponentSpec("sal", "Big Sal", "Sal", "Breaks like a freight train.", 0f, true)
                } },
        new() { Id = "silver", Name = "Silver", VenueId = "downtown", VenueName = "Downtown Hall", StarsPerStage = 3,
                Loss = LossRule.LoseStarKeepStage, WinCoins = 30, BotCueId = "club", SigmaMs = 62f, MaxCut = 65f, Candidates = 3,
                Color = Hex("C0C7D0"), Opponents = new[]
                {
                    new OpponentSpec("dina", "Dina Drift", "Dina", "Never in a hurry. Never.", 1.5f),
                    new OpponentSpec("marco", "Marco Mumbles", "Marco", "Talks to the balls. They listen."),
                    new OpponentSpec("hank", "Hustler Hank", "Hank", "Plays worse when there's no money on it.")
                } },
        new() { Id = "gold", Name = "Gold", VenueId = "riverside", VenueName = "Riverside Club", StarsPerStage = 3,
                Loss = LossRule.LoseStarDropStage, WinCoins = 45, BotCueId = "pro", SigmaMs = 54f, MaxCut = 75f, Candidates = 4,
                Color = Hex("E8B83A"), Opponents = new[]
                {
                    new OpponentSpec("ivy", "Ivy Bennett", "Ivy", "Club champion, three years running."),
                    new OpponentSpec("theo", "Theo Park", "Theo", "Calculates angles for fun.", 1.5f),
                    new OpponentSpec("duchess", "The Duchess", "Duchess", "Has never been seen to miss a straight one.")
                } },
        new() { Id = "platinum", Name = "Platinum", VenueId = "lounge", VenueName = "Grand Lounge", StarsPerStage = 3,
                Loss = LossRule.LoseStarDropStage, WinCoins = 65, BotCueId = "master", SigmaMs = 47f, MaxCut = 75f, Candidates = 5,
                Color = Hex("7FD6D0"), Opponents = new[]
                {
                    new OpponentSpec("viktor", "Viktor Kane", "Viktor", "Ice in his veins, chalk on his hands."),
                    new OpponentSpec("mei", "Mei Tanaka", "Mei", "Plays the table, not the player."),
                    new OpponentSpec("professor", "The Professor", "Professor", "Wrote the book. Literally.", 1.5f)
                } },
        new() { Id = "diamond", Name = "Diamond", VenueId = "royal", VenueName = "Royal Masters", StarsPerStage = 3,
                Loss = LossRule.LoseStarDropStage, WinCoins = 90, BotCueId = "champion", SigmaMs = 42f, MaxCut = 75f, Candidates = 5,
                Color = Hex("7DB7FF"), Opponents = new[]
                {
                    new OpponentSpec("sofia", "Sofia Reyes", "Sofia", "Never misses the same shot twice."),
                    new OpponentSpec("grant", "Grant Holloway", "Grant", "Old school. Slow, sure, deadly.", 1.5f),
                    new OpponentSpec("baron", "The Baron", "Baron", "Owns the table. Rents you a seat.")
                } }
    };

    // v3 V12: the ordered objective list (order = difficulty).
    private static ObjectiveDefinition Objective(string id, string text, ObjectiveKind kind, int target, int reward, float param = 0f) =>
        new() { Id = id, Text = text, Kind = kind, Target = target, Reward = reward, Param = param };

    public static ObjectiveDefinition[] ObjectiveSpecs() => new[]
    {
        Objective("win_1", "Win a match", ObjectiveKind.WinMatches, 1, 40),
        Objective("pocket_10", "Pocket 10 balls", ObjectiveKind.PocketBalls, 10, 40),
        Objective("perfect_5", "Release on PERFECT 5 times", ObjectiveKind.PerfectReleases, 5, 50),
        Objective("clean_win", "Win without a foul", ObjectiveKind.WinWithoutFoul, 1, 60),
        Objective("two_in_one", "Pot 2 balls in one shot", ObjectiveKind.TwoBallsOneShot, 1, 60),
        Objective("fine_aim_3", "Use FINE AIM and pot the ball (3 times)", ObjectiveKind.FineAimPots, 3, 50),
        Objective("perfect_row_3", "Get 3 PERFECT releases in a row", ObjectiveKind.PerfectStreak, 3, 70),
        Objective("reach_silver", "Reach Silver", ObjectiveKind.ReachDivision, 1, 80, 1f),
        Objective("long_pot", "Pot a ball from more than 1.5 m", ObjectiveKind.LongPot, 1, 80, 1.5f),
        Objective("win_row_2", "Win 2 matches in a row", ObjectiveKind.WinStreak, 2, 100),
        Objective("cut_45_5", "Pot a cut of 45° or more (5 times)", ObjectiveKind.CutPots, 5, 100, 45f),
        Objective("bank", "Bank shot: pot a ball after it hits a cushion", ObjectiveKind.BankShot, 1, 120),
        Objective("win_4_left", "Win with 4+ opponent balls still on the table", ObjectiveKind.WinWithOpponentLeft, 1, 140, 4f),
        Objective("perfect_50", "Release on PERFECT 50 times", ObjectiveKind.PerfectReleases, 50, 150),
        Objective("reach_gold", "Reach Gold", ObjectiveKind.ReachDivision, 1, 160, 2f),
        Objective("win_row_3", "Win 3 matches in a row", ObjectiveKind.WinStreak, 3, 180),
        Objective("run_out", "Clear your group in one visit (run out)", ObjectiveKind.RunOut, 1, 220),
        Objective("reach_platinum", "Reach Platinum", ObjectiveKind.ReachDivision, 1, 250, 3f),
        Objective("platinum_wins_10", "Win 10 matches in Platinum or higher", ObjectiveKind.WinsAtDivision, 10, 280, 3f),
        Objective("reach_diamond", "Reach Diamond", ObjectiveKind.ReachDivision, 1, 300, 4f)
    };

    // The cue catalog is always rebuilt from CueSpecs; MatchConfig, BotProfile, EconomyConfig and the division
    // and objective catalogs keep their tuned values unless resetTuning is true (or they are new).
    [MenuItem("Tools/Billiards/Builders/Data Assets")]
    public static void Build() => Build(false);

    public static void Build(bool resetTuning)
    {
        Directory.CreateDirectory(Folder);

        CueCatalog catalog = LoadOrCreate<CueCatalog>(CueCatalogPath, false);
        catalog.Cues.Clear();
        for (int i = 0; i < CueSpecs.Length; i++)
        {
            CueSpec spec = CueSpecs[i];
            catalog.Cues.Add(new CueDefinition
            {
                Id = spec.Id,
                Name = spec.Name,
                Price = spec.Price,
                GreenHalf = spec.GreenHalf,
                YellowWidth = spec.YellowWidth,
                OscillationPeriod = spec.Period,
                ObjectGuideDiameters = spec.GuideDiameters,
                ShaftColor = spec.Shaft,
                ButtColor = spec.Butt,
                BandColor = spec.Band,
                Glow = spec.Glow,
                Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedArtBuilder.CueSpritePath(spec.Id)),
                Material = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.CueMaterialPath(i + 1))
            });
        }
        EditorUtility.SetDirty(catalog);

        LoadOrCreate<MatchConfig>(MatchConfigPath, resetTuning);
        BotProfile baseBot = LoadOrCreate<BotProfile>(BotProfilePath, resetTuning);
        LoadOrCreate<EconomyConfig>(EconomyConfigPath, resetTuning);
        BuildVenues(resetTuning);
        BuildDivisions(baseBot, resetTuning);
        BuildObjectives(resetTuning);

        AssetDatabase.SaveAssets();
        Debug.Log("DataAssetBuilder: done");
    }

    // v3 V10: one BotProfile per division (the base profile with the division's sigma, shot choice, cue and the
    // faster v3 turn timing), and the catalog. Tuned numbers stay; ids, names, lines and references are set on every build.
    private static void BuildDivisions(BotProfile baseBot, bool resetTuning)
    {
        Directory.CreateDirectory(DivisionFolder);
        bool created = AssetDatabase.LoadAssetAtPath<DivisionCatalog>(DivisionCatalogPath) == null;
        DivisionCatalog catalog = LoadOrCreate<DivisionCatalog>(DivisionCatalogPath, resetTuning);
        bool fresh = created || resetTuning || catalog.Divisions.Count != DivisionSpecs.Length;
        if (fresh)
            catalog.Divisions.Clear();

        for (int i = 0; i < DivisionSpecs.Length; i++)
        {
            DivisionSpec spec = DivisionSpecs[i];
            string botPath = DivisionBotPath(spec.Id);
            bool botCreated = AssetDatabase.LoadAssetAtPath<BotProfile>(botPath) == null;
            BotProfile bot = LoadOrCreate<BotProfile>(botPath, false);
            if (botCreated || resetTuning)
            {
                EditorUtility.CopySerialized(baseBot, bot);
                bot.name = "Bot_" + spec.Id;
                bot.TimingSigmaMs = spec.SigmaMs;
                bot.MaxCutAngle = spec.MaxCut;
                bot.CandidatesToSimulate = spec.Candidates;
                bot.ThinkDuration = 2.0f;
                bot.ThinkJitter = 0.5f;
                bot.AimEnd = 3.8f;
                bot.PullEnd = 5.2f;
                bot.StrikeAt = 5.6f;
            }
            // The bot plays the division's cue (v3 V10), always from the catalog.
            CueSpec cue = System.Array.Find(CueSpecs, c => c.Id == spec.BotCueId);
            bot.GreenHalf = cue.GreenHalf;
            bot.YellowWidth = cue.YellowWidth;
            bot.Period = cue.Period;
            EditorUtility.SetDirty(bot);

            if (fresh)
                catalog.Divisions.Add(new DivisionDefinition
                {
                    StarsPerStage = spec.StarsPerStage,
                    Loss = spec.Loss,
                    WinCoins = spec.WinCoins
                });
            DivisionDefinition division = catalog.Divisions[i];
            division.Id = spec.Id;
            division.Name = spec.Name;
            division.VenueId = spec.VenueId;
            division.VenueName = spec.VenueName;
            division.Venue = AssetDatabase.LoadAssetAtPath<VenueTheme>(VenuePath(spec.VenueId));
            division.BotCueId = spec.BotCueId;
            division.Bot = bot;
            division.Color = spec.Color;
            division.Opponents.Clear();
            foreach (OpponentSpec opponent in spec.Opponents)
                division.Opponents.Add(new OpponentDefinition
                {
                    Id = opponent.Id,
                    Name = opponent.Name,
                    ShortName = opponent.ShortName,
                    Line = opponent.Line,
                    ThinkExtra = opponent.ThinkExtra,
                    FullBreak = opponent.FullBreak,
                    Portrait = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedArtBuilder.PortraitPath(opponent.Id)),
                    GeneratedPortrait = !File.Exists(GeneratedArtBuilder.CustomPortraitPath(opponent.Id))
                });
        }
        EditorUtility.SetDirty(catalog);
    }

    // Texts, kinds and order always follow ObjectiveSpecs; tuned targets and rewards stay unless reset.
    private static void BuildObjectives(bool resetTuning)
    {
        ObjectiveCatalog catalog = LoadOrCreate<ObjectiveCatalog>(ObjectiveCatalogPath, resetTuning);
        ObjectiveDefinition[] specs = ObjectiveSpecs();
        foreach (ObjectiveDefinition spec in specs)
        {
            ObjectiveDefinition tuned = resetTuning ? null : catalog.Get(spec.Id);
            if (tuned != null)
            {
                spec.Target = tuned.Target;
                spec.Reward = tuned.Reward;
                spec.Param = tuned.Param;
            }
        }
        catalog.Objectives.Clear();
        catalog.Objectives.AddRange(specs);
        EditorUtility.SetDirty(catalog);
    }

    public struct VenueSpec
    {
        public string Id;
        public string Name;
        public Color Felt;
        public string Cap;              // material: Brass or Chrome
        public Color Light;
        public float LightIntensity;
        public Color Ambient;
    }

    // Commit 9 (meta design venue table): cloth, caps and light per venue; every one keeps the TV-tournament light
    // over the table. Riverside's numbers are the V6 tuning.
    public static readonly VenueSpec[] VenueSpecs =
    {
        new() { Id = "basement", Name = "Basement Bar", Felt = Hex("2C6A3A"), Cap = "Brass", Light = new Color(1f, 0.86f, 0.7f), LightIntensity = 30f, Ambient = new Color(0.95f, 0.85f, 0.74f) },
        new() { Id = "downtown", Name = "Downtown Hall", Felt = Hex("2A5D9E"), Cap = "Chrome", Light = new Color(0.96f, 0.97f, 1f), LightIntensity = 32f, Ambient = new Color(0.9f, 0.96f, 1.05f) },
        new() { Id = "riverside", Name = "Riverside Club", Felt = Hex("1F6B45"), Cap = "Brass", Light = new Color(1f, 0.9f, 0.8f), LightIntensity = 32f, Ambient = new Color(1.1f, 1.0f, 0.88f) },
        new() { Id = "lounge", Name = "Grand Lounge", Felt = Hex("7A1E2C"), Cap = "Brass", Light = new Color(1f, 0.9f, 0.74f), LightIntensity = 34f, Ambient = new Color(1.05f, 0.94f, 0.82f) },
        new() { Id = "royal", Name = "Royal Masters", Felt = Hex("3A2F78"), Cap = "Chrome", Light = new Color(1f, 0.98f, 0.95f), LightIntensity = 36f, Ambient = new Color(0.92f, 0.92f, 1.02f) }
    };

    public static string VenueThumbnailPath(string id) => $"{GeneratedArtBuilder.ArtFolder}/venue_{id}.png";

    // Five themes; tuned numbers stay (only new or reset themes take the spec), references are set on every build.
    // The reflection cubemap is set by MatchSceneBuilder when it bakes.
    private static void BuildVenues(bool resetTuning)
    {
        Directory.CreateDirectory(VenueFolder);
        foreach (VenueSpec spec in VenueSpecs)
        {
            bool created = AssetDatabase.LoadAssetAtPath<VenueTheme>(VenuePath(spec.Id)) == null;
            VenueTheme theme = LoadOrCreate<VenueTheme>(VenuePath(spec.Id), resetTuning);
            if (created || resetTuning)
            {
                theme.FeltColor = spec.Felt;
                theme.TableLightColor = spec.Light;
                theme.TableLightIntensity = spec.LightIntensity;
                theme.AmbientColor = spec.Ambient;
            }
            theme.Id = spec.Id;
            theme.DisplayName = spec.Name;
            theme.WoodMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath("Wood"));
            theme.CapMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath(spec.Cap));
            theme.Thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(VenueThumbnailPath(spec.Id));
            EditorUtility.SetDirty(theme);
        }
    }

    private static T LoadOrCreate<T>(string path, bool reset) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        else if (reset)
        {
            // Back to the field initialisers in code, keeping the asset's GUID.
            T defaults = ScriptableObject.CreateInstance<T>();
            string name = asset.name;
            EditorUtility.CopySerialized(defaults, asset);
            asset.name = name;
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(asset);
        }
        return asset;
    }

    private static Color Hex(string hex) => GeneratedArtBuilder.Hex(hex);
}
