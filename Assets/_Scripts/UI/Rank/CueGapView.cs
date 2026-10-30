using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Under CUES on the menu (v4 V12/V13): when the division's cue is better than the best the player owns, a card
// "Gold opponents use the Pro Cue" with the coin bar, TRY and a pointer to the objectives that pay in a loss;
// otherwise one small line, "Next cue: Pro Cue · 412 / 1000". Tapping either opens the cues.
public class CueGapView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject _card;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _amount;
    [SerializeField] private TMP_Text _hint;
    [SerializeField] private UIButton _tryButton;
    [SerializeField] private GameObject _line;
    [SerializeField] private TMP_Text _lineText;

    private string _cueId;

    private void OnEnable()
    {
        GameEvents.OnProfileChanged += HandleProfileChanged;
        _tryButton.Clicked += HandleTry;
    }

    private void OnDisable()
    {
        GameEvents.OnProfileChanged -= HandleProfileChanged;
        _tryButton.Clicked -= HandleTry;
    }

    private void HandleTry()
    {
        if (!string.IsNullOrEmpty(_cueId))
            GameEvents.RaiseCueTryRequested(_cueId);
    }

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        CueGapData gap = snapshot.CueGap;
        CueViewData next = snapshot.NextCue;
        _card.SetActive(gap != null);
        _line.SetActive(gap == null && next != null);
        if (gap != null)
        {
            _cueId = gap.CueId;
            _title.text = $"{gap.DivisionName} opponents use the {gap.CueName}";
            _fill.fillAmount = gap.Price > 0 ? Mathf.Clamp01(gap.Coins / (float)gap.Price) : 1f;
            _amount.text = $"{Mathf.Min(gap.Coins, gap.Price)} / {gap.Price}";
            _hint.text = gap.PlayObjectivesLeft > 0 ? "Objectives with a coin pay even when you lose" : "Win and lose, coins add up";
        }
        else if (next != null)
        {
            _lineText.text = next.CanAfford ? $"Ready to buy: {next.Name}" : $"Next cue: {next.Name}  ·  {Mathf.Min(snapshot.Coins, next.Price)} / {next.Price}";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameEvents.RaiseButtonClicked();
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * -0.03f, 0.25f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaiseCuesRequested();
    }
}
