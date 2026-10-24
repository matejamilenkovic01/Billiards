using UnityEngine;

// Cue ratings 0-99 computed from the cue's data (v3 V11), never typed in: AIM from the aim line, CONTROL from the
// green + yellow width, TIMING from the bar's slowness; OVR weighs them 0.3 / 0.4 / 0.3.
public static class CueRatings
{
    private const float AimMinDiameters = 2f, AimMaxDiameters = 10f;
    private const float ControlMin = 0.40f, ControlMax = 0.47f;   // House and Legend green + yellow
    private const float TimingMin = 1.30f, TimingMax = 2.00f;     // bar period, s

    public static int Aim(CueDefinition cue) => Rate(cue.ObjectGuideDiameters, AimMinDiameters, AimMaxDiameters, 40f, 95f);
    public static int Control(CueDefinition cue) => Rate(cue.GreenHalf + cue.YellowWidth, ControlMin, ControlMax, 45f, 95f);
    public static int Timing(CueDefinition cue) => Rate(cue.OscillationPeriod, TimingMin, TimingMax, 45f, 95f);

    public static int Overall(CueDefinition cue) =>
        Mathf.RoundToInt(0.3f * Aim(cue) + 0.4f * Control(cue) + 0.3f * Timing(cue));

    private static int Rate(float value, float min, float max, float low, float high) =>
        Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(low, high, Mathf.InverseLerp(min, max, value))), 0, 99);
}
