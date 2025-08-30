using UnityEngine;
using UnityEngine.EventSystems;

public struct WhoolWindow
{
    public GameObject window;
    public GameObject tab;

    public WhoolWindow(GameObject Win, GameObject Tab)
    {
        window = Win;
        tab = Tab;
    }

}

public class IExecute : MeshRayReciver
{
    [SerializeField] private GameObject windowPrefab;
    [SerializeField] private GameObject tabPrefab;

    public bool isActivated;
    public WhoolWindow whool;

    protected void Exe()
    {
        if (isActivated) return;
        isActivated = true;
        whool = WindowManager.Instance.InstantiateWhoolWindow(new WhoolWindow(windowPrefab, tabPrefab));
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        Exe();
    }

    private void Update()
    {
        if (!whool.window || !whool.tab)
        {
            isActivated = false;
        }
    }
}
