using UnityEngine;

// What the player side did in one match, for the objectives (v3 V12): PERFECT releases and streaks, pots with
// their distance, cut, bank and FINE AIM, two-ball shots, fouls, run outs, the opponent's balls left. Pure: used by
// the match and by the progression simulation (ProgressionSim), so both count the same way.
public sealed class MatchStatsRecorder
{
    private readonly Vector2[] _start = new Vector2[BallGroups.BallCount];
    private Vector2 _direction;
    private bool _fineAim;
    private int _perfectStreak;
    private int _visitPots;
    private bool _visitRunOutCounted;

    public MatchStats Stats { get; private set; } = new();

    public void Reset()
    {
        Stats = new MatchStats();
        _perfectStreak = 0;
        _visitPots = 0;
        _visitRunOutCounted = false;
    }

    // At the release of a player shot.
    public void ShotStart(BallState[] balls, Vector2 direction, ReleaseZone zone, bool usedFineAim)
    {
        _direction = direction;
        _fineAim = usedFineAim;
        for (int i = 0; i < _start.Length; i++)
            _start[i] = balls[i].Pos;

        if (zone == ReleaseZone.Green)
        {
            Stats.PerfectReleases++;
            _perfectStreak++;
            Stats.MaxPerfectStreak = Mathf.Max(Stats.MaxPerfectStreak, _perfectStreak);
        }
        else
        {
            _perfectStreak = 0;
        }
    }

    // After a player shot, with the rules as they stood for it (`before`). Only a clean shot counts its pots.
    public void ShotEnd(RulesState before, RulesOutcome outcome, ShotResult result, TableGeometry geometry, float bankIgnoreBeforeDrop)
    {
        bool foul = outcome.Foul != FoulType.None;
        int own = 0;
        if (foul)
        {
            Stats.Fouls++;
        }
        else
        {
            foreach (PocketEvent pocket in result.Pocketed)
            {
                if (PocketKind(before, pocket.Number) != PocketFxKind.Legal)
                    continue;
                own++;
                Stats.Pots.Add(new PotRecord
                {
                    Distance = (geometry.Pockets[pocket.PocketIndex].Center - _start[pocket.Number]).magnitude,
                    Cut = pocket.Number == result.FirstContactBall ? Vector2.Angle(_direction, result.FirstContactObjectDir) : 0f,
                    Bank = TouchedCushionBefore(result, pocket.Number, pocket.Time - bankIgnoreBeforeDrop),
                    FineAim = _fineAim
                });
            }
        }
        if (own >= 2)
            Stats.TwoBallShots++;

        // A run out: the whole group (3+ balls) cleared in one visit, counted once per visit.
        _visitPots += own;
        bool cleared = outcome.Next.IsOnEight(Side.Player) || (outcome.Next.IsOver && outcome.Next.Winner == Side.Player);
        if (!foul && cleared && !_visitRunOutCounted && _visitPots >= 3)
        {
            Stats.RunOuts++;
            _visitRunOutCounted = true;
        }
        if (foul || !outcome.ShooterContinues)
        {
            _visitPots = 0;
            _visitRunOutCounted = false;
        }
    }

    // How the last finished match ended (checks).
    public EndReason LastReason { get; private set; }

    public void Finish(bool won, RulesState rules)
    {
        LastReason = rules.Reason;
        Stats.Won = won;
        Stats.OpponentLeft = won ? BallsLeft(rules, Side.Bot) : 0;
    }

    // How a ball dropped in a shot counts for the shooter, by the rules as they stood before the shot.
    public static PocketFxKind PocketKind(RulesState rules, int number)
    {
        if (number == 0)
            return PocketFxKind.Foul;
        Side shooter = rules.Turn;
        if (number == BallGroups.EightBall)
            return rules.IsOnEight(shooter) ? PocketFxKind.Legal : PocketFxKind.Foul;
        BallGroup group = rules.GroupOf(shooter);
        return group == BallGroup.Open || BallGroups.Contains(group, number) ? PocketFxKind.Legal : PocketFxKind.Opponent;
    }

    private static bool TouchedCushionBefore(ShotResult result, int number, float time)
    {
        foreach (CollisionEvent collision in result.Collisions)
        {
            if (collision.Time > time)
                break;
            if (collision.Kind == CollisionKind.Cushion && collision.BallA == number)
                return true;
        }
        return false;
    }

    private static int BallsLeft(RulesState rules, Side side)
    {
        BallGroup group = rules.GroupOf(side);
        if (group == BallGroup.Open)
            return 7;
        int left = 0;
        for (int n = 1; n < BallGroups.BallCount; n++)
        {
            if (n != BallGroups.EightBall && BallGroups.Contains(group, n) && !rules.Pocketed[n])
                left++;
        }
        return left;
    }
}
