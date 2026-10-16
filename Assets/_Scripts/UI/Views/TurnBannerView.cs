using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A band that slides across the middle at the start of each turn: YOUR TURN / OPPONENT'S TURN / BALL IN HAND,
// and the coin toss for who breaks.
public class TurnBannerView : MonoBehaviour
{
    private const float SlideIn = 0.22f;
    private const float Hold = 0.4f;
    private const float SlideOut = 0.2f;

    [SerializeField] private RectTransform _band;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Color _playerColor = new(0.18f, 0.62f, 0.32f, 0.95f);
    [SerializeField] private Color _botColor = new(0.9f, 0.5f, 0.12f, 0.95f);
    [SerializeField] private Color _ballInHandColor = new(0.2f, 0.45f, 0.85f, 0.95f);
    [SerializeField] private Color _neutralColor = new(0.1f, 0.12f, 0.12f, 0.92f);

    private Sequence _sequence;
    private float Width => ((RectTransform)transform).rect.width;

    private void Awake()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnTurnStarted += HandleTurnStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        _sequence?.Kill();
    }

    // Coin toss: the text flips between the two players a few times, then lands.
    private void HandleMatchStarted(MatchStartInfo info)
    {
        _sequence?.Kill();
        _background.color = _neutralColor;
        _band.anchoredPosition = Vector2.zero;
        _band.localScale = Vector3.one;
        _text.text = "COIN TOSS";

        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _sequence.Append(_group.DOFade(1f, 0.15f));
        for (int i = 0; i < 5; i++)
        {
            bool you = i % 2 == 0;
            _sequence.Append(_band.DOScaleY(0f, 0.06f));
            _sequence.AppendCallback(() => _text.text = you ? "YOU" : "OPPONENT");
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
            Show("OPPONENT'S TURN", _botColor);
        else if (info.BallInHand && !info.IsBreak)
            Show("BALL IN HAND", _ballInHandColor);
        else
            Show("YOUR TURN", _playerColor);
    }

    private void Show(string text, Color color)
    {
        _sequence?.Kill();
        _text.text = text;
        _background.color = color;
        _band.localScale = Vector3.one;
        float width = Width;
        _band.anchoredPosition = new Vector2(-width, 0f);
        _group.alpha = 1f;
        _sequence = DOTween.Sequence()
            .Append(_band.DOAnchorPosX(0f, SlideIn).SetEase(Ease.OutCubic))
            .AppendInterval(Hold)
            .Append(_band.DOAnchorPosX(width, SlideOut).SetEase(Ease.InCubic))
            .AppendCallback(() => _group.alpha = 0f)
            .SetUpdate(true)
            .SetLink(gameObject);
    }
}
