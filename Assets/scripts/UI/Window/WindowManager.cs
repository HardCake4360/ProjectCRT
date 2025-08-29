using UnityEngine;

public class WindowManager : MonoBehaviour
{
    public static WindowManager Instance { get; private set; }
    
    [SerializeField] float alignDamp;
    GameObject[] alignedWindow;
    
    private Vector2 anchoredPos;
    private int cnt;
    [SerializeField] int MaxAlignCount;

    [SerializeField] private GameObject taskCollection;
    [SerializeField] private RectTransform fullScreenRect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void adjustAnchorAlignment()
    {
        if(cnt > MaxAlignCount)
        {
            cnt = 0;
            anchoredPos = Vector2.zero;
        }
        anchoredPos += new Vector2(alignDamp, alignDamp);
        cnt++;
    }

    private GameObject InstantiateWindow(GameObject window)
    {
        GameObject newWindow = Instantiate(window, gameObject.transform);
        newWindow.GetComponent<RectTransform>().anchoredPosition = anchoredPos;
        adjustAnchorAlignment();
        return newWindow;
    }

    private GameObject InstantiateWindowTab(GameObject tab)
    {
        GameObject newTab = Instantiate(tab, taskCollection.transform);
        return newTab;
    }

    public void InstantiateWhoolWindow(GameObject win, GameObject tab)
    {
        GameObject newWin = InstantiateWindow(win);
        GameObject newTab = InstantiateWindowTab(tab);

        
        WindowObject wo = newWin.GetComponentInChildren<WindowObject>();
        RectTransform rt = newTab.GetComponent<RectTransform>();

        wo.fullScreen = fullScreenRect;
        newTab.GetComponent<WindowTabObject>().win = wo; //windowTabObject와 연결된 Window 초기화
        wo.SetHiddenPos(rt.anchoredPosition); //windowHiddenPos 설정
    }

    

}
