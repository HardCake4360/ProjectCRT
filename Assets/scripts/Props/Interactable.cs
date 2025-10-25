using UnityEngine;
using System.Collections;

public class Interactable: MonoBehaviour
{
    public bool canInteract = true;

    public virtual void Interact() { }
    
    public void SetInteractable(bool value)
    {
        canInteract = value;
    }

    public void SetInteractableWithDelay(float delay)
    {
        StartCoroutine(ReenableInteraction(delay));
    }

    private IEnumerator ReenableInteraction(float delay)
    {
        canInteract = false;
        yield return new WaitForSeconds(delay);
        canInteract = true;
    }
}
