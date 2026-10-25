using UnityEngine;

// Hand-built shot results for every rule in implementation_definition 4.6.2 (Standard, the Bronze rule), and the
// division rules of implementation_definition_iteration_v4 V4.
public static class RulesChecks
{
    public static string All() => Standard() + "\n" + DivisionRules() + "\n" + BotRespectsRules(200);

    public static string Standard()
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

    // v4 V4: the five division rules (pocket indexes 0..5, the 8 is ball 8).
    public static string DivisionRules()
    {
        var report = new CheckReport("RulesChecks.DivisionRules");

        // Silver: call the 8.
        RulesState silver = OnEight(RuleSetId.CallEight);
        report.Check(silver.NeedsCall(Side.Player) && !silver.NeedsOpponentEightChoice(Side.Player), "CallEight: on the 8 the shooter calls, nobody chooses for them");
        silver.CalledPocket = 2;
        RulesOutcome o = Eval(silver, Shot(8, true, new[] { 8 }, new[] { 2 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Player && o.Next.Reason == EndReason.EightPocketed, "CallEight_RightPocketWins");
        o = Eval(silver, Shot(8, true, new[] { 8 }, new[] { 4 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Bot && o.Next.Reason == EndReason.EightWrongPocket, "CallEight_WrongPocketLoses");
        report.Check(EightBallRules.ReasonText(o.Next) == "You pocketed the 8 in the wrong pocket.", "CallEight: wrong pocket reason text");
        report.Check(o.Next.CalledPocket == -1, "the call is cleared by the shot");
        RulesState silverGroup = WithGroups(Side.Player, BallGroup.Solids);
        silverGroup.Rules = RuleSetId.CallEight;
        report.Check(!silverGroup.NeedsCall(Side.Player), "CallEight_NoCallNeededForGroup");

        // Gold: the opponent blocks one pocket for your 8.
        RulesState gold = OnEight(RuleSetId.BlockedPocket);
        report.Check(gold.NeedsOpponentEightChoice(Side.Player) && !gold.NeedsCall(Side.Player), "Blocked: on the 8, the opponent must block first; no call");
        RulesState goldGroup = WithGroups(Side.Player, BallGroup.Solids);
        goldGroup.Rules = RuleSetId.BlockedPocket;
        report.Check(!goldGroup.NeedsOpponentEightChoice(Side.Player), "Blocked_ChosenOnceOnFirstEightTurn: not before the group is cleared");
        gold.EightPocket[(int)Side.Player] = 5;
        report.Check(!gold.NeedsOpponentEightChoice(Side.Player), "Blocked_ChosenOnceOnFirstEightTurn: once chosen, never again");
        o = Eval(gold, Shot(8, true));
        report.Check(!o.MatchOver && o.Next.Turn == Side.Bot && o.Next.EightPocket[(int)Side.Player] == 5 && !o.Next.NeedsOpponentEightChoice(Side.Player),
            "Blocked_PersistsAfterMiss");
        o = Eval(gold, Shot(8, true, new[] { 8 }, new[] { 5 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Bot && o.Next.Reason == EndReason.EightBlockedPocket, "Blocked_EightInBlockedLoses");
        report.Check(EightBallRules.ReasonText(o.Next) == "You pocketed the 8 in a blocked pocket.", "Blocked: reason text");
        o = Eval(gold, Shot(8, true, new[] { 8 }, new[] { 1 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Player && o.Next.Reason == EndReason.EightPocketed, "Blocked_EightInOtherPocketWins");
        report.Check(!gold.EightPocketAllowed(Side.Player, 5) && gold.EightPocketAllowed(Side.Player, 0), "Blocked: allowed pockets");

        // Platinum: the opponent picks the pocket for your 8.
        RulesState platinum = OnEight(RuleSetId.OpponentCallsEight);
        report.Check(platinum.NeedsOpponentEightChoice(Side.Player) && !platinum.NeedsCall(Side.Player), "OpponentCalls: the opponent picks, the shooter does not call");
        platinum.EightPocket[(int)Side.Player] = 3;
        o = Eval(platinum, Shot(8, true, new[] { 8 }, new[] { 3 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Player, "OpponentCalls_EightInPickedWins");
        o = Eval(platinum, Shot(8, true, new[] { 8 }, new[] { 0 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Bot && o.Next.Reason == EndReason.EightWrongPocket, "OpponentCalls_EightElsewhereLoses");
        o = Eval(platinum, Shot(8, true));
        report.Check(o.Next.EightPocket[(int)Side.Player] == 3 && !o.Next.NeedsOpponentEightChoice(Side.Player), "OpponentCalls_ChosenOnce");

        // Diamond: call every shot.
        RulesState diamond = WithGroups(Side.Player, BallGroup.Solids);
        diamond.Rules = RuleSetId.CallAll;
        report.Check(diamond.NeedsCall(Side.Player), "CallAll: every shot after the break is called");
        diamond.CalledPocket = 1;
        o = Eval(diamond, Shot(2, true, new[] { 2 }, new[] { 1 }));
        report.Check(o.Foul == FoulType.None && o.ShooterContinues, "CallAll_OwnBallInCalledContinues");
        o = Eval(diamond, Shot(2, true, new[] { 2 }, new[] { 4 }));
        report.Check(o.Foul == FoulType.None && !o.ShooterContinues && !o.Next.BallInHand && o.Next.Turn == Side.Bot && o.Next.Pocketed[2],
            "CallAll_OwnBallInOtherPassesNoFoul (the ball stays down)");
        RulesState diamondOpen = AfterBreak(Side.Player);
        diamondOpen.Rules = RuleSetId.CallAll;
        diamondOpen.CalledPocket = 0;
        o = Eval(diamondOpen, Shot(3, true, new[] { 12, 3 }, new[] { 2, 0 }));
        report.Check(o.GroupsAssigned && o.Next.GroupOf(Side.Player) == BallGroup.Solids && o.ShooterContinues, "CallAll_OpenTableGroupFromCalledPocket");
        RulesState diamondBreak = RulesState.NewRack(Side.Player, RuleSetId.CallAll);
        report.Check(!diamondBreak.NeedsCall(Side.Player), "CallAll_BreakExempt");
        o = Eval(diamondBreak, Shot(1, true, new[] { 4 }, new[] { 3 }));
        report.Check(o.ShooterContinues, "CallAll: anything down on the break keeps the turn");
        RulesState diamondEight = OnEight(RuleSetId.CallAll);
        report.Check(diamondEight.NeedsCall(Side.Player), "CallAll: the 8 is called too");
        diamondEight.CalledPocket = 2;
        o = Eval(diamondEight, Shot(8, true, new[] { 8 }, new[] { 3 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Bot && o.Next.Reason == EndReason.EightWrongPocket, "CallAll_EightWrongPocketLoses");

        // Grand Champion: the group is called, the 8's pocket is the opponent's pick.
        RulesState champion = WithGroups(Side.Player, BallGroup.Solids);
        champion.Rules = RuleSetId.CallAllOpponentEight;
        RulesState championEight = OnEight(RuleSetId.CallAllOpponentEight);
        report.Check(champion.NeedsCall(Side.Player) && !championEight.NeedsCall(Side.Player) && championEight.NeedsOpponentEightChoice(Side.Player),
            "ChampionRules_GroupCalledEightPicked");
        championEight.EightPocket[(int)Side.Player] = 4;
        o = Eval(championEight, Shot(8, true, new[] { 8 }, new[] { 4 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Player, "ChampionRules: the 8 in the picked pocket wins");
        o = Eval(championEight, Shot(8, true, new[] { 8 }, new[] { 1 }));
        report.Check(o.MatchOver && o.Next.Winner == Side.Bot && o.Next.Reason == EndReason.EightWrongPocket, "ChampionRules: elsewhere loses");

        // For every rule: a foul with the 8 loses; the 8 on the break is re-spotted.
        foreach (RuleSetId rules in System.Enum.GetValues(typeof(RuleSetId)))
        {
            RulesState state = OnEight(rules);
            state.EightPocket[(int)Side.Player] = 3;
            state.CalledPocket = 3;
            o = Eval(state, Shot(8, true, new[] { 8, 0 }, new[] { 3, 1 }));
            report.Check(o.MatchOver && o.Next.Winner == Side.Bot && o.Next.Reason == EndReason.EightWithFoul, $"Rules_FoulWithEightStillLoses ({rules})");
            RulesState rack = RulesState.NewRack(Side.Player, rules);
            o = Eval(rack, Shot(1, true, new[] { 8 }, new[] { 0 }));
            report.Check(o.RespotEight && !o.MatchOver, $"Rules_EightOnBreakRespotted ({rules})");
        }
        return report.ToString();
    }

    // v4 V4: random positions for every rule; the bot's chosen shot never targets a blocked or forbidden pocket with
    // the 8, its call is its candidate's pocket, and its block / pick is a real pocket chosen once.
    public static string BotRespectsRules(int statesPerRule)
    {
        var report = new CheckReport("RulesChecks.BotRespectsRules");
        MatchConfig config = PhysicsChecks.Config;
        PoolSettings pool = config.ToPoolSettings();
        var table = new TableGeometry(pool);
        var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<BotProfile>(DataAssetBuilder.BotProfilePath);
        var brain = new BotBrain(table, pool, profile.ToBotSettings(config));
        int wrongEight = 0, badCalls = 0, missingCalls = 0, badChoices = 0, eightShots = 0, calledShots = 0;
        foreach (RuleSetId rules in System.Enum.GetValues(typeof(RuleSetId)))
        {
            var random = new System.Random(77 + (int)rules);
            for (int i = 0; i < statesPerRule; i++)
            {
                BallState[] balls = PhysicsChecks.Scatter(random, table);
                // Half the states on the 8 (player Solids cleared), half mid-game.
                bool onEight = i % 2 == 0;
                RulesState state = WithGroups(Side.Player, BallGroup.Solids);
                state.Rules = rules;
                for (int n = 1; n <= 15; n++)
                {
                    bool down = n != 8 && (onEight ? n <= 7 || random.NextDouble() < 0.5 : random.NextDouble() < 0.4);
                    state.Pocketed[n] = down;
                    balls[n].Pocketed = down;
                }
                if (state.Remaining(BallGroup.Solids) == 0 && !onEight)
                    continue;
                if (state.NeedsOpponentEightChoice(Side.Player))
                {
                    int pocket = brain.ChooseEightPocketForOpponent(state, balls, Side.Player, random);
                    if (pocket < 0 || pocket >= table.Pockets.Length)
                        badChoices++;
                    state.EightPocket[(int)Side.Player] = pocket;
                }
                BotDecision decision = brain.ChooseShot(state, balls, random);
                if (decision.TargetBall == BallGroups.EightBall && decision.TargetPocket >= 0)
                {
                    eightShots++;
                    if (!state.EightPocketAllowed(Side.Player, decision.TargetPocket))
                        wrongEight++;
                }
                if (state.NeedsCall(Side.Player))
                {
                    calledShots++;
                    if (decision.TargetPocket < 0)
                        missingCalls++;
                    else if (!decision.IsSafety && decision.TargetBall > 0 && decision.TargetPocket >= table.Pockets.Length)
                        badCalls++;
                }
            }
        }
        report.Info($"{statesPerRule} states per rule: {eightShots} shots on the 8, {calledShots} called shots");
        report.Check(wrongEight == 0, $"never the 8 into a blocked / not picked pocket ({wrongEight})");
        report.Check(missingCalls == 0 && badCalls == 0, $"every called shot has a call = its candidate's pocket ({missingCalls} missing, {badCalls} bad)");
        report.Check(badChoices == 0, $"every block / pick is a pocket ({badChoices} bad)");
        return report.ToString();
    }

    // v4 V4: whole headless matches per rule (bot against bot), no exception and no match stuck at the shot cap.
    public static string BotVsBot(int matchesPerRule = 20)
    {
        var report = new CheckReport("RulesChecks.BotVsBot");
        MatchConfig config = PhysicsChecks.Config;
        PoolSettings pool = config.ToPoolSettings();
        var table = new TableGeometry(pool);
        var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<BotProfile>(DataAssetBuilder.BotProfilePath);
        BotSettings bot = profile.ToBotSettings(config);
        foreach (RuleSetId rules in System.Enum.GetValues(typeof(RuleSetId)))
        {
            int stuck = 0, errors = 0, shotsTotal = 0;
            var reasons = new System.Collections.Generic.Dictionary<EndReason, int>();
            for (int m = 0; m < matchesPerRule; m++)
            {
                try
                {
                    var recorder = new MatchStatsRecorder();
                    Side? winner = BotBalanceCheck.PlayMatch(500 + m, table, pool, bot, bot, out int shots, out _, recorder, 0f, config.BankIgnoreBeforeDrop, null, rules);
                    shotsTotal += shots;
                    if (winner == null)
                        stuck++;
                    EndReason reason = recorder.LastReason;
                    reasons[reason] = reasons.TryGetValue(reason, out int n) ? n + 1 : 1;
                }
                catch (System.Exception e)
                {
                    errors++;
                    Debug.LogException(e);
                }
            }
            var text = new System.Text.StringBuilder();
            foreach (var pair in reasons)
                text.Append($" {pair.Key} {pair.Value}");
            report.Info($"{rules}: {matchesPerRule} matches, avg {shotsTotal / (float)matchesPerRule:F1} shots; ends:{text}");
            report.Check(errors == 0 && stuck == 0, $"{rules}: no exception ({errors}), no match stuck ({stuck})");
        }
        return report.ToString();
    }

    private static RulesState OnEight(RuleSetId rules)
    {
        RulesState state = WithGroups(Side.Player, BallGroup.Solids);
        state.Rules = rules;
        for (int n = 1; n <= 7; n++)
            state.Pocketed[n] = true;
        return state;
    }

    private static ShotResult Shot(int first, bool rail, int[] pocketed, int[] pockets)
    {
        ShotResult result = Shot(first, rail, pocketed);
        for (int i = 0; i < result.Pocketed.Count; i++)
        {
            PocketEvent e = result.Pocketed[i];
            e.PocketIndex = pockets[i];
            result.Pocketed[i] = e;
        }
        return result;
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
