using UnityEngine;

// The single-ball motion model of PoolPhysics in closed form, for the predictor and the bot.
// A ball leaves the cue (or another ball) sliding with no roll; cloth friction turns slip into roll until it
// rolls at 5/7 of that speed, then rolling resistance slows it: dv/dt = -(RollDecel + LinearDamping v).
public static class BallMotion
{
    public const float Gravity = 9.81f;
    // Solid ball: the slip shrinks 7/2 times faster than the speed (1 linear + 5/2 angular).
    public const float SlipRate = 3.5f;
    public const float RollGain = 2.5f;

    public static float SlideDecel(in PoolSettings settings) => settings.SlideFriction * Gravity;

    // How far a ball that starts at v0 with no roll slides before it rolls (at 5/7 v0).
    public static float SlideDistance(float v0, in PoolSettings settings)
    {
        float decel = SlideDecel(settings);
        return decel > 1e-6f ? 12f / 49f * v0 * v0 / decel : float.MaxValue;
    }

    // Rolling distance from speed v1 down to v2.
    public static float RollDistance(float v1, float v2, in PoolSettings settings)
    {
        float a = settings.RollDecel;
        float k = settings.LinearDamping;
        if (v1 <= v2)
            return 0f;
        return k > 1e-5f
            ? ((v1 - v2) - a / k * Mathf.Log((a + k * v1) / (a + k * v2))) / k
            : (v1 * v1 - v2 * v2) / (2f * a);
    }

    // Speed and roll after `distance` along a straight line, for a ball that starts at v0 with no roll.
    // False if it stops first.
    public static bool StateAtDistance(float v0, float distance, in PoolSettings settings, out float speed, out float roll)
    {
        float decel = SlideDecel(settings);
        float slide = SlideDistance(v0, settings);
        if (distance <= slide)
        {
            speed = Mathf.Sqrt(Mathf.Max(0f, v0 * v0 - 2f * decel * distance));
            roll = RollGain * (v0 - speed);
            return true;
        }

        float rolling = v0 * 5f / 7f;
        float left = distance - slide;
        if (left >= RollDistance(rolling, settings.StopSpeed, settings))
        {
            speed = roll = 0f;
            return false;
        }

        // RollDistance falls as v2 rises: bisect for the speed that is left after `left` metres.
        float low = settings.StopSpeed, high = rolling;
        for (int i = 0; i < 32; i++)
        {
            float mid = (low + high) * 0.5f;
            if (RollDistance(rolling, mid, settings) > left)
                low = mid;
            else
                high = mid;
        }
        speed = roll = (low + high) * 0.5f;
        return true;
    }

    // Initial speed (no roll) that still has `arrival` left after `distance`.
    public static float SpeedToCover(float distance, float arrival, in PoolSettings settings)
    {
        float decel = SlideDecel(settings);
        float low = arrival, high = 30f;
        for (int i = 0; i < 40; i++)
        {
            float mid = (low + high) * 0.5f;
            float rolling = mid * 5f / 7f;
            float covered = arrival >= rolling
                ? (mid * mid - arrival * arrival) / (2f * decel)   // still sliding when it gets there
                : SlideDistance(mid, settings) + RollDistance(rolling, arrival, settings);
            if (covered < distance)
                low = mid;
            else
                high = mid;
        }
        return high;
    }

    // The path of a ball from `position` with velocity `velocity` and roll `roll`, `length` metres long: a parabola
    // while it slides (the slip keeps its direction, so the friction is constant), then a straight line.
    // Writes up to points.Length points (at least 3 useful) and returns how many; 1 means it does not move.
    public static int Path(Vector2 position, Vector2 velocity, Vector2 roll, float length, in PoolSettings settings, Vector2[] points)
    {
        const int Substeps = 48;
        int count = 0;
        points[count++] = position;

        Vector2 slip = velocity - roll;
        float slipSpeed = slip.magnitude;
        float decel = SlideDecel(settings);
        Vector2 at = position;
        Vector2 heading = velocity;
        float travelled = 0f;

        if (slipSpeed > settings.RollSlipEpsilon && decel > 1e-6f)
        {
            Vector2 accel = -slip / slipSpeed * decel;
            float duration = slipSpeed / (SlipRate * decel);
            float step = duration / Substeps;
            int emitEvery = Mathf.Max(1, Mathf.CeilToInt(Substeps / (float)(points.Length - 2)));
            for (int i = 1; i <= Substeps; i++)
            {
                float t = step * i;
                Vector2 next = position + velocity * t + 0.5f * t * t * accel;
                float segment = (next - at).magnitude;
                if (travelled + segment >= length)
                {
                    points[count++] = segment > 1e-9f ? at + (next - at) * ((length - travelled) / segment) : next;
                    return count;
                }
                travelled += segment;
                at = next;
                if ((i % emitEvery == 0 || i == Substeps) && count < points.Length - 1)
                    points[count++] = at;
            }
            heading = velocity + accel * duration;
        }

        if (heading.sqrMagnitude < 1e-10f)
            return count;
        if (count < points.Length)
            points[count++] = at + heading.normalized * (length - travelled);
        return count;
    }
}
