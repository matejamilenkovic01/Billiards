public sealed class RulesOutcome
{
    public RulesState Next;
    public Side Shooter;
    public FoulType Foul;
    public bool ShooterContinues;
    public bool GroupsAssigned;
    public bool RespotEight;
    public bool MatchOver;
    public bool PocketedOwnBall;
}

// 8-ball (implementation_definition 4.6.2) with the division rules of implementation_definition_iteration_v4 V4:
// called pockets for the 8 or for every shot, and the opponent blocking or picking the pocket for your 8.
public static class EightBallRules
{
    public static RulesOutcome Evaluate(RulesState state, ShotResult shot)
    {
        Side shooter = state.Turn;
        Side opponent = RulesState.Other(shooter);
        RulesState next = state.Clone();
        next.CalledPocket = -1;
        var outcome = new RulesOutcome { Next = next, Shooter = shooter };

        bool wasOnEight = state.IsOnEight(shooter);
        bool eightPocketed = shot.WasPocketed(BallGroups.EightBall);
        outcome.Foul = FindFoul(state, shot);

        // Object balls stay down, whoever's they are; the 8 on the break is the exception (re-spotted).
        foreach (PocketEvent pocketed in shot.Pocketed)
        {
            if (pocketed.Number == BallGroups.CueBall)
                continue;
            if (pocketed.Number == BallGroups.EightBall && state.IsBreakShot)
                continue;
            next.Pocketed[pocketed.Number] = true;
        }

        next.IsBreakShot = false;
        next.BallInHand = false;
        next.KitchenOnly = false;

        if (state.IsBreakShot)
        {
            outcome.RespotEight = eightPocketed;
            bool pocketedAny = false;
            foreach (PocketEvent pocketed in shot.Pocketed)
                pocketedAny |= pocketed.Number != BallGroups.CueBall && pocketed.Number != BallGroups.EightBall;
            outcome.ShooterContinues = outcome.Foul == FoulType.None && pocketedAny;
            FinishTurn(next, outcome, shooter, opponent);
            return outcome;
        }

        if (eightPocketed)
        {
            EndReason reason = !wasOnEight ? EndReason.EightTooEarly
                : outcome.Foul != FoulType.None ? EndReason.EightWithFoul
                : EightPocketReason(state, shooter, PocketOf(shot, BallGroups.EightBall));
            next.IsOver = true;
            next.Winner = reason == EndReason.EightPocketed ? shooter : opponent;
            next.Reason = reason;
            next.ReasonSide = shooter;
            next.ReasonFoul = outcome.Foul;
            outcome.MatchOver = true;
            return outcome;
        }

        // Called shots (Diamond, GC): only balls in the called pocket count for the turn and the groups; the others
        // stay down. No ball in the called pocket: the turn passes, no foul.
        bool calls = state.NeedsCall(shooter);
        int called = state.CalledPocket;

        // Open table: the first legally pocketed ball decides the groups.
        if (state.IsTableOpen && outcome.Foul == FoulType.None)
        {
            foreach (PocketEvent pocketed in shot.Pocketed)
            {
                BallGroup group = BallGroups.Of(pocketed.Number);
                if (group == BallGroup.Open || (calls && pocketed.PocketIndex != called))
                    continue;
                next.Groups[(int)shooter] = group;
                next.Groups[(int)opponent] = BallGroups.Other(group);
                outcome.GroupsAssigned = true;
                break;
            }
        }

        BallGroup own = next.GroupOf(shooter);
        foreach (PocketEvent pocketed in shot.Pocketed)
            outcome.PocketedOwnBall |= BallGroups.Contains(own, pocketed.Number) && (!calls || pocketed.PocketIndex == called);

        outcome.ShooterContinues = outcome.Foul == FoulType.None && outcome.PocketedOwnBall;
        FinishTurn(next, outcome, shooter, opponent);
        return outcome;
    }

    // A legal 8 (group cleared, no foul): does the pocket count under this rule?
    private static EndReason EightPocketReason(RulesState state, Side shooter, int pocket)
    {
        int chosen = state.EightPocket[(int)shooter];
        switch (state.Rules)
        {
            case RuleSetId.CallEight:
            case RuleSetId.CallAll:
                return pocket == state.CalledPocket ? EndReason.EightPocketed : EndReason.EightWrongPocket;
            case RuleSetId.BlockedPocket:
                return chosen >= 0 && pocket == chosen ? EndReason.EightBlockedPocket : EndReason.EightPocketed;
            case RuleSetId.OpponentCallsEight:
            case RuleSetId.CallAllOpponentEight:
                return chosen < 0 || pocket == chosen ? EndReason.EightPocketed : EndReason.EightWrongPocket;
            default:
                return EndReason.EightPocketed;
        }
    }

    private static int PocketOf(ShotResult shot, int number)
    {
        foreach (PocketEvent pocketed in shot.Pocketed)
        {
            if (pocketed.Number == number)
                return pocketed.PocketIndex;
        }
        return -1;
    }

    private static void FinishTurn(RulesState next, RulesOutcome outcome, Side shooter, Side opponent)
    {
        if (outcome.ShooterContinues)
        {
            next.Turn = shooter;
            return;
        }

        next.Turn = opponent;
        // Any foul: the opponent places the cue ball anywhere on the table.
        if (outcome.Foul != FoulType.None)
        {
            next.BallInHand = true;
            next.KitchenOnly = false;
        }
    }

    public static FoulType FindFoul(RulesState state, ShotResult shot)
    {
        if (shot.CueBallPocketed)
            return FoulType.CueBallPocketed;
        if (shot.FirstContactBall < 0)
            return FoulType.NoBallHit;
        if (!state.IsLegalTarget(state.Turn, shot.FirstContactBall))
            return FoulType.WrongBallFirst;
        if (!shot.AnyCushionAfterContact && !AnyObjectBallPocketed(shot))
            return FoulType.NoRailAfterContact;
        return FoulType.None;
    }

    private static bool AnyObjectBallPocketed(ShotResult shot)
    {
        foreach (PocketEvent pocketed in shot.Pocketed)
        {
            if (pocketed.Number != BallGroups.CueBall)
                return true;
        }
        return false;
    }

    public static RulesState Forfeit(RulesState state, Side forfeiting)
    {
        RulesState next = state.Clone();
        next.IsOver = true;
        next.Winner = RulesState.Other(forfeiting);
        next.Reason = EndReason.Forfeit;
        next.ReasonSide = forfeiting;
        return next;
    }

    // End-of-match line, e.g. "You pocketed the 8-ball!" or "Player 2 scratched on the 8-ball."
    public static string ReasonText(RulesState state, string playerName = "You", string botName = "Opponent")
    {
        bool byPlayer = state.ReasonSide == Side.Player;
        string who = byPlayer ? playerName : botName;
        // The computer winning gets a full stop, not a cheer.
        bool botWon = !byPlayer;
        return state.Reason switch
        {
            EndReason.EightPocketed => botWon ? $"{who} pocketed the 8-ball." : $"{who} pocketed the 8-ball!",
            EndReason.EightTooEarly => $"{who} pocketed the 8-ball too early.",
            EndReason.EightWithFoul => state.ReasonFoul == FoulType.CueBallPocketed ? $"{who} scratched on the 8-ball." : $"{who} fouled on the 8-ball.",
            EndReason.Forfeit => $"{who} forfeited.",
            EndReason.EightWrongPocket => $"{who} pocketed the 8 in the wrong pocket.",
            EndReason.EightBlockedPocket => $"{who} pocketed the 8 in a blocked pocket.",
            _ => ""
        };
    }

    public static string FoulText(FoulType foul) => foul switch
    {
        FoulType.CueBallPocketed => "FOUL! Cue ball scratched",
        FoulType.WrongBallFirst => "FOUL! Wrong ball first",
        FoulType.NoBallHit => "FOUL! No ball hit",
        FoulType.NoRailAfterContact => "FOUL! No rail after contact",
        _ => ""
    };
}
