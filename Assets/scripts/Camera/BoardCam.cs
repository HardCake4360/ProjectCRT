using UnityEngine;
using UnityEngine.InputSystem;

public class BoardCam : CustomCinemachineCamera
{
    public float DeltaFOV;
    public float defaultFOV;
    public float minFOV;
    private Vector2 scroll;

    public override void Focus()
    {
        base.Focus();
        cam.Lens.FieldOfView = defaultFOV;
    }

    public void ScrollZoom(float scroll)
    {
        float curFOV = cam.Lens.FieldOfView;
        if (scroll > 0)
        {
            if (curFOV <= minFOV) return;
            cam.Lens.FieldOfView -= DeltaFOV;
            Debug.Log("scroll up");
        }
        else if(scroll < 0)
        {
            if (curFOV >= defaultFOV) return;
            cam.Lens.FieldOfView += DeltaFOV;
            Debug.Log("scroll down");
        }
    }

    private void Update()
    {
        scroll = Mouse.current.scroll.ReadValue();
        ScrollZoom(scroll.y);
    }
}
