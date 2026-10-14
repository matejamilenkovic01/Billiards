using System;
using UnityEngine;

[Serializable]
public class CueDefinition
{
    public string Id;
    public string Name;

    [Header("Economy")]
    [Tooltip("Total wins needed before the cue can be bought.")]
    public int WinsToUnlock;
    public int Price;
    public bool InfiniteEnergy;
    public int MaxEnergy;
    [Tooltip("Coins for a full refill.")]
    public int RefillPrice;

    [Header("Release bar")]
    [Range(0f, 1f)] public float GreenHalf = 0.1f;
    [Range(0f, 1f)] public float YellowWidth = 0.3f;
    [Tooltip("Seconds for a full -1 -> 1 -> -1 cycle. Higher = slower = easier.")]
    public float OscillationPeriod = 1.3f;

    [Header("Aim guide")]
    [Tooltip("Object ball guide length in ball diameters.")]
    public float ObjectGuideDiameters = 2f;

    [Header("Look")]
    public Color ShaftColor = Color.white;
    public Color ButtColor = Color.white;
    public Color BandColor = Color.white;
    [Range(0f, 1f)] public float Glow;
    public Sprite Sprite;
    public Material Material;

    public bool IsStarter => WinsToUnlock <= 0 && Price <= 0;
}
