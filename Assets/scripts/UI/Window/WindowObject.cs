using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;
using TMPro;

public class WindowObject : MeshRayReciver
{
    //signature variable
    public TextMeshProUGUI WindowName;
    [SerializeField] private Transform contentTransform;
    //

    //debug setting
    public bool LogMousePos;
    public bool LogDragging;
    //debug setting

    [SerializeField] private GameObject Body;

    private Vector2 minSize;
    private Vector2 clickToAnchorVector;
    private Image selfImage;

    [SerializeField] private float duration;
    [SerializeField] private RectTransform motherRT; //whool window rect transform
    [SerializeField] private Vector2 fullScreenPos;
    [SerializeField] private ResizablePanel rp;

    public bool isMaximized;
    public bool isMinimized;
    public Vector2 HiddenPos;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isMaximized = false;
        selfImage = GetComponent<Image>();

        minSize = new Vector2(256, 160);
        //rect.SetSizeWithCurrentAnchors
    }
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        selfImage.raycastTarget = false;
        motherRT.SetAsLastSibling();
        WindowManager.Instance.DragingRect = motherRT;
        WindowManager.Instance.DetachWhileDraging();

        clickToAnchorVector = motherRT.anchoredPosition - eventData.position;
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        WindowManager.Instance.ParentToPeekingTab();
        WindowManager.Instance.DragingRect = null;
        selfImage.raycastTarget = true;
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if(LogDragging) Debug.Log("Window Object draging: " + gameObject.name);

        motherRT.anchoredPosition = eventData.position + clickToAnchorVector;
        if(LogMousePos) Debug.Log("local mouse pos: " + eventData.position);
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
            rp.originalScale = motherRT.localScale;
            rp.originalPos = motherRT.localPosition;
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

            motherRT.anchoredPosition3D = Vector3.Lerp(startPos, endPos, normalized);
            motherRT.localScale = Vector3.Lerp(startScale, endScale, normalized);

            yield return null;
        }

        // 최종 위치/스케일 보정
        motherRT.anchoredPosition3D = endPos;
        motherRT.localScale = endScale;
    }

    public void ToggleMaximize()
    {
        if (!isMaximized)
        {
            rp.lastSize = motherRT.sizeDelta;
            rp.lastPos = motherRT.anchoredPosition;

            motherRT.sizeDelta = WindowManager.Instance.FullScreenRect.sizeDelta;
            motherRT.anchoredPosition = fullScreenPos;
            isMaximized = true;
            
            Debug.Log("Window maximized");
        }
        else
        {
            motherRT.sizeDelta = rp.lastSize;
            motherRT.anchoredPosition = rp.lastPos;
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
