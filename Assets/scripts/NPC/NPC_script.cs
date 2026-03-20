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

    override public void Interact()
    {
        if (!canInteract) return;
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
