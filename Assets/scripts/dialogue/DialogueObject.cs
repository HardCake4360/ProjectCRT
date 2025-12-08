using UnityEngine;
using UnityEngine.SceneManagement;
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

        public UnityEvent OnLineStart;

        [TextArea(3, 5)]
        public string text;
    }
    [Tooltip("마지막 노드 이름: end, 선택지도 마지막 노드에 넣을 것")]
    public bool IsStart;
    public DialogueLine[] lines;

    public DialogueObject TailDia;
    public ChoicesObj Choices;
    public int continueIdx;

    public UnityEvent OnStart;
    public UnityEvent OnEnd;

    virtual public void DetonateEvent(int i = 0)
    {
        IsStart = true;
        DialogueManager.Instance.DialogueEventTrigger(this);
        DialogueManager.Instance.index = i;

    }
}