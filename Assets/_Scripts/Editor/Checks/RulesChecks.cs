// Hand-built shot results for every rule in implementation_definition 4.6.2.
public static class RulesChecks
{
    public static string All()
    {
        var report = new CheckReport("RulesChecks");

        // Break.
        RulesState state = RulesState.NewRack(Side.Player);
        RulesOutcome o = Eval(state, Shot(first: 1, rail: true));
        report.Check(o.Foul == FoulType.None && !o.ShooterContinues && o.Next.Turn == Side.Bot, "break, nothing down: turn passes");
        report.Check(!o.Next.IsBreakShot && o.Next.IsTableOpen && !o.Next.BallInHand, "after the break: open table, no ball in hand");

        o = Eval(state, Shot(first: 1, rail: true, pocketed: new[] { 3 }));
        report.Check(o.ShooterContinues && o.Next.Turn == Side.Player && o.Next.IsTableOpen, "break, solid down: shooter continues, table stays open");
        report.Check(o.Next.Pocketed[3], "break: pocketed ball stays down");

        o = Eval(state, Shot(first: 1, rail: true, pocketed: new[] { 0 }));
        report.Check(o.Foul == FoulType.CueBallPocketed && o.Next.Turn == Side.Bot, "break scratch: foul, turn passes");
        report.Check(o.Next.BallInHand && !o.Next.KitchenOnly, "break scratch: opponent has ball in hand anywhere");

        o = Eval(state, Shot(first: 1, rail: true, pocketed: new[] { 8 }));
        report.Check(o.RespotEight && !o.MatchOver && !o.Next.Pocketed[8], "8 on the break: re-spotted, no win or loss");
        report.Check(!o.ShooterContinues, "8 alone on the break does not keep the turn");
        o = Eval(state, Shot(first: 1, rail: true, pocketed: new[] { 8, 0 }));
        report.Check(o.RespotEight && !o.MatchOver && o.Foul == FoulType.CueBallPocketed, "8 + scratch on the break: re-spot and foul");

        // Open table.
        state = AfterBreak(Side.Player);
        o = Eval(state, Shot(first: 11, rail: true, pocketed: new[] { 11 }));
        report.Check(o.GroupsAssigned && o.Next.GroupOf(Side.Player) == BallGroup.Stripes && o.Next.GroupOf(Side.Bot) == BallGroup.Solids,
            "open table: first legal pot assigns stripes");
        report.Check(o.ShooterContinues, "open table: legal pot continues");

        o = Eval(state, Shot(first: 2, rail: true, pocketed: new[] { 2, 12 }));
        report.Check(o.Next.GroupOf(Side.Player) == BallGroup.Solids, "both kinds down: group of the first one");

        o = Eval(state, Shot(first: 8, rail: true));
        report.Check(o.Foul == FoulType.WrongBallFirst, "open table: 8 first is a foul");

        o = Eval(state, Shot(first: 3, rail: true, pocketed: new[] { 3, 0 }));
        report.Check(o.Foul == FoulType.CueBallPocketed && !o.GroupsAssigned && o.Next.IsTableOpen, "pot with a scratch assigns nothing");
        report.Check(o.Next.Pocketed[3], "ball potted on a foul stays down");

        // Fouls with groups.
        state = WithGroups(Side.Player, BallGroup.Solids);
        o = Eval(state, Shot(first: 12, rail: true));
        report.Check(o.Foul == FoulType.WrongBallFirst && o.Next.BallInHand && !o.Next.KitchenOnly && o.Next.Turn == Side.Bot,
            "wrong ball first: foul, opponent ball in hand anywhere");
        o = Eval(state, Shot(first: -1, rail: true));
        report.Check(o.Foul == FoulType.NoBallHit, "no ball hit: foul");
        o = Eval(state, Shot(first: 2, rail: false));
        report.Check(o.Foul == FoulType.NoRailAfterContact, "no rail after contact and nothing down: foul");
        o = Eval(state, Shot(first: 2, rail: false, pocketed: new[] { 2 }));
        report.Check(o.Foul == FoulType.None && o.ShooterContinues, "no rail but a pot: legal");
        o = Eval(state, Shot(first: 2, rail: true));
        report.Check(o.Foul == FoulType.None && !o.ShooterContinues && !o.Next.BallInHand, "legal miss: turn passes without ball in hand");
        o = Eval(state, Shot(first: 2, rail: true, pocketed: new[] { 10 }));
        report.Check(!o.ShooterContinues && o.Next.Pocketed[10], "potting only the opponent's ball: turn passes, ball stays down");
        o = Eval(state, Shot(first: 2, rail: true, pocketed: new[] { 2, 0 }));
        report.Check(o.Foul == FoulType.CueBallPocketed && !o.ShooterContinues && o.Next.Pocketed[2], "own pot with a scratch: foul, ball stays down");

        // The 8.
        o = Eval(state, Shot(first: 2, rail: true, pocketed: new[] { 8 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Bot && o.Next.Reason == EndReason.EightTooEarly, "8 before clearing the group: shooter loses");
        report.Check(EightBallRules.ReasonText(o.Next) == "You pocketed the 8-ball too early.", "reason text: 8 too early");

        RulesState onEight = WithGroups(Side.Player, BallGroup.Solids);
        for (int n = 1; n <= 7; n++)
            onEight.Pocketed[n] = true;
        report.Check(onEight.IsOnEight(Side.Player) && onEight.IsLegalTarget(Side.Player, 8) && !onEight.IsLegalTarget(Side.Player, 9),
            "group cleared: only the 8 is legal");
        o = Eval(onEight, Shot(first: 8, rail: true, pocketed: new[] { 8 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Player && o.Next.Reason == EndReason.EightPocketed, "legal 8: shooter wins");
        report.Check(EightBallRules.ReasonText(o.Next) == "You pocketed the 8-ball!", "reason text: win");
        o = Eval(onEight, Shot(first: 8, rail: true, pocketed: new[] { 8, 0 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Bot && o.Next.Reason == EndReason.EightWithFoul, "8 + scratch: shooter loses");
        report.Check(EightBallRules.ReasonText(o.Next) == "You scratched on the 8-ball.", "reason text: scratch on the 8");
        o = Eval(onEight, Shot(first: 9, rail: true));
        report.Check(o.Foul == FoulType.WrongBallFirst, "on the 8, hitting another ball first is a foul");

        RulesState lastBall = WithGroups(Side.Bot, BallGroup.Stripes);
        for (int n = 9; n <= 14; n++)
            lastBall.Pocketed[n] = true;
        o = Eval(lastBall, Shot(first: 15, rail: true, pocketed: new[] { 15, 8 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Player && o.Next.Reason == EndReason.EightTooEarly,
            "last group ball and the 8 in one shot: shooter loses");
        report.Check(EightBallRules.ReasonText(o.Next) == "Opponent pocketed the 8-ball too early.", "reason text from the user's view");

        // Turn continuation and forfeit.
        o = Eval(state, Shot(first: 4, rail: true, pocketed: new[] { 4, 5 }));
        report.Check(o.ShooterContinues && o.Next.Turn == Side.Player && o.Next.Remaining(BallGroup.Solids) == 5, "legal pots keep the turn");
        RulesState forfeited = EightBallRules.Forfeit(state, Side.Player);
        report.Check(forfeited.IsOver && forfeited.Winner == Side.Bot && EightBallRules.ReasonText(forfeited) == "You forfeited.", "forfeit = loss");

        return report.ToString();
    }

    private static RulesOutcome Eval(RulesState state, ShotResult shot) => EightBallRules.Evaluate(state, shot);

    private static RulesState AfterBreak(Side turn)
    {
        RulesState state = RulesState.NewRack(turn);
        state.IsBreakShot = false;
        state.BallInHand = false;
        state.KitchenOnly = false;
        return state;
    }

    private static RulesState WithGroups(Side turn, BallGroup shooterGroup)
    {
        RulesState state = AfterBreak(turn);
        state.Groups[(int)turn] = shooterGroup;
        state.Groups[(int)RulesState.Other(turn)] = BallGroups.Other(shooterGroup);
        return state;
    }

    private static ShotResult Shot(int first, bool rail, int[] pocketed = null)
    {
        var result = new ShotResult { FirstContactBall = first, AnyCushionAfterContact = rail };
        if (pocketed != null)
        {
            foreach (int number in pocketed)
            {
                result.Pocketed.Add(new PocketEvent { Number = number });
                if (number == 0)
                    result.CueBallPocketed = true;
            }
        }
        return result;
    }
}
