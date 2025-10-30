using UnityEngine;

[ExecuteInEditMode]
public class RectFitByChild : MonoBehaviour
{
    [SerializeField] private Vector2 delta; // 추가 크기 (예: (10, 20))
    [SerializeField] private RectTransform targetRect;

    void Update()
    {
        if (transform.childCount == 0)
            return;
        if (!targetRect)
        {
            // 첫 번째 자식 RectTransform 가져오기
            targetRect = transform.GetChild(0).GetComponent<RectTransform>();
        }
        RectTransform selfRect = GetComponent<RectTransform>();

        if (targetRect == null || selfRect == null)
            return;

        // 자식의 실제 크기 + delta 적용
        Vector2 targetSize = targetRect.sizeDelta + delta;
        selfRect.sizeDelta = targetSize;
    }
}
