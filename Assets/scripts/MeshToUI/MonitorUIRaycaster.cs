using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class MonitorUIRaycaster : MonoBehaviour
{
    /*
    모니터 메쉬에 Raycast한 위치 정보를 UI에 전달함
    모니터 메쉬에 부착되어야 함
    */
    public Camera mainCamera;
    public RectTransform uiCanvasRect;
    Vector3 adjustPos;
    public float RaycastLength;
    public GraphicRaycaster uiRaycaster;
    public LayerMask TargetLayer;

    private GameObject currentHovering;
    private GameObject currentPressing;
    private GameObject currentDragging;
    private bool beganDrag;
    private Vector2? prevLocalPoint = null;

    private bool interacting;
    public void SetInteracting(bool val) { interacting = val; }

    private void Start()
    {
        RaycastLength = 10f;
        if (uiCanvasRect != null)
        {
            adjustPos = new Vector3(uiCanvasRect.sizeDelta.x / 2, uiCanvasRect.sizeDelta.y / 2, 0);
        }
        interacting = false;
        if (MainLoop.Instance != null)
        {
            MainLoop.Instance.SetMainLoopState(MainState.Main);
        }
    }

    public void RaycastAndInteract()
    {
        if (mainCamera == null || uiCanvasRect == null || uiRaycaster == null || EventSystem.current == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * RaycastLength, Color.red);

        if (!Physics.Raycast(ray, out RaycastHit hit, RaycastLength))
        {
            ClearHoverState();
            return;
        }

        Debug.DrawRay(ray.origin, Vector3.up * 100f, Color.blue);

        TryHandleWorldInteraction(hit);

        if (hit.collider.gameObject == gameObject)
        {
            HandleMonitorUI(hit);
            return;
        }

        ClearHoverState();
    }

    private void TryHandleWorldInteraction(RaycastHit hit)
    {
        var interactableObject = hit.collider.gameObject.GetComponent<Interactable>();
        if (!interactableObject || InputManager.Instance == null
            || !InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.interactionKeys))
        {
            return;
        }

        interactableObject.Interact();
        interacting = true;
        if (MainLoop.Instance != null)
        {
            MainLoop.Instance.SetMainLoopState(MainState.Interact);
        }
    }

    private void HandleMonitorUI(RaycastHit hit)
    {
        Vector2 localPoint = new Vector2(
            hit.textureCoord.x * uiCanvasRect.sizeDelta.x,
            hit.textureCoord.y * uiCanvasRect.sizeDelta.y
        );

        if (prevLocalPoint.HasValue)
        {
            Vector3 worldPrev = uiCanvasRect.TransformPoint(prevLocalPoint.Value);
            Vector3 worldCurr = uiCanvasRect.TransformPoint(localPoint);
            Debug.DrawLine(worldPrev, worldCurr, Color.green, 0.1f);
        }
        prevLocalPoint = localPoint;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = localPoint
        };

        List<RaycastResult> results = new List<RaycastResult>();
        uiRaycaster.Raycast(eventData, results);

        if (WindowManager.Instance != null)
        {
            WindowManager.Instance.RaycastResults = results;
        }

        UpdateHoverState(results.Count > 0 ? results[0].gameObject : null, eventData);
        HandleScrollState(eventData);
        HandleDragState(eventData);
    }

    private void UpdateHoverState(GameObject newHovering, PointerEventData eventData)
    {
        if (currentHovering == newHovering)
        {
            return;
        }

        if (currentHovering != null)
        {
            ExecuteEvents.Execute<IPointerExitHandler>(
                currentHovering, eventData, ExecuteEvents.pointerExitHandler);
        }

        if (newHovering != null)
        {
            ExecuteEvents.Execute<IPointerEnterHandler>(
                newHovering, eventData, ExecuteEvents.pointerEnterHandler);
        }

        currentHovering = newHovering;
    }

    private void HandleScrollState(PointerEventData eventData)
    {
        Vector2 scrollDelta = Input.mouseScrollDelta;
        if (scrollDelta == Vector2.zero || currentHovering == null)
        {
            return;
        }

        GameObject scrollTarget = ExecuteEvents.GetEventHandler<IScrollHandler>(currentHovering);
        if (scrollTarget == null)
        {
            return;
        }

        eventData.scrollDelta = scrollDelta;
        ExecuteEvents.Execute<IScrollHandler>(scrollTarget, eventData, ExecuteEvents.scrollHandler);
    }

    private void HandleDragState(PointerEventData eventData)
    {
        if (Input.GetMouseButtonDown(0))
        {
            FocusWindowAtPointer(eventData.position);
        }

        if (Input.GetMouseButtonDown(0) && currentHovering != null)
        {
            currentPressing = ExecuteEvents.GetEventHandler<IPointerDownHandler>(currentHovering);
            currentDragging = ExecuteEvents.GetEventHandler<IDragHandler>(currentHovering);
            beganDrag = false;

            if (currentPressing != null)
            {
                ExecuteEvents.Execute<IPointerDownHandler>(currentPressing, eventData, ExecuteEvents.pointerDownHandler);
            }

            if (currentDragging != null)
            {
                ExecuteEvents.Execute<IInitializePotentialDragHandler>(currentDragging, eventData, ExecuteEvents.initializePotentialDrag);
            }
        }

        if (Input.GetMouseButton(0) && currentDragging != null)
        {
            if (!beganDrag)
            {
                ExecuteEvents.Execute<IBeginDragHandler>(currentDragging, eventData, ExecuteEvents.beginDragHandler);
                beganDrag = true;
            }

            ExecuteEvents.Execute<IDragHandler>(currentDragging, eventData, ExecuteEvents.dragHandler);
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (currentPressing != null)
            {
                ExecuteEvents.Execute<IPointerUpHandler>(currentPressing, eventData, ExecuteEvents.pointerUpHandler);
            }

            if (currentDragging != null && beganDrag)
            {
                ExecuteEvents.Execute<IEndDragHandler>(currentDragging, eventData, ExecuteEvents.endDragHandler);
            }

            currentPressing = null;
            currentDragging = null;
            beganDrag = false;
        }
    }

    private void FocusWindowAtPointer(Vector2 pointerPosition)
    {
        WindowFocusTarget topmostTarget = null;
        int topmostSiblingIndex = int.MinValue;
        WindowFocusTarget[] focusTargets = uiCanvasRect.GetComponentsInChildren<WindowFocusTarget>(false);

        foreach (WindowFocusTarget focusTarget in focusTargets)
        {
            RectTransform windowRect = focusTarget.WindowRect;
            if (windowRect == null || !windowRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!IsPointerInsideWindow(windowRect, pointerPosition))
            {
                continue;
            }

            int siblingIndex = windowRect.GetSiblingIndex();
            if (siblingIndex >= topmostSiblingIndex)
            {
                topmostTarget = focusTarget;
                topmostSiblingIndex = siblingIndex;
            }
        }

        if (topmostTarget != null)
        {
            topmostTarget.Focus();
        }
    }

    private bool IsPointerInsideWindow(RectTransform windowRect, Vector2 pointerPosition)
    {
        Vector2 canvasLocalPoint = new Vector2(
            uiCanvasRect.rect.xMin + pointerPosition.x,
            uiCanvasRect.rect.yMin + pointerPosition.y
        );

        Vector3 worldPoint = uiCanvasRect.TransformPoint(canvasLocalPoint);
        Vector2 windowLocalPoint = windowRect.InverseTransformPoint(worldPoint);
        return windowRect.rect.Contains(windowLocalPoint);
    }

    private void ClearHoverState()
    {
        if (currentHovering == null)
        {
            return;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute<IPointerExitHandler>(currentHovering, eventData, ExecuteEvents.pointerExitHandler);
        currentHovering = null;
        currentPressing = null;
        currentDragging = null;
        beganDrag = false;
        prevLocalPoint = null;
    }
}
