using UnityEngine;
using System.Collections;

[System.Serializable]
public enum MainState
{
    Main,
    Interacting
}

public class MainLoop : MonoBehaviour
{
    public static MainLoop Instance { get; private set; }

    public MemoPost memo;
    public bool posting;
    public Camera Cam;

    [Header("Managers")]
    public UIManager UI;
    public MonitorUIRaycaster Raycaster;
    public CameraFocusControl CamControler;

    [Header("Local properties")]
    public PlayerControler PC;

    public MainState MainLoopState;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetMainLoopState(MainState state)
    {
        MainLoopState = state;
    }

    //인스펙터 연결용 함수
    public void SetMainLoopState_Main()
    {
        MainLoopState = MainState.Main;
    }
    public void SetMainLoopState_Interacting()
    {
        MainLoopState = MainState.Interacting;
    }

    public void OnLoadSceneEnd()
    {
        //씬 이동하면서 파괴되는 오브젝트라 비교연산 이용
        if (PC == null) 
        {
            Cursor.lockState = CursorLockMode.None;
            Debug.Log("Cursor Lock disabled");
        }

        Debug.Log("SceneLoaded and MainLoop initialized");
    }

    // Update is called once per frame
    void Update()
    {
        switch (MainLoopState)
        {
            case MainState.Main:
                if(Raycaster) Raycaster.RaycastAndInteract();

                if (PC)
                {
                    PC.UpdatePlayer();
                }

                //camControler 없는 씬에서는 작동하지 않도록
                if (!CamControler) break;
                if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.ToLeft))
                {
                    CamControler.AddToCurrentCamNum(-1);
                }
                else if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.ToRight))
                {
                    CamControler.AddToCurrentCamNum(1);
                }
                
                break;
            case MainState.Interacting:
                break;

        }

    }
}
