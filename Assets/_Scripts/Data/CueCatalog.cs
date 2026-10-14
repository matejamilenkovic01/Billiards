using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CueCatalog", menuName = "Billiards/Cue Catalog")]
public class CueCatalog : ScriptableObject
{
    [Header("Economy")]
    public int StartingCoins = 10;
    public int WinReward = 10;
    public int LossPenalty = 1;

    [Tooltip("Display order. The first cue is owned from the start.")]
    public List<CueDefinition> Cues = new();

    public CueDefinition Starter => Cues.Count > 0 ? Cues[0] : null;

    public CueDefinition Get(string id)
    {
        foreach (CueDefinition cue in Cues)
        {
            if (cue.Id == id)
                return cue;
        }
        return null;
    }

    private void OnValidate()
    {
        var ids = new HashSet<string>();
        foreach (CueDefinition cue in Cues)
        {
            if (string.IsNullOrEmpty(cue.Id) || !ids.Add(cue.Id))
                Debug.LogWarning($"CueCatalog: empty or duplicate id '{cue.Id}'", this);
        }
    }
}
