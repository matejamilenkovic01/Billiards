using UnityEngine;

// Where the player's cue points at the start of a turn (gameplay redesign G8): a plain full-ball line at the
// nearest legal ball the cue ball can reach, never a solved shot. If that line happens to be a straight-in
// pot, it is turned a few degrees so the player still has to aim. Break: full ball on the apex ball.
public static class DefaultAim
{
    private const float MinTurnDegrees = 2f;
    private const float MaxTurnDegrees = 4f;
    // A turn never takes the aim off the target ball: at most a cut this thin (fraction of full contact).
    private const float MaxOffsetFraction = 0.9f;

    public static Vector2 Choose(ShotPredictor predictor, BallState[] balls, bool[] legalTargets, bool isBreak,
        Vector2 footSpot, float ballRadius, System.Random random)
    {
        Vector2 cue = balls[0].Pos;
        if (isBreak)
            return Direction(cue, ApexPosition(balls, footSpot));

        int target = NearestLegal(predictor, balls, legalTargets, true);
        if (target < 0)
        {
            target = NearestLegal(predictor, balls, legalTargets, false);
            return target < 0 ? Vector2.up : Direction(cue, balls[target].Pos);
        }

        Vector2 aim = Direction(cue, balls[target].Pos);
        if (!IsPot(predictor, balls, aim, legalTargets, target))
            return aim;

        // Straight-in: turn by 2-4 degrees (less when the ball is far, so the aim still hits it).
        float distance = (balls[target].Pos - cue).magnitude;
        float maxTurn = Mathf.Asin(Mathf.Clamp01(MaxOffsetFraction * 2f * ballRadius / Mathf.Max(distance, 1e-4f))) * Mathf.Rad2Deg;
        float turn = Mathf.Min(Mathf.Lerp(MinTurnDegrees, MaxTurnDegrees, (float)random.NextDouble()), maxTurn);
        float sign = random.NextDouble() < 0.5 ? -1f : 1f;
        foreach (float candidate in new[] { sign * turn, -sign * turn, sign * maxTurn, -sign * maxTurn })
        {
            Vector2 turned = ShotAccuracy.Rotate(aim, candidate).normalized;
            if (!IsPot(predictor, balls, turned, legalTargets, target) && Hits(predictor, balls, turned, legalTargets, target))
                return turned;
        }
        return ShotAccuracy.Rotate(aim, sign * turn).normalized;
    }

    // The aim's predicted object ball line ends in a pocket.
    public static bool IsPot(ShotPredictor predictor, BallState[] balls, Vector2 aim, bool[] legalTargets, int target)
    {
        Prediction prediction = predictor.Predict(balls, aim, legalTargets);
        if (prediction.Hit != PredictionHit.Ball || prediction.HitBall != target)
            return false;
        return predictor.CastFrom(balls, balls[target].Pos, prediction.ObjectDir, out _, out _) == PredictionHit.Pocket;
    }

    private static bool Hits(ShotPredictor predictor, BallState[] balls, Vector2 aim, bool[] legalTargets, int target)
    {
        Prediction prediction = predictor.Predict(balls, aim, legalTargets);
        return prediction.Hit == PredictionHit.Ball && prediction.HitBall == target;
    }

    // Nearest legal ball; with requireClear, only one the straight line from the cue ball reaches first.
    public static int NearestLegal(ShotPredictor predictor, BallState[] balls, bool[] legalTargets, bool requireClear)
    {
        Vector2 cue = balls[0].Pos;
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 1; i < balls.Length; i++)
        {
            if (balls[i].Pocketed || (legalTargets != null && !legalTargets[i]))
                continue;
            float distance = (balls[i].Pos - cue).sqrMagnitude;
            if (distance >= bestDistance)
                continue;
            if (requireClear && !Hits(predictor, balls, Direction(cue, balls[i].Pos), legalTargets, i))
                continue;
            bestDistance = distance;
            best = i;
        }
        return best;
    }

    private static Vector2 ApexPosition(BallState[] balls, Vector2 footSpot)
    {
        Vector2 best = footSpot;
        float bestDistance = float.MaxValue;
        for (int i = 1; i < balls.Length; i++)
        {
            float distance = (balls[i].Pos - footSpot).sqrMagnitude;
            if (!balls[i].Pocketed && distance < bestDistance)
            {
                bestDistance = distance;
                best = balls[i].Pos;
            }
        }
        return best;
    }

    private static Vector2 Direction(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        return delta.sqrMagnitude > 1e-10f ? delta.normalized : Vector2.up;
    }
}
