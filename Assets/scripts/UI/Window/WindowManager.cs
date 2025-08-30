using UnityEngine;
using UnityEngine.Events;


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

    public WhoolWindow InstantiateWhoolWindow(WhoolWindow whool)
    {
        GameObject newWin = InstantiateWindow(whool.window);
        GameObject newTab = InstantiateWindowTab(whool.tab);

        
        WindowObject wo = newWin.GetComponentInChildren<WindowObject>();
        RectTransform rt = newTab.GetComponent<RectTransform>();

        Canvas.ForceUpdateCanvases(); // 레이아웃 즉시 반영

        wo.fullScreen = fullScreenRect;
        newTab.GetComponent<WindowTabObject>().win = wo; //windowTabObject와 연결된 Window 초기화
        wo.SetHiddenPos(wo.GetComponent<RectTransform>().InverseTransformPoint(rt.position)); //windowHiddenPos 설정
        newTab.GetComponent<ButtonComponent>().OnClick.AddListener(wo.Minimize);
        return new WhoolWindow(newWin, newTab);
    }

    

}
