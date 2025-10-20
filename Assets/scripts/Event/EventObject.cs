using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "EventObject", menuName = "Scriptable Objects/EventObject")]
public class EventObject : ScriptableObject
{
    public string EventName;
    public bool IsMainEvent;
    [SerializeField] private bool[] conditions;
    [SerializeField] private UnityEvent OnComplete;
    public EventObject[] NextEvents;

    public bool CheckAllConditions()
    {
        bool conditionsMet = true;
        for(int i=0; i < conditions.Length; i++)
        {
            if (!conditions[i])
            {
                conditionsMet = false;
                break;
            }
        }
        return conditionsMet;
    }
    
    void ExecuteEvent() { OnComplete?.Invoke(); }
}
