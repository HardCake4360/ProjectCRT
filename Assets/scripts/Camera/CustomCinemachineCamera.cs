using Unity.Cinemachine;
using UnityEngine;

public class CustomCinemachineCamera : MonoBehaviour
{
    protected CinemachineCamera cam;
    private void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
    }
    
    virtual public void Focus()
    {
        cam.Priority = 10;
    }

    virtual public void Release()
    {
        cam.Priority = 5;
    }

}
