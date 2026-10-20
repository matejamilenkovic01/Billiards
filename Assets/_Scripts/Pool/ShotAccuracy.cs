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
        public float Period;      // seconds for a full -1 -> 1 -> -1 cycle
        public float YellowErrorMinDeg;
        public float YellowErrorMaxDeg;
        public float RedErrorMinDeg;
        public float RedErrorMaxDeg;
        public float RedPowerVariance;
    }

    // How the shot power scales a cue's release window (implementation_definition_v3 V8): from p = 0 to p = 1.
    public struct PowerScaling
    {
        public float GreenScale0, GreenScale1;
        public float YellowScale0, YellowScale1;
        public float PeriodScale0, PeriodScale1;
        public float ErrorScale0, ErrorScale1;
    }

    public struct Outcome
    {
        public ReleaseZone Zone;
        public float AngleErrorDeg;   // signed; applied to the cue ball direction
        public float PowerFactor;     // 1 except in the red zone
    }

    // The cue's window at a shot power: harder shots shrink green and yellow, speed the bar up and miss further.
    public static Settings AtPower(in Settings cue, in PowerScaling scaling, float power01)
    {
        float p = Mathf.Clamp01(power01);
        float errors = Mathf.Lerp(scaling.ErrorScale0, scaling.ErrorScale1, p);
        Settings window = cue;
        window.GreenHalf = cue.GreenHalf * Mathf.Lerp(scaling.GreenScale0, scaling.GreenScale1, p);
        window.YellowWidth = cue.YellowWidth * Mathf.Lerp(scaling.YellowScale0, scaling.YellowScale1, p);
        window.Period = cue.Period * Mathf.Lerp(scaling.PeriodScale0, scaling.PeriodScale1, p);
        window.YellowErrorMinDeg *= errors;
        window.YellowErrorMaxDeg *= errors;
        window.RedErrorMinDeg *= errors;
        window.RedErrorMaxDeg *= errors;
        return window;
    }

    // Linear ping-pong over one phase (cycles): 0 -> -1, 0.5 -> 1, 1 -> -1. Constant speed keeps the bar fair and
    // readable; the caller adds dt / period every frame, so a period that changes mid-pull never makes it jump.
    public static float BarPositionFromPhase(float phase)
    {
        phase -= Mathf.Floor(phase);
        return phase < 0.5f ? -1f + phase * 4f : 3f - phase * 4f;
    }

    public static float BarPosition(float elapsed, float period) => period > 0f ? BarPositionFromPhase(elapsed / period) : 0f;

    // A release `errorSeconds` early or late around the moment the marker crosses the centre of green
    // (the bot and the simulated players): the marker covers 4 units per period, bouncing off the ends.
    public static float PositionFromTimingError(float errorSeconds, float period)
    {
        float position = 4f * errorSeconds / Mathf.Max(1e-4f, period);
        position = Mathf.Repeat(position + 1f, 4f) - 1f;   // -1..3
        return position > 1f ? 2f - position : position;
    }

    // Standard normal sample (Box-Muller) from the caller's generator.
    public static float Gaussian(System.Random random)
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = random.NextDouble();
        return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
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

    // Positive degrees turn the direction to the shooter's left (counter-clockwise on the table plane):
    // a release above the green zone pulls the shot left, below it pulls right.
    public static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
    }

    // Pull curve: pull^exponent gives fine control over soft shots.
    public static float SpeedFromPull(float pull01, float minSpeed, float maxSpeed, float exponent) =>
        Mathf.Lerp(minSpeed, maxSpeed, Mathf.Pow(Mathf.Clamp01(pull01), exponent));

    public static float PullFromSpeed(float speed, float minSpeed, float maxSpeed, float exponent)
    {
        float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        return Mathf.Pow(t, 1f / exponent);
    }
}
