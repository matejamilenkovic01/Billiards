using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// House Rules card (gameplay redesign G5): six rows with icons. Opened automatically before the first match,
// from Pause -> RULES, and from a foul banner's "?", which highlights the rule that was broken.
public class RulesPopup : PopupBase
{
    [SerializeField] private Image[] _rowHighlights;   // indexed by RuleId - 1
    [SerializeField] private UIButton _closeButton;
    [SerializeField] private Color _highlightColor = new(0.96f, 0.77f, 0.27f, 0.35f);

    private void OnEnable() => _closeButton.Clicked += Hide;

    private void OnDisable() => _closeButton.Clicked -= Hide;

    protected override void OnShow(object payload)
    {
        RuleId focus = payload is RulesPopupData data ? data.Focus : RuleId.None;
        for (int i = 0; i < _rowHighlights.Length; i++)
        {
            Image row = _rowHighlights[i];
            row.DOKill();
            row.color = Color.clear;
            if ((int)focus - 1 != i)
                continue;
            // All six rows fit on the card, so no scrolling: the broken rule just lights up and breathes.
            row.color = _highlightColor;
            row.DOFade(_highlightColor.a * 0.35f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(row.gameObject);
        }
    }
}
