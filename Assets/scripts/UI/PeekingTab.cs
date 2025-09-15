using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.EventSystems;

public class PeekingTab : MeshRayReciver
{
    public RectTransform Rect;
    [SerializeField] private float duration;
    [SerializeField] private Vector2 hiddenPos;
    [SerializeField] private Vector2 activePos;

    [SerializeField] InputAction peekKey;

    private void OnEnable()
    {
        peekKey.started += OnActive;
        peekKey.canceled += OnHide;

        peekKey.Enable();
    }

    private void OnDisable()
    {
        peekKey.started -= OnActive;
        peekKey.canceled -= OnHide;

        peekKey.Disable();
    }

    void OnActive(InputAction.CallbackContext cbt)
    {
        StartCoroutine(MoveTo(Rect.anchoredPosition, activePos));
        Debug.Log("active: " + gameObject.name);
    }

    void OnHide(InputAction.CallbackContext cbt)
    {
        StartCoroutine(MoveTo(Rect.anchoredPosition, hiddenPos));
        Debug.Log("hidden: " + gameObject.name);
    }

    IEnumerator MoveTo(Vector2 startPos, Vector2 endPos)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);

            Rect.anchoredPosition3D = Vector3.Lerp(startPos, endPos, normalized);

            yield return null;
        }

        // 최종 위치 보정
        Rect.anchoredPosition3D = endPos;
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        RectTransform dragingRt = WindowManager.Instance.DragingRect;
        if (!dragingRt) return;
        Debug.Log("Parented: " + dragingRt.name);
        dragingRt.SetParent(gameObject.transform);

    }

}
