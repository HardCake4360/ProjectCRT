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
        if (targetPanel == null)
        {
            return;
        }

        originalSize = targetPanel.sizeDelta;
        originalPos = targetPanel.anchoredPosition;
    }

    public void RecordLastTransform()
    {
        if (targetPanel == null)
        {
            return;
        }

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
        if (targetPanel == null || !(resizeLeft || resizeRight || resizeTop || resizeBottom)) return;

        Vector2 delta = DragDelta;
        Vector2 newSize = originalSize;
        Vector2 newPos = originalPos;

        if (resizeLeft)
        {
            newSize.x = Mathf.Max(MinSize.x, originalSize.x - delta.x);
            newPos.x = originalPos.x + (originalSize.x - newSize.x) * 0.5f;
        }
        if (resizeRight)
        {
            newSize.x = Mathf.Max(MinSize.x, originalSize.x + delta.x);
            newPos.x = originalPos.x + (newSize.x - originalSize.x) * 0.5f;
        }
        if (resizeTop)
        {
            newSize.y = Mathf.Max(MinSize.y, originalSize.y + delta.y);
            newPos.y = originalPos.y + (newSize.y - originalSize.y) * 0.5f;
        }
        if (resizeBottom)
        {
            newSize.y = Mathf.Max(MinSize.y, originalSize.y - delta.y);
            newPos.y = originalPos.y + (originalSize.y - newSize.y) * 0.5f;
        }

        targetPanel.sizeDelta = newSize;
        targetPanel.anchoredPosition = newPos;
    }
}
