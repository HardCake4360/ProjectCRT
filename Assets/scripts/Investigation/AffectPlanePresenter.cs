using UnityEngine;

public class AffectPlanePresenter : MonoBehaviour
{
    [SerializeField] private RectTransform planeRoot;
    [SerializeField] private AffectPulseGraphic pulseGraphic;

    private bool isBuilt;
    private float currentInterest;
    private float currentAttitude;

    public void EnsureBuilt()
    {
        isBuilt = planeRoot != null && pulseGraphic != null;
        if (isBuilt)
        {
            ApplyAffectVisuals(currentInterest, currentAttitude);
        }
    }

    public void SetAffect(float interest, float attitude)
    {
        currentInterest = Mathf.Clamp(interest, -1f, 1f);
        currentAttitude = Mathf.Clamp(attitude, -1f, 1f);

        if (!isBuilt)
        {
            return;
        }

        ApplyAffectVisuals(currentInterest, currentAttitude);
    }

    public void ResetAffect(float interest = 0f, float attitude = 0f)
    {
        SetAffect(interest, attitude);
    }

    private void ApplyAffectVisuals(float interest, float attitude)
    {
        pulseGraphic.SetAffect(interest, attitude);
    }
}
