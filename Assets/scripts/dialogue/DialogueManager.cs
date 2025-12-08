using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;


public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    public DialogueObject dialogueData;
    public DialogueUIManager DUIManager;
    public ChoicesUIControler CUI;

    public int index = 0;
    [SerializeField] private bool dialogueStartFlag = false;
    [SerializeField] private bool selecting = false;
    public void SetSelecting(bool val) { selecting = val; }

    public UnityEvent StaticOnDialogueStart;
    public UnityEvent StaticOnDialogueEnd;
    private UnityEvent OnDialogueStart;
    private UnityEvent OnDialogueEnd;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        DUIManager.SetCanvasActive(false);
        DUIManager.SetChoicesUIActive(false);
    }

    public void DialogueEventTrigger(DialogueObject data)
    {
        index = 0;
        dialogueData = data;
        
        OnDialogueStart = data.OnStart;
        OnDialogueEnd = data.OnEnd;
        
        DUIManager.SetCanvasActive(true);//이벤트 실행했을때 UI 보이도록
        dialogueStartFlag = true;
        StaticOnDialogueStart?.Invoke();
        OnDialogueStart?.Invoke();
    }

    public bool IsDiaEnd() 
    {
        if (index == dialogueData.lines.Length - 1
            && !DUIManager.IsTyping())
        {
            return true;
        }
        else return false;
    }

    void Update()
    {
        if (!dialogueStartFlag) return;

        if ((index == 0 ||InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.DialogueAdvanceKeys)
            || dialogueData.IsStart)
            && !selecting)
        {
            dialogueData.IsStart = false;
            // 타이핑 중이면 스킵
            if (DUIManager.IsTyping())
            {
                DUIManager.StopAllCoroutines();
                DUIManager.SkipText(dialogueData.lines[index-1].text);
                return;
            }
            
            if (dialogueData.lines[index].characterName == "end")
            {
                if (dialogueData.TailDia)
                {
                    dialogueData.TailDia.DetonateEvent();
                    return;
                }
                ForceEndDia();
                return;
            }

            // 대사 타이핑 시작
            if (index < dialogueData.lines.Length)
            {
                DUIManager.DisplayDialogue(dialogueData.lines[index]);
                index++;
            }
        }

        //선택지 표시, 선택 상태 진입
        if (dialogueData.Choices
            && !DUIManager.IsTyping()
            && IsDiaEnd())
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void ForceEndDia()
    {
        MainLoop.Instance.SetMainLoopState(MainState.Main);
        selecting = false;
        CUI.SetSelfActive(false);
        DUIManager.SetCanvasActive(false);
        dialogueStartFlag = false;
        StaticOnDialogueEnd?.Invoke();
        OnDialogueEnd?.Invoke();
        Debug.Log("dialogue end");

        dialogueData = null;

        //모든 상호작용 오브젝트에 딜레이 생성
        foreach (var interactObj in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            interactObj.SetInteractableWithDelay(0.2f);

    }

    public void ChoiceEvent()
    {
        if (!dialogueData.Choices) return;
        selecting = true;
        DUIManager.InitChoiceUI(dialogueData.Choices);
        DUIManager.SetChoicesUIActive(true);
        CUI.IndicateByIdx(0);
    }

    public void Log(string txt)
    {
        Debug.Log(txt);
    }
}
