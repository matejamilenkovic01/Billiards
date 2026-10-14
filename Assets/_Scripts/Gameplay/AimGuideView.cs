using UnityEngine;

// Aim guide on the cloth: cue ball path (with dots drifting towards the target), ghost ball ring,
// a short object ball line (length depends on the cue), red ring + cross for an illegal target,
// the intended line after a missed release, and the ball-in-hand markers.
public class AimGuideView : MonoBehaviour
{
    private const float Lift = 0.0025f;
    private const int RingSegments = 40;
    private const float DotsSpeed = 0.35f;
    private const float DotsSpacing = 0.05f;

    [SerializeField] private MatchConfig _config;
    [SerializeField] private TableView _table;
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private Material _solidMaterial;
    [SerializeField] private Sprite _handSprite;

    private static readonly Color PathColor = new(1f, 1f, 1f, 0.8f);
    private static readonly Color ObjectColor = new(1f, 1f, 1f, 0.95f);
    private static readonly Color IllegalColor = new(1f, 0.25f, 0.2f, 0.95f);
    private static readonly Color ScratchColor = new(1f, 0.55f, 0.2f, 0.9f);
    private static readonly Color ValidColor = new(0.35f, 1f, 0.45f, 0.95f);
    private static readonly Color MissColor = new(1f, 1f, 1f, 0.35f);

    private LineRenderer _path;
    private LineRenderer _bounce;
    private LineRenderer _ring;
    private LineRenderer _objectLine;
    private LineRenderer _secondRing;
    private LineRenderer _secondObjectLine;
    private LineRenderer _crossA;
    private LineRenderer _crossB;
    private LineRenderer _miss;
    private LineRenderer _placeRing;
    private LineRenderer _headString;
    private GameObject _kitchen;
    private SpriteRenderer _hand;
    private MaterialPropertyBlock _dots;
    private float _missTimer;

    private void Awake()
    {
        _dots = new MaterialPropertyBlock();
        _path = Line("Path", _lineMaterial, 2);
        _bounce = Line("Bounce", _lineMaterial, 2);
        _ring = Line("GhostRing", _solidMaterial, RingSegments, true);
        _objectLine = Line("ObjectLine", _solidMaterial, 2);
        _secondRing = Line("SecondRing", _solidMaterial, RingSegments, true);
        _secondObjectLine = Line("SecondObjectLine", _solidMaterial, 2);
        _crossA = Line("CrossA", _solidMaterial, 2);
        _crossB = Line("CrossB", _solidMaterial, 2);
        _miss = Line("Miss", _solidMaterial, 2);
        _placeRing = Line("PlaceRing", _solidMaterial, RingSegments, true);
        _headString = Line("HeadString", _solidMaterial, 2);
        _headString.startColor = _headString.endColor = new Color(1f, 1f, 1f, 0.45f);

        _kitchen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _kitchen.name = "Kitchen";
        Destroy(_kitchen.GetComponent<Collider>());
        _kitchen.transform.SetParent(transform, false);
        var kitchenRenderer = _kitchen.GetComponent<MeshRenderer>();
        kitchenRenderer.sharedMaterial = _solidMaterial;
        kitchenRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", new Color(1f, 1f, 0.9f, 0.08f));
        kitchenRenderer.SetPropertyBlock(block);

        var handObject = new GameObject("Hand");
        handObject.transform.SetParent(transform, false);
        _hand = handObject.AddComponent<SpriteRenderer>();
        _hand.sprite = _handSprite;
        _hand.color = new Color(1f, 1f, 1f, 0.95f);

        HideAim();
        HidePlacement();
        _miss.enabled = false;
    }

    private void Update()
    {
        // Dots drift along the path towards the target: a "live" line.
        _dots.SetVector("_MainTex_ST", new Vector4(1f, 1f, -Time.unscaledTime * DotsSpeed / DotsSpacing, 0f));
        _path.SetPropertyBlock(_dots);
        _bounce.SetPropertyBlock(_dots);

        if (_miss.enabled)
        {
            _missTimer -= Time.deltaTime;
            Color color = MissColor;
            color.a *= Mathf.Clamp01(_missTimer / 0.25f);
            _miss.startColor = _miss.endColor = color;
            if (_missTimer <= 0f)
                _miss.enabled = false;
        }

        if (_placeRing.enabled)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f;
            _placeRing.transform.localScale = Vector3.one * pulse;
            Camera main = Camera.main;
            if (main != null)
                _hand.transform.rotation = main.transform.rotation;
        }
    }

    #region Aim
    public void ShowAim(in Prediction prediction, float objectGuideLength)
    {
        float radius = _config.BallRadius;
        bool scratch = prediction.Hit == PredictionHit.Pocket;
        Color pathColor = scratch ? ScratchColor : PathColor;
        SetSegment(_path, prediction.Start, prediction.Point, pathColor);

        _bounce.enabled = prediction.HasBounce;
        if (prediction.HasBounce)
            SetSegment(_bounce, prediction.Point, prediction.SecondPoint, prediction.SecondHit == PredictionHit.Pocket ? ScratchColor : PathColor);

        bool hitBall = prediction.Hit == PredictionHit.Ball;
        Color hitColor = prediction.IsLegal ? ObjectColor : IllegalColor;
        _ring.enabled = hitBall;
        _objectLine.enabled = hitBall && prediction.IsLegal;
        _crossA.enabled = _crossB.enabled = hitBall && !prediction.IsLegal;
        if (hitBall)
        {
            SetRing(_ring, prediction.Point, radius, hitColor);
            if (prediction.IsLegal)
                SetObjectLine(_objectLine, prediction, objectGuideLength, hitColor);
            else
                SetCross(prediction.Point, radius * 0.6f);
        }

        bool secondBall = prediction.HasBounce && prediction.SecondHit == PredictionHit.Ball;
        _secondRing.enabled = secondBall;
        _secondObjectLine.enabled = secondBall && prediction.SecondIsLegal;
        if (secondBall)
        {
            Color secondColor = prediction.SecondIsLegal ? ObjectColor : IllegalColor;
            SetRing(_secondRing, prediction.SecondPoint, radius, secondColor);
            if (prediction.SecondIsLegal)
            {
                Vector2 objectStart = prediction.SecondPoint + prediction.SecondObjectDir * 2f * radius;
                SetSegment(_secondObjectLine, objectStart, objectStart + prediction.SecondObjectDir * objectGuideLength, secondColor);
            }
        }
    }

    public void HideAim()
    {
        _path.enabled = _bounce.enabled = _ring.enabled = _objectLine.enabled = false;
        _secondRing.enabled = _secondObjectLine.enabled = _crossA.enabled = _crossB.enabled = false;
    }

    // After a missed release the intended line stays faintly visible next to the real path for a moment.
    public void ShowIntended(Vector2 from, Vector2 to)
    {
        SetSegment(_miss, from, to, MissColor);
        _missTimer = _config.MissGhostDuration;
    }

    private void SetObjectLine(LineRenderer line, in Prediction prediction, float length, Color color)
    {
        Vector2 objectCenter = prediction.Point + prediction.ObjectDir * 2f * _config.BallRadius;
        SetSegment(line, objectCenter, objectCenter + prediction.ObjectDir * length, color);
    }

    private void SetCross(Vector2 center, float size)
    {
        SetSegment(_crossA, center + new Vector2(-size, -size), center + new Vector2(size, size), IllegalColor);
        SetSegment(_crossB, center + new Vector2(-size, size), center + new Vector2(size, -size), IllegalColor);
    }
    #endregion

    #region Ball in hand
    public void ShowPlacement(Vector2 cueBall, bool valid, bool kitchenOnly)
    {
        _placeRing.enabled = true;
        _placeRing.transform.position = _table.TableToWorld(cueBall, Lift);
        SetRingLocal(_placeRing, _config.BallRadius * 1.6f, valid ? ValidColor : IllegalColor);

        _hand.enabled = true;
        _hand.transform.position = _table.TableToWorld(cueBall, _config.BallRadius * 2f + 0.06f);
        _hand.transform.localScale = Vector3.one * 0.07f / Mathf.Max(0.01f, _hand.sprite != null ? _hand.sprite.bounds.size.x : 1f);

        _kitchen.SetActive(kitchenOnly);
        _headString.enabled = kitchenOnly;
        if (kitchenOnly)
        {
            TableGeometry geometry = _table.Geometry;
            float length = geometry.HeadStringZ + geometry.HalfLength;
            _kitchen.transform.position = _table.TableToWorld(new Vector2(0f, -geometry.HalfLength + length * 0.5f), 0.0015f);
            _kitchen.transform.rotation = _table.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
            _kitchen.transform.localScale = new Vector3(geometry.HalfWidth * 2f, length, 1f);
            SetSegment(_headString, new Vector2(-geometry.HalfWidth, geometry.HeadStringZ), new Vector2(geometry.HalfWidth, geometry.HeadStringZ), _headString.startColor);
        }
    }

    public void HidePlacement()
    {
        _placeRing.enabled = false;
        _hand.enabled = false;
        _kitchen.SetActive(false);
        _headString.enabled = false;
    }
    #endregion

    #region Line helpers
    private LineRenderer Line(string name, Material material, int points, bool loop = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.positionCount = points;
        line.loop = loop;
        line.useWorldSpace = !loop;
        line.widthMultiplier = _config.GuideWidth;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Tile;
        line.textureScale = new Vector2(1f / DotsSpacing, 1f);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        // Lines lie flat on the cloth: their width is measured in the transform's XY plane, facing up.
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return line;
    }

    private void SetSegment(LineRenderer line, Vector2 from, Vector2 to, Color color)
    {
        line.enabled = true;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, _table.TableToWorld(from, Lift));
        line.SetPosition(1, _table.TableToWorld(to, Lift));
        line.startColor = line.endColor = color;
    }

    private void SetRing(LineRenderer line, Vector2 center, float radius, Color color)
    {
        line.useWorldSpace = true;
        line.positionCount = RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i / (float)RingSegments * Mathf.PI * 2f;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            line.SetPosition(i, _table.TableToWorld(point, Lift));
        }
        line.startColor = line.endColor = color;
    }

    // Ring in local space so it can pulse by scaling its transform.
    private void SetRingLocal(LineRenderer line, float radius, Color color)
    {
        line.useWorldSpace = false;
        line.positionCount = RingSegments;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i / (float)RingSegments * Mathf.PI * 2f;
            // The transform is rotated 90 degrees around X, so local XY is the cloth plane.
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        line.startColor = line.endColor = color;
    }
    #endregion
}
