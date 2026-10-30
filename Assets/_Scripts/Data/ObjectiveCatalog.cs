using System;
using System.Collections.Generic;
using UnityEngine;

// What an objective counts (v3 V12, v4 V12). Skill objectives, never daily: progress adds up over matches.
// "InDivision" kinds only count in matches played in the division at index Param or higher, so an old set can't get stuck.
public enum ObjectiveKind
{
    WinMatches = 0,          // Target wins
    PocketBalls = 1,         // Target of your own balls (or any on an open table)
    PerfectReleases = 2,     // Target PERFECT releases
    WinWithoutFoul = 3,      // Target wins without a foul
    TwoBallsOneShot = 4,     // Target shots that pot 2+ of your balls
    FineAimPots = 5,         // Target pots on shots where you used FINE AIM
    PerfectStreak = 6,       // Target PERFECT releases in a row (in one match)
    ReachDivision = 7,       // reach the division at index Param
    LongPot = 8,             // Target pots of a ball that travelled more than Param metres
    WinStreak = 9,           // Target wins in a row
    CutPots = 10,            // Target pots with a cut of Param degrees or more
    BankShot = 11,           // Target pots of a ball that touched a cushion first
    WinWithOpponentLeft = 12,// Target wins with Param+ opponent balls still on the table
    RunOut = 13,             // Target visits that clear your whole group (3+ balls)
    WinsAtDivision = 14,     // Target wins at division index Param or higher
    DrawShots = 15,          // Target pots where the cue ball came back Param metres or more
    FollowPots = 16,         // Target pots with follow (b >= 0.25) where the cue ball kept rolling 10 cm+
    StunPots = 17,           // Target straight pots (10 degrees or less) where the cue ball stopped within Param metres
    SideSpinPots = 18,       // Target pots with side spin (|a| >= 0.25)
    CalledPots = 19,         // Target own balls potted in the pocket you called
    CalledEightWins = 20,    // Target wins by potting the 8 in the pocket you called
    VisitPots = 21,          // Target visits that pot Param or more balls
    PocketBallsInDivision = 22,
    PerfectReleasesInDivision = 23,
    WinStreakInDivision = 24 // Target wins in a row, the last one played at division index Param or higher
}

[Serializable]
public class ObjectiveDefinition
{
    public string Id;
    [Tooltip("Shown to the player (EN).")]
    public string Text;
    public ObjectiveKind Kind;
    public int Target = 1;
    public float Param;
    public int Reward = 50;
}

// One division's set of objectives (v4 V12): unlocked by entering the division, a bonus when all are claimed.
[Serializable]
public class ObjectiveSet
{
    public string DivisionId;
    public int Bonus;
    public List<ObjectiveDefinition> Objectives = new();
}

// The objective sets, Bronze first (v4 V12): three objectives are shown at a time, progress counts for all that are
// unlocked. Grand Champion has no set.
[CreateAssetMenu(fileName = "ObjectiveCatalog", menuName = "Billiards/Objective Catalog")]
public class ObjectiveCatalog : ScriptableObject
{
    public const int Slots = 3;
    public List<ObjectiveSet> Sets = new();

    public IEnumerable<ObjectiveDefinition> All
    {
        get
        {
            foreach (ObjectiveSet set in Sets)
            {
                foreach (ObjectiveDefinition objective in set.Objectives)
                    yield return objective;
            }
        }
    }

    public int Count
    {
        get
        {
            int count = 0;
            foreach (ObjectiveSet set in Sets)
                count += set.Objectives.Count;
            return count;
        }
    }

    public ObjectiveDefinition Get(string id)
    {
        foreach (ObjectiveDefinition objective in All)
        {
            if (objective.Id == id)
                return objective;
        }
        return null;
    }

    // The division (set index) an objective belongs to; -1 if unknown.
    public int DivisionOf(string id)
    {
        for (int s = 0; s < Sets.Count; s++)
        {
            if (Sets[s].Objectives.Exists(o => o.Id == id))
                return s;
        }
        return -1;
    }

    public ObjectiveSet SetOf(int division) => division >= 0 && division < Sets.Count ? Sets[division] : null;
}
