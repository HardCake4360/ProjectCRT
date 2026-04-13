using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InvestigationInventoryUI : MonoBehaviour
{
    public const string ResourcePath = "prefab/InvestigationInventoryUI";

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

    public bool IsVisible => rootCanvas != null && rootCanvas.enabled;

    public void Initialize(InvestigationInventoryManager inventoryManager, System.Action closeRequested)
    {
        inventory = inventoryManager;
        ResolveReferences();

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

        if (titleText != null)
        {
            titleText.text = "Investigation Inventory";
        }

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
