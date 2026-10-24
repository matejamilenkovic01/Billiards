using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "NEXT CUE · Pro Cue  412 / 900" (v3 V12): what the coins are for. Tapping opens the cues.
public class NextCueView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject _content;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _amount;
    [SerializeField] private Image _background;
    [SerializeField] private Color _normal = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color _ready = new(0.13f, 0.38f, 0.2f, 0.85f);

    private void OnEnable() => GameEvents.OnProfileChanged += HandleProfileChanged;

    private void OnDisable() => GameEvents.OnProfileChanged -= HandleProfileChanged;

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        CueViewData next = snapshot.NextCue;
        _content.SetActive(next != null);
        if (next == null)
            return;
        bool ready = next.CanAfford;
        _label.text = ready ? $"READY TO BUY · {next.Name}" : $"NEXT CUE · {next.Name}";
        _fill.fillAmount = next.Price > 0 ? Mathf.Clamp01(snapshot.Coins / (float)next.Price) : 1f;
        _amount.text = $"{Mathf.Min(snapshot.Coins, next.Price)} / {next.Price}";
        _background.color = ready ? _ready : _normal;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_content.activeSelf)
            return;
        GameEvents.RaiseButtonClicked();
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * -0.04f, 0.25f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaiseCuesRequested();
    }
}
