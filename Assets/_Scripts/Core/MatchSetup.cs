using UnityEngine;

public enum MatchMode
{
    VsBot = 0,
    Tutorial = 1,
    Practice = 2      // TRY a cue: three practice shots, no rank (v4 V9)
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
    // TRY (v4 V9): the cue to practise with; and, coming back from it, whether the menu opens the cues again.
    public static string PracticeCueId = "";
    public static bool OpenCuesOnMenu;

    // A ranked match in `division` (v3 V10): a random character of it, in its venue.
    public static void Ranked(DivisionDefinition division)
    {
        Mode = MatchMode.VsBot;
        DivisionId = division.Id;
        OpponentId = division.Opponents.Count > 0 ? division.Opponents[Random.Range(0, division.Opponents.Count)].Id : "";
        VenueId = division.VenueId;
    }

    // The next ranked match, chosen when the last one ends so NEXT MATCH can name the opponent (v4 V13):
    // another character of the division when there is one.
    public static OpponentDefinition PrepareNext(DivisionDefinition division, string avoidOpponentId)
    {
        Ranked(division);
        if (division.Opponents.Count > 1)
        {
            var others = division.Opponents.FindAll(o => o.Id != avoidOpponentId);
            OpponentId = others[Random.Range(0, others.Count)].Id;
        }
        return division.Opponents.Find(o => o.Id == OpponentId);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Mode = MatchMode.VsBot;
        VenueId = DefaultVenueId;
        DivisionId = "";
        OpponentId = "";
        PracticeCueId = "";
        OpenCuesOnMenu = false;
    }
}
