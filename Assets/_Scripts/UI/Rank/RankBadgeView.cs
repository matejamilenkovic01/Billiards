using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The rank badge (v3 V12): division shield in its colour, "SILVER II", the stage's stars and the next step
// ("2 ★ to SILVER I", "3 ★ to GOLD · checkpoint"). On the menu it follows the profile and opens DIVISIONS;
// elsewhere (VS intro, results) it is bound by hand.
public class RankBadgeView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image _badge;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image[] _stars;
    [SerializeField] private TMP_Text _championStars;
    [SerializeField] private TMP_Text _next;
    [Tooltip("NEW: a division the player has not looked at on DIVISIONS yet.")]
    [SerializeField] private GameObject _newChip;
    [SerializeField] private bool _followProfile;
    [SerializeField] private bool _opensRoad;
    [SerializeField] private Color _starOn = new(1f, 0.82f, 0.26f, 1f);
    [SerializeField] private Color _starOff = new(1f, 1f, 1f, 0.18f);

    public Image[] Stars => _stars;
    public Color StarOn => _starOn;
    public Color StarOff => _starOff;

    private void OnEnable()
    {
        if (_followProfile)
            GameEvents.OnProfileChanged += HandleProfileChanged;
    }

    private void OnDisable()
    {
        if (_followProfile)
            GameEvents.OnProfileChanged -= HandleProfileChanged;
    }

    private void HandleProfileChanged(ProfileSnapshot snapshot) => Bind(snapshot.Rank);

    public void Bind(RankViewData rank) => Bind(rank, rank != null ? rank.Stars : 0);

    // `stars` lets the results screen show the stars before its animation fills or empties them.
    public void Bind(RankViewData rank, int stars)
    {
        if (rank == null)
            return;
        _badge.color = rank.Color;
        _label.text = rank.Label;
        for (int i = 0; i < _stars.Length; i++)
        {
            bool used = !rank.IsChampion && i < rank.StarsPerStage;
            _stars[i].gameObject.SetActive(used);
            _stars[i].color = i < stars ? _starOn : _starOff;
            _stars[i].transform.localScale = Vector3.one;
        }
        if (_championStars != null)
        {
            _championStars.gameObject.SetActive(rank.IsChampion);
            _championStars.text = $"★ {rank.ChampionStars}";
        }
        if (_next != null)
            _next.text = NextText(rank);
        if (_newChip != null)
            _newChip.SetActive(rank.IsNew && _opensRoad);
    }

    public static string NextText(RankViewData rank)
    {
        if (rank.IsChampion)
            return "Every win adds a star";
        string stars = $"{rank.StarsToNext} ★";
        return rank.NextIsCheckpoint ? $"{stars} to {rank.NextLabel} · checkpoint" : $"{stars} to {rank.NextLabel}";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_opensRoad)
            return;
        GameEvents.RaiseButtonClicked();
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * -0.05f, 0.25f, 6, 0.5f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaiseDivisionsRequested();
    }
}
