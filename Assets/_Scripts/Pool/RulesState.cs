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
    Forfeit = 4
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
            ReasonFoul = ReasonFoul
        };
        copy.Groups[0] = Groups[0];
        copy.Groups[1] = Groups[1];
        System.Array.Copy(Pocketed, copy.Pocketed, Pocketed.Length);
        return copy;
    }

    public static RulesState NewRack(Side breaker) => new() { Turn = breaker };
}
