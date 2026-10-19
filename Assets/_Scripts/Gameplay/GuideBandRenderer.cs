using UnityEngine;

// Flat guide bands on the cloth (gameplay redesign G9): one mesh of quads in world space, rebuilt every frame
// from table-plane points. Fixed capacity and index buffer, so a frame allocates nothing: unused quads are
// collapsed to a point. uv.x runs along a band in chevron tiles, uv.y across it (0..1), uv2.x turns chevrons on.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GuideBandRenderer : MonoBehaviour
{
    private const int MaxQuads = 24;
    private const int VertexCount = MaxQuads * 4;

    [SerializeField] private TableView _table;
    [SerializeField] private float _lift = 0.0025f;
    [SerializeField] private float _chevronSpacing = 0.05f;

    private readonly Vector3[] _vertices = new Vector3[VertexCount];
    private readonly Vector2[] _uvs = new Vector2[VertexCount];
    private readonly Vector2[] _flags = new Vector2[VertexCount];
    private readonly Color[] _colors = new Color[VertexCount];
    private Mesh _mesh;
    private MeshRenderer _renderer;
    private int _quads;
    private int _uploadedQuads;

    public MeshRenderer Renderer
    {
        get
        {
            Init();
            return _renderer;
        }
    }

    private void Awake() => Init();

    // Lazy as well: AimGuideView may hide the bands in its own Awake, before this one ran.
    private void Init()
    {
        if (_mesh != null)
            return;
        _renderer = GetComponent<MeshRenderer>();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _mesh = new Mesh { name = "GuideBands" };
        _mesh.MarkDynamic();
        var indices = new int[MaxQuads * 6];
        for (int q = 0; q < MaxQuads; q++)
        {
            int v = q * 4;
            int i = q * 6;
            indices[i] = v;
            indices[i + 1] = v + 2;
            indices[i + 2] = v + 1;
            indices[i + 3] = v + 1;
            indices[i + 4] = v + 2;
            indices[i + 5] = v + 3;
        }
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.uv2 = _flags;
        _mesh.colors = _colors;
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
        // The bands only ever lie on the table: generous fixed bounds, no per-frame recalculation.
        _mesh.bounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 20f));
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }

    public void Begin()
    {
        Init();
        _quads = 0;
    }

    // A straight band from `from` to `to` (table plane). Returns the chevron coordinate at its end, so a
    // following segment (after a cushion bounce) continues the same pattern.
    public float AddBand(Vector2 from, Vector2 to, float width, Color fill, bool chevrons, float uStart = 0f)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 1e-4f || _quads >= MaxQuads)
            return uStart;

        Vector2 along = delta / length;
        Vector2 side = new Vector2(-along.y, along.x) * (width * 0.5f);
        float uEnd = uStart + length / _chevronSpacing;
        // v = 0 on the left edge, 1 on the right.
        SetQuad(from + side, from - side, to + side, to - side,
            new Vector2(uStart, 0f), new Vector2(uStart, 1f), new Vector2(uEnd, 0f), new Vector2(uEnd, 1f),
            fill, chevrons);
        return uEnd;
    }

    // Arrowhead whose base sits at `baseCenter`, pointing along `direction`. Two halves, so the dark contour
    // runs along both slanted edges.
    public void AddArrowHead(Vector2 baseCenter, Vector2 direction, float baseWidth, float length, Color fill)
    {
        if (_quads + 2 > MaxQuads)
            return;
        Vector2 along = direction.normalized;
        Vector2 side = new Vector2(-along.y, along.x) * (baseWidth * 0.5f);
        Vector2 tip = baseCenter + along * length;
        SetQuad(baseCenter + side, baseCenter, tip, tip,
            new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f), fill, false);
        SetQuad(baseCenter, baseCenter - side, tip, tip,
            new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), fill, false);
    }

    // Uploads this frame's quads; the rest of the buffer is collapsed so it draws nothing.
    public void End()
    {
        for (int q = _quads; q < _uploadedQuads; q++)
        {
            int v = q * 4;
            _vertices[v] = _vertices[v + 1] = _vertices[v + 2] = _vertices[v + 3] = Vector3.zero;
        }
        _uploadedQuads = _quads;
        _mesh.SetVertices(_vertices);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetUVs(1, _flags);
        _mesh.SetColors(_colors);
        _renderer.enabled = _quads > 0;
    }

    public void Clear()
    {
        Begin();
        End();
    }

    private void SetQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD,
        Color fill, bool chevrons)
    {
        int v = _quads * 4;
        _vertices[v] = _table.TableToWorld(a, _lift);
        _vertices[v + 1] = _table.TableToWorld(b, _lift);
        _vertices[v + 2] = _table.TableToWorld(c, _lift);
        _vertices[v + 3] = _table.TableToWorld(d, _lift);
        _uvs[v] = uvA;
        _uvs[v + 1] = uvB;
        _uvs[v + 2] = uvC;
        _uvs[v + 3] = uvD;
        var flag = new Vector2(chevrons ? 1f : 0f, 0f);
        _flags[v] = _flags[v + 1] = _flags[v + 2] = _flags[v + 3] = flag;
        _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = fill;
        _quads++;
    }
}
