using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class SceneInitializer : MonoBehaviour
{
    public UnityEvent OnStart;
    public UnityEvent OnInitial;
    public float Delay;

    [Header("home scene Initial set")]
    //home scene
    public UIManager UIManager;
    public MonitorUIRaycaster Raycaster;
    public CameraFocusControl CamCon;
    public Camera MainCam;

    [Header("outer scene Initial set")]
    //outer scene
    public PlayerControler PC;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OnStart?.Invoke();
        StartCoroutine(initializeScene());
    }

    IEnumerator initializeScene()
    {
        yield return new WaitForSeconds(Delay);
        OnInitial?.Invoke();
    }
    
    public void OuterSceneInitPlayer()
    {
        MainLoop.Instance.PC = this.PC;
    }

    public void SetCursorLockState_None()
    {
        Cursor.lockState = CursorLockMode.None;
    }


    public void InitMainLoop_HomeScene()
    {
        MainLoop.Instance.UI = UIManager;
        MainLoop.Instance.Raycaster = Raycaster;
        MainLoop.Instance.CamControler = CamCon;
        MainLoop.Instance.Cam = MainCam;
    }
}
