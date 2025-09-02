using UnityEngine;

public class IContent : MonoBehaviour
{
    RectTransform parentRect;
    WindowObject window;

    private void Start()
    {
        parentRect = gameObject.transform.parent.gameObject.GetComponent<RectTransform>();
        gameObject.GetComponent<RectTransform>().sizeDelta = parentRect.sizeDelta;
    }
}
