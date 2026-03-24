public interface IDialogueFlowController
{
    bool IsDialogueRunning { get; }
    DialogueUIManager DialogueUI { get; }
    ChoicesUIControler ChoiceUI { get; }

    void SetSelecting(bool val);
    bool IsDiaEnd();
    void ChoiceEvent();
    bool TryStartDialogue(DialogueObject data, int startIndex = 0);
}

public static class DialogueFlowLocator
{
    public static IDialogueFlowController GetActive()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueRunning)
        {
            return DialogueManager.Instance;
        }

        return DialogueManager.Instance;
    }
}