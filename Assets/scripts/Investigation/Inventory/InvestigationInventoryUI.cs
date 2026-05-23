using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InvestigationInventoryUI : MonoBehaviour
{
    public const string ResourcePath = "prefab/InvestigationInventoryUI";
    private const string PanelName = "Panel";
    private const string HeaderName = "Header";
    private const string HeaderDragHitAreaName = "HeaderDragHitArea";
    private const string ResizeHandlesName = "ResizeHandles";
    private const string IconSuffix = "Icon";
    private const string ConversationLogWindowName = "ConversationLog";

    [Header("Prefab References")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text tabText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button itemButtonTemplate;
    [SerializeField] private Button closeButton;

    private readonly List<GameObject> spawnedItems = new();
    private InvestigationInventoryManager inventory;
    private InvestigationItemCategory currentCategory = InvestigationItemCategory.Topic;
    private bool windowChromeInitialized;

    public bool IsVisible => rootCanvas != null && rootCanvas.enabled;

    private void Awake()
    {
        ResolveReferences();
        EnsureWindowChrome();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureWindowChrome();
    }

    public void Initialize(InvestigationInventoryManager inventoryManager, System.Action closeRequested)
    {
        inventory = inventoryManager;
        ResolveReferences();
        EnsureWindowChrome();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => closeRequested?.Invoke());
        }

        if (itemButtonTemplate != null)
        {
            itemButtonTemplate.gameObject.SetActive(false);
        }

        if (inventory != null)
        {
            inventory.InventoryChanged -= Refresh;
            inventory.InventoryChanged += Refresh;
        }

        SetVisible(false);
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.InventoryChanged -= Refresh;
        }
    }

    public void SetVisible(bool visible)
    {
        ResolveReferences();
        EnsureWindowChrome();

        if (rootCanvas != null)
        {
            rootCanvas.enabled = visible;
        }

        if (visible)
        {
            Refresh();
        }
    }

    public void ShowCategory(InvestigationItemCategory category)
    {
        currentCategory = category;
        Refresh();
    }

    public void ShowNextCategory()
    {
        currentCategory = currentCategory switch
        {
            InvestigationItemCategory.Topic => InvestigationItemCategory.Evidence,
            InvestigationItemCategory.Evidence => InvestigationItemCategory.Information,
            _ => InvestigationItemCategory.Topic
        };

        Refresh();
    }

    public void ShowPreviousCategory()
    {
        currentCategory = currentCategory switch
        {
            InvestigationItemCategory.Topic => InvestigationItemCategory.Information,
            InvestigationItemCategory.Evidence => InvestigationItemCategory.Topic,
            _ => InvestigationItemCategory.Evidence
        };

        Refresh();
    }

    public void Refresh()
    {
        ResolveReferences();
        ClearItems();

        if (tabText != null)
        {
            tabText.text = $"Q / E  <  {GetCategoryLabel(currentCategory)}  >";
        }

        if (detailText != null)
        {
            detailText.text = "Select an unlocked item.";
        }

        if (inventory == null || contentRoot == null || itemButtonTemplate == null)
        {
            Debug.LogWarning("InvestigationInventoryUI prefab references are incomplete. Check contentRoot and itemButtonTemplate on the prefab.", this);
            return;
        }

        List<string> ids = inventory.GetUnlockedIds(currentCategory);
        if (ids.Count == 0)
        {
            Button emptyButton = CreateItemButton("(empty)");
            emptyButton.interactable = false;
            return;
        }

        foreach (string id in ids)
        {
            string label = id;
            if (inventory.TryGetDefinition(id, out InvestigationItemDefinition definition))
            {
                label = definition.DisplayName;
            }

            Button button = CreateItemButton(label);
            string capturedId = id;
            button.onClick.AddListener(() => ShowItemDetail(capturedId));
        }
    }

    private void ShowItemDetail(string id)
    {
        if (detailText == null)
        {
            return;
        }

        if (inventory != null && inventory.TryGetDefinition(id, out InvestigationItemDefinition definition))
        {
            string summary = string.IsNullOrWhiteSpace(definition.Summary) ? string.Empty : $"{definition.Summary}\n\n";
            detailText.text = $"{definition.DisplayName}\n{summary}{definition.Detail}";
            return;
        }

        detailText.text = id;
    }

    private Button CreateItemButton(string label)
    {
        Button button = Instantiate(itemButtonTemplate, contentRoot);
        button.gameObject.SetActive(true);
        button.name = $"InventoryItem_{label}";
        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText != null)
        {
            buttonText.text = label;
        }

        spawnedItems.Add(button.gameObject);
        return button;
    }

    private void ClearItems()
    {
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            if (spawnedItems[i] != null)
            {
                Destroy(spawnedItems[i]);
            }
        }

        spawnedItems.Clear();
    }

    private void ResolveReferences()
    {
        rootCanvas ??= GetComponent<Canvas>();
        titleText ??= FindNamedComponent<TMP_Text>("TitleText");
        tabText ??= FindNamedComponent<TMP_Text>("TabText");
        detailText ??= FindNamedComponent<TMP_Text>("DetailText");
        contentRoot ??= FindNamedRectTransform("ContentRoot");
        itemButtonTemplate ??= FindNamedComponent<Button>("ItemButtonTemplate");
        closeButton ??= FindNamedComponent<Button>("CloseButton");
    }

    private void EnsureWindowChrome()
    {
        if (windowChromeInitialized)
        {
            return;
        }

        bool configuredAny = false;
        RectTransform panelRect = FindNamedRectTransform(PanelName);
        if (panelRect != null)
        {
            configuredAny |= EnsureWindowChrome(panelRect);
        }

        foreach (RectTransform iconRect in FindIconRects())
        {
            RectTransform windowRect = FindIconWindow(iconRect);
            if (windowRect == null)
            {
                continue;
            }

            EnsureIconRaycastTarget(iconRect);
            IconWindowToggle iconToggle = iconRect.GetComponent<IconWindowToggle>();
            if (iconToggle == null)
            {
                iconToggle = iconRect.gameObject.AddComponent<IconWindowToggle>();
            }

            iconToggle.Configure(windowRect);
            configuredAny |= EnsureWindowChrome(windowRect);
        }

        windowChromeInitialized = configuredAny;
    }

    private bool EnsureWindowChrome(RectTransform windowRect)
    {
        RectTransform headerRect = FindChildRectTransform(windowRect, HeaderName);
        if (windowRect == null || headerRect == null)
        {
            return false;
        }

        WindowFocusTarget focusTarget = windowRect.GetComponent<WindowFocusTarget>();
        if (focusTarget == null)
        {
            focusTarget = windowRect.gameObject.AddComponent<WindowFocusTarget>();
        }

        focusTarget.Configure(windowRect);

        RectTransform dragHitAreaRect = EnsureHeaderDragHitArea(headerRect);
        DraggablePanel draggablePanel = dragHitAreaRect.GetComponent<DraggablePanel>();
        if (draggablePanel == null)
        {
            draggablePanel = dragHitAreaRect.gameObject.AddComponent<DraggablePanel>();
        }

        draggablePanel.Configure(windowRect);

        ResizablePanel resizablePanel = windowRect.GetComponent<ResizablePanel>();
        if (resizablePanel == null)
        {
            resizablePanel = windowRect.gameObject.AddComponent<ResizablePanel>();
        }

        resizablePanel.targetPanel = windowRect;
        resizablePanel.MinSize = new Vector2(480f, 320f);

        if (windowRect.Find(ResizeHandlesName) == null)
        {
            CreateResizeHandles(windowRect, resizablePanel);
        }

        if (windowRect.name.Equals(ConversationLogWindowName, System.StringComparison.OrdinalIgnoreCase))
        {
            EnsureConversationLogHistoryUI(windowRect);
        }

        return true;
    }

    private void EnsureConversationLogHistoryUI(RectTransform windowRect)
    {
        ConversationLogHistoryUI historyUI = windowRect.GetComponent<ConversationLogHistoryUI>();
        if (historyUI == null)
        {
            historyUI = windowRect.gameObject.AddComponent<ConversationLogHistoryUI>();
        }

        historyUI.Configure(windowRect);
    }

    private RectTransform EnsureHeaderDragHitArea(RectTransform headerRect)
    {
        Transform existing = headerRect.Find(HeaderDragHitAreaName);
        RectTransform dragHitAreaRect;

        if (existing is RectTransform existingRect)
        {
            dragHitAreaRect = existingRect;
        }
        else
        {
            GameObject dragHitArea = new GameObject(HeaderDragHitAreaName, typeof(RectTransform), typeof(Image));
            dragHitArea.transform.SetParent(headerRect, false);
            dragHitAreaRect = dragHitArea.GetComponent<RectTransform>();
        }

        dragHitAreaRect.anchorMin = Vector2.zero;
        dragHitAreaRect.anchorMax = Vector2.one;
        dragHitAreaRect.offsetMin = Vector2.zero;
        dragHitAreaRect.offsetMax = new Vector2(-58f, 0f);
        dragHitAreaRect.SetAsLastSibling();

        Image hitAreaImage = dragHitAreaRect.GetComponent<Image>();
        if (hitAreaImage == null)
        {
            hitAreaImage = dragHitAreaRect.gameObject.AddComponent<Image>();
        }

        hitAreaImage.color = new Color(1f, 1f, 1f, 0f);
        hitAreaImage.raycastTarget = true;

        Button close = closeButton != null ? closeButton : FindNamedComponent<Button>("CloseButton");
        if (close != null)
        {
            close.transform.SetAsLastSibling();
        }

        return dragHitAreaRect;
    }

    private void CreateResizeHandles(RectTransform panelRect, ResizablePanel resizablePanel)
    {
        GameObject resizeRoot = new GameObject(ResizeHandlesName, typeof(RectTransform));
        resizeRoot.transform.SetParent(panelRect, false);

        RectTransform resizeRootRect = resizeRoot.GetComponent<RectTransform>();
        resizeRootRect.anchorMin = Vector2.zero;
        resizeRootRect.anchorMax = Vector2.one;
        resizeRootRect.offsetMin = Vector2.zero;
        resizeRootRect.offsetMax = Vector2.zero;
        resizeRootRect.SetAsLastSibling();

        CreateResizeHandle(resizeRoot.transform, "Left", ResizableHandleState.left, resizablePanel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-5f, 0f), new Vector2(10f, 0f));
        CreateResizeHandle(resizeRoot.transform, "Right", ResizableHandleState.right, resizablePanel, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(5f, 0f), new Vector2(10f, 0f));
        CreateResizeHandle(resizeRoot.transform, "Top", ResizableHandleState.top, resizablePanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 5f), new Vector2(0f, 10f));
        CreateResizeHandle(resizeRoot.transform, "Bottom", ResizableHandleState.bottom, resizablePanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -5f), new Vector2(0f, 10f));
        CreateResizeHandle(resizeRoot.transform, "TopLeft", ResizableHandleState.top_left, resizablePanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-5f, 5f), new Vector2(16f, 16f));
        CreateResizeHandle(resizeRoot.transform, "TopRight", ResizableHandleState.top_right, resizablePanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(5f, 5f), new Vector2(16f, 16f));
        CreateResizeHandle(resizeRoot.transform, "BottomLeft", ResizableHandleState.bottom_left, resizablePanel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(-5f, -5f), new Vector2(16f, 16f));
        CreateResizeHandle(resizeRoot.transform, "BottomRight", ResizableHandleState.bottom_right, resizablePanel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(5f, -5f), new Vector2(16f, 16f));
    }

    private void CreateResizeHandle(
        Transform parent,
        string name,
        ResizableHandleState handleState,
        ResizablePanel resizablePanel,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject handleObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ResizableHandle));
        handleObject.transform.SetParent(parent, false);

        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = anchorMin;
        handleRect.anchorMax = anchorMax;
        handleRect.anchoredPosition = anchoredPosition;
        handleRect.sizeDelta = sizeDelta;

        Image image = handleObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        ResizableHandle handle = handleObject.GetComponent<ResizableHandle>();
        handle.Configure(handleState, resizablePanel);

        CursorZone cursorZone = handleObject.AddComponent<CursorZone>();
        cursorZone.OnHover = GetCursorState(handleState);
    }

    private CursorState GetCursorState(ResizableHandleState handleState)
    {
        return handleState switch
        {
            ResizableHandleState.left => CursorState.ResizeHorizontal,
            ResizableHandleState.right => CursorState.ResizeHorizontal,
            ResizableHandleState.top => CursorState.ResizeVertical,
            ResizableHandleState.bottom => CursorState.ResizeVertical,
            ResizableHandleState.top_left => CursorState.ResizeMainDiagonal,
            ResizableHandleState.bottom_right => CursorState.ResizeMainDiagonal,
            ResizableHandleState.top_right => CursorState.ResizeAntiDiagonal,
            ResizableHandleState.bottom_left => CursorState.ResizeAntiDiagonal,
            _ => CursorState.Default
        };
    }

    private RectTransform FindNamedRectTransform(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == objectName && child is RectTransform rectTransform)
            {
                return rectTransform;
            }
        }

        return null;
    }

    private List<RectTransform> FindIconRects()
    {
        var icons = new List<RectTransform>();
        RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in children)
        {
            if (child.name.EndsWith(IconSuffix, System.StringComparison.OrdinalIgnoreCase))
            {
                icons.Add(child);
            }
        }

        return icons;
    }

    private RectTransform FindIconWindow(RectTransform iconRect)
    {
        for (int i = 0; i < iconRect.childCount; i++)
        {
            if (iconRect.GetChild(i) is RectTransform childRect && FindChildRectTransform(childRect, HeaderName) != null)
            {
                return childRect;
            }
        }

        string iconName = iconRect.name;
        if (!iconName.EndsWith(IconSuffix, System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string windowName = iconName[..^IconSuffix.Length];
        return FindWindowByName(windowName);
    }

    private RectTransform FindWindowByName(string objectName)
    {
        RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in children)
        {
            if (child.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase)
                && FindChildRectTransform(child, HeaderName) != null)
            {
                return child;
            }
        }

        return null;
    }

    private RectTransform FindChildRectTransform(RectTransform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        RectTransform[] children = parent.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in children)
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private void EnsureIconRaycastTarget(RectTransform iconRect)
    {
        Graphic graphic = iconRect.GetComponent<Graphic>();
        if (graphic == null)
        {
            Image image = iconRect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            graphic = image;
        }

        graphic.raycastTarget = true;
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == objectName)
            {
                T component = child.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private string GetCategoryLabel(InvestigationItemCategory category)
    {
        return category switch
        {
            InvestigationItemCategory.Topic => "topic",
            InvestigationItemCategory.Evidence => "evidence",
            InvestigationItemCategory.Information => "information",
            _ => category.ToString()
        };
    }
}
