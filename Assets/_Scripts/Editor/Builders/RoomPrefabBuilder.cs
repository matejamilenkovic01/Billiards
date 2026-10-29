using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The rooms around the playing table (v3 commit 9): one builder, five venues from RoomStyles. Carpet, walls over
// wood panelling, decor tables under pendant lamps, wall lights, then per venue a bar, lounge sofas or tiered
// seating, and a neon sign. No realtime lights out here (v3 V6): the room is lit by ambient and emissive fixtures,
// so the table under its lamp stays the brightest thing. Walls and ceiling face inward only, so the top-down
// ball-in-hand camera (which sits above the ceiling) still sees the table.
public static class RoomPrefabBuilder
{
    public static string RoomPath(string venueId) => $"{TablePrefabBuilder.Folder}/Room_{venueId}.prefab";

    public enum Furniture { Bar, Sofas, Seating }

    public struct RoomStyle
    {
        public string VenueId;
        public float Height;
        public Color WallDark;
        public Color WallLight;
        public int WallStripes;
        public Color CarpetBase;
        public Color CarpetLattice;
        public Color WainscotTint;
        public Color LampShade;
        public Color CeilingColor;
        public float Glow;              // how brightly the room's own surfaces glow (V6 room emission)
        public Furniture Furniture;
        public int DecorTables;         // 0..2
        public string NeonText;
        public Color NeonColor;
    }

    // One per venue (v3 V10 / commit 9; looks from the meta design table), Bronze first. Riverside keeps the V6 look.
    public static readonly RoomStyle[] Styles =
    {
        new() { VenueId = "basement", Height = 2.6f, WallDark = Hex("3A1F17"), WallLight = Hex("54301F"), WallStripes = 0,
                CarpetBase = Hex("2A2624"), CarpetLattice = Hex("4A4440"), WainscotTint = new Color(0.55f, 0.48f, 0.42f),
                LampShade = Hex("3A3A36"), CeilingColor = Hex("100D0B"), Glow = 0.85f, Furniture = Furniture.Bar, DecorTables = 2,
                NeonText = "BAR", NeonColor = Hex("FF3B3B") },
        new() { VenueId = "downtown", Height = 3.0f, WallDark = Hex("3A4F5C"), WallLight = Hex("4E6878"), WallStripes = 6,
                CarpetBase = Hex("1D2630"), CarpetLattice = Hex("3C5566"), WainscotTint = new Color(0.45f, 0.45f, 0.48f),
                LampShade = Hex("22313A"), CeilingColor = Hex("0C1014"), Glow = 0.95f, Furniture = Furniture.Bar, DecorTables = 2,
                NeonText = "POOL", NeonColor = Hex("3FA9FF") },
        new() { VenueId = "riverside", Height = 3.1f, WallDark = Hex("244A3F"), WallLight = Hex("33604F"), WallStripes = 8,
                CarpetBase = Hex("4E1719"), CarpetLattice = Hex("8C6A2E"), WainscotTint = new Color(0.75f, 0.68f, 0.62f),
                LampShade = Hex("1D4A32"), CeilingColor = Hex("15110E"), Glow = 1.0f, Furniture = Furniture.Bar, DecorTables = 2,
                NeonText = "CLUB", NeonColor = Hex("FFB347") },
        new() { VenueId = "lounge", Height = 3.4f, WallDark = Hex("1A1F3A"), WallLight = Hex("2A3157"), WallStripes = 12,
                CarpetBase = Hex("5A1020"), CarpetLattice = Hex("B08A3C"), WainscotTint = new Color(0.6f, 0.45f, 0.38f),
                LampShade = Hex("5A1020"), CeilingColor = Hex("0F0D16"), Glow = 0.95f, Furniture = Furniture.Sofas, DecorTables = 1,
                NeonText = "GRAND", NeonColor = Hex("F4F1FF") },
        new() { VenueId = "royal", Height = 3.8f, WallDark = Hex("121218"), WallLight = Hex("1C1C26"), WallStripes = 4,
                CarpetBase = Hex("141A2E"), CarpetLattice = Hex("2C3A66"), WainscotTint = new Color(0.32f, 0.3f, 0.34f),
                LampShade = Hex("1A1A22"), CeilingColor = Hex("08080B"), Glow = 0.9f, Furniture = Furniture.Seating, DecorTables = 0,
                NeonText = "MASTERS", NeonColor = Hex("FFC84A") }
    };

    public static RoomStyle Style(string venueId) => System.Array.Find(Styles, s => s.VenueId == venueId);

    public static string CarpetMaterial(string venueId) => $"Carpet_{venueId}";
    public static string WallpaperMaterial(string venueId) => $"Wallpaper_{venueId}";
    public static string WainscotMaterial(string venueId) => $"Wainscot_{venueId}";
    public static string LampShadeMaterial(string venueId) => $"LampShade_{venueId}";
    public static string CeilingMaterial(string venueId) => $"Ceiling_{venueId}";
    public static string DecorFeltMaterial(string venueId) => $"FeltDecor_{venueId}";
    public static string DecorCushionMaterial(string venueId) => $"CushionDecor_{venueId}";
    public static string NeonMaterialPath(string venueId) => $"{GeneratedArtBuilder.MaterialsFolder}/Neon_{venueId}.mat";

    private const float HalfX = 4.3f;
    private const float HalfZ = 5.6f;
    private const float WainscotHeight = 1.05f;
    private static readonly Color WarmLight = new(1f, 0.82f, 0.6f);

    // The room being built (styles change the materials and the height).
    private static RoomStyle _style;
    private static float Height => _style.Height;

    [MenuItem("Tools/Billiards/Builders/Room Prefab")]
    public static void Build()
    {
        foreach (RoomStyle style in Styles)
            Build(style);
        Debug.Log("RoomPrefabBuilder: done");
    }

    private static void Build(RoomStyle style)
    {
        _style = style;
        var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);
        using var scope = new TempObjectScope();
        var root = new GameObject("Room_" + style.VenueId);

        // Floor and ceiling.
        GameObject floor = Quad(root.transform, "Floor", Material(CarpetMaterial(style.VenueId)), new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(HalfX * 2f, HalfZ * 2f));
        SetTiling(floor, new Vector2(HalfX * 2f / 0.9f, HalfZ * 2f / 0.9f));
        Quad(root.transform, "Ceiling", Material(CeilingMaterial(style.VenueId)), new Vector3(0f, Height, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector2(HalfX * 2f, HalfZ * 2f));

        // Four walls: wallpaper above wood panelling, each facing the room.
        Wall(root.transform, "WallNorth", new Vector3(0f, 0f, HalfZ), Quaternion.Euler(0f, 0f, 0f), HalfX * 2f);
        Wall(root.transform, "WallSouth", new Vector3(0f, 0f, -HalfZ), Quaternion.Euler(0f, 180f, 0f), HalfX * 2f);
        Wall(root.transform, "WallEast", new Vector3(HalfX, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), HalfZ * 2f);
        Wall(root.transform, "WallWest", new Vector3(-HalfX, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), HalfZ * 2f);

        // Decor tables, each under its own lamp: two at the sides, one on the left, or none.
        var decor = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.DecorTablePath);
        float[] sides = style.DecorTables >= 2 ? new[] { -1f, 1f } : style.DecorTables == 1 ? new[] { -1f } : new float[0];
        foreach (float side in sides)
        {
            var table = (GameObject)PrefabUtility.InstantiatePrefab(decor, root.transform);
            table.transform.localPosition = new Vector3(side * 2.75f, config.TableHeight, 1.2f);
            foreach (MeshRenderer renderer in table.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name is "Felt" or "PocketLips")
                    renderer.sharedMaterial = Material(DecorFeltMaterial(style.VenueId));
                else if (renderer.name == "Cushions")
                    renderer.sharedMaterial = Material(DecorCushionMaterial(style.VenueId));
            }
            PendantLamp(root.transform, new Vector3(side * 2.75f, config.TableHeight + 1.15f, 1.2f), false);
        }

        // v4 V6: the billiard lamp over the playing table (the spot that lights it is part of the table prefab).
        TableLamp(root.transform, config);

        // Wall lights so the room never falls into black.
        float sconceY = Mathf.Min(1.95f, Height - 0.6f);
        Sconce(root.transform, new Vector3(-HalfX + 0.06f, sconceY, -2.4f), Quaternion.Euler(0f, 90f, 0f), false);
        Sconce(root.transform, new Vector3(HalfX - 0.06f, sconceY, -2.4f), Quaternion.Euler(0f, -90f, 0f), false);
        Sconce(root.transform, new Vector3(-HalfX + 0.06f, sconceY, 3.4f), Quaternion.Euler(0f, 90f, 0f), false);
        Sconce(root.transform, new Vector3(HalfX - 0.06f, sconceY, 3.4f), Quaternion.Euler(0f, -90f, 0f), false);

        switch (style.Furniture)
        {
            case Furniture.Bar: Bar(root.transform); break;
            case Furniture.Sofas: Sofas(root.transform); break;
            case Furniture.Seating: Seating(root.transform); break;
        }
        CueRack(root.transform, new Vector3(-HalfX + 0.05f, 0f, -0.6f));
        Picture(root.transform, "PictureA", new Vector3(HalfX - 0.01f, 1.75f, -0.4f), Quaternion.Euler(0f, 90f, 0f), new Vector2(1.1f, 0.75f));
        Picture(root.transform, "PictureB", new Vector3(0f, 1.8f, -HalfZ + 0.01f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.4f, 0.85f));
        Neon(root.transform, style);

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child != root.transform && child.GetComponent<Light>() == null && child.GetComponent<TMPro.TextMeshPro>() == null &&
                !PrefabUtility.IsPartOfPrefabInstance(child))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
        }

        PrefabUtility.SaveAsPrefabAsset(root, RoomPath(style.VenueId));
        Object.DestroyImmediate(root);
    }

    // v4 V6: a real billiard lamp: a closed body along the table, ShadeCount shades with warm bulbs (their insides
    // glow), hung from the ceiling; the venue's LampStyle. It casts no shadow; LampView fades it out of the way.
    public const float LampBottomAboveCloth = 1.1f;
    private const int ShadeSides = 32;
    private const float ShadeThickness = 0.004f;

    private static void TableLamp(Transform parent, MatchConfig config)
    {
        LampStyle style = AssetDatabase.LoadAssetAtPath<VenueTheme>(DataAssetBuilder.VenuePath(_style.VenueId)).Lamp;
        float bottom = config.TableHeight + LampBottomAboveCloth;
        float shadeHeight = style.Shade == LampShadeShape.Cylinder ? 0.24f : 0.18f;
        float shadeRadius = style.Shade switch { LampShadeShape.Cylinder => 0.13f, LampShadeShape.Cone => 0.2f, _ => 0.19f };
        int count = Mathf.Max(1, style.ShadeCount);
        float spacing = count >= 4 ? 0.5f : 0.62f;
        float bodyLength = (count - 1) * spacing + 0.46f;
        const float bodyThickness = 0.05f;
        float bodyY = bottom + shadeHeight + bodyThickness * 0.5f;

        var lamp = new GameObject("TableLamp").transform;
        lamp.SetParent(parent, false);
        var renderers = new List<Renderer>();
        renderers.Add(Box(lamp, "Body", LampMaterial("Body"), new Vector3(0f, bodyY, 0f), new Vector3(0.26f, bodyThickness, bodyLength)).GetComponent<MeshRenderer>());

        var outer = new MeshData();
        var inner = new MeshData();
        var bulbs = new MeshData();
        var rim = new MeshData();
        for (int i = 0; i < count; i++)
        {
            var center = new Vector3(0f, bottom, (i - (count - 1) * 0.5f) * spacing);
            Shade(outer, inner, center, style.Shade, shadeRadius, shadeHeight);
            Sphere(bulbs, center + Vector3.up * 0.06f, 0.045f);
            if (style.GoldRim)
                Torus(rim, center, shadeRadius, 0.009f);
        }
        renderers.Add(LampMesh(lamp, "Shades", outer, LampMaterial("Shade")));
        renderers.Add(LampMesh(lamp, "ShadeInsides", inner, LampMaterial("Inner")));
        renderers.Add(LampMesh(lamp, "Bulbs", bulbs, LampMaterial("Bulb")));
        if (style.GoldRim)
            renderers.Add(LampMesh(lamp, "Rims", rim, LampMaterial("Rim")));

        // Chains (thicker, the body's metal) or slim rods (gold on the Royal lamp) up to the ceiling.
        float hangerBottom = bodyY + bodyThickness * 0.5f;
        float hanger = style.Hanger == LampHanger.Chains ? 0.02f : 0.012f;
        Material hangerMaterial = LampMaterial(style.GoldRim ? "Rim" : "Body");
        foreach (float z in new[] { -bodyLength * 0.5f + 0.12f, bodyLength * 0.5f - 0.12f })
            renderers.Add(Box(lamp, "Hanger", hangerMaterial, new Vector3(0f, (hangerBottom + Height) * 0.5f, z), new Vector3(hanger, Height - hangerBottom, hanger)).GetComponent<MeshRenderer>());

        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        var view = lamp.gameObject.AddComponent<LampView>();
        view.Configure(renderers.ToArray(), bottom, new Vector3(0f, config.TableHeight, 0f));
    }

    private static Material LampMaterial(string part) => AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.TableLampMaterial(_style.VenueId, part));

    private sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<int> Triangles = new();

        public int Add(Vector3 position, Vector3 normal)
        {
            Vertices.Add(position);
            Normals.Add(normal.normalized);
            return Vertices.Count - 1;
        }

        public void Quad(int a, int b, int c, int d)
        {
            Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
            Triangles.Add(a); Triangles.Add(c); Triangles.Add(d);
        }
    }

    // The shade's outline: radius and height for t = 0 (lower rim) .. 1 (where it meets the body).
    private static Vector2 ShadeProfile(LampShadeShape shape, float radius, float height, float t) => shape switch
    {
        LampShadeShape.Dome => new Vector2(Mathf.Max(0.035f, radius * Mathf.Cos(t * Mathf.PI * 0.5f)), height * Mathf.Sin(t * Mathf.PI * 0.5f)),
        LampShadeShape.Cone => new Vector2(Mathf.Lerp(radius, 0.04f, t), height * t),
        _ => new Vector2(radius, height * t)
    };

    // A closed shell: outside (facing out), inside (facing in, the glowing part), the lower rim and a top disk.
    private static void Shade(MeshData outer, MeshData inner, Vector3 center, LampShadeShape shape, float radius, float height)
    {
        const int rings = 10;
        for (int pass = 0; pass < 2; pass++)
        {
            MeshData mesh = pass == 0 ? outer : inner;
            float offset = pass == 0 ? 0f : -ShadeThickness;
            int start = mesh.Vertices.Count;
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                Vector2 p = ShadeProfile(shape, radius, height, t);
                Vector2 next = ShadeProfile(shape, radius, height, Mathf.Min(1f, t + 0.01f));
                Vector2 previous = ShadeProfile(shape, radius, height, Mathf.Max(0f, t - 0.01f));
                Vector2 tangent = (next - previous).normalized;
                var outward = new Vector2(tangent.y, -tangent.x);   // right of going up = away from the axis
                for (int k = 0; k <= ShadeSides; k++)
                {
                    float angle = k / (float)ShadeSides * Mathf.PI * 2f;
                    var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 position = center + around * (p.x + offset) + Vector3.up * p.y;
                    Vector3 normal = around * outward.x + Vector3.up * outward.y;
                    mesh.Add(position, pass == 0 ? normal : -normal);
                }
            }
            int row = ShadeSides + 1;
            for (int r = 0; r < rings; r++)
            for (int k = 0; k < ShadeSides; k++)
            {
                int a = start + r * row + k, b = a + 1, c = a + row + 1, d = a + row;
                if (pass == 0)
                    mesh.Quad(a, d, c, b);
                else
                    mesh.Quad(a, b, c, d);
            }
            // Top disk: up on the outside, down on the inside.
            Vector2 top = ShadeProfile(shape, radius, height, 1f);
            Disk(mesh, center + Vector3.up * top.y, top.x + offset, pass == 0);
        }
        // Lower rim between the two surfaces, facing down.
        int rimStart = outer.Vertices.Count;
        for (int k = 0; k <= ShadeSides; k++)
        {
            float angle = k / (float)ShadeSides * Mathf.PI * 2f;
            var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            outer.Add(center + around * radius, Vector3.down);
            outer.Add(center + around * (radius - ShadeThickness), Vector3.down);
        }
        for (int k = 0; k < ShadeSides; k++)
        {
            int a = rimStart + k * 2;
            outer.Quad(a, a + 1, a + 3, a + 2);
        }
    }

    private static void Disk(MeshData mesh, Vector3 center, float radius, bool up)
    {
        int middle = mesh.Add(center, up ? Vector3.up : Vector3.down);
        int start = mesh.Vertices.Count;
        for (int k = 0; k <= ShadeSides; k++)
        {
            float angle = k / (float)ShadeSides * Mathf.PI * 2f;
            mesh.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius, up ? Vector3.up : Vector3.down);
        }
        for (int k = 0; k < ShadeSides; k++)
        {
            if (up)
            {
                mesh.Triangles.Add(middle); mesh.Triangles.Add(start + k + 1); mesh.Triangles.Add(start + k);
            }
            else
            {
                mesh.Triangles.Add(middle); mesh.Triangles.Add(start + k); mesh.Triangles.Add(start + k + 1);
            }
        }
    }

    private static void Sphere(MeshData mesh, Vector3 center, float radius)
    {
        const int stacks = 8, slices = 12;
        int start = mesh.Vertices.Count;
        for (int i = 0; i <= stacks; i++)
        {
            float phi = i / (float)stacks * Mathf.PI;
            for (int j = 0; j <= slices; j++)
            {
                float theta = j / (float)slices * Mathf.PI * 2f;
                var normal = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                mesh.Add(center + normal * radius, normal);
            }
        }
        for (int i = 0; i < stacks; i++)
        for (int j = 0; j < slices; j++)
        {
            int a = start + i * (slices + 1) + j;
            mesh.Quad(a, a + 1, a + slices + 2, a + slices + 1);
        }
    }

    private static void Torus(MeshData mesh, Vector3 center, float radius, float tube)
    {
        const int sides = 8;
        int start = mesh.Vertices.Count;
        for (int k = 0; k <= ShadeSides; k++)
        {
            float angle = k / (float)ShadeSides * Mathf.PI * 2f;
            var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int j = 0; j <= sides; j++)
            {
                float beta = j / (float)sides * Mathf.PI * 2f;
                Vector3 normal = around * Mathf.Cos(beta) + Vector3.up * Mathf.Sin(beta);
                mesh.Add(center + around * radius + normal * tube, normal);
            }
        }
        for (int k = 0; k < ShadeSides; k++)
        for (int j = 0; j < sides; j++)
        {
            int a = start + k * (sides + 1) + j;
            mesh.Quad(a, a + sides + 1, a + sides + 2, a + 1);
        }
    }

    private static Renderer LampMesh(Transform parent, string name, MeshData data, Material material)
    {
        var mesh = new Mesh { name = $"{_style.VenueId}_TableLamp_{name}" };
        mesh.indexFormat = data.Vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(data.Vertices);
        mesh.SetNormals(data.Normals);
        var uvs = new List<Vector2>(data.Vertices.Count);
        foreach (Vector3 v in data.Vertices)
            uvs.Add(new Vector2(v.x * 8f + v.y * 3f, v.z * 8f + v.y * 5f));
        mesh.SetUVs(0, uvs);
        // Every triangle faces the way its normals point (Unity's front: Cross(b - a, c - a)).
        for (int t = 0; t < data.Triangles.Count; t += 3)
        {
            Vector3 a = data.Vertices[data.Triangles[t]], b = data.Vertices[data.Triangles[t + 1]], c = data.Vertices[data.Triangles[t + 2]];
            Vector3 n = data.Normals[data.Triangles[t]] + data.Normals[data.Triangles[t + 1]] + data.Normals[data.Triangles[t + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f)
                (data.Triangles[t + 1], data.Triangles[t + 2]) = (data.Triangles[t + 2], data.Triangles[t + 1]);
        }
        mesh.SetTriangles(data.Triangles, 0);
        mesh.RecalculateBounds();
        string path = $"{TablePrefabBuilder.Folder}/RoomMeshes/{mesh.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
            AssetDatabase.CreateAsset(mesh, path);
        else
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    // A neon sign on the far wall above the bar / seating: emissive text over HDR (it blooms), on a dark board.
    private static void Neon(Transform parent, RoomStyle style)
    {
        var sign = new GameObject("Neon").transform;
        sign.SetParent(parent, false);
        float y = Mathf.Min(Height - 0.45f, 2.35f);
        sign.localPosition = new Vector3(0.6f, y, HalfZ - 0.04f);
        float width = 0.3f + style.NeonText.Length * 0.24f;
        GameObject board = Box(sign, "Board", Material("BarTop"), new Vector3(0f, 0f, 0.01f), new Vector3(width, 0.48f, 0.03f));
        board.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        var textObject = new GameObject("Text");
        textObject.transform.SetParent(sign, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        // TextMeshPro faces -Z; the wall's front faces the room (-Z here), so no turn is needed.
        var text = textObject.AddComponent<TMPro.TextMeshPro>();
        text.font = UIPrefabBuilder.Font;
        text.fontSharedMaterial = NeonMaterial(style);
        text.text = style.NeonText;
        text.fontSize = 2.4f;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(width, 0.46f);
        text.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // HDR face and glow on the SDF font: bloom picks it up (threshold 1.1).
    private static Material NeonMaterial(RoomStyle style)
    {
        string path = NeonMaterialPath(style.VenueId);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(UIPrefabBuilder.Font.material);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("TextMeshPro/Distance Field");
        material.SetTexture(TMPro.ShaderUtilities.ID_MainTex, UIPrefabBuilder.Font.atlasTexture);
        material.SetColor(TMPro.ShaderUtilities.ID_FaceColor, style.NeonColor * 1.5f);
        material.EnableKeyword(TMPro.ShaderUtilities.Keyword_Glow);
        material.SetColor(TMPro.ShaderUtilities.ID_GlowColor, style.NeonColor * 1.6f);
        material.SetFloat(TMPro.ShaderUtilities.ID_GlowOuter, 0.55f);
        material.SetFloat(TMPro.ShaderUtilities.ID_GlowPower, 0.6f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Wall(Transform parent, string name, Vector3 basePosition, Quaternion facingOut, float width)
    {
        // A quad's front faces its local -Z; the rotation turns that towards the room.
        Quaternion inward = facingOut;
        var wall = new GameObject(name).transform;
        wall.SetParent(parent, false);
        wall.localPosition = basePosition;
        wall.localRotation = inward;

        GameObject paper = Quad(wall, "Paper", Material(WallpaperMaterial(_style.VenueId)), new Vector3(0f, WainscotHeight + (Height - WainscotHeight) * 0.5f, 0f), Quaternion.identity, new Vector2(width, Height - WainscotHeight));
        SetTiling(paper, new Vector2(width / 1.2f, (Height - WainscotHeight) / 1.2f));
        GameObject panel = Quad(wall, "Panelling", Material(WainscotMaterial(_style.VenueId)), new Vector3(0f, WainscotHeight * 0.5f, 0f), Quaternion.identity, new Vector2(width, WainscotHeight));
        SetTiling(panel, new Vector2(width / 1.5f, 1f));
        // Dado rail between the two.
        GameObject rail = Box(wall, "DadoRail", Material("Wood"), new Vector3(0f, WainscotHeight, -0.02f), new Vector3(width, 0.06f, 0.04f));
        rail.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void PendantLamp(Transform parent, Vector3 position, bool withLight)
    {
        var lamp = new GameObject("PendantLamp").transform;
        lamp.SetParent(parent, false);
        lamp.localPosition = position;
        Box(lamp, "Shade", Material(LampShadeMaterial(_style.VenueId)), new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.12f, 1.25f));
        GameObject glow = Quad(lamp, "Glow", Material("LampGlow"), Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector2(0.3f, 1.2f));
        glow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        foreach (float z in new[] { -0.45f, 0.45f })
            Box(lamp, "Rod", Material("Brass"), new Vector3(0f, 0.12f + (Height - position.y - 0.12f) * 0.5f, z), new Vector3(0.012f, Height - position.y - 0.12f, 0.012f));

        if (!withLight)
            return;
        var lightObject = new GameObject("Spot");
        lightObject.transform.SetParent(lamp, false);
        lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = WarmLight;
        light.intensity = 3.5f;
        light.range = 3f;
        light.spotAngle = 120f;
        light.innerSpotAngle = 70f;
        light.shadows = LightShadows.None;
        lightObject.AddComponent<UniversalAdditionalLightData>();
    }

    private static void Sconce(Transform parent, Vector3 position, Quaternion rotation, bool withLight)
    {
        var sconce = new GameObject("Sconce").transform;
        sconce.SetParent(parent, false);
        sconce.localPosition = position;
        sconce.localRotation = rotation;
        Box(sconce, "Plate", Material("Brass"), Vector3.zero, new Vector3(0.16f, 0.24f, 0.03f));
        GameObject shade = Box(sconce, "Shade", Material("LampGlow"), new Vector3(0f, 0.04f, 0.09f), new Vector3(0.18f, 0.14f, 0.14f));
        shade.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        if (!withLight)
            return;
        var lightObject = new GameObject("Point");
        lightObject.transform.SetParent(sconce, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.05f, 0.35f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = WarmLight;
        light.intensity = 2.6f;
        light.range = 5f;
        light.shadows = LightShadows.None;
        lightObject.AddComponent<UniversalAdditionalLightData>();
    }

    // A bar along the far wall with a shelf of bottles behind it.
    private static void Bar(Transform parent)
    {
        var bar = new GameObject("Bar").transform;
        bar.SetParent(parent, false);
        bar.localPosition = new Vector3(0.6f, 0f, HalfZ - 0.55f);
        Box(bar, "Counter", Material("Wood"), new Vector3(0f, 0.52f, 0f), new Vector3(3.2f, 1.04f, 0.6f));
        Box(bar, "Top", Material("BarTop"), new Vector3(0f, 1.06f, -0.03f), new Vector3(3.3f, 0.05f, 0.7f));
        Box(bar, "Footrest", Material("Brass"), new Vector3(0f, 0.18f, -0.36f), new Vector3(3.2f, 0.035f, 0.035f));
        Box(bar, "Shelf", Material("Wood"), new Vector3(0f, 1.55f, 0.45f), new Vector3(2.8f, 0.04f, 0.2f));
        var random = new System.Random(3);
        for (int i = 0; i < 14; i++)
        {
            float x = -1.3f + i * 0.2f;
            float height = 0.24f + (float)random.NextDouble() * 0.1f;
            GameObject bottle = Primitive(PrimitiveType.Cylinder, "Bottle", bar, Material(i % 3 == 0 ? "BottleGreen" : "BottleAmber"));
            bottle.transform.localPosition = new Vector3(x, 1.57f + height * 0.5f, 0.45f);
            bottle.transform.localScale = new Vector3(0.07f, height * 0.5f, 0.07f);
        }
        foreach (float x in new[] { -1.0f, 0f, 1.0f })
        {
            GameObject stool = Primitive(PrimitiveType.Cylinder, "Stool", bar, Material("Leather"));
            stool.transform.localPosition = new Vector3(x, 0.72f, -0.75f);
            stool.transform.localScale = new Vector3(0.36f, 0.04f, 0.36f);
            Box(bar, "StoolLeg", Material("Brass"), new Vector3(x, 0.35f, -0.75f), new Vector3(0.04f, 0.7f, 0.04f));
        }
    }

    // Grand Lounge: low leather sofas and a side table along the far wall.
    private static void Sofas(Transform parent)
    {
        var lounge = new GameObject("Sofas").transform;
        lounge.SetParent(parent, false);
        lounge.localPosition = new Vector3(0.6f, 0f, HalfZ - 0.6f);
        foreach (float x in new[] { -1.2f, 1.2f })
        {
            Box(lounge, "Seat", Material("Leather"), new Vector3(x, 0.24f, 0f), new Vector3(1.9f, 0.48f, 0.8f));
            Box(lounge, "Back", Material("Leather"), new Vector3(x, 0.6f, 0.32f), new Vector3(1.9f, 0.6f, 0.18f));
            Box(lounge, "ArmL", Material("Leather"), new Vector3(x - 0.9f, 0.42f, 0f), new Vector3(0.14f, 0.36f, 0.8f));
            Box(lounge, "ArmR", Material("Leather"), new Vector3(x + 0.9f, 0.42f, 0f), new Vector3(0.14f, 0.36f, 0.8f));
        }
        Box(lounge, "SideTable", Material("BarTop"), new Vector3(0f, 0.3f, 0f), new Vector3(0.42f, 0.6f, 0.42f));
    }

    // Royal Masters: two tiers of spectator seats along the far wall and one along each side (a TV event).
    private static void Seating(Transform parent)
    {
        var stands = new GameObject("Seating").transform;
        stands.SetParent(parent, false);
        for (int tier = 0; tier < 2; tier++)
        {
            float riser = 0.25f + tier * 0.45f;
            float z = HalfZ - 0.55f - (1 - tier) * 0.85f;
            Box(stands, "Riser" + tier, Material("BarTop"), new Vector3(0f, riser * 0.5f, z), new Vector3(HalfX * 2f - 0.4f, riser, 0.85f));
            for (int i = 0; i < 9; i++)
            {
                float x = -3.4f + i * 0.85f;
                Box(stands, "Seat", Material("Leather"), new Vector3(x, riser + 0.22f, z - 0.05f), new Vector3(0.62f, 0.12f, 0.5f));
                Box(stands, "SeatBack", Material("Leather"), new Vector3(x, riser + 0.5f, z + 0.2f), new Vector3(0.62f, 0.5f, 0.1f));
            }
        }
        foreach (float side in new[] { -1f, 1f })
        {
            Box(stands, "SideRiser", Material("BarTop"), new Vector3(side * (HalfX - 0.5f), 0.15f, 1.5f), new Vector3(0.8f, 0.3f, 4.4f));
            for (int i = 0; i < 5; i++)
                Box(stands, "SideSeat", Material("Leather"), new Vector3(side * (HalfX - 0.5f), 0.4f, -0.2f + i * 0.85f), new Vector3(0.5f, 0.12f, 0.62f));
        }
    }

    private static void CueRack(Transform parent, Vector3 position)
    {
        var rack = new GameObject("CueRack").transform;
        rack.SetParent(parent, false);
        rack.localPosition = position;
        rack.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Box(rack, "Board", Material("Wood"), new Vector3(0f, 1.0f, 0.02f), new Vector3(0.9f, 1.5f, 0.03f));
        var cue = AssetDatabase.LoadAssetAtPath<Mesh>(TablePrefabBuilder.CueMeshPath);
        for (int i = 0; i < 6; i++)
        {
            var stick = new GameObject("Cue");
            stick.transform.SetParent(rack, false);
            // The cue mesh runs along -Z from its tip: stand it up, tip at the top.
            stick.transform.localPosition = new Vector3(-0.35f + i * 0.14f, 1.7f, 0.07f);
            stick.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            stick.transform.localScale = Vector3.one * 0.95f;
            stick.AddComponent<MeshFilter>().sharedMesh = cue;
            stick.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.CueMaterialPath(1 + i % DataAssetBuilder.CueSpecs.Length));
        }
    }

    private static void Picture(Transform parent, string material, Vector3 position, Quaternion rotation, Vector2 size)
    {
        var picture = new GameObject("Picture").transform;
        picture.SetParent(parent, false);
        picture.localPosition = position;
        picture.localRotation = rotation;
        // Same orientation as the wall: local -Z points into the room.
        Box(picture, "Frame", Material("Wood"), new Vector3(0f, 0f, -0.015f), new Vector3(size.x + 0.08f, size.y + 0.08f, 0.03f));
        Quad(picture, "Canvas", Material(material), new Vector3(0f, 0f, -0.032f), Quaternion.identity, size);
    }

    private static GameObject Quad(Transform parent, string name, Material material, Vector3 position, Quaternion rotation, Vector2 size)
    {
        GameObject quad = Primitive(PrimitiveType.Quad, name, parent, material);
        quad.transform.localPosition = position;
        quad.transform.localRotation = rotation;
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        return quad;
    }

    private static GameObject Box(Transform parent, string name, Material material, Vector3 position, Vector3 size)
    {
        GameObject box = Primitive(PrimitiveType.Cube, name, parent, material);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        return box;
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // Per-object tiling without a material per wall.
    private static void SetTiling(GameObject go, Vector2 tiling)
    {
        var filter = go.GetComponent<MeshFilter>();
        Mesh mesh = Object.Instantiate(filter.sharedMesh);
        var uvs = mesh.uv;
        for (int i = 0; i < uvs.Length; i++)
            uvs[i] = Vector2.Scale(uvs[i], tiling);
        mesh.uv = uvs;
        mesh.name = go.name + "Mesh";
        string path = $"{TablePrefabBuilder.Folder}/RoomMeshes/{_style.VenueId}_{go.transform.parent.name}_{go.name}.asset";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        filter.sharedMesh = mesh;
    }

    private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath(name));

    private static Color Hex(string hex) => GeneratedArtBuilder.Hex(hex);
}
