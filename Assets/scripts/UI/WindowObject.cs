using UnityEngine;
using UnityEngine.EventSystems;

public class WindowObject : MeshRayReciver
{
    private bool isMaximized;
    private Vector2 maxSize;
    private Vector2 minSize;

    private RectTransform rt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isMaximized = false;
        maxSize = new Vector2(120, 80);
        minSize = new Vector2(256, 160);

        rt = GetComponent<RectTransform>();
        //rect.SetSizeWithCurrentAnchors
    }

    public override void OnDrag(PointerEventData eventData)
    {
        Debug.Log("Window Object draging: " + gameObject.name);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localMousePos
        );

        rt.localPosition = localMousePos;
    }
}
