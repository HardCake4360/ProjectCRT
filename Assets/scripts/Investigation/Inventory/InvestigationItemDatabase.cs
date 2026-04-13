using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InvestigationItemDatabase", menuName = "CRT/Investigation/Item Database")]
public class InvestigationItemDatabase : ScriptableObject
{
    [SerializeField] private List<InvestigationItemDefinition> items = new();

    private readonly Dictionary<string, InvestigationItemDefinition> lookup = new();
    private bool lookupBuilt;

    public bool TryGetDefinition(string id, out InvestigationItemDefinition definition)
    {
        BuildLookupIfNeeded();
        return lookup.TryGetValue(id, out definition);
    }

    public List<InvestigationItemDefinition> GetDefinitions(InvestigationItemCategory category, IReadOnlyList<string> ids)
    {
        BuildLookupIfNeeded();

        var definitions = new List<InvestigationItemDefinition>();
        if (ids == null)
        {
            return definitions;
        }

        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (lookup.TryGetValue(id, out InvestigationItemDefinition definition) && definition.Category == category)
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    private void OnValidate()
    {
        lookupBuilt = false;
    }

    private void BuildLookupIfNeeded()
    {
        if (lookupBuilt)
        {
            return;
        }

        lookup.Clear();
        foreach (InvestigationItemDefinition item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            if (lookup.ContainsKey(item.Id))
            {
                Debug.LogWarning($"Duplicate investigation item id found: {item.Id}", this);
                continue;
            }

            lookup.Add(item.Id, item);
        }

        lookupBuilt = true;
    }
}
