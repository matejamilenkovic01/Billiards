using DG.Tweening;
using TMPro;
using UnityEngine;

// The tutorial's lesson card at the top (v3 V9): "LESSON 2/5 · AIM", the instruction, SKIP (with a confirm).
public class TutorialView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _card;
    [SerializeField] private TMP_Text _step;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private UIButton _skipButton;

    private void Awake() => SetVisible(false);

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnTutorialStepShown += HandleStepShown;
        GameEvents.OnTutorialCompleted += HandleCompleted;
        _skipButton.Clicked += HandleSkip;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnTutorialStepShown -= HandleStepShown;
        GameEvents.OnTutorialCompleted -= HandleCompleted;
        _skipButton.Clicked -= HandleSkip;
    }

    private void HandleMatchStarted(MatchStartInfo info) => SetVisible(info.IsTutorial);

    private void HandleStepShown(TutorialStepInfo info)
    {
        SetVisible(true);
        _step.text = $"LESSON {info.Index + 1}/{info.Count}  ·  {info.Title}";
        _text.text = info.Instruction;
        _card.DOKill();
        _card.localScale = Vector3.one;
        _card.DOPunchScale(Vector3.one * 0.05f, 0.35f, 6, 0.5f).SetUpdate(true).SetLink(_card.gameObject);
    }

    private void HandleCompleted(bool skipped) => SetVisible(false);

    private void HandleSkip() =>
        PopupManager.Instance.Show<ConfirmPopup>(new ConfirmPopupData("SKIP TUTORIAL", "Skip the lessons and play your first match?", "SKIP",
            GameEvents.RaiseTutorialSkipRequested));

    private void SetVisible(bool visible)
    {
        _group.alpha = visible ? 1f : 0f;
        _group.blocksRaycasts = visible;
        _group.interactable = visible;
    }
}
