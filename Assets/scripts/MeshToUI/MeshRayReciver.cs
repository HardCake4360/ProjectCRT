using UnityEngine;
using UnityEngine.EventSystems;

public class MeshRayReciver : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("UI object clicked: " + gameObject.name);
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("UI object Entered: " + gameObject.name);
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("UI object Exited: " + gameObject.name);
    }

    public virtual void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("UI object PointerDown: " + gameObject.name);
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        Debug.Log("UI object Draging: " + gameObject.name);
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("UI object PointerUp: " + gameObject.name);
    }
}
