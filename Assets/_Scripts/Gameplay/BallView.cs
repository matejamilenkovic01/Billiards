using DG.Tweening;
using UnityEngine;

public class BallView : MonoBehaviour
{
    [SerializeField] private MeshRenderer _renderer;

    private float _lastRollAngle;
    private Vector3 _restScale;

    public int Number { get; private set; }
    public bool IsDropping { get; private set; }

    private void Awake() => _restScale = transform.localScale;

    public void Setup(int number, Material material)
    {
        Number = number;
        name = "Ball_" + number;
        _renderer.sharedMaterial = material;
        // Numbers mostly face up in the rack, each one turned a little differently.
        transform.rotation = Quaternion.Euler(-90f + Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
    }

    // Position from the simulation; rotation from the distance rolled since the last frame.
    public void Apply(in BallState state, Vector3 worldPosition)
    {
        if (IsDropping)
            return;

        transform.position = worldPosition;
        float delta = state.RollAngle - _lastRollAngle;
        _lastRollAngle = state.RollAngle;
        if (delta > 0f && state.RollAxis.sqrMagnitude > 0f)
            transform.rotation = Quaternion.AngleAxis(delta * Mathf.Rad2Deg, state.RollAxis) * transform.rotation;
    }

    public void SyncRollAngle(float rollAngle) => _lastRollAngle = rollAngle;

    public void Show(Vector3 worldPosition)
    {
        transform.DOKill();
        IsDropping = false;
        InPocket = false;
        transform.localScale = _restScale;
        transform.position = worldPosition;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        transform.DOKill();
        IsDropping = false;
        InPocket = false;
        gameObject.SetActive(false);
    }

    // Ball in hand: held in the air at `world` (no tween fighting it).
    public void HoldAt(Vector3 world)
    {
        transform.DOKill();
        transform.position = world;
    }

    // ...and set down on the cloth.
    public void SetDown(Vector3 restWorld, float duration)
    {
        transform.DOKill();
        transform.DOMove(restWorld, duration).SetEase(Ease.InQuad).SetLink(gameObject);
    }

    // In a pocket until the turn ends (v3 V7): no contact shadow, not synced to the simulation.
    public bool InPocket { get; private set; }

    // Carries on over the rim at its entry speed, falls to the bottom of the liner and bounces once; stays there.
    public void PlayPocket(Vector3 pocketCenterBottom, Vector3 entryVelocity, float holeRadius, float ballRadius, float fallTime, float bounce)
    {
        if (!gameObject.activeSelf)
            return;

        IsDropping = true;
        InPocket = true;
        transform.DOKill();
        Vector3 start = transform.position;
        Vector3 flat = new Vector3(entryVelocity.x, 0f, entryVelocity.z);
        // Rest a little towards the far side of the hole, never through its wall.
        Vector3 offset = Vector3.ClampMagnitude(new Vector3(start.x - pocketCenterBottom.x, 0f, start.z - pocketCenterBottom.z) + flat * fallTime, Mathf.Max(0f, holeRadius - ballRadius * 1.3f) * 0.6f);
        Vector3 rest = pocketCenterBottom + offset + Vector3.up * ballRadius;
        Vector3 over = Vector3.Lerp(start, new Vector3(rest.x, start.y, rest.z), 0.5f);
        DOTween.Sequence()
            .Append(transform.DOMove(over, fallTime * 0.35f).SetEase(Ease.Linear))
            .Append(transform.DOMove(rest, fallTime * 0.65f).SetEase(Ease.InQuad))
            .Append(transform.DOMoveY(rest.y + bounce, 0.08f).SetEase(Ease.OutQuad))
            .Append(transform.DOMoveY(rest.y, 0.08f).SetEase(Ease.InQuad))
            .OnComplete(() => IsDropping = false)
            .SetLink(gameObject);
    }
}
