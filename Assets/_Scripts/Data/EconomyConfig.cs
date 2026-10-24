using UnityEngine;

// Coins and the monetisation probes (v3 V12). Coins come only from play (wins, losses, objectives, Double coins)
// and are spent only on cues. Every number here, none in code.
[CreateAssetMenu(fileName = "EconomyConfig", menuName = "Billiards/Economy Config")]
public class EconomyConfig : ScriptableObject
{
    public int StartCoins = 0;
    [Tooltip("A loss still pays a little, so buying a cue never stops completely.")]
    public int LossCoins = 5;

    [Header("Rewarded (simulated in this phase)")]
    public int StarShieldsPerDay = 3;
    public int DoubleCoinsPerMatch = 1;

    [Header("Fake doors and survey")]
    public int StarterPackAfterMatches = 4;
    public int StarterPackMaxShows = 3;
    [Tooltip("Not after this many losses in a row.")]
    public int StarterPackSkipAfterLosses = 2;
    public string StarterPackPrice = "$1.99";
    public int SurveyAfterMatches = 6;
}
