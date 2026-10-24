using DG.Tweening;
using TMPro;
using UnityEngine;

// The stand-in for a rewarded ad (v3 V12): "Ad (test)", a countdown, CLOSE. Closing before the end = no reward.
public class SimulatedAdOverlay : PopupBase
{
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _countdown;
    [SerializeField] private TMP_Text _note;
    [SerializeField] private UIButton _closeButton;
    [SerializeField] private TMP_Text _closeLabel;

    private SimulatedAdData _data;
    private float _left;
    private bool _answered;

    private void OnEnable() => _closeButton.Clicked += HandleClose;

    private void OnDisable() => _closeButton.Clicked -= HandleClose;

    protected override void OnShow(object payload)
    {
        if (payload is not SimulatedAdData data)
            return;
        _data = data;
        _left = data.Seconds;
        _answered = false;
        _title.text = data.Placement == RewardedPlacement.StarShield ? "Ad (test) · Star Shield" : "Ad (test) · Double coins";
        _note.text = "Close now and you get nothing.";
        _closeLabel.text = "CLOSE";
        Refresh();
    }

    private void Update()
    {
        if (_data == null || _answered || _left <= 0f)
            return;
        _left -= Time.unscaledDeltaTime;
        Refresh();
    }

    private void Refresh()
    {
        bool done = _left <= 0f;
        _countdown.text = done ? "" : Mathf.CeilToInt(_left).ToString();
        _note.text = done ? "Reward earned!" : "Close now and you get nothing.";
        if (done)
        {
            _countdown.transform.DOKill();
            _countdown.transform.localScale = Vector3.one;
        }
    }

    private void HandleClose()
    {
        if (_answered || _data == null)
            return;
        _answered = true;
        bool completed = _left <= 0f;
        Hide();
        _data.OnClosed?.Invoke(completed);
    }
}
