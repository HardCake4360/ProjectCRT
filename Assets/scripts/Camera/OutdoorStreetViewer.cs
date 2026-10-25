using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Inspection camera for the standalone outdoor graphics scene.</summary>
public sealed class OutdoorStreetViewer : MonoBehaviour
{
    [Min(0.1f)] public float speed = 4;
    [Min(1)] public float fastMultiplier = 3;
    [Range(0.02f, 0.5f)] public float mouseSensitivity = 0.12f;
    float yaw, pitch;
    bool ownsCursor;
    void OnEnable() { yaw = transform.eulerAngles.y; pitch = Mathf.DeltaAngle(0, transform.eulerAngles.x); }
    void Update()
    {
        var keys = Keyboard.current; var mouse = Mouse.current;
        if (!Application.isFocused || keys == null || mouse == null) { ReleaseCursor(); return; }
        if (keys.escapeKey.wasPressedThisFrame) { ReleaseCursor(); return; }
        bool looking = mouse.rightButton.isPressed;
        if (looking)
        {
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; ownsCursor = true;
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * mouseSensitivity; pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, -80, 80);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
        else ReleaseCursor();
        Vector3 input = new Vector3((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
            (keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0),
            (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
        float amount = speed * (keys.leftShiftKey.isPressed ? fastMultiplier : 1) * Time.unscaledDeltaTime;
        transform.position += (transform.right * input.x + Vector3.up * input.y + transform.forward * input.z).normalized * amount;
    }
    void ReleaseCursor() { if (!ownsCursor) return; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; ownsCursor = false; }
    void OnDisable() => ReleaseCursor();
    void OnApplicationFocus(bool focused) { if (!focused) ReleaseCursor(); }
}
