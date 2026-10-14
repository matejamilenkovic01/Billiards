using DG.Tweening;
using TMPro;
using UnityEngine;

// Coin icon + balance. Changes count up (or down) with a little pop, ticking as they go.
public class CoinCounterView : MonoBehaviour
{
    private const float CountDuration = 0.6f;

    [SerializeField] private TMP_Text _amount;
    [SerializeField] private RectTransform _icon;

    private int _shown = -1;
    private Tween _tween;

    private void OnEnable() => GameEvents.OnProfileChanged += HandleProfileChanged;

    private void OnDisable()
    {
        GameEvents.OnProfileChanged -= HandleProfileChanged;
        _tween?.Kill();
    }

    private void HandleProfileChanged(ProfileSnapshot snapshot) => Show(snapshot.Coins);

    public void Show(int coins)
    {
        if (_shown < 0 || !isActiveAndEnabled)
        {
            _shown = coins;
            _amount.text = coins.ToString();
            return;
        }
        if (coins == _shown)
            return;

        _tween?.Kill();
        int from = _shown;
        int lastTick = from;
        _shown = coins;
        _tween = DOTween.To(() => (float)from, value =>
            {
                int whole = Mathf.RoundToInt(value);
                _amount.text = whole.ToString();
                if (whole != lastTick)
                {
                    lastTick = whole;
                    GameEvents.RaiseCoinTick();
                }
            }, coins, CountDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(gameObject);

        _icon.DOKill();
        _icon.localScale = Vector3.one;
        _icon.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.5f).SetUpdate(true).SetLink(_icon.gameObject);
    }
}
