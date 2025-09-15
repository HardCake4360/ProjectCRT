using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using TMPro;

public class WindowObject : MeshRayReciver
{
    //signature variable
    public TextMeshProUGUI WindowName;
    [SerializeField] private Transform contentTransform;
    //

    [SerializeField] private GameObject Body;

    private Vector2 minSize;
    private Vector2 clickToAnchorVector;

    [SerializeField] private float duration;
    [SerializeField] private RectTransform rt; //whool window rect transform
    [SerializeField] private Vector2 fullScreenPos;
    [SerializeField] private ResizablePanel rp;

    public bool isMaximized;
    public bool isMinimized;
    public Vector2 HiddenPos;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isMaximized = false;

        minSize = new Vector2(256, 160);
        //rect.SetSizeWithCurrentAnchors
    }
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        rt.SetAsLastSibling();
        WindowManager.Instance.DragingRect = gameObject.GetComponent<RectTransform>();
        clickToAnchorVector = rt.anchoredPosition - eventData.position;
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        WindowManager.Instance.DragingRect = null;
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

            rt.sizeDelta = WindowManager.Instance.FullScreenRect.sizeDelta;
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

    public void InstantiateContent(GameObject content)
    {
        Instantiate(content, contentTransform);
    }

}
