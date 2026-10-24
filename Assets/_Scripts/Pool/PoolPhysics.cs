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
    public readonly ShotResult Result = new();

    // Bot look-ahead turns this off: it only needs the outcome, not every click.
    public bool RecordCollisions = true;

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
        // The cue strikes the centre of the cue ball: it leaves sliding, with no roll.
        Balls[0].Vel = shot.Direction.normalized * shot.Speed;
        Balls[0].Roll = Vector2.zero;
        IsMoving = shot.Speed > 0f;
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
    private void Integrate(float dt)
    {
        float slideStep = BallMotion.SlideDecel(_settings) * dt;
        for (int i = 0; i < Balls.Length; i++)
        {
            ref BallState ball = ref Balls[i];
            if (ball.Pocketed || !ball.IsMoving)
                continue;

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
                if (newSpeed <= _settings.StopSpeed)
                {
                    ball.Vel = ball.Roll = Vector2.zero;
                    continue;
                }
                ball.Vel = ball.Roll = ball.Vel / speed * newSpeed;
            }

            ball.Pos += ball.Vel * dt;
            // The ball turns with its roll, not its velocity: a sliding ball skids without turning.
            float rollSpeed = ball.Roll.magnitude;
            if (rollSpeed > 0f)
            {
                ball.RollAngle += rollSpeed * dt / _radius;
                // Axis = up x roll (table y is world z).
                ball.RollAxis = new Vector3(ball.Roll.y, 0f, -ball.Roll.x) / rollSpeed;
            }
        }
    }

    private void ResolveBallContacts(float dt)
    {
        float contactSq = _contactDistance * _contactDistance;
        for (int i = 0; i < Balls.Length; i++)
        {
            if (Balls[i].Pocketed)
                continue;

            for (int j = i + 1; j < Balls.Length; j++)
            {
                if (Balls[j].Pocketed || (!Balls[i].IsMoving && !Balls[j].IsMoving))
                    continue;

                Vector2 delta = Balls[j].Pos - Balls[i].Pos;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq >= contactSq)
                    continue;

                Collide(i, j, delta, distanceSq, dt);
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
            // Equal masses: exchange of the normal component, scaled by restitution. No friction between the
            // balls, so their roll stays: the hit ball slides off, a rolling cue ball follows through.
            float impulse = 0.5f * (1f + _settings.BallRestitution) * approach;
            a.Vel -= impulse * normal;
            b.Vel += impulse * normal;

            if (Result.FirstContactBall < 0 && (i == 0 || j == 0))
            {
                Result.FirstContactBall = i == 0 ? j : i;
                Result.FirstContactCuePos = i == 0 ? a.Pos : b.Pos;
                Result.FirstContactObjectDir = i == 0 ? normal : -normal;
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
                float distanceSq = TableGeometry.DistanceSquaredToSegment(ball.Pos, cushions[s].A, cushions[s].B, out Vector2 closest);
                if (distanceSq >= radiusSq)
                    continue;

                Vector2 normal = ball.Pos - closest;
                float distance = Mathf.Sqrt(distanceSq);
                normal = distance > Epsilon ? normal / distance : -ball.Vel.normalized;

                float normalSpeed = Vector2.Dot(ball.Vel, normal);
                if (normalSpeed < 0f)
                {
                    Vector2 tangent = ball.Vel - normalSpeed * normal;
                    ball.Vel = -_settings.CushionRestitution * normalSpeed * normal + (1f - _settings.CushionFriction) * tangent;
                    // The cushion stops the roll into it; the ball slides off it for a moment.
                    ball.Roll -= Vector2.Dot(ball.Roll, normal) * normal;
                    if (Result.FirstContactBall >= 0)
                        Result.AnyCushionAfterContact = true;
                    if (RecordCollisions)
                        Result.Collisions.Add(new CollisionEvent { Time = Time, Speed = -normalSpeed, Kind = CollisionKind.Cushion, BallA = i, BallB = -1 });
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
            for (int p = 0; p < pockets.Length; p++)
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
            ball.Vel = ball.Roll = Vector2.zero;
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
