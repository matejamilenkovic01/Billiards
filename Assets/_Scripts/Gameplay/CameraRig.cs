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

// One perspective camera, five poses (implementation_definition 4.6.7). Mode changes blend over
// CameraBlendTime; inside a mode the pose follows its target every frame.
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    // Screen area the whole-table views must fit in, leaving room for the HUD at the top and the controls below.
    private static readonly Rect FitArea = new(0.04f, 0.15f, 0.92f, 0.70f);
    private const float TableExtentX = 0.72f;
    private const float TableExtentZ = 1.28f;
    private const float FollowLead = 0.25f;

    [SerializeField] private MatchConfig _config;
    [SerializeField] private TableView _table;

    private Camera _camera;
    private CameraMode _mode = CameraMode.Overview;
    private Vector3 _ballWorld;
    private Vector3 _aimDirection = Vector3.forward;
    private float _tilt;
    private float _lookYaw;
    private float _lookPitch;
    private bool _isPlayerTurn = true;

    private Vector3 _fromPosition;
    private Quaternion _fromRotation;
    private float _blend = 1f;
    private float _fitDistancePlace;
    private float _fitDistanceFollow;
    private float _followYaw;
    private Vector3 _followFocus;
    private Vector3 _followVelocity;

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
    }

    private void OnEnable()
    {
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
    }

    // Controllers feed the cue ball and aim every frame they own the turn.
    public void SetAim(Vector3 ballWorld, Vector3 aimDirection)
    {
        _ballWorld = ballWorld;
        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude > 1e-6f)
            _aimDirection = aimDirection.normalized;
    }

    public void SetMode(CameraMode mode)
    {
        if (mode == _mode)
            return;

        _fromPosition = transform.position;
        _fromRotation = transform.rotation;
        _blend = 0f;
        _mode = mode;

        switch (mode)
        {
            case CameraMode.Look:
                _lookYaw = Mathf.Atan2(_aimDirection.x, _aimDirection.z) * Mathf.Rad2Deg;
                _lookPitch = Mathf.Lerp(_config.LookMinPitch, _config.LookMaxPitch, 0.2f);
                break;
            case CameraMode.Place:
                _fitDistancePlace = FitDistance(0f, _config.PlacePitch, TableCenter);
                break;
            case CameraMode.Follow:
                float aimYaw = Mathf.Atan2(_aimDirection.x, _aimDirection.z) * Mathf.Rad2Deg;
                // Snap to the long axis so the whole (portrait) table stays in frame.
                _followYaw = Mathf.Abs(Mathf.DeltaAngle(aimYaw, 0f)) <= 90f ? 0f : 180f;
                _fitDistanceFollow = FitDistance(_followYaw, _config.FollowPitch, TableCenter);
                _followFocus = TableCenter;
                _followVelocity = Vector3.zero;
                break;
        }
    }

    // Horizontal drag orbits the table, vertical drag tilts between low and near top-down.
    public void Orbit(Vector2 deltaPixels)
    {
        _lookYaw += deltaPixels.x * _config.LookDegreesPerPixel;
        _lookPitch = Mathf.Clamp(_lookPitch - deltaPixels.y * _config.LookPitchPerPixel, _config.LookMinPitch, _config.LookMaxPitch);
    }

    private Vector3 TableCenter => _table.TableToWorld(Vector2.zero, 0f);

    private void LateUpdate()
    {
        TargetPose(out Vector3 position, out Quaternion rotation);
        if (_blend < 1f)
        {
            _blend = Mathf.Min(1f, _blend + Time.unscaledDeltaTime / Mathf.Max(0.01f, _config.CameraBlendTime));
            float t = Mathf.SmoothStep(0f, 1f, _blend);
            position = Vector3.Lerp(_fromPosition, position, t);
            rotation = Quaternion.Slerp(_fromRotation, rotation, t);
        }
        transform.SetPositionAndRotation(position, rotation);
    }

    private void TargetPose(out Vector3 position, out Quaternion rotation)
    {
        switch (_mode)
        {
            case CameraMode.Aim:
            {
                float height = Mathf.Lerp(_config.AimLowHeight, _config.AimHighHeight, _tilt);
                float pitch = Mathf.Lerp(_config.AimLowPitch, _config.AimHighPitch, _tilt);
                position = _ballWorld - _aimDirection * _config.AimDistance + Vector3.up * height;
                rotation = Quaternion.LookRotation(_aimDirection, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
                break;
            }
            case CameraMode.Look:
            {
                float distance = _config.LookRadius / Mathf.Cos(_config.LookMinPitch * Mathf.Deg2Rad);
                rotation = Quaternion.Euler(_lookPitch, _lookYaw, 0f);
                position = TableCenter - rotation * Vector3.forward * distance;
                break;
            }
            case CameraMode.Place:
                rotation = Quaternion.Euler(_config.PlacePitch, 0f, 0f);
                position = TableCenter - rotation * Vector3.forward * _fitDistancePlace;
                break;
            case CameraMode.Follow:
            {
                // Lean a little towards the cue ball while it rolls, without losing the table.
                Vector3 lead = Vector3.Lerp(TableCenter, _table.Ball(0).transform.position, FollowLead);
                _followFocus = Vector3.SmoothDamp(_followFocus, lead, ref _followVelocity, 0.4f, Mathf.Infinity, Time.unscaledDeltaTime);
                rotation = Quaternion.Euler(_config.FollowPitch, _followYaw, 0f);
                position = _followFocus - rotation * Vector3.forward * _fitDistanceFollow;
                break;
            }
            case CameraMode.Opponent:
            {
                Vector3 behind = _ballWorld - _aimDirection * _config.OpponentDistance;
                position = new Vector3(behind.x, _table.FeltHeight + _config.OpponentHeight, behind.z);
                rotation = Quaternion.LookRotation(_aimDirection, Vector3.up) * Quaternion.Euler(_config.OpponentPitch, 0f, 0f);
                break;
            }
            default:
            {
                rotation = Quaternion.Euler(_config.FollowPitch, 0f, 0f);
                float distance = _fitDistanceFollow > 0f ? _fitDistanceFollow : FitDistance(0f, _config.FollowPitch, TableCenter);
                _fitDistanceFollow = distance;
                position = TableCenter - rotation * Vector3.forward * distance;
                break;
            }
        }
    }

    // Smallest distance along the view direction at which the table outline fits FitArea (binary search
    // on the real projection, so it follows any aspect ratio).
    private float FitDistance(float yaw, float pitch, Vector3 focus)
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
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
        _isPlayerTurn = info.IsPlayer;
        if (!info.IsPlayer)
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
                SetMode(CameraMode.Follow);
                break;
        }
    }
}
