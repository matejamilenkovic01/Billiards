using UnityEngine;

// Deterministic 2D ball simulation on the table plane (3D is only the view). Fixed 1 ms step, no PhysX:
// it must be replayable ahead of time for the aim guide and the bot, and identical for the same input.
// Step() does not allocate.
public sealed class PoolPhysics
{
    private const float Epsilon = 1e-6f;
    private const float EscapeMargin = 0.15f;

    private readonly TableGeometry _table;
    private readonly PoolSettings _settings;
    private readonly float _radius;
    private readonly float _contactDistance;
    private float _accumulator;

    public readonly BallState[] Balls = new BallState[BallGroups.BallCount];
    // Per step: which balls move (a contact sets both), and each cushion segment's box grown by the radius, so far
    // balls skip the exact distance (the results are the same; the aim guide runs this every frame, v4 V1).
    private readonly bool[] _moving = new bool[BallGroups.BallCount];
    private readonly Vector4[] _cushionBoxes;
    public readonly ShotResult Result = new();

    // Bot look-ahead turns this off: it only needs the outcome, not every click.
    public bool RecordCollisions = true;
    // The ball views' rotation (RollAngle, RollAxis); the predictor and the bot turn it off.
    public bool TrackRotation = true;
    // A ball inside this box (|x|, |y|) cannot be in any pocket's capture circle.
    private readonly float _pocketFreeX, _pocketFreeY;

    public TableGeometry Table => _table;
    public PoolSettings Settings => _settings;
    public float Time { get; private set; }
    public bool IsMoving { get; private set; }

    public PoolPhysics(TableGeometry table, in PoolSettings settings)
    {
        _table = table;
        _settings = settings;
        _radius = settings.BallRadius;
        _contactDistance = 2f * settings.BallRadius;
        for (int i = 0; i < Balls.Length; i++)
            Balls[i].Number = i;
        _pocketFreeX = _pocketFreeY = float.MaxValue;
        foreach (PocketInfo pocket in table.Pockets)
        {
            // Inside |x| < freeX or |y| < freeY of a pocket's circle: out of it. Corners limit both, sides only x.
            float freeX = Mathf.Abs(pocket.Center.x) - pocket.CaptureRadius;
            float freeY = Mathf.Abs(pocket.Center.y) - pocket.CaptureRadius;
            if (pocket.IsCorner)
            {
                _pocketFreeX = Mathf.Min(_pocketFreeX, freeX);
                _pocketFreeY = Mathf.Min(_pocketFreeY, freeY);
            }
            else
                _pocketFreeX = Mathf.Min(_pocketFreeX, freeX);
        }
        _cushionBoxes = new Vector4[table.Cushions.Length];
        for (int s = 0; s < table.Cushions.Length; s++)
        {
            CushionSegment segment = table.Cushions[s];
            _cushionBoxes[s] = new Vector4(
                Mathf.Min(segment.A.x, segment.B.x) - _radius, Mathf.Min(segment.A.y, segment.B.y) - _radius,
                Mathf.Max(segment.A.x, segment.B.x) + _radius, Mathf.Max(segment.A.y, segment.B.y) + _radius);
        }
    }

    public void SetBalls(BallState[] source)
    {
        System.Array.Copy(source, Balls, Balls.Length);
        IsMoving = false;
    }

    public void CopyBallsFrom(PoolPhysics other) => SetBalls(other.Balls);

    public void BeginShot(in ShotParams shot)
    {
        Result.Reset();
        Time = 0f;
        _accumulator = 0f;
        ref BallState cue = ref Balls[0];
        float v0 = shot.Speed;
        float a = shot.Tip.x, b = shot.Tip.y;
        // Squirt: a tip right of centre pushes the cue ball a little left of the cue's line (and the other way).
        Vector2 direction = ShotAccuracy.Rotate(shot.Direction.normalized, _settings.SquirtDegPerR * a);
        cue.Vel = direction * v0;
        // A solid ball struck b radii above its centre spins at R·w = 5/2 b v0: b = 0.4 rolls at once, b < 0 draws.
        cue.Roll = direction * (2.5f * b * v0);
        cue.Side = 2.5f * a * v0 * _settings.SideTransfer;
        // Masse: an elevated cue tilts the side spin's axis, so its horizontal part slips sideways on the cloth and
        // the friction curves the ball towards the side of the hit until it rolls.
        float swerve = Mathf.Sin(shot.Elevation * Mathf.Deg2Rad) - Mathf.Sin(_settings.SwerveMinElevation * Mathf.Deg2Rad);
        if (swerve > 0f && cue.Side != 0f)
            cue.Roll += new Vector2(direction.y, -direction.x) * (_settings.SwerveFactor * cue.Side * swerve);
        IsMoving = v0 > 0f;
    }

    // Runs the shot to rest (or maxSeconds) and returns the recorded result.
    public ShotResult Simulate(in ShotParams shot, float maxSeconds = 60f)
    {
        BeginShot(shot);
        int maxSteps = Mathf.CeilToInt(maxSeconds / _settings.TimeStep);
        for (int i = 0; i < maxSteps && IsMoving; i++)
            Step();
        Finish();
        return Result;
    }

    // Real-time driver: runs as many fixed steps as the elapsed time covers, capped per frame.
    public int Advance(float seconds, int maxSteps)
    {
        if (!IsMoving)
            return 0;

        _accumulator += seconds;
        int steps = 0;
        while (_accumulator >= _settings.TimeStep && steps < maxSteps && IsMoving)
        {
            Step();
            _accumulator -= _settings.TimeStep;
            steps++;
        }

        // Falling behind (hitch, breakpoint): drop the backlog instead of spiralling.
        if (steps == maxSteps)
            _accumulator = Mathf.Min(_accumulator, _settings.TimeStep);
        if (!IsMoving)
            Finish();
        return steps;
    }

    public void Finish()
    {
        Result.Duration = Time;
        for (int i = 0; i < Balls.Length; i++)
            Result.FinalPositions[i] = Balls[i].Pos;
    }

    public void Step()
    {
        float dt = _settings.TimeStep;
        Integrate(dt);
        ResolveBallContacts(dt);
        ResolveCushions();
        CheckPockets();

        Time += dt;
        bool moving = false;
        for (int i = 0; i < Balls.Length; i++)
        {
            if (!Balls[i].Pocketed && Balls[i].IsMoving)
            {
                moving = true;
                break;
            }
        }
        IsMoving = moving;
    }

    // Sliding (Vel != Roll): cloth friction pulls the velocity back along the slip and turns it into roll, until
    // the ball rolls (implementation_definition_v3 V4). Rolling: the old resistance, with Roll == Vel.
    // Side spin wears off at a constant rate on its own (V1).
    private void Integrate(float dt)
    {
        float slideStep = BallMotion.SlideDecel(_settings) * dt;
        float sideStep = _settings.SideSpinDecel * dt;
        for (int i = 0; i < Balls.Length; i++)
        {
            ref BallState ball = ref Balls[i];
            if (ball.Pocketed || !ball.IsMoving)
                continue;

            if (ball.Side != 0f)
                ball.Side = Mathf.Abs(ball.Side) <= sideStep ? 0f : ball.Side - Mathf.Sign(ball.Side) * sideStep;

            Vector2 slip = ball.Vel - ball.Roll;
            float slipSpeed = slip.magnitude;
            if (slipSpeed > _settings.RollSlipEpsilon)
            {
                if (BallMotion.SlipRate * slideStep >= slipSpeed)
                {
                    // The slip ends within this step: the rolling velocity follows from the angular momentum
                    // about the contact point, (5 Vel + 2 Roll) / 7.
                    ball.Vel = ball.Roll = (5f * ball.Vel + 2f * ball.Roll) / 7f;
                }
                else
                {
                    Vector2 along = slip / slipSpeed;
                    ball.Vel -= along * slideStep;
                    ball.Roll += along * (BallMotion.RollGain * slideStep);
                }
            }
            else
            {
                float speed = ball.Vel.magnitude;
                float newSpeed = speed - (_settings.RollDecel + _settings.LinearDamping * speed) * dt;
                // At rest the side spin goes too: a ball spinning on the spot would only hold up the turn.
                if (newSpeed <= _settings.StopSpeed)
                    ball.Stop();
                else
                    ball.Vel = ball.Roll = ball.Vel / speed * newSpeed;
            }

            ball.Pos += ball.Vel * dt;
            if (!TrackRotation)
                continue;
            // The ball turns with its roll and its side spin, not its velocity: a sliding ball skids without turning.
            // Display axis in world space: up x roll for the roll (table y is world z), down for + side spin.
            float turn = Mathf.Sqrt(ball.Roll.sqrMagnitude + ball.Side * ball.Side);
            if (turn > 0f)
            {
                ball.RollAngle += turn * dt / _radius;
                ball.RollAxis = new Vector3(ball.Roll.y, -ball.Side, -ball.Roll.x) / turn;
            }
        }
    }

    private void ResolveBallContacts(float dt)
    {
        float contactSq = _contactDistance * _contactDistance;
        for (int i = 0; i < Balls.Length; i++)
            _moving[i] = !Balls[i].Pocketed && Balls[i].IsMoving;

        for (int i = 0; i < Balls.Length; i++)
        {
            if (Balls[i].Pocketed)
                continue;

            for (int j = i + 1; j < Balls.Length; j++)
            {
                if (Balls[j].Pocketed || (!_moving[i] && !_moving[j]))
                    continue;

                Vector2 delta = Balls[j].Pos - Balls[i].Pos;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq >= contactSq)
                    continue;

                Collide(i, j, delta, distanceSq, dt);
                _moving[i] = Balls[i].IsMoving;
                _moving[j] = Balls[j].IsMoving;
            }
        }
    }

    private void Collide(int i, int j, Vector2 delta, float distanceSq, float dt)
    {
        ref BallState a = ref Balls[i];
        ref BallState b = ref Balls[j];

        // Rewind both balls to the exact moment of contact, so the contact normal (and therefore the
        // object ball direction) does not depend on how deep they overlapped within the step.
        Vector2 relative = b.Vel - a.Vel;
        float qa = relative.sqrMagnitude;
        float tau = 0f;
        if (qa > Epsilon * Epsilon)
        {
            float qb = 2f * Vector2.Dot(delta, relative);
            float qc = distanceSq - _contactDistance * _contactDistance;
            float discriminant = qb * qb - 4f * qa * qc;
            if (discriminant >= 0f)
                tau = Mathf.Clamp((-qb - Mathf.Sqrt(discriminant)) / (2f * qa), -dt, 0f);
        }
        a.Pos += a.Vel * tau;
        b.Pos += b.Vel * tau;

        Vector2 normal = b.Pos - a.Pos;
        float distance = normal.magnitude;
        normal = distance > Epsilon ? normal / distance : Vector2.right;

        float approach = Vector2.Dot(a.Vel - b.Vel, normal);
        if (approach > 0f)
        {
            // Equal masses: exchange of the normal component, scaled by restitution.
            float impulse = 0.5f * (1f + _settings.BallRestitution) * approach;
            a.Vel -= impulse * normal;
            b.Vel += impulse * normal;

            // Throw (V1): the contact points slide past each other (cut angle and side spin); ball-ball friction,
            // weaker at higher sliding speed, turns part of that into a sideways push on the hit ball, at most mu
            // times the normal impulse. Each unit of it changes the sliding by 7 (2 linear + 2 x 5/2 spin). The
            // roll stays: the hit ball slides off, a rolling cue ball follows through.
            var tangent = new Vector2(-normal.y, normal.x);
            float slide = Vector2.Dot(a.Vel - b.Vel, tangent) + a.Side + b.Side;
            float slideSpeed = Mathf.Abs(slide);
            Vector2 objectPush = impulse * normal;
            if (slideSpeed > Epsilon)
            {
                float mu = _settings.BallFrictionFast + (_settings.BallFriction - _settings.BallFrictionFast) * Mathf.Exp(-_settings.BallFrictionDecay * slideSpeed);
                float friction = Mathf.Min(slideSpeed / 7f, mu * impulse) * Mathf.Sign(slide);
                a.Vel -= friction * tangent;
                b.Vel += friction * tangent;
                a.Side -= 2.5f * friction;
                b.Side -= 2.5f * friction;
                objectPush += friction * tangent;
            }

            if (i == 0)
                Result.CueBallContacts++;
            if (Result.FirstContactBall < 0 && (i == 0 || j == 0))
            {
                Result.FirstContactBall = i == 0 ? j : i;
                Result.FirstContactCuePos = i == 0 ? a.Pos : b.Pos;
                // The way the hit ball leaves, throw included (it was at rest).
                Result.FirstContactObjectDir = (i == 0 ? objectPush : -objectPush).normalized;
            }

            if (RecordCollisions)
                Result.Collisions.Add(new CollisionEvent { Time = Time, Speed = approach, Kind = CollisionKind.Ball, BallA = i, BallB = j });
        }

        // Replay the rewound time with the new velocities.
        a.Pos -= a.Vel * tau;
        b.Pos -= b.Vel * tau;

        // Rack contacts can still overlap a hair; push apart symmetrically.
        Vector2 after = b.Pos - a.Pos;
        float afterDistance = after.magnitude;
        if (afterDistance < _contactDistance)
        {
            Vector2 n = afterDistance > Epsilon ? after / afterDistance : normal;
            float push = (_contactDistance - afterDistance) * 0.5f + Epsilon;
            a.Pos -= n * push;
            b.Pos += n * push;
        }
    }

    private void ResolveCushions()
    {
        float radiusSq = _radius * _radius;
        CushionSegment[] cushions = _table.Cushions;
        for (int i = 0; i < Balls.Length; i++)
        {
            ref BallState ball = ref Balls[i];
            if (ball.Pocketed || !ball.IsMoving)
                continue;

            for (int s = 0; s < cushions.Length; s++)
            {
                Vector4 box = _cushionBoxes[s];
                if (ball.Pos.x < box.x || ball.Pos.y < box.y || ball.Pos.x > box.z || ball.Pos.y > box.w)
                    continue;
                float distanceSq = TableGeometry.DistanceSquaredToSegment(ball.Pos, cushions[s].A, cushions[s].B, out Vector2 closest);
                if (distanceSq >= radiusSq)
                    continue;

                Vector2 normal = ball.Pos - closest;
                float distance = Mathf.Sqrt(distanceSq);
                normal = distance > Epsilon ? normal / distance : -ball.Vel.normalized;

                float normalSpeed = Vector2.Dot(ball.Vel, normal);
                if (normalSpeed < 0f)
                {
                    // V1: the cushion gives back less of a hard hit; friction at the contact (the ball's slide along
                    // the cushion plus its side spin) works on the rebound: running english opens it, reverse closes
                    // it, and the spin it uses up is gone. Each unit of friction changes the sliding by 7/2.
                    float vn = -normalSpeed;
                    float restitution = Mathf.Lerp(_settings.CushionRestitutionSlow, _settings.CushionRestitutionFast, vn / _settings.CushionFastSpeed);
                    var tangent = new Vector2(-normal.y, normal.x);
                    float slide = Vector2.Dot(ball.Vel, tangent) - ball.Side;
                    float friction = Mathf.Min(Mathf.Abs(slide) / 3.5f, _settings.CushionSpinFriction * (1f + restitution) * vn) * Mathf.Sign(slide);
                    ball.Vel += (1f + restitution) * vn * normal - friction * tangent;
                    ball.Side += 2.5f * friction;
                    // The cushion stops the roll into it; the ball slides off it for a moment.
                    ball.Roll -= Vector2.Dot(ball.Roll, normal) * normal;
                    if (i == 0)
                        Result.CueCushions++;
                    if (Result.FirstContactBall >= 0)
                        Result.AnyCushionAfterContact = true;
                    if (RecordCollisions)
                        Result.Collisions.Add(new CollisionEvent { Time = Time, Speed = vn, Kind = CollisionKind.Cushion, BallA = i, BallB = -1 });
                }

                ball.Pos = closest + normal * _radius;
            }
        }
    }

    private void CheckPockets()
    {
        PocketInfo[] pockets = _table.Pockets;
        for (int i = 0; i < Balls.Length; i++)
        {
            ref BallState ball = ref Balls[i];
            if (ball.Pocketed || !ball.IsMoving)
                continue;

            int pocket = -1;
            bool nearPocket = Mathf.Abs(ball.Pos.x) >= _pocketFreeX || Mathf.Abs(ball.Pos.y) >= _pocketFreeY;
            for (int p = 0; nearPocket && p < pockets.Length; p++)
            {
                float capture = pockets[p].CaptureRadius;
                if ((ball.Pos - pockets[p].Center).sqrMagnitude < capture * capture)
                {
                    pocket = p;
                    break;
                }
            }

            // Safety net: a ball that somehow left the table goes into the nearest pocket, and is counted.
            if (pocket < 0 && (Mathf.Abs(ball.Pos.x) > _table.HalfWidth + EscapeMargin || Mathf.Abs(ball.Pos.y) > _table.HalfLength + EscapeMargin))
            {
                pocket = NearestPocket(ball.Pos);
                Result.Escaped++;
            }

            if (pocket < 0)
                continue;

            Vector2 entry = ball.Vel;
            ball.Pocketed = true;
            ball.PocketIndex = pocket;
            ball.Stop();
            Result.Pocketed.Add(new PocketEvent { Number = i, PocketIndex = pocket, Time = Time, Velocity = entry });
            if (i == 0)
                Result.CueBallPocketed = true;
        }
    }

    public int NearestPocket(Vector2 position)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int p = 0; p < _table.Pockets.Length; p++)
        {
            float distance = (position - _table.Pockets[p].Center).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = p;
            }
        }
        return best;
    }
}
