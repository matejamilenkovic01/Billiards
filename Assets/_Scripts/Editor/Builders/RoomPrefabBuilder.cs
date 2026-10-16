using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A small billiard club around the playing table: carpet, green walls over wood panelling, two more tables
// under pendant lamps, wall lights, a bar and a cue rack. Walls and ceiling face inward only, so the
// top-down ball-in-hand camera (which sits above the ceiling) still sees the table.
public static class RoomPrefabBuilder
{
    public const string RoomPath = TablePrefabBuilder.Folder + "/Room.prefab";

    private const float HalfX = 4.3f;
    private const float HalfZ = 5.6f;
    private const float Height = 3.1f;
    private const float WainscotHeight = 1.05f;
    private static readonly Color WarmLight = new(1f, 0.82f, 0.6f);

    [MenuItem("Tools/Billiards/Builders/Room Prefab")]
    public static void Build()
    {
        var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);
        using var scope = new TempObjectScope();
        var root = new GameObject("Room");

        // Floor and ceiling.
        GameObject floor = Quad(root.transform, "Floor", Material("Carpet"), new Vector3(0f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(HalfX * 2f, HalfZ * 2f));
        SetTiling(floor, new Vector2(HalfX * 2f / 0.9f, HalfZ * 2f / 0.9f));
        Quad(root.transform, "Ceiling", Material("Ceiling"), new Vector3(0f, Height, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector2(HalfX * 2f, HalfZ * 2f));

        // Four walls: wallpaper above wood panelling, each facing the room.
        Wall(root.transform, "WallNorth", new Vector3(0f, 0f, HalfZ), Quaternion.Euler(0f, 0f, 0f), HalfX * 2f);
        Wall(root.transform, "WallSouth", new Vector3(0f, 0f, -HalfZ), Quaternion.Euler(0f, 180f, 0f), HalfX * 2f);
        Wall(root.transform, "WallEast", new Vector3(HalfX, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), HalfZ * 2f);
        Wall(root.transform, "WallWest", new Vector3(-HalfX, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), HalfZ * 2f);

        // Two more tables, each under its own lamp.
        var decor = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefabBuilder.DecorTablePath);
        foreach (float side in new[] { -1f, 1f })
        {
            var table = (GameObject)PrefabUtility.InstantiatePrefab(decor, root.transform);
            table.transform.localPosition = new Vector3(side * 2.75f, config.TableHeight, 1.2f);
            PendantLamp(root.transform, new Vector3(side * 2.75f, config.TableHeight + 1.15f, 1.2f), true);
        }

        // Wall lights so the room never falls into black.
        Sconce(root.transform, new Vector3(-HalfX + 0.06f, 1.95f, -2.4f), Quaternion.Euler(0f, 90f, 0f), true);
        Sconce(root.transform, new Vector3(HalfX - 0.06f, 1.95f, -2.4f), Quaternion.Euler(0f, -90f, 0f), true);
        Sconce(root.transform, new Vector3(-HalfX + 0.06f, 1.95f, 3.4f), Quaternion.Euler(0f, 90f, 0f), true);
        Sconce(root.transform, new Vector3(HalfX - 0.06f, 1.95f, 3.4f), Quaternion.Euler(0f, -90f, 0f), true);

        Bar(root.transform);
        CueRack(root.transform, new Vector3(-HalfX + 0.05f, 0f, -0.6f));
        Picture(root.transform, "PictureA", new Vector3(HalfX - 0.01f, 1.75f, -0.4f), Quaternion.Euler(0f, 90f, 0f), new Vector2(1.1f, 0.75f));
        Picture(root.transform, "PictureB", new Vector3(0f, 1.8f, -HalfZ + 0.01f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.4f, 0.85f));

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child != root.transform && child.GetComponent<Light>() == null && !PrefabUtility.IsPartOfPrefabInstance(child))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
        }

        PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
        Object.DestroyImmediate(root);
        Debug.Log("RoomPrefabBuilder: done");
    }

    private static void Wall(Transform parent, string name, Vector3 basePosition, Quaternion facingOut, float width)
    {
        // A quad's front faces its local -Z; the rotation turns that towards the room.
        Quaternion inward = facingOut;
        var wall = new GameObject(name).transform;
        wall.SetParent(parent, false);
        wall.localPosition = basePosition;
        wall.localRotation = inward;

        GameObject paper = Quad(wall, "Paper", Material("Wallpaper"), new Vector3(0f, WainscotHeight + (Height - WainscotHeight) * 0.5f, 0f), Quaternion.identity, new Vector2(width, Height - WainscotHeight));
        SetTiling(paper, new Vector2(width / 1.2f, (Height - WainscotHeight) / 1.2f));
        GameObject panel = Quad(wall, "Panelling", Material("Wainscot"), new Vector3(0f, WainscotHeight * 0.5f, 0f), Quaternion.identity, new Vector2(width, WainscotHeight));
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
        Box(lamp, "Shade", Material("LampShade"), new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.12f, 1.25f));
        GameObject glow = Quad(lamp, "Glow", Material("LampGlow"), Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector2(0.3f, 1.2f));
        glow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        foreach (float z in new[] { -0.45f, 0.45f })
            Box(lamp, "Rod", Material("Brass"), new Vector3(0f, 0.12f + (3.1f - position.y - 0.12f) * 0.5f, z), new Vector3(0.012f, 3.1f - position.y - 0.12f, 0.012f));

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
            stick.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.CueMaterialPath(1 + i % 5));
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
        string path = $"{TablePrefabBuilder.Folder}/RoomMeshes/{go.transform.parent.name}_{go.name}.asset";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        filter.sharedMesh = mesh;
    }

    private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.MaterialPath(name));
}
