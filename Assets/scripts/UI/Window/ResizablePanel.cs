using UnityEngine;
using UnityEngine.EventSystems;

public enum ResizableHandleState
{
    left,
    right,
    top,
    bottom,
    top_left,
    top_right,
    bottom_left,
    bottom_right
}

public class ResizablePanel : MeshRayReciver, IPointerDownHandler, IDragHandler
{
    public RectTransform targetPanel;

    public Vector2 MinSize;
    public Vector2 StartPos;
    public Vector2 lastPos;
    public Vector2 lastSize;

    public Vector2 originalPos;
    public Vector2 originalSize;
    public Vector2 originalScale;

    public Vector2 DragDelta;

    [SerializeField] private bool resizeLeft, resizeRight, resizeTop, resizeBottom;

    public void SetHandleTrigger(ResizableHandleState handle)
    {
        switch (handle)
        {
            case ResizableHandleState.left:
                resizeLeft = true;
                break;
            
            case ResizableHandleState.right:
                resizeRight = true;
                break;
            
            case ResizableHandleState.top:
                resizeTop = true;
                break;
            
            case ResizableHandleState.bottom:
                resizeBottom = true;
                break;
            
            case ResizableHandleState.top_left:
                resizeTop = true;
                resizeLeft = true;
                break;
            
            case ResizableHandleState.top_right:
                resizeTop = true;
                resizeRight = true;
                break;
            
            case ResizableHandleState.bottom_left:
                resizeBottom = true;
                resizeLeft = true;
                break;
            
            case ResizableHandleState.bottom_right:
                resizeBottom = true;
                resizeRight = true;
                break;
        }
    }

    public void SetOriginalTransform()
    {
        originalSize = targetPanel.sizeDelta;
        originalPos = targetPanel.anchoredPosition;
    }

    public void RecordLastTransform()
    {
        lastPos = targetPanel.anchoredPosition;
        lastSize = targetPanel.sizeDelta;
    }

    public void ResetHandleTrigger()
    {
        resizeLeft = false;
        resizeRight = false;
        resizeTop = false;
        resizeBottom = false;
    }
    public override void OnPointerDown(PointerEventData eventData)
    {
        StartPos = eventData.position;
    }

    private void Update()
    {
        if (!(resizeLeft || resizeRight || resizeTop || resizeBottom)) return;

        Vector2 delta = DragDelta;
        Vector2 newSize = originalSize;
        Vector2 newPos = originalPos;

        if (resizeLeft)
        {
            newSize.x = Mathf.Max(MinSize.x, originalSize.x - delta.x);
            if (newSize.x != MinSize.x)
                newPos.x = originalPos.x + delta.x * 0.5f;
        }
        if (resizeRight)
        {
            newSize.x = Mathf.Max(MinSize.x, originalSize.x + delta.x);
            if (newSize.x != MinSize.x)
                newPos.x = originalPos.x + delta.x * 0.5f;
        }
        if (resizeTop)
        {
            newSize.y = Mathf.Max(MinSize.y, originalSize.y + delta.y);
            if (newSize.y != MinSize.y)
                newPos.y = originalPos.y + delta.y * 0.5f;
        }
        if (resizeBottom)
        {
            newSize.y = Mathf.Max(MinSize.y, originalSize.y - delta.y);
            if (newSize.y != MinSize.y)
                newPos.y = originalPos.y + delta.y * 0.5f;
        }

        Debug.Log("Drag delta: " + DragDelta);

        targetPanel.sizeDelta = newSize;
        targetPanel.anchoredPosition = newPos;
    }
}
