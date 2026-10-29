using System.Collections.Generic;
using UnityEngine;

public struct BotSettings
{
    public float WeightCut;
    public float WeightCueDistance;
    public float WeightPocketDistance;
    public float WeightPocketAngle;
    public float MaxCutAngle;
    public int CandidatesToSimulate;
    public int BallInHandSamples;
    public float ArrivalSpeed;
    public float MinShotSpeed;
    public PowerCurve Power;
    // v4: spin on the best pots (Gold and up), judged by the next shot it leaves.
    public bool UsesSpin;
    public Vector2[] SpinVariants;
    public int SpinCandidates;
    public float CueElevation;
    public float BreakPowerMin;
    public float BreakPowerMax;
    public float BreakSpreadDegrees;

    // The bot lets go N(0, sigma) off the centre of green, with its cue's window at the shot's power: the same
    // release bar as the player, so a hard shot is harder for it too.
    public float TimingSigmaMs;
    public ShotAccuracy.Settings Accuracy;
    public ShotAccuracy.PowerScaling Scaling;
}

public struct BotDecision
{
    public Vector2 Direction;
    public float Speed;
    public bool PlaceCueBall;
    public Vector2 CueBallPosition;
    public bool IsBreak;
    public bool IsSafety;
    public int TargetBall;
    // The pocket aimed at; under a calling rule (v4 V4) it is also the bot's call (-1 on the break).
    public int TargetPocket;
    // Where the tip meets the cue ball (ball radii): centre unless the bot chose follow, draw or stun (v4).
    public Vector2 Tip;
    // A few other directions the bot "considers" while it thinks (for the cue animation).
    public Vector2[] ConsideredDirections;
}

// Chooses the bot's shot: ghost-ball candidates for every legal ball x pocket, scored, the best few checked
// by simulating them without error. Pure C#: safe to run on a worker thread with its own instance.
public sealed class BotBrain
{
    private struct Candidate
    {
        public Vector2 Direction;
        public float Speed;
        public float Score;
        public int Ball;
        public int Pocket;
    }

    private static readonly float[] SafetySpeeds = { 0.8f, 1.2f, 1.7f, 2.3f, 3.0f };

    private readonly TableGeometry _table;
    private readonly PoolSettings _pool;
    private readonly BotSettings _bot;
    private readonly PoolPhysics _sim;
    private readonly ShotPredictor _predictor;
    private readonly BallState[] _work = new BallState[BallGroups.BallCount];
    private readonly List<Candidate> _candidates = new(96);
    private readonly bool[] _legal = new bool[BallGroups.BallCount];
    private readonly BallState[] _saved = new BallState[BallGroups.BallCount];
    private readonly bool[] _savedLegal = new bool[BallGroups.BallCount];
    private readonly List<Candidate> _pots = new(8);
    private readonly float _radius;
    // The rules of the decision being made (v4 V4): which pockets the 8 may go in, whether shots are called.
    private RulesState _state;
    private Side _side;
    private bool _anyEightPocket;

    public BotBrain(TableGeometry table, in PoolSettings pool, in BotSettings bot)
    {
        _table = table;
        _pool = pool;
        _bot = bot;
        _radius = pool.BallRadius;
        _sim = new PoolPhysics(table, pool) { RecordCollisions = false, TrackRotation = false };
        _predictor = new ShotPredictor(table, pool);
    }

    public BotDecision ChooseShot(RulesState state, BallState[] balls, System.Random random)
    {
        System.Array.Copy(balls, _work, _work.Length);
        Side side = state.Turn;
        state.FillLegalTargets(side, _legal);
        _state = state;
        _side = side;
        _anyEightPocket = false;
        bool calls = state.NeedsCall(side);

        if (state.IsBreakShot)
            return Break(state, random);

        var decision = new BotDecision { TargetBall = -1, TargetPocket = -1 };
        if (state.BallInHand)
        {
            decision.PlaceCueBall = true;
            decision.CueBallPosition = ChooseCueBallPosition(state.KitchenOnly, random);
            _work[0].Pos = decision.CueBallPosition;
            _work[0].Pocketed = false;
            _work[0].Stop();
        }

        GenerateCandidates(_work[0].Pos);
        _candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        decision.ConsideredDirections = Considered();

        // The best candidates that pot cleanly with a centre hit; with spin (Gold and up) a few of them, else the first.
        int tries = Mathf.Min(_bot.CandidatesToSimulate, _candidates.Count);
        int wanted = _bot.UsesSpin && _bot.SpinVariants != null && _bot.SpinVariants.Length > 1 ? Mathf.Max(1, _bot.SpinCandidates) : 1;
        _pots.Clear();
        for (int i = 0; i < tries && _pots.Count < wanted; i++)
        {
            Candidate candidate = _candidates[i];
            RulesOutcome outcome = SimulateOutcome(state, candidate.Direction, candidate.Speed, calls ? candidate.Pocket : -1, Vector2.zero, out ShotResult result);
            if (outcome.Foul == FoulType.None && !Loses(outcome, side) && PocketedIn(result, candidate.Ball, calls ? candidate.Pocket : -1))
                _pots.Add(candidate);
        }

        if (_pots.Count > 0)
        {
            Candidate best = _pots[0];
            Vector2 bestTip = Vector2.zero;
            if (wanted > 1)
                ChooseSpin(state, side, calls, ref best, ref bestTip);
            decision.Direction = best.Direction;
            decision.Speed = best.Speed;
            decision.TargetBall = best.Ball;
            decision.TargetPocket = best.Pocket;
            decision.Tip = bestTip;
            return decision;
        }

        Safety(state, random, ref decision);
        // A safety still calls a pocket: the one nearest its target ball.
        if (calls && decision.TargetPocket < 0)
            decision.TargetPocket = NearestPocket(decision.TargetBall > 0 ? _work[decision.TargetBall].Pos : _work[0].Pos);
        return decision;
    }

    // v4: every spin variant on every clean pot; the one that pots cleanly and leaves the best next shot (the best
    // candidate score from where the cue ball stops; a win beats everything) wins. Centre first: a tie keeps it.
    private void ChooseSpin(RulesState state, Side side, bool calls, ref Candidate best, ref Vector2 bestTip)
    {
        System.Array.Copy(_work, _saved, _work.Length);
        System.Array.Copy(_legal, _savedLegal, _legal.Length);
        float bestScore = float.MinValue;
        foreach (Candidate candidate in _pots)
        {
            foreach (Vector2 tip in _bot.SpinVariants)
            {
                RulesOutcome outcome = SimulateOutcome(state, candidate.Direction, candidate.Speed, calls ? candidate.Pocket : -1, tip, out ShotResult result);
                if (outcome.Foul != FoulType.None || Loses(outcome, side) || !PocketedIn(result, candidate.Ball, calls ? candidate.Pocket : -1))
                    continue;
                float score = NextShotScore(outcome, side);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    bestTip = tip;
                }
            }
        }
        System.Array.Copy(_saved, _work, _work.Length);
        System.Array.Copy(_savedLegal, _legal, _legal.Length);
    }

    // The best candidate score for the shooter's next shot from the table the last simulation left.
    private float NextShotScore(RulesOutcome outcome, Side side)
    {
        if (outcome.MatchOver)
            return outcome.Next.Winner == side ? 1000f : -1000f;
        System.Array.Copy(_sim.Balls, _work, _work.Length);
        outcome.Next.FillLegalTargets(side, _legal);
        GenerateCandidates(_work[0].Pos);
        float score = -5f;
        foreach (Candidate next in _candidates)
            score = Mathf.Max(score, next.Score);
        System.Array.Copy(_saved, _work, _work.Length);
        System.Array.Copy(_savedLegal, _legal, _legal.Length);
        return score;
    }

    // For the opponent of `side`: the pocket to block (Gold) or to pick (Platinum, GC) for `side`'s 8 (v4 V4). Every
    // pocket is scored by `side`'s best shot on the 8 into it from the table as it is (the same score as ChooseShot):
    // a block takes the best one away; a pick gives the worst one that still has a direct shot (else the worst).
    public int ChooseEightPocketForOpponent(RulesState state, BallState[] balls, Side side, System.Random random)
    {
        System.Array.Copy(balls, _work, _work.Length);
        for (int i = 0; i < _legal.Length; i++)
            _legal[i] = i == BallGroups.EightBall && !_work[i].Pocketed;
        _state = state;
        _side = side;
        _anyEightPocket = true;
        GenerateCandidates(_work[0].Pos);
        _anyEightPocket = false;

        int pockets = _table.Pockets.Length;
        var best = new float[pockets];
        for (int p = 0; p < pockets; p++)
            best[p] = float.MinValue;
        foreach (Candidate candidate in _candidates)
            best[candidate.Pocket] = Mathf.Max(best[candidate.Pocket], candidate.Score);

        bool block = state.Rules == RuleSetId.BlockedPocket;
        Vector2 eight = _work[BallGroups.EightBall].Pos;
        int choice = -1;
        float chosen = 0f;
        int offset = random.Next(pockets);   // ties go to a random pocket
        for (int k = 0; k < pockets; k++)
        {
            int p = (k + offset) % pockets;
            // Pockets with no direct shot: a block prefers the nearest, a pick the farthest.
            float distance = (eight - _table.Pockets[p].Center).magnitude;
            bool direct = best[p] > float.MinValue;
            float score = direct ? best[p] : block ? -1000f - distance : -1000f + distance;
            bool better = choice < 0 || (block ? score > chosen : Pick(best, p, score, choice, chosen));
            if (better)
            {
                choice = p;
                chosen = score;
            }
        }
        return choice;
    }

    // A pick: a pocket with a direct shot always beats one without; among those, the lower score wins; without any
    // direct shot, the farthest pocket.
    private static bool Pick(float[] best, int p, float score, int choice, float chosen)
    {
        bool direct = best[p] > float.MinValue, chosenDirect = best[choice] > float.MinValue;
        if (direct != chosenDirect)
            return direct;
        return direct ? score < chosen : score > chosen;
    }

    private int NearestPocket(Vector2 position)
    {
        int nearest = 0;
        for (int p = 1; p < _table.Pockets.Length; p++)
        {
            if ((_table.Pockets[p].Center - position).sqrMagnitude < (_table.Pockets[nearest].Center - position).sqrMagnitude)
                nearest = p;
        }
        return nearest;
    }

    // The ball went down (into `pocket` when it was called).
    private static bool PocketedIn(ShotResult result, int number, int pocket)
    {
        foreach (PocketEvent pocketed in result.Pocketed)
        {
            if (pocketed.Number == number)
                return pocket < 0 || pocketed.PocketIndex == pocket;
        }
        return false;
    }

    // A timed release through the same ShotAccuracy as the player.
    public static ShotAccuracy.Outcome Release(in BotSettings bot, float power01, System.Random random, out float barPosition)
    {
        ShotAccuracy.Settings window = ShotAccuracy.AtPower(bot.Accuracy, bot.Scaling, power01);
        float error = ShotAccuracy.Gaussian(random) * bot.TimingSigmaMs * 0.001f;
        barPosition = ShotAccuracy.PositionFromTimingError(error, window.Period);
        return ShotAccuracy.Evaluate(barPosition, window, random);
    }

    #region Break and ball in hand
    private BotDecision Break(RulesState state, System.Random random)
    {
        var decision = new BotDecision { IsBreak = true, TargetBall = -1, TargetPocket = -1 };
        Vector2 cue = _work[0].Pos;
        if (state.BallInHand)
        {
            cue = RackBuilder.DefaultCueBallPosition(_table) + new Vector2(((float)random.NextDouble() * 2f - 1f) * 0.15f, 0f);
            decision.PlaceCueBall = true;
            decision.CueBallPosition = cue;
        }

        Vector2 toApex = (_table.FootSpot - cue).normalized;
        float spread = ((float)random.NextDouble() * 2f - 1f) * _bot.BreakSpreadDegrees;
        decision.Direction = ShotAccuracy.Rotate(toApex, spread);
        float power = Mathf.Lerp(_bot.BreakPowerMin, _bot.BreakPowerMax, (float)random.NextDouble());
        decision.Speed = _bot.Power.SpeedFromPull(power);
        decision.ConsideredDirections = new[] { ShotAccuracy.Rotate(toApex, 4f), ShotAccuracy.Rotate(toApex, -3f) };
        return decision;
    }

    // Grid plus random samples over the allowed area; keeps the spot with the best direct shot.
    private Vector2 ChooseCueBallPosition(bool kitchenOnly, System.Random random)
    {
        float minY = -_table.MaxY;
        float maxY = kitchenOnly ? _table.HeadStringZ : _table.MaxY;
        int samples = Mathf.Max(8, _bot.BallInHandSamples);
        int gridCount = samples * 4 / 5;
        int columns = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(gridCount * _table.MaxX / (maxY - minY) * 2f)));
        int rows = Mathf.Max(2, gridCount / columns);

        Vector2 best = RackBuilder.DefaultCueBallPosition(_table);
        float bestScore = float.MinValue;
        for (int i = 0; i < samples; i++)
        {
            Vector2 position;
            if (i < columns * rows)
            {
                float u = (i % columns + 0.5f) / columns;
                float v = (i / columns + 0.5f) / rows;
                position = new Vector2(Mathf.Lerp(-_table.MaxX, _table.MaxX, u), Mathf.Lerp(minY, maxY, v));
            }
            else
            {
                position = new Vector2(
                    Mathf.Lerp(-_table.MaxX, _table.MaxX, (float)random.NextDouble()),
                    Mathf.Lerp(minY, maxY, (float)random.NextDouble()));
            }

            if (!IsFreeSpot(position))
                continue;

            GenerateCandidates(position);
            float score = -10f;
            foreach (Candidate candidate in _candidates)
                score = Mathf.Max(score, candidate.Score);
            if (score > bestScore)
            {
                bestScore = score;
                best = position;
            }
        }

        if (!IsFreeSpot(best))
            best = RackBuilder.FindFreeSpot(_work, _table, best, kitchenOnly);
        return best;
    }

    private bool IsFreeSpot(Vector2 position)
    {
        float min = 2f * _radius + 0.002f;
        for (int i = 1; i < _work.Length; i++)
        {
            if (!_work[i].Pocketed && (_work[i].Pos - position).sqrMagnitude < min * min)
                return false;
        }
        return true;
    }

    #endregion

    #region Candidates
    private void GenerateCandidates(Vector2 cue)
    {
        _candidates.Clear();
        float cosMaxCut = Mathf.Cos(_bot.MaxCutAngle * Mathf.Deg2Rad);
        float contact = 2f * _radius;

        for (int ball = 1; ball < _work.Length; ball++)
        {
            if (!_legal[ball] || _work[ball].Pocketed)
                continue;

            Vector2 target = _work[ball].Pos;
            for (int pocket = 0; pocket < _table.Pockets.Length; pocket++)
            {
                // v4 V4: the 8 only into a pocket the rules allow (not the blocked one; the picked one).
                if (ball == BallGroups.EightBall && !_anyEightPocket && _state != null && !_state.EightPocketAllowed(_side, pocket))
                    continue;
                PocketInfo info = _table.Pockets[pocket];
                Vector2 toPocket = info.Center - target;
                float pocketDistance = toPocket.magnitude;
                if (pocketDistance < 1e-4f)
                    continue;
                Vector2 objectDir = toPocket / pocketDistance;

                // Steep approaches hit the jaws.
                float entryCos = Vector2.Dot(objectDir, info.Inward);
                if (entryCos < (info.IsCorner ? 0.62f : 0.6f))
                    continue;

                Vector2 ghost = target - objectDir * contact;
                if (!_table.IsInsidePlayArea(ghost))
                    continue;

                Vector2 toGhost = ghost - cue;
                float cueDistance = toGhost.magnitude;
                if (cueDistance < 1e-4f)
                    continue;
                Vector2 aim = toGhost / cueDistance;
                float cutCos = Vector2.Dot(aim, objectDir);
                if (cutCos < cosMaxCut)
                    continue;

                if (!SegmentClear(cue, ghost, ball, 0, contact) || !SegmentClear(target, info.Center, ball, 0, contact))
                    continue;

                float objectSpeed = SpeedToCover(Mathf.Max(0f, pocketDistance - info.CaptureRadius * 0.5f), _bot.ArrivalSpeed);
                float transfer = cutCos * (1f + _pool.BallRestitution) * 0.5f;
                float contactSpeed = objectSpeed / Mathf.Max(transfer, 0.05f);
                float speed = Mathf.Max(_bot.MinShotSpeed, SpeedToCover(cueDistance, contactSpeed));
                if (speed > _bot.Power.MaxSpeed)
                    continue;

                float score = _bot.WeightCut * cutCos
                              - _bot.WeightCueDistance * cueDistance
                              - _bot.WeightPocketDistance * pocketDistance
                              + _bot.WeightPocketAngle * entryCos;
                _candidates.Add(new Candidate { Direction = aim, Speed = speed, Score = score, Ball = ball, Pocket = pocket });
            }
        }
    }

    // No other ball's centre within `clearance` of the segment (the moving ball would touch it).
    private bool SegmentClear(Vector2 from, Vector2 to, int ignoreA, int ignoreB, float clearance)
    {
        float clearanceSq = clearance * clearance;
        for (int i = 0; i < _work.Length; i++)
        {
            if (i == ignoreA || i == ignoreB || _work[i].Pocketed)
                continue;
            if (TableGeometry.DistanceSquaredToSegment(_work[i].Pos, from, to, out _) < clearanceSq)
                return false;
        }
        return true;
    }

    // Initial speed that still has `arrival` left after `distance`, for a ball that starts sliding with no roll
    // (both the cue ball off the tip and a hit ball): the slide costs it 2/7 of its speed before it rolls.
    public float SpeedToCover(float distance, float arrival) => BallMotion.SpeedToCover(distance, arrival, _pool);

    private Vector2[] Considered()
    {
        var directions = new List<Vector2>(3);
        var balls = new HashSet<int>();
        foreach (Candidate candidate in _candidates)
        {
            if (balls.Add(candidate.Ball))
                directions.Add(candidate.Direction);
            if (directions.Count == 3)
                break;
        }
        return directions.ToArray();
    }
    #endregion

    #region Safety
    // Softest shot that legally hits an own ball and keeps the rules happy; then any legal contact.
    private void Safety(RulesState state, System.Random random, ref BotDecision decision)
    {
        decision.IsSafety = true;
        Side side = state.Turn;
        Vector2 cue = _work[0].Pos;

        for (int ball = 1; ball < _work.Length; ball++)
        {
            if (!_legal[ball] || _work[ball].Pocketed)
                continue;
            Vector2 aim = (_work[ball].Pos - cue).normalized;
            Prediction prediction = _predictor.Predict(_work, aim, _legal);
            if (prediction.Hit != PredictionHit.Ball || !prediction.IsLegal)
                continue;

            foreach (float speed in SafetySpeeds)
            {
                RulesOutcome outcome = SimulateOutcome(state, aim, speed, CallFor(state, ball), out _);
                if (outcome.Foul == FoulType.None && !Loses(outcome, side))
                {
                    decision.Direction = aim;
                    decision.Speed = speed;
                    decision.TargetBall = ball;
                    return;
                }
            }
        }

        // Nothing clean: a random legal contact, checked once by simulation.
        Vector2 fallback = Vector2.up;
        float fallbackSpeed = 2.5f;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            int ball = RandomLegalBall(random);
            if (ball < 0)
                break;
            Vector2 toBall = _work[ball].Pos - cue;
            var side90 = new Vector2(-toBall.y, toBall.x).normalized;
            float offset = ((float)random.NextDouble() * 2f - 1f) * 1.8f * _radius;
            Vector2 aim = (toBall + side90 * offset).normalized;
            Prediction prediction = _predictor.Predict(_work, aim, _legal);
            if (prediction.Hit != PredictionHit.Ball || !prediction.IsLegal)
                continue;

            fallback = aim;
            RulesOutcome outcome = SimulateOutcome(state, aim, 2.2f, CallFor(state, ball), out _);
            if (outcome.Foul == FoulType.None && !Loses(outcome, side))
            {
                fallbackSpeed = 2.2f;
                break;
            }
        }

        decision.Direction = fallback;
        decision.Speed = fallbackSpeed;
    }

    private int RandomLegalBall(System.Random random)
    {
        int count = 0;
        for (int i = 1; i < _legal.Length; i++)
        {
            if (_legal[i] && !_work[i].Pocketed)
                count++;
        }
        if (count == 0)
            return -1;
        int pick = random.Next(count);
        for (int i = 1; i < _legal.Length; i++)
        {
            if (_legal[i] && !_work[i].Pocketed && pick-- == 0)
                return i;
        }
        return -1;
    }
    #endregion

    // Under a calling rule: the call for a shot at `ball` (its nearest pocket), else -1.
    private int CallFor(RulesState state, int ball) => state.NeedsCall(state.Turn) ? NearestPocket(_work[ball].Pos) : -1;

    private RulesOutcome SimulateOutcome(RulesState state, Vector2 direction, float speed, int call, out ShotResult result) =>
        SimulateOutcome(state, direction, speed, call, Vector2.zero, out result);

    private RulesOutcome SimulateOutcome(RulesState state, Vector2 direction, float speed, int call, Vector2 tip, out ShotResult result)
    {
        _sim.SetBalls(_work);
        result = _sim.Simulate(new ShotParams(direction, speed, tip, _bot.CueElevation), 20f);
        int before = state.CalledPocket;
        state.CalledPocket = call;
        RulesOutcome outcome = EightBallRules.Evaluate(state, result);
        state.CalledPocket = before;
        return outcome;
    }

    private static bool Loses(RulesOutcome outcome, Side side) => outcome.MatchOver && outcome.Next.Winner != side;
}
