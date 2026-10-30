using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One objective: text, progress bar and count, reward; CLAIM when it is done (v3 V12). v4 V8: two lines, and CLAIM
// (with the reward on it) takes the reward's place, so nothing is shown twice or overlaps. v4 V12: a claimed row
// is dimmed with a check, a row of the next division's set is locked (text and reward visible), and an objective
// that moves in a lost match too carries a small coin while the cue gap card is up.
public class ObjectiveRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _progress;
    [SerializeField] private TMP_Text _reward;
    [SerializeField] private GameObject _rewardIcon;
    [SerializeField] private UIButton _claimButton;
    [SerializeField] private TMP_Text _claimLabel;
    [SerializeField] private Image _background;
    [SerializeField] private GameObject _frame;
    [Tooltip("Small coin at the top right: this objective pays \"cue coins\", in a loss too.")]
    [SerializeField] private GameObject _cueCoinTag;
    [SerializeField] private Color _normal = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color _done = new(0.13f, 0.38f, 0.2f, 0.85f);
    [SerializeField] private Color _locked = new(0f, 0f, 0f, 0.3f);

    private string _id;
    private bool _claimable;

    private void OnEnable() => _claimButton.Clicked += HandleClaim;

    private void OnDisable() => _claimButton.Clicked -= HandleClaim;

    public void Bind(ObjectiveViewData objective, bool showCueCoin = false)
    {
        gameObject.SetActive(objective != null);
        if (objective == null)
            return;
        _id = objective.Id;
        _claimable = objective.Claimable;
        bool claimed = objective.Claimed;
        bool locked = objective.Locked;
        _text.text = objective.Text;
        _text.alpha = claimed ? 0.55f : locked ? 0.7f : 1f;
        _fill.fillAmount = objective.Target > 0 && !locked ? Mathf.Clamp01(objective.Progress / (float)objective.Target) : 0f;
        _fill.transform.parent.gameObject.SetActive(!locked);
        _progress.gameObject.SetActive(true);
        _progress.text = claimed ? "✓ CLAIMED" : locked ? "LOCKED" : $"{Mathf.Min(objective.Progress, objective.Target)}/{objective.Target}";
        _reward.text = objective.Reward.ToString();
        _reward.gameObject.SetActive(!_claimable);
        _rewardIcon.SetActive(!_claimable);
        _claimLabel.text = $"CLAIM {objective.Reward}";
        _claimButton.gameObject.SetActive(_claimable);
        _background.color = _claimable ? _done : locked || claimed ? _locked : _normal;
        _frame.SetActive(_claimable);
        _cueCoinTag.SetActive(showCueCoin && objective.Playable && !claimed && !locked);

        Transform claim = _claimButton.transform;
        claim.DOKill();
        claim.localScale = Vector3.one;
        if (_claimable)
            claim.DOScale(1.06f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(claim.gameObject);
    }

    // The menu rebinds from the new profile right away; the results screen keeps the row, marked CLAIMED.
    private void HandleClaim()
    {
        if (!_claimable || string.IsNullOrEmpty(_id))
            return;
        _claimable = false;
        _claimButton.transform.DOKill();
        _claimButton.gameObject.SetActive(false);
        _progress.gameObject.SetActive(true);
        _progress.text = "✓ CLAIMED";
        GameEvents.RaiseObjectiveClaimRequested(_id);
    }
}
