using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ReactiveButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [SerializeField] bool mindSelected;
    
    bool isSelected;
    Vector3 originalScale;
    RectTransform rect;

    [SerializeField] Vector3 targetScale;
    [SerializeField] float time;

    public UnityEvent OnClick;
    public UnityEvent OnDeselect;

    private void Start()
    {
        rect = GetComponent<RectTransform>();
        originalScale = rect.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StartCoroutine(ScaleCoroutine(targetScale, time));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isSelected && mindSelected) return;
        StartCoroutine(ScaleCoroutine(originalScale, time));

    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnClick?.Invoke();
        isSelected = true;
    }
    public void Deselect()
    {
        isSelected = false;
        OnDeselect?.Invoke();
        StartCoroutine(ScaleCoroutine(originalScale, time));
    }

    private IEnumerator ScaleCoroutine(Vector3 scale, float duration)
    {
        Vector3 startScale = rect.localScale;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            rect.localScale = Vector3.Lerp(startScale, scale, t);
            yield return null;
        }

        rect.localScale = scale; // 마지막 보정
    }
}
