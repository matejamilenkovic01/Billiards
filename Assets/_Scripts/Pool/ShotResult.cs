using System.Collections.Generic;
using UnityEngine;

public enum CollisionKind
{
    Ball = 0,
    Cushion = 1
}

public struct PocketEvent
{
    public int Number;
    public int PocketIndex;
    public float Time;
}

public struct CollisionEvent
{
    public float Time;
    public float Speed;
    public CollisionKind Kind;
    public int BallA;
    public int BallB;   // -1 for cushions
}

// Everything the rules need from one shot, recorded while it is simulated.
public sealed class ShotResult
{
    public int FirstContactBall = -1;
    public Vector2 FirstContactCuePos;
    public Vector2 FirstContactObjectDir;
    public bool AnyCushionAfterContact;
    public bool CueBallPocketed;
    public float Duration;
    // Balls that left the table without reaching a pocket (should stay 0; the checks watch it).
    public int Escaped;

    public readonly List<PocketEvent> Pocketed = new(16);
    public readonly List<CollisionEvent> Collisions = new(512);
    public readonly Vector2[] FinalPositions = new Vector2[BallGroups.BallCount];

    public void Reset()
    {
        FirstContactBall = -1;
        FirstContactCuePos = Vector2.zero;
        FirstContactObjectDir = Vector2.zero;
        AnyCushionAfterContact = false;
        CueBallPocketed = false;
        Duration = 0f;
        Escaped = 0;
        Pocketed.Clear();
        Collisions.Clear();
    }

    public bool WasPocketed(int number)
    {
        foreach (PocketEvent pocketEvent in Pocketed)
        {
            if (pocketEvent.Number == number)
                return true;
        }
        return false;
    }

    public int CountPocketed(BallGroup group)
    {
        int count = 0;
        foreach (PocketEvent pocketEvent in Pocketed)
        {
            if (BallGroups.Contains(group, pocketEvent.Number))
                count++;
        }
        return count;
    }
}
