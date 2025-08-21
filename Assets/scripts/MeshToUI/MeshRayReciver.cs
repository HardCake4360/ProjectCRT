using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MeshRayReciver : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public virtual void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("UI object clicked");
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("UI object Entered");
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("UI object Exited");
    }
}
