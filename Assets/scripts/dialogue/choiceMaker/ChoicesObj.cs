using UnityEngine;

[CreateAssetMenu(fileName = "ChoicesObj", menuName = "Scriptable Objects/ChoicesObj")]
public class ChoicesObj : ScriptableObject
{
    [System.Serializable]
    public class ChoiceData
    {
        public string name;
        public DialogueObject OnSelectDialogue;
    }
    public ChoiceData[] lines;
}
