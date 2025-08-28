using UnityEngine;
using UnityEngine.EventSystems;

public class CursorZone : MeshRayReciver
{
    public CursorState OnHover;
    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        CustomCursor.Instance.SetCursorSprite(OnHover);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        CustomCursor.Instance.SetCursorDefault();
    }
}
