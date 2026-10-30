using System.IO;
using UnityEditor;
using UnityEngine;

public static class DataAssetBuilder
{
    public const string Folder = "Assets/_ScriptableObjects";
    public const string CueCatalogPath = Folder + "/CueCatalog.asset";
    public const string PracticeConfigPath = Folder + "/PracticeConfig.asset";
    public const string MatchConfigPath = Folder + "/MatchConfig.asset";
    public const string BotProfilePath = Folder + "/BotProfile.asset";
    public const string VenueFolder = Folder + "/Venues";
    public static string VenuePath(string id) => $"{VenueFolder}/{id}.asset";

    public const string DivisionCatalogPath = Folder + "/DivisionCatalog.asset";
    public const string ObjectiveCatalogPath = Folder + "/ObjectiveCatalog.asset";
    public const string EconomyConfigPath = Folder + "/EconomyConfig.asset";
    public const string TutorialConfigPath = Folder + "/TutorialConfig.asset";
    public const string DivisionFolder = Folder + "/Divisions";
    public static string DivisionBotPath(string id) => $"{DivisionFolder}/Bot_{id}.asset";

    // How the stick is painted (GeneratedArtBuilder): the material family and the decoration on the butt.
    public enum CueKind { Wood, Lacquer, Carbon, Metal }
    public enum CueDecor { None, Rings, Diamonds, Weave, GoldRings, GlowLines }

    public struct CueSpec
    {
        public string Id;
        public string Name;
        public int Price;
        public float GreenHalf;
        public float YellowWidth;
        public float Period;
        public float GuideDiameters;
        public float MaxSpeed;
        public Color Shaft;
        public Color Butt;
        public Color Band;
        public float Glow;
        // v4 V9
        public CueProfile Profile;
        public CueKind Kind;
        public CueDecor Decor;
        public Color Wrap;
        public Color Tip;
        public Color Ferrule;
        public float Pulse;
        public PerfectEffect Perfect;
    }

    private static CueProfile Profile(float scale, float taper, float joint, float wrapStart, float wrapEnd, float wrapBump, CueButtShape butt) => new()
    {
        ButtScale = scale, TaperExponent = taper, JointAt = joint, WrapStart = wrapStart, WrapEnd = wrapEnd, WrapBump = wrapBump, Butt = butt
    };

    private static PerfectEffect Effect(PerfectShape shape, string color, string accent, int count, float speed, float size, float flash = 0f) => new()
    {
        Shape = shape, Color = Hex(color), Accent = Hex(accent), Count = count, Speed = speed, Size = size, CueFlash = flash
    };

    // Source of truth for the catalog (implementation_definition_v3 V11, POWER = MaxSpeed and the looks from v4 V9);
    // also drives the generated cue art. Adjacent cues differ in at least three silhouette numbers (CueChecks).
    // Prices tuned in commit 12 with ProgressionSim (Club 300 -> 400, Pro 900 -> 1000, Master 2200 -> 1700).
    public static readonly CueSpec[] CueSpecs =
    {
        // Light maple, straight cone, no decoration, blue chalk.
        new() { Id = "house", Name = "House Cue", Price = 0,
                GreenHalf = 0.10f, YellowWidth = 0.30f, Period = 1.30f, GuideDiameters = 2f, MaxSpeed = 4.5f,
                Shaft = Hex("EBCFA2"), Butt = Hex("D8A966"), Band = Hex("8A5A2B"),
                Profile = Profile(1.00f, 1.00f, 0.50f, 0.30f, 0.42f, 0f, CueButtShape.Flat),
                Kind = CueKind.Wood, Decor = CueDecor.None, Wrap = Hex("D8A966"), Tip = Hex("4A6FA5"), Ferrule = Hex("F3EEDF"),
                Perfect = Effect(PerfectShape.Puff, "F4F7FF", "D7E3FF", 12, 0.14f, 0.013f) },
        // Dark walnut, red linen wrap, two red rings.
        new() { Id = "club", Name = "Club Cue", Price = 400,
                GreenHalf = 0.12f, YellowWidth = 0.30f, Period = 1.40f, GuideDiameters = 3f, MaxSpeed = 5.0f,
                Shaft = Hex("E3C696"), Butt = Hex("4E2A17"), Band = Hex("C62828"),
                Profile = Profile(1.03f, 0.85f, 0.46f, 0.14f, 0.34f, 0.05f, CueButtShape.Flat),
                Kind = CueKind.Wood, Decor = CueDecor.Rings, Wrap = Hex("B02323"), Tip = Hex("4A6FA5"), Ferrule = Hex("F3EEDF"),
                Perfect = Effect(PerfectShape.Sparks, "FF5A4A", "FFB199", 16, 0.36f, 0.0085f) },
        // Ebony, gold diamond inlays, leather wrap, rounded butt.
        new() { Id = "pro", Name = "Pro Cue", Price = 1000,
                GreenHalf = 0.14f, YellowWidth = 0.30f, Period = 1.55f, GuideDiameters = 4f, MaxSpeed = 5.4f,
                Shaft = Hex("E8D2A8"), Butt = Hex("151515"), Band = Hex("D4A62A"),
                Profile = Profile(1.07f, 0.70f, 0.52f, 0.18f, 0.32f, 0.03f, CueButtShape.Rounded),
                Kind = CueKind.Lacquer, Decor = CueDecor.Diamonds, Wrap = Hex("5A3B22"), Tip = Hex("4A6FA5"), Ferrule = Hex("F3EEDF"),
                Perfect = Effect(PerfectShape.Sparks, "FFD54F", "FFF2B0", 20, 0.26f, 0.0095f) },
        // Blue carbon weave, silver metal joint and cap.
        new() { Id = "master", Name = "Master Cue", Price = 1700,
                GreenHalf = 0.16f, YellowWidth = 0.29f, Period = 1.70f, GuideDiameters = 6f, MaxSpeed = 5.8f,
                Shaft = Hex("E9D8B8"), Butt = Hex("1D4AA8"), Band = Hex("C9D1D9"),
                Profile = Profile(1.00f, 0.95f, 0.44f, 0.30f, 0.42f, 0f, CueButtShape.Flat),
                Kind = CueKind.Carbon, Decor = CueDecor.Weave, Wrap = Hex("1D4AA8"), Tip = Hex("4A6FA5"), Ferrule = Hex("F3EEDF"),
                Perfect = Effect(PerfectShape.Puff, "7CC4FF", "C8E6FF", 18, 0.18f, 0.017f) },
        // Burgundy lacquer, gold rings, a thicker butt with a steel ring.
        new() { Id = "champion", Name = "Champion Cue", Price = 5000,
                GreenHalf = 0.18f, YellowWidth = 0.28f, Period = 1.85f, GuideDiameters = 8f, MaxSpeed = 6.2f,
                Shaft = Hex("EAD6AE"), Butt = Hex("6E1423"), Band = Hex("D9A92E"),
                Profile = Profile(1.18f, 0.60f, 0.50f, 0.16f, 0.30f, 0.04f, CueButtShape.MetalRing),
                Kind = CueKind.Lacquer, Decor = CueDecor.GoldRings, Wrap = Hex("5A0F1C"), Tip = Hex("4A6FA5"), Ferrule = Hex("F3EEDF"),
                Perfect = Effect(PerfectShape.Sparks, "B3223D", "FFC94A", 28, 0.30f, 0.0095f) },
        // Gold metal with glowing lines, black tip.
        new() { Id = "legend", Name = "Legend Cue", Price = 10000,
                GreenHalf = 0.20f, YellowWidth = 0.27f, Period = 2.00f, GuideDiameters = 10f, MaxSpeed = 6.5f,
                Shaft = Hex("E7C46A"), Butt = Hex("D9A92E"), Band = Hex("FFF0B0"), Glow = 0.55f,
                Profile = Profile(1.08f, 0.80f, 0.47f, 0.30f, 0.42f, 0f, CueButtShape.Rounded),
                Kind = CueKind.Metal, Decor = CueDecor.GlowLines, Wrap = Hex("D9A92E"), Tip = Hex("141414"), Ferrule = Hex("1B1B1B"), Pulse = 0.35f,
                Perfect = Effect(PerfectShape.Ring, "FFD966", "FFF6C9", 40, 0.5f, 0.012f, 1.2f) }
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
        public int LossCoins;
        public int StageReward;
        public int DivisionReward;
        public string BotCueId;
        public float SigmaMs;
        public float MaxCut;
        public int Candidates;
        public Color Color;
        public OpponentSpec[] Opponents;
        public RuleSetId Rules;
        public string RuleText;
    }

    // v4 V4: Grand Champion plays Diamond's tables with its own rule.
    public const RuleSetId ChampionRules = RuleSetId.CallAllOpponentEight;
    public const string ChampionRuleText = "Call every shot. Your opponent picks your 8-ball pocket.";

    // v3 V10 values; bot sigma tuned in commit 12 (DivisionBalanceCheck: the average player on the division's target).
    // "Thinks slower" = +1.5 s, "thinks fast" = -0.5 s; Big Sal always breaks at full power.
    public static readonly DivisionSpec[] DivisionSpecs =
    {
        new() { Id = "bronze", Rules = RuleSetId.Standard, RuleText = "Pot the 8 in any pocket.", Name = "Bronze", VenueId = "basement", VenueName = "Basement Bar", StarsPerStage = 2,
                Loss = LossRule.NoLoss, WinCoins = 20, LossCoins = 5, StageReward = 50, DivisionReward = 0, BotCueId = "house", SigmaMs = 50f, MaxCut = 55f, Candidates = 2,
                Color = Hex("CD7F32"), Opponents = new[]
                {
                    new OpponentSpec("ray", "Rookie Ray", "Ray", "Just learned what a bridge is.", -0.5f),
                    new OpponentSpec("lin", "Lucky Lin", "Lin", "Swears every shot was planned."),
                    new OpponentSpec("sal", "Big Sal", "Sal", "Breaks like a freight train.", 0f, true)
                } },
        new() { Id = "silver", Rules = RuleSetId.CallEight, RuleText = "Call your pocket for the 8-ball.", Name = "Silver", VenueId = "downtown", VenueName = "Downtown Hall", StarsPerStage = 3,
                Loss = LossRule.LoseStarKeepStage, WinCoins = 30, LossCoins = 8, StageReward = 75, DivisionReward = 150, BotCueId = "club", SigmaMs = 48f, MaxCut = 65f, Candidates = 3,
                Color = Hex("C0C7D0"), Opponents = new[]
                {
                    new OpponentSpec("dina", "Dina Drift", "Dina", "Never in a hurry. Never.", 1.5f),
                    new OpponentSpec("marco", "Marco Mumbles", "Marco", "Talks to the balls. They listen."),
                    new OpponentSpec("hank", "Hustler Hank", "Hank", "Plays worse when there's no money on it.")
                } },
        new() { Id = "gold", Rules = RuleSetId.BlockedPocket, RuleText = "Your opponent blocks one pocket for your 8-ball.", Name = "Gold", VenueId = "riverside", VenueName = "Riverside Club", StarsPerStage = 3,
                Loss = LossRule.LoseStarDropStage, WinCoins = 45, LossCoins = 12, StageReward = 100, DivisionReward = 300, BotCueId = "pro", SigmaMs = 55f, MaxCut = 75f, Candidates = 4,
                Color = Hex("E8B83A"), Opponents = new[]
                {
                    new OpponentSpec("ivy", "Ivy Bennett", "Ivy", "Club champion, three years running."),
                    new OpponentSpec("theo", "Theo Park", "Theo", "Calculates angles for fun.", 1.5f),
                    new OpponentSpec("duchess", "The Duchess", "Duchess", "Has never been seen to miss a straight one.")
                } },
        new() { Id = "platinum", Rules = RuleSetId.OpponentCallsEight, RuleText = "Your opponent picks the pocket for your 8-ball.", Name = "Platinum", VenueId = "lounge", VenueName = "Grand Lounge", StarsPerStage = 3,
                Loss = LossRule.LoseStarDropStage, WinCoins = 65, LossCoins = 16, StageReward = 150, DivisionReward = 500, BotCueId = "master", SigmaMs = 50f, MaxCut = 75f, Candidates = 5,
                Color = Hex("7FD6D0"), Opponents = new[]
                {
                    new OpponentSpec("viktor", "Viktor Kane", "Viktor", "Ice in his veins, chalk on his hands."),
                    new OpponentSpec("mei", "Mei Tanaka", "Mei", "Plays the table, not the player."),
                    new OpponentSpec("professor", "The Professor", "Professor", "Wrote the book. Literally.", 1.5f)
                } },
        new() { Id = "diamond", Rules = RuleSetId.CallAll, RuleText = "Call your pocket on every shot \u2014 the 8 too.", Name = "Diamond", VenueId = "royal", VenueName = "Royal Masters", StarsPerStage = 3,
                Loss = LossRule.LoseStarDropStage, WinCoins = 90, LossCoins = 20, StageReward = 200, DivisionReward = 800, BotCueId = "champion", SigmaMs = 56f, MaxCut = 75f, Candidates = 5,
                Color = Hex("7DB7FF"), Opponents = new[]
                {
                    new OpponentSpec("sofia", "Sofia Reyes", "Sofia", "Never misses the same shot twice."),
                    new OpponentSpec("grant", "Grant Holloway", "Grant", "Old school. Slow, sure, deadly.", 1.5f),
                    new OpponentSpec("baron", "The Baron", "Baron", "Owns the table. Rents you a seat.")
                } }
    };

    // v4 V12: the objective sets, Bronze first (order in a set = difficulty). Skill objectives pay in a loss too.
    private static ObjectiveDefinition Objective(string id, string text, ObjectiveKind kind, int target, int reward, float param = 0f) =>
        new() { Id = id, Text = text, Kind = kind, Target = target, Reward = reward, Param = param };

    private static ObjectiveSet Set(string divisionId, int bonus, params ObjectiveDefinition[] objectives)
    {
        var set = new ObjectiveSet { DivisionId = divisionId, Bonus = bonus };
        set.Objectives.AddRange(objectives);
        return set;
    }

    public static ObjectiveSet[] ObjectiveSpecs() => new[]
    {
        Set("bronze", 100,
            Objective("bronze_pocket_8", "Pocket 8 balls", ObjectiveKind.PocketBalls, 8, 40),
            Objective("bronze_perfect_5", "Release on PERFECT 5 times", ObjectiveKind.PerfectReleases, 5, 40),
            Objective("bronze_win", "Win a match", ObjectiveKind.WinMatches, 1, 50),
            Objective("bronze_fine_aim", "Use FINE AIM and pot (3 times)", ObjectiveKind.FineAimPots, 3, 40),
            Objective("bronze_two_balls", "Pot 2 balls in one shot", ObjectiveKind.TwoBallsOneShot, 1, 60),
            Objective("bronze_clean_win", "Win without a foul", ObjectiveKind.WinWithoutFoul, 1, 60)),
        Set("silver", 150,
            Objective("silver_draw", "Draw the cue ball back 30 cm+ (5 times)", ObjectiveKind.DrawShots, 5, 70, 0.3f),
            Objective("silver_follow", "Follow through and pot (5 times)", ObjectiveKind.FollowPots, 5, 60),
            Objective("silver_pocket_20", "Pocket 20 balls in Silver", ObjectiveKind.PocketBallsInDivision, 20, 70, 1f),
            Objective("silver_called_8", "Win by potting the 8 in your called pocket (2 times)", ObjectiveKind.CalledEightWins, 2, 90),
            Objective("silver_perfect_row", "3 PERFECT releases in a row", ObjectiveKind.PerfectStreak, 3, 70),
            Objective("silver_win_row", "Win 2 matches in a row", ObjectiveKind.WinStreak, 2, 100)),
        Set("gold", 250,
            Objective("gold_cut_45", "Pot a cut of 45° or more (5 times)", ObjectiveKind.CutPots, 5, 100, 45f),
            Objective("gold_stun", "Stun: pot straight and stop the cue ball within 10 cm (3 times)", ObjectiveKind.StunPots, 3, 100, 0.1f),
            Objective("gold_pocket_30", "Pocket 30 balls in Gold", ObjectiveKind.PocketBallsInDivision, 30, 90, 2f),
            Objective("gold_long", "Pot a ball from more than 1.5 m (3 times)", ObjectiveKind.LongPot, 3, 100, 1.5f),
            Objective("gold_win_3", "Win 3 matches in Gold", ObjectiveKind.WinsAtDivision, 3, 150, 2f),
            Objective("gold_run_out", "Run out: clear your group in one visit", ObjectiveKind.RunOut, 1, 200)),
        Set("platinum", 400,
            Objective("platinum_side", "Pot with side spin (5 times)", ObjectiveKind.SideSpinPots, 5, 140),
            Objective("platinum_bank", "Bank shot: pot after the ball hits a cushion (2 times)", ObjectiveKind.BankShot, 2, 160),
            Objective("platinum_perfect_50", "Release on PERFECT 50 times in Platinum", ObjectiveKind.PerfectReleasesInDivision, 50, 150, 3f),
            Objective("platinum_visit_3", "Pot 3 balls in one visit (5 times)", ObjectiveKind.VisitPots, 5, 150, 3f),
            Objective("platinum_left_4", "Win with 4+ opponent balls left", ObjectiveKind.WinWithOpponentLeft, 1, 200, 4f),
            Objective("platinum_win_5", "Win 5 matches in Platinum", ObjectiveKind.WinsAtDivision, 5, 250, 3f)),
        Set("diamond", 600,
            Objective("diamond_called_40", "Pocket 40 balls in called pockets", ObjectiveKind.CalledPots, 40, 200),
            Objective("diamond_draw_60", "Draw the cue ball back 60 cm+ (5 times)", ObjectiveKind.DrawShots, 5, 200, 0.6f),
            Objective("diamond_visit_3", "Pot 3 balls in one visit (10 times)", ObjectiveKind.VisitPots, 10, 200, 3f),
            Objective("diamond_run_twice", "Run out twice", ObjectiveKind.RunOut, 2, 300),
            Objective("diamond_win_row", "Win 3 in a row in Diamond", ObjectiveKind.WinStreakInDivision, 3, 350, 4f),
            Objective("diamond_reach_gc", "Reach Grand Champion", ObjectiveKind.ReachDivision, 1, 400, 5f))
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
                MaxSpeed = spec.MaxSpeed,
                ShaftColor = spec.Shaft,
                ButtColor = spec.Butt,
                BandColor = spec.Band,
                Glow = spec.Glow,
                Profile = spec.Profile,
                Perfect = spec.Perfect,
                EmissionPulse = spec.Pulse,
                Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TablePrefabBuilder.CueMeshPath(spec.Id)),
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
        BuildTutorial(resetTuning);
        BuildPractice(resetTuning);

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
            bot.MaxSpeed = cue.MaxSpeed;
            // v4 V1: bots from Gold up choose spin (follow / draw / stun); Bronze and Silver hit the centre.
            bot.UsesSpin = i >= 2;
            EditorUtility.SetDirty(bot);

            if (fresh)
                catalog.Divisions.Add(new DivisionDefinition
                {
                    StarsPerStage = spec.StarsPerStage,
                    Loss = spec.Loss,
                    WinCoins = spec.WinCoins
                });
            // v4 V11/V12: what a loss pays and what stages and divisions pay (spec-driven; tuned in commit 12).
            DivisionDefinition division = catalog.Divisions[i];
            division.LossCoins = spec.LossCoins;
            division.StageReward = spec.StageReward;
            division.DivisionReward = spec.DivisionReward;
            division.Id = spec.Id;
            division.Name = spec.Name;
            division.VenueId = spec.VenueId;
            division.VenueName = spec.VenueName;
            division.Venue = AssetDatabase.LoadAssetAtPath<VenueTheme>(VenuePath(spec.VenueId));
            division.BotCueId = spec.BotCueId;
            division.Bot = bot;
            division.Color = spec.Color;
            division.Rules = spec.Rules;
            division.RuleText = spec.RuleText;
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
        catalog.ChampionRules = ChampionRules;
        catalog.ChampionRuleText = ChampionRuleText;
        catalog.ChampionLossCoins = 25;
        catalog.ChampionEntryReward = 1000;
        catalog.ChampionMilestoneStars = 10;
        catalog.ChampionMilestoneReward = 300;
        EditorUtility.SetDirty(catalog);
    }

    // Texts, kinds and order always follow ObjectiveSpecs; tuned targets, rewards and bonuses stay unless reset.
    private static void BuildObjectives(bool resetTuning)
    {
        ObjectiveCatalog catalog = LoadOrCreate<ObjectiveCatalog>(ObjectiveCatalogPath, resetTuning);
        ObjectiveSet[] specs = ObjectiveSpecs();
        foreach (ObjectiveSet spec in specs)
        {
            ObjectiveSet tunedSet = resetTuning ? null : catalog.Sets.Find(x => x.DivisionId == spec.DivisionId);
            if (tunedSet != null)
                spec.Bonus = tunedSet.Bonus;
            foreach (ObjectiveDefinition objective in spec.Objectives)
            {
                ObjectiveDefinition tuned = resetTuning ? null : catalog.Get(objective.Id);
                if (tuned != null)
                {
                    objective.Target = tuned.Target;
                    objective.Reward = tuned.Reward;
                    objective.Param = tuned.Param;
                }
            }
        }
        catalog.Sets.Clear();
        catalog.Sets.AddRange(specs);
        EditorUtility.SetDirty(catalog);
    }

    // v3 V9: the five lessons (texts, tables, bar help). The "straight" shots are cut a few degrees so a rolling cue
    // ball doesn't follow the object ball in. Tuned only in the asset; rebuilt when new or reset.
    // v4 V9: the three practice shots of TRY.
    private static void BuildPractice(bool resetTuning)
    {
        bool created = AssetDatabase.LoadAssetAtPath<PracticeConfig>(PracticeConfigPath) == null;
        PracticeConfig config = LoadOrCreate<PracticeConfig>(PracticeConfigPath, resetTuning);
        if (!created && !resetTuning && config.Steps.Count == 3)
            return;
        config.Steps.Clear();
        config.Steps.Add(new TutorialStep
        {
            Kind = TutorialStepKind.PowerControl, Title = "LONG STRAIGHT",
            Instruction = "Feel the bar: see how wide the green stays at full power. Pull to HARD.",
            Pocket = 5, ObjectDistance = 0.5f, CutDegrees = 0f, CueDistance = 1.2f
        });
        config.Steps.Add(new TutorialStep
        {
            Kind = TutorialStepKind.Aim, Title = "CUT",
            Instruction = "See how far the aim line reaches.",
            Pocket = 4, ObjectDistance = 0.5f, CutDegrees = 30f, CueDistance = 1.2f
        });
        config.Steps.Add(new TutorialStep
        {
            Kind = TutorialStepKind.BallInHand, Title = "BREAK",
            Instruction = "Place the cue ball behind the line, then break. Feel the power.",
            Break = true, BallInHand = true
        });
        EditorUtility.SetDirty(config);
    }

    private static void BuildTutorial(bool resetTuning)
    {
        bool created = AssetDatabase.LoadAssetAtPath<TutorialConfig>(TutorialConfigPath) == null;
        TutorialConfig config = LoadOrCreate<TutorialConfig>(TutorialConfigPath, resetTuning);
        if (!created && !resetTuning && config.Steps.Count > 0)
            return;
        config.Steps.Clear();
        config.Steps.Add(new TutorialStep
        {
            Kind = TutorialStepKind.Power, Title = "POWER",
            Instruction = "Grab the cue at the top and drag it DOWN. Further = harder. Let go to shoot.",
            Success = "In! That's the power.", Pocket = 5, ObjectDistance = 0.45f, CutDegrees = 8f, CueDistance = 0.55f,
            Assist = ReleaseAssistMode.AlwaysGreen, PresetAim = true, Coach = CoachTarget.Power, CoachText = "Drag the cue down, then let go"
        });
        config.Steps.Add(new TutorialStep
        {
            Kind = TutorialStepKind.Aim, Title = "AIM",
            Instruction = "Drag on the table to aim. Put the ghost ball where the gold line points into the pocket.",
            Success = "Nice cut!", Pocket = 4, ObjectDistance = 0.5f, CutDegrees = 20f, CueDistance = 0.6f,
            Assist = ReleaseAssistMode.AlwaysGreen, Coach = CoachTarget.FineAim, CoachText = "Use the wheel for tiny adjustments", ShowAimAfter = 2
        });
        config.Steps.Add(new TutorialStep
        {
            Kind = TutorialStepKind.Release, Title = "RELEASE",
            Instruction = "Let go on GREEN: the ball goes exactly where you aimed. Left of green = left, right = right. MED power or less.",
            Success = "Clean release!", Pocket = 5, ObjectDistance = 0.5f, CutDegrees = -20f, CueDistance = 0.6f,
            Assist = ReleaseAssistMode.Easy, YellowCounts = true, ShowAimAfter = 2
        });
        config.Steps.Add(new TutorialStep
        {
            Kind = TutorialStepKind.PowerControl, Title = "POWER VS CONTROL",
            Instruction = "Harder shots shrink the green and speed up the bar. Use only the power you need.",
            Success = "Just enough power!", Pocket = 5, ObjectDistance = 0.4f, Approach = new Vector2(0.3f, 0.954f), CutDegrees = 6f, CueDistance = 1.2f,
            Assist = ReleaseAssistMode.Normal, PresetAim = true, MaxPull = 0.6f, MoveOnAfter = 2
        });
        config.Steps.Add(new TutorialStep
        {
            Kind = TutorialStepKind.BallInHand, Title = "BALL IN HAND",
            Instruction = "Your opponent fouled: BALL IN HAND. Drag the cue ball anywhere, tap DONE, then shoot.",
            Success = "You're a natural!", Pocket = 0, ObjectDistance = 0.35f, CutDegrees = 10f, CueDistance = 0.5f, ObjectBall = 1,
            ExtraBalls = new[] { 11 }, ExtraPositions = new[] { new Vector2(0.25f, 0.5f) },
            BallInHand = true, Assist = ReleaseAssistMode.Normal
        });
        EditorUtility.SetDirty(config);
    }

    public struct VenueSpec
    {
        public string Id;
        public string Name;
        public Color Felt;
        public string Cap;              // material: CapBrass or CapChrome
        public LampStyle Lamp;
        public Color Light;
        public float LightIntensity;
        public Color Ambient;
    }

    // Commit 9 (meta design venue table): cloth, caps and light per venue; every one keeps the TV-tournament light
    // over the table. Riverside's numbers are the V6 tuning.
    public static readonly VenueSpec[] VenueSpecs =
    {
        // v4 V6: cloth about 11% darker (HSV value), satin caps, a billiard lamp per venue.
        new() { Id = "basement", Name = "Basement Bar", Felt = Darker(Hex("2C6A3A")), Cap = "CapBrass", Light = new Color(1f, 0.86f, 0.7f), LightIntensity = 33f, Ambient = new Color(0.95f, 0.85f, 0.74f),
                Lamp = new LampStyle { Body = Hex("1A1A1A"), BodyMetallic = 0.8f, BodySmoothness = 0.35f, Shade = LampShadeShape.Dome, ShadeCount = 3,
                    ShadeOuter = Hex("1F5A2C"), ShadeMetallic = 0f, ShadeSmoothness = 0.8f, ShadeInner = new Color(1.6f, 1.3f, 0.55f), Bulb = new Color(4f, 3.1f, 1.5f), Hanger = LampHanger.Chains } },
        new() { Id = "downtown", Name = "Downtown Hall", Felt = Darker(Hex("2A5D9E")), Cap = "CapChrome", Light = new Color(0.96f, 0.97f, 1f), LightIntensity = 32f, Ambient = new Color(0.9f, 0.96f, 1.05f),
                Lamp = new LampStyle { Body = Hex("5A5F64"), BodyMetallic = 0.7f, BodySmoothness = 0.3f, Shade = LampShadeShape.Cone, ShadeCount = 3,
                    ShadeOuter = Hex("4A4F55"), ShadeMetallic = 0.7f, ShadeSmoothness = 0.3f, ShadeInner = new Color(1.5f, 1.4f, 1.2f), Bulb = new Color(3.6f, 3.2f, 2.5f), Hanger = LampHanger.Rods } },
        new() { Id = "riverside", Name = "Riverside Club", Felt = Darker(Hex("1F6B45")), Cap = "CapBrass", Light = new Color(1f, 0.9f, 0.8f), LightIntensity = 32f, Ambient = new Color(1.1f, 1.0f, 0.88f),
                Lamp = new LampStyle { Body = Hex("3B2416"), BodyMetallic = 0f, BodySmoothness = 0.45f, Shade = LampShadeShape.Cylinder, ShadeCount = 3,
                    ShadeOuter = Hex("D8CCB0"), ShadeMetallic = 0f, ShadeSmoothness = 0.1f, ShadeInner = new Color(1.6f, 1.35f, 0.9f), Bulb = new Color(3.8f, 3f, 1.8f), Hanger = LampHanger.Rods } },
        new() { Id = "lounge", Name = "Grand Lounge", Felt = Darker(Hex("7A1E2C")), Cap = "CapBrass", Light = new Color(1f, 0.9f, 0.74f), LightIntensity = 34f, Ambient = new Color(1.05f, 0.94f, 0.82f),
                Lamp = new LampStyle { Body = Hex("9C7533"), BodyMetallic = 0.9f, BodySmoothness = 0.55f, Shade = LampShadeShape.Dome, ShadeCount = 3,
                    ShadeOuter = Hex("A88440"), ShadeMetallic = 0.9f, ShadeSmoothness = 0.55f, ShadeInner = new Color(1.6f, 1.3f, 0.8f), Bulb = new Color(3.8f, 3f, 1.7f), Hanger = LampHanger.Chains } },
        new() { Id = "royal", Name = "Royal Masters", Felt = Darker(Hex("3A2F78")), Cap = "CapChrome", Light = new Color(1f, 0.98f, 0.95f), LightIntensity = 36f, Ambient = new Color(0.92f, 0.92f, 1.02f),
                Lamp = new LampStyle { Body = Hex("121214"), BodyMetallic = 0.6f, BodySmoothness = 0.4f, Shade = LampShadeShape.Dome, ShadeCount = 4,
                    ShadeOuter = Hex("141416"), ShadeMetallic = 0.4f, ShadeSmoothness = 0.5f, ShadeInner = new Color(1.6f, 1.35f, 0.9f), Bulb = new Color(3.8f, 3.1f, 1.9f), GoldRim = true, Hanger = LampHanger.Rods } }
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
            theme.Lamp = spec.Lamp;
            theme.Thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(VenueThumbnailPath(spec.Id));
            EditorUtility.SetDirty(theme);
        }
    }

    // v4 V6: the same hue and saturation, about 11% less value.
    private static Color Darker(Color color)
    {
        Color.RGBToHSV(color, out float h, out float sat, out float v);
        return Color.HSVToRGB(h, sat, v * 0.89f);
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
