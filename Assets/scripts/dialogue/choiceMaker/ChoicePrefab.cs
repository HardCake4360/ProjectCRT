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
    public void ContextSetSelect() { indicator.enabled = isSelected; }

    private void Start()
    {
        indicator.enabled = isSelected;
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
        indicator.enabled = val; 
    }
    public void InitMembers(string txt,int idx, DialogueObject dia)
    {
        textMesh.text = txt;
        selfIdx = idx;
        button.OnClick.AddListener(() =>
        {
            DialogueManager.Instance.SetSelecting(false);
            DialogueManager.Instance.DUIManager.SetChoicesUIActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            DialogueManager.Instance.DialogueEventTrigger(dia);
        });
    }

    public void OnHover()
    {
        DialogueManager.Instance.CUI.IndicateByIdx(selfIdx);
    }
}
