using UnityEngine;
using UnityEngine.Events;

public class InspectableProp : Interactable
{
    public UnityEvent OnInteract = new();

    [Header("Investigation Information")]
    [SerializeField] private string informationId;
    [SerializeField] private InvestigationItemDefinition informationDefinition;
    [SerializeField] private InvestigationContextBuilder contextBuilder;
    [SerializeField] private bool returnToMainStateAfterInspect = true;

    [Header("Dialogue")]
    [SerializeField] private DialogueObject onInteractDialogue;

    public override void Interact()
    {
        string resolvedId = ResolveInformationId();
        if (string.IsNullOrWhiteSpace(resolvedId))
        {
            Debug.LogWarning($"InspectableProp '{name}' has no information id.", this);
            bool keepInteractionState = InvokeDialogueOrEvents();
            ReturnToMainStateIfNeeded(keepInteractionState);
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

        bool startedDialogue = InvokeDialogueOrEvents();
        ReturnToMainStateIfNeeded(startedDialogue);
    }

    public void StaticDiaEventTrigger(DialogueObject data)
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueEventTrigger(data);
        }
    }

    private string ResolveInformationId()
    {
        if (!string.IsNullOrWhiteSpace(informationId))
        {
            return informationId;
        }

        return informationDefinition != null ? informationDefinition.Id : string.Empty;
    }

    private bool InvokeDialogueOrEvents()
    {
        if (onInteractDialogue != null && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.TryStartDialogue(onInteractDialogue);
        }

        OnInteract?.Invoke();
        return DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueRunning;
    }

    private void ReturnToMainStateIfNeeded(bool keepInteractionState = false)
    {
        if (returnToMainStateAfterInspect && !keepInteractionState && MainLoop.Instance != null)
        {
            MainLoop.Instance.SetMainLoopState_Main();
        }
    }
}
