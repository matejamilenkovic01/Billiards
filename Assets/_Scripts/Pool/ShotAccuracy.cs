using UnityEngine;

public enum ReleaseZone
{
    Green = 0,
    Yellow = 1,
    Red = 2
}

// Release bar math shared by the player and the bot, so the bot has no hidden accuracy.
public static class ShotAccuracy
{
    public struct Settings
    {
        public float GreenHalf;
        public float YellowWidth;
        public float YellowErrorMinDeg;
        public float YellowErrorMaxDeg;
        public float RedErrorMinDeg;
        public float RedErrorMaxDeg;
        public float RedPowerVariance;
    }

    public struct Outcome
    {
        public ReleaseZone Zone;
        public float AngleErrorDeg;   // signed; applied to the cue ball direction
        public float PowerFactor;     // 1 except in the red zone
    }

    // Linear ping-pong starting at the bottom (-1): constant speed keeps the bar fair and readable.
    public static float BarPosition(float elapsed, float period)
    {
        if (period <= 0f)
            return 0f;

        float phase = elapsed / period;
        phase -= Mathf.Floor(phase);
        // 0 -> -1, 0.5 -> 1, 1 -> -1
        return phase < 0.5f ? -1f + phase * 4f : 3f - phase * 4f;
    }

    public static ReleaseZone ZoneOf(float position, float greenHalf, float yellowWidth)
    {
        float distance = Mathf.Abs(position);
        if (distance <= greenHalf)
            return ReleaseZone.Green;
        return distance <= greenHalf + yellowWidth ? ReleaseZone.Yellow : ReleaseZone.Red;
    }

    public static Outcome Evaluate(float position, in Settings settings, System.Random random)
    {
        position = Mathf.Clamp(position, -1f, 1f);
        float distance = Mathf.Abs(position);
        float sign = position >= 0f ? 1f : -1f;
        float yellowEnd = settings.GreenHalf + settings.YellowWidth;

        var outcome = new Outcome { PowerFactor = 1f };
        if (distance <= settings.GreenHalf)
        {
            outcome.Zone = ReleaseZone.Green;
            outcome.AngleErrorDeg = 0f;
        }
        else if (distance <= yellowEnd)
        {
            float depth = Mathf.InverseLerp(settings.GreenHalf, yellowEnd, distance);
            outcome.Zone = ReleaseZone.Yellow;
            outcome.AngleErrorDeg = sign * Mathf.Lerp(settings.YellowErrorMinDeg, settings.YellowErrorMaxDeg, depth);
        }
        else
        {
            float depth = Mathf.InverseLerp(yellowEnd, 1f, distance);
            outcome.Zone = ReleaseZone.Red;
            outcome.AngleErrorDeg = sign * Mathf.Lerp(settings.RedErrorMinDeg, settings.RedErrorMaxDeg, depth);
            float variance = settings.RedPowerVariance;
            outcome.PowerFactor = 1f + (float)(random.NextDouble() * 2.0 - 1.0) * variance;
        }

        return outcome;
    }

    // Pull curve: pull^exponent gives fine control over soft shots.
    public static float SpeedFromPull(float pull01, float minSpeed, float maxSpeed, float exponent) =>
        Mathf.Lerp(minSpeed, maxSpeed, Mathf.Pow(Mathf.Clamp01(pull01), exponent));

    public static float PullFromSpeed(float speed, float minSpeed, float maxSpeed, float exponent)
    {
        float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        return Mathf.Pow(t, 1f / exponent);
    }

    // A uniformly random bar position inside the given zone (used by the bot's simulated release).
    public static float SamplePositionInZone(ReleaseZone zone, float greenHalf, float yellowWidth, System.Random random)
    {
        float u = (float)random.NextDouble();
        float sign = random.NextDouble() < 0.5 ? -1f : 1f;
        float yellowEnd = Mathf.Min(1f, greenHalf + yellowWidth);
        return zone switch
        {
            ReleaseZone.Green => sign * u * greenHalf,
            ReleaseZone.Yellow => sign * Mathf.Lerp(greenHalf, yellowEnd, u),
            _ => sign * Mathf.Lerp(yellowEnd, 1f, u)
        };
    }
}
