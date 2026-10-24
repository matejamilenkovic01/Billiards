using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// The whole journey (v3 V12): 15 stages from Bronze III at the bottom to Grand Champion at the top, checkpoint
// flags at every division start, the venue and the bot's cue per division, YOU ARE HERE.
public class RankedRoadPopup : PopupBase
{
    [SerializeField] private RoadRowView[] _rows;     // bottom (Bronze III) first
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private UIButton _closeButton;

    private void OnEnable() => _closeButton.Clicked += Hide;

    private void OnDisable() => _closeButton.Clicked -= Hide;

    protected override void OnShow(object payload)
    {
        if (payload is not RankedRoadPopupData data)
            return;
        RoadViewData road = data.Road;
        for (int i = 0; i < _rows.Length; i++)
        {
            bool has = i < road.Stages.Count;
            _rows[i].gameObject.SetActive(has);
            if (has)
                _rows[i].Bind(road.Stages[i]);
        }

        // Scroll so the current stage sits in the middle (rows are laid out top = last).
        Canvas.ForceUpdateCanvases();
        int count = Mathf.Min(_rows.Length, road.Stages.Count);
        float fromTop = count > 1 ? (count - 1 - Mathf.Clamp(road.CurrentIndex, 0, count - 1)) / (float)(count - 1) : 0f;
        float target = 1f - fromTop;
        DOTween.Kill(this);
        _scroll.verticalNormalizedPosition = Mathf.Clamp01(target + (target > 0.5f ? 0.15f : -0.15f));
        DOTween.To(() => _scroll.verticalNormalizedPosition, v => _scroll.verticalNormalizedPosition = v, target, 0.6f)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this).SetLink(gameObject);
    }
}
