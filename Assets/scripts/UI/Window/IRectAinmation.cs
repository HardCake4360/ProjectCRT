using UnityEngine;
using System.Collections;


public interface IRectAnimation
{
    virtual IEnumerator MoveTo(Vector2 startPos, Vector2 endPos, RectTransform rect, float time = 0.1f)
    {
        float t = 0;
        while (t < time)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / time);

            rect.anchoredPosition3D = Vector3.Lerp(startPos, endPos, normalized);

            yield return null;
        }

        // 최종 위치 보정
        rect.anchoredPosition3D = endPos;
    }
}
