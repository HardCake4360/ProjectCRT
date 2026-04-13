using UnityEngine;

public class AffectPlanePresenter : MonoBehaviour
{
    [SerializeField] private RectTransform planeRoot;
    [SerializeField] private AffectPulseGraphic pulseGraphic;

    private bool isBuilt;
    private float currentInterest;
    private float currentAttitude;
    private bool warnedMissingPulseGraphic;

    public void EnsureBuilt()
    {
        isBuilt = pulseGraphic != null;
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
            if (!warnedMissingPulseGraphic)
            {
                warnedMissingPulseGraphic = true;
                Debug.LogWarning("[AffectPlanePresenter] Affect value received, but pulseGraphic is not assigned. The affect UI cannot update until the prefab binding is restored.", this);
            }

            return;
        }

        ApplyAffectVisuals(currentInterest, currentAttitude);
    }

    public void ResetAffect(float interest = 0f, float attitude = 0f)
    {
        currentInterest = Mathf.Clamp(interest, -1f, 1f);
        currentAttitude = Mathf.Clamp(attitude, -1f, 1f);

        if (!isBuilt)
        {
            return;
        }

        pulseGraphic.ResetAffect(currentInterest, currentAttitude);
    }

    public void SetPendingState(bool pending)
    {
        if (!isBuilt)
        {
            return;
        }

        pulseGraphic.SetPendingState(pending);
    }

    private void ApplyAffectVisuals(float interest, float attitude)
    {
        pulseGraphic.SetAffect(interest, attitude);
    }
}
