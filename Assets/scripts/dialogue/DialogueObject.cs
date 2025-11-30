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

        public PortraitType Portrait;
        public int CamNumber;
        public float CharInterval;

        public DialogueObject QuestionDia;

        [TextArea(3, 5)]
        public string text;
    }
    [Tooltip("마지막 노드 이름: end, 선택지도 마지막 노드에 넣을 것")]
    public DialogueLine[] lines;

    public DialogueObject TailDia;
    public ChoicesObj Choices;

    public UnityEvent OnStart;
    public UnityEvent OnEnd;

    virtual public void DetonateEvent(int i = -1)
    {
        DialogueManager.Instance.DialogueEventTrigger(this);
        if(i != -1)
        {
            DialogueManager.Instance.index = i;
        }
    }
}