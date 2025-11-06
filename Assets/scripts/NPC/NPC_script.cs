using UnityEngine;
using UnityEngine.Events;

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

}
