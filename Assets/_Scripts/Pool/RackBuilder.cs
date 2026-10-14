using UnityEngine;

public static class RackBuilder
{
    // Triangle with the apex on the foot spot, 8 in the centre, one solid and one stripe in the back
    // corners, everything else shuffled. Balls sit a hair apart so the rack is stable before the break.
    public static BallState[] Build(System.Random random, TableGeometry table, float gap)
    {
        var balls = new BallState[BallGroups.BallCount];
        for (int i = 0; i < balls.Length; i++)
            balls[i].Number = i;

        balls[0].Pos = DefaultCueBallPosition(table);

        // Rack slots: row 0 is the apex, row 4 the back row (towards the foot rail).
        var order = new int[15];
        bool solidOnLeft = random.NextDouble() < 0.5;
        int[] solids = { 1, 2, 3, 4, 5, 6, 7 };
        int[] stripes = { 9, 10, 11, 12, 13, 14, 15 };
        Shuffle(solids, random);
        Shuffle(stripes, random);

        const int eightSlot = 4;        // row 2, middle
        const int backLeftSlot = 10;    // row 4, first
        const int backRightSlot = 14;   // row 4, last
        order[eightSlot] = BallGroups.EightBall;
        order[backLeftSlot] = solidOnLeft ? solids[0] : stripes[0];
        order[backRightSlot] = solidOnLeft ? stripes[0] : solids[0];

        var rest = new int[12];
        for (int i = 0; i < 6; i++)
        {
            rest[i] = solids[i + 1];
            rest[6 + i] = stripes[i + 1];
        }
        Shuffle(rest, random);

        int next = 0;
        for (int slot = 0; slot < 15; slot++)
        {
            if (slot != eightSlot && slot != backLeftSlot && slot != backRightSlot)
                order[slot] = rest[next++];
        }

        float spacing = 2f * table.BallRadius + gap;
        float rowStep = spacing * Mathf.Sqrt(3f) * 0.5f;
        int index = 0;
        for (int row = 0; row < 5; row++)
        {
            for (int k = 0; k <= row; k++)
            {
                int number = order[index++];
                balls[number].Pos = new Vector2((k - row * 0.5f) * spacing, table.FootSpotZ + row * rowStep);
            }
        }

        return balls;
    }

    public static Vector2 DefaultCueBallPosition(TableGeometry table) =>
        new(0f, (table.HeadStringZ - table.MaxY) * 0.5f);

    // Re-spot (e.g. the 8 after a break): the foot spot, or the nearest free point on the long string,
    // first towards the foot rail, then back towards the head.
    public static Vector2 SpotPosition(BallState[] balls, TableGeometry table, int ignoreNumber)
    {
        float step = table.BallRadius * 0.5f;
        Vector2 spot = table.FootSpot;
        for (float y = spot.y; y <= table.MaxY; y += step)
        {
            var candidate = new Vector2(0f, y);
            if (IsFree(balls, candidate, table.BallRadius, ignoreNumber))
                return candidate;
        }
        for (float y = spot.y; y >= -table.MaxY; y -= step)
        {
            var candidate = new Vector2(0f, y);
            if (IsFree(balls, candidate, table.BallRadius, ignoreNumber))
                return candidate;
        }
        return spot;
    }

    public static bool IsFree(BallState[] balls, Vector2 position, float radius, int ignoreNumber)
    {
        float minSq = 4f * radius * radius;
        for (int i = 0; i < balls.Length; i++)
        {
            if (i == ignoreNumber || balls[i].Pocketed)
                continue;
            if ((balls[i].Pos - position).sqrMagnitude < minSq)
                return false;
        }
        return true;
    }

    private static void Shuffle(int[] values, System.Random random)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
