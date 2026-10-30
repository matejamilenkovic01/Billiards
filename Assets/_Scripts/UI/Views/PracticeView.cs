using DG.Tweening;
using TMPro;
using UnityEngine;

// The practice card (v4 V9, TRY): "TRYING: PRO CUE", how it differs from the selected cue, the shot's instruction,
// RETRY and NEXT. It takes the lesson card's place at the top.
public class PracticeView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _card;
    [SerializeField] private TMP_Text _header;
    [SerializeField] private TMP_Text _diff;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private UIButton _retryButton;
    [SerializeField] private UIButton _nextButton;

    private void Awake() => SetVisible(false);

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnPracticeStepShown += HandleStepShown;
        GameEvents.OnCueTryFinished += HandleFinished;
        _retryButton.Clicked += GameEvents.RaisePracticeRetryRequested;
        _nextButton.Clicked += GameEvents.RaisePracticeNextRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnPracticeStepShown -= HandleStepShown;
        GameEvents.OnCueTryFinished -= HandleFinished;
        _retryButton.Clicked -= GameEvents.RaisePracticeRetryRequested;
        _nextButton.Clicked -= GameEvents.RaisePracticeNextRequested;
    }

    private void HandleMatchStarted(MatchStartInfo info) => SetVisible(info.IsPractice);

    private void HandleStepShown(PracticeStepInfo info)
    {
        SetVisible(true);
        _header.text = info.Header;
        _diff.text = info.Diff;
        _text.text = info.Instruction;
        // The last shot's NEXT ends the try.
        _nextButton.GetComponentInChildren<TMP_Text>().text = info.Index + 1 >= info.Count ? "DONE" : "NEXT";
        _card.DOKill();
        _card.localScale = Vector3.one;
        _card.DOPunchScale(Vector3.one * 0.05f, 0.35f, 6, 0.5f).SetUpdate(true).SetLink(_card.gameObject);
    }

    private void HandleFinished(string cueId, int shots, int perfect) => SetVisible(false);

    private void SetVisible(bool visible)
    {
        _group.alpha = visible ? 1f : 0f;
        _group.blocksRaycasts = visible;
        _group.interactable = visible;
    }
}
