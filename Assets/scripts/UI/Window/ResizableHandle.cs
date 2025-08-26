using UnityEngine;
using UnityEngine.EventSystems;

public class ResizableHandle : MeshRayReciver
{
    [SerializeField] private ResizableHandleState handleSetting;
    [SerializeField] private ResizablePanel targetPanel;

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        targetPanel.SetHandleTrigger(handleSetting);
        targetPanel.SetOriginalTransform();
        targetPanel.StartPos = eventData.position;
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);
        targetPanel.DragDelta = eventData.position - targetPanel.StartPos;
        Debug.Log("pointer pos: " + eventData.position);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        targetPanel.ResetHandleTrigger();
    }
}
