using UnityEngine;

// What the player side did in one match, for the objectives (v3 V12): PERFECT releases and streaks, pots with
// their distance, cut, bank and FINE AIM, spin (draw, follow, stun, side) and called pockets, two-ball shots, fouls,
// visits, run outs, the opponent's balls left. Pure: used by
// the match and by the progression simulation (ProgressionSim), so both count the same way.
public sealed class MatchStatsRecorder
{
    private readonly Vector2[] _start = new Vector2[BallGroups.BallCount];
    private Vector2 _direction;
    private bool _fineAim;
    private Vector2 _tip;
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

    // Where the tip meets the cue ball for these objectives (ball radii, x right, y up).
    public const float DrawMinB = -0.2f;
    public const float FollowMinB = 0.25f;
    public const float SideMinA = 0.25f;
    public const float FollowRollMin = 0.10f;
    public const float StraightCutMax = 10f;

    // At the release of a player shot. `tip` is where the tip meets the cue ball (zero: the centre).
    public void ShotStart(BallState[] balls, Vector2 direction, ReleaseZone zone, bool usedFineAim, Vector2 tip = default)
    {
        _direction = direction;
        _fineAim = usedFineAim;
        _tip = tip;
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
            bool calls = before.NeedsCall(Side.Player);
            Vector2 contact = result.FirstContactCuePos;
            Vector2 cueEnd = result.FinalPositions[0];
            float along = Vector2.Dot(cueEnd - contact, _direction);
            foreach (PocketEvent pocket in result.Pocketed)
            {
                // Under a calling rule only the called pocket counts; the other balls stay down.
                if (PocketKind(before, pocket.Number) != PocketFxKind.Legal || (calls && pocket.PocketIndex != before.CalledPocket))
                    continue;
                own++;
                bool first = pocket.Number == result.FirstContactBall;
                float cut = first ? Vector2.Angle(_direction, result.FirstContactObjectDir) : 0f;
                Stats.Pots.Add(new PotRecord
                {
                    Distance = (geometry.Pockets[pocket.PocketIndex].Center - _start[pocket.Number]).magnitude,
                    Cut = cut,
                    Bank = TouchedCushionBefore(result, pocket.Number, pocket.Time - bankIgnoreBeforeDrop),
                    FineAim = _fineAim,
                    DrawBack = _tip.y <= DrawMinB && along < 0f ? -along : 0f,
                    Follow = _tip.y >= FollowMinB && along >= FollowRollMin,
                    Straight = first && cut <= StraightCutMax,
                    StunDistance = (cueEnd - contact).magnitude,
                    SideSpin = Mathf.Abs(_tip.x) >= SideMinA,
                    Called = before.CalledPocket >= 0 && pocket.PocketIndex == before.CalledPocket
                });
            }
            // The 8 in the called pocket ends the match for the player.
            if (outcome.Next.IsOver && outcome.Next.Winner == Side.Player && outcome.Next.Reason == EndReason.EightPocketed
                && calls && before.CalledPocket >= 0)
            {
                foreach (PocketEvent pocket in result.Pocketed)
                {
                    if (pocket.Number == BallGroups.EightBall && pocket.PocketIndex == before.CalledPocket)
                        Stats.CalledEightWin = true;
                }
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
        if (foul || !outcome.ShooterContinues || outcome.Next.IsOver)
        {
            if (_visitPots > 0)
                Stats.VisitPots.Add(_visitPots);
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
