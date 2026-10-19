using UnityEngine;
using UnityEngine.InputSystem;

// The player's turn: PlaceCueBall (ball in hand) -> Aim <-> Look -> Pulling -> shot (implementation_definition 4.7).
// Listens to the shot control events from the UI and drives the cue, the guide and the camera.
public class PlayerShotController : MonoBehaviour
{
    [SerializeField] private AimGuideView _guide;

    private MatchController _match;
    private System.Random _random;
    private TurnPhase _phase = TurnPhase.None;
    private Vector2 _aim = Vector2.up;
    private float _pull;
    private float _barTime;
    private bool _snapped;
    private bool _illegalHintShown;
    private Vector2 _lastValidCueBall;
    // Ball in hand: what the current press started on, and whether the cue ball is in the air.
    private enum Press { None, CueBall, EmptyTable }
    private Press _press;
    private bool _holding;
    // Fine aim: degrees turned since the last haptic tick, and how long an arrow has been held.
    private float _fineSinceTick;
    private int _stepRepeats;
    private Prediction _prediction;
    private CueDefinition _cue;
    private ShotAccuracy.Settings _accuracy;

    public TurnPhase Phase => _phase;
    public Vector2 Aim => _aim;
    public Prediction CurrentPrediction => _prediction;

    private void Awake() => _random = new System.Random(System.Environment.TickCount ^ 0x5f3759df);

    private void OnEnable()
    {
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnLookDrag += HandleLookDrag;
        GameEvents.OnLookModeToggled += HandleLookModeToggled;
        GameEvents.OnFineAimDrag += HandleFineAimDrag;
        GameEvents.OnFineAimStep += HandleFineAimStep;
        GameEvents.OnCueBallDragged += HandleCueBallDragged;
        GameEvents.OnCueBallDragEnded += HandleCueBallDragEnded;
        GameEvents.OnTableTapped += HandleTableTapped;
        GameEvents.OnTablePressed += HandleTablePressed;
        GameEvents.OnPlaceCueBallConfirmed += HandlePlaceCueBallConfirmed;
        GameEvents.OnMoveCueBallRequested += HandleMoveCueBallRequested;
        GameEvents.OnPullChanged += HandlePullChanged;
        GameEvents.OnPullReleased += HandlePullReleased;
        GameEvents.OnMatchEnded += HandleMatchEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnLookDrag -= HandleLookDrag;
        GameEvents.OnLookModeToggled -= HandleLookModeToggled;
        GameEvents.OnFineAimDrag -= HandleFineAimDrag;
        GameEvents.OnFineAimStep -= HandleFineAimStep;
        GameEvents.OnCueBallDragged -= HandleCueBallDragged;
        GameEvents.OnCueBallDragEnded -= HandleCueBallDragEnded;
        GameEvents.OnTableTapped -= HandleTableTapped;
        GameEvents.OnTablePressed -= HandleTablePressed;
        GameEvents.OnPlaceCueBallConfirmed -= HandlePlaceCueBallConfirmed;
        GameEvents.OnMoveCueBallRequested -= HandleMoveCueBallRequested;
        GameEvents.OnPullChanged -= HandlePullChanged;
        GameEvents.OnPullReleased -= HandlePullReleased;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
    }

    private bool Active => _phase != TurnPhase.None && _match != null && _match.IsHumanTurn;
    private MatchConfig Config => _match.Config;

    #region Turn
    private void HandleTurnStarted(TurnInfo info)
    {
        _match = MatchController.Instance;
        _press = Press.None;
        _holding = false;
        _match.CameraRig.HoldPose = false;
        if (!_match.IsHumanTurn)
        {
            _phase = TurnPhase.None;
            _guide.HideAim();
            _guide.HidePlacement();
            return;
        }

        _cue = _match.PlayerCue;
        _accuracy = Config.AccuracySettings(_cue.GreenHalf, _cue.YellowWidth);
        GameEvents.RaiseReleaseMeterSetup(new ReleaseMeterSettings
        {
            GreenHalf = _cue.GreenHalf,
            YellowWidth = _cue.YellowWidth,
            Period = _cue.OscillationPeriod
        });

        _pull = 0f;
        _illegalHintShown = false;
        _snapped = false;
        _lastValidCueBall = _match.Balls[0].Pos;
        _aim = ChooseDefaultAim();
        SetPhase(info.BallInHand ? TurnPhase.PlaceCueBall : TurnPhase.Aim);
    }

    // A neutral start: full ball on the nearest reachable legal ball, never a ready-made pot (G8).
    private Vector2 ChooseDefaultAim() => DefaultAim.Choose(_match.Predictor, _match.Balls, _match.LegalTargets,
        _match.Rules.IsBreakShot, _match.Geometry.FootSpot, Config.BallRadius, _random);

    private void SetPhase(TurnPhase phase)
    {
        _phase = phase;
        bool placing = phase == TurnPhase.PlaceCueBall;
        _match.Cue.SetVisible(!placing && phase != TurnPhase.None);
        if (placing)
        {
            _guide.HideAim();
            _guide.ShowPlacement(_match.Balls[0].Pos, true, _match.Rules.KitchenOnly);
        }
        else
        {
            _guide.HidePlacement();
        }
        if (phase == TurnPhase.Look || phase == TurnPhase.None)
            _guide.HideAim();
        GameEvents.RaiseTurnPhaseChanged(phase);
    }

    private void HandleMatchEnded(MatchResult result)
    {
        _phase = TurnPhase.None;
        _guide.HideAim();
        _guide.HidePlacement();
    }
    #endregion

    private void Update()
    {
        if (!Active)
            return;

        if (_phase == TurnPhase.Aim)
            ReadKeyboardFineAim();

        if (_phase == TurnPhase.Pulling)
        {
            _barTime += Time.deltaTime;
            float position = ShotAccuracy.BarPosition(_barTime, _cue.OscillationPeriod);
            GameEvents.RaiseReleaseMeterChanged(position, _pull);
        }

        if (_phase == TurnPhase.Aim || _phase == TurnPhase.Pulling || _phase == TurnPhase.Look)
        {
            Vector3 ball = _match.Table.BallCenterWorld(_match.Balls[0].Pos);
            Vector3 direction = _match.Table.TableDirectionToWorld(_aim);
            _match.Cue.SetPose(ball, direction, _pull);
            _match.CameraRig.SetAim(ball, direction);
            _match.CameraRig.CueElevation = _match.Cue.Elevation;
        }

        if (_phase == TurnPhase.Aim || _phase == TurnPhase.Pulling)
        {
            _prediction = _match.Predictor.Predict(_match.Balls, _aim, _match.LegalTargets);
            _guide.ShowAim(_prediction, _cue.ObjectGuideDiameters * 2f * Config.BallRadius);
            FrameShot();
            if (_prediction.Hit == PredictionHit.Ball && !_prediction.IsLegal && !_illegalHintShown)
            {
                _illegalHintShown = true;
                string hint = _match.Rules.IsOnEight(_match.Rules.Turn) ? "Hit the 8-ball now"
                    : _prediction.HitBall == BallGroups.EightBall ? "Hit the 8-ball only after clearing your group"
                    : "Hit your own group first";
                GameEvents.RaiseToastRequested(new ToastMessage(hint, ToastStyle.Warning));
            }
        }
    }

    // Tell the camera what must stay in view: the cue ball, the hit ball and where that ball is heading.
    private void FrameShot()
    {
        TableView table = _match.Table;
        Vector3 cue = table.BallCenterWorld(_match.Balls[0].Pos);
        if (_prediction.Hit != PredictionHit.Ball)
        {
            _match.CameraRig.SetFocus(cue, table.BallCenterWorld(_prediction.Point), default, 2);
            return;
        }

        Vector2 objectPos = _match.Balls[_prediction.HitBall].Pos;
        Vector2 heading = _prediction.ObjectDir;
        TableGeometry geometry = _match.Geometry;
        float tx = Mathf.Abs(heading.x) > 1e-4f ? (Mathf.Sign(heading.x) * geometry.HalfWidth - objectPos.x) / heading.x : float.MaxValue;
        float ty = Mathf.Abs(heading.y) > 1e-4f ? (Mathf.Sign(heading.y) * geometry.HalfLength - objectPos.y) / heading.y : float.MaxValue;
        Vector2 destination = objectPos + heading * Mathf.Clamp(Mathf.Min(tx, ty), 0f, 2f);
        _match.CameraRig.SetFocus(cue, table.BallCenterWorld(objectPos), table.TableToWorld(destination, 0f), 3);
    }

    #region Aiming
    private void HandleLookDrag(Vector2 deltaPixels)
    {
        if (!Active)
            return;

        if (_phase == TurnPhase.Look)
        {
            _match.CameraRig.Orbit(deltaPixels);
            return;
        }
        if (_phase != TurnPhase.Aim || _holding)
            return;   // locked while pulling (or moving the cue ball), so a stray finger can't swing the aim

        RotateAim(-deltaPixels.x * Config.AimDegreesPerPixel, true);
        _match.CameraRig.Tilt -= deltaPixels.y * Config.TiltPerPixel;
    }

    // The wheel at the bottom: dragging right turns the cue right (clockwise seen from above).
    private void HandleFineAimDrag(float deltaPixels)
    {
        if (Active && _phase == TurnPhase.Aim)
            FineTurn(-deltaPixels * Config.FineAimDegreesPerPixel);
    }

    // The arrows: one small step per tap; held, the steps repeat and grow after FineAimAccelerateAfter.
    private void HandleFineAimStep(int direction, bool repeating)
    {
        if (!Active || _phase != TurnPhase.Aim)
            return;
        _stepRepeats = repeating ? _stepRepeats + 1 : 0;
        float held = repeating ? Config.FineAimRepeatDelay + _stepRepeats / Mathf.Max(1f, Config.FineAimRepeatRate) : 0f;
        float ramp = Mathf.InverseLerp(Config.FineAimAccelerateAfter, Config.FineAimAccelerateAfter * 2f, held);
        float step = Mathf.Lerp(Config.FineAimStepDegrees, Config.FineAimMaxStepDegrees, ramp);
        FineTurn(-Mathf.Sign(direction) * step);
    }

    private void FineTurn(float degrees)
    {
        RotateAim(degrees, false);
        _fineSinceTick += Mathf.Abs(degrees);
        if (_fineSinceTick >= Config.FineAimTickDegrees)
        {
            _fineSinceTick %= Config.FineAimTickDegrees;
            GameEvents.RaiseFineAimTick();
        }
    }

    private void ReadKeyboardFineAim()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        float step = Config.KeyFineAimDegreesPerFrame;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            RotateAim(step, false);
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            RotateAim(-step, false);
    }

    private void RotateAim(float degrees, bool magnet)
    {
        _aim = ShotAccuracy.Rotate(_aim, degrees).normalized;
        if (magnet)
            ApplyMagnet();
        else
            _snapped = false;
    }

    // A full-ball hit on a legal target pulls the aim in by up to AimMagnetDegrees, with a light buzz.
    private void ApplyMagnet()
    {
        Prediction prediction = _match.Predictor.Predict(_match.Balls, _aim, _match.LegalTargets);
        if (prediction.Hit != PredictionHit.Ball || !prediction.IsLegal)
        {
            _snapped = false;
            return;
        }

        Vector2 fullBall = (_match.Balls[prediction.HitBall].Pos - _match.Balls[0].Pos).normalized;
        if (Vector2.Angle(_aim, fullBall) <= Config.AimMagnetDegrees)
        {
            _aim = fullBall;
            if (!_snapped)
                GameEvents.RaiseAimSnapped();
            _snapped = true;
        }
        else
        {
            _snapped = false;
        }
    }

    private void HandleLookModeToggled(bool on)
    {
        if (!Active)
            return;
        if (on && _phase == TurnPhase.Aim)
            SetPhase(TurnPhase.Look);
        else if (!on && _phase == TurnPhase.Look)
            SetPhase(TurnPhase.Aim);
    }

    // In Look mode a tap on a ball aims straight at it and returns to the shot.
    private void HandleTableTapped(Vector2 screenPosition)
    {
        if (!Active || _phase != TurnPhase.Look)
            return;
        if (!_match.Table.ScreenToTable(_match.CameraRig.Camera, screenPosition, out Vector2 point))
            return;

        int best = -1;
        float bestDistance = Config.BallRadius * 3f;
        for (int i = 1; i < _match.Balls.Length; i++)
        {
            if (_match.Balls[i].Pocketed)
                continue;
            float distance = (_match.Balls[i].Pos - point).magnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        if (best < 0)
            return;

        _aim = (_match.Balls[best].Pos - _match.Balls[0].Pos).normalized;
        SetPhase(TurnPhase.Aim);
    }
    #endregion

    #region Ball in hand
    // A press on the cue ball picks it up (while placing it, or while aiming with ball in hand); a drag that
    // starts on the empty table while placing it means "done here": straight on to aiming.
    private void HandleTablePressed(Vector2 screenPosition)
    {
        _press = Press.None;
        if (!Active)
            return;
        bool canMove = _phase == TurnPhase.PlaceCueBall || (_phase == TurnPhase.Aim && _match.Rules.BallInHand);
        if (!canMove)
            return;
        if (IsOnCueBall(screenPosition))
        {
            _press = Press.CueBall;
            PickUp();
        }
        else if (_phase == TurnPhase.PlaceCueBall)
        {
            _press = Press.EmptyTable;
        }
    }

    private void HandleCueBallDragged(Vector2 screenPosition)
    {
        if (!Active)
            return;
        if (_press == Press.EmptyTable && _phase == TurnPhase.PlaceCueBall)
        {
            _press = Press.None;
            ConfirmPlacement();
            return;
        }
        if (_press != Press.CueBall || !_holding)
            return;
        if (!_match.Table.ScreenToTable(_match.CameraRig.Camera, screenPosition, out Vector2 point))
            return;

        Vector2 clamped = _match.Geometry.ClampToPlayArea(point, _match.Rules.KitchenOnly);
        bool valid = _match.IsValidCueBallPosition(clamped);
        if (valid)
        {
            _lastValidCueBall = clamped;
            _match.PlaceCueBall(clamped);
        }
        HoldInAir();
        // The ring follows the finger: red where the ball can't go (it stays at the last valid spot).
        _guide.ShowPlacement(clamped, valid, _match.Rules.KitchenOnly);
    }

    // Letting go: the ball is set down at the last valid spot.
    private void HandleCueBallDragEnded()
    {
        if (_press == Press.CueBall && _holding)
            SetDown();
        _press = Press.None;
    }

    private bool IsOnCueBall(Vector2 screenPosition)
    {
        Camera camera = _match.CameraRig.Camera;
        Vector3 center = _match.Table.BallCenterWorld(_match.Balls[0].Pos);
        Vector3 screenCenter = camera.WorldToScreenPoint(center);
        if (screenCenter.z <= 0f)
            return false;
        Vector3 screenEdge = camera.WorldToScreenPoint(center + camera.transform.right * Config.BallRadius);
        float radius = Vector2.Distance(screenCenter, screenEdge);
        float minimum = Config.PlaceGrabMinPixels * Mathf.Min(Screen.width / 1080f, Screen.height / 1920f);
        return Vector2.Distance(screenPosition, screenCenter) <= Mathf.Max(radius * Config.PlaceGrabRadii, minimum);
    }

    private void PickUp()
    {
        _holding = true;
        _lastValidCueBall = _match.Balls[0].Pos;
        // In the aim view the camera sits behind the ball: it holds still, or it would chase your finger.
        _match.CameraRig.HoldPose = _phase == TurnPhase.Aim;
        HoldInAir();
    }

    private void HoldInAir()
    {
        Vector3 rest = _match.Table.BallCenterWorld(_match.Balls[0].Pos);
        _match.Table.Ball(0).HoldAt(rest + _match.Table.transform.up * Config.PlaceHoverHeight);
    }

    private void SetDown()
    {
        _holding = false;
        _match.CameraRig.HoldPose = false;
        _match.Table.Ball(0).SetDown(_match.Table.BallCenterWorld(_match.Balls[0].Pos), Config.PlaceDropDuration);
        GameEvents.RaiseCueBallPlaced();
        if (_phase == TurnPhase.PlaceCueBall)
            _guide.ShowPlacement(_lastValidCueBall, true, _match.Rules.KitchenOnly);
    }

    private void HandlePlaceCueBallConfirmed()
    {
        if (Active && _phase == TurnPhase.PlaceCueBall)
            ConfirmPlacement();
    }

    private void ConfirmPlacement()
    {
        if (_holding)
            SetDown();
        _aim = ChooseDefaultAim();
        SetPhase(TurnPhase.Aim);
    }

    private void HandleMoveCueBallRequested()
    {
        if (Active && _phase == TurnPhase.Aim && _match.Rules.BallInHand)
            SetPhase(TurnPhase.PlaceCueBall);
    }
    #endregion

    #region Pull and release
    private void HandlePullChanged(float pull01)
    {
        if (!Active)
            return;

        if (_phase == TurnPhase.Aim)
        {
            _pull = pull01;
            if (pull01 > Config.PullStartThreshold)
            {
                // The bar starts at its left end (position +1: drawn left, see ReleaseBarView), so the first pass
                // through green comes after ~1/4 period.
                _barTime = 0.5f * _cue.OscillationPeriod;
                SetPhase(TurnPhase.Pulling);
                GameEvents.RaiseReleaseMeterChanged(ShotAccuracy.BarPosition(_barTime, _cue.OscillationPeriod), _pull);
            }
        }
        else if (_phase == TurnPhase.Pulling)
        {
            _pull = pull01;
        }
    }

    private void HandlePullReleased()
    {
        if (!Active)
            return;

        if (_phase == TurnPhase.Aim)
        {
            _pull = 0f;
            return;
        }
        if (_phase != TurnPhase.Pulling)
            return;

        if (_pull <= Config.PullStartThreshold)
        {
            _pull = 0f;
            SetPhase(TurnPhase.Aim);
            GameEvents.RaiseToastRequested(new ToastMessage("Shot cancelled", ToastStyle.Info));
            return;
        }

        Fire(ShotAccuracy.BarPosition(_barTime, _cue.OscillationPeriod));
    }

    private void Fire(float barPosition)
    {
        // The bar freezes exactly where the finger let go.
        if (_phase == TurnPhase.Pulling)
            GameEvents.RaiseReleaseMeterChanged(barPosition, _pull);
        ShotAccuracy.Outcome release = ShotAccuracy.Evaluate(barPosition, _accuracy, _random);
        Vector2 direction = ShotAccuracy.Rotate(_aim, release.AngleErrorDeg);
        float speed = Mathf.Min(Config.SpeedFromPull(_pull) * release.PowerFactor, Config.MaxSpeed);
        Debug.Log($"Player: shot {speed:F2} m/s, pull {_pull:P0}, bar {barPosition:+0.00;-0.00} -> {release.Zone} {release.AngleErrorDeg:+0.00;-0.00} deg");

        // A miss shows why: the intended line (dashed) and where the ball really went (zone colour).
        if (release.Zone != ReleaseZone.Green)
        {
            Vector2 end = _prediction.Hit == PredictionHit.Ball || _prediction.Hit == PredictionHit.Cushion ? _prediction.Point : _prediction.Start + _aim * 0.6f;
            float length = Mathf.Max(0.3f, (end - _prediction.Start).magnitude);
            _guide.ShowMissReplay(_prediction.Start, end, _prediction.Start + direction * length, release.Zone);
        }

        float pull = _pull;
        _pull = 0f;
        _phase = TurnPhase.Shooting;
        _guide.HideAim();
        LastShot = new ShotRecord { Aim = _aim, Direction = direction, Zone = release.Zone, AngleError = release.AngleErrorDeg, BarPosition = barPosition, Pull = pull };
        _match.Shoot(direction, speed, new ShotFeedback
        {
            Zone = release.Zone,
            Power01 = pull,
            IsPlayer = true,
            AngleErrorDeg = release.AngleErrorDeg
        });
    }
    #endregion

    // Last player shot, for verification from `eval`.
    public struct ShotRecord
    {
        public Vector2 Aim;
        public Vector2 Direction;
        public ReleaseZone Zone;
        public float AngleError;
        public float BarPosition;
        public float Pull;
    }

    public ShotRecord LastShot { get; private set; }

#if UNITY_EDITOR
    // Editor testing: release at an exact bar position, as if the finger let go right then.
    public void DebugReleaseAt(float barPosition, float pull)
    {
        _pull = pull;
        Fire(barPosition);
    }

    public void DebugSetAim(Vector2 aim) => _aim = aim.normalized;

    // Puts the oscillating bar at `position` on its +1 -> -1 half (the one it starts on), so the next
    // OnPullReleased uses exactly it.
    public void DebugSetBarPosition(float position) => _barTime = (3f - Mathf.Clamp(position, -1f, 1f)) * 0.25f * _cue.OscillationPeriod;
#endif
}
