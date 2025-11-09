using UnityEngine;
using UnityEngine.Events;

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
    [Tooltip("마지막 노드 이름: end, 선택지도 마지막 노드에 넣을 것")]
    public DialogueLine[] lines;

    public DialogueObject TailDia;

    public UnityEvent OnStart;
    public UnityEvent OnEnd;

    public void DetonateEvent()
    {
        DialogueManager.Instance.DialogueEventTrigger(this);
    }
}
