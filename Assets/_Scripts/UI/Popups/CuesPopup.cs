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
        for (int i = 0; i < _cards.Length; i++)
        {
            bool hasCue = i < snapshot.Cues.Count;
            _cards[i].gameObject.SetActive(hasCue);
            if (hasCue)
                _cards[i].Bind(snapshot.Cues[i], snapshot.Cues[i].Id == snapshot.JustPurchasedId);
        }
    }
}
