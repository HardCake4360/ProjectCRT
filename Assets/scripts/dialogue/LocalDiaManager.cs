using UnityEngine;

// 심문 씬에서 사용할 대화 UI 바인딩을 제공하는 호환용 어댑터
public class LocalDiaManager : MonoBehaviour
{
    public static LocalDiaManager Instance { get; private set; }

    public DialogueObject NullDia;
    public DialogueUIManager DUIManager;
    public ChoicesUIControler CUI;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void DialogueEventTrigger(InterogationDiaObject data)
    {
        DialogueManager.Instance.TryStartDialogue(data, DUIManager, CUI, 0, NullDia, MainState.Interogate);
    }

    public void ForceEndDia()
    {
        DialogueManager.Instance.ForceEndDia();
    }

    public void ChoiceEvent()
    {
        DialogueManager.Instance.ChoiceEvent();
    }

    public void QuestionEvent()
    {
        DialogueManager.Instance.QuestionEvent();
    }

    public bool IsSelecting()
    {
        return DialogueManager.Instance != null && DialogueManager.Instance.IsSelecting();
    }

    public bool IsTyping()
    {
        return DialogueManager.Instance != null && DialogueManager.Instance.IsTyping();
    }
}
