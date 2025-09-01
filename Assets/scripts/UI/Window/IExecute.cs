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
    [Header("signature setting")]
    public string name;

    [SerializeField] private GameObject windowPrefab;
    [SerializeField] private GameObject tabPrefab;
    [SerializeField] private GameObject content;

    public bool isActivated;
    public WhoolWindow whool;

    protected void Exe()
    {
        if (isActivated) return;
        isActivated = true;
        whool = WindowManager.Instance.InstantiateWhoolWindow(new WhoolWindow(windowPrefab, tabPrefab), name,content);
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
