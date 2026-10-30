using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The pocket pick (v4 V5, v4 V9.1): while a pocket is called, or blocked / picked for the bot's 8, the camera goes
// up to a bird's-eye view of the whole table and this overlay sits on it: a tappable marker on every pocket (as
// big as a finger needs), a title that says what the pick is for, a "TAP" balloon over the pocket the hit ball
// heads for, a gold ring and a hand on the call, a red ring on the blocked pocket, a gold ring and a lock on the
// picked one. Once the pick is made the camera comes back and only a small POCKET button stays, to change the call
// before the cue is pulled.
public class PocketPickerView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private CanvasGroup _overlay;
    [SerializeField] private RectTransform _overlayArea;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _hint;
    [SerializeField] private RectTransform _panel;          // the POCKET button's container (also the coach mark's target)
    [SerializeField] private UIButton _reopenButton;
    [SerializeField] private RectTransform[] _pockets;
    [SerializeField] private PointerRelay[] _pocketInputs;
    [SerializeField] private Image[] _rings;
    [SerializeField] private Image[] _badges;
    [SerializeField] private RectTransform _tapBubble;
    [SerializeField] private RectTransform _tapArrow;
    [SerializeField] private Sprite _handSprite;
    [SerializeField] private Sprite _blockedSprite;
    [SerializeField] private Sprite _lockSprite;

    private static readonly Color CalledColor = new(1f, 0.82f, 0.29f, 1f);
    private static readonly Color BlockedColor = new(0.9f, 0.25f, 0.2f, 1f);
    private static readonly Color SuggestedColor = new(1f, 1f, 1f, 0.9f);

    private PocketMapInfo _info;
    private int _pulsing = -1;
    private bool _birdseye;
    private bool _bobbing;

    private void Awake()
    {
        _group.alpha = 1f;
        SetOverlay(false);
        _panel.gameObject.SetActive(false);
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
        _reopenButton.Clicked += GameEvents.RaisePocketPickRequested;
    }

    private void OnDisable()
    {
        GameEvents.OnPocketMapChanged -= HandleMapChanged;
        GameEvents.OnPocketCallChanged -= HandlePocketCallChanged;
        GameEvents.OnPocketCallNudged -= HandleNudged;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
        _reopenButton.Clicked -= GameEvents.RaisePocketPickRequested;
    }

    public bool IsShown => _overlay.blocksRaycasts || _panel.gameObject.activeSelf;
    public bool IsBig => _birdseye;

    private void HandleMatchEnded(MatchResult result) => Hide();

    private void Hide()
    {
        SetOverlay(false);
        _panel.gameObject.SetActive(false);
        _birdseye = false;
        StopPulse();
    }

    private void HandleMapChanged(PocketMapInfo info)
    {
        _info = info;
        if (!info.Show)
        {
            Hide();
            return;
        }
        _birdseye = info.Birdseye;
        _panel.gameObject.SetActive(!info.Birdseye && !info.ForOpponent);
        _title.text = info.Title;
        _hint.text = info.ForOpponent ? "Tap a pocket" : "Tap the pocket you are aiming for";
        SetOverlay(info.Birdseye);
        if (info.Birdseye)
            Layout();
    }

    private void HandlePocketCallChanged(int pocket, PocketCallKind kind)
    {
        if (_info == null || kind != PocketCallKind.Called)
            return;
        _info.Called = pocket;
        if (_birdseye)
            Layout();
    }

    private void HandleNudged()
    {
        if (!_panel.gameObject.activeSelf && !_birdseye)
            return;
        RectTransform target = _birdseye ? _overlayArea : _panel;
        target.DOKill(true);
        target.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
        GameEvents.RaiseDisabledButtonClicked();
    }

    private void HandlePocketClicked(int pocket)
    {
        if (_info == null || !_birdseye)
            return;
        GameEvents.RaiseButtonClicked();
        if (_info.ForOpponent)
            GameEvents.RaiseOpponentEightPocketChosen(pocket);
        else
            GameEvents.RaisePocketCallRequested(pocket);
    }

    private void SetOverlay(bool shown)
    {
        _overlay.blocksRaycasts = shown;
        _overlay.interactable = shown;
        _overlay.DOKill();
        _overlay.DOFade(shown ? 1f : 0f, 0.2f).SetUpdate(true).SetLink(gameObject);
        if (!shown)
            StopPulse();
    }

    // Viewport (0..1) -> the overlay's own coordinates.
    private Vector2 ToLocal(Vector2 viewport)
    {
        Vector2 size = _overlayArea.rect.size;
        return new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);
    }

    private void Layout()
    {
        int pulse = -1;
        for (int p = 0; p < _pockets.Length; p++)
        {
            _pockets[p].anchoredPosition = ToLocal(_info.PocketViewport[p]);
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
                Mark(ring, badge, p == _info.Suggested && _info.Called < 0 ? SuggestedColor : new Color(1f, 1f, 1f, 0.5f), null);
            if (p == _info.Suggested && _info.Called < 0 && !_info.ForOpponent)
                pulse = p;
        }
        // The balloon over the pocket the hit ball heads for.
        bool bubble = pulse >= 0;
        _tapBubble.gameObject.SetActive(bubble);
        if (bubble)
        {
            // Above the pocket, or below it for the pockets at the top of the screen (the title is there).
            bool below = _info.PocketViewport[pulse].y > 0.7f;
            _tapBubble.anchoredPosition = _pockets[pulse].anchoredPosition + new Vector2(0f, below ? -125f : 120f);
            _tapArrow.anchorMin = _tapArrow.anchorMax = new Vector2(0.5f, below ? 1f : 0f);
            _tapArrow.anchoredPosition = new Vector2(0f, below ? 30f : -30f);
            _tapArrow.localScale = new Vector3(1f, below ? -1f : 1f, 1f);
            if (!_bobbing)
            {
                _bobbing = true;
                _tapBubble.GetChild(0).DOLocalMoveY(14f, 0.45f).SetRelative().SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(gameObject);
            }
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
        if (_bobbing)
        {
            _tapBubble.GetChild(0).DOKill();
            _tapBubble.GetChild(0).localPosition = Vector3.zero;
            _bobbing = false;
        }
        _tapBubble.gameObject.SetActive(false);
    }
}
