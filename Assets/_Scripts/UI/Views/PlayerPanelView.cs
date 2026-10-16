using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One player's strip at the top (YOU or OPPONENT): their 7 balls (grey "?" while the table is open),
// pocketed ones greyed with a check, balls left, whose turn it is, and the 8-ball once the group is cleared.
public class PlayerPanelView : MonoBehaviour
{
    [SerializeField] private bool _isPlayer = true;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private Image _glow;
    [SerializeField] private RectTransform _arrow;
    [SerializeField] private TMP_Text _groupText;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private TMP_Text _thinking;
    [SerializeField] private RectTransform _slotRow;
    [SerializeField] private Image[] _slots;
    [SerializeField] private Image[] _checks;
    [SerializeField] private Image _eightIcon;
    [SerializeField] private Sprite[] _ballSprites;
    [SerializeField] private Sprite _unknownSprite;
    [SerializeField] private Color _activeColor = new(0.3f, 0.9f, 0.45f, 1f);

    private readonly bool[] _pocketed = new bool[BallGroups.BallCount];
    private BallGroup _group;
    private bool _onEight;
    private Tween _thinkingTween;
    private Vector2 _arrowRest;

    private void Awake()
    {
        _arrowRest = _arrow.anchoredPosition;
        ResetPanel();
    }

    private void OnEnable()
    {
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnGroupsAssigned += HandleGroupsAssigned;
        GameEvents.OnBallPocketed += HandleBallPocketed;
        GameEvents.OnShotFired += HandleShotFired;
        GameEvents.OnMatchEnded += HandleMatchEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnGroupsAssigned -= HandleGroupsAssigned;
        GameEvents.OnBallPocketed -= HandleBallPocketed;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
        _thinkingTween?.Kill();
    }

    private bool _twoPlayers;
    private string _myName = "YOU";
    private string _otherName = "OPPONENT";

    private void HandleMatchStarted(MatchStartInfo info)
    {
        _twoPlayers = info.IsTwoPlayers;
        _myName = _isPlayer ? info.PlayerName : info.OpponentName;
        _otherName = _isPlayer ? info.OpponentName : info.PlayerName;
        _name.text = _myName;
        ResetPanel();
    }

    private void ResetPanel()
    {
        System.Array.Clear(_pocketed, 0, _pocketed.Length);
        _group = BallGroup.Open;
        _onEight = false;
        ShowSlots(false);
        SetActive(false);
        SetThinking(false);
        _eightIcon.gameObject.SetActive(false);
        _slotRow.localScale = Vector3.one;
    }

    private void HandleTurnStarted(TurnInfo info)
    {
        System.Array.Copy(info.Pocketed, _pocketed, _pocketed.Length);
        BallGroup group = _isPlayer ? info.PlayerGroup : info.BotGroup;
        if (group != _group)
        {
            _group = group;
            ShowSlots(false);
        }
        RefreshSlots();
        SetOnEight(_isPlayer ? info.PlayerOnEight : info.BotOnEight, false);

        bool mine = info.IsPlayer == _isPlayer;
        SetActive(mine);
        SetThinking(mine && !info.IsHuman);
    }

    private void HandleGroupsAssigned(BallGroup player, BallGroup bot)
    {
        _group = _isPlayer ? player : bot;
        ShowSlots(true);
        string group = _group == BallGroup.Solids ? "SOLIDS" : "STRIPES";
        if (_isPlayer)
            GameEvents.RaiseToastRequested(new ToastMessage(_twoPlayers ? $"{_myName}: {group}  ·  {_otherName}: {(_group == BallGroup.Solids ? "STRIPES" : "SOLIDS")}" : $"You are {group}", ToastStyle.Good));
    }

    private void HandleBallPocketed(int number, int pocketIndex)
    {
        if (number <= 0 || number >= _pocketed.Length || !BallGroups.Contains(_group, number))
            return;

        _pocketed[number] = true;
        int slot = number - BallGroups.FirstNumber(_group);
        RefreshSlot(slot);
        _slots[slot].transform.DOKill();
        _slots[slot].transform.localScale = Vector3.one;
        _slots[slot].transform.DOPunchScale(Vector3.one * 0.35f, 0.35f, 6, 0.5f).SetUpdate(true).SetLink(_slots[slot].gameObject);
        UpdateCount();
        if (Remaining() == 0)
        {
            SetOnEight(true, true);
            if (_isPlayer || _twoPlayers)
                GameEvents.RaiseToastRequested(new ToastMessage(_twoPlayers ? $"{_myName}: pocket the 8-ball to win!" : "Pocket the 8-ball to win!", ToastStyle.Good));
        }
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (!feedback.IsPlayer)
            SetThinking(false);
    }

    private void HandleMatchEnded(MatchResult result)
    {
        SetActive(false);
        SetThinking(false);
    }

    // Open table: grey "?" balls. Once assigned, the group's balls drop into their slots one after another.
    private void ShowSlots(bool animate)
    {
        _groupText.text = _group switch
        {
            BallGroup.Solids => "SOLIDS",
            BallGroup.Stripes => "STRIPES",
            _ => "OPEN TABLE"
        };
        for (int i = 0; i < _slots.Length; i++)
        {
            RefreshSlot(i);
            Transform slot = _slots[i].transform;
            slot.DOKill();
            slot.localScale = Vector3.one;
            if (animate)
            {
                slot.localScale = Vector3.zero;
                slot.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetDelay(i * 0.05f).SetUpdate(true).SetLink(slot.gameObject);
            }
        }
        UpdateCount();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < _slots.Length; i++)
            RefreshSlot(i);
        UpdateCount();
    }

    private void RefreshSlot(int index)
    {
        if (_group == BallGroup.Open)
        {
            _slots[index].sprite = _unknownSprite;
            _slots[index].color = new Color(1f, 1f, 1f, 0.55f);
            _checks[index].enabled = false;
            return;
        }

        int number = BallGroups.FirstNumber(_group) + index;
        bool down = _pocketed[number];
        _slots[index].sprite = _ballSprites[number];
        _slots[index].color = down ? new Color(0.5f, 0.5f, 0.5f, 0.45f) : Color.white;
        _checks[index].enabled = down;
    }

    private int Remaining()
    {
        if (_group == BallGroup.Open)
            return 7;
        int first = BallGroups.FirstNumber(_group);
        int count = 0;
        for (int n = first; n < first + 7; n++)
        {
            if (!_pocketed[n])
                count++;
        }
        return count;
    }

    private void UpdateCount()
    {
        int left = Remaining();
        _countText.text = _group == BallGroup.Open ? "" : left == 0 ? "8-BALL" : $"{left} left";
    }

    private void SetOnEight(bool onEight, bool animate)
    {
        if (onEight == _onEight)
            return;
        _onEight = onEight;
        _eightIcon.gameObject.SetActive(onEight);
        _slotRow.DOKill();
        _eightIcon.transform.DOKill();
        if (!onEight)
        {
            _slotRow.localScale = Vector3.one;
            return;
        }

        if (animate)
            _slotRow.DOScale(0.8f, 0.3f).SetUpdate(true).SetLink(_slotRow.gameObject);
        else
            _slotRow.localScale = Vector3.one * 0.8f;
        _eightIcon.transform.localScale = Vector3.one;
        _eightIcon.transform.DOScale(1.25f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(_eightIcon.gameObject);
    }

    private void SetActive(bool active)
    {
        _glow.DOKill();
        _arrow.DOKill();
        _arrow.anchoredPosition = _arrowRest;
        _arrow.gameObject.SetActive(active);
        if (!active)
        {
            _glow.color = new Color(_activeColor.r, _activeColor.g, _activeColor.b, 0f);
            return;
        }

        _glow.color = new Color(_activeColor.r, _activeColor.g, _activeColor.b, 1f);
        _glow.DOFade(0.45f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(_glow.gameObject);
        _arrow.DOAnchorPosY(_arrowRest.y - 10f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(_arrow.gameObject);
    }

    // "Thinking..." with dots that come and go, while the opponent decides.
    private void SetThinking(bool thinking)
    {
        _thinkingTween?.Kill();
        _thinking.gameObject.SetActive(thinking);
        if (!thinking)
            return;
        int dots = 0;
        _thinking.text = "Thinking";
        _thinkingTween = DOVirtual.DelayedCall(0.35f, () =>
            {
                dots = (dots + 1) % 4;
                _thinking.text = "Thinking" + new string('.', dots);
            }, false)
            .SetLoops(-1)
            .SetLink(gameObject);
    }
}
