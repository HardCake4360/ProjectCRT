using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChoicePrefab : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMesh;
    [SerializeField] private Image indicator;
    [SerializeField] private int selfIdx;
    [SerializeField] private ReactiveButton button;

    private bool isSelected;

    public void ContextSetSelect() { if (indicator) indicator.enabled = isSelected; }

    private void Start()
    {
        if (indicator) indicator.enabled = isSelected;
    }

    void Awake()
    {
        textMesh = GetComponentInChildren<TextMeshProUGUI>();
        if (textMesh == null)
            Debug.LogError("TextMeshProUGUI not found in children!");
    }

    public void SetSelected(bool val)
    {
        isSelected = val;
        if (indicator) indicator.enabled = val;
    }

    public void InitMembers(int idx, ChoiceData cho)
    {
        textMesh.text = cho.name;
        selfIdx = idx;
        button.OnClick = cho.OnSelect;
        button.OnClick.AddListener(() =>
        {
            if (cho.OnSelectDialogue != null)
            {
                var activeFlow = DialogueFlowLocator.GetActive();
                if (activeFlow == null)
                {
                    Debug.LogWarning("No active dialogue flow found for choice selection.");
                    return;
                }

                activeFlow.SetSelecting(false);
                activeFlow.DialogueUI.SetChoicesUIActive(false);

                if (!activeFlow.TryStartDialogue(cho.OnSelectDialogue))
                {
                    Debug.LogWarning($"Choice dialogue type mismatch: {cho.OnSelectDialogue.name}");
                    return;
                }

                if (activeFlow is DialogueManager)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
        });
    }

    public void OnHover()
    {
        var activeFlow = DialogueFlowLocator.GetActive();
        if (activeFlow == null || activeFlow.ChoiceUI == null)
        {
            return;
        }

        activeFlow.ChoiceUI.IndicateByIdx(selfIdx);
    }
}
