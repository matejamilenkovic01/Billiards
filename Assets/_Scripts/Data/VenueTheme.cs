using UnityEngine;

// A venue's look (implementation_definition_v3 V6, G3): cloth, wood, pocket caps, the table light and the room's
// ambient. The match applies it at start (VenueView); commit 9 adds the room variants and neon.
[CreateAssetMenu(fileName = "VenueTheme", menuName = "Billiards/Venue Theme")]
public class VenueTheme : ScriptableObject
{
    public string Id;
    public string DisplayName;

    [Header("Table")]
    public Color FeltColor = new(0.12f, 0.42f, 0.27f);
    [Tooltip("Cushions are covered in the same cloth, a little darker.")]
    [Range(0.5f, 1f)] public float CushionShade = 0.92f;
    public Material WoodMaterial;
    public Material CapMaterial;

    [Header("Light (TV-tournament: strong, even, neutral-warm)")]
    public Color TableLightColor = new(1f, 0.9f, 0.8f);
    public float TableLightIntensity = 32f;
    [ColorUsage(false, true)] public Color LampPanelEmission = new(1.25f, 1.18f, 1.05f);

    [Header("Room")]
    public Color AmbientColor = new(1.1f, 1.0f, 0.88f);
    public int RoomVariant;
    public string NeonText;
    public Color NeonColor = Color.white;
}
