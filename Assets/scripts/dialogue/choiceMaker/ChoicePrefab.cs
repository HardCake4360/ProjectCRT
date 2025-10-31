using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class ChoicePrefab : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMesh;
    [SerializeField] Image indicator;
    public UnityEvent OnSelect = new UnityEvent();
    //[SerializeField, ContextMenuItem("SetSelected",nameof(ContextSetSelect))]
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
    public void InitMembers(string txt, DialogueObject dia)
    {
        textMesh.text = txt;
        OnSelect.AddListener(() =>
        {
            DialogueManager.Instance.DialogueEventTrigger(dia);
        });
    }

    public void TriggerChoice()
    {
        OnSelect?.Invoke();
    }
}
