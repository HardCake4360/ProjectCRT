using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ConversationLogHistoryUI : MonoBehaviour
{
    private const string NameListName = "nameList";
    private const string ChatLogName = "ChatLog";
    private const string GeneratedNamePrefix = "NpcLogName_";
    private const string TemplateName = "NpcLogNameTemplate";
    private const string NameListFontResourcePath = "font/NeoDunggeunmoPro-Regular 1 SDF";

    [SerializeField] private RectTransform nameListRoot;
    [SerializeField] private RectTransform chatLogRoot;

    private TMP_Text nameTemplate;
    private TMP_Text chatLogText;
    private TMP_FontAsset nameListFont;
    private readonly List<NpcConversationSaveEntry> entries = new();
    private int selectedIndex;

    public void Configure(RectTransform windowRoot)
    {
        nameListRoot = FindChildRectTransform(windowRoot, NameListName);
        chatLogRoot = FindChildRectTransform(windowRoot, ChatLogName);
        EnsureLayout();
        Refresh();
    }

    private void OnEnable()
    {
        EnsureLayout();
        Refresh();
    }

    public void SelectEntry(int index)
    {
        if (index < 0 || index >= entries.Count)
        {
            return;
        }

        selectedIndex = index;
        RebuildNameList();
        RenderChatLog(entries[selectedIndex]);
    }

    private void EnsureLayout()
    {
        nameListRoot ??= FindChildRectTransform(transform, NameListName);
        chatLogRoot ??= FindChildRectTransform(transform, ChatLogName);

        if (nameListRoot != null)
        {
            EnsureNameListLayout();
        }

        if (chatLogRoot != null)
        {
            ResolveChatLogText();
        }
    }

    private void Refresh()
    {
        if (nameListRoot == null || chatLogRoot == null)
        {
            return;
        }

        entries.Clear();
        entries.AddRange(NpcConversationSaveManager.GetAllEntries());
        entries.RemoveAll(entry => entry == null || entry.recentExchanges == null || entry.recentExchanges.Count == 0);
        entries.Sort(CompareEntries);

        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, entries.Count - 1));
        RebuildNameList();

        if (entries.Count == 0)
        {
            SetChatText("No conversation logs yet.");
            return;
        }

        RenderChatLog(entries[selectedIndex]);
    }

    private void EnsureNameListLayout()
    {
        nameListFont ??= Resources.Load<TMP_FontAsset>(NameListFontResourcePath);

        VerticalLayoutGroup layoutGroup = nameListRoot.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = nameListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layoutGroup.padding = new RectOffset(12, 12, 8, 8);
        layoutGroup.spacing = 6f;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        if (nameTemplate == null)
        {
            TMP_Text existingText = nameListRoot.GetComponentInChildren<TMP_Text>(true);
            if (existingText != null)
            {
                nameTemplate = Instantiate(existingText, nameListRoot);
                nameTemplate.name = TemplateName;
                ApplyNameTextFont(nameTemplate);
                nameTemplate.gameObject.SetActive(false);
            }
        }
    }

    private void ResolveChatLogText()
    {
        if (chatLogText != null || chatLogRoot == null)
        {
            return;
        }

        chatLogText = chatLogRoot.GetComponentInChildren<TMP_Text>(true);
    }

    private void RebuildNameList()
    {
        ClearGeneratedNames();

        if (entries.Count == 0)
        {
            TMP_Text emptyName = CreateNameText("No NPC logs", -1, false);
            emptyName.color = new Color(0.65f, 0.69f, 0.75f, 1f);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            TMP_Text nameText = CreateNameText(GetDisplayName(entries[i]), i, true);
            nameText.color = i == selectedIndex ? new Color(0.98f, 0.84f, 0.42f, 1f) : Color.white;
        }
    }

    private TMP_Text CreateNameText(string text, int index, bool selectable)
    {
        TMP_Text label;
        if (nameTemplate != null)
        {
            label = Instantiate(nameTemplate, nameListRoot);
            label.gameObject.SetActive(true);
        }
        else
        {
            GameObject labelObject = new GameObject("NpcLogName", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(nameListRoot, false);
            label = labelObject.GetComponent<TMP_Text>();
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        ApplyNameTextFont(label);
        label.name = index >= 0 ? $"{GeneratedNamePrefix}{index}" : $"{GeneratedNamePrefix}Empty";
        label.text = text;
        label.raycastTarget = selectable;
        label.textWrappingMode = TextWrappingModes.Normal;

        RectTransform labelRect = label.rectTransform;
        labelRect.sizeDelta = new Vector2(0f, 34f);

        LayoutElement layoutElement = label.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = label.gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.preferredHeight = 34f;

        ConversationLogNameItem item = label.GetComponent<ConversationLogNameItem>();
        if (selectable)
        {
            if (item == null)
            {
                item = label.gameObject.AddComponent<ConversationLogNameItem>();
            }
            item.Configure(this, index);
        }
        else if (item != null)
        {
            Destroy(item);
        }

        return label;
    }

    private void ApplyNameTextFont(TMP_Text label)
    {
        if (label == null)
        {
            return;
        }

        nameListFont ??= Resources.Load<TMP_FontAsset>(NameListFontResourcePath);
        if (nameListFont != null)
        {
            label.font = nameListFont;
        }
    }

    private void RenderChatLog(NpcConversationSaveEntry entry)
    {
        if (entry == null || entry.recentExchanges == null || entry.recentExchanges.Count == 0)
        {
            SetChatText("No conversation logs yet.");
            return;
        }

        var lines = new List<string>
        {
            $"<size=22><color=#F6D77E>{EscapeRichText(GetDisplayName(entry))}</color></size>"
        };

        foreach (InvestigationExchange exchange in entry.recentExchanges)
        {
            if (exchange == null || string.IsNullOrWhiteSpace(exchange.text))
            {
                continue;
            }

            string speaker = string.Equals(exchange.speaker, "player", StringComparison.OrdinalIgnoreCase)
                ? "You"
                : GetDisplayName(entry);
            string color = string.Equals(exchange.speaker, "player", StringComparison.OrdinalIgnoreCase)
                ? "#BFD7FF"
                : "#F6D77E";

            lines.Add($"<color={color}>{EscapeRichText(speaker)}</color>\n{EscapeRichText(exchange.text)}");
        }

        SetChatText(string.Join("\n\n", lines));
    }

    private void SetChatText(string text)
    {
        ResolveChatLogText();
        if (chatLogText == null)
        {
            return;
        }

        chatLogText.text = text;
    }

    private void ClearGeneratedNames()
    {
        if (nameListRoot == null)
        {
            return;
        }

        for (int i = nameListRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = nameListRoot.GetChild(i);
            if (child.name == TemplateName)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private static int CompareEntries(NpcConversationSaveEntry left, NpcConversationSaveEntry right)
    {
        DateTime leftTime = ParseTime(left?.lastInteractionTime);
        DateTime rightTime = ParseTime(right?.lastInteractionTime);
        int timeCompare = rightTime.CompareTo(leftTime);
        return timeCompare != 0
            ? timeCompare
            : string.Compare(GetDisplayName(left), GetDisplayName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ParseTime(string value)
    {
        return DateTime.TryParse(value, out DateTime parsed) ? parsed : DateTime.MinValue;
    }

    private static string GetDisplayName(NpcConversationSaveEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry?.npcId))
        {
            return entry.npcId;
        }

        if (!string.IsNullOrWhiteSpace(entry?.stateKey))
        {
            return entry.stateKey;
        }

        return "Unknown NPC";
    }

    private static string EscapeRichText(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static RectTransform FindChildRectTransform(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        RectTransform[] children = parent.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in children)
        {
            if (child.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }
}

public class ConversationLogNameItem : MonoBehaviour, IPointerDownHandler
{
    private ConversationLogHistoryUI owner;
    private int index;

    public void Configure(ConversationLogHistoryUI targetOwner, int targetIndex)
    {
        owner = targetOwner;
        index = targetIndex;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner?.SelectEntry(index);
        }
    }
}
