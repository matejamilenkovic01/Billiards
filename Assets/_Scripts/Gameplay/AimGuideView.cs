using UnityEngine;

// Aim guide on the cloth (gameplay redesign G9): the cue ball's path as a 2R-wide band with a dark contour and
// chevrons flowing to the target (it shows the whole corridor the ball sweeps), a 3D ghost ball where the
// cue ball will be at contact, the hit ball's direction as a gold band with an arrowhead, the cue ball's
// tangent line after contact (dashed), red for an illegal target, orange when the cue ball would drop
// (that pocket's rim lights up), the intended line after a missed release, and the ball-in-hand markers.
public class AimGuideView : MonoBehaviour
{
    private const float Lift = 0.0025f;
    private const int RingSegments = 40;
    private const float ChevronSpeed = 0.35f;
    private const float ChevronSpacing = 0.05f;
    private const float DashTile = 0.012f;

    [SerializeField] private MatchConfig _config;
    [SerializeField] private TableView _table;
    [SerializeField] private Material _solidMaterial;
    [SerializeField] private Material _dashMaterial;
    [SerializeField] private Material _ghostMaterial;
    [SerializeField] private GuideBandRenderer _bands;
    [SerializeField] private Sprite _handSprite;

    private static readonly Color CueFill = new(1f, 1f, 1f, 0.18f);
    private static readonly Color ObjectFill = new(1f, 0.835f, 0.29f, 0.45f);   // #FFD54A
    private static readonly Color IllegalFill = new(1f, 0.25f, 0.2f, 0.42f);
    private static readonly Color ScratchFill = new(1f, 0.55f, 0.2f, 0.38f);
    private static readonly Color GhostColor = Color.white;
    private static readonly Color IllegalColor = new(1f, 0.25f, 0.2f, 0.95f);
    private static readonly Color ScratchColor = new(1f, 0.55f, 0.2f, 0.95f);
    private static readonly Color TangentColor = new(1f, 1f, 1f, 0.5f);
    private static readonly Color ValidColor = new(0.35f, 1f, 0.45f, 0.95f);
    private static readonly Color MissColor = new(1f, 1f, 1f, 0.85f);
    private static readonly Color MissYellow = new(1f, 0.85f, 0.25f, 0.95f);
    private static readonly Color MissRed = new(1f, 0.3f, 0.25f, 0.95f);
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

    private Transform _ghost;
    private Transform _secondGhost;
    private MeshRenderer _ghostRenderer;
    private MeshRenderer _secondGhostRenderer;
    private LineRenderer _tangent;
    private LineRenderer _crossA;
    private LineRenderer _crossB;
    private LineRenderer _pocketGlow;
    private LineRenderer _miss;
    private LineRenderer _missActual;
    private Color _missActualColor;
    private LineRenderer _placeRing;
    private LineRenderer _headString;
    private GameObject _kitchen;
    private SpriteRenderer _hand;
    private MaterialPropertyBlock _chevrons;
    private MaterialPropertyBlock _ghostBlock;
    private float _missTimer;

    private void Awake()
    {
        _chevrons = new MaterialPropertyBlock();
        _ghostBlock = new MaterialPropertyBlock();
        _ghost = Ghost("Ghost", out _ghostRenderer);
        _secondGhost = Ghost("SecondGhost", out _secondGhostRenderer);
        _tangent = Line("Tangent", _dashMaterial, 2);
        _tangent.widthMultiplier = _config.GuideWidth * 0.6f;
        _tangent.textureScale = new Vector2(1f / DashTile, 1f);
        _crossA = Line("CrossA", _solidMaterial, 2);
        _crossB = Line("CrossB", _solidMaterial, 2);
        _crossA.alignment = _crossB.alignment = LineAlignment.View;
        _crossA.widthMultiplier = _crossB.widthMultiplier = _config.GuideWidth * 1.2f;
        _pocketGlow = Line("PocketGlow", _solidMaterial, RingSegments, true);
        _pocketGlow.widthMultiplier = _config.GuideWidth * 2f;
        _miss = Line("Miss", _dashMaterial, 2);
        _miss.textureScale = new Vector2(1f / DashTile, 1f);
        _missActual = Line("MissActual", _solidMaterial, 2);
        _miss.widthMultiplier = _missActual.widthMultiplier = _config.GuideWidth * 1.2f;
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
        block.SetColor(ColorId, new Color(1f, 1f, 0.9f, 0.08f));
        kitchenRenderer.SetPropertyBlock(block);

        var handObject = new GameObject("Hand");
        handObject.transform.SetParent(transform, false);
        _hand = handObject.AddComponent<SpriteRenderer>();
        _hand.sprite = _handSprite;
        _hand.color = new Color(1f, 1f, 1f, 0.95f);

        HideAim();
        HidePlacement();
        _miss.enabled = _missActual.enabled = false;
    }

    private void Update()
    {
        // Chevrons flow along the bands towards the target: a "live" guide.
        _chevrons.SetVector(MainTexStId, new Vector4(1f, 1f, -Time.unscaledTime * ChevronSpeed / ChevronSpacing, 0f));
        _bands.Renderer.SetPropertyBlock(_chevrons);

        if (_miss.enabled)
        {
            _missTimer -= Time.deltaTime;
            float fade = Mathf.Clamp01(_missTimer / 0.25f);
            Color intended = MissColor;
            intended.a *= fade;
            _miss.startColor = _miss.endColor = intended;
            Color actual = _missActualColor;
            actual.a *= fade;
            _missActual.startColor = _missActual.endColor = actual;
            if (_missTimer <= 0f)
                _miss.enabled = _missActual.enabled = false;
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
        float width = 2f * radius;
        bool hitBall = prediction.Hit == PredictionHit.Ball;
        bool secondBall = prediction.HasBounce && prediction.SecondHit == PredictionHit.Ball;
        bool scratch = prediction.Hit == PredictionHit.Pocket || (prediction.HasBounce && prediction.SecondHit == PredictionHit.Pocket);
        bool illegal = (hitBall && !prediction.IsLegal) || (secondBall && !prediction.SecondIsLegal);
        Color pathFill = scratch ? ScratchFill : illegal ? IllegalFill : CueFill;

        _bands.Begin();
        // The cue ball's band starts at its front edge, not under it, and ends at the ghost ball.
        Vector2 toFirst = prediction.Point - prediction.Start;
        float u = 0f;
        if (toFirst.magnitude > radius)
            u = _bands.AddBand(prediction.Start + toFirst.normalized * radius, prediction.Point, width, pathFill, true);
        if (prediction.HasBounce)
            _bands.AddBand(prediction.Point, prediction.SecondPoint, width, pathFill, true, u);

        if (hitBall && prediction.IsLegal)
            AddObjectBand(prediction.Point, prediction.ObjectDir, objectGuideLength);
        if (secondBall && prediction.SecondIsLegal)
            AddObjectBand(prediction.SecondPoint, prediction.SecondObjectDir, objectGuideLength);
        _bands.End();

        SetGhost(_ghost, _ghostRenderer, hitBall, prediction.Point, prediction.IsLegal);
        SetGhost(_secondGhost, _secondGhostRenderer, secondBall, prediction.SecondPoint, prediction.SecondIsLegal);

        // Where the cue ball heads after a cut (not shown on a full hit).
        bool tangent = hitBall && prediction.IsLegal && prediction.CueDeflectDir.sqrMagnitude > 0.5f;
        _tangent.enabled = tangent;
        if (tangent)
        {
            Vector2 from = prediction.Point + prediction.CueDeflectDir * radius;
            SetSegment(_tangent, from, from + prediction.CueDeflectDir * 3f * radius, TangentColor);
        }

        bool crossFirst = hitBall && !prediction.IsLegal;
        bool crossSecond = !crossFirst && secondBall && !prediction.SecondIsLegal;
        _crossA.enabled = _crossB.enabled = crossFirst || crossSecond;
        if (crossFirst || crossSecond)
            SetCross(_table.BallCenterWorld(crossFirst ? prediction.Point : prediction.SecondPoint), radius * 0.6f);

        _pocketGlow.enabled = scratch;
        if (scratch)
        {
            Vector2 drop = prediction.Hit == PredictionHit.Pocket ? prediction.Point : prediction.SecondPoint;
            int pocket = NearestPocket(drop);
            PocketInfo info = _table.Geometry.Pockets[pocket];
            SetRing(_pocketGlow, info.Center, info.CaptureRadius + 0.01f, ScratchColor);
        }
    }

    public void HideAim()
    {
        _bands.Clear();
        _ghostRenderer.enabled = _secondGhostRenderer.enabled = false;
        _tangent.enabled = _crossA.enabled = _crossB.enabled = _pocketGlow.enabled = false;
    }

    // After a yellow or red release (G11): the intended line (dashed white) and the real direction (solid, in
    // the zone's colour) from the cue ball, for MissGhostDuration. The player sees why it missed.
    public void ShowMissReplay(Vector2 from, Vector2 intendedEnd, Vector2 actualEnd, ReleaseZone zone)
    {
        SetSegment(_miss, from, intendedEnd, MissColor);
        _missActualColor = zone == ReleaseZone.Yellow ? MissYellow : MissRed;
        SetSegment(_missActual, from, actualEnd, _missActualColor);
        _missTimer = _config.MissGhostDuration;
    }

    // Starts at the hit ball's far edge (so none of it hides under the ball) and ends in an arrowhead.
    private void AddObjectBand(Vector2 ghost, Vector2 direction, float length)
    {
        float width = 2f * _config.BallRadius;
        Vector2 start = ghost + direction * 3f * _config.BallRadius;
        float head = Mathf.Min(width * 0.8f, length * 0.45f);
        Vector2 headBase = start + direction * (length - head);
        _bands.AddBand(start, headBase, width, ObjectFill, true);
        _bands.AddArrowHead(headBase, direction, width * 1.6f, head, ObjectFill);
    }

    private void SetGhost(Transform ghost, MeshRenderer renderer, bool visible, Vector2 point, bool legal)
    {
        renderer.enabled = visible;
        if (!visible)
            return;
        ghost.position = _table.BallCenterWorld(point);
        _ghostBlock.SetColor(ColorId, legal ? GhostColor : IllegalColor);
        renderer.SetPropertyBlock(_ghostBlock);
    }

    // An X facing the camera, drawn through the ghost ball.
    private void SetCross(Vector3 center, float size)
    {
        Camera main = Camera.main;
        Transform view = main != null ? main.transform : transform;
        Vector3 a = (view.right + view.up) * size;
        Vector3 b = (view.right - view.up) * size;
        SetWorldSegment(_crossA, center - a, center + a, IllegalColor);
        SetWorldSegment(_crossB, center - b, center + b, IllegalColor);
    }

    private int NearestPocket(Vector2 point)
    {
        PocketInfo[] pockets = _table.Geometry.Pockets;
        int best = 0;
        for (int i = 1; i < pockets.Length; i++)
        {
            if ((pockets[i].Center - point).sqrMagnitude < (pockets[best].Center - point).sqrMagnitude)
                best = i;
        }
        return best;
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
    private Transform Ghost(string name, out MeshRenderer renderer)
    {
        GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ghost.name = name;
        Destroy(ghost.GetComponent<Collider>());
        ghost.transform.SetParent(transform, false);
        ghost.transform.localScale = Vector3.one * _config.BallRadius * 2f;
        renderer = ghost.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _ghostMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ghost.transform;
    }

    private static void SetWorldSegment(LineRenderer line, Vector3 from, Vector3 to, Color color)
    {
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startColor = line.endColor = color;
    }

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
        line.textureScale = new Vector2(1f / ChevronSpacing, 1f);
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
