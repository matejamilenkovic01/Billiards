using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// House Rules card (gameplay redesign G5): six rows with icons. Opened automatically before the first match,
// from Pause -> RULES, and from a foul banner's "?", which highlights the rule that was broken.
public class RulesPopup : PopupBase
{
    [SerializeField] private Image[] _rowHighlights;   // indexed by RuleId - 1
    [SerializeField] private UIButton _closeButton;
    [SerializeField] private Color _highlightColor = new(0.96f, 0.77f, 0.27f, 0.35f);
    [Tooltip("v4 V4: the division's 8-ball rule, its text and icon (any pocket / called / blocked).")]
    [SerializeField] private GameObject _divisionRow;
    [SerializeField] private TMP_Text _divisionText;
    [SerializeField] private Image _divisionIcon;
    [SerializeField] private Sprite _anyPocketIcon;
    [SerializeField] private Sprite _calledPocketIcon;
    [SerializeField] private Sprite _blockedPocketIcon;

    private void OnEnable() => _closeButton.Clicked += Hide;

    private void OnDisable() => _closeButton.Clicked -= Hide;

    protected override void OnShow(object payload)
    {
        var data = payload as RulesPopupData;
        RuleId focus = data != null ? data.Focus : RuleId.None;
        bool division = data != null && !string.IsNullOrEmpty(data.DivisionRule);
        _divisionRow.SetActive(division);
        if (division)
        {
            _divisionText.text = data.DivisionRule;
            _divisionIcon.sprite = data.Rules switch
            {
                RuleSetId.Standard => _anyPocketIcon,
                RuleSetId.BlockedPocket => _blockedPocketIcon,
                _ => _calledPocketIcon
            };
        }
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
