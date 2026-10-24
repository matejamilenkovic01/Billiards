using DG.Tweening;
using TMPro;
using UnityEngine;

// VS intro (v3 V13, ~2 s before the coin toss): YOU with the rank badge on the left, the CPU character's
// portrait, name, CPU tag and line on the right, the venue below.
public class VsIntroView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _left;
    [SerializeField] private RectTransform _right;
    [SerializeField] private RectTransform _vs;
    [SerializeField] private RankBadgeView _rank;
    [SerializeField] private PortraitView _portrait;
    [SerializeField] private TMP_Text _opponentName;
    [SerializeField] private TMP_Text _opponentLine;
    [SerializeField] private TMP_Text _venue;

    private Sequence _sequence;
    private Vector2 _leftRest;
    private Vector2 _rightRest;

    private void Awake()
    {
        _leftRest = _left.anchoredPosition;
        _rightRest = _right.anchoredPosition;
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    private void OnEnable() => GameEvents.OnMatchStarted += HandleMatchStarted;

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        _sequence?.Kill();
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _sequence?.Kill();
        if (info.VsIntroDuration <= 0f)
        {
            _group.alpha = 0f;
            return;
        }

        _rank.gameObject.SetActive(info.Rank != null);
        _rank.Bind(info.Rank);
        _portrait.Bind(info.Opponent);
        _opponentName.text = info.OpponentName;
        _opponentLine.text = info.Opponent != null ? $"“{info.Opponent.Line}”" : "";
        _venue.text = info.VenueName;

        float hold = Mathf.Max(0.2f, info.VsIntroDuration - 0.9f);
        _left.anchoredPosition = _leftRest + new Vector2(-700f, 0f);
        _right.anchoredPosition = _rightRest + new Vector2(700f, 0f);
        _vs.localScale = Vector3.zero;
        _group.alpha = 0f;
        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(_group.DOFade(1f, 0.2f))
            .Join(_left.DOAnchorPos(_leftRest, 0.4f).SetEase(Ease.OutCubic))
            .Join(_right.DOAnchorPos(_rightRest, 0.4f).SetEase(Ease.OutCubic))
            .Append(_vs.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
            .AppendInterval(hold)
            .Append(_group.DOFade(0f, 0.25f));
    }
}
