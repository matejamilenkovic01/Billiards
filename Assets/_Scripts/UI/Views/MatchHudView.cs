using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Match HUD shell: pause button and the centre 8-ball that lights up when the player is down to the 8.
public class MatchHudView : MonoBehaviour
{
    [SerializeField] private UIButton _pauseButton;
    [SerializeField] private Image _centerEight;

    private bool _playerOnEight;

    private void OnEnable()
    {
        _pauseButton.Clicked += GameEvents.RaisePauseRequested;
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnMatchEnded += HandleMatchEnded;
    }

    private void OnDisable()
    {
        _pauseButton.Clicked -= GameEvents.RaisePauseRequested;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _pauseButton.gameObject.SetActive(true);
        SetPlayerOnEight(false);
    }

    private void HandleTurnStarted(TurnInfo info) => SetPlayerOnEight(info.PlayerOnEight);

    private void HandleMatchEnded(MatchResult result) => _pauseButton.gameObject.SetActive(false);

    private void SetPlayerOnEight(bool onEight)
    {
        if (onEight == _playerOnEight && _centerEight.color.a > 0f)
            return;
        _playerOnEight = onEight;
        Transform icon = _centerEight.transform;
        icon.DOKill();
        _centerEight.DOKill();
        icon.localScale = Vector3.one;
        _centerEight.color = new Color(1f, 1f, 1f, onEight ? 1f : 0.35f);
        if (onEight)
            icon.DOScale(1.2f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(icon.gameObject);
    }
}
