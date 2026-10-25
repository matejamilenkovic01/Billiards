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

    [Header("Table visuals (metres above the cloth / past the cushion nose)")]
    public float CushionDepth = 0.045f;
    [Tooltip("Cushion nose height: ~63% of the ball diameter, like a K-66 profile (the ball touches it there).")]
    public float CushionNoseHeight = 0.036f;
    [Tooltip("Cushion top where it meets the rail.")]
    public float CushionBackHeight = 0.045f;
    [Tooltip("The cushion face slants back this far at the cloth, under the nose.")]
    public float CushionUndercut = 0.008f;
    public float JawThickness = 0.02f;
    public float RailWidth = 0.11f;
    public float RailTop = 0.046f;
    [Tooltip("Pocket hole radius beyond the capture radius.")]
    public float PocketHoleExtra = 0.012f;

    [Header("Physics")]
    public float TimeStep = 0.001f;
    public int MaxStepsPerFrame = 40;
    [Tooltip("Ball-cloth sliding friction (mu). A ball off the cue or off another ball slides until it rolls at 5/7 of that speed.")]
    public float SlideFriction = 0.2f;
    [Tooltip("Slip below this (m/s) counts as rolling.")]
    public float RollSlipEpsilon = 0.005f;
    [Tooltip("Constant rolling resistance, m/s^2.")]
    public float RollDecel = 0.11f;
    [Tooltip("Speed-proportional damping, 1/s.")]
    public float LinearDamping = 0.02f;
    public float StopSpeed = 0.005f;
    public float BallRestitution = 0.95f;
    [Tooltip("Rack gap between neighbouring balls, metres.")]
    public float RackGap = 0.0001f;

    [Header("Spin (v4 V1): tip offsets in ball radii, spins as R·w in m/s")]
    [Tooltip("Furthest the tip may hit from the cue ball's centre, for every cue (no miscue).")]
    public float SpinMaxOffset = 0.5f;
    [Tooltip("Squirt: the cue ball leaves this many degrees off the cue's line per ball radius of side offset, away from the side hit.")]
    public float SquirtDegPerR = 4f;
    [Tooltip("Side spin off the tip = 5/2 x offset x speed x this.")]
    public float SideTransfer = 1f;
    [Tooltip("Side spin wears off at this rate on the cloth (m/s per second).")]
    public float SideSpinDecel = 0.6f;
    [Tooltip("Masse: share of the side spin that tilts into a sideways slip, per sin(elevation) above SwerveMinElevation.")]
    public float SwerveFactor = 0.5f;
    public float SwerveMinElevation = 5f;
    [Tooltip("Ball-ball friction (throw) at a slow slide between the balls...")]
    public float BallFriction = 0.07f;
    [Tooltip("...falling towards this at a fast slide...")]
    public float BallFrictionFast = 0.01f;
    [Tooltip("...by exp(-this x sliding speed in m/s).")]
    public float BallFrictionDecay = 1.1f;
    [Tooltip("Cushion restitution for a slow hit (normal speed 0)...")]
    public float CushionRestitutionSlow = 0.88f;
    [Tooltip("...down to this at CushionFastSpeed (m/s) and above.")]
    public float CushionRestitutionFast = 0.70f;
    public float CushionFastSpeed = 4f;
    [Tooltip("Cushion friction (mu) on the ball's slide along it and its side spin: running english opens the rebound, reverse closes it.")]
    public float CushionSpinFriction = 0.35f;

    [Header("Shot: power curve (v4 V1), the same for every cue up to PowerSharedUntil, then up to the cue's MaxSpeed")]
    public float MinSpeed = 0.35f;
    [Tooltip("Speed at PowerSharedUntil (HARD), m/s.")]
    public float HardSpeed = 3.5f;
    [Range(0.1f, 1f)] public float PowerSharedUntil = 0.85f;
    [Tooltip("1.9: MED (p = 0.5) gives 1.5 m/s.")]
    public float PowerExponent = 1.9f;
    [Tooltip("Pull needed before the release bar starts; releasing below it cancels the shot.")]
    public float PullStartThreshold = 0.05f;
    public float YellowErrorMinDeg = 0.4f;
    public float YellowErrorMaxDeg = 1.6f;
    public float RedErrorMinDeg = 2f;
    public float RedErrorMaxDeg = 5f;
    public float RedPowerVariance = 0.12f;
    [Header("Release window x shot power (v3 V8): cue value x lerp(p=0, p=1)")]
    public float GreenScale0 = 1.6f;
    public float GreenScale1 = 0.55f;
    public float YellowScale0 = 1.2f;
    public float YellowScale1 = 0.85f;
    [Tooltip("Bar period scale: higher = slower.")]
    public float PeriodScale0 = 1.35f;
    public float PeriodScale1 = 0.72f;
    [Tooltip("Scale of the yellow and red angle errors.")]
    public float ErrorScale0 = 0.7f;
    public float ErrorScale1 = 1.35f;

    [Header("Controls (reference pixels)")]
    public float AimDegreesPerPixel = 0.12f;
    public float FineAimDegreesPerPixel = 0.01f;
    public float KeyFineAimDegreesPerFrame = 0.05f;
    public float AimMagnetDegrees = 0.3f;
    [Tooltip("Power strip (v3 V5): the cue's grip moves exactly with the finger; 100% = this many reference pixels, from the grip at rest to 20 px above the strip's bottom.")]
    public float PullTravelPixels = 895f;
    [Tooltip("Only a touch in the top part of the strip (this fraction of its height) grabs the grip.")]
    [Range(0.1f, 1f)] public float PullGrabZone = 0.4f;
    [Tooltip("SOFT / MED / HARD marks sit where the grip is at these pulls.")]
    public Vector3 PowerMarks = new(0.15f, 0.5f, 0.85f);
    public float FineAimStepDegrees = 0.05f;
    [Tooltip("Holding a fine aim arrow repeats this many steps per second...")]
    public float FineAimRepeatRate = 12f;
    [Tooltip("...after this first wait, so a tap is one step...")]
    public float FineAimRepeatDelay = 0.3f;
    [Tooltip("...and after this long held, the step grows up to FineAimMaxStepDegrees.")]
    public float FineAimAccelerateAfter = 0.6f;
    public float FineAimMaxStepDegrees = 0.2f;
    [Tooltip("A light haptic tick for every this many degrees of fine aim.")]
    public float FineAimTickDegrees = 0.1f;
    [Tooltip("The power hint (a hand pulling the cue down) shows until the player has taken this many shots...")]
    public int PowerHintShots = 3;
    [Tooltip("...and again when the strip is left untouched this long while aiming.")]
    public float PowerIdleHintSeconds = 8f;
    public float TiltPerPixel = 0.0025f;

    [Header("Camera (first person)")]
    [Tooltip("Aiming: eyes over the frame stay at least this far above the rail top.")]
    public float AimFrameClearance = 0.05f;
    [Tooltip("Aiming: the line of sight to the cue ball centre passes this far over the cushion and rail.")]
    public float AimSightClearance = 0.005f;
    [Tooltip("Aiming: eyes stay this far above the cue's line where you stand, so a raised butt never crosses the view.")]
    public float AimAboveCue = 0.1f;
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
    [Header("Camera: orbit (LOOK and free look on the opponent's turn, v3 V2)")]
    [Tooltip("1 finger: yaw (horizontal) and pitch (vertical), degrees per reference pixel.")]
    public float OrbitYawPerPixel = 0.25f;
    public float OrbitPitchPerPixel = 0.15f;
    [Tooltip("2 fingers: the pivot moves this many metres per reference pixel per metre of distance.")]
    public float OrbitPanPerPixel = 0.0012f;
    public float OrbitPitchMin = 12f;
    public float OrbitPitchMax = 85f;
    public float OrbitDistanceMin = 0.35f;
    public float OrbitDistanceMax = 4f;
    [Tooltip("The pivot stays on the cloth plane, at most this far outside the play area.")]
    public float OrbitPivotMargin = 0.3f;
    [Tooltip("The camera never goes below the rail top plus this.")]
    public float OrbitRailClearance = 0.05f;
    [Tooltip("Over the table frame, the camera stays this far above a ball's top.")]
    public float OrbitBallClearance = 0.1f;
    [Tooltip("The camera eases to each new (bounded) pose over this time.")]
    public float OrbitSmooth = 0.08f;
    [Tooltip("After a 1-finger orbit is let go, it glides and slows down over this time.")]
    public float OrbitInertia = 0.25f;
    [Tooltip("Pinches smaller than this (scale change) are ignored, so a 2-finger pan doesn't zoom.")]
    public float OrbitPinchDeadzone = 0.003f;
    [Tooltip("Seconds after the stroke before you straighten up to watch.")]
    public float StandUpDelay = 0.35f;
    public float StandUpTime = 0.8f;
    [Header("Camera: watching and ball in hand")]
    [Tooltip("While balls roll and while the opponent aims, all six pocket rims stay inside this viewport area (below the player panels).")]
    public Rect WatchFitArea = new(0.03f, 0.06f, 0.94f, 0.78f);
    [Tooltip("The table centre sits this high inside WatchFitArea (0..1).")]
    [Range(0f, 1f)] public float WatchCenterHeight = 0.55f;
    [Tooltip("Shrinks the fit areas a little, so head sway never pushes a pocket out.")]
    public float CameraFitMargin = 0.01f;
    [Tooltip("Ball in hand: the allowed area and the cue ball stay inside this viewport area (above DONE, below the panels).")]
    public Rect PlaceFitArea = new(0.04f, 0.19f, 0.92f, 0.65f);
    public float PlacePitchMin = 50f;
    public float PlacePitchMax = 60f;
    [Tooltip("Ball in hand: the cue ball floats this high above the cloth while you drag it.")]
    public float PlaceHoverHeight = 0.02f;
    public float PlaceDropDuration = 0.12f;
    [Tooltip("A drag that starts within this many ball radii of the cue ball (on screen) picks it up.")]
    public float PlaceGrabRadii = 2.5f;
    [Tooltip("...but never less than this, in reference pixels, so a finger can always pick it up.")]
    public float PlaceGrabMinPixels = 60f;
    [Header("Head motion")]
    public float HeadSwayMetres = 0.003f;
    public float HeadSwayDegrees = 0.25f;
    public float HeadSwaySpeed = 0.3f;
    public float StrikeKick = 0.035f;

    [Header("Cue view")]
    public float CueLength = 1.45f;
    public float CueTipRadius = 0.006f;
    public float CueButtRadius = 0.015f;
    [Tooltip("The ivory ferrule behind the tip, and the chalked leather tip (v3 V3), metres.")]
    public float CueFerruleLength = 0.025f;
    public float CueTipLength = 0.012f;
    [Tooltip("Dome on the tip: its height as a fraction of the tip radius.")]
    public float CueTipDome = 0.45f;
    [Tooltip("Lowest cue elevation; it rises up to CueMaxElevation to clear cushions, rails and balls.")]
    public float CueElevation = 5f;
    public float CueMaxElevation = 50f;
    [Tooltip("Gap between the cue's underside and anything below it.")]
    public float CueClearance = 0.004f;
    public float CueElevationSmooth = 0.08f;
    [Tooltip("Smaller drops in the needed elevation are ignored, so the cue doesn't twitch.")]
    public float CueElevationMinChange = 0.5f;
    public float CueTipGap = 0.012f;
    public float CuePullDistance = 0.28f;
    public float CueStrikeDuration = 0.06f;

    [Header("Ball view")]
    [Tooltip("Pocket liner depth below the cloth: a dropped ball rests at the bottom until the turn ends.")]
    public float PocketDepth = 0.085f;
    [Tooltip("A pocketed ball carries on over the rim and falls for this long, then bounces once.")]
    public float PocketFallTime = 0.2f;
    public float PocketBounce = 0.012f;
    [Tooltip("The pocket's rim glows for this long: green legal, red foul, white opponent's ball.")]
    public float PocketGlowTime = 0.4f;
    public Color PocketGlowLegal = new(0.35f, 1f, 0.45f, 0.95f);
    public Color PocketGlowFoul = new(1f, 0.28f, 0.22f, 0.95f);
    public Color PocketGlowOpponent = new(1f, 1f, 1f, 0.85f);

    [Header("Guide")]
    public float GuideWidth = 0.006f;
    [Tooltip("The cue ball's line up to the first obstacle: a 4 mm white core with a 1.5 mm dark shadow each side.")]
    public float GuideLineWidth = 0.007f;
    public Color GuideLineColor = new(1f, 1f, 1f, 0.85f);
    [Tooltip("An opponent's ball (or the 8 too early) first: line and ghost turn this muted red. #E06A5A")]
    public Color GuideIllegalColor = new(0.878f, 0.416f, 0.353f, 0.9f);
    [Range(0f, 1f)] public float GuideIllegalGhostAlpha = 0.22f;
    [Tooltip("The ghost swells once when the aim moves onto an illegal ball.")]
    public float GuideIllegalPulseTime = 0.4f;
    public Color GuideScratchColor = new(1f, 0.55f, 0.2f, 0.95f);
    [Tooltip("Hit ball's band (#FFD54A, fill alpha).")]
    public Color GuideObjectColor = new(1f, 0.835f, 0.29f, 0.28f);
    [Tooltip("The cue ball's band after the contact or the cushion (fill alpha).")]
    public Color GuideCueBandColor = new(1f, 1f, 1f, 0.24f);
    [Tooltip("Chevrons flow along the bands this fast (m/s).")]
    public float GuideChevronSpeed = 0.12f;
    public float GuideChevronsPerBand = 2.5f;
    [Tooltip("Rounded arrow tip at the end of each band (metres, at most 45% of the band).")]
    public float GuideTipLength = 0.03f;
    [Tooltip("A guide prediction slower than this (ms) is rerun at most every other frame while the aim keeps moving (v4 V1).")]
    public float GuidePredictionBudgetMs = 1.5f;
    [Tooltip("Before the player pulls, the cue ball's path after contact is drawn for this pull (it depends on speed).")]
    [Range(0f, 1f)] public float GuidePreviewPull = 0.5f;
    [Tooltip("After a yellow or red release: the intended and the real line stay this long.")]
    public float MissGhostDuration = 1.2f;
    [Tooltip("The release bar holds the marker where you let go this long.")]
    public float ReleaseFreezeDuration = 0.5f;

    [Header("Flow timing (seconds)")]
    [Tooltip("The VS intro (you vs the CPU character, the venue) before the coin toss (v3 V13).")]
    public float VsIntroDuration = 2.0f;
    public float CoinFlipDuration = 1.2f;
    public float ResolvePause = 0.6f;
    [Tooltip("A foul banner stays this long, and the next turn waits for it.")]
    public float FoulBannerDuration = 2.2f;
    [Tooltip("The YOU ARE SOLIDS / STRIPES banner.")]
    public float GroupBannerDuration = 1.6f;
    public float EndPopupDelay = 1.0f;

    [Header("Objectives (v3 V12)")]
    [Tooltip("A cushion touch this close (s) before the drop is the pocket jaw, not a bank shot.")]
    public float BankIgnoreBeforeDrop = 0.15f;

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
        SlideFriction = SlideFriction,
        RollSlipEpsilon = RollSlipEpsilon,
        RollDecel = RollDecel,
        LinearDamping = LinearDamping,
        StopSpeed = StopSpeed,
        BallRestitution = BallRestitution,
        RackGap = RackGap,
        SquirtDegPerR = SquirtDegPerR,
        SideTransfer = SideTransfer,
        SideSpinDecel = SideSpinDecel,
        SwerveFactor = SwerveFactor,
        SwerveMinElevation = SwerveMinElevation,
        BallFriction = BallFriction,
        BallFrictionFast = BallFrictionFast,
        BallFrictionDecay = BallFrictionDecay,
        CushionRestitutionSlow = CushionRestitutionSlow,
        CushionRestitutionFast = CushionRestitutionFast,
        CushionFastSpeed = CushionFastSpeed,
        CushionSpinFriction = CushionSpinFriction
    };

    public CueClearanceSettings CueClearanceSettings() => new()
    {
        BallRadius = BallRadius,
        HalfWidth = HalfWidth,
        HalfLength = HalfLength,
        CushionDepth = CushionDepth,
        CushionNoseHeight = CushionNoseHeight,
        CushionBackHeight = CushionBackHeight,
        RailWidth = RailWidth,
        RailTop = RailTop,
        CueLength = CueLength,
        CueButtRadius = CueButtRadius,
        PullDistance = CuePullDistance,
        MinElevation = CueElevation,
        MaxElevation = CueMaxElevation,
        Clearance = CueClearance
    };

    public ShotAccuracy.Settings AccuracySettings(float greenHalf, float yellowWidth, float period) => new()
    {
        GreenHalf = greenHalf,
        YellowWidth = yellowWidth,
        Period = period,
        YellowErrorMinDeg = YellowErrorMinDeg,
        YellowErrorMaxDeg = YellowErrorMaxDeg,
        RedErrorMinDeg = RedErrorMinDeg,
        RedErrorMaxDeg = RedErrorMaxDeg,
        RedPowerVariance = RedPowerVariance
    };

    public ShotAccuracy.PowerScaling ReleaseScaling() => new()
    {
        GreenScale0 = GreenScale0,
        GreenScale1 = GreenScale1,
        YellowScale0 = YellowScale0,
        YellowScale1 = YellowScale1,
        PeriodScale0 = PeriodScale0,
        PeriodScale1 = PeriodScale1,
        ErrorScale0 = ErrorScale0,
        ErrorScale1 = ErrorScale1
    };

    // The pull -> speed curve for a cue whose top speed (POWER) is maxSpeed.
    public PowerCurve PowerCurve(float maxSpeed) => new()
    {
        MinSpeed = MinSpeed,
        HardSpeed = HardSpeed,
        SharedUntil = PowerSharedUntil,
        Exponent = PowerExponent,
        MaxSpeed = Mathf.Max(maxSpeed, HardSpeed)
    };

    public float SpeedFromPull(float pull01, float maxSpeed) => PowerCurve(maxSpeed).SpeedFromPull(pull01);
}
