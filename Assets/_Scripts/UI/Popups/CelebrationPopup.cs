using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The big moments (v4 V11), full screen: a new division or Grand Champion (the old badge breaks, the new one comes in
// with rays and confetti in the division's colour, its name, a NEW RULE card, the new venue, the reward counting up)
// and a finished objective set (the bonus). About 3 s; a tap skips to the end, the next tap closes.
public class CelebrationPopup : PopupBase
{
    private const int ConfettiCount = 36;

    [SerializeField] private RectTransform _rays;
    [SerializeField] private Image _glow;
    [SerializeField] private RankBadgeView _oldBadge;
    [SerializeField] private RankBadgeView _newBadge;
    [SerializeField] private RectTransform _trophy;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private CanvasGroup _ruleCard;
    [SerializeField] private Image _ruleIcon;
    [SerializeField] private TMP_Text _ruleText;
    [SerializeField] private CanvasGroup _venueCard;
    [SerializeField] private Image _venueImage;
    [SerializeField] private TMP_Text _venueText;
    [SerializeField] private CanvasGroup _rewardRow;
    [SerializeField] private TMP_Text _rewardText;
    [SerializeField] private TMP_Text _tapHint;
    [SerializeField] private PointerRelay _tapCatcher;
    [SerializeField] private RectTransform _confettiRoot;
    [SerializeField] private Sprite _confettiSprite;
    [SerializeField] private Sprite _anyPocketIcon;
    [SerializeField] private Sprite _calledPocketIcon;
    [SerializeField] private Sprite _blockedPocketIcon;

    private Image[] _confetti;
    private Sequence _sequence;
    private bool _finished;
    private int _shownReward;

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

    private void OnEnable() => _tapCatcher.Clicked += HandleTap;

    private void OnDisable() => _tapCatcher.Clicked -= HandleTap;

    protected override void OnHidden() => _sequence?.Kill();

    private void HandleTap(UnityEngine.EventSystems.PointerEventData _)
    {
        if (!_finished)
            _sequence?.Complete(true);
        else
            Hide();
    }

    protected override void OnShow(object payload)
    {
        if (payload is not CelebrationPopupData data)
            return;
        CelebrationData c = data.Celebration;
        bool division = c.Kind != CelebrationKind.SetComplete;
        Color tint = c.Color;
        _sequence?.Kill();
        _finished = false;

        _title.text = c.Title;
        _title.color = Color.Lerp(tint, Color.white, 0.35f);
        _glow.color = new Color(tint.r, tint.g, tint.b, 0f);
        foreach (Image ray in _rays.GetComponentsInChildren<Image>(true))
            ray.color = new Color(tint.r, tint.g, tint.b, 0.09f);
        _oldBadge.gameObject.SetActive(division);
        _newBadge.gameObject.SetActive(division);
        _trophy.gameObject.SetActive(!division);
        if (division)
        {
            _oldBadge.Bind(c.Before);
            _newBadge.Bind(c.After);
        }
        bool rule = division && !string.IsNullOrEmpty(c.RuleText);
        _ruleText.text = c.RuleText;
        _ruleIcon.sprite = c.Rules switch
        {
            RuleSetId.Standard => _anyPocketIcon,
            RuleSetId.BlockedPocket => _blockedPocketIcon,
            _ => _calledPocketIcon
        };
        _venueImage.sprite = c.VenueThumbnail;
        _venueText.text = division ? c.VenueName : "";
        bool venue = division && c.VenueThumbnail != null;
        _shownReward = 0;
        _rewardText.text = "+0";
        _tapHint.alpha = 0f;

        _rays.localScale = Vector3.zero;
        _rays.localRotation = Quaternion.identity;
        _oldBadge.transform.localScale = Vector3.one;
        _newBadge.transform.localScale = Vector3.zero;
        _trophy.localScale = Vector3.zero;
        _title.transform.localScale = Vector3.zero;
        PrepareCard(_ruleCard, rule);
        PrepareCard(_venueCard, venue);
        PrepareCard(_rewardRow, c.Reward > 0);

        GameEvents.RaiseRankCelebration(c.Kind == CelebrationKind.SetComplete ? RankCelebration.ObjectiveSetComplete : RankCelebration.DivisionUp);
        float t = 0f;
        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        if (division)
        {
            // The old badge shakes and breaks.
            _sequence.Append(_oldBadge.transform.DOShakePosition(0.5f, new Vector3(18f, 0f, 0f), 24).SetUpdate(true));
            _sequence.Append(_oldBadge.transform.DOScale(1.35f, 0.25f).SetEase(Ease.OutQuad));
            _sequence.Join(FadeBadge(_oldBadge, 0.25f));
            _sequence.AppendCallback(() => _oldBadge.gameObject.SetActive(false));
            t = 0.75f;
        }
        _sequence.AppendCallback(() => Burst(tint));
        _sequence.Append(_rays.DOScale(1f, 0.5f).SetEase(Ease.OutCubic));
        _sequence.Join(_glow.DOFade(0.35f, 0.5f));
        _sequence.Join(division ? _newBadge.transform.DOScale(1.35f, 0.55f).SetEase(Ease.OutBack) : _trophy.DOScale(1f, 0.55f).SetEase(Ease.OutBack));
        _sequence.Join(_title.transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.1f));
        if (rule)
            _sequence.Append(ShowCard(_ruleCard));
        if (venue)
            _sequence.Append(ShowCard(_venueCard));
        if (c.Reward > 0)
        {
            _sequence.Append(ShowCard(_rewardRow));
            _sequence.Append(DOTween.To(() => (float)_shownReward, v =>
            {
                int whole = Mathf.RoundToInt(v);
                if (whole != _shownReward)
                    GameEvents.RaiseCoinTick();
                _shownReward = whole;
                _rewardText.text = $"+{whole}";
            }, c.Reward, 0.7f).SetEase(Ease.OutCubic).SetUpdate(true));
        }
        _sequence.Append(_tapHint.DOFade(1f, 0.3f));
        _sequence.OnComplete(() =>
        {
            _finished = true;
            _rewardText.text = $"+{c.Reward}";
        });
        _sequence.OnKill(() => _finished = true);
        _ = t;
        _rays.DORotate(new Vector3(0f, 0f, -360f), 40f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true).SetTarget(this).SetLink(gameObject);
    }

    private static void PrepareCard(CanvasGroup card, bool on)
    {
        card.gameObject.SetActive(on);
        card.alpha = 0f;
        card.transform.localScale = Vector3.one * 0.85f;
    }

    private static Tween ShowCard(CanvasGroup card) =>
        DOTween.Sequence().SetUpdate(true).Append(card.DOFade(1f, 0.3f)).Join(card.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack)).AppendInterval(0.15f);

    private static Tween FadeBadge(RankBadgeView badge, float duration)
    {
        CanvasGroup group = badge.GetComponent<CanvasGroup>();
        if (group == null)
            group = badge.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        return group.DOFade(0f, duration);
    }

    // Confetti in the division's colour and white, bursting up from behind the badge.
    private void Burst(Color tint)
    {
        for (int i = 0; i < _confetti.Length; i++)
        {
            Image piece = _confetti[i];
            RectTransform rect = piece.rectTransform;
            rect.DOKill();
            piece.DOKill();
            piece.gameObject.SetActive(true);
            piece.color = i % 3 == 0 ? Color.white : i % 3 == 1 ? tint : Color.Lerp(tint, Color.white, 0.5f);
            rect.sizeDelta = new Vector2(Random.Range(14f, 24f), Random.Range(24f, 40f));
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            var peak = new Vector2(Random.Range(-480f, 480f), Random.Range(100f, 520f));
            var end = new Vector2(peak.x * 1.2f, Random.Range(-900f, -500f));
            float duration = Random.Range(1.5f, 2.3f);
            DOTween.Sequence()
                .Append(rect.DOAnchorPos(peak, duration * 0.3f).SetEase(Ease.OutQuad))
                .Append(rect.DOAnchorPos(end, duration * 0.7f).SetEase(Ease.InQuad))
                .Join(rect.DORotate(new Vector3(0f, 0f, Random.Range(-540f, 540f)), duration * 0.7f, RotateMode.FastBeyond360))
                .Insert(duration * 0.7f, piece.DOFade(0f, duration * 0.3f))
                .SetUpdate(true).SetLink(piece.gameObject)
                .OnComplete(() => piece.gameObject.SetActive(false));
        }
    }
}
