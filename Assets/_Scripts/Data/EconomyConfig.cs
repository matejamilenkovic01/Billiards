using UnityEngine;

// Coins (v3 V12, v4 V14). Coins come only from play (wins, losses, objectives, rank rewards) and are spent only on
// cues. What a win, a loss and a stage pay lives in the DivisionCatalog.
[CreateAssetMenu(fileName = "EconomyConfig", menuName = "Billiards/Economy Config")]
public class EconomyConfig : ScriptableObject
{
    public int StartCoins = 0;
}
