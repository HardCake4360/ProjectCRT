using UnityEngine;

public class DTB_transformSetter : MonoBehaviour
{
    [SerializeField] private RectTransform rt;
    [SerializeField] private RectTransform[] rects;

    private void Start()
    {
        rt = GetComponent<RectTransform>();
    }

    public void SetPosition(int i)
    {
        rt.position = rects[i].rect.position;
    }
}
