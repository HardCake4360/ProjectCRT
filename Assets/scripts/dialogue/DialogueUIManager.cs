using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

public class DialogueUIManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool showPortrait;

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
    private bool isTyping;
    private string fullText;

    private SpriteSheetObject currentSheet;
    private SpriteSheetObject prevSheet;

    public bool IsTyping() => isTyping;
    public void SetCanvasActive(bool val) { canvas.enabled = val; }
    void Start()
    {
        portraitManager = FindFirstObjectByType<PortraitManager>();
        isTyping = false;
        fullText = "";
        OnTypingComplete.AddListener(() =>
        {
            DialogueManager.Instance.ChoiceEvent();
        });
    }

    public void DisplayDialogue(DialogueObject.DialogueLine line)
    {
        //기존 코루틴 중단
        if (typingCorutine != null)
            StopCoroutine(typingCorutine);

        if (showPortrait)
        {
            if (line.characterName == "null")
            {
                Portrait.SetActive(false);
                Name.SetActive(false);
            }
            else
            {
                Portrait.SetActive(true);
                Name.SetActive(true);

                nameText.text = line.characterName.ToString();
                currentSheet = portraitManager.GetPortrait(line.characterName, line.portrait);

                //이전 라인과 초상화 같으면 업데이트 하지 않음 (둘이 달라야 업데이트)
                if (prevSheet != currentSheet)
                {
                    portraitAnim.SetPortrait(currentSheet);
                    prevSheet = currentSheet;
                }
            }
        }

        //새 대사 시작
        typingCorutine = StartCoroutine(TypeText(line));
    }

    IEnumerator TypeText(DialogueObject.DialogueLine line)
    {
        string text = line.text;
        isTyping = true;
        fullText = text;
        dialogueText.text = "";
        int i = 0;

        while (i < text.Length)
        {
            // 특수기호 처리 (“<”로 시작하면 “>”까지 한 번에 출력)
            if (text[i] == '<')
            {
                int end = text.IndexOf('>', i);
                if (end != -1)
                {
                    string specialWord = text.Substring(i + 1, end - i - 1);
                    dialogueText.text += specialWord;
                    i = end + 1;
                    continue;
                }
            }

            dialogueText.text += text[i];
            i++;
            yield return new WaitForSeconds(line.charInterval);
        }
        isTyping = false;
        OnTypingComplete?.Invoke();
    }

    public void SkipText(string text)
    {
        dialogueText.text = text;
        isTyping = false;
        OnTypingComplete?.Invoke();
    }

    public void InitChoiceUI(ChoicesObj choices)
    {
        var CUI = ChoicesUI.GetComponent<ChoicesUIControler>();
        CUI.SetChoices(choices);
        CUI.InstantiateChoices();
        Debug.Log("Choice UI Initiate Complete");
    }

    public void SetChoicesUIActive(bool val)
    {
        ChoicesUI.SetActive(val);
    }
}
