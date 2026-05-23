using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePanel : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform targetPanel;
    [SerializeField] private bool bringToFront = true;

    private Vector2 pointerToPanelOffset;
    private bool hasDragOrigin;

    public void Configure(RectTransform target, bool shouldBringToFront = true)
    {
        targetPanel = target;
        bringToFront = shouldBringToFront;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        BeginPanelDrag(eventData);
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        BeginPanelDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetPanel == null)
        {
            return;
        }

        if (!hasDragOrigin)
        {
            BeginPanelDrag(eventData);
        }

        targetPanel.anchoredPosition = eventData.position + pointerToPanelOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        hasDragOrigin = false;
    }

    private void BeginPanelDrag(PointerEventData eventData)
    {
        if (targetPanel == null)
        {
            return;
        }

        if (bringToFront)
        {
            targetPanel.SetAsLastSibling();
        }

        pointerToPanelOffset = targetPanel.anchoredPosition - eventData.position;
        hasDragOrigin = true;
    }
}
