using DG.Tweening;
using UnityEngine;

// Aim guide on the cloth (implementation_definition_v3 V1): a thin line for the cue ball up to the first obstacle
// (3D ghost ball, cushion or pocket), then only the cue's arrow length: the hit ball (gold) and the cue ball after
// the contact or the cushion (white) as soft bands with slow chevrons and a rounded tip. An illegal target turns
// the line and ghost a muted red (no X, no band); a scratch turns the line and that pocket's rim orange. Also the
// intended line after a missed release, and the ball-in-hand markers. Geometry comes from AimGuideLayout.
public class AimGuideView : MonoBehaviour
{
    private const float Lift = 0.0025f;
    private const int RingSegments = 40;
    private const float ChevronSpacing = 0.05f;
    private const float DashTile = 0.012f;

    [SerializeField] private MatchConfig _config;
    [SerializeField] private TableView _table;
    [SerializeField] private Material _solidMaterial;
    [SerializeField] private Material _dashMaterial;
    [SerializeField] private Material _ghostMaterial;
    [SerializeField] private GuideBandRenderer _bands;
    [SerializeField] private GuideBandRenderer _line;
    [SerializeField] private Material _tipMaterial;
    [SerializeField] private Sprite _handSprite;

    private static readonly Color GhostColor = Color.white;
    private static readonly Color IllegalColor = new(1f, 0.25f, 0.2f, 0.95f);
    private static readonly Color ValidColor = new(0.35f, 1f, 0.45f, 0.95f);
    private static readonly Color MissColor = new(1f, 1f, 1f, 0.85f);
    private static readonly Color MissYellow = new(1f, 0.85f, 0.25f, 0.95f);
    private static readonly Color MissRed = new(1f, 0.3f, 0.25f, 0.95f);
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
    private static readonly int BodyAlphaId = Shader.PropertyToID("_BodyAlpha");

    private readonly AimGuideLayout _layout = new();
    private readonly Transform[] _tips = new Transform[AimGuideLayout.MaxBands];
    private readonly MeshRenderer[] _tipRenderers = new MeshRenderer[AimGuideLayout.MaxBands];
    private MaterialPropertyBlock _tipBlock;
    private Transform _ghost;
    private MeshRenderer _ghostRenderer;
    private float _ghostBodyAlpha = 0.28f;
    private int _illegalBall = -1;
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
        _tipBlock = new MaterialPropertyBlock();
        _ghost = Ghost("Ghost", out _ghostRenderer);
        if (_ghostMaterial != null && _ghostMaterial.HasProperty(BodyAlphaId))
            _ghostBodyAlpha = Mathf.Max(0.01f, _ghostMaterial.GetFloat(BodyAlphaId));
        for (int i = 0; i < _tips.Length; i++)
            _tips[i] = Tip("Tip" + i, out _tipRenderers[i]);
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
        // Chevrons flow slowly along the bands, away from the obstacle: a "live" guide.
        _chevrons.SetVector(MainTexStId, new Vector4(1f, 1f, -Time.unscaledTime * _config.GuideChevronSpeed / _bands.ChevronSpacing, 0f));
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
    public void ShowAim(in Prediction prediction, BallState[] balls, float arrowLength)
    {
        float radius = _config.BallRadius;
        _layout.Build(prediction, balls, radius, arrowLength);
        Color lineColor = _layout.Scratch ? _config.GuideScratchColor : _layout.Illegal ? _config.GuideIllegalColor : _config.GuideLineColor;

        _line.Begin();
        if (_layout.HasLine)
            _line.AddStrip(_layout.LinePoints, _layout.LineCount, float.MaxValue, _config.GuideLineWidth, lineColor, false, out _, out _);
        _line.End();

        // 2-3 chevrons per band, whatever the cue's arrow length.
        _bands.ChevronSpacing = arrowLength / Mathf.Max(1f, _config.GuideChevronsPerBand);
        _bands.Begin();
        float width = 2f * radius;
        float tipLength = Mathf.Min(_config.GuideTipLength, arrowLength * 0.45f);
        for (int i = 0; i < _tips.Length; i++)
        {
            if (i >= _layout.BandCount)
            {
                _tipRenderers[i].enabled = false;
                continue;
            }
            AimGuideLayout.Band band = _layout.Bands[i];
            Color fill = band.Kind == GuideBandKind.Object ? _config.GuideObjectColor
                : _layout.Illegal ? WithAlpha(_config.GuideIllegalColor, _config.GuideCueBandColor.a)
                : _layout.Scratch ? WithAlpha(_config.GuideScratchColor, _config.GuideCueBandColor.a)
                : _config.GuideCueBandColor;
            _bands.AddStrip(band.Points, band.Count, Mathf.Max(0f, band.Length - tipLength), width, fill, true, out Vector2 end, out Vector2 heading);
            SetTip(i, end, heading, width, Mathf.Min(tipLength, band.Length), fill);
        }
        _bands.End();

        bool ghost = _layout.HasGhost;
        if (ghost && _layout.Illegal && _illegalBall != _layout.HitBall)
        {
            // Aim just moved onto an illegal ball: the ghost swells once.
            _ghost.DOKill();
            _ghost.localScale = Vector3.one * radius * 2f;
            _ghost.DOPunchScale(Vector3.one * radius * 0.5f, _config.GuideIllegalPulseTime, 1, 0f).SetLink(_ghost.gameObject);
        }
        _illegalBall = ghost && _layout.Illegal ? _layout.HitBall : -1;
        Color ghostColor = _layout.Illegal ? WithAlpha(_config.GuideIllegalColor, _config.GuideIllegalGhostAlpha / _ghostBodyAlpha) : GhostColor;
        SetGhost(_ghost, _ghostRenderer, ghost, _layout.GhostPoint, ghostColor);

        _pocketGlow.enabled = _layout.Scratch;
        if (_layout.Scratch)
        {
            PocketInfo info = _table.Geometry.Pockets[NearestPocket(_layout.ScratchPoint)];
            SetRing(_pocketGlow, info.Center, info.CaptureRadius + 0.01f, _config.GuideScratchColor);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    public void HideAim()
    {
        _bands.Clear();
        _line.Clear();
        _ghostRenderer.enabled = false;
        _pocketGlow.enabled = false;
        _illegalBall = -1;
        for (int i = 0; i < _tipRenderers.Length; i++)
            _tipRenderers[i].enabled = false;
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

    private void SetGhost(Transform ghost, MeshRenderer renderer, bool visible, Vector2 point, Color color)
    {
        renderer.enabled = visible;
        if (!visible)
            return;
        ghost.position = _table.BallCenterWorld(point);
        _ghostBlock.SetColor(ColorId, color);
        renderer.SetPropertyBlock(_ghostBlock);
    }

    // The rounded arrow tip: a flat quad on the cloth, its base at `baseCenter`, pointing along `direction`.
    private void SetTip(int index, Vector2 baseCenter, Vector2 direction, float width, float length, Color color)
    {
        MeshRenderer renderer = _tipRenderers[index];
        renderer.enabled = length > 1e-4f;
        if (!renderer.enabled)
            return;
        Transform tip = _tips[index];
        Vector3 forward = _table.TableDirectionToWorld(direction);
        tip.position = _table.TableToWorld(baseCenter + direction.normalized * length * 0.5f, Lift);
        // The quad lies in its local XY plane: X across the band, Y along it, facing up.
        tip.rotation = Quaternion.LookRotation(-_table.transform.up, forward);
        tip.localScale = new Vector3(width, length, 1f);
        _tipBlock.SetColor(ColorId, color);
        renderer.SetPropertyBlock(_tipBlock);
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
    private Transform Tip(string name, out MeshRenderer renderer)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _tipMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return quad.transform;
    }

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
