using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// End of the match (v3 V13): YOU WIN! (gold, confetti) or YOU LOSE and why; the rank badge animates the stars
// (gained, the hot-streak bonus, lost or kept), a stage step, a CHECKPOINT stamp with the next venue; the coins
// count into the balance; finished objectives can be claimed here; a cue that just became affordable.
public class MatchResultPopup : PopupBase
{
    private const int ConfettiCount = 40;
    private const float StarStep = 0.35f;

    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _reason;
    [SerializeField] private RankBadgeView _rank;
    [SerializeField] private TMP_Text _rankChange;
    [SerializeField] private RectTransform _stamp;
    [SerializeField] private TMP_Text _stampTitle;
    [SerializeField] private TMP_Text _stampSub;
    [SerializeField] private TMP_Text _delta;
    [SerializeField] private TMP_Text _balance;
    [SerializeField] private GameObject _coinRow;
    [SerializeField] private GameObject _objectivesHeader;
    [SerializeField] private ObjectiveRowView[] _objectives;
    [SerializeField] private GameObject _cueCard;
    [SerializeField] private TMP_Text _cueText;
    [SerializeField] private UIButton _seeCuesButton;
    [Tooltip("WATCH AD · KEEP YOUR STAR / DOUBLE COINS (simulated rewarded, v3 V12).")]
    [SerializeField] private UIButton _rewardedButton;
    [SerializeField] private TMP_Text _rewardedLabel;
    [SerializeField] private TMP_Text _playAgainLabel;
    [SerializeField] private UIButton _playAgainButton;
    [SerializeField] private UIButton _menuButton;
    [SerializeField] private RectTransform _confettiRoot;
    [SerializeField] private Sprite _confettiSprite;
    [SerializeField] private Color _winColor = new(0.97f, 0.77f, 0.27f);
    [SerializeField] private Color _loseColor = new(0.78f, 0.33f, 0.3f);
    [SerializeField] private Color _goodColor = new(0.5f, 0.9f, 0.55f);

    private Image[] _confetti;
    private int _shownBalance;
    private MatchResult _result;
    private RewardedPlacement _placement;

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
        _rewardedButton.Clicked += HandleRewarded;
        GameEvents.OnRewardedAdFinished += HandleRewardedFinished;
    }

    private void OnDisable()
    {
        _playAgainButton.Clicked -= GameEvents.RaisePlayRequested;
        _menuButton.Clicked -= GameEvents.RaiseMenuRequested;
        _seeCuesButton.Clicked -= GameEvents.RaiseCuesRequested;
        _rewardedButton.Clicked -= HandleRewarded;
        GameEvents.OnRewardedAdFinished -= HandleRewardedFinished;
    }

    private void HandleRewarded() => GameEvents.RaiseRewardedAdRequested(_placement);

    // The ad was watched: the star comes back, or the coins double; the offer goes.
    private void HandleRewardedFinished(RewardedPlacement placement, bool rewarded)
    {
        if (!rewarded || _result == null || placement != _placement || !_rewardedButton.gameObject.activeSelf)
            return;
        _rewardedButton.gameObject.SetActive(false);
        if (placement == RewardedPlacement.StarShield)
        {
            DOTween.Kill(this);
            _rank.Bind(_result.RankBeforeView);
            _rank.transform.DOKill();
            _rank.transform.localScale = Vector3.one;
            _rank.transform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 6, 0.5f).SetUpdate(true).SetLink(_rank.gameObject);
            _rankChange.text = "STAR KEPT!";
            _rankChange.color = _goodColor;
            _rankChange.alpha = 1f;
            GameEvents.RaiseRankCelebration(RankCelebration.StarGained);
        }
        else
        {
            int added = _result.CoinDelta;
            _delta.text = $"+{_result.CoinDelta + added}";
            _delta.transform.DOKill();
            _delta.transform.localScale = Vector3.one;
            _delta.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 6, 0.5f).SetUpdate(true).SetLink(_delta.gameObject);
            int from = _shownBalance;
            DOTween.To(() => (float)from, value =>
                {
                    int whole = Mathf.RoundToInt(value);
                    if (whole != _shownBalance)
                        GameEvents.RaiseCoinTick();
                    _shownBalance = whole;
                    _balance.text = $"Balance {whole}";
                }, _result.NewBalance + added, 0.8f)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
        }
    }

    protected override void OnShow(object payload)
    {
        if (payload is not MatchResultPopupData data)
            return;

        MatchResult result = data.Result;
        _result = result;
        _placement = result.Won ? RewardedPlacement.DoubleCoins : RewardedPlacement.StarShield;
        bool offer = result.Won ? result.DoubleCoinsAvailable : result.StarShieldAvailable;
        _rewardedButton.gameObject.SetActive(offer);
        _rewardedLabel.text = result.Won ? "WATCH AD · DOUBLE COINS" : "WATCH AD · KEEP YOUR STAR";
        _title.text = result.Title;
        _title.color = result.Won ? _winColor : _loseColor;
        _reason.text = result.Reason;
        _playAgainLabel.text = "PLAY AGAIN";
        _coinRow.SetActive(true);
        _delta.text = result.CoinDelta > 0 ? $"+{result.CoinDelta}" : result.CoinDelta.ToString();
        _delta.color = result.CoinDelta > 0 ? _winColor : _loseColor;

        for (int i = 0; i < _objectives.Length; i++)
            _objectives[i].Bind(i < result.ObjectivesCompleted.Count ? result.ObjectivesCompleted[i] : null);
        _objectivesHeader.SetActive(result.ObjectivesCompleted.Count > 0);

        // v4 V8: three objective rows fill the space; the cue card waits for a result with fewer.
        _cueCard.SetActive(!string.IsNullOrEmpty(result.CueAffordable) && result.ObjectivesCompleted.Count < _objectives.Length);
        _cueText.text = $"You can buy the {result.CueAffordable}!";

        _title.transform.DOKill();
        _title.transform.localScale = Vector3.one * 0.6f;
        _title.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetDelay(0.1f).SetUpdate(true).SetLink(_title.gameObject);
        if (result.Won)
            PlayConfetti();

        DOTween.Kill(this);
        PlayRank(result);

        // Balance counts from the old value to the new one once the panel has popped in.
        _shownBalance = data.PreviousBalance;
        _balance.text = $"Balance {_shownBalance}";
        DOTween.To(() => (float)_shownBalance, value =>
            {
                int whole = Mathf.RoundToInt(value);
                if (whole != _shownBalance)
                    GameEvents.RaiseCoinTick();
                _shownBalance = whole;
                _balance.text = $"Balance {whole}";
            }, result.NewBalance, 0.8f)
            .SetDelay(0.45f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetTarget(this)
            .SetLink(gameObject);

        if (result.ObjectivesCompleted.Count > 0)
            DOVirtual.DelayedCall(1.6f, () => GameEvents.RaiseRankCelebration(RankCelebration.ObjectiveDone), true).SetTarget(this).SetLink(gameObject);
    }

    #region Rank
    // The stars move one by one from where the player stood to where they stand now.
    private void PlayRank(MatchResult result)
    {
        _stamp.gameObject.SetActive(false);
        RankViewData before = result.RankBeforeView;
        RankViewData after = result.RankAfterView;
        _rank.gameObject.SetActive(after != null);
        if (after == null)
        {
            _rankChange.text = "";
            return;
        }

        _rank.Bind(before, before.Stars);
        _rankChange.text = ChangeText(result);
        _rankChange.color = result.StarsGained > 0 ? _goodColor : result.StarsGained < 0 ? _loseColor : Color.white;
        _rankChange.alpha = 0f;

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this).SetLink(gameObject).AppendInterval(0.6f);
        sequence.Append(_rankChange.DOFade(1f, 0.25f));

        if (result.StarsGained > 0)
        {
            bool stepped = result.StageUp || result.NewDivision;
            int fillTo = stepped ? before.StarsPerStage : after.Stars;
            if (before.IsChampion)
            {
                sequence.AppendCallback(() => { _rank.Bind(after); GameEvents.RaiseRankCelebration(RankCelebration.StarGained); });
            }
            else
            {
                for (int i = before.Stars; i < fillTo; i++)
                    AppendStar(sequence, i, true);
                if (stepped)
                {
                    sequence.AppendInterval(0.15f);
                    sequence.AppendCallback(() =>
                    {
                        _rank.Bind(after, 0);
                        _rank.transform.DOKill();
                        _rank.transform.localScale = Vector3.one;
                        _rank.transform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 6, 0.5f).SetUpdate(true).SetLink(_rank.gameObject);
                        GameEvents.RaiseRankCelebration(result.NewDivision ? RankCelebration.Checkpoint : RankCelebration.StageUp);
                    });
                    if (result.NewDivision)
                        AppendStamp(sequence, after, result.NewVenueName);
                    for (int i = 0; i < after.Stars && !after.IsChampion; i++)
                        AppendStar(sequence, i, true);
                }
            }
        }
        else if (result.StarsGained < 0)
        {
            if (result.StageDown)
            {
                sequence.AppendCallback(() =>
                {
                    _rank.Bind(after);
                    _rank.transform.DOKill();
                    _rank.transform.DOShakePosition(0.4f, new Vector3(14f, 0f, 0f), 18).SetUpdate(true).SetLink(_rank.gameObject);
                    GameEvents.RaiseRankCelebration(RankCelebration.StarLost);
                });
            }
            else if (before.IsChampion)
            {
                sequence.AppendCallback(() => { _rank.Bind(after); GameEvents.RaiseRankCelebration(RankCelebration.StarLost); });
            }
            else
            {
                AppendStar(sequence, before.Stars - 1, false);
            }
        }
    }

    private void AppendStar(Sequence sequence, int index, bool gain)
    {
        Image[] stars = _rank.Stars;
        if (index < 0 || index >= stars.Length)
            return;
        Image star = stars[index];
        sequence.AppendCallback(() =>
        {
            star.DOKill();
            star.transform.DOKill();
            star.color = gain ? _rank.StarOn : _rank.StarOff;
            star.transform.localScale = Vector3.one * (gain ? 1.8f : 1f);
            if (gain)
                star.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(star.gameObject);
            else
                star.transform.DOShakeRotation(0.35f, new Vector3(0f, 0f, 40f), 20).SetUpdate(true).SetLink(star.gameObject);
            GameEvents.RaiseRankCelebration(gain ? RankCelebration.StarGained : RankCelebration.StarLost);
        });
        sequence.AppendInterval(StarStep);
    }

    private void AppendStamp(Sequence sequence, RankViewData after, string venue)
    {
        sequence.AppendCallback(() =>
        {
            _stamp.gameObject.SetActive(true);
            _stampTitle.text = after.IsChampion ? "GRAND CHAMPION" : $"CHECKPOINT · {after.DivisionName.ToUpperInvariant()}";
            _stampSub.text = after.IsChampion ? "Every win adds a star" : $"New venue: {venue}";
            _stamp.localScale = Vector3.one * 2.2f;
            _stamp.localRotation = Quaternion.Euler(0f, 0f, -8f);
            var group = _stamp.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.DOFade(1f, 0.2f).SetUpdate(true).SetLink(_stamp.gameObject);
            _stamp.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(_stamp.gameObject);
            PlayConfetti();
        });
        // The stamp lifts after a moment so the new rank underneath reads.
        sequence.AppendInterval(1.4f);
        sequence.AppendCallback(() => _stamp.GetComponent<CanvasGroup>().DOFade(0f, 0.3f).SetUpdate(true).SetLink(_stamp.gameObject));
        sequence.AppendInterval(0.3f);
    }

    private static string ChangeText(MatchResult result)
    {
        if (result.StarsGained > 0)
        {
            string stars = $"+{result.StarsGained} ★";
            if (result.NewDivision)
                return stars + "  ·  NEW DIVISION!";
            if (result.StageUp)
                return stars + "  ·  STAGE UP!";
            return result.StreakBonus ? stars + "  ·  HOT STREAK" : stars;
        }
        if (result.StarsGained < 0)
            return result.StageDown ? "-1 ★  ·  STAGE DOWN" : "-1 ★";
        return result.Won ? "" : "NO STAR LOST";
    }
    #endregion

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
