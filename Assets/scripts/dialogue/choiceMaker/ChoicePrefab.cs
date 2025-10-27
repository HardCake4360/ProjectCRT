using UnityEngine;
using UnityEngine.Events;
using TMPro;
public class ChoicePrefab : MonoBehaviour
{
    TextMeshPro textMesh;
    UnityEvent OnSelect;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void SetText(string txt)
    {
        textMesh.text = txt;
    }
    public void SetEvent(UnityEvent evt)
    {
        OnSelect = evt;
    }

    public void InitMembers(string txt, DialogueObject dia)
    {
        textMesh.text = txt;
        OnSelect.AddListener(() =>
        {
            DialogueManager.Instance.DialogueEventTrigger(dia);
        });
    }
}
