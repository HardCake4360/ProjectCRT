using UnityEngine;
using UnityEngine.EventSystems;

public class WindowObject : MeshRayReciver
{
    public bool isMaximized;

    private Vector2 maxSize;
    private Vector2 minSize;
    private Vector2 clickToAnchorVector;

    [SerializeField] private RectTransform rt;
    [SerializeField] private RectTransform fullScreen;
    [SerializeField] private ResizablePanel rp;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isMaximized = false;
        maxSize = new Vector2(120, 80);
        minSize = new Vector2(256, 160);
        //rect.SetSizeWithCurrentAnchors
    }
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        clickToAnchorVector = rt.anchoredPosition - eventData.position;
    }

    public override void OnDrag(PointerEventData eventData)
    {
        Debug.Log("Window Object draging: " + gameObject.name);

        rt.anchoredPosition = eventData.position + clickToAnchorVector;
        Debug.Log("local mouse pos: " + eventData.position);
    }

    public void ToggleMaximize()
    {
        if (!isMaximized)
        {
            rt.sizeDelta = fullScreen.sizeDelta;
            rt.anchoredPosition = fullScreen.anchoredPosition;
            isMaximized = true;
            Debug.Log("Window maximized");
        }
        else
        {
            rt.sizeDelta = rp.lastSize;
            rt.anchoredPosition = rp.lastPos;
            isMaximized = false;
            Debug.Log("Window unmaximized");
        }
    }
}
