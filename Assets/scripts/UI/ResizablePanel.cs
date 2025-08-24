using UnityEngine;
using UnityEngine.EventSystems;

public class ResizablePanel : MeshRayReciver, IPointerDownHandler, IDragHandler
{
    public RectTransform targetPanel;
    public float borderThickness = 10f;   // 테두리 감지 범위
    public float minWidth = 120f;
    public float minHeight = 80f;

    private Vector2 originalSize;
    private Vector2 originalPos;
    private Vector2 originalMousePos;

    private bool resizeLeft, resizeRight, resizeTop, resizeBottom;

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
}
