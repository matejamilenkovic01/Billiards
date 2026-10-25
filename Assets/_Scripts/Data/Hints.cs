// What a coach mark points at (gameplay redesign G10, v3 V2/V9): the UI finds the control by this id.
public enum CoachTarget
{
    None = -1,
    // A gesture over the whole table (orbit + pinch), without darkening it.
    FreeLook = 0,
    Power = 1,          // the power strip's grip: a finger drags down
    FineAim = 2,        // the fine aim wheel: a finger drags sideways
    Balloon = 3,        // text only, mid screen
    RankBadge = 4,      // menu: the rank badge
    Objectives = 5,     // menu: the objectives
    PocketMap = 6       // v4: the pocket map (calling a pocket)
}

// One-time hints, a bit each in the HintsShown profile mask.
[System.Flags]
public enum HintId
{
    None = 0,
    FreeLook = 1 << 0,      // the first opponent turn: you can look around
    LookMode = 1 << 1,      // the first press of LOOK
    BallInHand = 1 << 2,    // the first ball in hand
    Groups = 1 << 3,        // the first group assignment
    EightOnly = 1 << 4,     // the first time only the 8 is left
    FineAim = 1 << 5,       // five shots without FINE AIM
    GreenRelease = 1 << 6,  // two Red releases in a row
    PowerMiss = 1 << 7,     // the first hard shot (> 85%) that missed
    StarLost = 1 << 8,      // the first loss that took a star
    ClaimObjective = 1 << 9,// the first finished objective
    MenuRank = 1 << 10,     // FTUE: the rank badge on the menu
    MenuObjectives = 1 << 11,// FTUE: the objectives on the menu
    PocketCall = 1 << 12,   // v4: the first shot that must be called
    PocketChoice = 1 << 13  // v4: the first time the player blocks / picks a pocket for the opponent's 8
}
