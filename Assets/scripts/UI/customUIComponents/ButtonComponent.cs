using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ButtonComponent : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public bool interactable;

    [Header("Transition")]
    [SerializeField] Image targetGraphic;
    [SerializeField] Color normalColor;
    [SerializeField] Color hilightedColor;
    [SerializeField] Color pressedColor;
    [SerializeField] Color selectedColor;
    [SerializeField] Color disabledColor;

    [Header("OnClick()")]
    public UnityEvent OnClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Clicked: " + gameObject.name);
        OnClick?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("Pointer down: " + gameObject.name);
        OnClick?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("Pointer up: " + gameObject.name);
    }
}
