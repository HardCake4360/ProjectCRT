using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoBehaviour, IDialogueFlowController
{
    public static DialogueManager Instance { get; private set; }
    public DialogueObject dialogueData;
    public DialogueUIManager DUIManager;
    public ChoicesUIControler CUI;

    public int index = 0;
    [SerializeField] private bool dialogueStartFlag = false;
    [SerializeField] private bool selecting = false;

    public UnityEvent StaticOnDialogueStart;
    public UnityEvent StaticOnDialogueEnd;

    private UnityEvent OnDialogueStart;
    private UnityEvent OnDialogueEnd;
    private DialogueUIManager activeDialogueUI;
    private ChoicesUIControler activeChoiceUI;
    private DialogueObject questionFallbackDialogue;
    private MainState stateAfterDialogue = MainState.Main;
    private bool pendingAdvance;

    public bool IsDialogueRunning => dialogueStartFlag;
    public DialogueUIManager DialogueUI => activeDialogueUI != null ? activeDialogueUI : DUIManager;
    public ChoicesUIControler ChoiceUI => activeChoiceUI != null ? activeChoiceUI : CUI;

    public void SetSelecting(bool val) { selecting = val; }
    public bool IsSelecting() => selecting;
    public bool IsTyping() => DialogueUI != null && DialogueUI.IsTyping();
    public MainState StateAfterDialogue => stateAfterDialogue;

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
        if (DialogueUI != null)
        {
            DialogueUI.SetCanvasActive(false);
            DialogueUI.SetChoicesUIActive(false);
        }
    }

    public void DialogueEventTrigger(DialogueObject data)
    {
        if (data == null)
        {
            Debug.LogWarning("DialogueEventTrigger called with null data.");
            return;
        }

        if (DialogueUI == null)
        {
            Debug.LogWarning("DialogueEventTrigger called without a valid DialogueUIManager.");
            return;
        }

        index = 0;
        dialogueData = data;
        selecting = false;
        pendingAdvance = true;

        OnDialogueStart = data.OnStart;
        OnDialogueEnd = data.OnEnd;

        DialogueUI.SetCanvasActive(true);
        DialogueUI.SetChoicesUIActive(false);
        dialogueStartFlag = true;
        StaticOnDialogueStart?.Invoke();
        OnDialogueStart?.Invoke();
    }

    public bool IsDiaEnd()
    {
        return dialogueData != null
            && DialogueUI != null
            && dialogueData.lines != null
            && dialogueData.lines.Length > 0
            && index == dialogueData.lines.Length - 1
            && !DialogueUI.IsTyping();
    }

    void Update()
    {
        if (!dialogueStartFlag || dialogueData == null || DialogueUI == null)
        {
            return;
        }

        if (dialogueData.lines == null || dialogueData.lines.Length == 0)
        {
            ForceEndDia();
            return;
        }

        if ((index == 0 || (InputManager.Instance != null && InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.DialogueAdvanceKeys))
            || pendingAdvance || dialogueData.IsStart)
            && !selecting)
        {
            pendingAdvance = false;
            dialogueData.IsStart = false;

            if (DialogueUI.IsTyping())
            {
                if (index > 0 && index - 1 < dialogueData.lines.Length)
                {
                    DialogueUI.StopTyping();
                    DialogueUI.SkipText(dialogueData.lines[index - 1].text);
                }
                return;
            }

            if (index >= dialogueData.lines.Length)
            {
                ForceEndDia();
                return;
            }

            if (dialogueData.lines[index].characterName == "end")
            {
                if (dialogueData.TailDia)
                {
                    OnDialogueEnd?.Invoke();
                    OnDialogueEnd = null;
                    dialogueData.TailDia.DetonateEvent();
                    return;
                }

                ForceEndDia();
                return;
            }

            if (InterogationManager.Instance != null && InterogationManager.Instance.InterogationState != InterogationState.Idle)
            {
                if (CameraFocusControl.Instance != null)
                {
                    CameraFocusControl.Instance.FocusTo(dialogueData.lines[index].CamNumber);
                }

                if (InterogationManager.Instance.DTB_Setter != null)
                {
                    InterogationManager.Instance.DTB_Setter.SetPosition(dialogueData.lines[index].CamNumber);
                }
            }

            DialogueUI.DisplayDialogue(dialogueData.lines[index]);
            index++;
        }

        if (dialogueData.Choices
            && !DialogueUI.IsTyping()
            && IsDiaEnd())
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void ForceEndDia()
    {
        if (MainLoop.Instance != null)
        {
            MainLoop.Instance.SetMainLoopState(stateAfterDialogue);
        }
        selecting = false;
        if (ChoiceUI != null)
        {
            ChoiceUI.SetSelfActive(false);
        }
        if (DialogueUI != null)
        {
            DialogueUI.SetCanvasActive(false);
            DialogueUI.SetChoicesUIActive(false);
        }
        dialogueStartFlag = false;

        if (stateAfterDialogue == MainState.Interogate && InterogationManager.Instance != null)
        {
            InterogationManager.Instance.ReturnToSelection();
        }

        StaticOnDialogueEnd?.Invoke();
        OnDialogueEnd?.Invoke();
        Debug.Log("dialogue end");

        dialogueData = null;

        foreach (var interactObj in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
        {
            interactObj.SetInteractableWithDelay(0.2f);
        }

        OnDialogueStart = null;
        OnDialogueEnd = null;
        activeDialogueUI = null;
        activeChoiceUI = null;
        questionFallbackDialogue = null;
        stateAfterDialogue = MainState.Main;
        pendingAdvance = false;
    }

    public bool TryStartDialogue(DialogueObject data, int startIndex = 0)
    {
        if (data == null)
        {
            return false;
        }

        DialogueEventTrigger(data);
        index = Mathf.Max(0, startIndex);
        return true;
    }

    public bool TryStartDialogue(
        DialogueObject data,
        DialogueUIManager overrideUI,
        ChoicesUIControler overrideChoiceUI,
        int startIndex = 0,
        DialogueObject fallbackQuestionDialogue = null,
        MainState endState = MainState.Main)
    {
        activeDialogueUI = overrideUI;
        activeChoiceUI = overrideChoiceUI;
        questionFallbackDialogue = fallbackQuestionDialogue;
        stateAfterDialogue = endState;
        return TryStartDialogue(data, startIndex);
    }

    public void ChoiceEvent()
    {
        if (dialogueData == null || !dialogueData.Choices) return;
        selecting = true;
        DialogueUI.InitChoiceUI(dialogueData.Choices);
        DialogueUI.SetChoicesUIActive(true);
        if (ChoiceUI != null)
        {
            ChoiceUI.IndicateByIdx(0);
        }
    }

    public void QuestionEvent()
    {
        if (dialogueData == null || index <= 0 || index - 1 >= dialogueData.lines.Length)
        {
            Debug.LogWarning("QuestionEvent called with an invalid dialogue index.");
            return;
        }

        var currentDialogue = dialogueData;
        var questionDialogue = CloneDialogue(dialogueData.lines[index - 1].QuestionDia as InterogationDiaObject);
        if (!questionDialogue)
        {
            questionDialogue = CloneDialogue(questionFallbackDialogue as InterogationDiaObject);
            if (!questionDialogue)
            {
                Debug.LogWarning("QuestionEvent could not find a fallback interrogation dialogue.");
                ForceEndDia();
                return;
            }
        }

        questionDialogue.TailDia = currentDialogue;
        questionDialogue.continueIdx = index - 1;
        questionDialogue.OnEnd.AddListener(() =>
        {
            InterogationManager.Instance.InterogationState = InterogationState.Testify;
        });

        TryStartDialogue(questionDialogue, DialogueUI, ChoiceUI, 0, questionFallbackDialogue, stateAfterDialogue);
    }

    public void Log(string txt)
    {
        Debug.Log(txt);
    }

    private InterogationDiaObject CloneDialogue(InterogationDiaObject source)
    {
        if (!source)
        {
            return null;
        }

        return ScriptableObject.Instantiate(source);
    }
}
