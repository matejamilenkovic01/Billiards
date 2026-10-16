using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// End of the match: YOU WIN! (gold, confetti) or YOU LOSE, the reason, the coin change counting into the
// balance, and any cue the win just unlocked.
public class MatchResultPopup : PopupBase
{
    private const int ConfettiCount = 40;

    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _reason;
    [SerializeField] private TMP_Text _delta;
    [SerializeField] private TMP_Text _balance;
    [SerializeField] private GameObject _coinRow;
    [SerializeField] private TMP_Text _playAgainLabel;
    [SerializeField] private GameObject _unlockCard;
    [SerializeField] private TMP_Text _unlockText;
    [SerializeField] private UIButton _seeCuesButton;
    [SerializeField] private UIButton _playAgainButton;
    [SerializeField] private UIButton _menuButton;
    [SerializeField] private RectTransform _confettiRoot;
    [SerializeField] private Sprite _confettiSprite;
    [SerializeField] private Color _winColor = new(0.97f, 0.77f, 0.27f);
    [SerializeField] private Color _loseColor = new(0.78f, 0.33f, 0.3f);

    private Image[] _confetti;
    private int _shownBalance;

    private static readonly Color[] ConfettiColors =
    {
        new(0.97f, 0.77f, 0.27f), new(0.25f, 0.8f, 0.4f), new(0.95f, 0.35f, 0.3f), new(0.3f, 0.6f, 0.95f), Color.white
    };

    protected override void Awake()
    {
        base.Awake();
        _confetti = new Image[ConfettiCount];
        for (int i = 0; i < ConfettiCount; i++)
        {
            var piece = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            piece.transform.SetParent(_confettiRoot, false);
            _confetti[i] = piece.GetComponent<Image>();
            _confetti[i].sprite = _confettiSprite;
            _confetti[i].raycastTarget = false;
            piece.SetActive(false);
        }
    }

    private void OnEnable()
    {
        _playAgainButton.Clicked += GameEvents.RaisePlayRequested;
        _menuButton.Clicked += GameEvents.RaiseMenuRequested;
        _seeCuesButton.Clicked += GameEvents.RaiseCuesRequested;
    }

    private void OnDisable()
    {
        _playAgainButton.Clicked -= GameEvents.RaisePlayRequested;
        _menuButton.Clicked -= GameEvents.RaiseMenuRequested;
        _seeCuesButton.Clicked -= GameEvents.RaiseCuesRequested;
    }

    protected override void OnShow(object payload)
    {
        if (payload is not MatchResultPopupData data)
            return;

        MatchResult result = data.Result;
        bool celebrate = result.Won || result.IsTwoPlayers;
        _title.text = result.Title;
        _title.color = celebrate ? _winColor : _loseColor;
        // Two players on one device: nothing to count, just a rematch.
        _coinRow.SetActive(!result.IsTwoPlayers);

        _playAgainLabel.text = result.IsTwoPlayers ? "REMATCH" : "PLAY AGAIN";
        _reason.text = result.Reason;
        _delta.text = result.CoinDelta > 0 ? $"+{result.CoinDelta}" : result.CoinDelta < 0 ? $"{result.CoinDelta}" : "0";
        _delta.color = result.CoinDelta >= 0 ? _winColor : _loseColor;

        _unlockCard.SetActive(result.NewUnlocks.Count > 0);
        if (result.NewUnlocks.Count > 0)
            _unlockText.text = $"New cue unlocked: {string.Join(", ", result.NewUnlocks)}!";

        _title.transform.DOKill();
        _title.transform.localScale = Vector3.one * 0.6f;
        _title.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetDelay(0.1f).SetUpdate(true).SetLink(_title.gameObject);
        if (celebrate)
            PlayConfetti();

        DOTween.Kill(this);
        if (result.IsTwoPlayers)
        {
            _balance.text = "FRIENDLY MATCH  ·  NOTHING AT STAKE";
            return;
        }

        // Balance counts from the old value to the new one once the panel has popped in.
        _shownBalance = data.PreviousBalance;
        _balance.text = $"Balance {_shownBalance}";
        int target = result.NewBalance;
        DOTween.To(() => (float)_shownBalance, value =>
            {
                int whole = Mathf.RoundToInt(value);
                if (whole != _shownBalance)
                    GameEvents.RaiseCoinTick();
                _shownBalance = whole;
                _balance.text = $"Balance {whole}";
            }, target, 0.8f)
            .SetDelay(0.45f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetTarget(this)
            .SetLink(gameObject);
    }

    // Simple UI confetti: pieces burst up from behind the title and tumble down.
    private void PlayConfetti()
    {
        for (int i = 0; i < _confetti.Length; i++)
        {
            Image piece = _confetti[i];
            RectTransform rect = piece.rectTransform;
            rect.DOKill();
            piece.DOKill();
            piece.gameObject.SetActive(true);
            piece.color = ConfettiColors[i % ConfettiColors.Length];
            rect.sizeDelta = new Vector2(Random.Range(14f, 24f), Random.Range(24f, 40f));
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var peak = new Vector2(Random.Range(-480f, 480f), Random.Range(120f, 420f));
            var end = new Vector2(peak.x * 1.2f, Random.Range(-900f, -600f));
            float duration = Random.Range(1.4f, 2.2f);
            DOTween.Sequence()
                .Append(rect.DOAnchorPos(peak, duration * 0.3f).SetEase(Ease.OutQuad))
                .Append(rect.DOAnchorPos(end, duration * 0.7f).SetEase(Ease.InQuad))
                .Join(rect.DORotate(new Vector3(0f, 0f, Random.Range(-540f, 540f)), duration * 0.7f, RotateMode.FastBeyond360))
                .Insert(duration * 0.7f, piece.DOFade(0f, duration * 0.3f))
                .SetDelay(Random.Range(0f, 0.25f))
                .SetUpdate(true)
                .SetLink(piece.gameObject)
                .OnComplete(() => piece.gameObject.SetActive(false));
        }
    }
}
