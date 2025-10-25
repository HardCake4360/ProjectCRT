using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "ChoicesObj", menuName = "Scriptable Objects/ChoicesObj")]
public class ChoicesObj : ScriptableObject
{
    public class ChoiceData
    {
        public string name;
        public UnityEvent OnSelect;
    }
    public ChoiceData[] lines;
}
