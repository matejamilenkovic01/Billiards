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
        transform.localScale = _restScale;
        transform.position = worldPosition;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        transform.DOKill();
        IsDropping = false;
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

    // Slides into the pocket, sinks below the cloth while shrinking a little, then disappears.
    public void PlayPocket(Vector3 pocketWorld, float drop, float shrink, float duration)
    {
        if (!gameObject.activeSelf)
            return;

        IsDropping = true;
        transform.DOKill();
        Vector3 target = new Vector3(pocketWorld.x, transform.position.y - drop, pocketWorld.z);
        DOTween.Sequence()
            .Append(transform.DOMove(target, duration).SetEase(Ease.InQuad))
            .Join(transform.DOScale(_restScale * shrink, duration))
            .OnComplete(() =>
            {
                IsDropping = false;
                gameObject.SetActive(false);
            })
            .SetLink(gameObject);
    }
}
