using UnityEngine;

[CreateAssetMenu(fileName = "ParadoxParts", menuName = "Scriptable Objects/ParadoxParts")]
public class ParadoxParts : InterogationDiaObject
{
    public int SortingPriority;

    public void AddParadoxParts()
    {
        var self = this;
        InterogationManager.Instance.ParadoxSet.Add(self);
    }
}
