using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Row of pocketed balls in the order they went down. Each one flies in from its pocket's screen position.
public class PocketTrayView : MonoBehaviour
{
    private const float FlyDuration = 0.4f;

    [SerializeField] private Image[] _slots;
    [SerializeField] private Image _flyer;
    [SerializeField] private Sprite[] _ballSprites;

    private readonly List<int> _order = new();
    private readonly List<Image> _flyers = new();

    private void Awake()
    {
        _flyer.gameObject.SetActive(false);
        Refresh();
    }

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnBallPocketedOnScreen += HandlePocketed;
        GameEvents.OnTurnStarted += HandleTurnStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnBallPocketedOnScreen -= HandlePocketed;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _order.Clear();
        Refresh();
    }

    private void HandlePocketed(int number, Vector2 screenPosition)
    {
        if (number <= 0 || number >= _ballSprites.Length || _order.Contains(number) || _order.Count >= _slots.Length)
            return;

        int slot = _order.Count;
        _order.Add(number);
        Image target = _slots[slot];
        target.enabled = false;

        Image flyer = GetFlyer();
        flyer.sprite = _ballSprites[number];
        var rect = (RectTransform)flyer.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, screenPosition, null, out Vector2 start);
        rect.anchoredPosition = start;
        rect.localScale = Vector3.one * 1.4f;
        Vector2 end = ((RectTransform)rect.parent).InverseTransformPoint(target.transform.position);
        flyer.gameObject.SetActive(true);
        DOTween.Sequence()
            .Append(rect.DOAnchorPos(end, FlyDuration).SetEase(Ease.OutCubic))
            .Join(rect.DOScale(1f, FlyDuration))
            .OnComplete(() =>
            {
                flyer.gameObject.SetActive(false);
                Refresh();
            })
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    // The rules may undo a pocketing (the 8 re-spotted after a break): drop what isn't down any more.
    private void HandleTurnStarted(TurnInfo info)
    {
        if (_order.RemoveAll(number => !info.Pocketed[number]) > 0)
            Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            bool filled = i < _order.Count;
            _slots[i].enabled = filled;
            if (filled)
                _slots[i].sprite = _ballSprites[_order[i]];
        }
    }

    private Image GetFlyer()
    {
        foreach (Image flyer in _flyers)
        {
            if (!flyer.gameObject.activeSelf)
                return flyer;
        }
        Image created = Instantiate(_flyer, _flyer.transform.parent);
        _flyers.Add(created);
        return created;
    }
}
