using System;
using System.Collections.Generic;
using UnityEngine;

// How a loss counts in a division (v3 V10).
public enum LossRule
{
    NoLoss = 0,              // Bronze: a loss takes nothing
    LoseStarKeepStage = 1,   // Silver: -1 star, never below the start of the stage
    LoseStarDropStage = 2    // Gold and up: -1 star; at 0 stars you drop a stage, never below the division start
}

[Serializable]
public class OpponentDefinition
{
    public string Id;
    public string Name;
    [Tooltip("For banners: \"DINA'S TURN\".")]
    public string ShortName;
    [Tooltip("One line of character (EN).")]
    public string Line;
    public Sprite Portrait;
    [Tooltip("The portrait is the generated avatar (initials are drawn over it).")]
    public bool GeneratedPortrait = true;
    [Tooltip("Personality, display and timing only: seconds added to (or taken from) thinking.")]
    public float ThinkExtra;
    [Tooltip("Personality: always breaks at full power.")]
    public bool FullBreak;

    // "Rookie Ray" -> "RR", "The Duchess" -> "D".
    public string Initials
    {
        get
        {
            string initials = "";
            foreach (string word in Name.Split(' '))
            {
                if (word.Length > 0 && word != "The" && initials.Length < 2)
                    initials += char.ToUpperInvariant(word[0]);
            }
            return initials;
        }
    }

    public OpponentViewData ToView(DivisionDefinition division) => new()
    {
        Id = Id,
        Name = Name,
        ShortName = string.IsNullOrEmpty(ShortName) ? Name : ShortName,
        Line = Line,
        Portrait = Portrait,
        GeneratedPortrait = GeneratedPortrait || Portrait == null,
        Initials = Initials,
        Color = division.Color
    };
}

// One ranked division (v3 V10): three stages of stars, played in its venue against its CPU characters, all of
// the same fixed difficulty (the division's BotProfile, playing the division's cue).
[Serializable]
public class DivisionDefinition
{
    public string Id;
    public string Name;
    public string VenueId;
    public string VenueName;
    public VenueTheme Venue;
    [Range(1, 5)] public int StarsPerStage = 3;
    public LossRule Loss = LossRule.LoseStarDropStage;
    public int WinCoins = 20;
    [Tooltip("What a loss pays here (v4 V12): it grows with the division so a cue is always within reach.")]
    public int LossCoins = 5;
    [Tooltip("Coins for each new stage (II, I) of this division, once (v4 V11).")]
    public int StageReward;
    [Tooltip("Coins for entering this division (its checkpoint), once.")]
    public int DivisionReward;
    [Tooltip("The bot's cue, and the cue a player should have here (fair, v3 V8).")]
    public string BotCueId;
    public BotProfile Bot;
    public Color Color = Color.white;
    public List<OpponentDefinition> Opponents = new();
    [Tooltip("The 8-ball rule of this division (v4 V4) and its one-line card text.")]
    public RuleSetId Rules;
    public string RuleText;
}

[CreateAssetMenu(fileName = "DivisionCatalog", menuName = "Billiards/Division Catalog")]
public class DivisionCatalog : ScriptableObject
{
    public const int StagesPerDivision = 3;

    [Tooltip("Bronze first. The last division leads to Grand Champion (an endless star count).")]
    public List<DivisionDefinition> Divisions = new();
    [Tooltip("From this win in a row on, each win gives one more star.")]
    public int StreakBonusFrom = 3;
    [Tooltip("Coins for a win in Grand Champion.")]
    public int ChampionWinCoins = 100;
    public int ChampionLossCoins = 25;
    [Tooltip("Coins for entering Grand Champion, once.")]
    public int ChampionEntryReward = 1000;
    [Tooltip("Grand Champion: every this many stars pays the reward, once each.")]
    public int ChampionMilestoneStars = 10;
    public int ChampionMilestoneReward = 300;
    [Tooltip("Grand Champion's 8-ball rule (v4 V4); it plays the last division's tables.")]
    public RuleSetId ChampionRules = RuleSetId.CallAllOpponentEight;
    public string ChampionRuleText;

    public int Count => Divisions.Count;

    public DivisionDefinition Get(string id) => Divisions.Find(d => d.Id == id);

    public int IndexOf(string id) => Divisions.FindIndex(d => d.Id == id);

    public DivisionDefinition At(int index) => Divisions[Mathf.Clamp(index, 0, Divisions.Count - 1)];

    // The reward for reaching a stage (RankState.StageIndex) the first time: entering a division pays DivisionReward
    // (Bronze III, where everyone starts, pays nothing), the other stages their division's StageReward.
    public int RewardForStage(int stageIndex, out bool division)
    {
        division = false;
        if (stageIndex <= 0)
            return 0;
        if (stageIndex >= Count * StagesPerDivision)
        {
            division = true;
            return ChampionEntryReward;
        }
        DivisionDefinition at = Divisions[stageIndex / StagesPerDivision];
        division = stageIndex % StagesPerDivision == 0;
        return division ? at.DivisionReward : at.StageReward;
    }

    public int WinCoinsFor(RankState rank) => rank.IsChampion ? ChampionWinCoins : At(rank.Division).WinCoins;

    public int LossCoinsFor(RankState rank) => rank.IsChampion ? ChampionLossCoins : At(rank.Division).LossCoins;
}
