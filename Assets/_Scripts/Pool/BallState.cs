using UnityEngine;

// One ball on the table plane: x across the table, y along it (world z). 0 = cue ball.
public struct BallState
{
    public int Number;
    public Vector2 Pos;
    public Vector2 Vel;
    // R·ω on the table plane (m/s): rolling without slipping when Roll == Vel.
    public Vector2 Roll;
    // R·ω about the vertical (m/s): side spin, + = counter-clockwise seen from above (right english).
    public float Side;
    public bool Pocketed;
    public int PocketIndex;
    // Display only: total rolled angle (radians) and the current roll axis in world space.
    public float RollAngle;
    public Vector3 RollAxis;

    public bool IsMoving => Vel.x != 0f || Vel.y != 0f || Roll.x != 0f || Roll.y != 0f || Side != 0f;

    public void Stop()
    {
        Vel = Roll = Vector2.zero;
        Side = 0f;
    }
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
    public float SlideFriction;
    public float RollSlipEpsilon;
    public float RollDecel;
    public float LinearDamping;
    public float StopSpeed;
    public float BallRestitution;
    public float RackGap;

    // Off-centre hits (implementation_definition_iteration_v4 V1).
    public float SquirtDegPerR;
    public float SideTransfer;
    public float SideSpinDecel;
    public float SwerveFactor;
    public float SwerveMinElevation;
    public float BallFriction;
    public float BallFrictionFast;
    public float BallFrictionDecay;
    public float CushionRestitutionSlow;
    public float CushionRestitutionFast;
    public float CushionFastSpeed;
    public float CushionSpinFriction;
}

public struct ShotParams
{
    public Vector2 Direction;
    public float Speed;
    // Where the tip meets the cue ball, in ball radii from its centre: x right, y up (|Tip| <= SpinMaxOffset).
    public Vector2 Tip;
    // Cue elevation in degrees (CueClearance): with side spin above SwerveMinElevation the cue ball curves (masse).
    public float Elevation;

    public ShotParams(Vector2 direction, float speed)
    {
        Direction = direction.normalized;
        Speed = speed;
        Tip = Vector2.zero;
        Elevation = 0f;
    }

    public ShotParams(Vector2 direction, float speed, Vector2 tip, float elevation)
    {
        Direction = direction.normalized;
        Speed = speed;
        Tip = tip;
        Elevation = elevation;
    }
}

// Pull -> cue ball speed (V1): the same curve for every cue up to SharedUntil (HARD), then a straight line from
// HardSpeed to the cue's own MaxSpeed (its POWER).
public struct PowerCurve
{
    public float MinSpeed;
    public float HardSpeed;
    public float SharedUntil;
    public float Exponent;
    public float MaxSpeed;

    public float SpeedFromPull(float pull01)
    {
        float p = Mathf.Clamp01(pull01);
        if (p <= SharedUntil)
            return MinSpeed + (HardSpeed - MinSpeed) * Mathf.Pow(p / SharedUntil, Exponent);
        return Mathf.Lerp(HardSpeed, MaxSpeed, (p - SharedUntil) / (1f - SharedUntil));
    }

    public float PullFromSpeed(float speed)
    {
        if (speed >= HardSpeed)
            return MaxSpeed > HardSpeed ? Mathf.Lerp(SharedUntil, 1f, Mathf.InverseLerp(HardSpeed, MaxSpeed, speed)) : SharedUntil;
        float t = Mathf.InverseLerp(MinSpeed, HardSpeed, speed);
        return SharedUntil * Mathf.Pow(t, 1f / Exponent);
    }
}
