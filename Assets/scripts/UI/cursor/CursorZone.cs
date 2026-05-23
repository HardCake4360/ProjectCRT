using UnityEngine;
using UnityEngine.EventSystems;

public class CursorZone : MeshRayReciver
{
    public CursorState OnHover;
    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (CustomCursor.Instance != null)
        {
            CustomCursor.Instance.SetCursorSprite(OnHover);
        }
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        if (CustomCursor.Instance != null)
        {
            CustomCursor.Instance.SetCursorDefault();
        }
    }
}
