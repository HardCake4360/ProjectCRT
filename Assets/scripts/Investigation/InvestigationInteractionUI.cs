using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InvestigationInteractionUI : MonoBehaviour
{
    public static InvestigationInteractionUI Instance { get; private set; }
    public const string ResourcePath = "prefab/NpcInvestigationUI";

    [Header("Optional Existing References")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text transcriptText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text signalText;
    [SerializeField] private TMP_InputField intentInput;
    [SerializeField] private TMP_InputField topicInput;
    [SerializeField] private TMP_InputField evidenceInput;
    [SerializeField] private Button talkButton;
    [SerializeField] private Button askTopicButton;
    [SerializeField] private Button presentEvidenceButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private NpcDialoguePresenter dialoguePresenter;
    [SerializeField] private BioSignalPresenter signalPresenter;

    public event Action<InvestigationInteractionPayload> ActionRequested;
    public event Action CloseRequested;

    private bool uiBuilt;

    private void Awake()
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
        EnsureBuilt();
        SetVisible(false);
    }

    public void EnsureBuilt()
    {
        if (uiBuilt)
        {
            return;
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponent<Canvas>();
        }

        TryResolveReferencesFromHierarchy();

        if (dialoguePresenter == null)
        {
            dialoguePresenter = gameObject.GetComponent<NpcDialoguePresenter>();
            if (dialoguePresenter == null)
            {
                dialoguePresenter = gameObject.AddComponent<NpcDialoguePresenter>();
            }
        }

        if (signalPresenter == null)
        {
            signalPresenter = gameObject.GetComponent<BioSignalPresenter>();
            if (signalPresenter == null)
            {
                signalPresenter = gameObject.AddComponent<BioSignalPresenter>();
            }
        }

        if (!HasMinimumReferences(out string missingReferences))
        {
            Debug.LogError($"NpcInvestigationUI prefab references are incomplete. Missing: {missingReferences}", this);
            return;
        }

        dialoguePresenter.Configure(titleText, transcriptText, statusText, scrollRect);
        signalPresenter.SetDebugSignalText(signalText);

        talkButton.onClick.AddListener(() => SubmitAction("Talk"));
        askTopicButton.onClick.AddListener(() => SubmitAction("AskTopic"));
        presentEvidenceButton.onClick.AddListener(() => SubmitAction("PresentEvidence"));
        closeButton.onClick.AddListener(() => CloseRequested?.Invoke());

        uiBuilt = true;
    }

    public void Open(string npcDisplayName)
    {
        EnsureBuilt();
        SetVisible(true);
        dialoguePresenter.SetTitle($"{npcDisplayName} 조사 대화");
        SetStatus("Action을 선택해 질문을 진행하세요.");
        ResetInputs();
    }

    public void SetVisible(bool visible)
    {
        if (rootCanvas != null)
        {
            rootCanvas.enabled = visible;
        }
    }

    public void SetBusy(bool busy)
    {
        if (talkButton != null) talkButton.interactable = !busy;
        if (askTopicButton != null) askTopicButton.interactable = !busy;
        if (presentEvidenceButton != null) presentEvidenceButton.interactable = !busy;
        if (intentInput != null) intentInput.interactable = !busy;
        if (topicInput != null) topicInput.interactable = !busy;
        if (evidenceInput != null) evidenceInput.interactable = !busy;
    }

    public void SetStatus(string message)
    {
        dialoguePresenter.SetStatus(message);
    }

    public void ClearConversation()
    {
        EnsureBuilt();
        dialoguePresenter.Clear();
        ResetInputs();
    }

    public void AppendPlayerMessage(string text)
    {
        EnsureBuilt();
        dialoguePresenter.AppendPlayerMessage(text);
    }

    public void AppendNpcMessage(string npcDisplayName, string text)
    {
        EnsureBuilt();
        dialoguePresenter.AppendNpcMessage(npcDisplayName, text);
    }

    public void BeginNpcStreamingMessage(string npcDisplayName)
    {
        EnsureBuilt();
        dialoguePresenter.BeginStreamingNpcMessage(npcDisplayName);
    }

    public void UpdateNpcStreamingMessage(string npcDisplayName, string text)
    {
        EnsureBuilt();
        dialoguePresenter.UpdateStreamingNpcMessage(npcDisplayName, text);
    }

    public void CommitNpcStreamingMessage(string npcDisplayName, string text)
    {
        EnsureBuilt();
        dialoguePresenter.CommitStreamingNpcMessage(npcDisplayName, text);
    }

    public void CancelNpcStreamingMessage()
    {
        EnsureBuilt();
        dialoguePresenter.CancelStreamingNpcMessage();
    }

    public void AppendSystemMessage(string text)
    {
        EnsureBuilt();
        dialoguePresenter.AppendSystemMessage(text);
    }

    public void PresentSignal(BioSignalPayload signal)
    {
        EnsureBuilt();
        signalPresenter.Present(signal);
    }

    private void SubmitAction(string actionType)
    {
        string intent = intentInput != null ? intentInput.text.Trim() : string.Empty;
        string topicId = topicInput != null ? topicInput.text.Trim() : string.Empty;
        string evidenceId = evidenceInput != null ? evidenceInput.text.Trim() : string.Empty;

        var payload = new InvestigationInteractionPayload
        {
            actionType = actionType,
            playerIntentText = BuildIntentText(actionType, intent, topicId, evidenceId),
            topicId = string.IsNullOrWhiteSpace(topicId) ? null : topicId,
            evidenceId = string.IsNullOrWhiteSpace(evidenceId) ? null : evidenceId
        };

        ActionRequested?.Invoke(payload);
        ResetInputs(clearIntentOnly: false);
    }

    private string BuildIntentText(string actionType, string intent, string topicId, string evidenceId)
    {
        if (!string.IsNullOrWhiteSpace(intent))
        {
            return intent;
        }

        return actionType switch
        {
            "AskTopic" => string.IsNullOrWhiteSpace(topicId) ? "새 주제를 물어본다." : $"{topicId}에 대해 묻는다.",
            "PresentEvidence" => string.IsNullOrWhiteSpace(evidenceId) ? "증거를 제시하며 반응을 본다." : $"{evidenceId} 증거를 제시하며 설명을 요구한다.",
            _ => "현재 상황에 대해 다시 묻는다."
        };
    }

    private void ResetInputs(bool clearIntentOnly = false)
    {
        if (intentInput != null)
        {
            intentInput.text = string.Empty;
        }

        if (clearIntentOnly)
        {
            return;
        }

        if (topicInput != null)
        {
            topicInput.text = string.Empty;
        }

        if (evidenceInput != null)
        {
            evidenceInput.text = string.Empty;
        }
    }

    public static InvestigationInteractionUI GetOrCreateInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab);
            return instance.GetComponent<InvestigationInteractionUI>();
        }

        Debug.LogError($"Failed to load investigation UI prefab from Resources/{ResourcePath}.");
        return null;
    }

    private void TryResolveReferencesFromHierarchy()
    {
        rootCanvas ??= GetComponent<Canvas>();
        panelRoot ??= FindNamedRectTransform("Panel");
        titleText ??= FindNamedComponent<TMP_Text>("TitleText");
        statusText ??= FindNamedComponent<TMP_Text>("StatusText");
        signalText ??= FindNamedComponent<TMP_Text>("SignalText");
        transcriptText ??= FindNamedComponent<TMP_Text>("TranscriptText");
        intentInput ??= FindNamedComponent<TMP_InputField>("IntentInput");
        topicInput ??= FindNamedComponent<TMP_InputField>("TopicInput");
        evidenceInput ??= FindNamedComponent<TMP_InputField>("EvidenceInput");
        talkButton ??= FindNamedComponent<Button>("TalkButton");
        askTopicButton ??= FindNamedComponent<Button>("AskTopicButton");
        presentEvidenceButton ??= FindNamedComponent<Button>("PresentEvidenceButton");
        closeButton ??= FindNamedComponent<Button>("CloseButton");
        scrollRect ??= FindNamedComponent<ScrollRect>("ScrollView");
        dialoguePresenter ??= GetComponent<NpcDialoguePresenter>();
        signalPresenter ??= GetComponent<BioSignalPresenter>();

        ResolveButtonsFromComponentScan();
        EnsureButtonComponents();
    }

    private RectTransform FindNamedRectTransform(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == objectName && child is RectTransform rectTransform)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == objectName)
            {
                T component = child.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private bool HasMinimumReferences(out string missingReferences)
    {
        List<string> missing = new();

        if (rootCanvas == null) missing.Add(nameof(rootCanvas));
        if (panelRoot == null) missing.Add(nameof(panelRoot));
        if (titleText == null) missing.Add(nameof(titleText));
        if (transcriptText == null) missing.Add(nameof(transcriptText));
        if (statusText == null) missing.Add(nameof(statusText));
        if (signalText == null) missing.Add(nameof(signalText));
        if (intentInput == null) missing.Add(nameof(intentInput));
        if (talkButton == null) missing.Add(nameof(talkButton));
        if (askTopicButton == null) missing.Add(nameof(askTopicButton));
        if (presentEvidenceButton == null) missing.Add(nameof(presentEvidenceButton));
        if (closeButton == null) missing.Add(nameof(closeButton));
        if (scrollRect == null) missing.Add(nameof(scrollRect));
        if (dialoguePresenter == null) missing.Add(nameof(dialoguePresenter));
        if (signalPresenter == null) missing.Add(nameof(signalPresenter));

        missingReferences = missing.Count == 0 ? string.Empty : string.Join(", ", missing);
        return missing.Count == 0;
    }

    private void ResolveButtonsFromComponentScan()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            switch (button.gameObject.name)
            {
                case "TalkButton":
                    talkButton ??= button;
                    break;
                case "AskTopicButton":
                    askTopicButton ??= button;
                    break;
                case "PresentEvidenceButton":
                    presentEvidenceButton ??= button;
                    break;
                case "CloseButton":
                    closeButton ??= button;
                    break;
            }
        }
    }

    private void EnsureButtonComponents()
    {
        talkButton ??= EnsureButtonComponent("TalkButton");
        askTopicButton ??= EnsureButtonComponent("AskTopicButton");
        presentEvidenceButton ??= EnsureButtonComponent("PresentEvidenceButton");
        closeButton ??= EnsureButtonComponent("CloseButton");
    }

    private Button EnsureButtonComponent(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name != objectName)
            {
                continue;
            }

            Button button = child.GetComponent<Button>();
            if (button == null)
            {
                // Skip corrupted nodes that are already using another Selectable.
                Selectable existingSelectable = child.GetComponent<Selectable>();
                if (existingSelectable != null)
                {
                    continue;
                }

                Image image = child.GetComponent<Image>();
                if (image == null)
                {
                    image = child.gameObject.AddComponent<Image>();
                    image.color = new Color(0.18f, 0.24f, 0.34f, 1f);
                }

                button = child.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                Debug.LogWarning($"Missing Button component on '{objectName}' was restored at runtime.", child.gameObject);
            }

            return button;
        }

        return CreateFallbackButton(objectName);
    }

    private Button CreateFallbackButton(string objectName)
    {
        if (panelRoot == null)
        {
            return null;
        }

        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panelRoot, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.sizeDelta = new Vector2(160f, 52f);

        switch (objectName)
        {
            case "TalkButton":
                rectTransform.anchorMin = new Vector2(0f, 0f);
                rectTransform.anchorMax = new Vector2(0.333f, 0f);
                rectTransform.anchoredPosition = new Vector2(0f, 24f);
                rectTransform.sizeDelta = new Vector2(-12f, 58f);
                break;
            case "AskTopicButton":
                rectTransform.anchorMin = new Vector2(0.333f, 0f);
                rectTransform.anchorMax = new Vector2(0.666f, 0f);
                rectTransform.anchoredPosition = new Vector2(0f, 24f);
                rectTransform.sizeDelta = new Vector2(-12f, 58f);
                break;
            case "PresentEvidenceButton":
                rectTransform.anchorMin = new Vector2(0.666f, 0f);
                rectTransform.anchorMax = new Vector2(1f, 0f);
                rectTransform.anchoredPosition = new Vector2(0f, 24f);
                rectTransform.sizeDelta = new Vector2(-12f, 58f);
                break;
            case "CloseButton":
                rectTransform.anchorMin = new Vector2(1f, 1f);
                rectTransform.anchorMax = new Vector2(1f, 1f);
                rectTransform.pivot = new Vector2(1f, 1f);
                rectTransform.anchoredPosition = new Vector2(-18f, -18f);
                rectTransform.sizeDelta = new Vector2(34f, 34f);
                break;
        }

        Image image = buttonObject.GetComponent<Image>();
        image.color = objectName switch
        {
            "AskTopicButton" => new Color(0.27f, 0.24f, 0.11f, 1f),
            "PresentEvidenceButton" => new Color(0.31f, 0.17f, 0.17f, 1f),
            "CloseButton" => new Color(0.18f, 0.22f, 0.28f, 1f),
            _ => new Color(0.18f, 0.24f, 0.34f, 1f)
        };

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = objectName switch
        {
            "AskTopicButton" => "Ask Topic",
            "PresentEvidenceButton" => "Present Evidence",
            "CloseButton" => "X",
            _ => "Talk"
        };
        label.font = titleText != null ? titleText.font : TMP_Settings.defaultFontAsset;
        label.fontSize = objectName == "CloseButton" ? 18f : 18f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        Debug.LogWarning($"Created fallback runtime button for '{objectName}' because prefab binding was invalid.", buttonObject);
        return button;
    }
}
