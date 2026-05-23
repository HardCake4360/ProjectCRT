using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class IconWindowToggle : MonoBehaviour, IPointerDownHandler
{
    private const string TitleTextName = "TitleText";

    [SerializeField] private RectTransform windowRect;
    [SerializeField] private float animationDuration = 0.18f;

    private RectTransform iconRect;
    private CanvasGroup windowCanvasGroup;
    private Coroutine animationCoroutine;
    private readonly Vector3[] iconWorldCorners = new Vector3[4];
    private Vector2 expandedPosition;
    private Vector2 expandedSize;
    private bool isWindowVisible = true;
    private bool initialized;

    public void Configure(RectTransform targetWindow, float duration = 0.18f)
    {
        windowRect = targetWindow;
        animationDuration = duration;
        Initialize(true);
    }

    private void Awake()
    {
        Initialize(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        ToggleWindow();
    }

    public void ToggleWindow()
    {
        Initialize(false);
        if (windowRect == null)
        {
            return;
        }

        bool wasAnimating = animationCoroutine != null;
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        if (isWindowVisible)
        {
            if (!wasAnimating)
            {
                expandedPosition = windowRect.anchoredPosition;
                expandedSize = windowRect.sizeDelta;
            }

            isWindowVisible = false;
            SetTitleTextActive(false);
            animationCoroutine = StartCoroutine(AnimateWindow(GetIconAnchoredPosition(), Vector2.zero, false));
        }
        else
        {
            isWindowVisible = true;
            windowRect.gameObject.SetActive(true);
            SetWindowInteractive(true);
            windowRect.SetAsLastSibling();
            animationCoroutine = StartCoroutine(AnimateWindow(expandedPosition, expandedSize, true));
        }
    }

    private void Initialize(bool forceRecord)
    {
        iconRect ??= GetComponent<RectTransform>();
        if (windowRect == null)
        {
            windowRect = FindDefaultWindow();
        }

        if (windowRect == null)
        {
            return;
        }

        windowCanvasGroup = windowRect.GetComponent<CanvasGroup>();
        if (windowCanvasGroup == null)
        {
            windowCanvasGroup = windowRect.gameObject.AddComponent<CanvasGroup>();
        }

        if (!initialized || forceRecord)
        {
            expandedPosition = windowRect.anchoredPosition;
            expandedSize = windowRect.sizeDelta;
            initialized = true;
            SetWindowInteractive(true);
        }
    }

    private RectTransform FindDefaultWindow()
    {
        foreach (RectTransform child in GetComponentsInChildren<RectTransform>(true))
        {
            if (child != iconRect && child.parent == transform)
            {
                return child;
            }
        }

        return null;
    }

    private IEnumerator AnimateWindow(Vector2 targetPosition, Vector2 targetSize, bool visibleAfter)
    {
        Vector2 startPosition = windowRect.anchoredPosition;
        Vector2 startSize = windowRect.sizeDelta;
        float elapsed = 0f;

        if (!visibleAfter)
        {
            SetWindowInteractive(false);
        }

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = animationDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / animationDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            windowRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased);
            windowRect.sizeDelta = Vector2.Lerp(startSize, targetSize, eased);

            yield return null;
        }

        windowRect.anchoredPosition = targetPosition;
        windowRect.sizeDelta = targetSize;
        isWindowVisible = visibleAfter;
        SetWindowInteractive(visibleAfter);
        SetTitleTextActive(visibleAfter);
        windowRect.gameObject.SetActive(visibleAfter);
        animationCoroutine = null;
    }

    private Vector2 GetIconAnchoredPosition()
    {
        if (iconRect == null || windowRect == null)
        {
            return Vector2.zero;
        }

        RectTransform windowParent = windowRect.parent as RectTransform;
        if (windowParent == null)
        {
            return Vector2.zero;
        }

        Canvas.ForceUpdateCanvases();
        if (iconRect.parent is RectTransform iconParent)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(iconParent);
        }

        Vector3 iconWorldCenter = GetIconWorldCenter();
        Vector2 parentLocalPoint = windowParent.InverseTransformPoint(iconWorldCenter);
        return ConvertParentLocalPointToAnchoredPosition(windowParent, parentLocalPoint);
    }

    private Vector3 GetIconWorldCenter()
    {
        iconRect.GetWorldCorners(iconWorldCorners);
        return (iconWorldCorners[0] + iconWorldCorners[2]) * 0.5f;
    }

    private Vector2 ConvertParentLocalPointToAnchoredPosition(RectTransform parentRect, Vector2 parentLocalPoint)
    {
        Rect parentRectValue = parentRect.rect;
        Vector2 anchorReference = new Vector2(
            parentRectValue.xMin + Mathf.Lerp(windowRect.anchorMin.x, windowRect.anchorMax.x, windowRect.pivot.x) * parentRectValue.width,
            parentRectValue.yMin + Mathf.Lerp(windowRect.anchorMin.y, windowRect.anchorMax.y, windowRect.pivot.y) * parentRectValue.height
        );

        return parentLocalPoint - anchorReference;
    }

    private void SetWindowInteractive(bool interactive)
    {
        if (windowCanvasGroup == null)
        {
            return;
        }

        windowCanvasGroup.alpha = 1f;
        windowCanvasGroup.interactable = interactive;
        windowCanvasGroup.blocksRaycasts = interactive;
    }

    private void SetTitleTextActive(bool active)
    {
        if (windowRect == null)
        {
            return;
        }

        Transform[] children = windowRect.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == TitleTextName)
            {
                child.gameObject.SetActive(active);
            }
        }
    }
}
