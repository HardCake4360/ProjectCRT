using UnityEngine;
using System.Collections;

public class PointerAnim : MonoBehaviour
{
    public Vector3[] Targets;
    public float duration;
    public int CurrentSelection;
    int prevSelection;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        prevSelection = CurrentSelection;
    }

    // Update is called once per frame
    void Update()
    {
        if (CurrentSelection != prevSelection)
        {
            prevSelection = CurrentSelection;
            StartCoroutine(PointTarget(CurrentSelection));
        }
    }
    public void SetSelection(int idx)
    {
        CurrentSelection = idx;
    }

    public IEnumerator PointTarget(int idx)
    {
        Vector3 startPos = Targets[CurrentSelection];
        Vector3 targetPos = Targets[idx];
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 이징 함수 적용 (EaseInOutQuad)
            t = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            gameObject.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        gameObject.transform.localPosition = targetPos; // 마지막 위치 보정
    }
}
