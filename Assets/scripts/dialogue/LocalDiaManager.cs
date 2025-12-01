using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

//DiaManager와 독립적으로 실행되는 심문 기능을 위한 스크립트(사실상 DiaManager와 같고 이름만 다름)
public class LocalDiaManager : MonoBehaviour
{
    public static LocalDiaManager Instance { get; private set; }
    public DialogueObject dialogueData;
    public DialogueObject NullDia;
    public DialogueUIManager DUIManager;
    public ChoicesUIControler CUI;

    public int index = 0;
    private bool dialogueStartFlag = false;
    public bool isDiaEnd = false;
    private bool selecting = false; public bool IsSelecting() { return selecting; }
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

        if ((index == 0 || InputManager.Instance.IsAnyKeyPressedIn(
                            InputManager.Instance.DialogueAdvanceKeys))
             || dialogueData.IsStart
             && !selecting)
        {
            dialogueData.IsStart = false;
            // 타이핑 중이면 스킵
            if (DUIManager.IsTyping())
            {
                DUIManager.StopAllCoroutines();
                DUIManager.SkipText(dialogueData.lines[index - 1].text);
                return;
            }

            if (dialogueData.lines[index].characterName == "end")
            {
                //선택지 표시
                if (dialogueData.TailDia 
                    || InterogationManager.Instance.InterogationState == InterogationState.Question)
                {
                    isDiaEnd = true;
                    dialogueData.OnEnd?.Invoke();
                    dialogueData.TailDia.DetonateEvent(dialogueData.continueIdx);
                    return;
                }
                isDiaEnd = true;
                DUIManager.SetCanvasActive(false);
                dialogueStartFlag = false;
                StaticOnDialogueEnd?.Invoke();
                OnDialogueEnd?.Invoke();

                Debug.Log("다이어로그 종료");

                //모든 상호작용 오브젝트에 딜레이 생성
                foreach (var interactObj in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
                    interactObj.SetInteractableWithDelay(0.2f);

                return;
            }

            // 대사 타이핑 시작
            if (index < dialogueData.lines.Length)
            {
                CameraFocusControl.Instance.FocusTo(dialogueData.lines[index].CamNumber);
                InterogationManager.Instance.DTB_Setter.SetPosition(dialogueData.lines[index].CamNumber);
                DUIManager.DisplayDialogue(dialogueData.lines[index]);
                index++;
            }
        }

        //선택 상태 진입
        if (dialogueData.Choices 
            && !DUIManager.IsTyping()
            && isDiaEnd)
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void ChoiceEvent()
    {
        if (!dialogueData.Choices) return;
        selecting = true;
        DUIManager.InitChoiceUI(dialogueData.Choices);
        DUIManager.SetChoicesUIActive(true);
        CUI.IndicateByIdx(0);
    }

    public void QuestionEvent()
    {
        var currentDia = dialogueData;
        var question = (InterogationDiaObject)dialogueData.lines[index-1].QuestionDia;
        if (!question)
        {
            questionIsNull();
            return;
        }
        question.TailDia = currentDia;
        question.continueIdx = index-1;
        question.OnEnd.AddListener(() =>
        {
            InterogationManager.Instance.InterogationState = InterogationState.Testify;
        });
        question.DetonateEvent();
    }

    private void questionIsNull()
    {
        //질의가 null일때 출력하는 대사
        Debug.Log("Question Event is null");
        var currentDia = dialogueData;
        NullDia.TailDia = currentDia;
        NullDia.TailDia.continueIdx = index-1;
        NullDia.OnEnd.AddListener(() =>
        {
            InterogationManager.Instance.InterogationState = InterogationState.Testify;
        });
        NullDia.DetonateEvent();
    }

    public void Log(string txt)
    {
        Debug.Log(txt);
    }
}
