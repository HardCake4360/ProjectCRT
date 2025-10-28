using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;


public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    public DialogueObject dialogueData;
    public DialogueUIManager uiManager;

    private int index = 0;
    private bool dialogueStartFlag = false;
    private bool selecting = false;

    public UnityEvent OnDialogueEnd;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public void DialogueEventTrigger(DialogueObject data)
    {
        index = 0;
        dialogueData = data;
        uiManager.SetCanvasActive(true);//이벤트 실행했을때 UI 보이도록
        dialogueStartFlag = true;
    }

    void Update()
    {
        if (!dialogueStartFlag) return;

        if ((index == 0 ||  InputManager.Instance.IsAnyKeyPressedIn(
                            InputManager.Instance.dialogueAdvanceKeys))
             && !selecting)
        {
            // 타이핑 중이면 스킵
            if (uiManager.IsTyping())
            {
                uiManager.StopAllCoroutines();
                uiManager.SkipText(dialogueData.lines[index-1].text);
                return;
            }
            
            if (dialogueData.lines[index].characterName == "end")
            { 
                uiManager.SetCanvasActive(false);
                dialogueStartFlag = false;
                OnDialogueEnd?.Invoke();

                //모든 상호작용 오브젝트에 딜레이 생성
                foreach (var interactObj in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
                    interactObj.SetInteractableWithDelay(0.2f);
                
                return;
            }

            // 대사 타이핑 시작
            if (index < dialogueData.lines.Length)
            {
                uiManager.DisplayDialogue(dialogueData.lines[index]);
                index++;
            }
        }

        //선택지 표시, 선택 상태 진입
        if (dialogueData.lines[index].choices && !uiManager.IsTyping())
        {
            selecting = true;
            uiManager.InitChoiceUI(dialogueData.lines[index].choices);
            uiManager.SetChoicesUIActive(true);
        }
    }
}
