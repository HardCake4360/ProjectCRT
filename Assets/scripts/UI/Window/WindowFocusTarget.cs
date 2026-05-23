using UnityEngine;

public class WindowFocusTarget : MonoBehaviour
{
    [SerializeField] private RectTransform windowRect;

    public RectTransform WindowRect => windowRect != null ? windowRect : transform as RectTransform;

    public void Configure(RectTransform targetWindow)
    {
        windowRect = targetWindow;
    }

    public void Focus()
    {
        RectTransform target = WindowRect;
        if (target != null)
        {
            target.SetAsLastSibling();
            return;
        }

        transform.SetAsLastSibling();
    }
}
