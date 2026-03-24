using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DialogueUIManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool showPortrait;
    [SerializeField] private bool useCharInterval;

    [Header("Properties")]
    public Canvas canvas;
    public PortraitAnimator portraitAnim;
    public TMP_Text nameText;
    public TMP_Text dialogueText;
    public GameObject ChoicesUI;

    public GameObject Portrait;
    public GameObject Name;

    private PortraitManager portraitManager;
    public UnityEvent OnTypingComplete;

    private Coroutine typingCorutine;
    [SerializeField] private bool isTyping;

    private SpriteSheetObject currentSheet;
    private SpriteSheetObject prevSheet;

    public bool IsTyping() => isTyping;
    public void SetCanvasActive(bool val)
    {
        if (canvas != null)
        {
            canvas.enabled = val;
        }
    }

    void Start()
    {
        portraitManager = FindFirstObjectByType<PortraitManager>();
        isTyping = false;
    }

    public void DisplayDialogue(DialogueObject.DialogueLine line)
    {
        StopTyping();

        if (showPortrait && portraitManager != null)
        {
            if (line.characterName == "null")
            {
                if (Portrait != null) Portrait.SetActive(false);
                if (Name != null) Name.SetActive(false);
            }
            else
            {
                if (Portrait != null) Portrait.SetActive(true);
                if (Name != null) Name.SetActive(true);

                if (nameText != null)
                {
                    nameText.text = line.characterName;
                }

                currentSheet = portraitManager.GetPortrait(line.characterName, line.Portrait);

                if (prevSheet != currentSheet && portraitAnim != null)
                {
                    portraitAnim.SetPortrait(currentSheet);
                    prevSheet = currentSheet;
                }
            }
        }

        line.OnLineStart?.Invoke();
        typingCorutine = StartCoroutine(TypeText(line));
    }

    IEnumerator TypeText(DialogueObject.DialogueLine line)
    {
        string text = line.text;
        isTyping = true;
        if (dialogueText != null)
        {
            dialogueText.text = "";
        }
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                int end = text.IndexOf('>', i);
                if (end != -1)
                {
                    string specialWord = text.Substring(i + 1, end - i - 1);
                    if (dialogueText != null)
                    {
                        dialogueText.text += specialWord;
                    }
                    i = end + 1;
                    continue;
                }
            }

            if (dialogueText != null)
            {
                dialogueText.text += text[i];
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTypeSFX();
            }
            i++;

            if (useCharInterval)
            {
                yield return new WaitForSeconds(line.CharInterval);
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }
        }

        isTyping = false;
        typingCorutine = null;
        handleEndEvent();
    }

    public void SkipText(string text)
    {
        if (dialogueText != null)
        {
            dialogueText.text = text;
        }
        isTyping = false;
        typingCorutine = null;
        handleEndEvent();
    }

    public void StopTyping()
    {
        if (typingCorutine != null)
        {
            StopCoroutine(typingCorutine);
            typingCorutine = null;
        }

        isTyping = false;
    }

    private void handleEndEvent()
    {
        var activeFlow = DialogueFlowLocator.GetActive();
        if (activeFlow != null && activeFlow.IsDiaEnd())
        {
            OnTypingComplete?.Invoke();
            activeFlow.ChoiceEvent();
        }
    }

    public void InitChoiceUI(ChoicesObj choices)
    {
        if (ChoicesUI == null)
        {
            return;
        }

        var choiceUIController = ChoicesUI.GetComponent<ChoicesUIControler>();
        if (choiceUIController == null)
        {
            return;
        }

        choiceUIController.SetChoices(choices);
        choiceUIController.InstantiateChoices();
        Debug.Log("Choice UI Initiate Complete");
    }

    public void SetChoicesUIActive(bool val)
    {
        if (ChoicesUI != null)
        {
            ChoicesUI.SetActive(val);
        }
    }
}