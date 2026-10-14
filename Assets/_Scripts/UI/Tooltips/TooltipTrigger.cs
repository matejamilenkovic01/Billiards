using UnityEngine;
using UnityEngine.EventSystems;

// Add to any raycastable UI element to give it a tooltip.
public class TooltipTrigger : MonoBehaviour, IPointerClickHandler
{
    [SerializeField, TextArea] private string _text;
    [SerializeField] private TooltipSide _preferredSide = TooltipSide.Below;

    public void OnPointerClick(PointerEventData eventData) =>
        TooltipManager.Instance.Toggle(_text, (RectTransform)transform, _preferredSide);
}

public enum TooltipSide
{
    Below,
    Above
}
