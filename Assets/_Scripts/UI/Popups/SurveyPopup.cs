using UnityEngine;
using UnityEngine.UI;

// The one-time survey (v3 V12, meta 6.9): how the release bar feels (three faces as words), what is missing (chips,
// any number), SEND or SKIP.
public class SurveyPopup : PopupBase
{
    [SerializeField] private UIButton[] _scoreButtons;   // 3 = love it, 2 = it's OK, 1 = don't like it
    [SerializeField] private Image[] _scoreFrames;
    [SerializeField] private UIButton[] _chips;          // SurveyMissing bits, in order
    [SerializeField] private Image[] _chipFrames;
    [SerializeField] private UIButton _sendButton;
    [SerializeField] private UIButton _skipButton;
    [SerializeField] private Color _selected = new(0.3f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color _unselected = new(1f, 1f, 1f, 0f);

    private int _score;
    private int _missing;
    private readonly System.Action[] _scoreHandlers = new System.Action[3];
    private System.Action[] _chipHandlers;

    protected override void Awake()
    {
        base.Awake();
        for (int i = 0; i < _scoreHandlers.Length; i++)
        {
            int score = 3 - i;
            _scoreHandlers[i] = () => SetScore(score);
        }
        _chipHandlers = new System.Action[_chips.Length];
        for (int i = 0; i < _chips.Length; i++)
        {
            int bit = 1 << i;
            _chipHandlers[i] = () => ToggleChip(bit);
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < _scoreButtons.Length; i++)
            _scoreButtons[i].Clicked += _scoreHandlers[i];
        for (int i = 0; i < _chips.Length; i++)
            _chips[i].Clicked += _chipHandlers[i];
        _sendButton.Clicked += HandleSend;
        _skipButton.Clicked += HandleSkip;
    }

    private void OnDisable()
    {
        for (int i = 0; i < _scoreButtons.Length; i++)
            _scoreButtons[i].Clicked -= _scoreHandlers[i];
        for (int i = 0; i < _chips.Length; i++)
            _chips[i].Clicked -= _chipHandlers[i];
        _sendButton.Clicked -= HandleSend;
        _skipButton.Clicked -= HandleSkip;
    }

    protected override void OnShow(object payload)
    {
        _score = 0;
        _missing = 0;
        Refresh();
    }

    private void SetScore(int score)
    {
        _score = score;
        Refresh();
    }

    private void ToggleChip(int bit)
    {
        // "Nothing" excludes the others and back.
        int nothing = (int)SurveyMissing.Nothing;
        _missing ^= bit;
        if (bit == nothing && (_missing & nothing) != 0)
            _missing = nothing;
        else if (bit != nothing)
            _missing &= ~nothing;
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < _scoreFrames.Length; i++)
            _scoreFrames[i].color = _score == 3 - i ? _selected : _unselected;
        for (int i = 0; i < _chipFrames.Length; i++)
            _chipFrames[i].color = (_missing & (1 << i)) != 0 ? _selected : _unselected;
        _sendButton.Interactable = _score > 0;
    }

    private void HandleSend()
    {
        GameEvents.RaiseSurveyAnswered(_score, _missing);
        Hide();
    }

    private void HandleSkip()
    {
        GameEvents.RaiseSurveySkipped();
        Hide();
    }
}
