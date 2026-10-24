using UnityEngine;

// A venue's look (implementation_definition_v3 V6, G3, commit 9): cloth, wood, pocket caps, the table light, the
// room's ambient, its baked reflections and a thumbnail. VenueView applies it (and turns on the venue's room,
// built by RoomPrefabBuilder with its neon sign) in the match and behind the menu.
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
    [Tooltip("The reflection probe's cubemap, baked in this venue's room (MatchSceneBuilder).")]
    public Texture Reflection;
    [Tooltip("512x256 render of the table in the room (Ranked Road).")]
    public Sprite Thumbnail;
}
