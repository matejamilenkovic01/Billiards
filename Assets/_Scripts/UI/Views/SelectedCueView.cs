using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Small card with the current cue and its overall rating; tapping it opens the inventory.
public class SelectedCueView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image _cueImage;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _rating;

    private void OnEnable() => GameEvents.OnProfileChanged += HandleProfileChanged;

    private void OnDisable() => GameEvents.OnProfileChanged -= HandleProfileChanged;

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        CueViewData cue = snapshot.SelectedCue;
        if (cue == null)
            return;

        _name.text = cue.Name;
        if (cue.Sprite != null)
            _cueImage.sprite = cue.Sprite;
        _rating.text = $"OVR {cue.Overall}";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameEvents.RaiseButtonClicked();
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * -0.06f, 0.25f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaiseCuesRequested();
    }
}
