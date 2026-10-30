using UnityEngine;
using UnityEngine.EventSystems;

// Transparent full-screen graphic behind every shot control (same idea as a tap catcher): drags that no
// control above it takes become aim drags, orbit gestures, or move the cue ball while placing it. Every press and
// drag also reports its screen position, so the shot controller can tell a drag that starts on the cue ball
// (ball in hand) from one on the empty table. Orbit (v3 V2: LOOK and the opponent's turn): 1 finger orbits,
// 2 fingers pinch to zoom and drag to pan; with a mouse, left drag orbits, the wheel zooms and right drag pans.
// A double tap on the opponent's turn asks for the table view again.
public class TableTouchSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IPointerClickHandler, IScrollHandler
{
    private const int MaxFingers = 2;
    private const float ScrollZoomStep = 0.1f;

    private readonly int[] _ids = new int[MaxFingers];
    private readonly Vector2[] _positions = new Vector2[MaxFingers];
    private int _fingers;
    private Canvas _canvas;
    private TurnPhase _phase;
    private bool _isPlayerTurn = true;

    private float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
    private bool Placing => _phase == TurnPhase.PlaceCueBall;
    private bool Orbiting => !_isPlayerTurn || _phase == TurnPhase.Look;
    // v4 V9.1: two fingers also zoom and slide the view while aiming (the pinch is ignored while the cue is pulled).
    private bool Gestures => Orbiting || _phase == TurnPhase.Aim;

    private void Awake() => _canvas = GetComponentInParent<Canvas>();

    private void OnEnable()
    {
        GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
        GameEvents.OnTurnStarted += HandleTurnStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        _fingers = 0;
    }

    private void HandleTurnPhaseChanged(TurnPhase phase) => _phase = phase;

    private void HandleTurnStarted(TurnInfo info) => _isPlayerTurn = info.IsHuman;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_fingers < MaxFingers && Find(eventData.pointerId) < 0)
        {
            _ids[_fingers] = eventData.pointerId;
            _positions[_fingers] = eventData.position;
            _fingers++;
        }
        GameEvents.RaiseTablePressed(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        int index = Find(eventData.pointerId);
        if (index < 0)
            return;
        _fingers--;
        _ids[index] = _ids[_fingers];
        _positions[index] = _positions[_fingers];
        if (_fingers == 0 && Orbiting)
            GameEvents.RaiseOrbitDragEnded();
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        int index = Find(eventData.pointerId);
        if (_fingers == MaxFingers && index >= 0)
        {
            // Two fingers: the change in their spread zooms, the move of their middle pans.
            Vector2 otherFinger = _positions[1 - index];
            Vector2 before = _positions[index];
            _positions[index] = eventData.position;
            if (Gestures)
            {
                float spreadBefore = Vector2.Distance(before, otherFinger);
                float spreadNow = Vector2.Distance(eventData.position, otherFinger);
                if (spreadBefore > 1f)
                    GameEvents.RaiseOrbitPinch(spreadNow / spreadBefore);
                GameEvents.RaiseOrbitPan((eventData.position - before) * 0.5f / Scale);
            }
            return;
        }
        if (index >= 0)
            _positions[index] = eventData.position;

        GameEvents.RaiseCueBallDragged(eventData.position);
        if (Placing)
            return;
        Vector2 delta = eventData.delta / Scale;
        if (!Orbiting)
            GameEvents.RaiseLookDrag(delta);
        else if (eventData.button == PointerEventData.InputButton.Right)
            GameEvents.RaiseOrbitPan(delta);
        else
            GameEvents.RaiseOrbitDrag(delta);
    }

    public void OnEndDrag(PointerEventData eventData) => GameEvents.RaiseCueBallDragEnded();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging)
            return;
        // A press that never moved: still a release (it may have picked up the cue ball), and in the
        // aim and look views a tap.
        GameEvents.RaiseCueBallDragEnded();
        if (!_isPlayerTurn && eventData.clickCount >= 2)
            GameEvents.RaiseTableViewRequested();
        else if (!Placing)
            GameEvents.RaiseTableTapped(eventData.position);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (Orbiting && Mathf.Abs(eventData.scrollDelta.y) > 0.01f)
            GameEvents.RaiseOrbitPinch(1f + Mathf.Sign(eventData.scrollDelta.y) * ScrollZoomStep);
    }

    private int Find(int pointerId)
    {
        for (int i = 0; i < _fingers; i++)
        {
            if (_ids[i] == pointerId)
                return i;
        }
        return -1;
    }
}
