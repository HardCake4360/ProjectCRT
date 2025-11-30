using UnityEngine;

public class DTB_transformSetter : MonoBehaviour
{
    [SerializeField] private RectTransform rt;
    [SerializeField] private RectTransform[] rects;
    public int currentCamNum;

    public void SetPosition(int i)
    {
        currentCamNum = i;
        rt.anchoredPosition = rects[i].anchoredPosition;
    }
}
