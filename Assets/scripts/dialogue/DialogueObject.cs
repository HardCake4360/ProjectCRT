using UnityEngine;

public enum PortraitType
{
    Neutral,
    Happy,
    Angry,
    Sad
}

[CreateAssetMenu(fileName = "DialogueObject", menuName = "Scriptable Objects/DialogueObject")]
public class DialogueObject : ScriptableObject
{
    [System.Serializable]
    public class DialogueLine
    {
        public string characterName;

        public PortraitType portrait;

        public float charInterval;

        public ChoicesObj choices;

        [TextArea(3, 5)]
        public string text;
    }

    public DialogueLine[] lines;
}
