using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class SceneInitializer : MonoBehaviour
{
    public UnityEvent OnStart;
    public UnityEvent OnInitial;
    public float Delay;

    [Header("Settings")]
    [SerializeField] private string startBgm;

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
        if (startBgm != null) StartCoroutine(PlayBGMWhenReady(startBgm));
        StartCoroutine(initializeScene());
    }

    IEnumerator initializeScene()
    {
        yield return new WaitForSeconds(Delay);
        yield return new WaitUntil(() => MainLoop.Instance != null);
        OnInitial?.Invoke();
    }
    
    public void OuterSceneInitPlayer()
    {
        if (MainLoop.Instance == null) return;
        MainLoop.Instance.PC = this.PC;
    }

    public void SetCursorLockState_None()
    {
        Cursor.lockState = CursorLockMode.None;
    }


    public void InitMainLoop_HomeScene()
    {
        if (MainLoop.Instance == null) return;
        MainLoop.Instance.UI = UIManager;
        MainLoop.Instance.Raycaster = Raycaster;
        MainLoop.Instance.CamControler = CamCon;
        MainLoop.Instance.Cam = MainCam;
    }

    public void InitMainLoop_OuterScene()
    {
        if (MainLoop.Instance == null) return;
        MainLoop.Instance.PC = PC;
    }

    IEnumerator PlayBGMWhenReady(string name)
    {
        yield return new WaitUntil(() => AudioManager.Instance != null);
        yield return new WaitForEndOfFrame();
        AudioManager.Instance.PlayBGM(name);
    }
}
