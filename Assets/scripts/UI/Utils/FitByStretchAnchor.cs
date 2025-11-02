using UnityEngine;

public class FitByStretchAnchor : MonoBehaviour
{
    RectTransform rt;
    
    private void Start()
    {
        rt = GetComponent<RectTransform>();
    }
    private void Update()
    {
        rt.offsetMax = new Vector2(0, rt.offsetMax.y);
    }
}
