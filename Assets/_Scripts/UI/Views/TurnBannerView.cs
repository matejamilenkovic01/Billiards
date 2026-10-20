using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A band that slides across the middle: the coin toss for who breaks, whose turn it is, and (gameplay redesign
// G5) what just happened and what it means: FOUL with its consequence (and a "?" to the rule), BALL IN HAND /
// BREAK with where the cue ball may go, YOU ARE SOLIDS / STRIPES with the group's ball.
public class TurnBannerView : MonoBehaviour
{
    private const float SlideIn = 0.22f;
    private const float Hold = 0.4f;
    private const float SlideOut = 0.2f;

    [SerializeField] private RectTransform _band;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private TMP_Text _subText;
    [SerializeField] private Image _icon;
    [SerializeField] private UIButton _helpButton;
    [SerializeField] private Sprite _solidsSprite;
    [SerializeField] private Sprite _stripesSprite;
    [SerializeField] private Color _playerColor = new(0.18f, 0.62f, 0.32f, 0.95f);
    [SerializeField] private Color _botColor = new(0.9f, 0.5f, 0.12f, 0.95f);
    [SerializeField] private Color _ballInHandColor = new(0.2f, 0.45f, 0.85f, 0.95f);
    [SerializeField] private Color _foulColor = new(0.72f, 0.16f, 0.14f, 0.96f);
    [SerializeField] private Color _neutralColor = new(0.1f, 0.12f, 0.12f, 0.92f);

    private Sequence _sequence;
    private string _playerName = "YOU";
    private string _opponentName = "OPPONENT";
    private float _foulDuration = 2.2f;
    private float _groupDuration = 1.6f;
    private RuleId _helpRule;

    private float Width => ((RectTransform)transform).rect.width;

    private void Awake()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _helpButton.gameObject.SetActive(false);
        _icon.gameObject.SetActive(false);
        _subText.text = "";
    }

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnFoul += HandleFoul;
        GameEvents.OnGroupsAssigned += HandleGroupsAssigned;
        _helpButton.Clicked += HandleHelp;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnFoul -= HandleFoul;
        GameEvents.OnGroupsAssigned -= HandleGroupsAssigned;
        _helpButton.Clicked -= HandleHelp;
        _sequence?.Kill();
    }

    // Coin toss: the text flips between the two players a few times, then lands.
    private void HandleMatchStarted(MatchStartInfo info)
    {
        _playerName = info.PlayerName;
        _opponentName = info.OpponentName;
        _foulDuration = info.FoulBannerDuration;
        _groupDuration = info.GroupBannerDuration;
        _sequence?.Kill();
        SetContent("COIN TOSS", null, null, false);
        _background.color = _neutralColor;
        _band.anchoredPosition = Vector2.zero;
        _band.localScale = Vector3.one;

        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _sequence.Append(_group.DOFade(1f, 0.15f));
        for (int i = 0; i < 5; i++)
        {
            bool you = i % 2 == 0;
            _sequence.Append(_band.DOScaleY(0f, 0.06f));
            _sequence.AppendCallback(() => _text.text = you ? _playerName : _opponentName);
            _sequence.Append(_band.DOScaleY(1f, 0.06f));
        }
        _sequence.Append(_band.DOScaleY(0f, 0.06f));
        _sequence.AppendCallback(() =>
        {
            _text.text = info.PlayerBreaks ? "YOU BREAK" : "OPPONENT BREAKS";
            _background.color = info.PlayerBreaks ? _playerColor : _botColor;
        });
        _sequence.Append(_band.DOScaleY(1f, 0.1f).SetEase(Ease.OutBack));
        _sequence.AppendInterval(0.35f);
        _sequence.Append(_group.DOFade(0f, 0.15f));
    }

    private void HandleTurnStarted(TurnInfo info)
    {
        if (!info.IsPlayer)
            Show("OPPONENT'S TURN", info.BallInHand && !info.IsBreak ? "Ball in hand" : null, null, _botColor, Hold, false);
        else if (info.BallInHand && info.IsBreak)
            Show("BREAK", "Place the cue ball behind the line", null, _ballInHandColor, Hold + 0.5f, false);
        else if (info.BallInHand)
            Show("BALL IN HAND", "Place the cue ball anywhere", null, _ballInHandColor, Hold + 0.5f, false);
        else
            Show("YOUR TURN", null, null, _playerColor, Hold, false);
    }

    // FOUL, why, and what happens now. The next turn waits until this banner is gone.
    private void HandleFoul(FoulInfo foul)
    {
        string reason = foul.Foul switch
        {
            FoulType.WrongBallFirst => "Wrong ball first",
            FoulType.CueBallPocketed => "Cue ball pocketed",
            FoulType.NoBallHit => "No ball hit",
            FoulType.NoRailAfterContact => "No rail after contact",
            _ => null
        };
        if (reason == null)
            return;
        string consequence = foul.MatchOver ? null
            : foul.ByPlayer ? "Opponent has ball in hand"
            : "You have ball in hand — place the cue ball anywhere";
        _helpRule = foul.Rule;
        Show("FOUL — " + reason, consequence, null, _foulColor, HoldFor(_foulDuration), true);
    }

    private void HandleGroupsAssigned(BallGroup player, BallGroup bot)
    {
        bool solids = player == BallGroup.Solids;
        Show(solids ? "YOU ARE SOLIDS (1–7)" : "YOU ARE STRIPES (9–15)", null, solids ? _solidsSprite : _stripesSprite,
            _playerColor, HoldFor(_groupDuration), false);
    }

    private void HandleHelp() => GameEvents.RaiseRulesRequested(_helpRule);

    private static float HoldFor(float total) => Mathf.Max(0.2f, total - SlideIn - SlideOut);

    private void SetContent(string text, string subText, Sprite icon, bool help)
    {
        _text.text = text;
        bool hasSub = !string.IsNullOrEmpty(subText);
        _subText.text = hasSub ? subText : "";
        // Two lines: the headline moves up to make room for the consequence.
        // A ball icon on the left or the "?" on the right: the headline narrows and steps aside.
        float side = icon != null ? 70f : help ? -60f : 0f;
        _text.rectTransform.anchoredPosition = new Vector2(side, hasSub ? 26f : 0f);
        _text.rectTransform.sizeDelta = new Vector2(icon != null || help ? 760f : 900f, _text.rectTransform.sizeDelta.y);
        _subText.rectTransform.anchoredPosition = new Vector2(help ? -60f : 0f, _subText.rectTransform.anchoredPosition.y);
        _subText.rectTransform.sizeDelta = new Vector2(help ? 800f : 900f, _subText.rectTransform.sizeDelta.y);
        _icon.gameObject.SetActive(icon != null);
        if (icon != null)
            _icon.sprite = icon;
        _helpButton.gameObject.SetActive(help);
        // Only the "?" takes touches; everything else lets drags through to the table.
        _group.blocksRaycasts = help;
    }

    private void Show(string text, string subText, Sprite icon, Color color, float hold, bool help)
    {
        _sequence?.Kill();
        SetContent(text, subText, icon, help);
        _background.color = color;
        _band.localScale = Vector3.one;
        float width = Width;
        _band.anchoredPosition = new Vector2(-width, 0f);
        _group.alpha = 1f;
        _sequence = DOTween.Sequence()
            .Append(_band.DOAnchorPosX(0f, SlideIn).SetEase(Ease.OutCubic))
            .AppendInterval(hold)
            .Append(_band.DOAnchorPosX(width, SlideOut).SetEase(Ease.InCubic))
            .AppendCallback(() =>
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
            })
            .SetUpdate(true)
            .SetLink(gameObject);
    }
}
