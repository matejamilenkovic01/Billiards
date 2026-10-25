public enum FoulType
{
    None = 0,
    CueBallPocketed = 1,
    NoBallHit = 2,
    WrongBallFirst = 3,
    NoRailAfterContact = 4
}

public enum Side
{
    Player = 0,
    Bot = 1
}

public enum EndReason
{
    None = 0,
    EightPocketed = 1,     // legal 8: the shooter wins
    EightTooEarly = 2,     // 8 before the group was cleared: the shooter loses
    EightWithFoul = 3,     // 8 together with a foul: the shooter loses
    Forfeit = 4,
    EightWrongPocket = 5,  // v4: the 8 in a pocket that was not called / not the one picked: the shooter loses
    EightBlockedPocket = 6 // v4: the 8 in the blocked pocket: the shooter loses
}

// The 8-ball rule of a division (implementation_definition_iteration_v4 V4). The break is never called.
public enum RuleSetId
{
    Standard = 0,              // Bronze: the 8 in any pocket
    CallEight = 1,             // Silver: call the pocket for the 8
    BlockedPocket = 2,         // Gold: the opponent blocks one pocket for your 8
    OpponentCallsEight = 3,    // Platinum: the opponent picks the pocket for your 8
    CallAll = 4,               // Diamond: call every shot, the 8 too
    CallAllOpponentEight = 5   // Grand Champion: call every shot; the opponent picks your 8's pocket
}

// What the opponent does with the pocket for a side's 8 (v4 V4).
public enum PocketChoiceKind
{
    Block = 0,   // Gold
    Pick = 1     // Platinum, Grand Champion
}

// What a pocket marker means (the call for this shot, or the opponent's choice for the 8).
public enum PocketCallKind
{
    Called = 0,
    Blocked = 1,
    Assigned = 2
}

// Everything the rules track between shots.
public sealed class RulesState
{
    public Side Turn;
    public readonly BallGroup[] Groups = new BallGroup[2];
    public bool IsBreakShot = true;
    public bool BallInHand = true;
    public bool KitchenOnly = true;
    public readonly bool[] Pocketed = new bool[BallGroups.BallCount];

    public RuleSetId Rules;
    // Per side: the pocket the opponent blocked (Gold) or picked (Platinum, GC) for that side's 8; -1 = not yet.
    // Chosen once, the first time the side starts a turn on the 8, and kept to the end of the match.
    public readonly int[] EightPocket = { -1, -1 };
    // The shooter's called pocket for this shot (-1 = none); cleared by every shot.
    public int CalledPocket = -1;

    public bool IsOver;
    public Side Winner;
    public EndReason Reason;
    public Side ReasonSide;      // whose shot ended the match (or who forfeited)
    public FoulType ReasonFoul;

    public static Side Other(Side side) => side == Side.Player ? Side.Bot : Side.Player;

    public bool IsTableOpen => Groups[0] == BallGroup.Open;

    public BallGroup GroupOf(Side side) => Groups[(int)side];

    public int Remaining(BallGroup group)
    {
        if (group == BallGroup.Open)
            return 7;
        int first = BallGroups.FirstNumber(group);
        int count = 0;
        for (int number = first; number < first + 7; number++)
        {
            if (!Pocketed[number])
                count++;
        }
        return count;
    }

    // The shooter's group is cleared, so the 8 is their only target.
    public bool IsOnEight(Side side) => GroupOf(side) != BallGroup.Open && Remaining(GroupOf(side)) == 0;

    public bool IsLegalTarget(Side side, int number)
    {
        if (number <= 0 || number >= BallGroups.BallCount || Pocketed[number])
            return false;
        if (IsBreakShot)
            return true;
        if (IsOnEight(side))
            return number == BallGroups.EightBall;
        if (IsTableOpen)
            return number != BallGroups.EightBall;
        return BallGroups.Contains(GroupOf(side), number);
    }

    public bool OpponentChoosesEight => Rules is RuleSetId.BlockedPocket or RuleSetId.OpponentCallsEight or RuleSetId.CallAllOpponentEight;
    public bool OpponentPicksEight => Rules is RuleSetId.OpponentCallsEight or RuleSetId.CallAllOpponentEight;
    public bool CallsGroupShots => Rules is RuleSetId.CallAll or RuleSetId.CallAllOpponentEight;

    // The shooter must call a pocket before this shot.
    public bool NeedsCall(Side side)
    {
        if (IsBreakShot || IsOver)
            return false;
        bool onEight = IsOnEight(side);
        return Rules switch
        {
            RuleSetId.CallEight => onEight,
            RuleSetId.CallAll => true,
            RuleSetId.CallAllOpponentEight => !onEight,
            _ => false
        };
    }

    // The opponent has to block or pick the pocket for this side's 8 before it shoots.
    public bool NeedsOpponentEightChoice(Side side) =>
        OpponentChoosesEight && !IsBreakShot && !IsOver && IsOnEight(side) && EightPocket[(int)side] < 0;

    // May `side` pot the 8 into `pocket` (before calling)?
    public bool EightPocketAllowed(Side side, int pocket)
    {
        int chosen = EightPocket[(int)side];
        if (chosen < 0)
            return true;
        return Rules == RuleSetId.BlockedPocket ? pocket != chosen : pocket == chosen;
    }

    public void FillLegalTargets(Side side, bool[] mask)
    {
        for (int i = 0; i < mask.Length; i++)
            mask[i] = IsLegalTarget(side, i);
    }

    public RulesState Clone()
    {
        var copy = new RulesState
        {
            Turn = Turn,
            IsBreakShot = IsBreakShot,
            BallInHand = BallInHand,
            KitchenOnly = KitchenOnly,
            IsOver = IsOver,
            Winner = Winner,
            Reason = Reason,
            ReasonSide = ReasonSide,
            ReasonFoul = ReasonFoul,
            Rules = Rules,
            CalledPocket = CalledPocket
        };
        copy.EightPocket[0] = EightPocket[0];
        copy.EightPocket[1] = EightPocket[1];
        copy.Groups[0] = Groups[0];
        copy.Groups[1] = Groups[1];
        System.Array.Copy(Pocketed, copy.Pocketed, Pocketed.Length);
        return copy;
    }

    public static RulesState NewRack(Side breaker, RuleSetId rules = RuleSetId.Standard) => new() { Turn = breaker, Rules = rules };
}
