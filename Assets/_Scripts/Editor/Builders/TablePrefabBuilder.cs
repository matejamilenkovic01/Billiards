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
    public const string RailsMeshPath = Folder + "/RailsMesh.asset";
    public const string PocketLipMeshPath = Folder + "/PocketLipMesh.asset";
    public const string PocketCapMeshPath = Folder + "/PocketCapMesh.asset";

    // Visual sizes around the play area (metres, relative to the cloth surface). The cushion and rail sizes live
    // in MatchConfig: the cue (G6) and the aim camera clear exactly these shapes.
    private static MatchConfig _config;
    private static float CushionDepth => _config.CushionDepth;
    private static float JawThickness => _config.JawThickness;
    private static float RailWidth => _config.RailWidth;
    private static float RailTop => _config.RailTop;
    private const float RailBottom = -0.1f;
    // The liner's depth (MatchConfig.PocketDepth): a dropped ball rests at the bottom until the turn ends.
    private static float PocketDepth => _config.PocketDepth;
    private const float PocketRimWidth = 0.022f;
    private const float FeltTile = 0.3f;
    private const float ApronBottom = -0.24f;
    private const float LegSize = 0.13f;
    // v3 V6 pockets: the cloth rolls over the hole edge (radius PocketLip), a leather funnel narrows to LinerBottom
    // of the hole radius, a metal cap sits on the rail around each hole.
    private const float PocketLip = 0.008f;
    private const int LipSegments = 7;
    private const float LinerBottom = 0.85f;
    private const float CapGap = 0.004f;
    private const float CapWidth = 0.032f;
    private const float CapHeight = 0.0045f;
    // Rail top edges: rounded towards the cushion (small) and the outside (larger).
    private const float RailInnerRound = 0.007f;
    private const float RailOuterRound = 0.014f;
    // TV-style lighting (V6): one spot high above the table (the only shadow caster), and a visible canopy with an
    // emissive panel; the canopy casts no shadow and is invisible from above (panel faces down).
    private const float LightHeight = 3.0f;
    private const float CanopyHeight = 1.4f;
    private static readonly Vector2 CanopySize = new(1.35f, 2.5f);

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
        SetField(view, "_chalkPuff", ChalkPuff(cue.transform));
        PrefabUtility.SaveAsPrefabAsset(cue, CuePath);
        Object.DestroyImmediate(cue);
    }

    // A small puff of blue-white chalk where the tip meets the ball on a PERFECT release (v3 V7, G4): 8-12 soft
    // specks, world space, a third of a second.
    private static ParticleSystem ChalkPuff(Transform cue)
    {
        var go = new GameObject("ChalkPuff");
        go.transform.SetParent(cue, false);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.009f);
        main.startColor = new Color(0.82f, 0.9f, 1f, 0.85f);
        main.gravityModifier = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 16;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.004f;
        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Material("ChalkParticle");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return particles;
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
        // Covered in the same cloth, a little darker (VenueView tints it).
        GameObject cushions = MeshObject("Cushions", root.transform, SaveMesh(CushionMesh(geometry), CushionMeshPath), Material("Cushion"));

        // Wooden frame: pieces that stop around the pocket holes, one mesh with rounded top edges, flush with the
        // cushion backs (RailTop ~ CushionBackHeight, no step).
        float railCenterX = w + CushionDepth + RailWidth * 0.5f;
        float railCenterZ = l + CushionDepth + RailWidth * 0.5f;
        float outerLength = (l + CushionDepth + RailWidth) * 2f;
        _railPieces.Clear();
        foreach (float s in new[] { -1f, 1f })
        {
            BuildRail(geometry, true, s, -outerLength * 0.5f, outerLength * 0.5f);
            BuildRail(geometry, false, s, -(w + CushionDepth), w + CushionDepth);
        }
        GameObject rails = MeshObject("Rails", root.transform, SaveMesh(RailsMesh(), RailsMeshPath), Material("Wood"));

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

        // Pockets: cloth rolling over the edge, a leather funnel, a metal cap on the rail.
        GameObject lips = MeshObject("PocketLips", root.transform, SaveMesh(PocketLipMesh(geometry), PocketLipMeshPath), Material("Felt"));
        lips.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameObject liners = MeshObject("PocketLiners", root.transform, SaveMesh(PocketLinerMesh(geometry), PocketMeshPath), Material("PocketLiner"));
        liners.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameObject caps = MeshObject("PocketCaps", root.transform, SaveMesh(PocketCapMesh(geometry), PocketCapMeshPath), Material("Brass"));

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

        Light tableLight = BuildLamp(root.transform, out MeshRenderer lampPanel);

        var shadowObject = new GameObject("BallShadows", typeof(MeshFilter), typeof(MeshRenderer));
        shadowObject.transform.SetParent(root.transform, false);
        var shadowRenderer = shadowObject.GetComponent<MeshRenderer>();
        shadowRenderer.sharedMaterial = Material("BallShadow");
        shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
        var shadows = shadowObject.AddComponent<BallShadowRenderer>();

        var venue = root.AddComponent<VenueView>();
        SetRenderers(venue, "_feltRenderers", felt.GetComponent<MeshRenderer>(), lips.GetComponent<MeshRenderer>());
        SetRenderers(venue, "_cushionRenderers", cushions.GetComponent<MeshRenderer>());
        SetRenderers(venue, "_capRenderers", caps.GetComponent<MeshRenderer>());
        SetRenderers(venue, "_woodRenderers", rails.GetComponent<MeshRenderer>());
        SetField(venue, "_tableLight", tableLight);
        SetField(venue, "_lampPanel", lampPanel);

        var view = root.AddComponent<TableView>();
        SetField(view, "_shadows", shadows);
        SetField(view, "_glowMaterial", Material("GuideSolid"));
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
                float radius = HoleRadius(pocket) + PocketLip;
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

    private struct RailPiece
    {
        public bool IsLong;
        public float Side, From, To, Inner, Outer;
    }

    private static readonly List<RailPiece> _railPieces = new();

    // One rail (long: along z at x = side * centre; short: along x). Full-width pieces between the holes;
    // where a hole cuts the inner edge, only the strip beyond the hole stays.
    private static void BuildRail(TableGeometry geometry, bool isLong, float side, float from, float to)
    {
        float inner = (isLong ? geometry.HalfWidth : geometry.HalfLength) + CushionDepth;
        float outer = inner + RailWidth;

        var cuts = new List<(float start, float end, float across)>();
        foreach (PocketInfo pocket in geometry.Pockets)
        {
            float along = isLong ? pocket.Center.y : pocket.Center.x;
            float across = (isLong ? pocket.Center.x : pocket.Center.y) * side;
            float radius = HoleRadius(pocket) + 0.004f;
            // Widest chord of the hole across the whole rail width (a hole centred beyond the inner edge is wider
            // further in), so no piece of rail reaches into the hole.
            float depth = across < inner ? inner - across : across > outer ? across - outer : 0f;
            if (depth >= radius)
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
            AddRailPiece(isLong, side, cursor, start, inner, outer);
            AddRailPiece(isLong, side, start, end, Mathf.Max(inner, cut.across), outer);
            cursor = end;
        }
        AddRailPiece(isLong, side, cursor, to, inner, outer);
    }

    private static void AddRailPiece(bool isLong, float side, float from, float to, float inner, float outer)
    {
        if (to - from < 0.002f || outer - inner < 0.002f)
            return;
        _railPieces.Add(new RailPiece { IsLong = isLong, Side = side, From = from, To = to, Inner = inner, Outer = outer });
    }

    // Every rail piece extruded from one cross-section: straight inner face, rounded inner top edge, flat top,
    // rounded outer edge, outer face; flat ends. Wood UVs run along the piece.
    private static Mesh RailsMesh()
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        const float uvScale = 0.6f;
        foreach (RailPiece piece in _railPieces)
        {
            float innerRound = Mathf.Min(RailInnerRound, (piece.Outer - piece.Inner) * 0.3f);
            float outerRound = Mathf.Min(RailOuterRound, (piece.Outer - piece.Inner) * 0.3f);
            // (across outward, height, normal across, normal up)
            var profile = new List<Vector4> { new(piece.Inner, RailBottom, -1f, 0f), new(piece.Inner, RailTop - innerRound, -1f, 0f) };
            for (int k = 1; k <= 3; k++)
            {
                float a = k / 3f * Mathf.PI * 0.5f;
                profile.Add(new Vector4(piece.Inner + innerRound * (1f - Mathf.Cos(a)), RailTop - innerRound * (1f - Mathf.Sin(a)), -Mathf.Cos(a), Mathf.Sin(a)));
            }
            profile.Add(new Vector4(piece.Outer - outerRound, RailTop, 0f, 1f));
            for (int k = 1; k <= 3; k++)
            {
                float a = k / 3f * Mathf.PI * 0.5f;
                profile.Add(new Vector4(piece.Outer - outerRound * (1f - Mathf.Sin(a)), RailTop - outerRound * (1f - Mathf.Cos(a)), Mathf.Sin(a), Mathf.Cos(a)));
            }
            profile.Add(new Vector4(piece.Outer, RailBottom, 1f, 0f));

            Vector3 Point(float along, float across, float y) =>
                piece.IsLong ? new Vector3(across * piece.Side, y, along) : new Vector3(along, y, across * piece.Side);
            Vector3 Normal(float nAcross, float nUp) =>
                piece.IsLong ? new Vector3(nAcross * piece.Side, nUp, 0f) : new Vector3(0f, nUp, nAcross * piece.Side);

            int start = vertices.Count;
            float arc = 0f;
            for (int k = 0; k < profile.Count; k++)
            {
                if (k > 0)
                    arc += Vector2.Distance(profile[k], profile[k - 1]);
                foreach (float along in new[] { piece.From, piece.To })
                {
                    vertices.Add(Point(along, profile[k].x, profile[k].y));
                    normals.Add(Normal(profile[k].z, profile[k].w).normalized);
                    uvs.Add(new Vector2(along / uvScale, arc / uvScale));
                }
            }
            for (int k = 0; k < profile.Count - 1; k++)
            {
                int a = start + k * 2;
                AddOriented(triangles, vertices, normals, a, a + 1, a + 2);
                AddOriented(triangles, vertices, normals, a + 1, a + 3, a + 2);
            }
            // Ends: a fan over the cross-section at each end.
            foreach (bool atTo in new[] { false, true })
            {
                float along = atTo ? piece.To : piece.From;
                Vector3 endNormal = (piece.IsLong ? Vector3.forward : Vector3.right) * (atTo ? 1f : -1f);
                int first = vertices.Count;
                foreach (Vector4 corner in profile)
                {
                    vertices.Add(Point(along, corner.x, corner.y));
                    normals.Add(endNormal);
                    uvs.Add(new Vector2(corner.x / uvScale, corner.y / uvScale));
                }
                for (int k = 1; k < profile.Count - 1; k++)
                    AddOriented(triangles, vertices, normals, first, first + k, first + k + 1);
            }
        }

        var mesh = new Mesh { name = "RailsMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // Adds triangle a-b-c facing the way its vertex normals point (Unity front face: cross(b - a, c - a)).
    private static void AddOriented(List<int> triangles, List<Vector3> vertices, List<Vector3> normals, int a, int b, int c)
    {
        Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) >= 0f)
            triangles.AddRange(new[] { a, b, c });
        else
            triangles.AddRange(new[] { a, c, b });
    }

    // A profile (radius, height, normal radial, normal up, v) turned around `center` from angle a0 to a1.
    private static void Lathe(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
        Vector3 center, IReadOnlyList<Vector4> profile, IReadOnlyList<float> v, float a0, float a1, int segments, float uRepeats)
    {
        int start = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(a0, a1, i / (float)segments);
            var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int k = 0; k < profile.Count; k++)
            {
                vertices.Add(center + radial * profile[k].x + Vector3.up * profile[k].y);
                normals.Add((radial * profile[k].z + Vector3.up * profile[k].w).normalized);
                uvs.Add(new Vector2(i / (float)segments * uRepeats, v[k]));
            }
        }
        int stride = profile.Count;
        for (int i = 0; i < segments; i++)
        {
            for (int k = 0; k < stride - 1; k++)
            {
                int a = start + i * stride + k;
                AddOriented(triangles, vertices, normals, a, a + stride, a + 1);
                AddOriented(triangles, vertices, normals, a + 1, a + stride, a + stride + 1);
            }
        }
    }

    private static Mesh FinishMesh(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
    {
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // The cloth rolls over each hole's edge: a quarter round from the cloth (at HoleRadius + PocketLip) down to
    // HoleRadius, PocketLip below. Same cloth tiling as the bed (planar UVs).
    private static Mesh PocketLipMesh(TableGeometry geometry)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        foreach (PocketInfo pocket in geometry.Pockets)
        {
            float radius = HoleRadius(pocket);
            var profile = new List<Vector4>();
            var v = new List<float>();
            for (int k = 0; k <= LipSegments; k++)
            {
                float phi = Mathf.Lerp(90f, 180f, k / (float)LipSegments) * Mathf.Deg2Rad;
                profile.Add(new Vector4(radius + PocketLip + PocketLip * Mathf.Cos(phi), -PocketLip + PocketLip * Mathf.Sin(phi), Mathf.Cos(phi), Mathf.Sin(phi)));
                v.Add(0f);
            }
            // Only where the hole opens to the cloth; the rail side gets the leather collar (PocketLinerMesh).
            MouthSector(pocket, out float mouth, out float half);
            int first = vertices.Count;
            Lathe(vertices, normals, uvs, triangles, new Vector3(pocket.Center.x, 0f, pocket.Center.y), profile, v, mouth - half, mouth + half, 24, 1f);
            for (int i = first; i < vertices.Count; i++)
                uvs[i] = new Vector2(vertices[i].x, vertices[i].z) / FeltTile;
        }
        return FinishMesh("PocketLipMesh", vertices, normals, uvs, triangles);
    }

    // Leather funnel under each hole: from the bottom of the cloth lip it narrows to LinerBottom of the hole
    // radius at PocketDepth, with a rounded bottom. Seen from inside (normals face the axis). v = 1 at the top,
    // 0 at the bottom: the liner texture darkens with depth.
    private static Mesh PocketLinerMesh(TableGeometry geometry)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        foreach (PocketInfo pocket in geometry.Pockets)
        {
            float radius = HoleRadius(pocket);
            float bottomRadius = radius * LinerBottom;
            float round = bottomRadius * 0.55f;
            float wallBottom = -(PocketDepth - round);
            // The wall narrows going down, so its inner face looks towards the axis and a little up.
            float slope = (radius - bottomRadius) / Mathf.Max(1e-4f, -PocketLip - wallBottom);
            var profile = new List<Vector4>();
            for (int k = 0; k <= 4; k++)
            {
                float t = k / 4f;
                profile.Add(new Vector4(Mathf.Lerp(radius, bottomRadius, t), Mathf.Lerp(-PocketLip, wallBottom, t), -1f, slope));
            }
            for (int k = 1; k <= 5; k++)
            {
                float theta = k / 5f * Mathf.PI * 0.5f;
                profile.Add(new Vector4(bottomRadius - round + round * Mathf.Cos(theta), wallBottom - round * Mathf.Sin(theta), -Mathf.Cos(theta), Mathf.Sin(theta)));
            }
            profile.Add(new Vector4(0f, -PocketDepth, 0f, 1f));
            var v = new List<float>();
            float total = 0f;
            var lengths = new List<float> { 0f };
            for (int k = 1; k < profile.Count; k++)
            {
                total += Vector2.Distance(profile[k], profile[k - 1]);
                lengths.Add(total);
            }
            foreach (float length in lengths)
                v.Add(1f - length / total);
            var center = new Vector3(pocket.Center.x, 0f, pocket.Center.y);
            Lathe(vertices, normals, uvs, triangles, center, profile, v, 0f, Mathf.PI * 2f, 48, 2f);

            // Collar: on the rail side the leather goes up to the rail top, hiding the cut rails and cushion ends.
            MouthSector(pocket, out float mouth, out float half);
            var collar = new List<Vector4> { new(radius, RailTop + 0.002f, -1f, 0f), new(radius, -PocketLip, -1f, 0f) };
            var collarV = new List<float> { 1f, 0.92f };
            Lathe(vertices, normals, uvs, triangles, center, collar, collarV, mouth + half, mouth + Mathf.PI * 2f - half, 40, 2f);
        }
        return FinishMesh("PocketLinerMesh", vertices, normals, uvs, triangles);
    }

    // The side of a hole that opens to the cloth: centre angle (towards the table) and half-width, radians.
    private static void MouthSector(PocketInfo pocket, out float mouth, out float half)
    {
        mouth = Mathf.Atan2(-pocket.Inward.y, -pocket.Inward.x);
        half = (pocket.IsCorner ? 50f : 64f) * Mathf.Deg2Rad;
    }

    // A rounded metal cap on the rail around each hole, leaving out the side that opens to the table.
    private static Mesh PocketCapMesh(TableGeometry geometry)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        foreach (PocketInfo pocket in geometry.Pockets)
        {
            float inner = HoleRadius(pocket) + CapGap;
            var profile = new List<Vector4> { new(inner, RailTop - 0.006f, -1f, 0f) };
            var v = new List<float> { 0f };
            for (int k = 0; k <= 6; k++)
            {
                float s = k / 6f;
                float angle = s * Mathf.PI;
                // Half-round across the cap: normal from the cap's centre line.
                profile.Add(new Vector4(Mathf.Lerp(inner, inner + CapWidth, s), RailTop + CapHeight * Mathf.Sin(angle), -Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f));
                v.Add(s);
            }
            profile.Add(new Vector4(inner + CapWidth, RailTop - 0.006f, 1f, 0f));
            v.Add(1f);
            MouthSector(pocket, out float mouth, out float mouthHalf);
            Lathe(vertices, normals, uvs, triangles, new Vector3(pocket.Center.x, 0f, pocket.Center.y), profile, v,
                mouth + mouthHalf, mouth + Mathf.PI * 2f - mouthHalf, 48, 1f);
        }
        return FinishMesh("PocketCapMesh", vertices, normals, uvs, triangles);
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
        foreach (CushionSegment physical in geometry.Cushions)
        {
            // Drawn only outside the pocket holes (v3 V6): nothing sticks out over an opening. Physics unchanged.
            if (!ClipToHoles(geometry, physical, out CushionSegment segment))
                continue;
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

    // The part of a cushion segment outside every hole (the hole plus the cloth lip); false if nothing is left.
    private static bool ClipToHoles(TableGeometry geometry, CushionSegment segment, out CushionSegment clipped)
    {
        clipped = segment;
        foreach (PocketInfo pocket in geometry.Pockets)
        {
            float radius = HoleRadius(pocket) + PocketLip;
            bool aInside = (clipped.A - pocket.Center).sqrMagnitude < radius * radius;
            bool bInside = (clipped.B - pocket.Center).sqrMagnitude < radius * radius;
            if (aInside && bInside)
                return false;
            if (!aInside && !bInside)
                continue;
            // Move the inside end out to where the segment crosses the circle.
            Vector2 inside = aInside ? clipped.A : clipped.B;
            Vector2 outside = aInside ? clipped.B : clipped.A;
            Vector2 d = outside - inside;
            Vector2 f = inside - pocket.Center;
            float a = d.sqrMagnitude, b = 2f * Vector2.Dot(f, d), c = f.sqrMagnitude - radius * radius;
            float t = (-b + Mathf.Sqrt(Mathf.Max(0f, b * b - 4f * a * c))) / (2f * a);
            Vector2 cross = inside + d * Mathf.Clamp01(t);
            if (aInside)
                clipped.A = cross;
            else
                clipped.B = cross;
        }
        return (clipped.B - clipped.A).sqrMagnitude > 1e-6f;
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
                Diamond(parent, new Vector3(s * railX, RailTop + 0.0006f, z), true);
        }
        for (int k = 1; k <= 3; k++)
        {
            float x = -w + k * w / 2f;
            foreach (float s in new[] { -1f, 1f })
                Diamond(parent, new Vector3(x, RailTop + 0.0006f, s * railZ), false);
        }
    }

    private static void Diamond(Transform parent, Vector3 position, bool onLongRail)
    {
        GameObject quad = Primitive(PrimitiveType.Quad, "Diamond", parent, Material("Diamond"));
        quad.transform.localPosition = position;
        // Oval mother-of-pearl inlays, long along the rail.
        quad.transform.localRotation = Quaternion.Euler(90f, onLongRail ? 90f : 0f, 0f);
        quad.transform.localScale = new Vector3(0.02f, 0.011f, 1f);
        quad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // TV-tournament lighting (v3 V6): one neutral-warm spot high above the table, wide enough inside its inner cone
    // that the cloth is lit evenly; the only shadow-casting light. A canopy with an emissive panel hangs lower:
    // seen from the side it reads as the lamp, it casts no shadow, and from above you see through it (the panel
    // faces down, the frame strips face out).
    private static Light BuildLamp(Transform root, out MeshRenderer panel)
    {
        var lamp = new GameObject("Lamp").transform;
        lamp.SetParent(root, false);

        var lightObject = new GameObject("TableLight");
        lightObject.transform.SetParent(lamp, false);
        lightObject.transform.localPosition = new Vector3(0f, LightHeight, 0f);
        lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.9f, 0.8f);
        light.intensity = 32f;
        light.range = 7f;
        light.spotAngle = 78f;
        light.innerSpotAngle = 60f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.75f;
        light.shadowBias = 0.02f;
        light.shadowNormalBias = 0.25f;
        var lightData = lightObject.AddComponent<UniversalAdditionalLightData>();
        // Read-only in the API; the tier is only settable through its serialized field.
        var lightSerialized = new SerializedObject(lightData);
        lightSerialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
        lightSerialized.ApplyModifiedPropertiesWithoutUndo();

        var canopy = new GameObject("Canopy").transform;
        canopy.SetParent(lamp, false);
        canopy.localPosition = new Vector3(0f, CanopyHeight, 0f);
        GameObject panelObject = Primitive(PrimitiveType.Quad, "Panel", canopy, Material("LampPanel"));
        // A quad's front faces its local -Z: point it down.
        panelObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        panelObject.transform.localScale = new Vector3(CanopySize.x, CanopySize.y, 1f);
        panel = panelObject.GetComponent<MeshRenderer>();
        const float stripHeight = 0.07f;
        foreach (float s in new[] { -1f, 1f })
        {
            Strip(canopy, new Vector3(s * CanopySize.x * 0.5f, stripHeight * 0.5f, 0f), Quaternion.Euler(0f, s * -90f, 0f), new Vector2(CanopySize.y, stripHeight));
            Strip(canopy, new Vector3(0f, stripHeight * 0.5f, s * CanopySize.y * 0.5f), Quaternion.Euler(0f, s > 0f ? 180f : 0f, 0f), new Vector2(CanopySize.x, stripHeight));
        }
        foreach (Renderer renderer in canopy.GetComponentsInChildren<Renderer>())
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        return light;
    }

    private static void Strip(Transform parent, Vector3 position, Quaternion facingOut, Vector2 size)
    {
        GameObject strip = Primitive(PrimitiveType.Quad, "Frame", parent, Material("LampFrame"));
        strip.transform.localPosition = position;
        // Front (-Z) faces outward.
        strip.transform.localRotation = facingOut * Quaternion.Euler(0f, 180f, 0f);
        strip.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    private static void SetRenderers(Object target, string field, params Renderer[] renderers)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        property.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        // v3 V6: HDR + tone mapping; bloom only on emission above the threshold; a light vignette.
        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.3f);
        bloom.scatter.Override(0.6f);
        bloom.highQualityFiltering.Override(false);

        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.2f);
        vignette.smoothness.Override(0.45f);

        Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
        color.postExposure.Override(0.2f);
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
