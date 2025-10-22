using UnityEngine;
using UnityEngine.Events;

public class NPC_script : Interactable
{
    public UnityEvent OnInteract;


    override public void Interact()
    {
        OnInteract?.Invoke();
    }

    

}
