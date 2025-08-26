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
    public float minWidth = 120f;
    public float minHeight = 80f;
    public Vector2 StartPos;
    public Vector2 lastPos;
    public Vector2 lastSize;
    public Vector2 DragDelta;

    private Vector2 originalPos;
    private Vector2 originalSize;


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

    /*
    public override void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetPanel,
            eventData.position,
            eventData.pressEventCamera,
            out originalMousePos
        );

        originalSize = targetPanel.sizeDelta;
        originalPos = targetPanel.anchoredPosition;

        // 현재 클릭한 위치 Rect 기준
        Rect rect = targetPanel.rect;

        resizeLeft = Mathf.Abs(originalMousePos.x - rect.xMin) <= borderThickness;
        resizeRight = Mathf.Abs(originalMousePos.x - rect.xMax) <= borderThickness;
        resizeBottom = Mathf.Abs(originalMousePos.y - rect.yMin) <= borderThickness;
        resizeTop = Mathf.Abs(originalMousePos.y - rect.yMax) <= borderThickness;
        Debug.Log(
            "resizeLeft: " + resizeLeft + "\n" +
            "resizeRight: " + resizeRight + "\n" +
            "resizeBottom: " + resizeBottom + "\n" +
            "resizeTop: " + resizeTop + "\n"
            );
    }
    */

    private void Update()
    {
        if (!(resizeLeft || resizeRight || resizeTop || resizeBottom)) return;

        Vector2 delta = DragDelta;
        Vector2 newSize = originalSize;
        Vector2 newPos = originalPos;

        if (resizeLeft)
        {
            newSize.x = Mathf.Max(minWidth, originalSize.x - delta.x);
            if (newSize.x != minWidth)
                newPos.x = originalPos.x + delta.x * 0.5f;
        }
        if (resizeRight)
        {
            newSize.x = Mathf.Max(minWidth, originalSize.x + delta.x);
            if (newSize.x != minWidth)
                newPos.x = originalPos.x + delta.x * 0.5f;
        }
        if (resizeTop)
        {
            newSize.y = Mathf.Max(minHeight, originalSize.y + delta.y);
            if (newSize.y != minHeight)
                newPos.y = originalPos.y + delta.y * 0.5f;
        }
        if (resizeBottom)
        {
            newSize.y = Mathf.Max(minHeight, originalSize.y - delta.y);
            if (newSize.y != minHeight)
                newPos.y = originalPos.y + delta.y * 0.5f;
        }

        Debug.Log("Drag delta: " + DragDelta);

        targetPanel.sizeDelta = newSize;
        targetPanel.anchoredPosition = newPos;
    }
    /*
    public override void OnDrag(PointerEventData eventData)
    {
        if (!(resizeLeft || resizeRight || resizeTop || resizeBottom)) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetPanel.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 currentMousePos
        );

        Vector2 delta = currentMousePos - originalMousePos;
        Vector2 newSize = originalSize;
        Vector2 newPos = originalPos;

        if (resizeLeft)
        {
            newSize.x = Mathf.Max(minWidth, originalSize.x - delta.x);
            newPos.x = originalPos.x + delta.x * 0.5f;
        }
        if (resizeRight)
        {
            newSize.x = Mathf.Max(minWidth, originalSize.x + delta.x);
            newPos.x = originalPos.x + delta.x * 0.5f;
        }
        if (resizeBottom)
        {
            newSize.y = Mathf.Max(minHeight, originalSize.y - delta.y);
            newPos.y = originalPos.y + delta.y * 0.5f;
        }
        if (resizeTop)
        {
            newSize.y = Mathf.Max(minHeight, originalSize.y + delta.y);
            newPos.y = originalPos.y + delta.y * 0.5f;
        }

        targetPanel.sizeDelta = newSize;
        targetPanel.anchoredPosition = newPos;
    }
    */
}
