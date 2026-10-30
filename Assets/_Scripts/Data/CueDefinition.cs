using System;
using UnityEngine;

public enum PerfectShape
{
    Puff = 0,      // soft specks drifting from the tip
    Sparks = 1,    // fast bright sparks
    Ring = 2       // an expanding ring (Legend adds a flash of the stick's glow)
}

// What the cue throws off at the ball on a PERFECT release (v4 V9): each cue its own.
[Serializable]
public struct PerfectEffect
{
    public PerfectShape Shape;
    public Color Color;
    public Color Accent;
    [Tooltip("Particles in the burst.")]
    public int Count;
    public float Speed;
    [Tooltip("Metres.")]
    public float Size;
    [Tooltip("The stick's emission flashes by this much on a PERFECT (0 = no flash).")]
    public float CueFlash;

    public static PerfectEffect Default => new()
    {
        Shape = PerfectShape.Puff,
        Color = new Color(0.82f, 0.9f, 1f, 0.85f),
        Accent = new Color(1f, 1f, 1f, 0.85f),
        Count = 10,
        Speed = 0.12f,
        Size = 0.006f
    };
}

[Serializable]
public class CueDefinition
{
    public string Id;
    public string Name;

    [Header("Economy")]
    [Tooltip("Coins; 0 = owned from the start.")]
    public int Price;

    [Header("Release bar")]
    [Range(0f, 1f)] public float GreenHalf = 0.1f;
    [Range(0f, 1f)] public float YellowWidth = 0.3f;
    [Tooltip("Seconds for a full -1 -> 1 -> -1 cycle. Higher = slower = easier.")]
    public float OscillationPeriod = 1.3f;

    [Header("Power (v4 V1, POWER)")]
    [Tooltip("Cue ball speed at a full pull, m/s; up to HARD (85%) every cue is the same.")]
    public float MaxSpeed = 4.5f;

    [Header("Aim guide")]
    [Tooltip("Object ball guide length in ball diameters.")]
    public float ObjectGuideDiameters = 2f;

    [Header("Shape and effect (v4 V9)")]
    public CueProfile Profile = CueProfile.Default;
    public PerfectEffect Perfect = PerfectEffect.Default;
    [Tooltip("The emission breathes by this share (Legend); 0 = steady.")]
    [Range(0f, 1f)] public float EmissionPulse;
    public Mesh Mesh;

    [Header("Look")]
    public Color ShaftColor = Color.white;
    public Color ButtColor = Color.white;
    public Color BandColor = Color.white;
    [Range(0f, 1f)] public float Glow;
    public Sprite Sprite;
    public Material Material;

    public bool IsStarter => Price <= 0;
}
