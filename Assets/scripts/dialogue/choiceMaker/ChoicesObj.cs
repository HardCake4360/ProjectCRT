using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class ChoiceData
{
    public string name;
    public DialogueObject OnSelectDialogue;
    public UnityEvent OnSelect;
}

[CreateAssetMenu(fileName = "ChoicesObj", menuName = "Scriptable Objects/ChoicesObj")]
public class ChoicesObj : ScriptableObject
{
    
    public ChoiceData[] lines;
}
