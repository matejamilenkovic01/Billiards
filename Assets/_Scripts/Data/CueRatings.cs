using UnityEngine;

// Cue ratings 0-99 computed from the cue's data (v3 V11, v4 V9), never typed in: AIM from the aim line, CONTROL from
// the green + yellow width, TIMING from the bar's slowness, POWER from MaxSpeed (4.5 -> 40, 6.5 -> 95, linear);
// OVR weighs them 0.25 / 0.35 / 0.25 / 0.15.
public static class CueRatings
{
    private const float AimMinDiameters = 2f, AimMaxDiameters = 10f;
    private const float ControlMin = 0.40f, ControlMax = 0.47f;   // House and Legend green + yellow
    private const float TimingMin = 1.30f, TimingMax = 2.00f;     // bar period, s
    private const float PowerMin = 4.5f, PowerMax = 6.5f;         // MaxSpeed, m/s

    public static int Aim(CueDefinition cue) => Rate(cue.ObjectGuideDiameters, AimMinDiameters, AimMaxDiameters, 40f, 95f);
    public static int Control(CueDefinition cue) => Rate(cue.GreenHalf + cue.YellowWidth, ControlMin, ControlMax, 45f, 95f);
    public static int Timing(CueDefinition cue) => Rate(cue.OscillationPeriod, TimingMin, TimingMax, 45f, 95f);

    public static int Power(CueDefinition cue) => Rate(cue.MaxSpeed, PowerMin, PowerMax, 40f, 95f);

    public static int Overall(CueDefinition cue) =>
        Mathf.RoundToInt(0.25f * Aim(cue) + 0.35f * Control(cue) + 0.25f * Timing(cue) + 0.15f * Power(cue));

    private static int Rate(float value, float min, float max, float low, float high) =>
        Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(low, high, Mathf.InverseLerp(min, max, value))), 0, 99);
}
