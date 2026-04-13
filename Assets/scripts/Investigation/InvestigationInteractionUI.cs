using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
    [SerializeField] private TMP_InputField intentInput;
    [SerializeField] private TMP_InputField topicInput;
    [SerializeField] private TMP_InputField evidenceInput;
    [FormerlySerializedAs("talkButton")]
    [SerializeField] private Button sendButton;
    [SerializeField] private Button askTopicButton;
    [SerializeField] private Button presentEvidenceButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button floatingActionButton;
    [SerializeField] private RectTransform speedDialRoot;
    [SerializeField] private RectTransform selectionListRoot;
    [SerializeField] private RectTransform attachmentBarRoot;
    [SerializeField] private TMP_Text attachmentSummaryText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private NpcDialoguePresenter dialoguePresenter;
    [SerializeField] private TellMeterPresenter tellMeterPresenter;
    [SerializeField] private AffectPlanePresenter affectPlanePresenter;

    [Header("Tell Meter")]
    [SerializeField] private float minimumPulseBpm = 48f;
    [SerializeField] private float maximumPulseBpm = 132f;
    [SerializeField] private float pulseDurationSeconds = 0.26f;

    public event Action<InvestigationInteractionPayload> ActionRequested;
    public event Action CloseRequested;

    private const int MaxSelectedEvidenceCount = 3;
    private readonly List<string> availableTopicIds = new();
    private readonly List<string> availableEvidenceIds = new();
    private readonly List<string> availableInformationIds = new();
    private readonly List<string> selectedEvidenceIds = new();
    private readonly List<string> selectedInformationIds = new();
    private string selectedTopicId;
    private bool uiBuilt;
    private bool isBusy;
    private bool speedDialOpen;

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

    private void Update()
    {
        if (!uiBuilt || isBusy || intentInput == null || !intentInput.isFocused)
        {
            return;
        }

        bool pressedEnter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool wantsNewLine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (pressedEnter && !wantsNewLine)
        {
            SubmitAction("Talk");
        }
    }

    public void EnsureBuilt()
    {
        if (uiBuilt)
        {
            return;
        }

        rootCanvas ??= GetComponent<Canvas>();
        TryResolveReferencesFromHierarchy();

        if (dialoguePresenter == null)
        {
            dialoguePresenter = gameObject.GetComponent<NpcDialoguePresenter>();
            if (dialoguePresenter == null)
            {
                dialoguePresenter = gameObject.AddComponent<NpcDialoguePresenter>();
            }
        }

        if (!HasMinimumReferences(out string missingReferences))
        {
            Debug.LogWarning($"NpcInvestigationUI prefab references are incomplete. Missing: {missingReferences}", this);
            return;
        }

        intentInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        dialoguePresenter.Configure(titleText, transcriptText, statusText, scrollRect);
        tellMeterPresenter?.EnsureBuilt(minimumPulseBpm, maximumPulseBpm, pulseDurationSeconds);
        affectPlanePresenter?.EnsureBuilt();
        EnsureInvestigationActionUI();

        sendButton.onClick.AddListener(() => SubmitAction("Talk"));
        askTopicButton.onClick.AddListener(ShowTopicSelection);
        presentEvidenceButton.onClick.AddListener(ShowEvidenceSelection);
        floatingActionButton?.onClick.AddListener(ToggleSpeedDial);
        closeButton.onClick.AddListener(() => CloseRequested?.Invoke());

        SetSpeedDialOpen(false);
        SetSelectionListVisible(false);
        RefreshAttachmentSummary();
        uiBuilt = true;
    }

    public void Open(string npcDisplayName)
    {
        EnsureBuilt();
        SetVisible(true);
        dialoguePresenter.SetTitle($"{npcDisplayName} Investigation");
        SetStatus("Type a message, attach a topic or evidence if needed, then send.");
        ResetInputs();
        SetSpeedDialOpen(false);
        SetSelectionListVisible(false);
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
        isBusy = busy;
        if (sendButton != null) sendButton.interactable = !busy;
        if (askTopicButton != null) askTopicButton.interactable = !busy;
        if (presentEvidenceButton != null) presentEvidenceButton.interactable = !busy;
        if (floatingActionButton != null) floatingActionButton.interactable = !busy;
        if (intentInput != null) intentInput.interactable = !busy;
        if (topicInput != null) topicInput.interactable = !busy;
        if (evidenceInput != null) evidenceInput.interactable = !busy;

        if (busy)
        {
            SetSpeedDialOpen(false);
            SetSelectionListVisible(false);
        }
    }

    public void ConfigureSelectionOptions(IReadOnlyList<string> topicIds, IReadOnlyList<string> evidenceIds, IReadOnlyList<string> informationIds = null)
    {
        EnsureBuilt();
        availableTopicIds.Clear();
        availableEvidenceIds.Clear();
        availableInformationIds.Clear();

        AddUniqueOptions(availableTopicIds, topicIds);
        AddUniqueOptions(availableEvidenceIds, evidenceIds);
        AddUniqueOptions(availableInformationIds, informationIds);
        AddUniqueOptions(availableEvidenceIds, informationIds);
    }

    public void SetStatus(string message)
    {
        dialoguePresenter.SetStatus(message);
    }

    public void SetTellValue(float tell)
    {
        EnsureBuilt();
        tellMeterPresenter?.SetTell(tell);
    }

    public void ResetTellValue(float tell = 0f)
    {
        EnsureBuilt();
        tellMeterPresenter?.ResetTell(tell);
    }

    public void SetAffectValue(float interest, float attitude)
    {
        EnsureBuilt();
        affectPlanePresenter?.SetAffect(interest, attitude);
    }

    public void ResetAffectValue(float interest = 0f, float attitude = 0f)
    {
        EnsureBuilt();
        affectPlanePresenter?.ResetAffect(interest, attitude);
    }

    public void SetAffectPending(bool pending)
    {
        EnsureBuilt();
        affectPlanePresenter?.SetPendingState(pending);
    }

    public void SetPatienceValue(int patience)
    {
        EnsureBuilt();
    }

    public void SetTellPending(bool pending)
    {
        EnsureBuilt();
        tellMeterPresenter?.SetPendingState(pending);
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

    private void SubmitAction(string actionType)
    {
        string intent = intentInput != null ? intentInput.text.Trim() : string.Empty;
        string topicId = !string.IsNullOrWhiteSpace(selectedTopicId)
            ? selectedTopicId
            : topicInput != null ? topicInput.text.Trim() : string.Empty;
        List<string> evidenceIds = new(selectedEvidenceIds);
        string evidenceId = evidenceIds.Count > 0
            ? evidenceIds[0]
            : evidenceInput != null ? evidenceInput.text.Trim() : string.Empty;

        if (!string.IsNullOrWhiteSpace(evidenceId) && !evidenceIds.Contains(evidenceId))
        {
            evidenceIds.Add(evidenceId);
        }

        string resolvedActionType = ResolveActionType(actionType, topicId, evidenceIds);
        var payload = new InvestigationInteractionPayload
        {
            actionType = resolvedActionType,
            playerIntentText = BuildIntentText(resolvedActionType, intent, topicId, evidenceIds),
            topicId = string.IsNullOrWhiteSpace(topicId) ? null : topicId,
            evidenceId = string.IsNullOrWhiteSpace(evidenceId) ? null : evidenceId,
            evidenceIds = evidenceIds,
            informationIds = new List<string>(selectedInformationIds)
        };

        ActionRequested?.Invoke(payload);
        ResetInputs(clearIntentOnly: false);
    }

    private string ResolveActionType(string actionType, string topicId, IReadOnlyList<string> evidenceIds)
    {
        if (actionType != "Talk")
        {
            return actionType;
        }

        if (evidenceIds != null && evidenceIds.Count > 0)
        {
            return "PresentEvidence";
        }

        if (!string.IsNullOrWhiteSpace(topicId))
        {
            return "AskTopic";
        }

        return "Talk";
    }

    private string BuildIntentText(string actionType, string intent, string topicId, IReadOnlyList<string> evidenceIds)
    {
        if (!string.IsNullOrWhiteSpace(intent))
        {
            return intent;
        }

        return actionType switch
        {
            "AskTopic" => string.IsNullOrWhiteSpace(topicId) ? "Ask about an unlocked topic." : $"Ask about topic: {topicId}.",
            "PresentEvidence" => evidenceIds == null || evidenceIds.Count == 0 ? "Present evidence." : $"Present evidence: {string.Join(", ", evidenceIds)}.",
            _ => "Ask about the current situation."
        };
    }

    private void ShowTopicSelection()
    {
        SetSpeedDialOpen(false);
        RebuildSelectionList("Unlocked Topics", availableTopicIds, false);
    }

    private void ShowEvidenceSelection()
    {
        SetSpeedDialOpen(false);
        RebuildSelectionList("Evidence", availableEvidenceIds, true);
    }

    private void ToggleSpeedDial()
    {
        SetSelectionListVisible(false);
        SetSpeedDialOpen(!speedDialOpen);
    }

    private void SelectTopic(string topicId)
    {
        selectedTopicId = selectedTopicId == topicId ? null : topicId;
        if (topicInput != null)
        {
            topicInput.text = selectedTopicId ?? string.Empty;
        }

        RefreshAttachmentSummary();
        SetSelectionListVisible(false);
    }

    private void ToggleEvidence(string evidenceId)
    {
        if (selectedEvidenceIds.Contains(evidenceId))
        {
            selectedEvidenceIds.Remove(evidenceId);
            selectedInformationIds.Remove(evidenceId);
        }
        else if (selectedEvidenceIds.Count >= MaxSelectedEvidenceCount)
        {
            SetStatus($"Evidence limit reached ({MaxSelectedEvidenceCount}).");
            return;
        }
        else
        {
            selectedEvidenceIds.Add(evidenceId);
            if (availableInformationIds.Contains(evidenceId) && !selectedInformationIds.Contains(evidenceId))
            {
                selectedInformationIds.Add(evidenceId);
            }
        }

        if (evidenceInput != null)
        {
            evidenceInput.text = string.Join(", ", selectedEvidenceIds);
        }

        RefreshAttachmentSummary();
        RebuildSelectionList("Evidence", availableEvidenceIds, true);
    }

    private void RebuildSelectionList(string title, IReadOnlyList<string> items, bool evidenceMode)
    {
        if (selectionListRoot == null)
        {
            Debug.LogWarning("SelectionListRoot is missing from NpcInvestigationUI prefab. Cannot show attachment choices.", this);
            return;
        }

        ClearChildren(selectionListRoot);
        SetSelectionListVisible(true);

        CreateSelectionLabel(title, true);
        if (items == null || items.Count == 0)
        {
            CreateSelectionLabel(evidenceMode ? "No discovered evidence." : "No unlocked topics.", false);
            return;
        }

        foreach (string itemId in items)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                continue;
            }

            bool selected = evidenceMode ? selectedEvidenceIds.Contains(itemId) : selectedTopicId == itemId;
            Button optionButton = CreateTextButton(selectionListRoot, selected ? $"* {itemId}" : itemId, new Vector2(280f, 34f));
            string capturedId = itemId;
            optionButton.onClick.AddListener(() =>
            {
                if (evidenceMode)
                {
                    ToggleEvidence(capturedId);
                }
                else
                {
                    SelectTopic(capturedId);
                }
            });
        }
    }

    private void CreateSelectionLabel(string text, bool header)
    {
        GameObject labelObject = new GameObject(header ? "SelectionHeader" : "SelectionEmpty", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(selectionListRoot, false);
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = text;
        label.font = titleText != null ? titleText.font : TMP_Settings.defaultFontAsset;
        label.fontSize = header ? 16f : 13f;
        label.fontStyle = header ? FontStyles.Bold : FontStyles.Normal;
        label.color = new Color(0.92f, 0.95f, 0.98f, 1f);
    }

    private void SetSpeedDialOpen(bool open)
    {
        speedDialOpen = open;
        if (speedDialRoot != null)
        {
            speedDialRoot.gameObject.SetActive(open);
        }
    }

    private void SetSelectionListVisible(bool visible)
    {
        if (selectionListRoot != null)
        {
            selectionListRoot.gameObject.SetActive(visible);
        }
    }

    private void RefreshAttachmentSummary()
    {
        if (attachmentSummaryText == null)
        {
            return;
        }

        List<string> parts = new();
        if (!string.IsNullOrWhiteSpace(selectedTopicId))
        {
            parts.Add($"Topic: {selectedTopicId}");
        }

        if (selectedEvidenceIds.Count > 0)
        {
            List<string> evidenceOnly = new();
            List<string> informationOnly = new();
            foreach (string id in selectedEvidenceIds)
            {
                if (selectedInformationIds.Contains(id))
                {
                    informationOnly.Add(id);
                }
                else
                {
                    evidenceOnly.Add(id);
                }
            }

            if (evidenceOnly.Count > 0)
            {
                parts.Add($"Evidence: {string.Join(", ", evidenceOnly)}");
            }

            if (informationOnly.Count > 0)
            {
                parts.Add($"Information: {string.Join(", ", informationOnly)}");
            }
        }

        attachmentSummaryText.text = parts.Count == 0
            ? "No attachments"
            : string.Join("   |   ", parts);
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

        selectedTopicId = null;
        selectedEvidenceIds.Clear();
        selectedInformationIds.Clear();
        if (topicInput != null)
        {
            topicInput.text = string.Empty;
        }

        if (evidenceInput != null)
        {
            evidenceInput.text = string.Empty;
        }

        RefreshAttachmentSummary();
        SetSpeedDialOpen(false);
        SetSelectionListVisible(false);
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
        transcriptText ??= FindNamedComponent<TMP_Text>("TranscriptText");
        intentInput ??= FindNamedComponent<TMP_InputField>("IntentInput");
        topicInput ??= FindNamedComponent<TMP_InputField>("TopicInput");
        evidenceInput ??= FindNamedComponent<TMP_InputField>("EvidenceInput");
        sendButton ??= FindNamedComponent<Button>("SendButton") ?? FindNamedComponent<Button>("TalkButton");
        RenameLegacySendButton();
        askTopicButton ??= FindNamedComponent<Button>("AskTopicButton");
        presentEvidenceButton ??= FindNamedComponent<Button>("PresentEvidenceButton");
        closeButton ??= FindNamedComponent<Button>("CloseButton");
        floatingActionButton ??= FindNamedComponent<Button>("FloatingActionButton");
        speedDialRoot ??= FindNamedRectTransform("SpeedDialRoot");
        selectionListRoot ??= FindNamedRectTransform("SelectionListRoot");
        attachmentBarRoot ??= FindNamedRectTransform("AttachmentBar");
        attachmentSummaryText ??= FindNamedComponent<TMP_Text>("AttachmentSummaryText");
        scrollRect ??= FindNamedComponent<ScrollRect>("ScrollView");
        dialoguePresenter ??= GetComponent<NpcDialoguePresenter>();
        tellMeterPresenter ??= GetComponent<TellMeterPresenter>();
        affectPlanePresenter ??= GetComponent<AffectPlanePresenter>();

        if (tellMeterPresenter == null)
        {
            tellMeterPresenter = gameObject.AddComponent<TellMeterPresenter>();
        }

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
        if (intentInput == null) missing.Add(nameof(intentInput));
        if (sendButton == null) missing.Add(nameof(sendButton));
        if (askTopicButton == null) missing.Add(nameof(askTopicButton));
        if (presentEvidenceButton == null) missing.Add(nameof(presentEvidenceButton));
        if (closeButton == null) missing.Add(nameof(closeButton));

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
                case "SendButton":
                case "TalkButton":
                    sendButton ??= button;
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
                case "FloatingActionButton":
                    floatingActionButton ??= button;
                    break;
            }
        }

        RenameLegacySendButton();
    }

    private void RenameLegacySendButton()
    {
        if (sendButton != null && sendButton.gameObject.name == "TalkButton")
        {
            sendButton.gameObject.name = "SendButton";
        }
    }

    private void EnsureButtonComponents()
    {
        sendButton ??= FindNamedComponent<Button>("SendButton") ?? FindNamedComponent<Button>("TalkButton");
        RenameLegacySendButton();
        sendButton ??= FindNamedComponent<Button>("SendButton");
        askTopicButton ??= FindNamedComponent<Button>("AskTopicButton");
        presentEvidenceButton ??= FindNamedComponent<Button>("PresentEvidenceButton");
        closeButton ??= FindNamedComponent<Button>("CloseButton");
    }

    private void EnsureInvestigationActionUI()
    {
        SetButtonLabel(askTopicButton, "Ask Topic");
        SetButtonLabel(presentEvidenceButton, "Present Evidence");
        SetButtonLabel(sendButton, "Send");
        SetButtonLabel(floatingActionButton, "+");
    }

    private Button CreateTextButton(RectTransform parent, string labelText, Vector2 size)
    {
        GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.22f, 0.3f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = labelText;
        label.font = titleText != null ? titleText.font : TMP_Settings.defaultFontAsset;
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return button;
    }

    private void SetButtonLabel(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
        }
    }

    private void AddUniqueOptions(List<string> destination, IReadOnlyList<string> source)
    {
        if (source == null)
        {
            return;
        }

        foreach (string item in source)
        {
            if (!string.IsNullOrWhiteSpace(item) && !destination.Contains(item))
            {
                destination.Add(item);
            }
        }
    }

    private void ClearChildren(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

}
