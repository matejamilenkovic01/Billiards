using UnityEngine;

public enum MatchMode
{
    VsBot = 0
}

// What the next Match scene plays: set by the menu (or Play Again) right before loading it.
public static class MatchSetup
{
    public static MatchMode Mode = MatchMode.VsBot;
    // The venue theme for the match (v3 V6): the division's venue, set by the menu.
    public const string DefaultVenueId = "riverside";
    public static string VenueId = DefaultVenueId;
    // The ranked division the match counts for and the CPU character (v3 V10); empty = the player's current division.
    public static string DivisionId = "";
    public static string OpponentId = "";

    // A ranked match in `division` (v3 V10): a random character of it, in its venue.
    public static void Ranked(DivisionDefinition division)
    {
        Mode = MatchMode.VsBot;
        DivisionId = division.Id;
        OpponentId = division.Opponents.Count > 0 ? division.Opponents[Random.Range(0, division.Opponents.Count)].Id : "";
        VenueId = division.VenueId;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Mode = MatchMode.VsBot;
        VenueId = DefaultVenueId;
        DivisionId = "";
        OpponentId = "";
    }
}
