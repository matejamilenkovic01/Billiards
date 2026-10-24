using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One objective: text, progress bar and count, reward; CLAIM when it is done (v3 V12).
public class ObjectiveRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _progress;
    [SerializeField] private TMP_Text _reward;
    [SerializeField] private UIButton _claimButton;
    [SerializeField] private TMP_Text _claimLabel;
    [SerializeField] private Image _background;
    [SerializeField] private Color _normal = new(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color _done = new(0.13f, 0.38f, 0.2f, 0.85f);

    private int _slot = -1;
    private bool _claimable;

    private void OnEnable() => _claimButton.Clicked += HandleClaim;

    private void OnDisable() => _claimButton.Clicked -= HandleClaim;

    public void Bind(ObjectiveViewData objective)
    {
        gameObject.SetActive(objective != null);
        if (objective == null)
            return;
        _slot = objective.Slot;
        _claimable = objective.Claimable;
        _text.text = objective.Text;
        _fill.fillAmount = objective.Target > 0 ? Mathf.Clamp01(objective.Progress / (float)objective.Target) : 0f;
        _progress.text = $"{Mathf.Min(objective.Progress, objective.Target)}/{objective.Target}";
        _progress.gameObject.SetActive(!_claimable);
        _reward.text = objective.Reward.ToString();
        _claimLabel.text = "CLAIM";
        _claimButton.gameObject.SetActive(_claimable);
        _background.color = _claimable ? _done : _normal;

        Transform claim = _claimButton.transform;
        claim.DOKill();
        claim.localScale = Vector3.one;
        if (_claimable)
            claim.DOScale(1.06f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(claim.gameObject);
    }

    // The menu rebinds from the new profile right away; the results screen keeps the row, marked CLAIMED.
    private void HandleClaim()
    {
        if (!_claimable || _slot < 0)
            return;
        _claimable = false;
        _claimButton.transform.DOKill();
        _claimButton.gameObject.SetActive(false);
        _progress.gameObject.SetActive(true);
        _progress.text = "CLAIMED";
        GameEvents.RaiseObjectiveClaimRequested(_slot);
    }
}
