using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TooltipManager : SingletonPersistent<TooltipManager>
{
    private const float AutoHideDelay = 2.5f;
    private const float AnchorGap = 12f;
    private const float ScreenMargin = 16f;
    // A click that closed the tooltip on press must not reopen it on release of the same tap.
    private const float ReopenGuard = 0.5f;

    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private TooltipView _viewPrefab;

    private TooltipView _view;
    private RectTransform _currentAnchor;
    private RectTransform _closedByPressAnchor;
    private float _closedByPressTime;
    private float _hideAt;

    public bool IsShown => _currentAnchor != null;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        _view = Instantiate(_viewPrefab, _canvasRect);
        _view.HideImmediate();
    }

    private void OnEnable() => SceneManager.activeSceneChanged += HandleActiveSceneChanged;

    private void OnDisable() => SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

    private void Update()
    {
        if (!IsShown)
            return;

        if (Time.unscaledTime >= _hideAt)
        {
            Hide();
            return;
        }

        // Watches raw input instead of a blocking overlay, so the tap still reaches the game.
        if (WasPointerPressedThisFrame())
        {
            _closedByPressAnchor = _currentAnchor;
            _closedByPressTime = Time.unscaledTime;
            Hide();
        }
    }

    public void Toggle(string text, RectTransform anchor, TooltipSide side)
    {
        bool sameTapClosedIt = _closedByPressAnchor == anchor && Time.unscaledTime - _closedByPressTime < ReopenGuard;
        _closedByPressAnchor = null;

        if (sameTapClosedIt || _currentAnchor == anchor)
        {
            Hide();
            return;
        }

        Show(text, anchor, side);
    }

    public void Show(string text, RectTransform anchor, TooltipSide side)
    {
        _currentAnchor = anchor;
        _hideAt = Time.unscaledTime + AutoHideDelay;

        Vector2 size = _view.SetText(text);
        Rect safe = GetSafeRectLocal();
        Rect anchorRect = GetLocalRect(anchor);
        float offset = AnchorGap + _view.ArrowHeight;

        // Flip to the other side when the preferred one doesn't fit inside the safe area.
        float belowBottom = anchorRect.yMin - offset - size.y;
        float aboveTop = anchorRect.yMax + offset + size.y;
        if (side == TooltipSide.Below && belowBottom < safe.yMin + ScreenMargin)
            side = TooltipSide.Above;
        else if (side == TooltipSide.Above && aboveTop > safe.yMax - ScreenMargin)
            side = TooltipSide.Below;

        float y = side == TooltipSide.Below ? anchorRect.yMin - offset : anchorRect.yMax + offset;
        float halfWidth = size.x * 0.5f;
        float x = Mathf.Clamp(anchorRect.center.x, safe.xMin + ScreenMargin + halfWidth, safe.xMax - ScreenMargin - halfWidth);

        _view.Place(new Vector2(x, y), side, anchorRect.center.x - x);
        _view.AnimateIn();
    }

    public void Hide()
    {
        _currentAnchor = null;
        _view.AnimateOut();
    }

    public void HideImmediate()
    {
        _currentAnchor = null;
        _view.HideImmediate();
    }

    private void HandleActiveSceneChanged(Scene from, Scene to) => HideImmediate();

    private static bool WasPointerPressedThisFrame()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
    }

    // Canvas-local rect of a UI element anywhere (any Screen Space Overlay canvas).
    private Rect GetLocalRect(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = ScreenToLocal(corners[0]);
        Vector2 max = ScreenToLocal(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Rect GetSafeRectLocal()
    {
        Rect safe = Screen.safeArea;
        Vector2 min = ScreenToLocal(safe.min);
        Vector2 max = ScreenToLocal(safe.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Vector2 ScreenToLocal(Vector2 screenPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out Vector2 local);
        return local;
    }
}
