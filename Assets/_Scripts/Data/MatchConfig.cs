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
    [Tooltip("Width of the cushion opening at a corner pocket (along each rail).")]
    public float CornerMouth = 0.115f;
    [Tooltip("Width of the cushion opening at a side pocket.")]
    public float SideMouth = 0.13f;
    [Tooltip("How far the angled jaw segments reach back from the rail.")]
    public float JawDepth = 0.05f;
    [Tooltip("Corner pocket centre offset outward from the play-area corner along the diagonal.")]
    public float CornerPocketInset = 0.018f;
    [Tooltip("Side pocket centre offset outward from the rail.")]
    public float SidePocketInset = 0.03f;
    public float HeadStringZ = -0.56f;
    public float FootSpotZ = 0.56f;

    [Header("Physics")]
    public float TimeStep = 0.001f;
    public int MaxStepsPerFrame = 40;
    [Tooltip("Constant rolling resistance, m/s^2.")]
    public float RollDecel = 0.18f;
    [Tooltip("Speed-proportional damping, 1/s.")]
    public float LinearDamping = 0.12f;
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
    public float LookPitchPerPixel = 0.12f;

    [Header("Camera")]
    public float FieldOfView = 55f;
    public float CameraBlendTime = 0.45f;
    public float AimDistance = 0.55f;
    public float AimLowHeight = 0.12f;
    public float AimLowPitch = 10f;
    public float AimHighHeight = 0.55f;
    public float AimHighPitch = 35f;
    [Range(0f, 1f)] public float AimDefaultTilt = 0.25f;
    public float TableHeight = 0.8f;
    public float LookEyeHeight = 1.55f;
    public float LookRadius = 1.9f;
    public float LookMinPitch = 25f;
    public float LookMaxPitch = 80f;
    public float PlacePitch = 75f;
    public float FollowPitch = 60f;
    public float OpponentHeight = 1.0f;
    public float OpponentPitch = 40f;
    public float OpponentDistance = 1.1f;

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
}
