using UnityEngine;

public class WindowTabObject : MonoBehaviour
{
    public WindowObject win;
    private RectTransform rt;

    private void Start()
    {
        rt = GetComponent<RectTransform>();
        //if (win != null)
          //  win.SetHiddenPos(rt.anchoredPosition);
    }

    private void Update()
    {
        if (!win) Destroy(gameObject);
    }

}
