using System.Collections.Generic;
using UnityEngine;

// TRY a cue (v4 V9): three practice shots set up like tutorial lessons (TutorialStep layout: the cue ball behind the
// ghost ball, or a full rack for the break). No rank, no coins, no objectives.
[CreateAssetMenu(fileName = "PracticeConfig", menuName = "Billiards/Practice Config")]
public class PracticeConfig : ScriptableObject
{
    public List<TutorialStep> Steps = new();
    [Tooltip("Shown above the card: {0} is the cue's name in capitals.")]
    public string Header = "TRYING: {0}";
    public string SameCue = "This is your cue";
}
