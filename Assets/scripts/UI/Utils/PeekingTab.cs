using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.EventSystems;

public class PeekingTab : MeshRayReciver
{
    public RectTransform Rect;
    public bool IsHovering;

    [SerializeField] private float duration;
    [SerializeField] private Vector2 hiddenPos;
    [SerializeField] private Vector2 activePos;

    [SerializeField] InputAction peekKey;
    private RectAnimation rectAnim = new RectAnimation();

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
        Rect.SetAsLastSibling();
        StartCoroutine(rectAnim.MoveTo(Rect.anchoredPosition, activePos, Rect, duration));
        Debug.Log("active: " + gameObject.name);
    }

    void OnHide(InputAction.CallbackContext cbt)
    {
        StartCoroutine(rectAnim.MoveTo(Rect.anchoredPosition, hiddenPos, Rect, duration));
        Debug.Log("hidden: " + gameObject.name);
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (WindowManager.Instance.DragingRect) IsHovering = true;
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        IsHovering = false;
    }

}
