using UnityEngine;

/// <summary>Scene-local settings for the Bar graphics experiment. Disable to compare.</summary>
[ExecuteAlways]
public sealed class BarRetroLook : MonoBehaviour
{
    [Header("Output (480 lines, preserves the camera aspect ratio)")]
    public bool effectEnabled = true;
    [Range(120, 1080)] public int verticalResolution = 480;
    [Range(2, 8)] public int colorBits = 5;
    [Range(0, 1)] public float dithering = 1;
    [Range(0, 1)] public float horizontalBlur = 0.35f;
    [Header("Color")]
    [Range(0, 2)] public float saturation = 0.85f;
    [Range(0.5f, 1.5f)] public float contrast = 0.94f;
    public Color tint = new Color(1f, 0.96f, 0.92f);
    [Range(0, 0.5f)] public float vignette = 0.12f;

    public static BarRetroLook ForCamera(Camera camera)
    {
        if (camera.cameraType != CameraType.Game || camera.targetTexture != null && camera.name != "Bar Retro Verification") return null;
        foreach (var look in FindObjectsByType<BarRetroLook>(FindObjectsSortMode.None))
            if (look.isActiveAndEnabled && look.effectEnabled && look.gameObject.scene == camera.gameObject.scene)
                return look;
        return null;
    }
}
