using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
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
        if(indicator) indicator.enabled = isSelected;
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
                if (LocalDiaManager.Instance)
                {
                    LocalDiaManager.Instance.SetSelecting(false);
                    LocalDiaManager.Instance.DUIManager.SetChoicesUIActive(false);
                    LocalDiaManager.Instance.DialogueEventTrigger((InterogationDiaObject)cho.OnSelectDialogue);
                }
                else
                {
                    DialogueManager.Instance.SetSelecting(false);
                    DialogueManager.Instance.DUIManager.SetChoicesUIActive(false);
                    DialogueManager.Instance.DialogueEventTrigger(cho.OnSelectDialogue);
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
            //cho.OnSelect?.Invoke();
        });
    }

    public void OnHover()
    {
        if (LocalDiaManager.Instance)
        {
            LocalDiaManager.Instance.CUI.IndicateByIdx(selfIdx);
        }
        else
        {
            DialogueManager.Instance.CUI.IndicateByIdx(selfIdx);
        }
    }
}
