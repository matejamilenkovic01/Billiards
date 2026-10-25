using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Passes pointer events on a plain graphic to whoever listens (the spin picker's ball and backdrop).
public class PointerRelay : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerClickHandler
{
    public event Action<PointerEventData> Down;
    public event Action<PointerEventData> Dragged;
    public event Action<PointerEventData> Clicked;

    public void OnPointerDown(PointerEventData eventData) => Down?.Invoke(eventData);

    public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(eventData);
}
