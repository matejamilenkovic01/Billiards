using UnityEngine;

public enum PredictionHit
{
    None = 0,
    Ball = 1,
    Cushion = 2,
    Pocket = 3
}

public struct Prediction
{
    public PredictionHit Hit;
    public Vector2 Start;
    public Vector2 Point;          // cue ball centre at the first event (ghost ball for a ball hit; where it stops for None)
    public int HitBall;
    public Vector2 ObjectDir;      // direction the hit ball leaves in (with throw when simulated)
    public Vector2 CueDeflectDir;  // analytic only: stun-shot tangent line for the cue ball (zero on a full hit)
    public bool IsLegal;

    // Simulated (v4 V1, a shot with a speed): the cue ball's real path up to the first event (Line, curved by squirt
    // and masse) and after it (CuePath: after the contact, or after the cushion up to what it meets next), up to the
    // arrow length. Both are the predictor's preallocated buffers, valid until the next Predict.
    public bool HasSpeed;
    public bool Reaches;
    public Vector2[] Line;
    public int LineCount;
    public Vector2[] CuePath;
    public int CuePathCount;
    // The path after the first event ended before the arrow length (the cue ball stopped, dropped or met something).
    public bool CuePathCut;

    // Analytic: one cushion bounce direction. Simulated: what the cue ball meets after the first event, within the
    // arrow length (after a cushion: a ball, a cushion or a pocket; after a ball: a pocket = scratch).
    public bool HasBounce;
    public Vector2 BounceDir;
    public PredictionHit SecondHit;
    public Vector2 SecondPoint;
    public int SecondBall;
    public Vector2 SecondObjectDir;
    public bool SecondIsLegal;

    // Cut angle in degrees between the cue ball's arrival and the object ball direction (0 = full ball).
    public float CutAngle => Hit == PredictionHit.Ball ? Vector2.Angle(Point - (LineCount >= 2 ? Line[LineCount - 2] : Start), ObjectDir) : 0f;
}

// Two predictions. Predict(balls, direction, legal): analytic first contact (swept circle along the aim, centre hit),
// cheap, for the bot's candidates and the default aim. Predict(balls, shot, legal, arrow): the aim guide (v4 V1) runs
// the same PoolPhysics on its own preallocated copy of the table, from the shot until the cue ball's path is known
// past the first event, so spin, squirt, throw, masse and the cushions are drawn exactly as they will happen.
public sealed class ShotPredictor
{
    private const float MaxDistance = 10f;
    public const int MaxPathPoints = 32;
    private const float MaxSeconds = 15f;
    // A new path point whenever the cue ball has drifted this far (m) off the line it was on at the last point: the
    // drawn chords stay well within the 2 mm the checks allow, and a straight path is just its two ends.
    private const float Drift = 0.0004f;
    // A straight roll with nothing else moving is jumped in closed form up to this far before the next obstacle.
    private const float JumpMargin = 0.02f;

    private readonly TableGeometry _table;
    private readonly PoolSettings _settings;
    private readonly float _radius;
    private readonly PoolPhysics _sim;
    private readonly Vector2[] _line = new Vector2[MaxPathPoints];
    private readonly Vector2[] _cuePath = new Vector2[MaxPathPoints];

    public ShotPredictor(TableGeometry table, in PoolSettings settings)
    {
        _table = table;
        _settings = settings;
        _radius = table.BallRadius;
        _sim = new PoolPhysics(table, settings) { RecordCollisions = false, TrackRotation = false };
    }

    public Prediction Predict(BallState[] balls, Vector2 direction, bool[] legalTargets)
    {
        direction = direction.normalized;
        Vector2 start = balls[0].Pos;
        var prediction = new Prediction { Start = start, HitBall = -1, SecondBall = -1 };

        Cast(balls, start, direction, out PredictionHit hit, out float distance, out int ball, out Vector2 cushionNormal);
        prediction.Hit = hit;
        prediction.Point = start + direction * distance;
        if (hit == PredictionHit.Ball)
        {
            prediction.HitBall = ball;
            prediction.ObjectDir = (balls[ball].Pos - prediction.Point).normalized;
            Vector2 tangent = direction - Vector2.Dot(direction, prediction.ObjectDir) * prediction.ObjectDir;
            prediction.CueDeflectDir = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector2.zero;
            prediction.IsLegal = legalTargets == null || legalTargets[ball];
            return prediction;
        }

        prediction.IsLegal = true;
        if (hit == PredictionHit.Cushion)
        {
            // Direction only (no spin): the simulated prediction has the real rebound.
            float normalSpeed = Vector2.Dot(direction, cushionNormal);
            prediction.HasBounce = true;
            prediction.BounceDir = (direction - 2f * normalSpeed * cushionNormal).normalized;
        }
        return prediction;
    }

    // The guide's prediction: simulated, spin and all. arrowLength = how far the cue ball's path is followed after
    // the first event. Allocation-free.
    public Prediction Predict(BallState[] balls, in ShotParams shot, bool[] legalTargets, float arrowLength)
    {
        var prediction = new Prediction
        {
            Start = balls[0].Pos, HitBall = -1, SecondBall = -1, HasSpeed = true, Line = _line, CuePath = _cuePath, IsLegal = true
        };
        _sim.SetBalls(balls);
        _sim.BeginShot(shot);
        ShotResult result = _sim.Result;
        BallState[] simBalls = _sim.Balls;

        int lineCount = 0;
        _line[lineCount++] = prediction.Start;
        Vector2 heading = simBalls[0].Vel;
        Vector2 segmentStart = prediction.Start;
        int cushions = 0;
        int pathCount = 0;
        float arc = 0f;
        Vector2 last = prediction.Start;
        bool after = false;
        int maxSteps = Mathf.CeilToInt(MaxSeconds / _settings.TimeStep);

        Vector2 previous = prediction.Start;
        for (int step = 0; step < maxSteps && _sim.IsMoving; step++)
        {
            previous = simBalls[0].Pos;
            _sim.Step();
            ref BallState cue = ref simBalls[0];

            if (!after)
            {
                if (result.FirstContactBall >= 0)
                {
                    prediction.Hit = PredictionHit.Ball;
                    prediction.Point = result.FirstContactCuePos;
                    prediction.HitBall = result.FirstContactBall;
                    prediction.ObjectDir = result.FirstContactObjectDir;
                    prediction.IsLegal = legalTargets == null || legalTargets[result.FirstContactBall];
                }
                else if (result.CueCushions > 0)
                {
                    prediction.Hit = PredictionHit.Cushion;
                    prediction.Point = cue.Pos;
                    prediction.HasBounce = true;
                }
                else if (cue.Pocketed)
                {
                    prediction.Hit = PredictionHit.Pocket;
                    prediction.Point = PocketEntry(previous, cue.Pos, cue.PocketIndex);
                    break;
                }
                else if (!cue.IsMoving)
                {
                    prediction.Hit = PredictionHit.None;
                    prediction.Point = cue.Pos;
                    break;
                }
                else
                {
                    if (Drifted(cue.Pos, previous, ref segmentStart, ref heading) && lineCount < MaxPathPoints - 1)
                        _line[lineCount++] = previous;
                    if (JumpRoll(simBalls, ref cue, ref step))
                    {
                        // It stops on the straight before reaching anything.
                        prediction.Hit = PredictionHit.None;
                        prediction.Point = cue.Pos;
                        break;
                    }
                    continue;
                }

                // The first event: the line ends there, the path after it starts there.
                prediction.Reaches = true;
                _line[lineCount++] = prediction.Point;
                after = true;
                _cuePath[pathCount++] = prediction.Point;
                last = prediction.Point;
                segmentStart = prediction.Point;
                heading = cue.Vel;
                cushions = result.CueCushions;
                if (arrowLength <= 0f)
                    break;
            }

            // After the first event: follow the cue ball for the arrow length.
            bool stop = false;
            if (prediction.Hit == PredictionHit.Cushion && result.FirstContactBall >= 0)
            {
                prediction.SecondHit = PredictionHit.Ball;
                prediction.SecondPoint = result.FirstContactCuePos;
                prediction.SecondBall = result.FirstContactBall;
                prediction.SecondObjectDir = result.FirstContactObjectDir;
                prediction.SecondIsLegal = legalTargets == null || legalTargets[result.FirstContactBall];
                stop = true;
            }
            else if (prediction.Hit == PredictionHit.Ball && result.CueBallContacts > 1)
                stop = true;   // a second ball: the path ends at it
            else if (prediction.Hit == PredictionHit.Cushion && result.CueCushions > 1)
            {
                prediction.SecondHit = PredictionHit.Cushion;
                prediction.SecondPoint = cue.Pos;
                stop = true;
            }
            else if (cue.Pocketed)
            {
                prediction.SecondHit = PredictionHit.Pocket;
                prediction.SecondPoint = PocketEntry(previous, cue.Pos, cue.PocketIndex);
                stop = true;
            }

            Vector2 at = prediction.SecondHit == PredictionHit.Ball || prediction.SecondHit == PredictionHit.Pocket ? prediction.SecondPoint : cue.Pos;
            float segment = (at - last).magnitude;
            if (arc + segment >= arrowLength)
            {
                // The arrow ends inside this step.
                Vector2 end = segment > 1e-9f ? last + (at - last) * ((arrowLength - arc) / segment) : at;
                _cuePath[pathCount++] = end;
                // Whatever it meets in this step lies beyond the arrow: not shown.
                if (stop && arc + segment > arrowLength + 1e-6f)
                {
                    prediction.SecondHit = PredictionHit.None;
                    prediction.SecondBall = -1;
                }
                break;
            }
            arc += segment;
            last = at;
            if (stop || !cue.IsMoving)
            {
                _cuePath[pathCount++] = at;
                prediction.CuePathCut = true;
                break;
            }
            if (result.CueCushions > cushions)
            {
                // A bounce within the path: the corner is where the cushion put the ball.
                cushions = result.CueCushions;
                segmentStart = at;
                heading = cue.Vel;
                if (pathCount < MaxPathPoints - 1)
                    _cuePath[pathCount++] = at;
            }
            else if (Drifted(at, previous, ref segmentStart, ref heading) && pathCount < MaxPathPoints - 1)
                _cuePath[pathCount++] = previous;
        }

        prediction.LineCount = lineCount;
        prediction.CuePathCount = pathCount;
        if (!after && prediction.Hit == PredictionHit.None)
        {
            // Ran out of time without an event (cannot happen on a real table): end where it is.
            prediction.Point = simBalls[0].Pos;
            if (lineCount == 1 || _line[lineCount - 1] != prediction.Point)
                _line[prediction.LineCount++] = prediction.Point;
        }
        else if (!after && lineCount < MaxPathPoints)
        {
            _line[prediction.LineCount++] = prediction.Point;
        }
        return prediction;
    }

    // Where the step from `from` to `to` crossed into the pocket's capture circle (the simulation drops the ball
    // inside it, up to one step deep).
    private Vector2 PocketEntry(Vector2 from, Vector2 to, int pocket)
    {
        PocketInfo info = _table.Pockets[pocket];
        Vector2 d = to - from;
        float length = d.magnitude;
        if (length < 1e-9f)
            return to;
        d /= length;
        return RayCircle(from, d, info.Center, info.CaptureRadius, out float t) && t <= length ? from + d * t : to;
    }

    // Before the first event only the cue ball moves. Once it rolls (no slip) it goes straight, so the steps up to
    // JumpMargin before the first obstacle are run as the simulation's own rolling step on plain numbers (whole
    // steps, so it stays in step with the full simulation), without the table checks. True if it stops on the way.
    private bool JumpRoll(BallState[] balls, ref BallState cue, ref int step)
    {
        if (cue.Vel != cue.Roll)
            return false;
        float speed = cue.Vel.magnitude;
        if (speed <= _settings.StopSpeed)
            return false;
        Vector2 direction = cue.Vel / speed;
        Cast(balls, cue.Pos, direction, out _, out float obstacle, out _, out _);
        float jump = obstacle - JumpMargin;
        if (jump <= 0.05f)
            return false;

        float dt = _settings.TimeStep;
        float a = _settings.RollDecel, k = _settings.LinearDamping;
        float sideStep = _settings.SideSpinDecel * dt;
        Vector2 velocity = cue.Vel;
        Vector2 position = cue.Pos;
        float side = cue.Side;
        float travelled = 0f;
        bool stopped = false;
        while (travelled < jump)
        {
            if (side != 0f)
                side = Mathf.Abs(side) <= sideStep ? 0f : side - Mathf.Sign(side) * sideStep;
            float newSpeed = speed - (a + k * speed) * dt;
            if (newSpeed <= _settings.StopSpeed)
            {
                stopped = true;
                break;
            }
            velocity = velocity / speed * newSpeed;
            speed = newSpeed;
            position += velocity * dt;
            travelled += speed * dt;
            step++;
        }
        cue.Pos = position;
        if (stopped)
        {
            cue.Stop();
            return true;
        }
        cue.Vel = cue.Roll = velocity;
        cue.Side = side;
        return false;
    }

    // True when `position` has left the line from segmentStart along heading by more than Drift: `previous` (the
    // step before) becomes a point and starts the next line.
    private static bool Drifted(Vector2 position, Vector2 previous, ref Vector2 segmentStart, ref Vector2 heading)
    {
        float length = heading.magnitude;
        if (length < 1e-9f)
        {
            heading = position - previous;
            return false;
        }
        Vector2 offset = position - segmentStart;
        if (Mathf.Abs(offset.x * heading.y - offset.y * heading.x) <= Drift * length)
            return false;
        segmentStart = previous;
        heading = position - previous;
        return true;
    }

    // First event for a ball centre travelling from origin (a ball sitting at origin is ignored): used to see
    // where a hit ball would go, e.g. whether a line is a straight-in pot.
    public PredictionHit CastFrom(BallState[] balls, Vector2 origin, Vector2 direction, out float distance, out int hitBall)
    {
        Cast(balls, origin, direction.normalized, out PredictionHit hit, out distance, out hitBall, out _);
        return hit;
    }

    private void Cast(BallState[] balls, Vector2 origin, Vector2 direction, out PredictionHit hit, out float distance,
        out int hitBall, out Vector2 cushionNormal)
    {
        hit = PredictionHit.None;
        distance = MaxDistance;
        hitBall = -1;
        cushionNormal = Vector2.zero;

        float contact = 2f * _radius;
        for (int i = 1; i < balls.Length; i++)
        {
            if (balls[i].Pocketed)
                continue;
            if (RayCircle(origin, direction, balls[i].Pos, contact, out float t) && t < distance)
            {
                distance = t;
                hit = PredictionHit.Ball;
                hitBall = i;
            }
        }

        foreach (CushionSegment segment in _table.Cushions)
        {
            if (RayCapsule(origin, direction, segment.A, segment.B, _radius, out float t, out Vector2 normal) && t < distance)
            {
                distance = t;
                hit = PredictionHit.Cushion;
                hitBall = -1;
                cushionNormal = normal;
            }
        }

        foreach (PocketInfo pocket in _table.Pockets)
        {
            if (RayCircle(origin, direction, pocket.Center, pocket.CaptureRadius, out float t) && t < distance)
            {
                distance = t;
                hit = PredictionHit.Pocket;
                hitBall = -1;
            }
        }
    }

    // First t >= 0 where |origin + direction*t - center| = radius, entering the circle.
    private static bool RayCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius, out float t)
    {
        Vector2 f = origin - center;
        float b = Vector2.Dot(f, direction);
        float c = f.sqrMagnitude - radius * radius;
        t = 0f;
        if (c < 0f)
            return false;   // already inside (overlapping): ignore
        float discriminant = b * b - c;
        if (discriminant < 0f || b > 0f)
            return false;
        t = -b - Mathf.Sqrt(discriminant);
        return t >= 0f;
    }

    // Swept circle against a segment = ray against the segment's capsule.
    private static bool RayCapsule(Vector2 origin, Vector2 direction, Vector2 a, Vector2 b, float radius, out float t, out Vector2 normal)
    {
        t = float.MaxValue;
        normal = Vector2.zero;
        bool found = false;

        Vector2 ab = b - a;
        float length = ab.magnitude;
        if (length > 1e-6f)
        {
            Vector2 along = ab / length;
            var side = new Vector2(-along.y, along.x);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 n = side * s;
                float facing = Vector2.Dot(direction, n);
                if (facing >= 0f)
                    continue;   // moving away from this face
                float height = Vector2.Dot(origin - a, n) - radius;
                if (height < 0f)
                    continue;
                float hitT = height / -facing;
                Vector2 point = origin + direction * hitT;
                float projection = Vector2.Dot(point - a, along);
                if (projection >= 0f && projection <= length && hitT < t)
                {
                    t = hitT;
                    normal = n;
                    found = true;
                }
            }
        }

        found |= RayKnuckle(origin, direction, a, radius, ref t, ref normal);
        found |= RayKnuckle(origin, direction, b, radius, ref t, ref normal);
        return found;
    }

    private static bool RayKnuckle(Vector2 origin, Vector2 direction, Vector2 end, float radius, ref float t, ref Vector2 normal)
    {
        if (!RayCircle(origin, direction, end, radius, out float hitT) || hitT >= t)
            return false;

        t = hitT;
        normal = (origin + direction * hitT - end).normalized;
        return true;
    }
}
