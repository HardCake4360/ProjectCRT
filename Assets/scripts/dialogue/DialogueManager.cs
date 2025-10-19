using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public DialogueObject dialogueData;
    public DialogueUIManager uiManager;

    private int index = 0;
    private bool dialogueStartFlag = false;

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
        if (index == 0 || InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.dialogueAdvanceKeys))
        {
            if (dialogueData.lines[index].characterName == "end")
            { 
                uiManager.SetCanvasActive(false);
                dialogueStartFlag = false;
                return;
            }
            
            // 타이핑 중이면 스킵
            if (uiManager.IsTyping()) return;

            // 다음 대사로
            if (index < dialogueData.lines.Length)
            {
                uiManager.DisplayDialogue(dialogueData.lines[index]);
                index++;
            }
        }
    }
}
