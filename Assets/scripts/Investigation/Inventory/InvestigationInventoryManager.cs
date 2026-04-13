using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class InvestigationInventoryManager : MonoBehaviour
{
    public static InvestigationInventoryManager Instance { get; private set; }

    [Header("Case")]
    [SerializeField] private string caseId = "bar_case_001";
    [SerializeField] private InvestigationItemDatabase database;
    [SerializeField] private string databaseResourcePath = "Investigation/InvestigationItemDatabase";
    [SerializeField] private bool loadOnAwake = true;
    [SerializeField] private bool saveOnUnlock = true;

    public event Action InventoryChanged;

    private readonly HashSet<string> topicIds = new();
    private readonly HashSet<string> evidenceIds = new();
    private readonly HashSet<string> informationIds = new();
    private bool attemptedDatabaseLoad;

    public string CaseId => caseId;
    public InvestigationItemDatabase Database => database;

    private string SavePath => Path.Combine(Application.persistentDataPath, $"investigation_inventory_{caseId}.json");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureDatabaseLoaded();

        if (loadOnAwake)
        {
            Load();
        }
    }

    public static InvestigationInventoryManager GetOrCreateInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        var inventoryObject = new GameObject("InvestigationInventoryManager");
        return inventoryObject.AddComponent<InvestigationInventoryManager>();
    }

    public bool UnlockTopic(string id)
    {
        return Unlock(id, InvestigationItemCategory.Topic);
    }

    public bool UnlockEvidence(string id)
    {
        return Unlock(id, InvestigationItemCategory.Evidence);
    }

    public bool UnlockInformation(string id)
    {
        return Unlock(id, InvestigationItemCategory.Information);
    }

    public List<string> GetUnlockedIds(InvestigationItemCategory category)
    {
        return category switch
        {
            InvestigationItemCategory.Topic => new List<string>(topicIds),
            InvestigationItemCategory.Evidence => new List<string>(evidenceIds),
            InvestigationItemCategory.Information => new List<string>(informationIds),
            _ => new List<string>()
        };
    }

    public bool TryGetDefinition(string id, out InvestigationItemDefinition definition)
    {
        EnsureDatabaseLoaded();

        if (database != null)
        {
            return database.TryGetDefinition(id, out definition);
        }

        definition = null;
        return false;
    }

    public InvestigationInventorySaveData ToSaveData()
    {
        return new InvestigationInventorySaveData
        {
            version = 1,
            caseId = caseId,
            topicIds = new List<string>(topicIds),
            evidenceIds = new List<string>(evidenceIds),
            informationIds = new List<string>(informationIds)
        };
    }

    public void Load()
    {
        topicIds.Clear();
        evidenceIds.Clear();
        informationIds.Clear();

        if (!File.Exists(SavePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            InvestigationInventorySaveData data = JsonUtility.FromJson<InvestigationInventorySaveData>(json);
            if (data == null)
            {
                return;
            }

            caseId = string.IsNullOrWhiteSpace(data.caseId) ? caseId : data.caseId;
            AddRange(topicIds, data.topicIds);
            AddRange(evidenceIds, data.evidenceIds);
            AddRange(informationIds, data.informationIds);
            InventoryChanged?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to load investigation inventory save: {exception.Message}", this);
        }
    }

    public void Save()
    {
        try
        {
            InvestigationInventorySaveData data = ToSaveData();
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to save investigation inventory: {exception.Message}", this);
        }
    }

    private bool Unlock(string id, InvestigationItemCategory category)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"Cannot unlock empty investigation {category} id.", this);
            return false;
        }

        HashSet<string> target = category switch
        {
            InvestigationItemCategory.Topic => topicIds,
            InvestigationItemCategory.Evidence => evidenceIds,
            InvestigationItemCategory.Information => informationIds,
            _ => null
        };

        if (target == null || !target.Add(id))
        {
            return false;
        }

        if (saveOnUnlock)
        {
            Save();
        }

        Debug.Log($"[InvestigationInventory] Unlocked {category}: {id}", this);
        InventoryChanged?.Invoke();
        return true;
    }

    private void EnsureDatabaseLoaded()
    {
        if (database != null || attemptedDatabaseLoad || string.IsNullOrWhiteSpace(databaseResourcePath))
        {
            return;
        }

        attemptedDatabaseLoad = true;
        database = Resources.Load<InvestigationItemDatabase>(databaseResourcePath);
        if (database == null)
        {
            Debug.LogWarning(
                $"Investigation item database was not found at Resources/{databaseResourcePath}. " +
                "Unlocked ids will still be saved, but inventory details require a database asset.",
                this);
        }
    }

    private void AddRange(HashSet<string> destination, IEnumerable<string> source)
    {
        if (source == null)
        {
            return;
        }

        foreach (string id in source)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                destination.Add(id);
            }
        }
    }
}
