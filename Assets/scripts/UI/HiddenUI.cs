using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HiddenUI : MonoBehaviour
{
    [SerializeField] private Vector2 hiddenPos;
    [SerializeField] private Vector2 shownPos;
    private RectTransform UIbody;
    private float duration = 0.5f;

    public IEnumerator MoveRoutine(bool show)
    {
        Vector2 startPos = UIbody.anchoredPosition;
        Vector2 targetPos = show ? shownPos : hiddenPos;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            UIbody.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        UIbody.anchoredPosition = targetPos; // 마지막 위치 보정
    }
}
