using UnityEngine;

public enum CameraMode
{
    Overview = 0,
    Aim = 1,
    Orbit = 2,
    Place = 3,
    Watch = 4,
    PocketPick = 5   // bird's-eye view while a pocket is picked (v4 V9.1)
}

// Orbit camera (v3 V2): a pivot on the cloth plane and spherical coordinates around it, degrees and metres.
public struct OrbitState
{
    public Vector3 Pivot;
    public float Yaw;
    public float Pitch;
    public float Distance;
}

// The camera is the player's eyes. Aiming: bent over the cue, eyes on the line ahead. After the stroke, and
// while the opponent aims: stand at the short end of the table on the shooter's side, far enough back that all
// six pockets stay in view, and follow the action with your head (Watch, gameplay redesign G1). Ball in hand:
// stand over the table end with the ball in your hand, looking down at 50-60 degrees (Place, G5).
// Orbit (v3 V2): LOOK on your turn, and free look on the opponent's (the first drag or pinch takes the camera,
// TABLE VIEW or a double tap gives it back): 1 finger orbits and tilts, a pinch zooms, 2 fingers move the pivot;
// it never goes below the rail or into the table, and eases into every bounded pose.
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    // While aiming, what matters (cue ball, hit ball, where it goes) must sit clear of the side strips,
    // the player panels at the top and the buttons at the bottom.
    private static readonly Rect AimFitArea = new(0.13f, 0.22f, 0.68f, 0.58f);
    private const float TableExtentZ = 1.28f;
    private const int SearchSteps = 24;
    private const int GazeSteps = 8;

    [SerializeField] private MatchConfig _config;
    [SerializeField] private TableView _table;

    private Camera _camera;
    private CueClearanceSettings _frame;
    private CameraMode _mode = CameraMode.Overview;
    private Vector3 _ballWorld;
    private Vector3 _aimDirection = Vector3.forward;
    private float _tilt;
    private bool _isPlayerTurn = true;
    private bool _kitchenOnly;
    // Turn and phase events can arrive in either order within a frame (listener order isn't guaranteed),
    // so they are applied together in LateUpdate.
    private bool _turnPending;
    private bool _phasePending;
    private TurnPhase _phase;

    private Vector3 _fromPosition;
    private Quaternion _fromRotation;
    private float _blend = 1f;
    private float _blendTime;

    // Watch: which end you stand at, where, and what your eyes follow (smoothed).
    private float _watchEnd = -1f;
    private Vector3 _watchPosition;
    private Vector3 _gaze;
    private Vector3 _gazeVelocity;
    // v4 V10.1: how slowly the eyes follow the action: a slow smoothing and a speed limit on the look point.
    private const float GazeSmoothTime = 0.45f;
    private const float GazeMaxSpeed = 1.5f;
    // Place: one pose per placement.
    private Vector3 _placePosition;
    private Quaternion _placeRotation;
    private float _kick;
    // Orbit: the bounded target the gestures move, the eased pose that follows it, and the 1-finger glide.
    private OrbitState _orbitTarget;
    private OrbitState _orbit;
    private Vector3 _orbitPivotVelocity;
    private float _orbitYawVelocity;
    private float _orbitPitchVelocity;
    private float _orbitDistanceVelocity;
    private Vector2 _orbitGlide;
    private bool _gliding;
    private bool _freeLook;
    private float _pendingWatchAt = -1f;
    // v4 V7: after the player's shot: holding down on it (eyes on the cue ball), then rising into Watch.
    private bool _shotHold;
    // v4 V10.1: the eyes during the hold and after: the cue ball's position, smoothed (no snapping to its bounces).
    private Vector3 _holdFocus;
    private Vector3 _holdFocusVelocity;
    private bool _shotRise;
    private Quaternion _lastRotation;

    // v4 V9.1: the rise from Aim to Watch is one eased orbit round the table centre (no separate turn), and a pinch
    // while aiming moves the eyes closer / further and a little sideways (reset by the shot and by every new aim).
    private OrbitState _riseFrom;
    // v5 V1: walking to the other end while the opponent aims: the same eased orbit as the rise, eyes on the action.
    private bool _walk;
    // v5 V1: from watching back down to aiming at the start of your turn: the same eased orbit, the view turning on
    // pitch and yaw only; its time is set on its first frame, when the aim pose is known.
    private bool _descend;
    private bool _descendTimed;
    // v5 V1: how fast the camera is turning now (deg/s), for LimitTurn.
    private float _turnRate;
    private bool _turnCatchUp;
    private float _aimZoom;
    private float _aimZoomTarget;
    private float _aimZoomVelocity;
    private float _aimPan;
    private float _aimPanTarget;
    private float _aimPanVelocity;
    private CameraMode _pickReturn = CameraMode.Aim;
    private Vector3 _pickPosition;
    private Quaternion _pickRotation;

    // Aim framing: points to keep in view and the smoothed step-back distance.
    private readonly Vector3[] _focus = new Vector3[3];
    private int _focusCount;
    private float _aimDistance = -1f;
    private float _aimDistanceVelocity;
    // v5 V1: while the aim moves (and AimFrameSettleTime after) the framing holds: the look-ahead stays, the step back
    // and the cue's elevation only grow. Then everything eases to the new framing: the look-ahead, the step back and
    // the eyes' height above what the player chose (tilt, pinch), with the pitch and height speed limited.
    private float _aimMovedAt = -10f;
    private bool _framingSettled = true;
    private float _distanceTarget;
    private float _along;
    private float _alongTarget;
    private float _alongVelocity;
    private float _elevationHeld;
    private float _heightFrame;
    private float _heightFrameVelocity;
    private float _heightTarget;
    private float _aimPitch;
    private float _lastTilt;
    private float _sway;
    private float _standing = 1f;
    // The camera's own clock (unscaled seconds): the pause's time scale never stops the eyes.
    private float _clock;
    // v5 V1: the last pose before the head's sway: every blend starts from it (from the transform, the sway would be
    // counted twice in the blend's first frame).
    private Vector3 _posePosition;
    private Quaternion _poseRotation = Quaternion.identity;

#if UNITY_EDITOR
    // Editor checks (v5 V1): a fixed frame time for the camera (used with Time.captureDeltaTime), so a slow unfocused
    // Editor measures the same motion as 60 fps on a phone. 0: real time.
    public static float DebugFrameTime;
    private static float FrameTime => DebugFrameTime > 0f ? DebugFrameTime : Time.unscaledDeltaTime;
    // ...and checks that step the camera several times per Editor frame (DebugStep) switch its own LateUpdate off.
    public static bool DebugManual;
    private bool _debugStepping;

    public void DebugStep()
    {
        _debugStepping = true;
        LateUpdate();
        _debugStepping = false;
    }
#else
    private static float FrameTime => Time.unscaledDeltaTime;
#endif

    public CameraMode Mode => _mode;
    // The pinch zoom (metres further than the framing asked for) and the sideways slide, for checks and the HUD.
    public float AimZoom => _aimZoom;
    public float AimPan => _aimPan;
    public float AimDistanceNow => _aimDistance;
    public bool Pulling => _mode == CameraMode.Aim && _phase == TurnPhase.Pulling;
    public Camera Camera => _camera;

    // The cue's current elevation (degrees): with the butt raised, you stand more upright over it.
    public float CueElevation { get; set; }

    // While the player drags the cue ball in the aim view, the view holds still (it would chase the ball).
    public bool HoldPose { get; set; }
    // The camera is still blending into its mode's pose (checks).
    public bool IsBlending => _blend < 1f || _shotHold || _shotRise || _walk || _descend || _turnCatchUp;

    public float Tilt
    {
        get => _tilt;
        set => _tilt = Mathf.Clamp01(value);
    }

    // v4 V9.1: the bird's-eye view while a pocket is picked; off, back to where the camera was (Aim or Watch).
    public void SetPocketPick(bool on)
    {
        if (on && _mode != CameraMode.PocketPick)
        {
            _pickReturn = _mode == CameraMode.Aim || _mode == CameraMode.Watch ? _mode : CameraMode.Aim;
            SetMode(CameraMode.PocketPick);
        }
        else if (!on && _mode == CameraMode.PocketPick)
        {
            SetMode(_isPlayerTurn ? CameraMode.Aim : CameraMode.Watch);
        }
    }

    public bool IsPocketPick => _mode == CameraMode.PocketPick;

    // Where a pocket's cap shows on screen right now (viewport 0..1), for the pick overlay: the centre of the hole at
    // the rail's height, where the marker must sit (v4 V10.1; the rim is inward of it).
    public Vector2 PocketViewport(int pocket)
    {
        Vector2 center = _table.Geometry.Pockets[pocket].Center;
        Vector3 viewport = _camera.WorldToViewportPoint(TableOrigin + new Vector3(center.x, _config.RailTop, center.y));
        return new Vector2(viewport.x, viewport.y);
    }

    // LOOK starts orbiting around this point (the ghost ball or the cue ball), set before the phase changes.
    public Vector3 OrbitPivot { get; set; }

    // For checks and debugging: the orbit target and whether the opponent's-turn free look is on.
    public OrbitState OrbitTarget => _orbitTarget;
    public bool FreeLook => _freeLook;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _camera.fieldOfView = _config.FieldOfView;
        _frame = _config.CueClearanceSettings();
        _tilt = _config.AimDefaultTilt;
        _posePosition = transform.position;
        _poseRotation = transform.rotation;
    }

    private void OnEnable()
    {
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        GameEvents.OnShotFired += HandleShotFired;
        GameEvents.OnOrbitDrag += HandleOrbitDrag;
        GameEvents.OnOrbitPinch += HandleOrbitPinch;
        GameEvents.OnOrbitPan += HandleOrbitPan;
        GameEvents.OnOrbitDragEnded += HandleOrbitDragEnded;
        GameEvents.OnTableViewRequested += HandleTableViewRequested;
        GameEvents.OnLookDrag += HandleAimInput;
        GameEvents.OnFineAimDrag += HandleAimInput;
        GameEvents.OnFineAimStep += HandleAimInput;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnOrbitDrag -= HandleOrbitDrag;
        GameEvents.OnOrbitPinch -= HandleOrbitPinch;
        GameEvents.OnOrbitPan -= HandleOrbitPan;
        GameEvents.OnOrbitDragEnded -= HandleOrbitDragEnded;
        GameEvents.OnTableViewRequested -= HandleTableViewRequested;
        GameEvents.OnLookDrag -= HandleAimInput;
        GameEvents.OnFineAimDrag -= HandleAimInput;
        GameEvents.OnFineAimStep -= HandleAimInput;
    }

    // v5 V1: a finger on the aim counts as moving it, even while the full-ball magnet holds the line still.
    private void HandleAimInput(Vector2 _) => _aimMovedAt = _clock;
    private void HandleAimInput(float _) => _aimMovedAt = _clock;
    private void HandleAimInput(int _, bool __) => _aimMovedAt = _clock;

    // Controllers feed the cue ball and aim every frame they own the turn.
    public void SetAim(Vector3 ballWorld, Vector3 aimDirection)
    {
        _ballWorld = ballWorld;
        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude <= 1e-6f)
            return;
        aimDirection.Normalize();
        if (Vector3.Angle(aimDirection, _aimDirection) > 1e-3f)
            _aimMovedAt = _clock;
        _aimDirection = aimDirection;
    }

    // Aim framing: the cue ball, the ball it will hit and where that ball is heading.
    public void SetFocus(Vector3 a, Vector3 b, Vector3 c, int count)
    {
        _focus[0] = a;
        _focus[1] = b;
        _focus[2] = c;
        _focusCount = Mathf.Clamp(count, 0, 3);
    }

    public void SetMode(CameraMode mode)
    {
        if (mode == _mode)
            return;

        _fromPosition = _posePosition;
        _fromRotation = _poseRotation;
        _blend = 0f;
        _blendTime = mode == CameraMode.Watch && _mode == CameraMode.Aim ? _config.ShotRiseTime : mode == CameraMode.PocketPick ? _config.PickBlendTime : _config.CameraBlendTime;
        Vector3 cueBallNow = _table.Ball(0).transform.position;
        _shotRise = mode == CameraMode.Watch && _mode == CameraMode.Aim;
        _shotHold = false;
        _walk = false;
        _descend = mode == CameraMode.Aim && _mode == CameraMode.Watch;
        _descendTimed = false;
        _turnCatchUp = false;
        CameraMode previous = _mode;
        _mode = mode;
        _pendingWatchAt = -1f;
        _gliding = false;
        SetFreeLook(mode == CameraMode.Orbit && !_isPlayerTurn);

        switch (mode)
        {
            case CameraMode.Aim:
                _aimDistance = -1f;
                ResetAimZoom();
                if (_descend)
                    _riseFrom = OrbitFromPose(_fromPosition, TableOrigin);
                break;
            case CameraMode.PocketPick:
                ResetAimZoom();
                PickPose(_camera, _config, _table.Geometry, TableOrigin, EndOf(_table.WorldToTable(_ballWorld).y), out _pickPosition, out _pickRotation);
                break;
            case CameraMode.Orbit:
            {
                // From exactly where you are now, around the pivot: no jump.
                _orbitTarget = OrbitFromPose(_posePosition, OrbitPivot);
                ConstrainOrbit(ref _orbitTarget, _config, _table.Geometry, TableOrigin);
                _orbit = _orbitTarget;
                _orbitPivotVelocity = Vector3.zero;
                _orbitYawVelocity = _orbitPitchVelocity = _orbitDistanceVelocity = 0f;
                break;
            }
            case CameraMode.Place:
            {
                // Break: at the head end, the kitchen in front of you. After a foul: the end nearer the cue ball.
                Vector3 cueBall = _table.Ball(0).transform.position;
                float cueY = _table.WorldToTable(cueBall).y;
                float end = _kitchenOnly || Mathf.Abs(cueY) < 0.05f ? -1f : Mathf.Sign(cueY);
                PlacePose(_camera, _config, _table.Geometry, TableOrigin, end, _kitchenOnly, cueBall, out _placePosition, out _placeRotation);
                break;
            }
            case CameraMode.Watch:
                // Yours: the end you shot from. The opponent's: the end behind their cue ball.
                _watchEnd = previous == CameraMode.Aim ? EndOf(_table.WorldToTable(transform.position).y) : ShooterEnd();
                _watchPosition = WatchPosition(_camera, _config, _table.Geometry, TableOrigin, _watchEnd);
                // After the shot's hold the eyes go on from where they were looking, not from the ball's new spot.
                _gaze = previous == CameraMode.Aim && _shotRise ? _holdFocus : WatchFocus();
                _gazeVelocity = previous == CameraMode.Aim && _shotRise ? _holdFocusVelocity : Vector3.zero;
                ResetAimZoom();
                if (_shotRise)
                {
                    // One orbit round the table centre from where the eyes are to where they stand in Watch; a bigger
                    // turn takes longer so the camera never turns faster than MaxCameraTurnDegPerSec at its peak.
                    _riseFrom = OrbitFromPose(_fromPosition, TableOrigin);
                    Quaternion end = WatchRotation(_camera, _config, _table.Geometry, TableOrigin, _watchPosition, _gaze);
                    float turn = Quaternion.Angle(_fromRotation, end);
                    _blendTime = Mathf.Clamp(1.875f * turn / Mathf.Max(1f, _config.MaxCameraTurnDegPerSec), _config.ShotRiseTime, _config.ShotRiseMaxTime);
                }
                break;
        }
    }

    private Vector3 TableOrigin => _table.TableToWorld(Vector2.zero, 0f);
    private float RingZ => TableExtentZ + _config.StandMargin;

    private void LateUpdate()
    {
#if UNITY_EDITOR
        if (DebugManual && !_debugStepping)
            return;
#endif
        float dt = FrameTime;
        _clock += dt;
        ApplyPendingEvents();
        if (_pendingWatchAt >= 0f && _clock >= _pendingWatchAt)
        {
            // The rise starts when it was due, not when this frame noticed (v4 V7: ~2 s whatever the frame rate).
            float late = _clock - _pendingWatchAt;
            SetMode(CameraMode.Watch);
            if (_shotRise)
                _blend = Mathf.Clamp01(late / Mathf.Max(0.01f, _blendTime));
        }
        if (HoldPose && _mode == CameraMode.Aim)
            return;

        if (_gliding && _mode == CameraMode.Orbit)
        {
            ApplyOrbitDrag(ref _orbitTarget, _orbitGlide * dt, _config);
            ConstrainOrbit(ref _orbitTarget, _config, _table.Geometry, TableOrigin);
            _orbitGlide *= Mathf.Exp(-dt / Mathf.Max(0.01f, _config.OrbitInertia));
            _gliding = _orbitGlide.sqrMagnitude > 25f;
        }
        TargetPose(dt, out Vector3 position, out Quaternion rotation);
        if (_shotHold)
        {
            // v4 V7: still down on the shot, only the eyes follow the cue ball.
            position = _fromPosition;
            Vector3 ball = _table.Ball(0).transform.position;
            ball.y = _table.FeltHeight;
            _holdFocus = Vector3.SmoothDamp(_holdFocus, ball, ref _holdFocusVelocity, GazeSmoothTime, GazeMaxSpeed, dt);
            rotation = Quaternion.LookRotation(_holdFocus - position, Vector3.up);
        }
        else if (_blend < 1f && _shotRise)
        {
            // v4 V9.1: one eased orbit round the table centre: height, distance and turn follow the same curve, the
            // eyes slide from the cue ball to Watch's gaze, and the end is exactly Watch's pose (no snap).
            _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, _blendTime));
            float t = Smootherstep(_blend);
            OrbitState end = OrbitFromPose(position, TableOrigin);
            var state = new OrbitState
            {
                Pivot = TableOrigin,
                Yaw = Mathf.LerpAngle(_riseFrom.Yaw, end.Yaw, t),
                Pitch = Mathf.Lerp(_riseFrom.Pitch, end.Pitch, t),
                Distance = Mathf.Lerp(_riseFrom.Distance, end.Distance, t)
            };
            Vector3 eyes = OrbitPosition(state);
            // v5 V1: the eyes stay on the point they were following in the hold (it moves on smoothly), and turn into
            // Watch's framing with the same soft start and end.
            Quaternion look = Quaternion.LookRotation(_gaze - eyes, Vector3.up);
            rotation = Quaternion.Slerp(look, rotation, t);
            position = eyes;
            if (_blend >= 1f)
                _shotRise = false;
        }
        else if (_blend < 1f && _descend)
        {
            // A bigger turn takes longer (CameraBlendTime at least, ShotRiseMaxTime at most), instead of a 0.45 s
            // straight blend that swung the view up to ~500 deg/s. The new aim can reach the camera a frame late, so
            // the time may still grow while the move has barely started (it has hardly moved yet).
            float needed = Mathf.Clamp(1.875f * Angle(_fromRotation, rotation) / Mathf.Max(1f, _config.MaxCameraTurnDegPerSec), _config.CameraBlendTime, _config.ShotRiseMaxTime);
            if (!_descendTimed)
            {
                _blendTime = needed;
                _descendTimed = true;
            }
            else if (_blend < 0.1f && needed > _blendTime)
            {
                _blend *= _blendTime / needed;
                _blendTime = needed;
            }
            _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, _blendTime));
            float t = Smootherstep(_blend);
            OrbitState end = OrbitFromPose(position, TableOrigin);
            var state = new OrbitState
            {
                Pivot = TableOrigin,
                Yaw = Mathf.LerpAngle(_riseFrom.Yaw, end.Yaw, t),
                Pitch = Mathf.Lerp(_riseFrom.Pitch, end.Pitch, t),
                Distance = Mathf.Lerp(_riseFrom.Distance, end.Distance, t)
            };
            position = OrbitPosition(state);
            Vector3 from = _fromRotation.eulerAngles, to = rotation.eulerAngles;
            rotation = Quaternion.Euler(Mathf.LerpAngle(from.x, to.x, t), Mathf.LerpAngle(from.y, to.y, t), 0f);
            if (_blend >= 1f)
                _descend = false;
        }
        else if (_blend < 1f && _walk)
        {
            // v5 V1: round the table on one eased orbit; at every point Watch's own framing of the action (no snap at
            // either end, no turn faster than the walk).
            _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, _blendTime));
            float t = Smootherstep(_blend);
            OrbitState end = OrbitFromPose(position, TableOrigin);
            var state = new OrbitState
            {
                Pivot = TableOrigin,
                Yaw = Mathf.LerpAngle(_riseFrom.Yaw, end.Yaw, t),
                Pitch = Mathf.Lerp(_riseFrom.Pitch, end.Pitch, t),
                Distance = Mathf.Lerp(_riseFrom.Distance, end.Distance, t)
            };
            position = OrbitPosition(state);
            rotation = WatchRotation(_camera, _config, _table.Geometry, TableOrigin, position, _gaze);
            if (_blend >= 1f)
                _walk = false;
        }
        else if (_blend < 1f)
        {
            _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, _blendTime));
            float t = Mathf.SmoothStep(0f, 1f, _blend);
            position = Vector3.Lerp(_fromPosition, position, t);
            rotation = Quaternion.Slerp(_fromRotation, rotation, t);
        }
        // v5 V1: what the camera turns on its own is speed and acceleration limited; the player's aim and look are not.
        // Coming down to aim, it keeps limiting until it has caught up with the aim view (no snap at the end).
        if (_shotHold || _mode == CameraMode.Watch || _descend || _turnCatchUp)
        {
            Quaternion target = rotation;
            rotation = LimitTurn(target, dt);
            _turnCatchUp = (_descend || _turnCatchUp) && _mode == CameraMode.Aim && Angle(rotation, target) > 0.01f;
        }
        else
        {
            _turnRate = Angle(_lastRotation, rotation) / Mathf.Max(dt, 1e-4f);
        }
        _lastRotation = rotation;
        _posePosition = position;
        _poseRotation = rotation;

        ApplyHeadMotion(dt, ref position, ref rotation);
        transform.SetPositionAndRotation(position, rotation);
    }

    // v5 V1: the shot's hold and rise, watching, walking round and coming down to aim turn the view at most
    // MaxCameraTurnDegPerSec and change that speed at most CameraTurnAccel (braking in time to stop on a still target):
    // a framing that jumps (a pocket coming into view, the eyes close to a moving ball) is followed, never snapped to.
    private Quaternion LimitTurn(Quaternion target, float dt)
    {
        float accel = _config.CameraTurnAccel;
        float angle = Angle(_lastRotation, target);
        if (angle < 1e-4f)
        {
            _turnRate = Mathf.MoveTowards(_turnRate, 0f, accel * dt);
            return target;
        }
        float wanted = angle / Mathf.Max(dt, 1e-4f);
        float rate = Mathf.Clamp(wanted, _turnRate - accel * dt, _turnRate + accel * dt);
        // The fastest this frame that can still stop on the target after it, in whole frames: r dt + r^2 / 2a = angle.
        float brake = accel * (Mathf.Sqrt(dt * dt + 2f * angle / accel) - dt);
        // 3% under the limit: the head's sway turns a little on top.
        float cap = Mathf.Min(0.97f * _config.MaxCameraTurnDegPerSec, brake);
        rate = Mathf.Max(0f, Mathf.Min(rate, Mathf.Max(cap, _turnRate - accel * dt)));
        _turnRate = rate;
        return Quaternion.Slerp(_lastRotation, target, Mathf.Min(1f, rate * dt / angle));
    }

    // The angle between two rotations in degrees, exact for small angles too (Quaternion.Angle is 0 below ~0.16 deg).
    private static float Angle(Quaternion a, Quaternion b)
    {
        Quaternion d = Quaternion.Inverse(a) * b;
        float v = Mathf.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z);
        return 2f * Mathf.Atan2(v, Mathf.Abs(d.w)) * Mathf.Rad2Deg;
    }

    private void TargetPose(float dt, out Vector3 position, out Quaternion rotation)
    {
        switch (_mode)
        {
            case CameraMode.Aim:
            {
                // Behind the cue, eyes on the line; steps back (and up) only as far as needed to keep the
                // hit ball and its pocket in view.
                // v4 V9.1: once the cue is being pulled back the framing stays exactly as it was.
                bool first = _aimDistance < 0f;
                if (!Pulling || first)
                    UpdateAimFraming(dt, first);
                _aimZoom = Mathf.SmoothDamp(_aimZoom, _aimZoomTarget, ref _aimZoomVelocity, 0.1f, Mathf.Infinity, dt);
                _aimPan = Mathf.SmoothDamp(_aimPan, _aimPanTarget, ref _aimPanVelocity, 0.1f, Mathf.Infinity, dt);
                float distance = Mathf.Clamp(_aimDistance + _aimZoom, _config.AimZoomMinDistance, _config.AimZoomMaxDistance);
                // What the player chose (tilt, the pinch's rise) moves the eyes at once; what the framing adds above
                // it (stepping back, seeing over the rail, staying above the cue) eases at most AimMaxHeightPerSec.
                float chosen = TiltHeight + (distance - _aimDistance) * _config.AimRiseSlope;
                float wanted = AimHeight(distance, _elevationHeld) - chosen;
                if (first)
                {
                    _heightFrame = _heightTarget = wanted;
                    _heightFrameVelocity = 0f;
                }
                else if (!Pulling)
                {
                    // While the aim moves the eyes only go up (seeing over a rail the new line passes), never down.
                    _heightTarget = _framingSettled ? wanted : Mathf.Max(_heightTarget, wanted);
                    _heightFrame = Mathf.SmoothDamp(_heightFrame, _heightTarget, ref _heightFrameVelocity, _config.AimFramingSmooth, _config.AimMaxHeightPerSec, dt);
                }
                AimPose(distance, _along, chosen + _heightFrame, out position, out rotation, true);
                // The pitch the framing asks for turns at most AimMaxPitchDegPerSec; the player's tilt and pinch at once.
                float pitch = rotation.eulerAngles.x;
                bool chosenNow = Mathf.Abs(_tilt - _lastTilt) > 1e-5f || Mathf.Abs(_aimZoom - _aimZoomTarget) > 1e-4f;
                if (Pulling && !first)
                    pitch = _aimPitch;
                else if (!first && !chosenNow)
                    pitch = Mathf.MoveTowardsAngle(_aimPitch, pitch, _config.AimMaxPitchDegPerSec * dt);
                _aimPitch = pitch;
                _lastTilt = _tilt;
                rotation = Quaternion.Euler(pitch, rotation.eulerAngles.y, 0f);
                break;
            }
            case CameraMode.Orbit:
            {
                // Ease towards the bounded target; the eased pose is bounded again, so it never dips either.
                float smooth = _config.OrbitSmooth;
                _orbit.Pivot = Vector3.SmoothDamp(_orbit.Pivot, _orbitTarget.Pivot, ref _orbitPivotVelocity, smooth, Mathf.Infinity, dt);
                _orbit.Yaw = Mathf.SmoothDampAngle(_orbit.Yaw, _orbitTarget.Yaw, ref _orbitYawVelocity, smooth, Mathf.Infinity, dt);
                _orbit.Pitch = Mathf.SmoothDamp(_orbit.Pitch, _orbitTarget.Pitch, ref _orbitPitchVelocity, smooth, Mathf.Infinity, dt);
                _orbit.Distance = Mathf.SmoothDamp(_orbit.Distance, _orbitTarget.Distance, ref _orbitDistanceVelocity, smooth, Mathf.Infinity, dt);
                ConstrainOrbit(ref _orbit, _config, _table.Geometry, TableOrigin);
                position = OrbitPosition(_orbit);
                rotation = Quaternion.LookRotation(_orbit.Pivot - position, Vector3.up);
                break;
            }
            case CameraMode.Place:
                position = _placePosition;
                rotation = _placeRotation;
                break;
            case CameraMode.PocketPick:
                position = _pickPosition;
                rotation = _pickRotation;
                break;
            case CameraMode.Watch:
            {
                // Eyes track the action with a little lag, like a head turning, but never so far that a
                // pocket leaves the view.
                _gaze = Vector3.SmoothDamp(_gaze, WatchFocus(), ref _gazeVelocity, GazeSmoothTime, GazeMaxSpeed, dt);
                position = _watchPosition;
                rotation = WatchRotation(_camera, _config, _table.Geometry, TableOrigin, position, _gaze);
                break;
            }
            default:
            {
                // Standing at the head of the table.
                position = _table.TableToWorld(new Vector2(0f, -RingZ), _config.StandingEyeHeight);
                rotation = Quaternion.LookRotation(_table.TableToWorld(new Vector2(0f, 0.2f), 0f) - position, Vector3.up);
                break;
            }
        }
    }

    // Watching yourself: the cue ball. Watching the opponent: where their shot is going.
    private Vector3 WatchFocus()
    {
        Vector3 focus = _isPlayerTurn ? _table.Ball(0).transform.position : _ballWorld + _aimDirection * 0.5f;
        focus.y = _table.FeltHeight;
        return focus;
    }

    private float ShooterEnd()
    {
        Vector2 aim = _table.WorldDirectionToTable(_aimDirection);
        Vector2 ball = _table.WorldToTable(_ballWorld);
        return Mathf.Abs(aim.y) > 0.25f ? -Mathf.Sign(aim.y) : EndOf(ball.y);
    }

    private static float EndOf(float tableY) => tableY > 0f ? 1f : -1f;

    private float TiltHeight => Mathf.Lerp(_config.AimLowHeight, _config.AimHighHeight, _tilt);

    // The eyes' height above the cloth `distance` behind the cue ball, with the cue at `elevation` degrees.
    private float AimHeight(float distance, float elevation)
    {
        float height = TiltHeight + (distance - _config.AimDistance) * _config.AimRiseSlope;
        // Never inside or just over the frame, and always seeing the cue ball over the cushion and rail (G6).
        height = Mathf.Max(height, CueClearance.MinEyeHeight(_frame, _table.WorldToTable(_ballWorld),
            _table.WorldDirectionToTable(_aimDirection), distance, _config.AimSightClearance, _config.AimFrameClearance));
#if UNITY_EDITOR
        _debugClear = height;
#endif
        return Mathf.Max(height, _config.BallRadius + distance * Mathf.Tan(elevation * Mathf.Deg2Rad) + _config.AimAboveCue);
    }

    // Eyes rest further down the line when the action is further away.
    private float FocusAlong()
    {
        if (_focusCount <= 1)
            return _config.AimLookAhead;
        float sum = 0f;
        for (int i = 1; i < _focusCount; i++)
            sum += Vector3.Dot(_focus[i] - _ballWorld, _aimDirection);
        return Mathf.Clamp(sum / (_focusCount - 1) * 0.6f, 0.4f, 1.6f);
    }

    private void AimPose(float distance, float along, float height, out Vector3 position, out Quaternion rotation, bool user = false)
    {
        Vector3 behind = _ballWorld - _aimDirection * distance;
        // The pinch's slide: the whole view moves sideways (eyes and the point they rest on), it never turns.
        Vector3 slide = user ? Vector3.Cross(Vector3.up, _aimDirection).normalized * _aimPan : Vector3.zero;
        position = new Vector3(behind.x + slide.x, _table.FeltHeight + height, behind.z + slide.z);

        Vector3 focus = _ballWorld + _aimDirection * along + slide;
        focus.y = _table.FeltHeight;
        rotation = Quaternion.LookRotation(focus - position, Vector3.up);
#if UNITY_EDITOR
        if (user)
        {
            DebugAimParts = new AimParts
            {
                Along = along, Fit = _distanceTarget, Distance = distance,
                Base = TiltHeight + (distance - _config.AimDistance) * _config.AimRiseSlope, Clear = _debugClear,
                Cue = _config.BallRadius + distance * Mathf.Tan(_elevationHeld * Mathf.Deg2Rad) + _config.AimAboveCue,
                Height = height, Elevation = _elevationHeld
            };
        }
#endif
    }

    // v5 V1: the framing's look-ahead and step back. While the aim moves the look-ahead holds and the step back and
    // the cue's elevation only grow (the view may step back to keep a ball in frame, never in or down); once it has
    // settled they ease to the new shot. The step back moves no faster than the eyes may rise.
    private void UpdateAimFraming(float dt, bool first)
    {
        bool settled = first || _clock - _aimMovedAt >= _config.AimFrameSettleTime;
        if (!settled && _framingSettled)
        {
            // The aim starts moving: the framing stops where it is now (it was maybe still easing in).
            _alongTarget = _along;
            _distanceTarget = _aimDistance;
            _heightTarget = _heightFrame;
            _aimDistanceVelocity = _alongVelocity = _heightFrameVelocity = 0f;
        }
        _framingSettled = settled;
        if (settled)
            _alongTarget = FocusAlong();
        _elevationHeld = settled ? CueElevation : Mathf.Max(_elevationHeld, CueElevation);
        float wanted = FitAimDistance(_alongTarget, _elevationHeld);
        _distanceTarget = settled ? wanted : Mathf.Max(_distanceTarget, wanted);
        if (first)
        {
            _aimDistance = _distanceTarget;
            _along = _alongTarget;
            _aimDistanceVelocity = _alongVelocity = 0f;
            return;
        }
        float maxDistanceSpeed = 0.9f * _config.AimMaxHeightPerSec / Mathf.Max(0.1f, _config.AimRiseSlope);
        _aimDistance = Mathf.SmoothDamp(_aimDistance, _distanceTarget, ref _aimDistanceVelocity, _config.AimFramingSmooth, maxDistanceSpeed, dt);
        _along = Mathf.SmoothDamp(_along, _alongTarget, ref _alongVelocity, _config.AimFramingSmooth, Mathf.Infinity, dt);
    }

    private void ResetAimZoom()
    {
        _aimZoom = _aimZoomTarget = _aimPan = _aimPanTarget = 0f;
        _aimZoomVelocity = _aimPanVelocity = 0f;
    }

    // Bird's-eye view for a pocket pick (v4 V9.1): looking down at PickPitch from the cue ball's end, as close as
    // keeps the whole table and every pocket rim inside PickFitArea.
    public static void PickPose(Camera camera, MatchConfig config, TableGeometry geometry, Vector3 tableOrigin, float end,
        out Vector3 position, out Quaternion rotation)
    {
        Rect area = Shrink(config.PickFitArea, config.CameraFitMargin);
        float yaw = end < 0f ? 0f : 180f;
        rotation = Quaternion.Euler(config.PickPitch, yaw, 0f);
        Vector3 forward = rotation * Vector3.forward;
        Vector3 centre = tableOrigin + Vector3.up * config.RailTop;
        Transform t = camera.transform;
        Vector3 savedPosition = t.position;
        Quaternion savedRotation = t.rotation;
        float low = 0.5f, high = 8f;
        for (int i = 0; i < SearchSteps; i++)
        {
            float mid = (low + high) * 0.5f;
            t.SetPositionAndRotation(centre - forward * mid, rotation);
            if (PocketsFit(camera, config, geometry, tableOrigin, area))
                high = mid;
            else
                low = mid;
        }
        t.SetPositionAndRotation(savedPosition, savedRotation);
        position = centre - forward * high;
    }

    // Smallest step back at which every focus point is inside AimFitArea: the first 5 cm step that fits, then a binary
    // search inside it, so the distance changes continuously with the aim (v5 V1).
    private float FitAimDistance(float along, float elevation)
    {
        if (_focusCount == 0)
            return _config.AimDistance;

        Vector3 savedPosition = transform.position;
        Quaternion savedRotation = transform.rotation;
        float result = _config.AimMaxDistance;
        for (float d = _config.AimDistance; d <= _config.AimMaxDistance; d += 0.05f)
        {
            if (!Fits(d))
                continue;
            float low = Mathf.Max(_config.AimDistance, d - 0.05f), high = d;
            if (d > _config.AimDistance)
            {
                for (int i = 0; i < 10; i++)
                {
                    float mid = (low + high) * 0.5f;
                    if (Fits(mid))
                        high = mid;
                    else
                        low = mid;
                }
            }
            result = high;
            break;
        }
        transform.SetPositionAndRotation(savedPosition, savedRotation);
        return result;

        bool Fits(float d)
        {
            AimPose(d, along, AimHeight(d, elevation), out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            for (int i = 0; i < _focusCount; i++)
            {
                Vector3 viewport = _camera.WorldToViewportPoint(_focus[i]);
                if (viewport.z <= 0f || !AimFitArea.Contains(new Vector2(viewport.x, viewport.y)))
                    return false;
            }
            return true;
        }
    }

    // Breathing sway (never while placing the cue ball, pulling the cue or rising after the shot) and a short push forward
    // on the stroke (StrikeKick, 0 since v5). v5 V1: the sway fades in over HeadSwayFadeIn and out twice as fast, so
    // the pull and the shot never jump the view.
    private void ApplyHeadMotion(float dt, ref Vector3 position, ref Quaternion rotation)
    {
        bool still = _mode == CameraMode.Place || _mode == CameraMode.Orbit || _mode == CameraMode.PocketPick || Pulling || _shotRise || _walk || _descend;
        float fade = Mathf.Max(0.01f, _config.HeadSwayFadeIn);
        _sway = Mathf.MoveTowards(_sway, still ? 0f : 1f, dt / (still ? fade * 0.5f : fade));
        float weight = Mathf.SmoothStep(0f, 1f, _sway);
        // Standing sways more than bent over the cue; the change between the two eases too.
        _standing = Mathf.MoveTowards(_standing, _mode == CameraMode.Aim ? 1f : 1.6f, 0.6f * dt / fade);
        if (weight > 0f)
        {
            float time = _clock * _config.HeadSwaySpeed;
            float standing = _standing * weight;
            var sway = new Vector3(Mathf.PerlinNoise(time, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, time) - 0.5f, 0f) * 2f;
            position += rotation * sway * _config.HeadSwayMetres * standing;
            rotation *= Quaternion.Euler(sway.y * _config.HeadSwayDegrees * standing, sway.x * _config.HeadSwayDegrees * standing, 0f);
        }

        if (_kick > 0.0001f)
        {
            position += rotation * Vector3.forward * _kick;
            _kick = Mathf.MoveTowards(_kick, 0f, dt * _config.StrikeKick * 6f);
        }
    }

    #region Orbit (static, so checks can run them without a scene)
    public static Vector3 OrbitPosition(in OrbitState state)
    {
        float yaw = state.Yaw * Mathf.Deg2Rad;
        float pitch = state.Pitch * Mathf.Deg2Rad;
        var back = new Vector3(-Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), -Mathf.Cos(yaw) * Mathf.Cos(pitch));
        return state.Pivot + back * state.Distance;
    }

    public static OrbitState OrbitFromPose(Vector3 position, Vector3 pivot)
    {
        Vector3 offset = position - pivot;
        float distance = Mathf.Max(1e-3f, offset.magnitude);
        return new OrbitState
        {
            Pivot = pivot,
            Distance = distance,
            Pitch = Mathf.Asin(Mathf.Clamp(offset.y / distance, -1f, 1f)) * Mathf.Rad2Deg,
            Yaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg
        };
    }

    // 1 finger: right turns the view with the finger, up lowers the eyes towards the cloth.
    public static void ApplyOrbitDrag(ref OrbitState state, Vector2 deltaPixels, MatchConfig config)
    {
        state.Yaw += deltaPixels.x * config.OrbitYawPerPixel;
        state.Pitch -= deltaPixels.y * config.OrbitPitchPerPixel;
    }

    // Fingers apart (scale > 1) come closer.
    public static void ApplyOrbitPinch(ref OrbitState state, float scale) => state.Distance /= Mathf.Max(0.05f, scale);

    // 2 fingers drag the cloth with them: the pivot moves the other way, more when you are further away.
    public static void ApplyOrbitPan(ref OrbitState state, Vector2 deltaPixels, MatchConfig config)
    {
        float yaw = state.Yaw * Mathf.Deg2Rad;
        var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        var right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
        float scale = config.OrbitPanPerPixel * state.Distance;
        state.Pivot -= (right * deltaPixels.x + forward * deltaPixels.y) * scale;
    }

    // The bounds (v3 V2): the pivot on the cloth within the table + margin; pitch and distance in range; eyes at least
    // a little above the rail top, and over the table frame high enough to clear any ball.
    public static void ConstrainOrbit(ref OrbitState state, MatchConfig config, TableGeometry geometry, Vector3 tableOrigin)
    {
        float maxX = geometry.HalfWidth + config.OrbitPivotMargin;
        float maxZ = geometry.HalfLength + config.OrbitPivotMargin;
        Vector3 local = state.Pivot - tableOrigin;
        state.Pivot = tableOrigin + new Vector3(Mathf.Clamp(local.x, -maxX, maxX), 0f, Mathf.Clamp(local.z, -maxZ, maxZ));
        state.Yaw = Mathf.Repeat(state.Yaw + 180f, 360f) - 180f;
        state.Distance = Mathf.Clamp(state.Distance, config.OrbitDistanceMin, config.OrbitDistanceMax);
        state.Pitch = Mathf.Clamp(state.Pitch, config.OrbitPitchMin, config.OrbitPitchMax);

        // Raising the eyes moves them in towards the pivot (over the frame), so settle it in a couple of passes.
        for (int pass = 0; pass < 3; pass++)
        {
            float needed = OrbitMinHeight(config, geometry, OrbitPosition(state) - tableOrigin);
            if (state.Distance * Mathf.Sin(state.Pitch * Mathf.Deg2Rad) >= needed)
                break;
            float pitch = Mathf.Asin(Mathf.Clamp01(needed / state.Distance)) * Mathf.Rad2Deg + 0.01f;
            state.Pitch = Mathf.Clamp(pitch, config.OrbitPitchMin, 90f);
        }
    }

    // Lowest eye height above the cloth at this spot (table-local): over the frame, above any ball; elsewhere,
    // above the rail.
    public static float OrbitMinHeight(MatchConfig config, TableGeometry geometry, Vector3 local)
    {
        float frameX = geometry.HalfWidth + config.CushionDepth + config.RailWidth;
        float frameZ = geometry.HalfLength + config.CushionDepth + config.RailWidth;
        float rail = config.RailTop + config.OrbitRailClearance;
        bool overFrame = Mathf.Abs(local.x) <= frameX && Mathf.Abs(local.z) <= frameZ;
        return overFrame ? Mathf.Max(rail, 2f * config.BallRadius + config.OrbitBallClearance) : rail;
    }
    #endregion

    #region Watch and place framing (static, so checks can run them without a scene)
    // A point on each pocket's outer rim at rail height: what must stay in view.
    public static Vector3 PocketRim(MatchConfig config, TableGeometry geometry, Vector3 tableOrigin, int pocket)
    {
        PocketInfo info = geometry.Pockets[pocket];
        Vector2 rim = info.Center + info.Inward * (info.CaptureRadius + config.PocketHoleExtra);
        return tableOrigin + new Vector3(rim.x, config.RailTop, rim.y);
    }

    public static bool PocketsFit(Camera camera, MatchConfig config, TableGeometry geometry, Vector3 tableOrigin, Rect area)
    {
        for (int i = 0; i < geometry.Pockets.Length; i++)
        {
            Vector3 viewport = camera.WorldToViewportPoint(PocketRim(config, geometry, tableOrigin, i));
            if (viewport.z <= 0f || !area.Contains(new Vector2(viewport.x, viewport.y)))
                return false;
        }
        return true;
    }

    public static Rect Shrink(Rect area, float margin) =>
        new(area.x + margin, area.y + margin, area.width - 2f * margin, area.height - 2f * margin);

    // Standing at the short end `end` (-1 head, +1 foot) on the long axis: from standing eye height, steps back
    // and a little up until every pocket rim is inside WatchFitArea.
    public static Vector3 WatchPosition(Camera camera, MatchConfig config, TableGeometry geometry, Vector3 tableOrigin, float end)
    {
        Rect area = Shrink(config.WatchFitArea, config.CameraFitMargin);
        float start = geometry.HalfLength + config.CushionDepth + config.RailWidth + config.StandMargin;
        Transform t = camera.transform;
        Vector3 savedPosition = t.position;
        Quaternion savedRotation = t.rotation;
        float low = start, high = start + 8f;
        if (Fits(start))
        {
            high = start;
        }
        else
        {
            for (int i = 0; i < SearchSteps; i++)
            {
                float mid = (low + high) * 0.5f;
                if (Fits(mid))
                    high = mid;
                else
                    low = mid;
            }
        }
        t.SetPositionAndRotation(savedPosition, savedRotation);
        return Position(high);

        Vector3 Position(float distance) => tableOrigin + new Vector3(0f,
            config.StandingEyeHeight + (distance - start) * config.AimRiseSlope, end * distance);

        bool Fits(float distance)
        {
            Vector3 position = Position(distance);
            t.SetPositionAndRotation(position, LookWithOffset(camera, config, position, tableOrigin));
            return PocketsFit(camera, config, geometry, tableOrigin, area);
        }
    }

    // Looking at `point` so that it sits at WatchCenterHeight inside WatchFitArea (not at the screen centre).
    public static Quaternion LookWithOffset(Camera camera, MatchConfig config, Vector3 position, Vector3 point)
    {
        Rect area = config.WatchFitArea;
        float viewportY = area.y + area.height * config.WatchCenterHeight;
        float offset = Mathf.Atan((viewportY - 0.5f) * 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
        return Quaternion.LookRotation(point - position, Vector3.up) * Quaternion.Euler(offset, 0f, 0f);
    }

    // Eyes go from the table centre towards `focus` as far as they can with every pocket still in view.
    public static Quaternion WatchRotation(Camera camera, MatchConfig config, TableGeometry geometry, Vector3 tableOrigin, Vector3 position, Vector3 focus)
    {
        Rect area = Shrink(config.WatchFitArea, config.CameraFitMargin);
        Transform t = camera.transform;
        Vector3 savedPosition = t.position;
        Quaternion savedRotation = t.rotation;
        Quaternion best = LookWithOffset(camera, config, position, tableOrigin);
        Quaternion full = LookWithOffset(camera, config, position, focus);
        t.SetPositionAndRotation(position, full);
        if (PocketsFit(camera, config, geometry, tableOrigin, area))
        {
            best = full;
        }
        else
        {
            float low = 0f, high = 1f;
            for (int i = 0; i < GazeSteps; i++)
            {
                float mid = (low + high) * 0.5f;
                Quaternion rotation = LookWithOffset(camera, config, position, Vector3.Lerp(tableOrigin, focus, mid));
                t.SetPositionAndRotation(position, rotation);
                if (PocketsFit(camera, config, geometry, tableOrigin, area))
                {
                    low = mid;
                    best = rotation;
                }
                else
                {
                    high = mid;
                }
            }
        }
        t.SetPositionAndRotation(savedPosition, savedRotation);
        return best;
    }

    // Ball in hand: standing over end `end`, looking down at 50-60 degrees at the allowed area (kitchen or the
    // whole table); the pitch that brings you closest wins. The area and the cue ball fit PlaceFitArea.
    public static void PlacePose(Camera camera, MatchConfig config, TableGeometry geometry, Vector3 tableOrigin, float end,
        bool kitchenOnly, Vector3 cueBallWorld, out Vector3 position, out Quaternion rotation)
    {
        Rect area = Shrink(config.PlaceFitArea, config.CameraFitMargin);
        // The kitchen is always at the head end (-z).
        float zNear = -geometry.HalfLength;
        float zFar = kitchenOnly ? geometry.HeadStringZ : geometry.HalfLength;
        Vector3 c0 = tableOrigin + new Vector3(-geometry.HalfWidth, 0f, zNear);
        Vector3 c1 = tableOrigin + new Vector3(geometry.HalfWidth, 0f, zNear);
        Vector3 c2 = tableOrigin + new Vector3(-geometry.HalfWidth, 0f, zFar);
        Vector3 c3 = tableOrigin + new Vector3(geometry.HalfWidth, 0f, zFar);
        Vector3 center = (c0 + c3) * 0.5f;
        float yaw = end < 0f ? 0f : 180f;

        Transform t = camera.transform;
        Vector3 savedPosition = t.position;
        Quaternion savedRotation = t.rotation;
        float bestDistance = float.MaxValue;
        position = center;
        rotation = Quaternion.identity;
        for (float pitch = config.PlacePitchMin; pitch <= config.PlacePitchMax + 0.01f; pitch += 1f)
        {
            Quaternion candidate = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 forward = candidate * Vector3.forward;
            float low = 0.3f, high = 10f;
            for (int i = 0; i < SearchSteps; i++)
            {
                float mid = (low + high) * 0.5f;
                t.SetPositionAndRotation(center - forward * mid, candidate);
                if (Inside(c0) && Inside(c1) && Inside(c2) && Inside(c3) && Inside(cueBallWorld))
                    high = mid;
                else
                    low = mid;
            }
            if (high < bestDistance)
            {
                bestDistance = high;
                position = center - forward * high;
                rotation = candidate;
            }
        }
        t.SetPositionAndRotation(savedPosition, savedRotation);

        bool Inside(Vector3 world)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return viewport.z > 0f && area.Contains(new Vector2(viewport.x, viewport.y));
        }
    }
    #endregion

    private void HandleTurnStarted(TurnInfo info)
    {
        _isPlayerTurn = info.IsHuman;
        _kitchenOnly = info.KitchenOnly;
        _turnPending = true;
    }

    private void HandleTurnPhaseChanged(TurnPhase phase)
    {
        _phase = phase;
        _phasePending = true;
    }

    private void ApplyPendingEvents()
    {
        bool turn = _turnPending;
        bool phase = _phasePending;
        _turnPending = _phasePending = false;
        // Every new turn starts from the normal view (yours: aim, theirs: watch), whatever you were looking at.
        if (turn && _mode == CameraMode.Orbit)
            SetMode(CameraMode.Watch);
        if (phase)
            ApplyPhase(_phase);
        else if (turn && !_isPlayerTurn)
            SetMode(CameraMode.Watch);
    }

    private void ApplyPhase(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.PlaceCueBall:
                SetMode(CameraMode.Place);
                break;
            case TurnPhase.Aim:
            case TurnPhase.Pulling:
                // A pocket pick keeps its bird's-eye view until the pick is made.
                if (_mode == CameraMode.PocketPick)
                    break;
                if (_isPlayerTurn)
                    SetMode(CameraMode.Aim);
                else if (!_freeLook)
                    WatchOpponent();
                break;
            case TurnPhase.Look:
                SetMode(CameraMode.Orbit);
                break;
            case TurnPhase.Shooting:
                // Stay down on the shot for a moment, then straighten up and watch.
                // The opponent's shot: if you are looking around, you keep your view.
                if (_mode == CameraMode.Aim)
                {
                    _pendingWatchAt = _clock + _config.ShotHoldTime;
                    _shotHold = true;
                    // The eyes start on what the aim view is looking at (where its axis meets the cloth), so the shot
                    // does not jerk the view; from there they follow the cue ball slowly.
                    Vector3 ball = _table.Ball(0).transform.position;
                    Vector3 eyes = transform.position, forward = transform.forward;
                    _holdFocus = forward.y < -0.05f
                        ? eyes + forward * ((_table.FeltHeight - eyes.y) / forward.y)
                        : new Vector3(ball.x, _table.FeltHeight, ball.z);
                    _holdFocusVelocity = Vector3.zero;
                    _fromPosition = _posePosition;
                    _lastRotation = _poseRotation;
                }
                else if (_isPlayerTurn)
                    SetMode(CameraMode.Watch);
                break;
        }
    }

    // The opponent's cue settles on its line as they think: walk to the other end if it turns out to be there.
    private void WatchOpponent()
    {
        if (_mode != CameraMode.Watch)
        {
            SetMode(CameraMode.Watch);
            return;
        }
        float end = ShooterEnd();
        if (Mathf.Approximately(end, _watchEnd) || _walk || _shotRise)
            return;
        _fromPosition = _posePosition;
        _fromRotation = _poseRotation;
        _blend = 0f;
        _watchEnd = end;
        _watchPosition = WatchPosition(_camera, _config, _table.Geometry, TableOrigin, end);
        // v5 V1: one eased orbit round the table (like the rise), as slow as MaxCameraTurnDegPerSec at its peak asks
        // (StandUpTime at least; ~7.5 s for the far end), instead of a 0.8 s straight blend that turned up to ~340 deg/s.
        _walk = true;
        _riseFrom = OrbitFromPose(_fromPosition, TableOrigin);
        float turn = Quaternion.Angle(_fromRotation, WatchRotation(_camera, _config, _table.Geometry, TableOrigin, _watchPosition, _gaze));
        _blendTime = Mathf.Max(_config.StandUpTime, 1.875f * turn / Mathf.Max(1f, _config.MaxCameraTurnDegPerSec));
    }

    // v5 V1: the opponent has chosen its shot (BotController): walk to the end behind it now, while it settles on the
    // line, not when it already pulls the cue.
    public void OpponentChose(Vector3 ballWorld, Vector3 aimDirection)
    {
        if (_isPlayerTurn || _mode != CameraMode.Watch || _freeLook)
            return;
        SetAim(ballWorld, aimDirection);
        WatchOpponent();
    }

    #region Orbit gestures
    // LOOK on your turn; on the opponent's, the first gesture takes the camera (pivot: the table centre).
    private bool OrbitReady()
    {
        if (_mode == CameraMode.Orbit)
            return true;
        if (_isPlayerTurn || _mode != CameraMode.Watch)
            return false;
        OrbitPivot = TableOrigin;
        SetMode(CameraMode.Orbit);
        return true;
    }

    private void HandleOrbitDrag(Vector2 deltaPixels)
    {
        if (_mode == CameraMode.Aim || _mode == CameraMode.PocketPick || !OrbitReady())
            return;
        ApplyOrbitDrag(ref _orbitTarget, deltaPixels, _config);
        ConstrainOrbit(ref _orbitTarget, _config, _table.Geometry, TableOrigin);
        // The glide after letting go continues at the finger's last speed.
        float dt = Mathf.Max(1e-3f, Time.unscaledDeltaTime);
        _orbitGlide = Vector2.Lerp(_orbitGlide, deltaPixels / dt, 0.5f);
        _gliding = false;
    }

    // Two fingers while aiming (not while the cue is being pulled): closer / further, and a little sideways.
    private bool AimGestures => _mode == CameraMode.Aim && _isPlayerTurn && !Pulling && !HoldPose;

    private void HandleOrbitPinch(float scale)
    {
        if (AimGestures)
        {
            if (Mathf.Abs(scale - 1f) < _config.OrbitPinchDeadzone)
                return;
            float distance = Mathf.Clamp(_aimDistance + _aimZoomTarget, _config.AimZoomMinDistance, _config.AimZoomMaxDistance);
            float wanted = Mathf.Clamp(distance / Mathf.Max(0.05f, scale), _config.AimZoomMinDistance, _config.AimZoomMaxDistance);
            _aimZoomTarget = wanted - Mathf.Max(0f, _aimDistance);
            return;
        }
        if (Mathf.Abs(scale - 1f) < _config.OrbitPinchDeadzone || !OrbitReady())
            return;
        _gliding = false;
        _orbitGlide = Vector2.zero;
        ApplyOrbitPinch(ref _orbitTarget, scale);
        ConstrainOrbit(ref _orbitTarget, _config, _table.Geometry, TableOrigin);
    }

    private void HandleOrbitPan(Vector2 deltaPixels)
    {
        if (AimGestures)
        {
            _aimPanTarget = Mathf.Clamp(_aimPanTarget - deltaPixels.x * _config.AimPanPerPixel, -_config.AimPanMax, _config.AimPanMax);
            return;
        }
        if (!OrbitReady())
            return;
        _gliding = false;
        _orbitGlide = Vector2.zero;
        ApplyOrbitPan(ref _orbitTarget, deltaPixels, _config);
        ConstrainOrbit(ref _orbitTarget, _config, _table.Geometry, TableOrigin);
    }

    private void HandleOrbitDragEnded()
    {
        if (_mode == CameraMode.Orbit)
            _gliding = _orbitGlide.sqrMagnitude > 25f;
    }

    // TABLE VIEW or a double tap: back to watching all six pockets.
    private void HandleTableViewRequested()
    {
        if (_freeLook)
            SetMode(CameraMode.Watch);
    }

    private void SetFreeLook(bool on)
    {
        if (on == _freeLook)
            return;
        _freeLook = on;
        GameEvents.RaiseFreeLookChanged(on);
    }
    #endregion

#if UNITY_EDITOR
    // Editor checks (v5 V1): the parts of the last aim pose, to find which one makes the view jump.
    public struct AimParts
    {
        public float Along, Fit, Distance, Base, Clear, Cue, Height, Elevation;
    }

    public AimParts DebugAimParts { get; private set; }
    // ...and its blend state, sampled per frame by AimFeelChecks.Jitter.
    public string DebugFlags => $"blend {_blend:F2} hold {_shotHold} rise {_shotRise} walk {_walk} desc {_descend} bt {_blendTime:F2} free {_freeLook} phase {_phase} turn {(_isPlayerTurn ? "P" : "B")} gaze {_gaze.x:F2},{_gaze.z:F2} end {_watchEnd}";
    private float _debugClear;

    // Editor testing from `eval`: jump the orbit target to an exact pose (bounded like a gesture would be).
    public void DebugSetOrbit(OrbitState state)
    {
        if (_mode != CameraMode.Orbit)
            return;
        _orbitTarget = state;
        ConstrainOrbit(ref _orbitTarget, _config, _table.Geometry, TableOrigin);
    }
#endif

    private static float Smootherstep(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (feedback.IsPlayer && _mode == CameraMode.Aim)
            _kick = _config.StrikeKick * Mathf.Lerp(0.4f, 1f, feedback.Power01);
    }
}
