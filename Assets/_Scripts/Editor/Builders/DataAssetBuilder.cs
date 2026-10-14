using System.IO;
using UnityEditor;
using UnityEngine;

public static class DataAssetBuilder
{
    public const string Folder = "Assets/_ScriptableObjects";
    public const string CueCatalogPath = Folder + "/CueCatalog.asset";
    public const string MatchConfigPath = Folder + "/MatchConfig.asset";
    public const string BotProfilePath = Folder + "/BotProfile.asset";

    public struct CueSpec
    {
        public string Id;
        public string Name;
        public int WinsToUnlock;
        public int Price;
        public bool Infinite;
        public int MaxEnergy;
        public int Refill;
        public float GreenHalf;
        public float YellowWidth;
        public float Period;
        public float GuideDiameters;
        public Color Shaft;
        public Color Butt;
        public Color Band;
        public float Glow;
    }

    // Source of truth for the catalog (implementation_definition 4.6.5); also drives the generated cue art.
    public static readonly CueSpec[] CueSpecs =
    {
        new() { Id = "rookie", Name = "Rookie Cue", WinsToUnlock = 0, Price = 0, Infinite = true, MaxEnergy = 0, Refill = 0,
                GreenHalf = 0.10f, YellowWidth = 0.30f, Period = 1.30f, GuideDiameters = 2f,
                Shaft = Hex("EBCFA2"), Butt = Hex("C99358"), Band = Hex("8A5A2B") },
        new() { Id = "club", Name = "Club Cue", WinsToUnlock = 1, Price = 30, MaxEnergy = 8, Refill = 5,
                GreenHalf = 0.13f, YellowWidth = 0.30f, Period = 1.45f, GuideDiameters = 4f,
                Shaft = Hex("E3C696"), Butt = Hex("4E2A17"), Band = Hex("C62828") },
        new() { Id = "pro", Name = "Pro Cue", WinsToUnlock = 3, Price = 70, MaxEnergy = 10, Refill = 8,
                GreenHalf = 0.16f, YellowWidth = 0.30f, Period = 1.60f, GuideDiameters = 6f,
                Shaft = Hex("E8D2A8"), Butt = Hex("151515"), Band = Hex("D4A62A") },
        new() { Id = "master", Name = "Master Cue", WinsToUnlock = 6, Price = 140, MaxEnergy = 12, Refill = 12,
                GreenHalf = 0.19f, YellowWidth = 0.28f, Period = 1.80f, GuideDiameters = 9f,
                Shaft = Hex("E9D8B8"), Butt = Hex("1D4AA8"), Band = Hex("C9D1D9") },
        new() { Id = "legend", Name = "Legend Cue", WinsToUnlock = 10, Price = 260, MaxEnergy = 15, Refill = 18,
                GreenHalf = 0.22f, YellowWidth = 0.26f, Period = 2.00f, GuideDiameters = 13f,
                Shaft = Hex("F1DEB0"), Butt = Hex("D9A92E"), Band = Hex("FFF0B0"), Glow = 0.55f }
    };

    // The catalog is always rebuilt from CueSpecs; MatchConfig and BotProfile keep their tuned values
    // unless resetTuning is true.
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
                WinsToUnlock = spec.WinsToUnlock,
                Price = spec.Price,
                InfiniteEnergy = spec.Infinite,
                MaxEnergy = spec.MaxEnergy,
                RefillPrice = spec.Refill,
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
        LoadOrCreate<BotProfile>(BotProfilePath, resetTuning);

        AssetDatabase.SaveAssets();
        Debug.Log("DataAssetBuilder: done");
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
