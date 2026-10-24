using UnityEngine;
using UnityEngine.UI;

// The cue inventory. Stays open through purchases: every OnProfileChanged re-binds the cards in place.
public class CuesPopup : PopupBase
{
    [SerializeField] private CueCardView[] _cards;
    [SerializeField] private CoinCounterView _coins;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private UIButton _closeButton;

    private void OnEnable()
    {
        _closeButton.Clicked += Hide;
        GameEvents.OnProfileChanged += HandleProfileChanged;
    }

    private void OnDisable()
    {
        _closeButton.Clicked -= Hide;
        GameEvents.OnProfileChanged -= HandleProfileChanged;
    }

    protected override void OnShow(object payload)
    {
        if (payload is not CuesPopupData data)
            return;

        Bind(data.Profile);
        _scroll.verticalNormalizedPosition = 1f;
    }

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        if (IsShown)
            Bind(snapshot);
    }

    private void Bind(ProfileSnapshot snapshot)
    {
        _coins.Show(snapshot.Coins);
        // Sorted by OVR (v3 V13); every cue has its own OVR, so cards keep their place between rebinds.
        var cues = new System.Collections.Generic.List<CueViewData>(snapshot.Cues);
        cues.Sort((a, b) => a.Overall.CompareTo(b.Overall));
        for (int i = 0; i < _cards.Length; i++)
        {
            bool hasCue = i < cues.Count;
            _cards[i].gameObject.SetActive(hasCue);
            if (hasCue)
                _cards[i].Bind(cues[i], cues[i].Id == snapshot.JustPurchasedId);
        }
    }
}
