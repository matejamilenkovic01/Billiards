using UnityEngine;

[CreateAssetMenu(fileName = "BotProfile", menuName = "Billiards/Bot Profile")]
public class BotProfile : ScriptableObject
{
    [Header("Release timing (balance key)")]
    [Tooltip("The bot lets go this many ms (standard deviation) off the centre of green.")]
    public float TimingSigmaMs = 45f;

    [Header("Bot cue (Club Cue values)")]
    public float GreenHalf = 0.13f;
    public float YellowWidth = 0.30f;
    public float Period = 1.45f;
    [Tooltip("The cue's POWER: cue ball speed at a full pull (v4 V1).")]
    public float MaxSpeed = 5.0f;

    [Header("Shot choice")]
    public float WeightCut = 1.0f;
    public float WeightCueDistance = 0.35f;
    public float WeightPocketDistance = 0.55f;
    public float WeightPocketAngle = 0.4f;
    public float MaxCutAngle = 75f;
    public int CandidatesToSimulate = 5;
    public int BallInHandSamples = 60;
    [Tooltip("Speed (m/s) the object ball should still have when it reaches the pocket.")]
    public float ArrivalSpeed = 0.35f;
    public float MinShotSpeed = 0.6f;
    public float BreakPowerMin = 0.95f;
    public float BreakPowerMax = 1f;
    public float BreakSpreadDegrees = 1.5f;

    [Header("Spin (v4 V1): Gold and up try spin on their best pots")]
    public bool UsesSpin;
    [Tooltip("Tips tried (ball radii): centre, follow, draw, stun.")]
    public Vector2[] SpinVariants = { Vector2.zero, new(0f, 0.4f), new(0f, -0.45f), new(0f, -0.2f) };
    [Tooltip("How many of the best pots that work get the spin variants tried.")]
    public int SpinCandidates = 3;

    [Header("Turn timing (seconds, scaled time)")]
    public float ThinkDuration = 3.5f;
    public float ThinkJitter = 0.7f;
    public float AimEnd = 6.5f;
    public float PullEnd = 8.5f;
    public float StrikeAt = 9.0f;
    public float CueBallMoveDuration = 0.6f;

    public BotSettings ToBotSettings(MatchConfig config) => new()
    {
        WeightCut = WeightCut,
        WeightCueDistance = WeightCueDistance,
        WeightPocketDistance = WeightPocketDistance,
        WeightPocketAngle = WeightPocketAngle,
        MaxCutAngle = MaxCutAngle,
        CandidatesToSimulate = CandidatesToSimulate,
        BallInHandSamples = BallInHandSamples,
        ArrivalSpeed = ArrivalSpeed,
        MinShotSpeed = MinShotSpeed,
        Power = config.PowerCurve(MaxSpeed),
        UsesSpin = UsesSpin,
        SpinVariants = SpinVariants,
        SpinCandidates = SpinCandidates,
        CueElevation = config.CueElevation,
        BreakPowerMin = BreakPowerMin,
        BreakPowerMax = BreakPowerMax,
        BreakSpreadDegrees = BreakSpreadDegrees,
        TimingSigmaMs = TimingSigmaMs,
        Accuracy = config.AccuracySettings(GreenHalf, YellowWidth, Period),
        Scaling = config.ReleaseScaling()
    };
}
