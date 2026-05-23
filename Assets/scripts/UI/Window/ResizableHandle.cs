using UnityEngine;
using UnityEngine.EventSystems;

public class ResizableHandle : MeshRayReciver
{
    [SerializeField] private ResizableHandleState handleSetting;
    [SerializeField] private ResizablePanel targetPanel;

    public void Configure(ResizableHandleState handle, ResizablePanel panel)
    {
        handleSetting = handle;
        targetPanel = panel;
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (targetPanel == null)
        {
            return;
        }

        targetPanel.SetHandleTrigger(handleSetting);
        targetPanel.SetOriginalTransform();
        targetPanel.StartPos = eventData.position;
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (targetPanel == null)
        {
            return;
        }

        targetPanel.DragDelta = eventData.position - targetPanel.StartPos;
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        if (targetPanel == null)
        {
            return;
        }

        targetPanel.ResetHandleTrigger();
    }
}
