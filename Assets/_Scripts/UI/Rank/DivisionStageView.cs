using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One stage on the DIVISIONS line (v4 V11): a node (bigger and brighter where the player is), the label, the stars
// in it, the reward and a check once it is paid; the first stage of a division also says CHECKPOINT.
public class DivisionStageView : MonoBehaviour
{
    [SerializeField] private Image _node;
    [SerializeField] private RectTransform _nodeRect;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image[] _stars;
    [SerializeField] private TMP_Text _reward;
    [SerializeField] private GameObject _rewardIcon;
    [SerializeField] private GameObject _check;
    [SerializeField] private TMP_Text _here;
    [SerializeField] private TMP_Text _flag;
    [SerializeField] private Color _starOn = new(1f, 0.82f, 0.26f, 1f);
    [SerializeField] private Color _starOff = new(1f, 1f, 1f, 0.18f);

    public void Bind(DivisionStageData stage, Color tint)
    {
        bool current = stage.State == RoadStageState.Current;
        bool done = stage.State == RoadStageState.Done;
        _node.color = stage.State == RoadStageState.Ahead ? new Color(tint.r, tint.g, tint.b, 0.35f) : tint;
        _nodeRect.localScale = Vector3.one * (current ? 1.35f : 1f);
        _label.text = stage.Label;
        _label.alpha = stage.State == RoadStageState.Ahead ? 0.7f : 1f;
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i].gameObject.SetActive(i < stage.StarsPerStage);
            _stars[i].color = done || (current && i < stage.Stars) ? _starOn : _starOff;
        }
        bool paid = stage.RewardPaid || stage.Reward <= 0;
        _reward.gameObject.SetActive(stage.Reward > 0);
        _rewardIcon.SetActive(stage.Reward > 0);
        _reward.text = stage.Reward.ToString();
        _reward.alpha = stage.RewardPaid ? 0.55f : 1f;
        _check.SetActive(stage.RewardPaid && stage.Reward > 0);
        _here.gameObject.SetActive(current);
        _flag.gameObject.SetActive(stage.IsCheckpoint && stage.Label != "BRONZE III");
        _ = paid;
    }
}
