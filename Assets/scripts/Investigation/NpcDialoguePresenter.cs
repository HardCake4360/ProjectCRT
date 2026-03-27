using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NpcDialoguePresenter : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text transcriptText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private ScrollRect scrollRect;

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
        if (transcriptText != null)
        {
            transcriptText.text = string.Empty;
        }
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

    private void AppendLine(string text)
    {
        if (transcriptText == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(transcriptText.text))
        {
            transcriptText.text += "\n\n";
        }

        transcriptText.text += text;
        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
