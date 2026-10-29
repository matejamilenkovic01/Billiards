using UnityEngine;

public enum CameraMode
{
    Overview = 0,
    Aim = 1,
    Orbit = 2,
    Place = 3,
    Watch = 4
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
    private bool _shotRise;
    private Quaternion _lastRotation;

    // Aim framing: points to keep in view and the smoothed step-back distance.
    private readonly Vector3[] _focus = new Vector3[3];
    private int _focusCount;
    private float _aimDistance = -1f;
    private float _aimDistanceVelocity;

    public CameraMode Mode => _mode;
    public Camera Camera => _camera;

    // The cue's current elevation (degrees): with the butt raised, you stand more upright over it.
    public float CueElevation { get; set; }

    // While the player drags the cue ball in the aim view, the view holds still (it would chase the ball).
    public bool HoldPose { get; set; }
    // The camera is still blending into its mode's pose (checks).
    public bool IsBlending => _blend < 1f || _shotHold || _shotRise;

    public float Tilt
    {
        get => _tilt;
        set => _tilt = Mathf.Clamp01(value);
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
    }

    // Controllers feed the cue ball and aim every frame they own the turn.
    public void SetAim(Vector3 ballWorld, Vector3 aimDirection)
    {
        _ballWorld = ballWorld;
        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude > 1e-6f)
            _aimDirection = aimDirection.normalized;
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

        _fromPosition = transform.position;
        _fromRotation = transform.rotation;
        _blend = 0f;
        _blendTime = mode == CameraMode.Watch && _mode == CameraMode.Aim ? _config.ShotRiseTime : _config.CameraBlendTime;
        _shotRise = mode == CameraMode.Watch && _mode == CameraMode.Aim;
        _shotHold = false;
        CameraMode previous = _mode;
        _mode = mode;
        _pendingWatchAt = -1f;
        _gliding = false;
        SetFreeLook(mode == CameraMode.Orbit && !_isPlayerTurn);

        switch (mode)
        {
            case CameraMode.Aim:
                _aimDistance = -1f;
                break;
            case CameraMode.Orbit:
            {
                // From exactly where you are now, around the pivot: no jump.
                _orbitTarget = OrbitFromPose(transform.position, OrbitPivot);
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
                _gaze = WatchFocus();
                _gazeVelocity = Vector3.zero;
                break;
        }
    }

    private Vector3 TableOrigin => _table.TableToWorld(Vector2.zero, 0f);
    private float RingZ => TableExtentZ + _config.StandMargin;

    private void LateUpdate()
    {
        ApplyPendingEvents();
        if (_pendingWatchAt >= 0f && Time.unscaledTime >= _pendingWatchAt)
        {
            // The rise starts when it was due, not when this frame noticed (v4 V7: ~2 s whatever the frame rate).
            float late = Time.unscaledTime - _pendingWatchAt;
            SetMode(CameraMode.Watch);
            if (_shotRise)
                _blend = Mathf.Clamp01(late / Mathf.Max(0.01f, _blendTime));
        }
        if (HoldPose && _mode == CameraMode.Aim)
            return;

        float dt = Time.unscaledDeltaTime;
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
            rotation = Quaternion.LookRotation(_table.Ball(0).transform.position - position, Vector3.up);
        }
        else if (_blend < 1f && _shotRise)
        {
            // v4 V7: straightening up like a person: the height comes first, the step back later (smootherstep),
            // the eyes stay on the balls (Watch's gaze), never turning faster than MaxCameraTurnDegPerSec.
            _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, _blendTime));
            float up = Smootherstep(Mathf.Clamp01(_blend / 0.6f));
            float back = Smootherstep(Mathf.Clamp01((_blend - 0.25f) / 0.75f));
            var blended = new Vector3(Mathf.Lerp(_fromPosition.x, position.x, back), Mathf.Lerp(_fromPosition.y, position.y, up), Mathf.Lerp(_fromPosition.z, position.z, back));
            Quaternion look = Quaternion.LookRotation(_gaze - blended, Vector3.up);
            rotation = Quaternion.Slerp(look, rotation, Smootherstep(_blend));
            position = blended;
        }
        else if (_blend < 1f)
        {
            _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, _blendTime));
            float t = Mathf.SmoothStep(0f, 1f, _blend);
            position = Vector3.Lerp(_fromPosition, position, t);
            rotation = Quaternion.Slerp(_fromRotation, rotation, t);
        }
        if (_shotHold || _shotRise)
        {
            Quaternion target = rotation;
            rotation = Quaternion.RotateTowards(_lastRotation, target, _config.MaxCameraTurnDegPerSec * dt);
            // The limit stays on until the eyes have caught up with Watch: no snap at the end.
            if (_blend >= 1f && !_shotHold && Quaternion.Angle(rotation, target) < 0.5f)
                _shotRise = false;
        }
        _lastRotation = rotation;

        ApplyHeadMotion(dt, ref position, ref rotation);
        transform.SetPositionAndRotation(position, rotation);
    }

    private void TargetPose(float dt, out Vector3 position, out Quaternion rotation)
    {
        switch (_mode)
        {
            case CameraMode.Aim:
            {
                // Behind the cue, eyes on the line; steps back (and up) only as far as needed to keep the
                // hit ball and its pocket in view.
                float wanted = FitAimDistance();
                if (_aimDistance < 0f)
                    _aimDistance = wanted;
                _aimDistance = Mathf.SmoothDamp(_aimDistance, wanted, ref _aimDistanceVelocity, _config.AimFramingSmooth, Mathf.Infinity, dt);
                AimPose(_aimDistance, out position, out rotation);
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
            case CameraMode.Watch:
            {
                // Eyes track the action with a little lag, like a head turning, but never so far that a
                // pocket leaves the view.
                _gaze = Vector3.SmoothDamp(_gaze, WatchFocus(), ref _gazeVelocity, 0.25f, Mathf.Infinity, dt);
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

    private void AimPose(float distance, out Vector3 position, out Quaternion rotation)
    {
        float height = Mathf.Lerp(_config.AimLowHeight, _config.AimHighHeight, _tilt) + (distance - _config.AimDistance) * _config.AimRiseSlope;
        // Never inside or just over the frame, and always seeing the cue ball over the cushion and rail (G6).
        height = Mathf.Max(height, CueClearance.MinEyeHeight(_frame, _table.WorldToTable(_ballWorld),
            _table.WorldDirectionToTable(_aimDirection), distance, _config.AimSightClearance, _config.AimFrameClearance));
        height = Mathf.Max(height, _config.BallRadius + distance * Mathf.Tan(CueElevation * Mathf.Deg2Rad) + _config.AimAboveCue);
        Vector3 behind = _ballWorld - _aimDirection * distance;
        position = new Vector3(behind.x, _table.FeltHeight + height, behind.z);

        // Eyes rest further down the line when the action is further away.
        float along = _config.AimLookAhead;
        if (_focusCount > 1)
        {
            float sum = 0f;
            for (int i = 1; i < _focusCount; i++)
                sum += Vector3.Dot(_focus[i] - _ballWorld, _aimDirection);
            along = Mathf.Clamp(sum / (_focusCount - 1) * 0.6f, 0.4f, 1.6f);
        }
        Vector3 focus = _ballWorld + _aimDirection * along;
        focus.y = _table.FeltHeight;
        rotation = Quaternion.LookRotation(focus - position, Vector3.up);
    }

    // Smallest step back at which every focus point is inside AimFitArea.
    private float FitAimDistance()
    {
        if (_focusCount == 0)
            return _config.AimDistance;

        Vector3 savedPosition = transform.position;
        Quaternion savedRotation = transform.rotation;
        float result = _config.AimMaxDistance;
        for (float d = _config.AimDistance; d <= _config.AimMaxDistance; d += 0.05f)
        {
            AimPose(d, out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            bool fits = true;
            for (int i = 0; i < _focusCount && fits; i++)
            {
                Vector3 viewport = _camera.WorldToViewportPoint(_focus[i]);
                fits = viewport.z > 0f && AimFitArea.Contains(new Vector2(viewport.x, viewport.y));
            }
            if (fits)
            {
                result = d;
                break;
            }
        }
        transform.SetPositionAndRotation(savedPosition, savedRotation);
        return result;
    }

    // Breathing sway (never while placing the cue ball) and a short push forward on the stroke.
    private void ApplyHeadMotion(float dt, ref Vector3 position, ref Quaternion rotation)
    {
        if (_mode == CameraMode.Place || _mode == CameraMode.Orbit)
            return;

        float time = Time.unscaledTime * _config.HeadSwaySpeed;
        float standing = _mode == CameraMode.Aim ? 1f : 1.6f;
        var sway = new Vector3(Mathf.PerlinNoise(time, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, time) - 0.5f, 0f) * 2f;
        position += rotation * sway * _config.HeadSwayMetres * standing;
        rotation *= Quaternion.Euler(sway.y * _config.HeadSwayDegrees * standing, sway.x * _config.HeadSwayDegrees * standing, 0f);

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
                    _pendingWatchAt = Time.unscaledTime + _config.ShotHoldTime;
                    _shotHold = true;
                    _fromPosition = transform.position;
                    _lastRotation = transform.rotation;
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
        if (Mathf.Approximately(end, _watchEnd))
            return;
        _fromPosition = transform.position;
        _fromRotation = transform.rotation;
        _blend = 0f;
        _blendTime = _config.StandUpTime;
        _watchEnd = end;
        _watchPosition = WatchPosition(_camera, _config, _table.Geometry, TableOrigin, end);
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
        if (!OrbitReady())
            return;
        ApplyOrbitDrag(ref _orbitTarget, deltaPixels, _config);
        ConstrainOrbit(ref _orbitTarget, _config, _table.Geometry, TableOrigin);
        // The glide after letting go continues at the finger's last speed.
        float dt = Mathf.Max(1e-3f, Time.unscaledDeltaTime);
        _orbitGlide = Vector2.Lerp(_orbitGlide, deltaPixels / dt, 0.5f);
        _gliding = false;
    }

    private void HandleOrbitPinch(float scale)
    {
        if (Mathf.Abs(scale - 1f) < _config.OrbitPinchDeadzone || !OrbitReady())
            return;
        _gliding = false;
        _orbitGlide = Vector2.zero;
        ApplyOrbitPinch(ref _orbitTarget, scale);
        ConstrainOrbit(ref _orbitTarget, _config, _table.Geometry, TableOrigin);
    }

    private void HandleOrbitPan(Vector2 deltaPixels)
    {
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
