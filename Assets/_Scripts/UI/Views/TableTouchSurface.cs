using UnityEngine;
using UnityEngine.EventSystems;

// Transparent full-screen graphic behind every shot control (same idea as a tap catcher): drags that no
// control above it takes become look/aim drags, or move the cue ball while placing it. Every press and drag
// also reports its screen position, so the shot controller can tell a drag that starts on the cue ball
// (ball in hand) from one on the empty table. Mouse and touch alike.
public class TableTouchSurface : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private Canvas _canvas;
    private TurnPhase _phase;

    private float Scale => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
    private bool Placing => _phase == TurnPhase.PlaceCueBall;

    private void Awake() => _canvas = GetComponentInParent<Canvas>();

    private void OnEnable() => GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;

    private void OnDisable() => GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;

    private void HandleTurnPhaseChanged(TurnPhase phase) => _phase = phase;

    public void OnPointerDown(PointerEventData eventData) => GameEvents.RaiseTablePressed(eventData.position);

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        GameEvents.RaiseCueBallDragged(eventData.position);
        if (!Placing)
            GameEvents.RaiseLookDrag(eventData.delta / Scale);
    }

    public void OnEndDrag(PointerEventData eventData) => GameEvents.RaiseCueBallDragEnded();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging)
            return;
        // A press that never moved: still a release (it may have picked up the cue ball), and in the
        // aim and look views a tap.
        GameEvents.RaiseCueBallDragEnded();
        if (!Placing)
            GameEvents.RaiseTableTapped(eventData.position);
    }
}
