using UnityEngine;

public class InvestigationInventoryController : MonoBehaviour
{
    public static InvestigationInventoryController Instance { get; private set; }

    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private KeyCode previousTabKey = KeyCode.Q;
    [SerializeField] private KeyCode nextTabKey = KeyCode.E;

    private InvestigationInventoryManager inventory;
    private InvestigationInventoryUI inventoryUI;
    private bool isOpen;
    private MainState previousMainState = MainState.Main;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        inventory = InvestigationInventoryManager.GetOrCreateInstance();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeController()
    {
        if (Instance != null)
        {
            return;
        }

        var controllerObject = new GameObject("InvestigationInventoryController");
        controllerObject.AddComponent<InvestigationInventoryController>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleInventory();
            return;
        }

        if (!isOpen || inventoryUI == null)
        {
            return;
        }

        if (Input.GetKeyDown(previousTabKey))
        {
            inventoryUI.ShowPreviousCategory();
        }
        else if (Input.GetKeyDown(nextTabKey))
        {
            inventoryUI.ShowNextCategory();
        }
    }

    public void ToggleInventory()
    {
        if (isOpen)
        {
            CloseInventory();
            return;
        }

        if (MainLoop.Instance != null && MainLoop.Instance.MainLoopState != MainState.Main)
        {
            return;
        }

        OpenInventory();
    }

    public void OpenInventory()
    {
        EnsureUI();
        if (inventoryUI == null)
        {
            return;
        }

        if (MainLoop.Instance != null)
        {
            previousMainState = MainLoop.Instance.MainLoopState;
            MainLoop.Instance.SetMainLoopState_Interacting();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isOpen = true;
        inventoryUI.SetVisible(true);
    }

    public void CloseInventory()
    {
        if (inventoryUI != null)
        {
            inventoryUI.SetVisible(false);
        }

        isOpen = false;

        if (MainLoop.Instance != null)
        {
            if (previousMainState == MainState.Main)
            {
                MainLoop.Instance.SetMainLoopState_Main();
            }
            else
            {
                MainLoop.Instance.SetMainLoopState(previousMainState);
            }
        }
    }

    private void EnsureUI()
    {
        if (inventoryUI != null)
        {
            return;
        }

        inventory ??= InvestigationInventoryManager.GetOrCreateInstance();
        GameObject prefab = Resources.Load<GameObject>(InvestigationInventoryUI.ResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"Failed to load inventory UI prefab from Resources/{InvestigationInventoryUI.ResourcePath}. Create it with Tools/Investigation/Create Inventory UI Prefab.", this);
            return;
        }

        GameObject instance = Instantiate(prefab);
        inventoryUI = instance.GetComponent<InvestigationInventoryUI>();
        if (inventoryUI == null)
        {
            Debug.LogError("InvestigationInventoryUI prefab does not contain InvestigationInventoryUI component.", instance);
            Destroy(instance);
            return;
        }

        inventoryUI.Initialize(inventory, CloseInventory);
    }
}
