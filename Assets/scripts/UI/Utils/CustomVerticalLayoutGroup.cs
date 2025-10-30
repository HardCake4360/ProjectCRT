using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[AddComponentMenu("Layout/Custom Vertical Layout Group")]
public class CustomVerticalLayoutGroup : VerticalLayoutGroup
{
    [Tooltip("각 자식 RectTransform의 실제 anchoredPosition 계산값을 저장")]
    public List<Vector2> calculatedPositions = new List<Vector2>();

    public override void SetLayoutVertical()
    {
        // 기존 수직 레이아웃 계산 로직 유지
        base.SetLayoutVertical();

        calculatedPositions.Clear();

        float offsetY = padding.top;

        // rectChildren은 LayoutGroup이 관리하는 실제 자식 목록이
        for (int i = 0; i < rectChildren.Count; i++)
        {
            RectTransform child = rectChildren[i];

            // Unity의 LayoutUtility로 자식 크기 계산
            float height = LayoutUtility.GetPreferredHeight(child);
            float width = LayoutUtility.GetPreferredWidth(child);

            // 수직 배치 계산 (alignment와 spacing 반영 가능)
            float x = padding.left;
            float y = -offsetY;

            // 계산된 위치 저장
            calculatedPositions.Add(new Vector2(x, y));

            // 실제 UI 배치
            SetChildAlongAxis(child, 0, x, width);
            SetChildAlongAxis(child, 1, y, height);

            // 다음 요소 오프셋 적용
            offsetY += height + spacing;
        }
    }

#if UNITY_EDITOR
    // Scene 뷰에서 확인용 시각화
    private void OnDrawGizmosSelected()
    {
        if (calculatedPositions == null || calculatedPositions.Count == 0) return;

        Gizmos.color = Color.cyan;
        foreach (var pos in calculatedPositions)
        {
            Vector3 worldPos = transform.TransformPoint(pos);
            Gizmos.DrawSphere(worldPos, 6f);
        }
    }
#endif
}
