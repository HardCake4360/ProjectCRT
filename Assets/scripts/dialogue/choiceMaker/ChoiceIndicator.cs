using UnityEngine;
using System.Collections.Generic;


public class ChoiceIndicator : MonoBehaviour
{
    public Vector3 Damp;
    [SerializeField] private RectTransform indicatingRect;
    private RectTransform rt;
    private RectTransform parentRt;
    private List<RectTransform> Targets;

    private void Start()
    {
        rt = GetComponent<RectTransform>();
        parentRt = gameObject.GetComponentInParent<RectTransform>();
    }

    public void SetTargets(List<GameObject> objs)
    {
        foreach(var obj in objs)
        {
            Targets.Add(obj.GetComponent<RectTransform>());
        }
    }

    public void IndicateByIdx(int idx)
    {
        Vector3 worldPos = Targets[idx].TransformPoint(Targets[idx].localPosition);
        Vector3 localPos = parentRt.InverseTransformPoint(worldPos);
        rt.anchoredPosition = localPos + Damp;
    }

}
