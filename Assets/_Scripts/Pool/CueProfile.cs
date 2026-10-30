using System;
using UnityEngine;

public enum CueButtShape
{
    Flat = 0,
    Rounded = 1,
    MetalRing = 2
}

// The cue's silhouette (v4 V9): its radius along the stick as a fraction of MatchConfig.CueButtRadius, 0 = butt,
// 1 = tip. Every cue ends in the same tip; they differ in the butt's size, how long the taper stays thick, a swelling
// wrap, and the shape of the butt. The mesh is built from it and CueClearance lifts the stick by it, so what is drawn
// is what is cleared (CueChecks.NoIntersection runs it for every cue).
[Serializable]
public struct CueProfile
{
    [Tooltip("The butt's radius against the base cue (1 = CueButtRadius).")]
    public float ButtScale;
    [Tooltip("1 = a straight cone; lower keeps the stick thick for longer.")]
    public float TaperExponent;
    [Tooltip("Where the joint is along the cue (0 butt .. 1 tip).")]
    public float JointAt;
    [Tooltip("The wrap: a swelling between these points, WrapBump thicker (a share of the radius).")]
    public float WrapStart;
    public float WrapEnd;
    public float WrapBump;
    public CueButtShape Butt;

    public static CueProfile Default => new()
    {
        ButtScale = 1f,
        TaperExponent = 0.85f,
        JointAt = 0.5f,
        WrapStart = 0.3f,
        WrapEnd = 0.42f,
        WrapBump = 0f,
        Butt = CueButtShape.Flat
    };

    private const float TipRadius01 = 0.4f;
    private const float RoundedEnd = 0.04f;
    private const float RingStart = 0.04f, RingEnd = 0.09f, RingBump = 0.12f;

    public float Radius01(float t)
    {
        t = Mathf.Clamp01(t);
        float radius = Mathf.Lerp(ButtScale, TipRadius01, Mathf.Pow(t, TaperExponent));
        if (WrapBump > 0f && t > WrapStart && t < WrapEnd)
            radius *= 1f + WrapBump * Mathf.Clamp01(Mathf.Sin(Mathf.PI * (t - WrapStart) / (WrapEnd - WrapStart)) * 3f);
        if (Butt == CueButtShape.Rounded && t < RoundedEnd)
        {
            float x = 1f - t / RoundedEnd;
            radius *= Mathf.Sqrt(1f - 0.85f * x * x);
        }
        else if (Butt == CueButtShape.MetalRing && t > RingStart && t < RingEnd)
        {
            radius *= 1f + RingBump * Mathf.Clamp01(Mathf.Sin(Mathf.PI * (t - RingStart) / (RingEnd - RingStart)) * 3f);
        }
        return radius;
    }

    // The thickest point (for the reach of the butt over a cushion).
    public float MaxRadius01()
    {
        float max = 0f;
        for (int i = 0; i <= 64; i++)
            max = Mathf.Max(max, Radius01(i / 64f));
        return max;
    }

    // The points where the radius changes character: the mesh needs a ring on each.
    public void Breakpoints(System.Collections.Generic.List<float> into)
    {
        into.Add(JointAt);
        if (WrapBump > 0f)
        {
            into.Add(WrapStart);
            into.Add(WrapEnd);
            into.Add(Mathf.Lerp(WrapStart, WrapEnd, 0.1f));
            into.Add(Mathf.Lerp(WrapStart, WrapEnd, 0.9f));
        }
        if (Butt == CueButtShape.Rounded)
            into.AddRange(new[] { 0.01f, 0.02f, 0.03f, RoundedEnd });
        if (Butt == CueButtShape.MetalRing)
            into.AddRange(new[] { RingStart, 0.05f, 0.065f, 0.08f, RingEnd });
    }
}
