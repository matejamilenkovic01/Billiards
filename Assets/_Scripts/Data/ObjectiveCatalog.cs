using System;
using System.Collections.Generic;
using UnityEngine;

// What an objective counts (v3 V12). Skill objectives, never daily: progress adds up over matches.
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
    WinsAtDivision = 14      // Target wins at division index Param or higher
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

// The ordered list of objectives (v3 V12): three are shown at a time; a claimed one is replaced by the next.
[CreateAssetMenu(fileName = "ObjectiveCatalog", menuName = "Billiards/Objective Catalog")]
public class ObjectiveCatalog : ScriptableObject
{
    public const int Slots = 3;
    public List<ObjectiveDefinition> Objectives = new();

    public ObjectiveDefinition Get(string id) => Objectives.Find(o => o.Id == id);
}
