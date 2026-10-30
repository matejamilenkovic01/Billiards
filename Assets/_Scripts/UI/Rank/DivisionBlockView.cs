using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One division on the DIVISIONS screen (v4 V11): a strong header in the division's colour (name, venue and the bot's
// cue, the 8-ball rule with its icon, the reward for entering) and its stages as a line of nodes. Passed divisions
// are dimmed with a check, later ones stay visible with a lock and what it takes. Grand Champion has no stages.
public class DivisionBlockView : MonoBehaviour
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _header;
    [SerializeField] private Image _thumbnail;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _venue;
    [SerializeField] private Image _ruleIcon;
    [SerializeField] private TMP_Text _rule;
    [SerializeField] private TMP_Text _entry;
    [SerializeField] private GameObject _entryIcon;
    [SerializeField] private GameObject _lock;
    [SerializeField] private TMP_Text _lockText;
    [SerializeField] private GameObject _done;
    [SerializeField] private DivisionStageView[] _stages;
    [SerializeField] private Sprite _anyPocketIcon;
    [SerializeField] private Sprite _calledPocketIcon;
    [SerializeField] private Sprite _blockedPocketIcon;

    public void Bind(DivisionBlockData block)
    {
        bool ahead = block.State == RoadStageState.Ahead;
        bool done = block.State == RoadStageState.Done;
        Color tint = block.Color;
        _header.color = ahead ? new Color(tint.r * 0.3f, tint.g * 0.3f, tint.b * 0.3f, 0.95f) : new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.55f, 1f);
        _background.color = done ? new Color(0.06f, 0.08f, 0.07f, 0.85f) : new Color(0f, 0f, 0f, 0.55f);
        _name.text = block.Name;
        _name.alpha = ahead ? 0.75f : 1f;
        _venue.text = block.IsChampion ? $"{block.VenueName}  ·  endless stars" : $"{block.VenueName}  ·  {block.BotCueName}";
        _thumbnail.gameObject.SetActive(block.VenueThumbnail != null);
        _thumbnail.sprite = block.VenueThumbnail;
        _thumbnail.color = new Color(1f, 1f, 1f, ahead ? 0.25f : 0.5f);
        _ruleIcon.sprite = IconFor(block.Rules);
        _rule.text = block.RuleText;
        bool reward = block.EntryReward > 0;
        _entry.gameObject.SetActive(reward);
        _entryIcon.SetActive(reward);
        _entry.text = block.EntryPaid ? $"{block.EntryReward}  ✓" : block.EntryReward.ToString();
        _lock.SetActive(ahead);
        _lockText.text = block.LockText;
        _done.SetActive(done);
        for (int i = 0; i < _stages.Length; i++)
        {
            bool has = i < block.Stages.Count;
            _stages[i].gameObject.SetActive(has);
            if (has)
                _stages[i].Bind(block.Stages[i], tint);
        }
    }

    private Sprite IconFor(RuleSetId rules) => rules switch
    {
        RuleSetId.Standard => _anyPocketIcon,
        RuleSetId.BlockedPocket => _blockedPocketIcon,
        _ => _calledPocketIcon
    };
}
