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
    public Camera mainCamera;          // 플레이어 카메라
    public RectTransform uiCanvasRect; // UI Canvas RectTransform
    Vector3 adjustPos;
    public float RaycastLength;
    public GraphicRaycaster uiRaycaster; // UI GraphicRaycaster
    public LayerMask TargetLayer;

    private GameObject currentHovering;
    private GameObject currentDragging;
    private Vector2? prevLocalPoint = null;

    private bool interacting;
    public void SetInteracting(bool val) { interacting = val; }

    private void Start()
    {
        RaycastLength = 10f;
        adjustPos = new Vector3(uiCanvasRect.sizeDelta.x / 2, uiCanvasRect.sizeDelta.y / 2, 0);
        interacting = false; MainLoop.Instance.SetMainLoopState(MainState.Main);
    }

    public void RaycastAndInteract()
    {
        // 메인 카메라에서 마우스 → 월드 Ray 쏘기
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);//레이캐스트 길이 조정 아직 안함
        Debug.DrawRay(ray.origin, ray.direction * RaycastLength, Color.red);

        //if (interacting) return;

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.DrawRay(ray.origin, Vector3.up * 100f, Color.blue);

            //임시 상호작용 코드
            var obj = hit.collider.gameObject.GetComponent<Interactable>();
            if (obj && InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.interactionKeys))
            {
                obj.Interact();
                interacting = true; MainLoop.Instance.SetMainLoopState(MainState.Interacting);
            }


            // 이 오브젝트에 닿았는지 확인
            if (hit.collider.gameObject == gameObject)
            {
                // UV 좌표(0~1) 얻기
                Vector2 uv = hit.textureCoord;

                // UV → Canvas 좌표로 변환
                Vector2 localPoint = new Vector2(
                    uv.x * uiCanvasRect.sizeDelta.x,
                    uv.y * uiCanvasRect.sizeDelta.y
                );

                // 디버그용 선 그리기
                if (prevLocalPoint.HasValue)
                {
                    Vector3 worldPrev = uiCanvasRect.TransformPoint(prevLocalPoint.Value);
                    Vector3 worldCurr = uiCanvasRect.TransformPoint(localPoint);

                    Debug.DrawLine(worldPrev, worldCurr, Color.green, 0.1f);
                }
                prevLocalPoint = localPoint;

                // 이벤트 데이터 만들기
                PointerEventData eventData = new PointerEventData(EventSystem.current);
                eventData.position = localPoint;

                // UI 레이캐스트
                List<RaycastResult> results = new List<RaycastResult>();
                uiRaycaster.Raycast(eventData, results);

                WindowManager.Instance.RaycastResults = results;

                GameObject newHovering = results.Count > 0 ? results[0].gameObject : null;

                // Hovering 상태 갱신 시 실행
                if (currentHovering != newHovering)
                {
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

                // 마우스 버튼 눌렀을 때
                if (Input.GetMouseButtonDown(0) && currentHovering != null)
                {
                    currentDragging = currentHovering;
                    ExecuteEvents.Execute<IPointerDownHandler>(currentDragging, eventData, ExecuteEvents.pointerDownHandler);
                }

                // 마우스 이동 중 드래그
                if (Input.GetMouseButton(0) && currentDragging != null)
                {
                    ExecuteEvents.Execute<IDragHandler>(currentDragging, eventData, ExecuteEvents.dragHandler);
                }

                // 마우스 버튼 뗐을 때
                if (Input.GetMouseButtonUp(0) && currentDragging != null)
                {
                    ExecuteEvents.Execute<IPointerUpHandler>(currentDragging, eventData, ExecuteEvents.pointerUpHandler);
                    currentDragging = null;
                }
            }
        }
    }
}
