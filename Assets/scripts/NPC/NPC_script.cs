using UnityEngine;
using UnityEngine.Events;

enum emotionState
{
    happy,
    sad,
    surprise,
}

public class NPC_script : Interactable
{
    public UnityEvent OnInteract;
    [SerializeField] private NpcInvestigationController investigationController;

    override public void Interact()
    {
        if (!canInteract) return;

        if (investigationController == null)
        {
            investigationController = GetComponent<NpcInvestigationController>();
        }

        if (investigationController != null && investigationController.enabled)
        {
            investigationController.BeginInteraction();
            return;
        }

        OnInteract?.Invoke();
    }

    public void StaticDiaEventTrigger(DialogueObject data)
    {
        DialogueManager.Instance.DialogueEventTrigger(data);
    }

    public void Emotion()
    {

    }
}
