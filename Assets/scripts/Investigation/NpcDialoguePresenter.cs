using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class NpcDialoguePresenter : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text transcriptText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private ScrollRect scrollRect;
    private readonly List<string> committedEntries = new();
    private string streamingEntry;

    public void Configure(TMP_Text title, TMP_Text transcript, TMP_Text status, ScrollRect targetScrollRect)
    {
        titleText = title;
        transcriptText = transcript;
        statusText = status;
        scrollRect = targetScrollRect;
    }

    public void SetTitle(string value)
    {
        if (titleText != null)
        {
            titleText.text = value;
        }
    }

    public void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }
    }

    public void Clear()
    {
        committedEntries.Clear();
        streamingEntry = null;
        RenderTranscript();
    }

    public void AppendPlayerMessage(string text)
    {
        AppendLine($"<align=\"right\"><color=#BFD7FF>You</color>\n{text}</align>");
    }

    public void AppendNpcMessage(string speakerName, string text)
    {
        AppendLine($"<color=#F6D77E>{speakerName}</color>\n{text}");
    }

    public void AppendSystemMessage(string text)
    {
        AppendLine($"<align=\"center\"><color=#8F9AA8>{text}</color></align>");
    }

    public void BeginStreamingNpcMessage(string speakerName)
    {
        streamingEntry = $"<color=#F6D77E>{speakerName}</color>\n";
        RenderTranscript();
    }

    public void UpdateStreamingNpcMessage(string speakerName, string text)
    {
        streamingEntry = $"<color=#F6D77E>{speakerName}</color>\n{text}";
        RenderTranscript();
    }

    public void CommitStreamingNpcMessage(string speakerName, string text)
    {
        streamingEntry = null;
        AppendNpcMessage(speakerName, text);
    }

    public void CancelStreamingNpcMessage()
    {
        streamingEntry = null;
        RenderTranscript();
    }

    private void AppendLine(string text)
    {
        committedEntries.Add(text);
        RenderTranscript();
    }

    private void RenderTranscript()
    {
        if (transcriptText == null)
        {
            return;
        }

        List<string> lines = new(committedEntries);
        if (!string.IsNullOrEmpty(streamingEntry))
        {
            lines.Add(streamingEntry);
        }

        transcriptText.text = string.Join("\n\n", lines);
        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
