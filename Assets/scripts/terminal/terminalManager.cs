using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class terminalManager : MonoBehaviour
{
    public GameObject dirLine;
    public GameObject responseLine;

    public MeshTMPInputField terminalInput;
    public GameObject userInputLine;
    public ScrollRect scrollRect;
    public GameObject msgList;

    public List<ClueObj> Clues;

    Interpreter interpreter;
    private RectTransform msgListRect;
    private bool isProcessingInput;

    private void Start()
    {
        interpreter = GetComponent<Interpreter>();
        msgListRect = msgList != null ? msgList.GetComponent<RectTransform>() : null;

        foreach (var clue in Clues)
        {
            clue.isCleared = false;
        }
    }

    private void OnGUI()
    {
        if (CanSubmitInput())
        {
            SubmitInput();
        }
    }

    private bool CanSubmitInput()
    {
        return !isProcessingInput
            && terminalInput != null
            && terminalInput.inputField != null
            && terminalInput.isFocused
            && terminalInput.inputField.text != ""
            && Input.GetKeyDown(KeyCode.Return)
            && interpreter != null;
    }

    private void SubmitInput()
    {
        string userInput = terminalInput.inputField.text;
        isProcessingInput = true;

        ClearInputField();
        AddUserLine(userInput);

        GameObject response = Instantiate(responseLine, msgList.transform);
        response.transform.SetAsLastSibling();
        ResizeMessageList(35f);

        userInputLine.transform.SetAsLastSibling();
        userInputLine.SetActive(false);

        TextMeshProUGUI uiText = response.GetComponentInChildren<TextMeshProUGUI>();

        StartCoroutine(interpreter.Interpret(userInput, uiText, () =>
        {
            HandleInterpretationCompleted(uiText);
        }));
    }

    void ClearInputField()
    {
        terminalInput.inputField.text = "";
    }

    void AddUserLine(string userInput)
    {
        ResizeMessageList(35f);

        GameObject msg = Instantiate(dirLine, msgList.transform);
        msg.transform.SetSiblingIndex(msgList.transform.childCount - 1);
        msg.GetComponentsInChildren<TextMeshProUGUI>()[1].text = userInput;
    }

    int AddInterpreterLines(List<string> interpretation)
    {
        for(int i = 0; i < interpretation.Count; i++)
        {
            GameObject response = Instantiate(responseLine, msgList.transform);
            response.transform.SetAsLastSibling();
            ResizeMessageList(35f);
            response.GetComponentInChildren<TextMeshProUGUI>().text = interpretation[i];
        }

        return interpretation.Count;
    }

    void ScrollToBottom(int lines)
    {
        if(lines > 4)
        {
            scrollRect.velocity = new Vector2(0, 900);
        }
        else
        {
            scrollRect.verticalNormalizedPosition = 0;
        }
    }

    private void ResizeMessageList(float deltaHeight)
    {
        if (msgListRect == null)
        {
            return;
        }

        Vector2 size = msgListRect.sizeDelta;
        msgListRect.sizeDelta = new Vector2(size.x, size.y + deltaHeight);
    }

    private void HandleInterpretationCompleted(TextMeshProUGUI uiText)
    {
        isProcessingInput = false;

        userInputLine.SetActive(true);
        userInputLine.transform.SetAsLastSibling();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0;
        }

        StartCoroutine(CheckCluesSequentially(uiText != null ? uiText.text : string.Empty));

        if (terminalInput?.inputField != null)
        {
            terminalInput.inputField.ActivateInputField();
            terminalInput.inputField.Select();
        }
    }

    private IEnumerator CheckCluesSequentially(string text)
    {
        if (DialogueManager.Instance == null)
        {
            yield break;
        }

        foreach (var clue in Clues)
        {
            if (clue.isCleared) continue;
            Debug.Log("----------------checking " + clue.Keyword);
            bool dialogueFinished = false;

            UnityAction onEnd = () => dialogueFinished = true;
            DialogueManager.Instance.StaticOnDialogueEnd.AddListener(onEnd);

            bool isContaining = clue.FindKeywordFrom(text);

            while (isContaining && !dialogueFinished)
                yield return null;

            DialogueManager.Instance.StaticOnDialogueEnd.RemoveListener(onEnd);
        }
        Debug.Log("checking end");
    }
}