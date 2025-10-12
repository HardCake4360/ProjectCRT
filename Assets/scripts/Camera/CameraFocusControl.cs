using Unity.Cinemachine;
using UnityEngine;

public class CameraFocusControl : MonoBehaviour
{
    public static CameraFocusControl Instance { get; private set; }

    public int StartCamNum;
    public int CurrentCamNum;
    private int prevCamNum;
    [SerializeField] private CustomCinemachineCamera[] cameras;

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

    private void Start()
    {
        FocusTo(StartCamNum);
        prevCamNum = StartCamNum;
    }

    private void Update()
    {
        if(CurrentCamNum != prevCamNum)
        {
            prevCamNum = CurrentCamNum;
            FocusTo(CurrentCamNum);
        }
    }

    public void AddToCurrentCamNum(int val)
    {
        CurrentCamNum = Mathf.Clamp(CurrentCamNum + val, 0, cameras.Length-1);
    }

    public void FocusTo(int idx)
    {
        CurrentCamNum = idx;
        for (int cam = 0; cam < cameras.Length; cam++)
        {
            if (cameras[cam] == null) continue;

            if (cam == idx)
            {
                cameras[cam].Focus();
                continue;
            }
            cameras[cam].Release();
        }
    }
}
