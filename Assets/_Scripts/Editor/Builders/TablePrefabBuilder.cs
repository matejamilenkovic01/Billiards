using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds the table, ball and cue prefabs from primitives, using the same TableGeometry as the simulation,
// so what you see is exactly what the balls bounce off.
public static class TablePrefabBuilder
{
    public const string Folder = "Assets/_Prefabs/Table";
    public const string TablePath = Folder + "/Table.prefab";
    public const string DecorTablePath = Folder + "/TableDecor.prefab";
    public const string BallPath = Folder + "/Ball.prefab";
    public const string CuePath = Folder + "/Cue.prefab";
    public const string CueMeshPath = Folder + "/CueMesh.asset";
    public const string VolumeProfilePath = Folder + "/TableVolumeProfile.asset";
    public const string FeltMeshPath = Folder + "/FeltMesh.asset";
    public const string PocketMeshPath = Folder + "/PocketMesh.asset";
    public const string CushionMeshPath = Folder + "/CushionMesh.asset";

    // Visual sizes around the play area (metres, relative to the cloth surface). The cushion and rail sizes live
    // in MatchConfig: the cue (G6) and the aim camera clear exactly these shapes.
    private static MatchConfig _config;
    private static float CushionDepth => _config.CushionDepth;
    private static float JawThickness => _config.JawThickness;
    private static float RailWidth => _config.RailWidth;
    private static float RailTop => _config.RailTop;
    private const float RailBottom = -0.1f;
    // Above the apron top (RailBottom), deep enough for the 0.08 m drop animation.
    private const float PocketDepth = 0.085f;
    private const float PocketRimWidth = 0.022f;
    private const float FeltTile = 0.3f;
    private const float ApronBottom = -0.24f;
    private const float LegSize = 0.13f;
    private const float LampHeight = 1.25f;

    [MenuItem("Tools/Billiards/Builders/Table Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataAssetBuilder.MatchConfigPath);
        _config = config;

        BuildBall(config);
        BuildCue(config);
        BuildVolumeProfile();
        BuildTable(config, false);
        BuildTable(config, true);
        AssetDatabase.SaveAssets();
        Debug.Log("TablePrefabBuilder: done");
    }

    #region Ball and cue
    private static void BuildBall(MatchConfig config)
    {
        using var scope = new TempObjectScope();
        GameObject ball = Primitive(PrimitiveType.Sphere, "Ball", null, Material("Ball_0"));
        ball.transform.localScale = Vector3.one * config.BallRadius * 2f;
        var view = ball.AddComponent<BallView>();
        SetField(view, "_renderer", ball.GetComponent<MeshRenderer>());
        PrefabUtility.SaveAsPrefabAsset(ball, BallPath);
        Object.DestroyImmediate(ball);
    }

    private static void BuildCue(MatchConfig config)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CueMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "CueMesh" };
            AssetDatabase.CreateAsset(mesh, CueMeshPath);
        }
        FillCueMesh(mesh, config);
        EditorUtility.SetDirty(mesh);

        using var scope = new TempObjectScope();
        var cue = new GameObject("Cue");
        cue.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = cue.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.CueMaterialPath(1));
        var view = cue.AddComponent<CueView>();
        SetField(view, "_renderer", renderer);
        SetField(view, "_config", config);
        PrefabUtility.SaveAsPrefabAsset(cue, CuePath);
        Object.DestroyImmediate(cue);
    }

    // Lathe: tip at the origin, butt at -Z. u runs butt (0) -> tip (1) to match the cue albedo strip. Rings sit
    // exactly on the ferrule and tip edges (v3 V3), and the leather tip ends in a dome. Triangles wind clockwise
    // seen from outside (Unity's front face), checked against the normals.
    private static void FillCueMesh(Mesh mesh, MatchConfig config)
    {
        float tipStart = GeneratedArtBuilder.CueTipStart01(config);
        float ferruleStart = GeneratedArtBuilder.CueFerruleStart01(config);
        var rings = new List<float> { 0f, 0.006f, 0.012f, 0.05f, 0.15f, 0.3f, 0.305f, 0.42f, 0.425f, 0.47f, 0.485f, 0.5f,
                                      0.6f, 0.7f, 0.8f, 0.9f, 0.95f, ferruleStart - 0.002f, ferruleStart, tipStart, 1f };
        rings.Sort();
        const int sides = 24;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        float slope = (config.CueButtRadius - config.CueTipRadius) / config.CueLength;
        foreach (float t in rings)
        {
            float radius = config.CueButtRadius * GeneratedArtBuilder.CueRadius01(t);
            float z = -config.CueLength * (1f - t);
            AddRing(vertices, normals, uvs, sides, radius, z, t, around => new Vector3(around.x, around.y, slope).normalized);
        }
        int stride = sides + 1;
        for (int r = 0; r < rings.Count - 1; r++)
            AddBand(triangles, r * stride, (r + 1) * stride, sides);

        // Dome: a spherical cap over the tip face, a few rings towards the apex, normals from the sphere's centre.
        float tipRadius = config.CueButtRadius * GeneratedArtBuilder.CueRadius01(1f);
        float height = tipRadius * config.CueTipDome;
        float sphere = (tipRadius * tipRadius + height * height) / (2f * height);
        var centre = new Vector3(0f, 0f, height - sphere);
        float startAngle = Mathf.Asin(Mathf.Clamp01(tipRadius / sphere));
        const int domeRings = 4;
        int previous = (rings.Count - 1) * stride;
        for (int k = 1; k <= domeRings; k++)
        {
            float angle = startAngle * (1f - k / (float)(domeRings + 1));
            float radius = sphere * Mathf.Sin(angle);
            float z = centre.z + sphere * Mathf.Cos(angle);
            float u = Mathf.Lerp(tipStart, 1f, 0.5f + 0.5f * k / domeRings);
            int start = vertices.Count;
            AddRing(vertices, normals, uvs, sides, radius, z, u, around => (new Vector3(around.x * radius, around.y * radius, z) - centre).normalized);
            AddBand(triangles, previous, start, sides);
            previous = start;
        }
        int apex = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, height));
        normals.Add(Vector3.forward);
        uvs.Add(new Vector2(1f, 0.5f));
        for (int s = 0; s < sides; s++)
            triangles.AddRange(new[] { previous + s, previous + s + 1, apex });

        // Flat butt cap, facing back.
        int butt = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, -config.CueLength));
        normals.Add(Vector3.back);
        uvs.Add(new Vector2(0.001f, 0.5f));
        for (int s = 0; s <= sides; s++)
        {
            float a = s / (float)sides * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(a) * config.CueButtRadius, Mathf.Sin(a) * config.CueButtRadius, -config.CueLength));
            normals.Add(Vector3.back);
            uvs.Add(new Vector2(0.001f, s / (float)sides));
        }
        for (int s = 0; s < sides; s++)
            triangles.AddRange(new[] { butt, butt + 2 + s, butt + 1 + s });

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    private static void AddRing(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, int sides, float radius, float z, float u,
        System.Func<Vector3, Vector3> normal)
    {
        for (int s = 0; s <= sides; s++)
        {
            float angle = s / (float)sides * Mathf.PI * 2f;
            var around = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            vertices.Add(new Vector3(around.x * radius, around.y * radius, z));
            normals.Add(normal(around));
            uvs.Add(new Vector2(u, s / (float)sides));
        }
    }

    // Quads between ring `a` (towards the butt) and ring `b` (towards the tip), clockwise from outside.
    private static void AddBand(List<int> triangles, int a, int b, int sides)
    {
        for (int s = 0; s < sides; s++)
            triangles.AddRange(new[] { a + s, a + s + 1, b + s, a + s + 1, b + s + 1, b + s });
    }
    #endregion

    #region Table
    // decorative: the same table for the club background, without balls, light or gameplay components.
    private static void BuildTable(MatchConfig config, bool decorative)
    {
        var geometry = new TableGeometry(config.ToPoolSettings());
        float w = geometry.HalfWidth;
        float l = geometry.HalfLength;

        using var scope = new TempObjectScope();
        var root = new GameObject("Table");

        // Cloth: one sheet out to the cushion backs, with round notches where the pockets are.
        GameObject felt = MeshObject("Felt", root.transform, SaveMesh(FeltMesh(geometry), FeltMeshPath), Material("Felt"));
        felt.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        // Cushions and jaws: one mesh with a K-66-like profile along every segment of TableGeometry.
        MeshObject("Cushions", root.transform, SaveMesh(CushionMesh(geometry), CushionMeshPath), Material("Rail"));

        // Wooden frame, assembled from pieces that stop around the pocket holes.
        var rails = new GameObject("Rails").transform;
        rails.SetParent(root.transform, false);
        float railCenterX = w + CushionDepth + RailWidth * 0.5f;
        float railCenterZ = l + CushionDepth + RailWidth * 0.5f;
        float outerLength = (l + CushionDepth + RailWidth) * 2f;
        foreach (float s in new[] { -1f, 1f })
        {
            BuildRail(rails, geometry, true, s, -outerLength * 0.5f, outerLength * 0.5f);
            BuildRail(rails, geometry, false, s, -(w + CushionDepth), w + CushionDepth);
        }

        // Apron and legs.
        float outerWidth = (w + CushionDepth + RailWidth) * 2f;
        Box("Apron", root.transform, Material("Wood"), new Vector3(0f, (RailBottom + ApronBottom) * 0.5f, 0f),
            new Vector3(outerWidth - 0.04f, RailBottom - ApronBottom, outerLength - 0.04f));
        float legHeight = config.TableHeight + ApronBottom;
        foreach (float sx in new[] { -1f, 1f })
        {
            foreach (float sz in new[] { -1f, 1f })
            {
                Box("Leg", root.transform, Material("Wood"),
                    new Vector3(sx * (outerWidth * 0.5f - LegSize), ApronBottom - legHeight * 0.5f, sz * (outerLength * 0.5f - LegSize)),
                    new Vector3(LegSize, legHeight, LegSize));
            }
        }

        // Pockets: a cup going down through the cloth notch, with a leather rim on the rail side.
        GameObject pockets = MeshObject("Pockets", root.transform, SaveMesh(PocketMesh(geometry), PocketMeshPath), Material("PocketBlack"));
        pockets.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        BuildDiamonds(root.transform, w, l, railCenterX, railCenterZ);

        if (decorative)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
            {
                if (child != root.transform)
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
            }
            PrefabUtility.SaveAsPrefabAsset(root, DecorTablePath);
            Object.DestroyImmediate(root);
            return;
        }

        // Ball container filled by TableView at runtime.
        var ballRoot = new GameObject("Balls").transform;
        ballRoot.SetParent(root.transform, false);

        BuildLamp(root.transform);

        var view = root.AddComponent<TableView>();
        SetField(view, "_config", config);
        SetField(view, "_ballPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(BallPath).GetComponent<BallView>());
        SetField(view, "_ballRoot", ballRoot);
        var serialized = new SerializedObject(view);
        SerializedProperty materials = serialized.FindProperty("_ballMaterials");
        materials.arraySize = BallGroups.BallCount;
        for (int i = 0; i < BallGroups.BallCount; i++)
            materials.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.BallMaterialPath(i));
        serialized.ApplyModifiedPropertiesWithoutUndo();

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child != root.transform && child != ballRoot && !child.IsChildOf(ballRoot) && child.GetComponent<Light>() == null)
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
        }

        PrefabUtility.SaveAsPrefabAsset(root, TablePath);
        Object.DestroyImmediate(root);
    }

    private static float HoleRadius(PocketInfo pocket) => pocket.CaptureRadius + _config.PocketHoleExtra;

    // Star-shaped outline seen from the table centre: for each direction, the cloth ends at the bed edge or
    // where the ray first enters a pocket hole, whichever is nearer. Triangle fan from the centre.
    private static Mesh FeltMesh(TableGeometry geometry)
    {
        float halfX = geometry.HalfWidth + CushionDepth;
        float halfZ = geometry.HalfLength + CushionDepth;
        const int samples = 1440;
        var vertices = new List<Vector3> { Vector3.zero };
        var uvs = new List<Vector2> { Vector2.zero };
        var triangles = new List<int>();

        for (int i = 0; i < samples; i++)
        {
            float angle = i / (float)samples * Mathf.PI * 2f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float distance = Mathf.Min(
                Mathf.Abs(direction.x) > 1e-6f ? halfX / Mathf.Abs(direction.x) : float.MaxValue,
                Mathf.Abs(direction.y) > 1e-6f ? halfZ / Mathf.Abs(direction.y) : float.MaxValue);
            foreach (PocketInfo pocket in geometry.Pockets)
            {
                float radius = HoleRadius(pocket);
                float b = Vector2.Dot(-pocket.Center, direction);
                float c = pocket.Center.sqrMagnitude - radius * radius;
                float discriminant = b * b - c;
                if (discriminant < 0f)
                    continue;
                float entry = -b - Mathf.Sqrt(discriminant);
                if (entry > 0f && entry < distance)
                    distance = entry;
            }
            Vector2 point = direction * distance;
            vertices.Add(new Vector3(point.x, 0f, point.y));
            uvs.Add(point / FeltTile);
        }

        for (int i = 0; i < samples; i++)
        {
            int a = 1 + i;
            int b = 1 + (i + 1) % samples;
            triangles.AddRange(new[] { 0, b, a });
        }

        var mesh = new Mesh { name = "FeltMesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Per pocket: inward-facing cup wall from below the cloth up to the rail top (only its far side renders,
    // so the mouth stays open), a bottom, and a leather rim on top of the rail that skips the mouth.
    private static Mesh PocketMesh(TableGeometry geometry)
    {
        const int segments = 48;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        float top = RailTop + 0.003f;

        foreach (PocketInfo pocket in geometry.Pockets)
        {
            float radius = HoleRadius(pocket);
            var center = new Vector3(pocket.Center.x, 0f, pocket.Center.y);

            // Cup wall, normals pointing to the centre.
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                vertices.Add(center + offset + Vector3.up * top);
                vertices.Add(center + offset + Vector3.down * PocketDepth);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }

            // Bottom.
            int bottomCenter = vertices.Count;
            vertices.Add(center + Vector3.down * PocketDepth);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vertices.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius + Vector3.down * PocketDepth);
            }
            for (int i = 0; i < segments; i++)
                triangles.AddRange(new[] { bottomCenter, bottomCenter + 2 + i, bottomCenter + 1 + i });

            // Rim on the rail, leaving out the sector that faces the table.
            float mouthHalf = (pocket.IsCorner ? 45f : 62f) * Mathf.Deg2Rad;
            float mouthAngle = Mathf.Atan2(-pocket.Inward.y, -pocket.Inward.x);
            float from = mouthAngle + mouthHalf;
            float span = Mathf.PI * 2f - 2f * mouthHalf;
            int rimStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = from + i / (float)segments * span;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(center + direction * radius + Vector3.up * top);
                vertices.Add(center + direction * (radius + PocketRimWidth) + Vector3.up * top);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = rimStart + i * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
        }

        var mesh = new Mesh { name = "PocketMesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // One rail (long: along z at x = side * centre; short: along x). Full-width pieces between the holes;
    // where a hole cuts the inner edge, only the strip beyond the hole stays.
    private static void BuildRail(Transform parent, TableGeometry geometry, bool isLong, float side, float from, float to)
    {
        float inner = (isLong ? geometry.HalfWidth : geometry.HalfLength) + CushionDepth;
        float outer = inner + RailWidth;

        var cuts = new List<(float start, float end, float across)>();
        foreach (PocketInfo pocket in geometry.Pockets)
        {
            float along = isLong ? pocket.Center.y : pocket.Center.x;
            float across = (isLong ? pocket.Center.x : pocket.Center.y) * side;
            float radius = HoleRadius(pocket) + 0.004f;
            float depth = inner - across;
            if (Mathf.Abs(depth) >= radius)
                continue;
            float half = Mathf.Sqrt(radius * radius - depth * depth);
            cuts.Add((along - half, along + half, across + radius));
        }
        cuts.Sort((a, b) => a.start.CompareTo(b.start));

        float cursor = from;
        foreach (var cut in cuts)
        {
            float start = Mathf.Max(cut.start, from);
            float end = Mathf.Min(cut.end, to);
            if (end <= from || start >= to)
                continue;
            RailPiece(parent, isLong, side, cursor, start, inner, outer);
            RailPiece(parent, isLong, side, start, end, Mathf.Max(inner, cut.across), outer);
            cursor = end;
        }
        RailPiece(parent, isLong, side, cursor, to, inner, outer);
    }

    private static void RailPiece(Transform parent, bool isLong, float side, float from, float to, float inner, float outer)
    {
        if (to - from < 0.002f || outer - inner < 0.002f)
            return;
        float along = (from + to) * 0.5f;
        float across = (inner + outer) * 0.5f * side;
        float height = RailTop - RailBottom;
        float y = (RailTop + RailBottom) * 0.5f;
        Vector3 position = isLong ? new Vector3(across, y, along) : new Vector3(along, y, across);
        Vector3 size = isLong ? new Vector3(outer - inner, height, to - from) : new Vector3(to - from, height, outer - inner);
        Box(isLong ? "LongRail" : "ShortRail", parent, Material("Wood"), position, size);
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // Cushion cross-section, outward from the nose line: the face slants back under the nose (the ball touches
    // the nose at ~63% of its height), the top rises gently to the back, where the rail takes over.
    // Rails use CushionDepth, jaws JawThickness on the side away from the pocket opening. Flat-shaded.
    private static Mesh CushionMesh(TableGeometry geometry)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        foreach (CushionSegment segment in geometry.Cushions)
        {
            Vector2 along = segment.B - segment.A;
            float length = along.magnitude;
            if (length < 1e-5f)
                continue;
            Vector2 direction = along / length;
            Vector2 outward;
            float depth;
            if (segment.IsJaw)
            {
                Vector2 mid = (segment.A + segment.B) * 0.5f;
                outward = new Vector2(-direction.y, direction.x);
                if (Vector2.Dot(NearestPocket(geometry, mid) - mid, outward) > 0f)
                    outward = -outward;
                depth = JawThickness;
            }
            else
            {
                outward = Outward(segment);
                depth = CushionDepth;
            }

            // Profile corners (outward, height), front face first.
            var profile = new[]
            {
                new Vector2(Mathf.Min(_config.CushionUndercut, depth * 0.5f), 0f),
                new Vector2(0f, _config.CushionNoseHeight),
                new Vector2(depth, _config.CushionBackHeight),
                new Vector2(depth, 0f)
            };
            Vector3 Point(Vector2 basePoint, Vector2 corner)
            {
                Vector2 p = basePoint + outward * corner.x;
                return new Vector3(p.x, corner.y, p.y);
            }

            Vector2 middle = (segment.A + segment.B) * 0.5f;
            Vector3 interior = Point(middle, new Vector2(depth * 0.5f, _config.CushionNoseHeight * 0.4f));
            for (int k = 0; k < 3; k++)
            {
                Vector3 a = Point(segment.A, profile[k]);
                Vector3 b = Point(segment.A, profile[k + 1]);
                Vector3 c = Point(segment.B, profile[k]);
                Vector3 d = Point(segment.B, profile[k + 1]);
                AddQuad(vertices, triangles, a, b, c, d, interior);
            }
            foreach (bool start in new[] { true, false })
            {
                Vector2 basePoint = start ? segment.A : segment.B;
                int first = vertices.Count;
                foreach (Vector2 corner in profile)
                    vertices.Add(Point(basePoint, corner));
                var faceOut = new Vector3(direction.x, 0f, direction.y) * (start ? -1f : 1f);
                Vector3 normal = Vector3.Cross(vertices[first + 1] - vertices[first], vertices[first + 2] - vertices[first]);
                bool flip = Vector3.Dot(normal, faceOut) < 0f;
                triangles.AddRange(flip ? new[] { first, first + 2, first + 1, first, first + 3, first + 2 }
                                        : new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
        }

        var mesh = new Mesh { name = "CushionMesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Quad a-b (one end) to c-d (other end), wound so it faces away from a point inside the cushion.
    private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 interior)
    {
        int first = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        // Unity front faces: the side cross(b - a, c - a) points to. Cycle a -> b -> d -> c.
        Vector3 normal = Vector3.Cross(b - a, d - a);
        bool flip = Vector3.Dot(normal, (a + b + c + d) * 0.25f - interior) < 0f;
        triangles.AddRange(flip ? new[] { first, first + 3, first + 1, first, first + 2, first + 3 }
                                : new[] { first, first + 1, first + 3, first, first + 3, first + 2 });
    }

    // Rails are axis-aligned: outward is away from the table centre.
    private static Vector2 Outward(CushionSegment segment)
    {
        Vector2 mid = (segment.A + segment.B) * 0.5f;
        return Mathf.Abs(segment.A.x - segment.B.x) < 1e-4f ? new Vector2(Mathf.Sign(mid.x), 0f) : new Vector2(0f, Mathf.Sign(mid.y));
    }

    private static Vector2 NearestPocket(TableGeometry geometry, Vector2 point)
    {
        Vector2 best = geometry.Pockets[0].Center;
        foreach (PocketInfo pocket in geometry.Pockets)
        {
            if ((pocket.Center - point).sqrMagnitude < (best - point).sqrMagnitude)
                best = pocket.Center;
        }
        return best;
    }

    // Sights on the rails: every eighth of the length (minus the side pockets), every quarter of the width.
    private static void BuildDiamonds(Transform root, float w, float l, float railX, float railZ)
    {
        var parent = new GameObject("Diamonds").transform;
        parent.SetParent(root, false);
        for (int k = 1; k <= 7; k++)
        {
            if (k == 4)
                continue;
            float z = -l + k * l / 4f;
            foreach (float s in new[] { -1f, 1f })
                Diamond(parent, new Vector3(s * railX, RailTop + 0.0006f, z));
        }
        for (int k = 1; k <= 3; k++)
        {
            float x = -w + k * w / 2f;
            foreach (float s in new[] { -1f, 1f })
                Diamond(parent, new Vector3(x, RailTop + 0.0006f, s * railZ));
        }
    }

    private static void Diamond(Transform parent, Vector3 position)
    {
        GameObject quad = Primitive(PrimitiveType.Quad, "Diamond", parent, Material("Diamond"));
        quad.transform.localPosition = position;
        quad.transform.localRotation = Quaternion.Euler(90f, 45f, 0f);
        quad.transform.localScale = new Vector3(0.014f, 0.014f, 1f);
        quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // One warm spot above the table, the only shadow-casting light. No visible lamp body: every high camera
    // (shot follow, look around) would look straight through it.
    private static void BuildLamp(Transform root)
    {
        var lamp = new GameObject("Lamp").transform;
        lamp.SetParent(root, false);
        lamp.localPosition = new Vector3(0f, LampHeight, 0f);

        var lightObject = new GameObject("Spot");
        lightObject.transform.SetParent(lamp, false);
        lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.87f, 0.7f);
        light.intensity = 6.5f;
        light.range = 4f;
        light.spotAngle = 118f;
        light.innerSpotAngle = 70f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.8f;
        light.shadowBias = 0.02f;
        light.shadowNormalBias = 0.2f;
        var lightData = lightObject.AddComponent<UniversalAdditionalLightData>();
        // Read-only in the API; the tier is only settable through its serialized field.
        var lightSerialized = new SerializedObject(lightData);
        lightSerialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
        lightSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.6f);
        bloom.highQualityFiltering.Override(false);

        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.45f);

        ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
        color.contrast.Override(8f);
        color.saturation.Override(6f);

        EditorUtility.SetDirty(profile);
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
            return component;
        component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }
    #endregion

    #region Helpers
    private static Material Material(string name)
    {
        string path = GeneratedArtBuilder.MaterialPath(name);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Debug.LogError($"TablePrefabBuilder: missing material {path}");
        return null;
    }

    private static GameObject Box(string name, Transform parent, Material material, Vector3 position, Vector3 size)
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
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    public static void SetField(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"Builder: {target.GetType().Name} has no field {field}");
            return;
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    #endregion
}
