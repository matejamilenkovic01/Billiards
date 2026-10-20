// What a coach mark points at (gameplay redesign G10, v3 V2/V9): the UI finds the control by this id.
public enum CoachTarget
{
    // A gesture over the whole table (orbit + pinch), without darkening it.
    FreeLook = 0
}

// One-time hints, a bit each in the HintsShown profile mask.
[System.Flags]
public enum HintId
{
    None = 0,
    FreeLook = 1 << 0,   // the first opponent turn: you can look around
    LookMode = 1 << 1    // the first press of LOOK
}
