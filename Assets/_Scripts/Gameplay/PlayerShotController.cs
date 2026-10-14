using UnityEngine;
using UnityEngine.InputSystem;

// The player's turn: PlaceCueBall (ball in hand) -> Aim <-> Look -> Pulling -> shot (implementation_definition 4.7).
// Listens to the shot control events from the UI and drives the cue, the guide and the camera.
public class PlayerShotController : MonoBehaviour
{
    [SerializeField] private AimGuideView _guide;

    private MatchController _match;
    private BotBrain _advisor;
    private System.Random _random;
    private TurnPhase _phase = TurnPhase.None;
    private Vector2 _aim = Vector2.up;
    private float _pull;
    private float _barTime;
    private bool _snapped;
    private bool _illegalHintShown;
    private Vector2 _lastValidCueBall;
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
        GameEvents.OnCueBallDragged += HandleCueBallDragged;
        GameEvents.OnCueBallDragEnded += HandleCueBallDragEnded;
        GameEvents.OnTableTapped += HandleTableTapped;
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
        GameEvents.OnCueBallDragged -= HandleCueBallDragged;
        GameEvents.OnCueBallDragEnded -= HandleCueBallDragEnded;
        GameEvents.OnTableTapped -= HandleTableTapped;
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
        if (!_match.IsHumanTurn)
        {
            _phase = TurnPhase.None;
            return;
        }

        if (_advisor == null)
            _advisor = new BotBrain(_match.Geometry, Config.ToPoolSettings(), _match.BotProfile.ToBotSettings(Config));

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
        _aim = _advisor.SuggestAim(_match.Rules, _match.Balls);
        SetPhase(info.BallInHand ? TurnPhase.PlaceCueBall : TurnPhase.Aim);
    }

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
        }

        if (_phase == TurnPhase.Aim || _phase == TurnPhase.Pulling)
        {
            _prediction = _match.Predictor.Predict(_match.Balls, _aim, _match.LegalTargets);
            _guide.ShowAim(_prediction, _cue.ObjectGuideDiameters * 2f * Config.BallRadius);
            if (_prediction.Hit == PredictionHit.Ball && !_prediction.IsLegal && !_illegalHintShown)
            {
                _illegalHintShown = true;
                string hint = _match.Rules.IsOnEight(Side.Player) ? "Hit the 8-ball now"
                    : _prediction.HitBall == BallGroups.EightBall ? "Hit the 8-ball only after clearing your group"
                    : "Hit your own group first";
                GameEvents.RaiseToastRequested(new ToastMessage(hint, ToastStyle.Warning));
            }
        }
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
        if (_phase != TurnPhase.Aim)
            return;   // locked while pulling, so a stray finger can't swing the aim

        RotateAim(-deltaPixels.x * Config.AimDegreesPerPixel, true);
        _match.CameraRig.Tilt -= deltaPixels.y * Config.TiltPerPixel;
    }

    private void HandleFineAimDrag(float deltaPixels)
    {
        if (Active && _phase == TurnPhase.Aim)
            RotateAim(deltaPixels * Config.FineAimDegreesPerPixel, false);
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
    private void HandleCueBallDragged(Vector2 screenPosition)
    {
        if (!Active || _phase != TurnPhase.PlaceCueBall)
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
        _guide.ShowPlacement(clamped, valid, _match.Rules.KitchenOnly);
    }

    // Letting go on an invalid spot: the ball simply stays at the last valid one.
    private void HandleCueBallDragEnded()
    {
        if (Active && _phase == TurnPhase.PlaceCueBall)
            _guide.ShowPlacement(_lastValidCueBall, true, _match.Rules.KitchenOnly);
    }

    private void HandlePlaceCueBallConfirmed()
    {
        if (!Active || _phase != TurnPhase.PlaceCueBall)
            return;
        _aim = _advisor.SuggestAim(_match.Rules, _match.Balls);
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
                // The bar starts at its bottom end, so the first pass through green comes after ~1/4 period.
                _barTime = 0f;
                SetPhase(TurnPhase.Pulling);
                GameEvents.RaiseReleaseMeterChanged(ShotAccuracy.BarPosition(0f, _cue.OscillationPeriod), _pull);
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
        ShotAccuracy.Outcome release = ShotAccuracy.Evaluate(barPosition, _accuracy, _random);
        Vector2 direction = ShotAccuracy.Rotate(_aim, release.AngleErrorDeg);
        float speed = Mathf.Min(Config.SpeedFromPull(_pull) * release.PowerFactor, Config.MaxSpeed);
        Debug.Log($"Player: shot {speed:F2} m/s, pull {_pull:P0}, bar {barPosition:+0.00;-0.00} -> {release.Zone} {release.AngleErrorDeg:+0.00;-0.00} deg");

        if (release.Zone != ReleaseZone.Green)
        {
            Vector2 end = _prediction.Hit == PredictionHit.Ball ? _prediction.Point : _prediction.Start + _aim * 0.6f;
            _guide.ShowIntended(_prediction.Start, end);
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

    // Puts the oscillating bar at `position` on its rising half, so the next OnPullReleased uses exactly it.
    public void DebugSetBarPosition(float position) => _barTime = (Mathf.Clamp(position, -1f, 1f) + 1f) * 0.25f * _cue.OscillationPeriod;
#endif
}
