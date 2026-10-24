using UnityEngine;

// How a match moves an objective along (v3 V12), pure: shared by ProfileService, the checks and ProgressionSim.
// Counting objectives add up; "best" ones (a streak, reaching a division) keep the highest value.
public static class ObjectiveRules
{
    // `before` is where the match was played (wins in a division), `after` where it left the player (reaching one).
    public static int Advance(ObjectiveDefinition objective, int progress, MatchStats stats, RankState before, RankState after, int winStreak)
    {
        int next = progress;
        switch (objective.Kind)
        {
            case ObjectiveKind.WinMatches: next += stats.Won ? 1 : 0; break;
            case ObjectiveKind.PocketBalls: next += stats.Pots.Count; break;
            case ObjectiveKind.PerfectReleases: next += stats.PerfectReleases; break;
            case ObjectiveKind.WinWithoutFoul: next += stats.Won && stats.Fouls == 0 ? 1 : 0; break;
            case ObjectiveKind.TwoBallsOneShot: next += stats.TwoBallShots; break;
            case ObjectiveKind.FineAimPots: next += Count(stats, p => p.FineAim); break;
            case ObjectiveKind.PerfectStreak: next = Mathf.Max(next, stats.MaxPerfectStreak); break;
            case ObjectiveKind.ReachDivision: next = Mathf.Max(next, Reached(after, objective.Param) ? objective.Target : 0); break;
            case ObjectiveKind.LongPot: next += Count(stats, p => p.Distance > objective.Param); break;
            case ObjectiveKind.WinStreak: next = Mathf.Max(next, winStreak); break;
            case ObjectiveKind.CutPots: next += Count(stats, p => p.Cut >= objective.Param); break;
            case ObjectiveKind.BankShot: next += Count(stats, p => p.Bank); break;
            case ObjectiveKind.WinWithOpponentLeft: next += stats.Won && stats.OpponentLeft >= objective.Param ? 1 : 0; break;
            case ObjectiveKind.RunOut: next += stats.RunOuts; break;
            case ObjectiveKind.WinsAtDivision: next += stats.Won && Reached(before, objective.Param) ? 1 : 0; break;
        }
        return Mathf.Min(next, objective.Target);
    }

    // For objectives that only look at where the player stands (a new slot may already be done).
    public static int Initial(ObjectiveDefinition objective, RankState rank) =>
        objective.Kind == ObjectiveKind.ReachDivision && Reached(rank, objective.Param) ? objective.Target : 0;

    private static bool Reached(RankState rank, float divisionIndex) => rank.IsChampion || rank.Division >= Mathf.RoundToInt(divisionIndex);

    private static int Count(MatchStats stats, System.Predicate<PotRecord> match)
    {
        int count = 0;
        foreach (PotRecord pot in stats.Pots)
        {
            if (match(pot))
                count++;
        }
        return count;
    }
}
