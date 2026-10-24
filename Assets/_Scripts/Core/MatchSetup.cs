using UnityEngine;

public enum MatchMode
{
    VsBot = 0
}

// What the next Match scene plays: set by the menu (or Play Again) right before loading it.
public static class MatchSetup
{
    public static MatchMode Mode = MatchMode.VsBot;
    // The venue theme for the match (v3 V6); the menu sets it from the division later.
    public const string DefaultVenueId = "riverside";
    public static string VenueId = DefaultVenueId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Mode = MatchMode.VsBot;
        VenueId = DefaultVenueId;
    }
}
