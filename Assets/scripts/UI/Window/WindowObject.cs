using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class WindowObject : MeshRayReciver
{
    [SerializeField] private GameObject Body;

    private Vector2 maxSize;
    private Vector2 minSize;
    private Vector2 clickToAnchorVector;

    [SerializeField] private float duration;
    [SerializeField] private RectTransform rt; //whool window rect transform
    [SerializeField] private RectTransform fullScreen; //maximum rect size
    [SerializeField] private Vector2 fullScreenPos;
    [SerializeField] private ResizablePanel rp;

    public bool isMaximized;
    public bool isMinimized;
    public Vector2 HiddenPos;
    
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

    public void SetHiddenPos(Vector2 pos)
    {
        HiddenPos = pos;
    }

    public void Minimize()
    {
        if (!isMinimized)
        {
            isMinimized = true;
            rp.originalScale = rt.localScale;
            rp.originalPos = rt.localPosition;
            StopAllCoroutines();
            StartCoroutine(AnimateWindow(rp.originalPos, HiddenPos, rp.originalScale, Vector3.zero));
        }
        else
        {
            isMinimized = false;
            StopAllCoroutines();
            StartCoroutine(AnimateWindow(HiddenPos, rp.originalPos, Vector3.zero, rp.originalScale));
        }
    }

    private IEnumerator AnimateWindow(Vector3 startPos, Vector3 endPos, Vector3 startScale, Vector3 endScale)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);

            rt.anchoredPosition3D = Vector3.Lerp(startPos, endPos, normalized);
            rt.localScale = Vector3.Lerp(startScale, endScale, normalized);

            yield return null;
        }

        // 최종 위치/스케일 보정
        rt.anchoredPosition3D = endPos;
        rt.localScale = endScale;
    }

    public void ToggleMaximize()
    {
        if (!isMaximized)
        {
            rp.lastSize = rt.sizeDelta;
            rp.lastPos = rt.anchoredPosition;

            rt.sizeDelta = fullScreen.sizeDelta;
            rt.anchoredPosition = fullScreenPos;
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

    public void Close()
    {
        Destroy(Body);
    }
}
