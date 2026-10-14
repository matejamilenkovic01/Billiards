using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

// Plays the bot's turn on a ~9 s timeline (scaled time, so pause stops it): think, aim, pull back, strike.
// The decision itself is computed on a worker thread while the cue "searches".
public class BotController : MonoBehaviour
{
    private BotBrain _brain;
    private BotSettings _settings;
    private Coroutine _turn;
    private Vector2 _aim = Vector2.up;

    public void TakeTurn()
    {
        CancelTurn();
        _turn = StartCoroutine(TurnRoutine());
    }

    public void CancelTurn()
    {
        if (_turn != null)
            StopCoroutine(_turn);
        _turn = null;
    }

    private void EnsureBrain(MatchController match)
    {
        if (_brain != null)
            return;
        PoolSettings pool = match.Config.ToPoolSettings();
        _settings = match.BotProfile.ToBotSettings(match.Config);
        _brain = new BotBrain(match.Geometry, pool, _settings);
    }

    private IEnumerator TurnRoutine()
    {
        MatchController match = MatchController.Instance;
        BotProfile profile = match.BotProfile;
        EnsureBrain(match);

        RulesState rules = match.Rules.Clone();
        var balls = (BallState[])match.Balls.Clone();
        var random = new System.Random(match.Random.Next());
        Task<BotDecision> decisionTask = Task.Run(() => _brain.ChooseShot(rules, balls, random));

        float jitter = Random.Range(-profile.ThinkJitter, profile.ThinkJitter);
        float thinkEnd = profile.ThinkDuration + jitter;
        float aimEnd = profile.AimEnd + jitter;
        float pullEnd = profile.PullEnd + jitter;
        float strikeAt = profile.StrikeAt + jitter;

        _aim = InitialAim(match);
        CueView cue = match.Cue;
        cue.SetVisible(true);
        GameEvents.RaiseTurnPhaseChanged(TurnPhase.Aim);

        float elapsed = 0f;
        bool placed = false;
        BotDecision decision = default;
        bool hasDecision = false;

        // Think: the cue drifts between a few options while the decision is computed.
        while (elapsed < thinkEnd || !hasDecision)
        {
            if (!hasDecision && decisionTask.IsCompleted)
            {
                if (decisionTask.IsFaulted)
                {
                    Debug.LogException(decisionTask.Exception);
                    decision = FallbackDecision(match);
                }
                else
                {
                    decision = decisionTask.Result;
                }
                hasDecision = true;
            }

            if (hasDecision && decision.PlaceCueBall && !placed)
            {
                yield return MoveCueBall(match, decision.CueBallPosition, profile.CueBallMoveDuration);
                elapsed += profile.CueBallMoveDuration;
                placed = true;
            }

            Vector2 target = hasDecision ? Considered(decision, elapsed) : ShotAccuracy.Rotate(_aim, Mathf.Sin(elapsed * 1.3f) * 6f);
            _aim = Turn(_aim, target, 25f * Time.deltaTime);
            PoseCue(match, _aim, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Aim: settle on the chosen line with small corrections that die out.
        while (elapsed < aimEnd)
        {
            float remaining = Mathf.Max(0f, aimEnd - elapsed);
            float wobble = Mathf.Sin(elapsed * 9f) * Mathf.Min(1.2f, remaining * 0.5f);
            _aim = Turn(_aim, ShotAccuracy.Rotate(decision.Direction, wobble), 40f * Time.deltaTime);
            PoseCue(match, _aim, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Pull back in proportion to the power, then hold for a beat.
        MatchConfig config = match.Config;
        float targetPull = Mathf.Clamp01(ShotAccuracy.PullFromSpeed(decision.Speed, config.MinSpeed, config.MaxSpeed, config.PowerExponent));
        GameEvents.RaiseTurnPhaseChanged(TurnPhase.Pulling);
        float pullStart = elapsed;
        while (elapsed < strikeAt)
        {
            float t = Mathf.InverseLerp(pullStart, pullEnd, elapsed);
            PoseCue(match, decision.Direction, Mathf.SmoothStep(0f, targetPull, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Release: same accuracy function as the player, with the bot's zone odds and Club Cue window.
        ShotAccuracy.Outcome release = BotBrain.Release(_settings, random, out _);
        Vector2 direction = ShotAccuracy.Rotate(decision.Direction, release.AngleErrorDeg);
        float speed = Mathf.Min(decision.Speed * release.PowerFactor, config.MaxSpeed);
        Debug.Log($"Bot: shot after {elapsed:F2}s ({(decision.IsBreak ? "break" : decision.IsSafety ? "safety" : $"ball {decision.TargetBall} -> pocket {decision.TargetPocket}")}, " +
                  $"{speed:F2} m/s, release {release.Zone} {release.AngleErrorDeg:+0.0;-0.0} deg{(decision.PlaceCueBall ? ", ball in hand" : "")})");

        _turn = null;
        match.Shoot(direction, speed, new ShotFeedback
        {
            Zone = release.Zone,
            Power01 = targetPull,
            IsPlayer = false,
            AngleErrorDeg = release.AngleErrorDeg
        });
    }

    private static Vector2 InitialAim(MatchController match)
    {
        Vector2 cue = match.Balls[0].Pos;
        Vector2 aim = match.Geometry.FootSpot - cue;
        float best = float.MaxValue;
        for (int i = 1; i < match.Balls.Length; i++)
        {
            if (match.Balls[i].Pocketed || !match.LegalTargets[i])
                continue;
            float distance = (match.Balls[i].Pos - cue).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                aim = match.Balls[i].Pos - cue;
            }
        }
        return aim.sqrMagnitude > 0f ? aim.normalized : Vector2.up;
    }

    // Cycles through the considered directions, ending on the chosen one.
    private static Vector2 Considered(BotDecision decision, float elapsed)
    {
        Vector2[] options = decision.ConsideredDirections;
        if (options == null || options.Length == 0)
            return decision.Direction;
        int index = Mathf.FloorToInt(elapsed / 1.1f) % (options.Length + 1);
        return index < options.Length ? options[index] : decision.Direction;
    }

    private static Vector2 Turn(Vector2 from, Vector2 to, float maxDegrees)
    {
        float angle = Vector2.SignedAngle(from, to);
        return ShotAccuracy.Rotate(from, Mathf.Clamp(angle, -maxDegrees, maxDegrees)).normalized;
    }

    private static void PoseCue(MatchController match, Vector2 aim, float pull)
    {
        Vector3 ball = match.Table.BallCenterWorld(match.Balls[0].Pos);
        Vector3 direction = match.Table.TableDirectionToWorld(aim);
        match.Cue.SetPose(ball, direction, pull);
        if (match.CameraRig != null)
            match.CameraRig.SetAim(ball, direction);
    }

    private IEnumerator MoveCueBall(MatchController match, Vector2 target, float duration)
    {
        Vector2 start = match.Balls[0].Pos;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            Vector2 position = Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
            match.PlaceCueBall(position);
            PoseCue(match, _aim, 0f);
            yield return null;
        }
        match.PlaceCueBall(target);
    }

    private static BotDecision FallbackDecision(MatchController match) => new()
    {
        Direction = InitialAim(match),
        Speed = 2f,
        TargetBall = -1,
        TargetPocket = -1
    };
}
