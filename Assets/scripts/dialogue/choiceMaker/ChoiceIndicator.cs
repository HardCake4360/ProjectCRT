using UnityEngine;
using System.Collections.Generic;


public class ChoiceIndicator : MonoBehaviour
{
    public Vector2 Damp;
    //[SerializeField] private RectTransform indicatingRect;
    public RectTransform rt;
    public RectTransform parentRt;
    private List<RectTransform> Targets = new List<RectTransform>();

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void SetTargets(List<GameObject> objs)
    {
        Targets.Clear();
        foreach (var obj in objs)
        {
            if (!obj)
            {
                Debug.Log("obj list is NULL");
                return;
            }
            var target = obj.GetComponent<RectTransform>();
            if (!target) Debug.Log("Can't get RectTransform from " + obj.name);
            Targets.Add(target);
        }
    }

    public void IndicateByIdx(int idx)
    {
        Debug.Log("indicating: " + Targets[idx].gameObject.name);
        Vector3 worldPos = Targets[idx].position;
        Debug.Log("calculated world pos: " + worldPos);
        
        Vector3 localPos = parentRt.InverseTransformPoint(worldPos);
        Debug.Log("calculated local pos: " + localPos);

        rt.anchoredPosition = (Vector2)localPos + Damp;
    }

}
