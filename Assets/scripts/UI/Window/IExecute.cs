using UnityEngine;
using UnityEngine.EventSystems;

public class IExecute : MeshRayReciver
{
    [SerializeField] private GameObject window;
    [SerializeField] private GameObject tab;

    protected void Exe()
    {
        WindowManager.Instance.InstantiateWhoolWindow(window,tab);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        Exe();
    }

}
