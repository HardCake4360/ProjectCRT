using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class NpcConversationSaveManager
{
    private const string SaveFileName = "npc_investigation_conversations.json";

    private static bool isLoaded;
    private static NpcConversationSaveData cachedData;
    private static readonly Dictionary<string, NpcConversationSaveEntry> entryLookup = new();

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static bool TryLoadState(string stateKey, int exchangeLimit, int statementLimit, out NpcConversationState state)
    {
        EnsureLoaded();

        if (!string.IsNullOrWhiteSpace(stateKey) && entryLookup.TryGetValue(stateKey, out NpcConversationSaveEntry entry))
        {
            state = NpcConversationState.FromSaveEntry(entry, exchangeLimit, statementLimit);
            Debug.Log($"[NpcConversationSaveManager] Loaded conversation state: {stateKey}");
            return true;
        }

        state = null;
        return false;
    }

    public static void SaveState(string stateKey, NpcConversationState state, int exchangeLimit, int statementLimit)
    {
        if (string.IsNullOrWhiteSpace(stateKey) || state == null)
        {
            return;
        }

        EnsureLoaded();

        NpcConversationSaveEntry entry = state.ToSaveEntry(stateKey, exchangeLimit, statementLimit);
        entryLookup[stateKey] = entry;
        RebuildEntriesFromLookup();
        WriteToDisk();

        Debug.Log($"[NpcConversationSaveManager] Saved conversation state: {stateKey}");
    }

    public static List<NpcConversationSaveEntry> GetAllEntries()
    {
        EnsureLoaded();
        return new List<NpcConversationSaveEntry>(entryLookup.Values);
    }

    private static void EnsureLoaded()
    {
        if (isLoaded)
        {
            return;
        }

        isLoaded = true;
        cachedData = new NpcConversationSaveData();
        entryLookup.Clear();

        if (!File.Exists(SavePath))
        {
            Debug.Log($"[NpcConversationSaveManager] No conversation save file found. Path={SavePath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            NpcConversationSaveData loaded = JsonUtility.FromJson<NpcConversationSaveData>(json);
            if (loaded == null)
            {
                Debug.LogWarning($"[NpcConversationSaveManager] Failed to parse conversation save file. Path={SavePath}");
                return;
            }

            cachedData = loaded;
            if (cachedData.entries == null)
            {
                cachedData.entries = new List<NpcConversationSaveEntry>();
            }

            foreach (NpcConversationSaveEntry entry in cachedData.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.stateKey))
                {
                    continue;
                }

                entryLookup[entry.stateKey] = entry;
            }

            Debug.Log($"[NpcConversationSaveManager] Loaded {entryLookup.Count} conversation states from disk.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NpcConversationSaveManager] Failed to load conversation save file: {exception.Message}");
            cachedData = new NpcConversationSaveData();
            entryLookup.Clear();
        }
    }

    private static void RebuildEntriesFromLookup()
    {
        cachedData ??= new NpcConversationSaveData();
        cachedData.entries = new List<NpcConversationSaveEntry>(entryLookup.Values);
    }

    private static void WriteToDisk()
    {
        try
        {
            string json = JsonUtility.ToJson(cachedData, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NpcConversationSaveManager] Failed to write conversation save file: {exception.Message}");
        }
    }
}
