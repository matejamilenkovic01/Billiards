using TMPro;
using UnityEngine;

// The three visible objectives on the menu; a claimed one is replaced by the next on the list.
public class ObjectivesPanelView : MonoBehaviour
{
    [SerializeField] private ObjectiveRowView[] _rows;
    [SerializeField] private TMP_Text _empty;

    private void OnEnable() => GameEvents.OnProfileChanged += HandleProfileChanged;

    private void OnDisable() => GameEvents.OnProfileChanged -= HandleProfileChanged;

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        for (int i = 0; i < _rows.Length; i++)
            _rows[i].Bind(i < snapshot.Objectives.Count ? snapshot.Objectives[i] : null);
        _empty.gameObject.SetActive(snapshot.Objectives.Count == 0);
    }
}
