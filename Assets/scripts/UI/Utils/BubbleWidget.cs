using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class BubbleWidget : MeshRayReciver
{
    [SerializeField] private RectTransform rect;            // 위젯 RectTransform
    [SerializeField] private float movementThreshold = 50f; // 마우스 이동 크기 기준
    [SerializeField] private float animationTime = 0.2f;    // 이동 시간
    RectTransform parentRect;

    private RectAnimation rectAnimation = new RectAnimation();
    private Vector2 lastMousePos;

    void Start()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();
        parentRect = gameObject.transform.parent.gameObject.GetComponent<RectTransform>();

        lastMousePos = Input.mousePosition;
    }

    void Update()
    {
        lastMousePos = Input.mousePosition;
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);

        Vector2 currentMousePos = Input.mousePosition;
        float delta = (currentMousePos - lastMousePos).magnitude;

        Vector2 startPos = rect.anchoredPosition;
        Vector2 endPos = startPos;

        // 화면 크기 (Canvas RectTransform 기준)
        float halfWidth = parentRect.sizeDelta.x / 2f;
        float halfHeight = parentRect.sizeDelta.y / 2f;

        // 버블 크기 보정
        float offsetX = rect.sizeDelta.x / 2f;
        float offsetY = rect.sizeDelta.y / 2f;

        // 움직임이 작으면 → 현재 X값 기준 좌/우 꼭짓점
        if (delta < movementThreshold)
        {
            if (rect.anchoredPosition.x < 0)
            {
                // 오른쪽 꼭짓점 (상/하 판정)
                endPos = (currentMousePos.y < halfHeight)
                    ? new Vector2(halfWidth - offsetX, -halfHeight + offsetY)    // 우하단
                    : new Vector2(halfWidth - offsetX, halfHeight - offsetY);    // 우상단
            }
            else
            {
                // 왼쪽 꼭짓점 (상/하 판정)
                endPos = (currentMousePos.y < halfHeight)
                    ? new Vector2(-halfWidth + offsetX, -halfHeight + offsetY)   // 좌하단
                    : new Vector2(-halfWidth + offsetX, halfHeight - offsetY);   // 좌상단
            }
        }
        else
        {
            // 움직임이 크면 → 상/하 가까운 꼭짓점
            if (currentMousePos.y < halfHeight)
            {
                endPos = (currentMousePos.x < halfWidth)
                    ? new Vector2(-halfWidth + offsetX, -halfHeight + offsetY)   // 좌하단
                    : new Vector2(halfWidth - offsetX, -halfHeight + offsetY);   // 우하단
            }
            else
            {
                endPos = (currentMousePos.x < halfWidth)
                    ? new Vector2(-halfWidth + offsetX, halfHeight - offsetY)    // 좌상단
                    : new Vector2(halfWidth - offsetX, halfHeight - offsetY);    // 우상단
            }
        }

        // 애니메이션 실행
        StopAllCoroutines();
        StartCoroutine(rectAnimation.MoveTo(startPos, endPos, rect, animationTime));
    }
}
