using UnityEngine;

public enum CameraMode
{
    Overview = 0,
    Aim = 1,
    Look = 2,
    Place = 3,
    Follow = 4,
    Opponent = 5
}

// The camera is the player's eyes. Aiming: bent over the cue, eyes on the line ahead. After the stroke:
// straighten up where you stood and follow the cue ball with your head. Opponent's turn: stand at the
// table edge beside them and watch. Look: walk around the table. Only ball in hand uses a top-down view.
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    // Screen area the top-down placement view must fit, leaving room for the HUD and controls.
    private static readonly Rect FitArea = new(0.04f, 0.15f, 0.92f, 0.70f);
    // While aiming, what matters (cue ball, hit ball, where it goes) must sit clear of the side strips,
    // the player panels at the top and the buttons at the bottom.
    private static readonly Rect AimFitArea = new(0.13f, 0.22f, 0.68f, 0.58f);
    private const float TableExtentX = 0.72f;
    private const float TableExtentZ = 1.28f;
    private const float OpponentSideStep = 1.0f;

    [SerializeField] private MatchConfig _config;
    [SerializeField] private TableView _table;

    private Camera _camera;
    private CameraMode _mode = CameraMode.Overview;
    private Vector3 _ballWorld;
    private Vector3 _aimDirection = Vector3.forward;
    private float _tilt;
    private bool _isPlayerTurn = true;

    private Vector3 _fromPosition;
    private Quaternion _fromRotation;
    private float _blend = 1f;
    private float _blendTime;
    private float _fitDistancePlace;

    // Standing modes: where you stand (smoothed) and what your eyes follow (smoothed).
    private Vector3 _stand;
    private Vector3 _standTarget;
    private Vector3 _standVelocity;
    private Vector3 _gaze;
    private Vector3 _gazeVelocity;
    private float _walkAngle;
    private float _lookHeight;
    private float _kick;
    private float _pendingFollowAt = -1f;

    // Aim framing: points to keep in view and the smoothed step-back distance.
    private readonly Vector3[] _focus = new Vector3[3];
    private int _focusCount;
    private float _aimDistance = -1f;
    private float _aimDistanceVelocity;

    public CameraMode Mode => _mode;
    public Camera Camera => _camera;

    public float Tilt
    {
        get => _tilt;
        set => _tilt = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _camera.fieldOfView = _config.FieldOfView;
        _tilt = _config.AimDefaultTilt;
        _lookHeight = _config.StandingEyeHeight;
        _walkAngle = -90f;
    }

    private void OnEnable()
    {
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        GameEvents.OnShotFired += HandleShotFired;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        GameEvents.OnShotFired -= HandleShotFired;
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
        _blendTime = mode is CameraMode.Follow ? _config.StandUpTime : _config.CameraBlendTime;
        _mode = mode;
        _pendingFollowAt = -1f;

        switch (mode)
        {
            case CameraMode.Aim:
                _aimDistance = -1f;
                break;
            case CameraMode.Look:
                // Start walking from wherever you are now.
                Vector2 here = _table.WorldToTable(transform.position);
                _walkAngle = Mathf.Atan2(here.y / RingZ, here.x / RingX) * Mathf.Rad2Deg;
                _lookHeight = _config.StandingEyeHeight;
                break;
            case CameraMode.Place:
                _fitDistancePlace = FitDistance(_config.PlacePitch, TableCenter);
                break;
            case CameraMode.Follow:
                // Straighten up where you were standing to shoot.
                _standTarget = StandingBehind(_ballWorld, _aimDirection);
                _stand = _standTarget;
                _gaze = _ballWorld;
                _gazeVelocity = _standVelocity = Vector3.zero;
                break;
            case CameraMode.Opponent:
                _standTarget = OpponentSpot();
                _stand = _standTarget;
                _gaze = _ballWorld;
                _gazeVelocity = _standVelocity = Vector3.zero;
                break;
        }
    }

    // Look mode: horizontal drag walks around the table, vertical drag crouches or rises.
    public void Orbit(Vector2 deltaPixels)
    {
        _walkAngle -= deltaPixels.x * _config.LookDegreesPerPixel;
        _lookHeight = Mathf.Clamp(_lookHeight - deltaPixels.y * _config.LookHeightPerPixel, _config.LookMinHeight, _config.LookMaxHeight);
    }

    private Vector3 TableCenter => _table.TableToWorld(Vector2.zero, 0f);
    private float RingX => TableExtentX + _config.StandMargin;
    private float RingZ => TableExtentZ + _config.StandMargin;
    private float EyeY => _table.FeltHeight + _config.StandingEyeHeight;

    private void LateUpdate()
    {
        if (_pendingFollowAt >= 0f && Time.unscaledTime >= _pendingFollowAt)
            SetMode(CameraMode.Follow);

        float dt = Time.unscaledDeltaTime;
        TargetPose(dt, out Vector3 position, out Quaternion rotation);
        if (_blend < 1f)
        {
            _blend = Mathf.Min(1f, _blend + dt / Mathf.Max(0.01f, _blendTime));
            float t = Mathf.SmoothStep(0f, 1f, _blend);
            position = Vector3.Lerp(_fromPosition, position, t);
            rotation = Quaternion.Slerp(_fromRotation, rotation, t);
        }

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
            case CameraMode.Look:
            {
                float radians = _walkAngle * Mathf.Deg2Rad;
                Vector3 spot = _table.TableToWorld(new Vector2(Mathf.Cos(radians) * RingX, Mathf.Sin(radians) * RingZ), 0f);
                position = new Vector3(spot.x, _table.FeltHeight + _lookHeight, spot.z);
                rotation = Quaternion.LookRotation(TableCenter - position, Vector3.up);
                break;
            }
            case CameraMode.Place:
                rotation = Quaternion.Euler(_config.PlacePitch, 0f, 0f);
                position = TableCenter - rotation * Vector3.forward * _fitDistancePlace;
                break;
            case CameraMode.Follow:
            case CameraMode.Opponent:
            {
                if (_mode == CameraMode.Opponent)
                    _standTarget = OpponentSpot();
                _stand = Vector3.SmoothDamp(_stand, _standTarget, ref _standVelocity, 1.2f, Mathf.Infinity, dt);
                // Watching yourself: follow the cue ball. Watching the opponent: look where their shot is going.
                Vector3 watch = _mode == CameraMode.Follow ? _table.Ball(0).transform.position : _ballWorld + _aimDirection * 0.5f;
                // Eyes track the action with a little lag, like a head turning.
                _gaze = Vector3.SmoothDamp(_gaze, watch, ref _gazeVelocity, 0.25f, Mathf.Infinity, dt);
                position = new Vector3(_stand.x, EyeY, _stand.z);
                Vector3 look = Vector3.Lerp(_gaze, TableCenter, 0.15f);
                rotation = Quaternion.LookRotation(look - position, Vector3.up);
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

    private void AimPose(float distance, out Vector3 position, out Quaternion rotation)
    {
        float height = Mathf.Lerp(_config.AimLowHeight, _config.AimHighHeight, _tilt) + (distance - _config.AimDistance) * _config.AimRiseSlope;
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

    // Breathing sway (never in the top-down placement view) and a short push forward on the stroke.
    private void ApplyHeadMotion(float dt, ref Vector3 position, ref Quaternion rotation)
    {
        if (_mode == CameraMode.Place)
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

    // A point on the walkway around the table, straight behind `from` against `direction`.
    private Vector3 StandingBehind(Vector3 fromWorld, Vector3 directionWorld)
    {
        Vector2 from = _table.WorldToTable(fromWorld);
        Vector2 back = -_table.WorldDirectionToTable(directionWorld);
        float tx = Mathf.Abs(back.x) > 1e-4f ? (Mathf.Sign(back.x) * RingX - from.x) / back.x : float.MaxValue;
        float tz = Mathf.Abs(back.y) > 1e-4f ? (Mathf.Sign(back.y) * RingZ - from.y) / back.y : float.MaxValue;
        Vector2 spot = from + back * Mathf.Max(0f, Mathf.Min(tx, tz));
        return _table.TableToWorld(spot, 0f);
    }

    // Beside the opponent (who stands behind their cue ball), a step to the side, on the walkway.
    private Vector3 OpponentSpot()
    {
        Vector3 opponent = StandingBehind(_ballWorld, _aimDirection);
        Vector2 spot = _table.WorldToTable(opponent);
        Vector2 aim = _table.WorldDirectionToTable(_aimDirection);
        spot += new Vector2(-aim.y, aim.x) * OpponentSideStep;
        // Back onto the walkway, outside the rails.
        if (Mathf.Abs(spot.x) < RingX && Mathf.Abs(spot.y) < RingZ)
        {
            float toX = RingX - Mathf.Abs(spot.x);
            float toZ = RingZ - Mathf.Abs(spot.y);
            if (toX < toZ)
                spot.x = Mathf.Sign(spot.x == 0f ? 1f : spot.x) * RingX;
            else
                spot.y = Mathf.Sign(spot.y == 0f ? 1f : spot.y) * RingZ;
        }
        spot.x = Mathf.Clamp(spot.x, -RingX, RingX);
        spot.y = Mathf.Clamp(spot.y, -RingZ, RingZ);
        return _table.TableToWorld(spot, 0f);
    }

    // Smallest distance along the view direction at which the table outline fits FitArea.
    private float FitDistance(float pitch, Vector3 focus)
    {
        Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 savedPosition = transform.position;
        Quaternion savedRotation = transform.rotation;
        float low = 0.5f, high = 12f;
        for (int i = 0; i < 24; i++)
        {
            float mid = (low + high) * 0.5f;
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * mid, rotation);
            if (TableFits())
                high = mid;
            else
                low = mid;
        }
        transform.SetPositionAndRotation(savedPosition, savedRotation);
        return high;
    }

    private bool TableFits()
    {
        for (int corner = 0; corner < 4; corner++)
        {
            var local = new Vector2(corner % 2 == 0 ? -TableExtentX : TableExtentX, corner < 2 ? -TableExtentZ : TableExtentZ);
            Vector3 viewport = _camera.WorldToViewportPoint(_table.TableToWorld(local, 0.07f));
            if (viewport.z <= 0f || !FitArea.Contains(new Vector2(viewport.x, viewport.y)))
                return false;
        }
        return true;
    }

    private void HandleTurnStarted(TurnInfo info)
    {
        _isPlayerTurn = info.IsHuman;
        if (!info.IsHuman)
            SetMode(CameraMode.Opponent);
    }

    private void HandleTurnPhaseChanged(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.PlaceCueBall:
                SetMode(CameraMode.Place);
                break;
            case TurnPhase.Aim:
            case TurnPhase.Pulling:
                SetMode(_isPlayerTurn ? CameraMode.Aim : CameraMode.Opponent);
                break;
            case TurnPhase.Look:
                SetMode(CameraMode.Look);
                break;
            case TurnPhase.Shooting:
                // Stay down on the shot for a moment, then straighten up and watch.
                if (_mode == CameraMode.Aim)
                    _pendingFollowAt = Time.unscaledTime + _config.StandUpDelay;
                else if (_mode != CameraMode.Opponent)
                    SetMode(CameraMode.Follow);
                break;
        }
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (feedback.IsPlayer && _mode == CameraMode.Aim)
            _kick = _config.StrikeKick * Mathf.Lerp(0.4f, 1f, feedback.Power01);
    }
}
