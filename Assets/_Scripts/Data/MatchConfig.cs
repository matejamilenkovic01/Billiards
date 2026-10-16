using UnityEngine;

// Every gameplay number lives here (or in CueDefinition / BotProfile), never in code.
[CreateAssetMenu(fileName = "MatchConfig", menuName = "Billiards/Match Config")]
public class MatchConfig : ScriptableObject
{
    [Header("Table (metres, 8ft)")]
    public float PlayLength = 2.24f;
    public float PlayWidth = 1.12f;
    public float BallRadius = 0.028575f;
    public float CornerPocketRadius = 0.058f;
    public float SidePocketRadius = 0.062f;
    [Tooltip("Corner pocket opening, cushion nose to cushion nose across the mouth.")]
    public float CornerMouth = 0.12f;
    [Tooltip("Width of the cushion opening at a side pocket.")]
    public float SideMouth = 0.13f;
    [Tooltip("How far the angled jaw segments reach back from the rail.")]
    public float JawDepth = 0.05f;
    [Tooltip("Corner pocket centre offset outward from the play-area corner along the diagonal.")]
    public float CornerPocketInset = 0.018f;
    [Tooltip("Side pocket centre offset outward from the rail. Must exceed SidePocketRadius - BallRadius, or balls rolling along the rail drop in.")]
    public float SidePocketInset = 0.04f;
    public float HeadStringZ = -0.56f;
    public float FootSpotZ = 0.56f;

    [Header("Physics")]
    public float TimeStep = 0.001f;
    public int MaxStepsPerFrame = 40;
    [Tooltip("Constant rolling resistance, m/s^2.")]
    public float RollDecel = 0.12f;
    [Tooltip("Speed-proportional damping, 1/s.")]
    public float LinearDamping = 0.09f;
    public float StopSpeed = 0.005f;
    public float BallRestitution = 0.95f;
    public float CushionRestitution = 0.78f;
    public float CushionFriction = 0.1f;
    [Tooltip("Rack gap between neighbouring balls, metres.")]
    public float RackGap = 0.0001f;

    [Header("Shot")]
    public float MinSpeed = 0.35f;
    public float MaxSpeed = 6.5f;
    public float PowerExponent = 1.6f;
    [Tooltip("Pull needed before the release bar starts; releasing below it cancels the shot.")]
    public float PullStartThreshold = 0.05f;
    public float YellowErrorMinDeg = 0.4f;
    public float YellowErrorMaxDeg = 1.6f;
    public float RedErrorMinDeg = 2f;
    public float RedErrorMaxDeg = 5f;
    public float RedPowerVariance = 0.12f;

    [Header("Controls (reference pixels)")]
    public float AimDegreesPerPixel = 0.12f;
    public float FineAimDegreesPerPixel = 0.01f;
    public float KeyFineAimDegreesPerFrame = 0.05f;
    public float AimMagnetDegrees = 0.3f;
    public float PullRangePixels = 800f;
    public float TiltPerPixel = 0.0025f;
    public float LookDegreesPerPixel = 0.25f;

    [Header("Camera (first person)")]
    public float FieldOfView = 60f;
    public float CameraBlendTime = 0.45f;
    [Tooltip("Aiming: how far behind the cue ball your eyes are (horizontal), closest.")]
    public float AimDistance = 0.8f;
    [Tooltip("Aiming: the camera steps back up to this far so the hit ball and its pocket stay in view.")]
    public float AimMaxDistance = 1.9f;
    [Tooltip("Aiming: metres of extra height per metre stepped back.")]
    public float AimRiseSlope = 0.5f;
    public float AimFramingSmooth = 0.3f;
    [Tooltip("Aiming: eye height above the cloth, head down on the cue.")]
    public float AimLowHeight = 0.24f;
    [Tooltip("Aiming: eye height above the cloth, standing more upright.")]
    public float AimHighHeight = 0.62f;
    [Tooltip("Aiming: your eyes rest on a point this far ahead of the cue ball.")]
    public float AimLookAhead = 0.9f;
    [Range(0f, 1f)] public float AimDefaultTilt = 0.2f;
    public float TableHeight = 0.8f;
    [Tooltip("Standing eye height above the cloth (~1.68 m above the floor).")]
    public float StandingEyeHeight = 0.88f;
    [Tooltip("Where you stand: this far outside the rails.")]
    public float StandMargin = 0.35f;
    public float LookMinHeight = 0.35f;
    public float LookMaxHeight = 1.45f;
    public float LookHeightPerPixel = 0.0025f;
    [Tooltip("Seconds after the stroke before you straighten up to watch.")]
    public float StandUpDelay = 0.35f;
    public float StandUpTime = 0.8f;
    public float PlacePitch = 75f;
    [Header("Head motion")]
    public float HeadSwayMetres = 0.003f;
    public float HeadSwayDegrees = 0.25f;
    public float HeadSwaySpeed = 0.3f;
    public float StrikeKick = 0.035f;

    [Header("Cue view")]
    public float CueLength = 1.45f;
    public float CueTipRadius = 0.006f;
    public float CueButtRadius = 0.015f;
    public float CueElevation = 5f;
    public float CueTipGap = 0.012f;
    public float CuePullDistance = 0.28f;
    public float CueStrikeDuration = 0.06f;

    [Header("Ball view")]
    public float PocketDrop = 0.08f;
    public float PocketShrink = 0.8f;
    public float PocketAnimDuration = 0.25f;

    [Header("Guide")]
    public float GuideWidth = 0.006f;
    public float MissGhostDuration = 0.6f;

    [Header("Flow timing (seconds)")]
    public float CoinFlipDuration = 1.2f;
    public float ResolvePause = 0.6f;
    public float EndPopupDelay = 1.0f;

    public float HalfLength => PlayLength * 0.5f;
    public float HalfWidth => PlayWidth * 0.5f;

    public PoolSettings ToPoolSettings() => new()
    {
        HalfWidth = HalfWidth,
        HalfLength = HalfLength,
        BallRadius = BallRadius,
        CornerPocketRadius = CornerPocketRadius,
        SidePocketRadius = SidePocketRadius,
        CornerMouth = CornerMouth,
        SideMouth = SideMouth,
        JawDepth = JawDepth,
        CornerPocketInset = CornerPocketInset,
        SidePocketInset = SidePocketInset,
        HeadStringZ = HeadStringZ,
        FootSpotZ = FootSpotZ,
        TimeStep = TimeStep,
        RollDecel = RollDecel,
        LinearDamping = LinearDamping,
        StopSpeed = StopSpeed,
        BallRestitution = BallRestitution,
        CushionRestitution = CushionRestitution,
        CushionFriction = CushionFriction,
        RackGap = RackGap
    };

    public ShotAccuracy.Settings AccuracySettings(float greenHalf, float yellowWidth) => new()
    {
        GreenHalf = greenHalf,
        YellowWidth = yellowWidth,
        YellowErrorMinDeg = YellowErrorMinDeg,
        YellowErrorMaxDeg = YellowErrorMaxDeg,
        RedErrorMinDeg = RedErrorMinDeg,
        RedErrorMaxDeg = RedErrorMaxDeg,
        RedPowerVariance = RedPowerVariance
    };

    public float SpeedFromPull(float pull01) => ShotAccuracy.SpeedFromPull(pull01, MinSpeed, MaxSpeed, PowerExponent);
}
