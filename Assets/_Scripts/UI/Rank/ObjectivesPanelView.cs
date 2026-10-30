using TMPro;
using UnityEngine;

// The three visible objectives on the menu (v4 V12): finished ones first, a claimed one is replaced by the next;
// ALL › opens every set. While the cue gap card is up, objectives that pay in a loss carry a coin.
public class ObjectivesPanelView : MonoBehaviour
{
    [SerializeField] private ObjectiveRowView[] _rows;
    [SerializeField] private TMP_Text _empty;
    [SerializeField] private UIButton _allButton;

    private void OnEnable()
    {
        GameEvents.OnProfileChanged += HandleProfileChanged;
        _allButton.Clicked += GameEvents.RaiseObjectivesRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnProfileChanged -= HandleProfileChanged;
        _allButton.Clicked -= GameEvents.RaiseObjectivesRequested;
    }

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        bool gap = snapshot.CueGap != null;
        for (int i = 0; i < _rows.Length; i++)
            _rows[i].Bind(i < snapshot.Objectives.Count ? snapshot.Objectives[i] : null, gap);
        _empty.gameObject.SetActive(snapshot.Objectives.Count == 0);
    }
}
