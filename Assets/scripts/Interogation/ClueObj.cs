using UnityEngine;

[CreateAssetMenu(fileName = "ClueObj", menuName = "Scriptable Objects/ClueObj")]
public class ClueObj : ScriptableObject
{
    public string Keyword;
    public DialogueObject DiaObj;
    public bool isCleared = false;

    public void detonateClueEvent()
    {
        if (isCleared == true)
        {
            Debug.Log("--------------------" + Keyword + " Event is already happened!--------------------");
            return;
        }
        DialogueManager.Instance.DialogueEventTrigger(DiaObj);
        isCleared = true;
    }

    public bool FindKeywordFrom(string context)
    {
        if (string.IsNullOrEmpty(context) || string.IsNullOrEmpty(Keyword))
            return false;

        if (context.Contains(Keyword))
        {
            detonateClueEvent();
            return true;
        }
        return false;
    }
}
