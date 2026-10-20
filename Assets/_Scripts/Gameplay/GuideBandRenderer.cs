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

    // Metres per chevron tile along the bands (the aim guide sets it so each band shows 2-3 chevrons).
    public float ChevronSpacing
    {
        get => _chevronSpacing;
        set => _chevronSpacing = Mathf.Max(1e-3f, value);
    }

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

    // A band along a polyline (table plane), cut off `maxLength` metres from its start. Neighbouring quads share
    // their edge (mitred at the joint), so a curved path has no overlapping, darker wedges. Returns the chevron
    // coordinate at its end; `end` and `endDirection` are where it stopped and which way it was heading.
    public float AddStrip(Vector2[] points, int count, float maxLength, float width, Color fill, bool chevrons,
        out Vector2 end, out Vector2 endDirection, float uStart = 0f)
    {
        end = count > 0 ? points[0] : Vector2.zero;
        endDirection = count > 1 ? (points[1] - points[0]).normalized : Vector2.up;
        float half = width * 0.5f;
        float travelled = 0f;
        float u = uStart;
        Vector2 previous = points[0];
        Vector2 previousSide = Side(points, count, 0) * half;
        for (int i = 1; i < count && travelled < maxLength && _quads < MaxQuads; i++)
        {
            Vector2 segment = points[i] - previous;
            float length = segment.magnitude;
            if (length < 1e-5f)
                continue;
            Vector2 next = points[i];
            Vector2 side = Side(points, count, i) * half;
            if (travelled + length > maxLength)
            {
                next = previous + segment * ((maxLength - travelled) / length);
                side = new Vector2(-segment.y, segment.x) / length * half;
                length = maxLength - travelled;
            }
            float uEnd = u + length / _chevronSpacing;
            SetQuad(previous + previousSide, previous - previousSide, next + side, next - side,
                new Vector2(u, 0f), new Vector2(u, 1f), new Vector2(uEnd, 0f), new Vector2(uEnd, 1f), fill, chevrons);
            travelled += length;
            u = uEnd;
            endDirection = segment / Mathf.Max(1e-5f, segment.magnitude);
            end = next;
            previous = next;
            previousSide = side;
        }
        return u;
    }

    // Left-pointing unit normal at point i, averaged over the segments on both sides (a mitre, capped).
    private static Vector2 Side(Vector2[] points, int count, int i)
    {
        Vector2 before = i > 0 ? (points[i] - points[i - 1]).normalized : Vector2.zero;
        Vector2 after = i < count - 1 ? (points[i + 1] - points[i]).normalized : Vector2.zero;
        Vector2 tangent = (before + after).sqrMagnitude > 1e-8f ? (before + after).normalized : (after.sqrMagnitude > 0f ? after : before);
        var normal = new Vector2(-tangent.y, tangent.x);
        Vector2 reference = after.sqrMagnitude > 0f ? after : before;
        float miter = Vector2.Dot(normal, new Vector2(-reference.y, reference.x));
        return normal / Mathf.Max(0.5f, miter);
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
