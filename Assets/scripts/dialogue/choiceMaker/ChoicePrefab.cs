using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class ChoicePrefab : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMesh;
    public UnityEvent OnSelect = new UnityEvent();

    void Awake()
    {
        textMesh = GetComponentInChildren<TextMeshProUGUI>();
        if (textMesh == null)
            Debug.LogError("TextMeshProUGUI not found in children!");
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
