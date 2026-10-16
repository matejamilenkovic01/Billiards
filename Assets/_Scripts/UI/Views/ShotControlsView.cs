using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Which shot controls are on screen, per phase of the player's turn (implementation_definition 4.7.2-4.7.4).
public class ShotControlsView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private UIButton _lookButton;
    [SerializeField] private TMP_Text _lookLabel;
    [SerializeField] private Image _lookIcon;
    [SerializeField] private Sprite _lookSprite;
    [SerializeField] private Sprite _backSprite;
    [SerializeField] private UIButton _moveBallButton;
    [SerializeField] private UIButton _doneButton;
    [SerializeField] private TMP_Text _hint;
    [SerializeField] private GameObject _powerStrip;
    [SerializeField] private GameObject _fineAim;

    private bool _isPlayerTurn;
    private bool _ballInHand;
    private bool _kitchenOnly;
    private TurnPhase _phase;

    private void Awake() => Apply(TurnPhase.None);

    private void OnEnable()
    {
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        GameEvents.OnMatchEnded += HandleMatchEnded;
        _lookButton.Clicked += HandleLookClicked;
        _moveBallButton.Clicked += GameEvents.RaiseMoveCueBallRequested;
        _doneButton.Clicked += GameEvents.RaisePlaceCueBallConfirmed;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
        _lookButton.Clicked -= HandleLookClicked;
        _moveBallButton.Clicked -= GameEvents.RaiseMoveCueBallRequested;
        _doneButton.Clicked -= GameEvents.RaisePlaceCueBallConfirmed;
    }

    private void HandleTurnStarted(TurnInfo info)
    {
        _isPlayerTurn = info.IsPlayer;
        _ballInHand = info.BallInHand;
        _kitchenOnly = info.KitchenOnly;
        // Listener order isn't guaranteed: the shot controller may already have announced this turn's phase.
        Apply(info.IsPlayer ? _phase : TurnPhase.None);
    }

    private void HandleTurnPhaseChanged(TurnPhase phase) => Apply(phase);

    private void HandleMatchEnded(MatchResult result) => Apply(TurnPhase.None);

    private void HandleLookClicked() => GameEvents.RaiseLookModeToggled(_phase != TurnPhase.Look);

    private void Apply(TurnPhase phase)
    {
        _phase = phase;
        bool show = _isPlayerTurn && phase is TurnPhase.PlaceCueBall or TurnPhase.Aim or TurnPhase.Look or TurnPhase.Pulling;
        _group.DOKill();
        _group.DOFade(show ? 1f : 0f, 0.2f).SetUpdate(true).SetLink(gameObject);
        _group.blocksRaycasts = show;
        if (!show)
            return;

        bool aiming = phase == TurnPhase.Aim;
        bool looking = phase == TurnPhase.Look;
        bool pulling = phase == TurnPhase.Pulling;
        bool placing = phase == TurnPhase.PlaceCueBall;

        _powerStrip.SetActive(aiming || pulling);
        _fineAim.SetActive(aiming);
        _lookButton.gameObject.SetActive(aiming || looking);
        _lookLabel.text = looking ? "BACK TO SHOT" : "LOOK";
        _lookLabel.fontSize = looking ? 31f : 40f;
        _lookIcon.sprite = looking ? _backSprite : _lookSprite;
        _moveBallButton.gameObject.SetActive(aiming && _ballInHand);
        _doneButton.gameObject.SetActive(placing);

        _hint.gameObject.SetActive(placing || looking);
        _hint.text = placing
            ? (_kitchenOnly ? "Place the cue ball behind the line" : "Drag to place the cue ball")
            : "Drag to look. Tap a ball to aim.";
    }
}
