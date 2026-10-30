using UnityEngine;

// Where a player stands: division (index), stage (0 = III, 1 = II, 2 = I) and stars in that stage; past the last
// division, Grand Champion counts stars without end.
public struct RankState
{
    public int Division;
    public int Stage;
    public int Stars;
    public int ChampionStars;
    public bool IsChampion;

    // Stages climbed so far (0 = Bronze III), for "best reached" and the road.
    public int StageIndex => Division * DivisionCatalog.StagesPerDivision + Stage + (IsChampion ? 1 : 0);
}

public struct RankChange
{
    public RankState Before;
    public RankState After;
    public int StarsGained;      // +1, +2 (streak), -1 or 0
    public bool StreakBonus;
    public bool StageUp;
    public bool StageDown;
    public bool NewDivision;     // a checkpoint was reached
    public bool LossCostNothing; // a loss in a division (or at a point) where losing takes nothing
}

// The rules of the ladder (v3 V10), pure: win +1 star (+1 more from the StreakBonusFrom-th win in a row), full
// stages promote with the extra stars carried over, the start of every division is a checkpoint; a loss follows
// the division's LossRule. Shared by ProfileService, the checks and ProgressionSim.
public static class RankLadder
{
    public static RankChange Apply(DivisionCatalog catalog, RankState rank, bool won, int winStreakAfter)
    {
        var change = new RankChange { Before = rank };
        if (won)
        {
            int gained = 1;
            if (winStreakAfter >= catalog.StreakBonusFrom)
            {
                gained++;
                change.StreakBonus = true;
            }
            change.StarsGained = gained;
            rank = AddStars(catalog, rank, gained, ref change);
        }
        else
        {
            rank = Lose(catalog, rank, ref change);
        }
        change.After = rank;
        return change;
    }

    // What a loss would cost right now: false if it takes nothing.
    public static bool LossCosts(DivisionCatalog catalog, RankState rank)
    {
        var change = new RankChange();
        RankState after = Lose(catalog, rank, ref change);
        return after.Stars != rank.Stars || after.Stage != rank.Stage || after.ChampionStars != rank.ChampionStars;
    }

    private static RankState AddStars(DivisionCatalog catalog, RankState rank, int stars, ref RankChange change)
    {
        if (rank.IsChampion)
        {
            rank.ChampionStars += stars;
            return rank;
        }
        rank.Stars += stars;
        while (!rank.IsChampion && rank.Stars >= catalog.At(rank.Division).StarsPerStage)
        {
            rank.Stars -= catalog.At(rank.Division).StarsPerStage;
            change.StageUp = true;
            rank.Stage++;
            if (rank.Stage >= DivisionCatalog.StagesPerDivision)
            {
                rank.Stage = 0;
                rank.Division++;
                change.NewDivision = true;
                if (rank.Division >= catalog.Count)
                {
                    rank.Division = catalog.Count - 1;
                    rank.Stage = DivisionCatalog.StagesPerDivision - 1;
                    rank.IsChampion = true;
                    rank.ChampionStars = rank.Stars;
                    rank.Stars = 0;
                }
            }
        }
        return rank;
    }

    private static RankState Lose(DivisionCatalog catalog, RankState rank, ref RankChange change)
    {
        RankState before = rank;
        if (rank.IsChampion)
        {
            rank.ChampionStars = Mathf.Max(0, rank.ChampionStars - 1);
        }
        else
        {
            DivisionDefinition division = catalog.At(rank.Division);
            switch (division.Loss)
            {
                case LossRule.NoLoss:
                    break;
                case LossRule.LoseStarKeepStage:
                    rank.Stars = Mathf.Max(0, rank.Stars - 1);
                    break;
                case LossRule.LoseStarDropStage:
                    if (rank.Stars > 0)
                    {
                        rank.Stars--;
                    }
                    else if (rank.Stage > 0)
                    {
                        // Down to the previous stage, its stars one short of full; never below the division's start.
                        rank.Stage--;
                        rank.Stars = division.StarsPerStage - 1;
                        change.StageDown = true;
                    }
                    break;
            }
        }

        bool costs = rank.Stars != before.Stars || rank.Stage != before.Stage || rank.ChampionStars != before.ChampionStars;
        if (!costs)
            change.LossCostNothing = true;
        else
        {
            change.StarsGained = -1;
        }
        return rank;
    }

    public static string StageLabel(int stage) => stage switch { 0 => "III", 1 => "II", _ => "I" };

    public static string Label(DivisionCatalog catalog, RankState rank) =>
        rank.IsChampion ? "GRAND CHAMPION" : $"{catalog.At(rank.Division).Name.ToUpperInvariant()} {StageLabel(rank.Stage)}";

    // Stars still needed for the next stage, and whether that step is a new division (a checkpoint).
    public static int StarsToNext(DivisionCatalog catalog, RankState rank, out bool checkpoint, out string nextLabel)
    {
        checkpoint = false;
        if (rank.IsChampion)
        {
            nextLabel = "";
            return 0;
        }
        DivisionDefinition division = catalog.At(rank.Division);
        int need = division.StarsPerStage - rank.Stars;
        if (rank.Stage < DivisionCatalog.StagesPerDivision - 1)
        {
            nextLabel = $"{division.Name.ToUpperInvariant()} {StageLabel(rank.Stage + 1)}";
            return need;
        }
        checkpoint = true;
        nextLabel = rank.Division + 1 < catalog.Count ? catalog.At(rank.Division + 1).Name.ToUpperInvariant() : "GRAND CHAMPION";
        return need;
    }
}
