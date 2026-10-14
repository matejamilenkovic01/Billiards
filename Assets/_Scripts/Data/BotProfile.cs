using UnityEngine;

[CreateAssetMenu(fileName = "BotProfile", menuName = "Billiards/Bot Profile")]
public class BotProfile : ScriptableObject
{
    [Header("Release distribution (balance key)")]
    [Range(0f, 1f)] public float GreenChance = 0.45f;
    [Range(0f, 1f)] public float YellowChance = 0.40f;
    [Range(0f, 1f)] public float RedChance = 0.15f;

    [Header("Bot cue (Club Cue values)")]
    public float GreenHalf = 0.13f;
    public float YellowWidth = 0.30f;

    [Header("Shot choice")]
    public float WeightCut = 1.0f;
    public float WeightCueDistance = 0.35f;
    public float WeightPocketDistance = 0.55f;
    public float WeightPocketAngle = 0.4f;
    public float MaxCutAngle = 75f;
    public int CandidatesToSimulate = 5;
    public int BallInHandSamples = 60;
    [Tooltip("Extra speed on top of what the object ball needs to reach the pocket.")]
    public float SpeedMargin = 0.35f;
    public float MinShotSpeed = 0.6f;
    public float BreakPowerMin = 0.95f;
    public float BreakPowerMax = 1f;
    public float BreakSpreadDegrees = 1.5f;

    [Header("Turn timing (seconds, scaled time)")]
    public float ThinkDuration = 3.5f;
    public float ThinkJitter = 0.7f;
    public float AimEnd = 6.5f;
    public float PullEnd = 8.5f;
    public float StrikeAt = 9.0f;
    public float CueBallMoveDuration = 0.6f;

    public float Total => GreenChance + YellowChance + RedChance;
}
