using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One stage on the Ranked Road: its label and stars; a checkpoint row also names the venue and the bot's cue.
public class RoadRowView : MonoBehaviour
{
    [SerializeField] private Image _background;
    [Tooltip("A checkpoint row shows its venue behind it.")]
    [SerializeField] private Image _thumbnail;
    [SerializeField] private Image _badge;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image[] _stars;
    [SerializeField] private TMP_Text _championStars;
    [SerializeField] private GameObject _checkpoint;
    [SerializeField] private TMP_Text _checkpointText;
    [SerializeField] private GameObject _here;
    [SerializeField] private GameObject _done;
    [SerializeField] private Color _starOn = new(1f, 0.82f, 0.26f, 1f);
    [SerializeField] private Color _starOff = new(1f, 1f, 1f, 0.18f);

    public void Bind(RoadStageViewData stage)
    {
        bool current = stage.State == RoadStageState.Current;
        bool done = stage.State == RoadStageState.Done;
        Color tint = stage.Color;
        _background.color = current ? new Color(tint.r * 0.45f, tint.g * 0.45f, tint.b * 0.45f, 0.95f)
            : done ? new Color(0.08f, 0.1f, 0.09f, 0.85f) : new Color(0f, 0f, 0f, 0.55f);
        _badge.color = stage.State == RoadStageState.Ahead ? new Color(tint.r, tint.g, tint.b, 0.45f) : tint;
        _label.text = stage.Label;
        _label.alpha = stage.State == RoadStageState.Ahead ? 0.6f : 1f;
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i].gameObject.SetActive(!stage.IsChampion && i < stage.StarsPerStage);
            _stars[i].color = done || (current && i < stage.Stars) ? _starOn : _starOff;
        }
        _championStars.gameObject.SetActive(stage.IsChampion);
        _championStars.text = current ? $"{stage.Stars} STARS" : "ENDLESS";
        _checkpoint.SetActive(stage.IsCheckpoint);
        _thumbnail.gameObject.SetActive(stage.IsCheckpoint && stage.VenueThumbnail != null);
        _thumbnail.sprite = stage.VenueThumbnail;
        _thumbnail.color = new Color(1f, 1f, 1f, stage.State == RoadStageState.Ahead ? 0.22f : 0.38f);
        _checkpointText.text = stage.IsChampion ? $"{stage.VenueName} · endless stars" : $"CHECKPOINT · {stage.VenueName} · bot: {stage.BotCueName}";
        _here.SetActive(current);
        _done.SetActive(done);
    }
}
