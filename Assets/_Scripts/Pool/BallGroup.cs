public enum BallGroup
{
    Open = 0,
    Solids = 1,
    Stripes = 2
}

public static class BallGroups
{
    public const int CueBall = 0;
    public const int EightBall = 8;
    public const int BallCount = 16;

    public static bool IsSolid(int number) => number >= 1 && number <= 7;

    public static bool IsStripe(int number) => number >= 9 && number <= 15;

    public static bool Contains(BallGroup group, int number) => group switch
    {
        BallGroup.Solids => IsSolid(number),
        BallGroup.Stripes => IsStripe(number),
        _ => false
    };

    public static BallGroup Of(int number) =>
        IsSolid(number) ? BallGroup.Solids : IsStripe(number) ? BallGroup.Stripes : BallGroup.Open;

    public static BallGroup Other(BallGroup group) => group switch
    {
        BallGroup.Solids => BallGroup.Stripes,
        BallGroup.Stripes => BallGroup.Solids,
        _ => BallGroup.Open
    };

    // First ball number of the group's 1..7 / 9..15 run.
    public static int FirstNumber(BallGroup group) => group == BallGroup.Stripes ? 9 : 1;
}
