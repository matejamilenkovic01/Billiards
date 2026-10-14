using UnityEngine;

// One ball on the table plane: x across the table, y along it (world z). 0 = cue ball.
public struct BallState
{
    public int Number;
    public Vector2 Pos;
    public Vector2 Vel;
    public bool Pocketed;
    public int PocketIndex;
    // Display only: total rolled angle (radians) and the current roll axis in world space.
    public float RollAngle;
    public Vector3 RollAxis;

    public bool IsMoving => Vel.x != 0f || Vel.y != 0f;
}

// Plain numbers the simulation needs; filled from MatchConfig by the gameplay layer.
public struct PoolSettings
{
    public float HalfWidth;
    public float HalfLength;
    public float BallRadius;
    public float CornerPocketRadius;
    public float SidePocketRadius;
    public float CornerMouth;
    public float SideMouth;
    public float JawDepth;
    public float CornerPocketInset;
    public float SidePocketInset;
    public float HeadStringZ;
    public float FootSpotZ;

    public float TimeStep;
    public float RollDecel;
    public float LinearDamping;
    public float StopSpeed;
    public float BallRestitution;
    public float CushionRestitution;
    public float CushionFriction;
    public float RackGap;
}

public struct ShotParams
{
    public Vector2 Direction;
    public float Speed;

    public ShotParams(Vector2 direction, float speed)
    {
        Direction = direction.normalized;
        Speed = speed;
    }
}
