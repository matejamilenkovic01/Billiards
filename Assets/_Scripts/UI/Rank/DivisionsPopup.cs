using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// DIVISIONS (v4 V11): the whole journey, Grand Champion at the top and Bronze at the bottom, one block per division
// with its stages, rule, venue and rewards. Opens on the player's block.
public class DivisionsPopup : PopupBase
{
    [SerializeField] private DivisionBlockView[] _blocks;     // Bronze first
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private UIButton _closeButton;

    private void OnEnable() => _closeButton.Clicked += Hide;

    private void OnDisable() => _closeButton.Clicked -= Hide;

    protected override void OnShow(object payload)
    {
        if (payload is not DivisionsPopupData data)
            return;
        DivisionsViewData view = data.Divisions;
        for (int i = 0; i < _blocks.Length; i++)
        {
            bool has = i < view.Blocks.Count;
            _blocks[i].gameObject.SetActive(has);
            if (has)
                _blocks[i].Bind(view.Blocks[i]);
        }

        // Blocks are laid out top = last: scroll so the current one sits in the middle.
        Canvas.ForceUpdateCanvases();
        int count = Mathf.Min(_blocks.Length, view.Blocks.Count);
        float fromTop = count > 1 ? (count - 1 - Mathf.Clamp(view.CurrentIndex, 0, count - 1)) / (float)(count - 1) : 0f;
        float target = 1f - fromTop;
        DOTween.Kill(this);
        _scroll.verticalNormalizedPosition = Mathf.Clamp01(target + (target > 0.5f ? 0.1f : -0.1f));
        DOTween.To(() => _scroll.verticalNormalizedPosition, v => _scroll.verticalNormalizedPosition = v, target, 0.6f)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this).SetLink(gameObject);
    }
}
