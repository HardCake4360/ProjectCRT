using UnityEngine;
using System.Collections;

[System.Serializable]
public enum MainState
{
    Main,
    Interact,
    Interogate,
    Freeze
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

    private void Start()
    {
        PC = FindAnyObjectByType<PlayerControler>();
    }

    public void SetMainLoopState(MainState state)
    {
        MainLoopState = state;
    }

    public void SetMainLoopState_Main()
    {
        MainLoopState = MainState.Main;
    }

    public void SetMainLoopState_Interacting()
    {
        MainLoopState = MainState.Interact;
    }

    public void OnLoadSceneEnd()
    {
        if (PC == null) 
        {
            Cursor.lockState = CursorLockMode.None;
            Debug.Log("Cursor Lock disabled");
        }

        Debug.Log("SceneLoaded and MainLoop initialized");
    }

    void Update()
    {
        switch (MainLoopState)
        {
            case MainState.Main:
                HandleMainState();
                break;
            case MainState.Interact:
                break;
            case MainState.Interogate:
                HandleInterogationState();
                break;
            case MainState.Freeze:
                break;
        }
    }

    private void HandleMainState()
    {
        if (Raycaster)
        {
            Raycaster.RaycastAndInteract();
        }

        SetPlayerActive(true);
        HandleCameraShortcuts();
    }

    private void HandleInterogationState()
    {
        SetPlayerActive(false);
        Cursor.lockState = CursorLockMode.None;
    }

    private void SetPlayerActive(bool shouldEnable)
    {
        if (!PC)
        {
            return;
        }

        if (shouldEnable)
        {
            if (!PC.enabled)
            {
                PC.enabled = true;
                Cursor.lockState = CursorLockMode.Locked;
            }

            PC.UpdatePlayer();
            return;
        }

        if (PC.enabled)
        {
            PC.HideHint();
            PC.enabled = false;
        }
    }

    private void HandleCameraShortcuts()
    {
        if (!CamControler)
        {
            return;
        }

        if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.ToLeft))
        {
            CamControler.AddToCurrentCamNum(-1);
        }
        else if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.ToRight))
        {
            CamControler.AddToCurrentCamNum(1);
        }
    }
}