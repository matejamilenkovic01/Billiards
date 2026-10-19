using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

// One of the arrows at the ends of the fine aim wheel (gameplay redesign G2). A tap turns the aim by one
// small step; holding repeats it (after a short wait) and the shot controller speeds the steps up.
public class FineAimStepButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("+1 = right, -1 = left.")]
    [SerializeField] private int _direction = 1;

    private float _repeatRate = 12f;
    private float _repeatDelay = 0.3f;
    private bool _held;
    private float _nextRepeat;

    private void OnEnable() => GameEvents.OnMatchStarted += HandleMatchStarted;

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        _held = false;
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _repeatRate = info.FineAimRepeatRate;
        _repeatDelay = info.FineAimRepeatDelay;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _held = true;
        _nextRepeat = Time.unscaledTime + _repeatDelay;
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOScale(0.9f, 0.06f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaiseFineAimStep(_direction, false);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();

    public void OnPointerExit(PointerEventData eventData) => Release();

    private void Release()
    {
        if (!_held)
            return;
        _held = false;
        transform.DOKill();
        transform.DOScale(1f, 0.08f).SetUpdate(true).SetLink(gameObject);
    }

    private void Update()
    {
        if (!_held || Time.unscaledTime < _nextRepeat)
            return;
        _nextRepeat += 1f / Mathf.Max(1f, _repeatRate);
        GameEvents.RaiseFineAimStep(_direction, true);
    }
}
