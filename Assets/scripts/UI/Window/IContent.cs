using UnityEngine;

public class IContent : MonoBehaviour
{
    RectTransform parentRect;

    private void Start()
    {
        parentRect = gameObject.transform.parent.gameObject.GetComponent<RectTransform>();
        gameObject.GetComponent<RectTransform>().sizeDelta = parentRect.sizeDelta;
    }
}
