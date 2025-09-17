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
    public GraphicRaycaster uiRaycaster; // UI GraphicRaycaster
    public LayerMask TargetLayer;

    private GameObject currentHovering;
    private GameObject currentDragging;


    void Update()
    {
        // 메인 카메라에서 마우스 → 월드 Ray 쏘기
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.DrawRay(ray.origin, Vector3.up * 100f, Color.blue);
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
