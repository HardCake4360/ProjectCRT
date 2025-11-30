using UnityEngine;

[CreateAssetMenu(fileName = "InterogationDiaObject", menuName = "Scriptable Objects/InterogationDiaObject")]
public class InterogationDiaObject : DialogueObject
{
    public int continueIdx;
    override public void DetonateEvent(int i = -1)
    {
        LocalDiaManager.Instance.DialogueEventTrigger(this);
        if (i != -1)
        {
            LocalDiaManager.Instance.index = i;
        }
    }

}