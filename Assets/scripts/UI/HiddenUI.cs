using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HiddenUI : MonoBehaviour
{
    [SerializeField] private Vector2 hiddenPos;
    [SerializeField] private Vector2 shownPos;
    [SerializeField]private float duration = 0.5f;
    private RectTransform UIbody;

    private void Start()
    {
        UIbody = GetComponent<RectTransform>();
    }

    public void SwitchPos(bool val)
    {
        StartCoroutine(MoveRoutine(val));
    }

    public IEnumerator MoveRoutine(bool show)
    {
        Vector2 startPos = UIbody.anchoredPosition;
        Vector2 targetPos = show ? shownPos : hiddenPos;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 이징 함수 적용 (EaseInOutQuad)
            t = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            UIbody.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        UIbody.anchoredPosition = targetPos; // 마지막 위치 보정
    }

}
