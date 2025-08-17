using Unity.Cinemachine;
using UnityEngine;

public class CameraFocusControl : MonoBehaviour
{
    public static CameraFocusControl Instance { get; private set; }

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

    public void FocusTo(int idx)
    {
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
