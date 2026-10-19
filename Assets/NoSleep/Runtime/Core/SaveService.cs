using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoSleep
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = SaveService.CurrentVersion;
        public string mapId;
        public Vector3 position;
        public float yaw;
        public GameState state;
        public long savedUtcTicks;
        public string mapDisplayName;
        public string objectiveTitle;
        public int contentVersion = SaveService.CurrentContentVersion;
        [NonSerialized] public int sourceVersion = SaveService.CurrentVersion;
    }

    public static class SaveService
    {
        public const int CurrentVersion = 4;
        public const int CurrentContentVersion = 1;
        public static string Serialize(SaveData data)
        {
            Validate(data, false);
            data.state.ValidateEventProgress();
            NpcState.ValidateRecords(data.state.npcStates);
            return JsonUtility.ToJson(data, true);
        }
        public static SaveData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Save file is empty.");
            RequireRootFields(json);
            // Overwrite explicit invalid sentinels so missing fields cannot silently become
            // default-initialized state or the origin during Unity JSON deserialization.
            var data = new SaveData { version = 0, state = null, mapId = null,
                position = new Vector3(float.NaN, float.NaN, float.NaN), yaw = float.NaN };
            try { JsonUtility.FromJsonOverwrite(json, data); }
            catch (Exception ex) { throw new InvalidDataException("Save JSON is invalid.", ex); }
            Validate(data, true);
            data.sourceVersion = data.version;
            if (data.version == 1)
            {
                // V1 defined none of the horror-progress fields. Initialize their V2
                // defaults explicitly: Unity may zero missing nested numeric fields
                // instead of applying their C# field initializers during JSON loading.
                data.state.player ??= new PlayerStats();
                data.state.player.strength = 12;
                data.state.survival = new SurvivalState();
                data.state.journey = new JourneyState();
                data.state.party = new PartyState();
                data.state.unlockedActions = new List<string> { "strike", "guard", "flee" };
            }
            if (data.version < 3)
            {
                data.state.committedTransactions = new List<string>();
                data.state.eventCheckpoints = new List<EventCheckpointState>();
                data.contentVersion = CurrentContentVersion;
            }
            if (data.version < 4) data.state.npcStates = new List<NpcState>();
            data.state.Normalize();
            data.version = CurrentVersion;
            return data;
        }
        public static void Write(string path, SaveData data)
        {
            string json = Serialize(data);
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, System.Text.Encoding.UTF8);
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, fullPath + ".bak");
                else File.Move(temporary, fullPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static SaveData Read(string path) => Deserialize(File.ReadAllText(path));
        static void RequireRootFields(string json)
        {
            string trimmed = json.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                throw new InvalidDataException("Save JSON must contain one root object.");
            // Unity may create a nested serializable object even when that field is absent.
            // Inspect root keys, ignoring nested values and escaped strings, before parsing.
            var required = new HashSet<string> { "version", "mapId", "position", "yaw", "state" };
            var found = new HashSet<string>();
            var delimiters = new Stack<char>();
            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{' || c == '[') { delimiters.Push(c); depth++; continue; }
                if (c == '}' || c == ']')
                {
                    if (delimiters.Count == 0 || delimiters.Pop() != (c == '}' ? '{' : '['))
                        throw new InvalidDataException("Save JSON delimiters are invalid.");
                    depth--;
                    if (depth == 0)
                        for (int tail = i + 1; tail < json.Length; tail++)
                            if (!char.IsWhiteSpace(json[tail])) throw new InvalidDataException("Save JSON has trailing content.");
                    continue;
                }
                if (c != '"') continue;
                int start = ++i;
                bool escaped = false;
                while (i < json.Length && json[i] != '"')
                {
                    if (json[i] == '\\') { escaped = true; i += 2; }
                    else i++;
                }
                if (i >= json.Length) throw new InvalidDataException("Unterminated save JSON string.");
                if (depth != 1 || escaped) continue;
                string key = json.Substring(start, i - start);
                int next = i + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next >= json.Length || json[next] != ':' || !required.Contains(key)) continue;
                if (!found.Add(key)) throw new InvalidDataException("Duplicate save field: " + key);
                next++;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if ((key == "state" || key == "position") && (next >= json.Length || json[next] != '{'))
                    throw new InvalidDataException("Save field must be an object: " + key);
            }
            if (depth != 0) throw new InvalidDataException("Save JSON object is incomplete.");
            if (found.Count != required.Count) throw new InvalidDataException("Save is missing required fields.");
        }
        static void Validate(SaveData data, bool acceptLegacy)
        {
            if (data == null || data.state == null || string.IsNullOrWhiteSpace(data.mapId))
                throw new InvalidDataException("Save requires a map ID and game state.");
            if (data.version != CurrentVersion && !(acceptLegacy && (data.version == 1 || data.version == 2 || data.version == 3)))
                throw new InvalidDataException("Unsupported save version: " + data.version);
            if (!Finite(data.position.x) || !Finite(data.position.y) || !Finite(data.position.z) || !Finite(data.yaw))
                throw new InvalidDataException("Save position is invalid.");
            if (data.savedUtcTicks < 0 || data.savedUtcTicks > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("Save timestamp is invalid.");
            if (data.contentVersion < 1)
                throw new InvalidDataException("Save content version is invalid.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
