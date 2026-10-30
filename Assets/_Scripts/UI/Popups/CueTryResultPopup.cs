using TMPro;
using UnityEngine;
using UnityEngine.UI;

// After the three try shots (v4 V9): the tried cue's bars beside the selected cue's, how many releases were PERFECT /
// GOOD, then BUY (or what is missing), PLAY (the menu) and BACK TO CUES. A purchase re-binds the screen in place.
public class CueTryResultPopup : PopupBase
{
    [SerializeField] private TMP_Text _triedName;
    [SerializeField] private TMP_Text _selectedName;
    [SerializeField] private Image _triedCue;
    [SerializeField] private Image _selectedCue;
    [SerializeField] private TMP_Text _triedOverall;
    [SerializeField] private TMP_Text _selectedOverall;
    [SerializeField] private Image[] _triedFills;       // AIM, CONTROL, TIMING, POWER
    [SerializeField] private Image[] _selectedFills;
    [SerializeField] private TMP_Text[] _triedValues;
    [SerializeField] private TMP_Text[] _selectedValues;
    [SerializeField] private TMP_Text _releases;
    [SerializeField] private UIButton _buyButton;
    [SerializeField] private TMP_Text _buyLabel;
    [SerializeField] private GameObject _buyCoin;
    [SerializeField] private TMP_Text _note;
    [SerializeField] private UIButton _playButton;
    [SerializeField] private UIButton _cuesButton;
    [SerializeField] private Color _shortColor = new(0.106f, 0.078f, 0.055f, 1f);

    private CueTryResultData _data;

    private void OnEnable()
    {
        _buyButton.Clicked += HandleBuy;
        _playButton.Clicked += HandlePlay;
        _cuesButton.Clicked += HandleCues;
        GameEvents.OnProfileChanged += HandleProfileChanged;
    }

    private void OnDisable()
    {
        _buyButton.Clicked -= HandleBuy;
        _playButton.Clicked -= HandlePlay;
        _cuesButton.Clicked -= HandleCues;
        GameEvents.OnProfileChanged -= HandleProfileChanged;
    }

    public override void HandleBack() => HandleCues();

    protected override void OnShow(object payload)
    {
        if (payload is CueTryResultPopupData data)
            Bind(data.Result);
    }

    // Bought here: the tried cue is now owned (BUY becomes SELECT's job in the cues).
    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        if (!IsShown || _data == null)
            return;
        _data.Tried = snapshot.Find(_data.Tried.Id);
        _data.Selected = snapshot.SelectedCue;
        Bind(_data);
    }

    private void Bind(CueTryResultData data)
    {
        _data = data;
        Fill(data.Tried, _triedName, _triedCue, _triedOverall, _triedFills, _triedValues);
        Fill(data.Selected, _selectedName, _selectedCue, _selectedOverall, _selectedFills, _selectedValues);
        _releases.text = $"PERFECT {data.Perfect} · GOOD {data.Good} · {data.Shots} {(data.Shots == 1 ? "shot" : "shots")}";

        bool forSale = data.Tried.State == CueState.ForSale;
        _buyButton.gameObject.SetActive(forSale && data.Tried.CanAfford);
        _buyLabel.text = $"BUY · {data.Tried.Price}";
        _buyCoin.SetActive(true);
        _note.gameObject.SetActive(forSale && !data.Tried.CanAfford);
        _note.text = $"{data.Tried.CoinsShort} more coins to buy it";
    }

    private static void Fill(CueViewData cue, TMP_Text name, Image image, TMP_Text overall, Image[] fills, TMP_Text[] values)
    {
        name.text = cue.Name;
        if (cue.Sprite != null)
            image.sprite = cue.Sprite;
        overall.text = $"OVR {cue.Overall}";
        int[] ratings = { cue.Aim, cue.Control, cue.Timing, cue.Power };
        for (int i = 0; i < fills.Length; i++)
        {
            fills[i].fillAmount = ratings[i] / 99f;
            values[i].text = ratings[i].ToString();
        }
    }

    private void HandleBuy()
    {
        CueViewData cue = _data.Tried;
        PopupManager.Instance.Show<ConfirmPopup>(new ConfirmPopupData(
            "BUY CUE", $"Buy the {cue.Name}?", "BUY", () => GameEvents.RaiseCueBuyRequested(cue.Id), cue.Price));
    }

    private void HandlePlay() => GameEvents.RaisePracticeLeaveRequested(false);

    private void HandleCues() => GameEvents.RaisePracticeLeaveRequested(true);
}
