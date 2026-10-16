using UnityEngine;

public enum MatchMode
{
    VsBot = 0,
    TwoPlayers = 1
}

// What the next Match scene plays: set by the menu (or Play Again) right before loading it.
public static class MatchSetup
{
    public static MatchMode Mode = MatchMode.VsBot;

    public static bool IsTwoPlayers => Mode == MatchMode.TwoPlayers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Mode = MatchMode.VsBot;
}
