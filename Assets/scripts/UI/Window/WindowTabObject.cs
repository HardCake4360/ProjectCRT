using UnityEngine;
using TMPro;

public class WindowTabObject : MonoBehaviour
{
    public TextMeshProUGUI TabName;
    public WindowObject win;
    //private RectTransform rt;

    private void Start()
    {
        //rt = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (!win) Destroy(gameObject);
    }

}
