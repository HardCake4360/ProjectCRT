#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InvestigationInventoryUIPrefabCreator
{
    private const string PrefabPath = "Assets/resources/prefab/InvestigationInventoryUI.prefab";

    [InitializeOnLoadMethod]
    private static void CreatePrefabIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            CreatePrefab();
        };
    }

    [MenuItem("Tools/Investigation/Create Inventory UI Prefab")]
    public static void CreatePrefab()
    {
        GameObject root = new GameObject("InvestigationInventoryUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(InvestigationInventoryUI));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.enabled = false;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject panel = CreateImage("Panel", root.transform, new Color(0.78f, 0.79f, 0.77f, 1f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.16f);
        panelRect.anchorMax = new Vector2(0.75f, 0.84f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject header = CreateImage("Header", panel.transform, new Color(0.34f, 0.4f, 0.86f, 1f));
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 0.92f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.offsetMin = Vector2.zero;
        headerRect.offsetMax = Vector2.zero;

        TMP_Text title = CreateText("TitleText", header.transform, "Investigation Inventory", 22f, TextAlignmentOptions.MidlineLeft, Color.white);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(18f, 0f);
        titleRect.offsetMax = new Vector2(-70f, 0f);

        Button closeButton = CreateButton("CloseButton", header.transform, "X", new Color(0.52f, 0.22f, 0.35f, 1f), Color.white);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-14f, 0f);
        closeRect.sizeDelta = new Vector2(42f, 30f);

        TMP_Text tab = CreateText("TabText", panel.transform, "Q / E  <  topic  >", 18f, TextAlignmentOptions.Center, Color.white);
        RectTransform tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0f, 0.84f);
        tabRect.anchorMax = new Vector2(1f, 0.92f);
        tabRect.offsetMin = new Vector2(18f, 0f);
        tabRect.offsetMax = new Vector2(-18f, 0f);

        GameObject body = CreateImage("Body", panel.transform, new Color(0.08f, 0.11f, 0.16f, 1f));
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.03f, 0.05f);
        bodyRect.anchorMax = new Vector2(0.97f, 0.84f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        GameObject listPanel = CreateImage("ListPanel", body.transform, new Color(0.11f, 0.16f, 0.23f, 1f));
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.02f, 0.05f);
        listRect.anchorMax = new Vector2(0.42f, 0.95f);
        listRect.offsetMin = Vector2.zero;
        listRect.offsetMax = Vector2.zero;

        GameObject contentRoot = new GameObject("ContentRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentRoot.transform.SetParent(listPanel.transform, false);
        RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(14f, 14f);
        contentRect.offsetMax = new Vector2(-14f, -14f);

        VerticalLayoutGroup layout = contentRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Button template = CreateButton("ItemButtonTemplate", contentRoot.transform, "Item", new Color(0.16f, 0.22f, 0.3f, 1f), Color.white);
        LayoutElement templateLayout = template.gameObject.AddComponent<LayoutElement>();
        templateLayout.preferredHeight = 40f;
        template.gameObject.SetActive(false);

        GameObject detailPanel = CreateImage("DetailPanel", body.transform, new Color(0.07f, 0.09f, 0.13f, 1f));
        RectTransform detailPanelRect = detailPanel.GetComponent<RectTransform>();
        detailPanelRect.anchorMin = new Vector2(0.45f, 0.05f);
        detailPanelRect.anchorMax = new Vector2(0.98f, 0.95f);
        detailPanelRect.offsetMin = Vector2.zero;
        detailPanelRect.offsetMax = Vector2.zero;

        TMP_Text detail = CreateText("DetailText", detailPanel.transform, "Select an unlocked item.", 18f, TextAlignmentOptions.TopLeft, new Color(0.9f, 0.93f, 0.95f, 1f));
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = Vector2.zero;
        detailRect.anchorMax = Vector2.one;
        detailRect.offsetMin = new Vector2(18f, 18f);
        detailRect.offsetMax = new Vector2(-18f, -18f);

        ConfigureWindowChrome(panelRect, headerRect);

        SerializedObject serializedObject = new SerializedObject(root.GetComponent<InvestigationInventoryUI>());
        serializedObject.FindProperty("rootCanvas").objectReferenceValue = canvas;
        serializedObject.FindProperty("titleText").objectReferenceValue = title;
        serializedObject.FindProperty("tabText").objectReferenceValue = tab;
        serializedObject.FindProperty("detailText").objectReferenceValue = detail;
        serializedObject.FindProperty("contentRoot").objectReferenceValue = contentRoot.GetComponent<RectTransform>();
        serializedObject.FindProperty("itemButtonTemplate").objectReferenceValue = template;
        serializedObject.FindProperty("closeButton").objectReferenceValue = closeButton;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created investigation inventory UI prefab: {PrefabPath}");
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return obj;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text label = obj.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        return label;
    }

    private static Button CreateButton(string name, Transform parent, string labelText, Color backgroundColor, Color textColor)
    {
        GameObject obj = CreateImage(name, parent, backgroundColor);
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();

        TMP_Text label = CreateText("Label", obj.transform, labelText, 18f, TextAlignmentOptions.Center, textColor);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return button;
    }

    private static void ConfigureWindowChrome(RectTransform panelRect, RectTransform headerRect)
    {
        RectTransform dragHitAreaRect = CreateHeaderDragHitArea(headerRect);
        DraggablePanel draggablePanel = dragHitAreaRect.gameObject.AddComponent<DraggablePanel>();
        draggablePanel.Configure(panelRect);

        ResizablePanel resizablePanel = panelRect.gameObject.AddComponent<ResizablePanel>();
        resizablePanel.targetPanel = panelRect;
        resizablePanel.MinSize = new Vector2(480f, 320f);

        GameObject resizeRoot = new GameObject("ResizeHandles", typeof(RectTransform));
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

    private static RectTransform CreateHeaderDragHitArea(RectTransform headerRect)
    {
        GameObject dragHitArea = new GameObject("HeaderDragHitArea", typeof(RectTransform), typeof(Image));
        dragHitArea.transform.SetParent(headerRect, false);

        RectTransform dragHitAreaRect = dragHitArea.GetComponent<RectTransform>();
        dragHitAreaRect.anchorMin = Vector2.zero;
        dragHitAreaRect.anchorMax = Vector2.one;
        dragHitAreaRect.offsetMin = Vector2.zero;
        dragHitAreaRect.offsetMax = new Vector2(-58f, 0f);
        dragHitAreaRect.SetAsLastSibling();

        Image image = dragHitArea.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        Transform closeButton = headerRect.Find("CloseButton");
        if (closeButton != null)
        {
            closeButton.SetAsLastSibling();
        }

        return dragHitAreaRect;
    }

    private static void CreateResizeHandle(
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

    private static CursorState GetCursorState(ResizableHandleState handleState)
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
}
#endif
