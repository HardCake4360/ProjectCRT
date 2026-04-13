using UnityEngine;

public class InspectableProp : Interactable
{
    [Header("Investigation Information")]
    [SerializeField] private string informationId;
    [SerializeField] private InvestigationItemDefinition informationDefinition;
    [SerializeField] private InvestigationContextBuilder contextBuilder;
    [SerializeField] private bool returnToMainStateAfterInspect = true;

    public override void Interact()
    {
        string resolvedId = ResolveInformationId();
        if (string.IsNullOrWhiteSpace(resolvedId))
        {
            Debug.LogWarning($"InspectableProp '{name}' has no information id.", this);
            ReturnToMainStateIfNeeded();
            return;
        }

        InvestigationInventoryManager inventory = InvestigationInventoryManager.GetOrCreateInstance();
        bool unlocked = inventory.UnlockInformation(resolvedId);

        if (contextBuilder == null)
        {
            contextBuilder = FindFirstObjectByType<InvestigationContextBuilder>();
        }

        contextBuilder?.RegisterInformation(resolvedId);

        Debug.Log(
            unlocked
                ? $"[InspectableProp] Investigated '{name}' and unlocked information: {resolvedId}"
                : $"[InspectableProp] Investigated '{name}' but information was already unlocked: {resolvedId}",
            this);

        ReturnToMainStateIfNeeded();
    }

    private string ResolveInformationId()
    {
        if (!string.IsNullOrWhiteSpace(informationId))
        {
            return informationId;
        }

        return informationDefinition != null ? informationDefinition.Id : string.Empty;
    }

    private void ReturnToMainStateIfNeeded()
    {
        if (returnToMainStateAfterInspect && MainLoop.Instance != null)
        {
            MainLoop.Instance.SetMainLoopState_Main();
        }
    }
}
