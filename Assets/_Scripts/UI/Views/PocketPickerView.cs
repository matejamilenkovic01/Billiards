using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The pocket map (implementation_definition_iteration_v4 V5): the table from above, upright like the phone. Small,
// left above LOOK, while the player's shot must be called: the balls, the aim line, six pocket buttons; the pocket
// the hit ball heads for pulses, the call gets a gold ring and a hand, a blocked pocket a red ring, a picked one a
// gold ring with a lock. Big, in the middle, while the player blocks / picks the pocket for the bot's 8.
public class PocketPickerView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private RectTransform _felt;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private RectTransform[] _balls;
    [SerializeField] private RectTransform _aim;
    [SerializeField] private RectTransform[] _pockets;
    [SerializeField] private PointerRelay[] _pocketInputs;
    [SerializeField] private Image[] _rings;
    [SerializeField] private Image[] _badges;
    [SerializeField] private Sprite _handSprite;
    [SerializeField] private Sprite _blockedSprite;
    [SerializeField] private Sprite _lockSprite;
    [SerializeField] private Vector2 _smallPosition;
    [SerializeField] private Vector2 _smallSize;
    [SerializeField] private Vector2 _bigPosition;
    [SerializeField] private Vector2 _bigSize;
    [Tooltip("Ball dot diameter as a share of the felt's width.")]
    [SerializeField] private float _ballScale = 0.075f;

    private static readonly Color CalledColor = new(1f, 0.82f, 0.29f, 1f);
    private static readonly Color BlockedColor = new(0.9f, 0.25f, 0.2f, 1f);
    private static readonly Color SuggestedColor = new(1f, 1f, 1f, 0.8f);

    private PocketMapInfo _info;
    private int _pulsing = -1;
    private bool _big;

    private void Awake()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        for (int p = 0; p < _pocketInputs.Length; p++)
        {
            int pocket = p;
            _pocketInputs[p].Clicked += _ => HandlePocketClicked(pocket);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnPocketMapChanged += HandleMapChanged;
        GameEvents.OnPocketCallChanged += HandlePocketCallChanged;
        GameEvents.OnPocketCallNudged += HandleNudged;
        GameEvents.OnMatchEnded += HandleMatchEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnPocketMapChanged -= HandleMapChanged;
        GameEvents.OnPocketCallChanged -= HandlePocketCallChanged;
        GameEvents.OnPocketCallNudged -= HandleNudged;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
    }

    public bool IsShown => _group.blocksRaycasts;
    public bool IsBig => _big;

    private void HandleMatchEnded(MatchResult result) => SetShown(false);

    private void HandleMapChanged(PocketMapInfo info)
    {
        _info = info;
        if (!info.Show)
        {
            SetShown(false);
            return;
        }
        _big = info.ForOpponent;
        _panel.anchoredPosition = _big ? _bigPosition : _smallPosition;
        _panel.sizeDelta = _big ? _bigSize : _smallSize;
        _title.gameObject.SetActive(_big);
        _title.text = info.Title;
        Layout();
        SetShown(true);
    }

    private void HandlePocketCallChanged(int pocket, PocketCallKind kind)
    {
        if (_info == null || kind != PocketCallKind.Called)
            return;
        _info.Called = pocket;
        if (IsShown)
            Layout();
    }

    private void HandleNudged()
    {
        if (!IsShown)
            return;
        _panel.DOKill(true);
        _panel.DOPunchScale(Vector3.one * 0.12f, 0.35f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaiseDisabledButtonClicked();
    }

    private void HandlePocketClicked(int pocket)
    {
        if (_info == null || !IsShown)
            return;
        GameEvents.RaiseButtonClicked();
        if (_info.ForOpponent)
            GameEvents.RaiseOpponentEightPocketChosen(pocket);
        else
            GameEvents.RaisePocketCallRequested(pocket);
    }

    private void SetShown(bool shown)
    {
        _group.DOKill();
        _group.blocksRaycasts = shown;
        _group.DOFade(shown ? 1f : 0f, 0.15f).SetUpdate(true).SetLink(gameObject);
        if (!shown)
            StopPulse();
    }

    // Table metres -> felt pixels (x across, y along the table: the rack end up).
    private Vector2 ToMap(Vector2 table)
    {
        Vector2 size = _felt.rect.size;
        return new Vector2(table.x / _info.HalfSize.x * size.x * 0.5f, table.y / _info.HalfSize.y * size.y * 0.5f);
    }

    private void Layout()
    {
        float dot = _felt.rect.width * _ballScale;
        for (int i = 0; i < _balls.Length; i++)
        {
            bool on = _info.OnTable[i];
            _balls[i].gameObject.SetActive(on);
            if (!on)
                continue;
            _balls[i].anchoredPosition = ToMap(_info.Balls[i]);
            _balls[i].sizeDelta = new Vector2(dot, dot);
        }

        _aim.gameObject.SetActive(_info.HasAim && !_info.ForOpponent);
        if (_aim.gameObject.activeSelf)
        {
            Vector2 from = ToMap(_info.AimFrom), to = ToMap(_info.AimTo);
            Vector2 delta = to - from;
            _aim.anchoredPosition = (from + to) * 0.5f;
            _aim.sizeDelta = new Vector2(delta.magnitude, Mathf.Max(2f, dot * 0.18f));
            _aim.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        int pulse = -1;
        for (int p = 0; p < _pockets.Length; p++)
        {
            _pockets[p].anchoredPosition = ToMap(_info.Pockets[p]);
            _pockets[p].localScale = Vector3.one * (_big ? 1.6f : 1f);
            Image ring = _rings[p];
            Image badge = _badges[p];
            bool eight = p == _info.EightPocket;
            bool called = p == _info.Called;
            if (eight && _info.EightPocketKind == PocketCallKind.Blocked)
                Mark(ring, badge, BlockedColor, _blockedSprite);
            else if (eight)
                Mark(ring, badge, CalledColor, _lockSprite);
            else if (called)
                Mark(ring, badge, CalledColor, _handSprite);
            else
                Mark(ring, badge, p == _info.Suggested && _info.Called < 0 ? SuggestedColor : new Color(1f, 1f, 1f, 0.25f), null);
            if (p == _info.Suggested && _info.Called < 0 && !_info.ForOpponent)
                pulse = p;
        }
        SetPulse(pulse);
    }

    private static void Mark(Image ring, Image badge, Color color, Sprite icon)
    {
        ring.color = color;
        badge.gameObject.SetActive(icon != null);
        if (icon != null)
            badge.sprite = icon;
    }

    // The suggested pocket breathes until a pocket is called.
    private void SetPulse(int pocket)
    {
        if (pocket == _pulsing)
            return;
        StopPulse();
        _pulsing = pocket;
        if (pocket >= 0)
            _rings[pocket].transform.DOScale(1.25f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject);
    }

    private void StopPulse()
    {
        if (_pulsing >= 0)
        {
            _rings[_pulsing].transform.DOKill();
            _rings[_pulsing].transform.localScale = Vector3.one;
        }
        _pulsing = -1;
    }
}
