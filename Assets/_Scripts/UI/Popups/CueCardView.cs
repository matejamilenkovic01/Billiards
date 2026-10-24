using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One cue in the inventory: for sale (buy), Owned (select) or Selected, with its ratings (v3 V11): a big OVR
// badge, AIM / CONTROL / TIMING bars with their numbers, "+X vs your cue", "Recommended for <division>".
// Purchases go through the confirm popup.
public class CueCardView : MonoBehaviour
{
    private const float StatAnimDuration = 0.35f;

    [SerializeField] private Image _frame;
    [SerializeField] private Image _cueImage;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private GameObject _selectedBadge;
    [SerializeField] private TMP_Text _overall;
    [SerializeField] private Image[] _ratingFills;      // AIM, CONTROL, TIMING
    [SerializeField] private TMP_Text[] _ratingValues;
    [SerializeField] private TMP_Text _vsSelected;
    [SerializeField] private GameObject _recommended;
    [SerializeField] private TMP_Text _recommendedText;
    [SerializeField] private UIButton _actionButton;
    [SerializeField] private TMP_Text _actionLabel;
    [SerializeField] private GameObject _actionCoin;
    [SerializeField] private TMP_Text _note;
    [SerializeField] private CanvasGroup _content;
    [SerializeField] private Color _frameNormal = new(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color _frameSelected = new(0.25f, 0.85f, 0.4f, 1f);
    [Tooltip("Label on the grey (disabled) button: dark for contrast.")]
    [SerializeField] private Color _shortColor = new(0.106f, 0.078f, 0.055f, 1f);

    private CueViewData _data;
    private bool _hasData;

    private void OnEnable() => _actionButton.Clicked += HandleAction;

    private void OnDisable() => _actionButton.Clicked -= HandleAction;

    public void Bind(CueViewData data, bool justPurchased)
    {
        bool animate = _hasData && _data.Id == data.Id;
        _data = data;
        _hasData = true;

        _name.text = data.Name;
        if (data.Sprite != null)
            _cueImage.sprite = data.Sprite;
        int[] ratings = { data.Aim, data.Control, data.Timing };
        for (int i = 0; i < _ratingFills.Length; i++)
        {
            SetFill(_ratingFills[i], ratings[i] / 99f, animate);
            _ratingValues[i].text = ratings[i].ToString();
        }
        _overall.text = data.Overall.ToString();
        _vsSelected.gameObject.SetActive(data.OverallVsSelected > 0);
        _vsSelected.text = $"+{data.OverallVsSelected} vs your cue";
        _recommended.SetActive(!string.IsNullOrEmpty(data.RecommendedFor));
        _recommendedText.text = $"Recommended for {data.RecommendedFor}";

        _content.alpha = 1f;
        _frame.color = data.IsSelected ? _frameSelected : _frameNormal;
        _selectedBadge.SetActive(data.IsSelected);

        // Main action per state.
        _note.gameObject.SetActive(false);
        _actionCoin.SetActive(false);
        _actionButton.gameObject.SetActive(!data.IsSelected);
        if (data.State == CueState.ForSale)
        {
            _actionLabel.text = $"BUY · {data.Price}";
            _actionCoin.SetActive(true);
            _actionButton.Interactable = data.CanAfford;
            _actionLabel.color = data.CanAfford ? Color.white : _shortColor;
            if (!data.CanAfford)
            {
                _note.gameObject.SetActive(true);
                _note.text = $"{data.CoinsShort} more coins";
            }
        }
        else if (!data.IsSelected)
        {
            _actionLabel.text = "SELECT";
            _actionLabel.color = Color.white;
            _actionButton.Interactable = true;
        }

        if (justPurchased)
        {
            Transform button = _actionButton.transform;
            button.DOKill();
            button.localScale = Vector3.one;
            button.DOPunchScale(Vector3.one * 0.18f, 0.5f, 6, 0.6f).SetLoops(2).SetUpdate(true).SetLink(button.gameObject);
        }
    }

    private void SetFill(Image bar, float value, bool animate)
    {
        bar.DOKill();
        if (animate)
            bar.DOFillAmount(value, StatAnimDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(bar.gameObject);
        else
            bar.fillAmount = value;
    }

    private void HandleAction()
    {
        if (!_hasData)
            return;

        CueViewData cue = _data;
        if (cue.State == CueState.Owned)
        {
            GameEvents.RaiseCueSelectRequested(cue.Id);
            return;
        }

        PopupManager.Instance.Show<ConfirmPopup>(new ConfirmPopupData(
            "BUY CUE", $"Buy the {cue.Name}?", "BUY", () => GameEvents.RaiseCueBuyRequested(cue.Id), cue.Price));
    }
}
