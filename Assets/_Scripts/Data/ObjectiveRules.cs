using UnityEngine;

// How a match moves an objective along (v3 V12, v4 V12), pure: shared by ProfileService, the checks and ProgressionSim.
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
            case ObjectiveKind.DrawShots: next += Count(stats, p => p.DrawBack >= objective.Param); break;
            case ObjectiveKind.FollowPots: next += Count(stats, p => p.Follow); break;
            case ObjectiveKind.StunPots: next += Count(stats, p => p.Straight && p.StunDistance <= objective.Param); break;
            case ObjectiveKind.SideSpinPots: next += Count(stats, p => p.SideSpin); break;
            case ObjectiveKind.CalledPots: next += Count(stats, p => p.Called); break;
            case ObjectiveKind.CalledEightWins: next += stats.CalledEightWin ? 1 : 0; break;
            case ObjectiveKind.VisitPots: next += stats.VisitsWithPots(Mathf.RoundToInt(objective.Param)); break;
            case ObjectiveKind.PocketBallsInDivision: next += Reached(before, objective.Param) ? stats.Pots.Count : 0; break;
            case ObjectiveKind.PerfectReleasesInDivision: next += Reached(before, objective.Param) ? stats.PerfectReleases : 0; break;
            case ObjectiveKind.WinStreakInDivision: next = Mathf.Max(next, stats.Won && Reached(before, objective.Param) ? winStreak : 0); break;
        }
        return Mathf.Min(next, objective.Target);
    }

    // Whether this objective can move in a lost match (v4 V12 "cue coins": skill objectives that pay in a loss too).
    public static bool CountsInLoss(ObjectiveKind kind) => kind switch
    {
        ObjectiveKind.PocketBalls or ObjectiveKind.PerfectReleases or ObjectiveKind.TwoBallsOneShot or ObjectiveKind.FineAimPots
            or ObjectiveKind.PerfectStreak or ObjectiveKind.LongPot or ObjectiveKind.CutPots or ObjectiveKind.BankShot
            or ObjectiveKind.DrawShots or ObjectiveKind.FollowPots or ObjectiveKind.StunPots or ObjectiveKind.SideSpinPots
            or ObjectiveKind.CalledPots or ObjectiveKind.VisitPots or ObjectiveKind.PocketBallsInDivision
            or ObjectiveKind.PerfectReleasesInDivision => true,
        _ => false
    };

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
