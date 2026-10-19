using UnityEngine;

public enum MatchMode
{
    VsBot = 0
}

// What the next Match scene plays: set by the menu (or Play Again) right before loading it.
public static class MatchSetup
{
    public static MatchMode Mode = MatchMode.VsBot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Mode = MatchMode.VsBot;
}
