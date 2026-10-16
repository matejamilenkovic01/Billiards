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

// 8-ball, no called pockets (implementation_definition 4.6.2).
public static class EightBallRules
{
    public static RulesOutcome Evaluate(RulesState state, ShotResult shot)
    {
        Side shooter = state.Turn;
        Side opponent = RulesState.Other(shooter);
        RulesState next = state.Clone();
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
                : EndReason.EightPocketed;
            next.IsOver = true;
            next.Winner = reason == EndReason.EightPocketed ? shooter : opponent;
            next.Reason = reason;
            next.ReasonSide = shooter;
            next.ReasonFoul = outcome.Foul;
            outcome.MatchOver = true;
            return outcome;
        }

        // Open table: the first legally pocketed ball decides the groups.
        if (state.IsTableOpen && outcome.Foul == FoulType.None)
        {
            foreach (PocketEvent pocketed in shot.Pocketed)
            {
                BallGroup group = BallGroups.Of(pocketed.Number);
                if (group == BallGroup.Open)
                    continue;
                next.Groups[(int)shooter] = group;
                next.Groups[(int)opponent] = BallGroups.Other(group);
                outcome.GroupsAssigned = true;
                break;
            }
        }

        BallGroup own = next.GroupOf(shooter);
        foreach (PocketEvent pocketed in shot.Pocketed)
            outcome.PocketedOwnBall |= BallGroups.Contains(own, pocketed.Number);

        outcome.ShooterContinues = outcome.Foul == FoulType.None && outcome.PocketedOwnBall;
        FinishTurn(next, outcome, shooter, opponent);
        return outcome;
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
        bool botWon = !byPlayer && botName == "Opponent";
        return state.Reason switch
        {
            EndReason.EightPocketed => botWon ? $"{who} pocketed the 8-ball." : $"{who} pocketed the 8-ball!",
            EndReason.EightTooEarly => $"{who} pocketed the 8-ball too early.",
            EndReason.EightWithFoul => state.ReasonFoul == FoulType.CueBallPocketed ? $"{who} scratched on the 8-ball." : $"{who} fouled on the 8-ball.",
            EndReason.Forfeit => $"{who} forfeited.",
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
