using UnityEngine;

// Soft contact shadows under the balls (v3 V6): one mesh of up to 16 flat quads on the cloth, a radial blob each,
// rebuilt every frame from the ball views. Fixed buffers, no allocation; a hidden or dropping ball has none.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BallShadowRenderer : MonoBehaviour
{
    private const int MaxBalls = BallGroups.BallCount;

    [SerializeField] private float _sizeInRadii = 2.2f;
    [SerializeField] private float _alpha = 0.5f;
    [SerializeField] private float _lift = 0.0008f;

    private readonly Vector3[] _vertices = new Vector3[MaxBalls * 4];
    private readonly Color[] _colors = new Color[MaxBalls * 4];
    private Mesh _mesh;

    private void Awake()
    {
        _mesh = new Mesh { name = "BallShadows" };
        _mesh.MarkDynamic();
        var uvs = new Vector2[MaxBalls * 4];
        var indices = new int[MaxBalls * 6];
        for (int i = 0; i < MaxBalls; i++)
        {
            int v = i * 4;
            uvs[v] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(1f, 0f);
            uvs[v + 2] = new Vector2(0f, 1f);
            uvs[v + 3] = new Vector2(1f, 1f);
            int t = i * 6;
            indices[t] = v;
            indices[t + 1] = v + 2;
            indices[t + 2] = v + 1;
            indices[t + 3] = v + 1;
            indices[t + 4] = v + 2;
            indices[t + 5] = v + 3;
        }
        _mesh.vertices = _vertices;
        _mesh.uv = uvs;
        _mesh.colors = _colors;
        _mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20f);
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }

    // Each ball's shadow sits on the cloth under it; it fades as the ball lifts (held in hand) and is gone when
    // the ball is hidden or dropping into a pocket.
    public void UpdateShadows(BallView[] balls, Transform table, float ballRadius)
    {
        Vector3 up = table.up;
        Vector3 right = table.right * ballRadius * _sizeInRadii * 0.5f;
        Vector3 forward = table.forward * ballRadius * _sizeInRadii * 0.5f;
        for (int i = 0; i < MaxBalls; i++)
        {
            int v = i * 4;
            BallView ball = i < balls.Length ? balls[i] : null;
            bool visible = ball != null && ball.gameObject.activeInHierarchy && !ball.IsDropping;
            if (!visible)
            {
                _vertices[v] = _vertices[v + 1] = _vertices[v + 2] = _vertices[v + 3] = Vector3.zero;
                continue;
            }
            Vector3 center = ball.transform.position;
            float height = Vector3.Dot(center - table.position, up) - ballRadius;
            Vector3 onCloth = center - up * (height + ballRadius - _lift);
            float fade = Mathf.Clamp01(1f - height / (ballRadius * 2f));
            _vertices[v] = onCloth - right - forward;
            _vertices[v + 1] = onCloth + right - forward;
            _vertices[v + 2] = onCloth - right + forward;
            _vertices[v + 3] = onCloth + right + forward;
            var color = new Color(1f, 1f, 1f, _alpha * fade);
            _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = color;
        }
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
    }
}
