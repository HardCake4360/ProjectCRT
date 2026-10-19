using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class EventIdentityUpgrader
    {
        [MenuItem("No Sleep/Upgrade Event Identities")]
        public static void UpgradeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/NoSleep" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (UpgradeDefinition(definition)) changed++;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:CommonEventDefinition", new[] { "Assets/NoSleep" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<CommonEventDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (UpgradeCommon(definition)) changed++;
            }
            if (changed > 0) AssetDatabase.SaveAssets();
            Debug.Log("NO_SLEEP_EVENT_IDENTITIES_UPGRADED: " + changed + " changed assets; existing IDs and legacy indices preserved.");
        }

        public static bool UpgradeDefinition(EventDefinition definition)
        {
            if (definition == null || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            Undo.RecordObject(definition, "Upgrade Event Identities");
            bool changed = definition.EnsureStableIds();
            if (changed) EditorUtility.SetDirty(definition);
            return changed;
        }

        public static bool UpgradeCommon(CommonEventDefinition definition)
        {
            if (definition == null || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            Undo.RecordObject(definition, "Upgrade Common Event Identities");
            bool changed = definition.EnsureStableIds();
            if (changed) EditorUtility.SetDirty(definition);
            return changed;
        }

        // Inspector array duplication copies serialized IDs. Keep the first identity and give the copy new ones.
        public static int RepairCopiedEntries(EventDefinition definition, bool logWarning = true)
        {
            if (definition == null || definition.pages == null || EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            Undo.RecordObject(definition, "Repair Copied Event Identities");
            int repaired = 0;
            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            var commandIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < definition.pages.Count; index++)
            {
                var page = definition.pages[index];
                if (page == null) continue;
                if (!string.IsNullOrWhiteSpace(page.pageId) && !pageIds.Add(page.pageId))
                {
                    page = JsonUtility.FromJson<EventPage>(JsonUtility.ToJson(page));
                    page.pageId = NewId(); page.legacyPageIndex = -1; definition.pages[index] = page;
                    if (page.commands != null)
                        foreach (var command in page.commands) if (command != null) command.commandId = NewId();
                    pageIds.Add(page.pageId); repaired++;
                }
                if (page.commands == null) continue;
                for (int commandIndex = 0; commandIndex < page.commands.Count; commandIndex++)
                {
                    var command = page.commands[commandIndex];
                    if (command == null || string.IsNullOrWhiteSpace(command.commandId) || commandIds.Add(command.commandId)) continue;
                    command = JsonUtility.FromJson<EventCommand>(JsonUtility.ToJson(command));
                    command.commandId = NewId(); page.commands[commandIndex] = command;
                    commandIds.Add(command.commandId); repaired++;
                }
            }
            if (repaired > 0)
            {
                EditorUtility.SetDirty(definition);
                if (logWarning) Debug.LogWarning("Copied page/command identities were renewed in " + definition.name +
                    ". Original identities and legacy mappings were retained.", definition);
            }
            return repaired;
        }

        public static int RepairCopiedCommands(CommonEventDefinition definition, bool logWarning = true)
        {
            if (definition?.commands == null || EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            Undo.RecordObject(definition, "Repair Copied Common Command Identities");
            int repaired = 0; var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < definition.commands.Count; index++)
            {
                var command = definition.commands[index];
                if (command == null || string.IsNullOrWhiteSpace(command.commandId) || ids.Add(command.commandId)) continue;
                var copy = JsonUtility.FromJson<EventCommand>(JsonUtility.ToJson(command));
                copy.commandId = NewId(); definition.commands[index] = copy; ids.Add(copy.commandId); repaired++;
            }
            if (repaired > 0)
            {
                EditorUtility.SetDirty(definition);
                if (logWarning) Debug.LogWarning("Copied command identities were renewed in " + definition.name + ".", definition);
            }
            return repaired;
        }

        [MenuItem("No Sleep/Regenerate Copied Event Identities")]
        public static void RegenerateSelected()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var selected = Selection.objects.Where(value => value is EventDefinition || value is CommonEventDefinition).ToArray();
            if (selected.Length == 0) return;
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("복제한 이벤트 ID 다시 발급",
                "선택한 에셋의 저장 완료·체크포인트 연결을 새로 만듭니다. 복제한 에셋에만 사용하세요. 기존 저장이 사용하는 원본에는 사용하지 마세요.",
                "새 ID 발급", "취소")) return;
            foreach (var value in selected) RegenerateCopiedAsset(value as ScriptableObject);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("No Sleep/Regenerate Copied Event Identities", true)]
        static bool CanRegenerate() => !EditorApplication.isPlayingOrWillChangePlaymode && Selection.objects.Any(value => value is EventDefinition || value is CommonEventDefinition);

        public static void RegenerateCopiedAsset(ScriptableObject value)
        {
            if (value == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Undo.RecordObject(value, "Regenerate Copied Event Identities");
            if (value is EventDefinition definition)
            {
                definition.definitionId = NewId();
                if (definition.pages != null)
                    foreach (var page in definition.pages)
                    {
                        if (page == null) continue;
                        page.pageId = NewId(); page.legacyPageIndex = -1;
                        RenewCommands(page.commands);
                    }
            }
            else if (value is CommonEventDefinition common) { common.commonId = NewId(); RenewCommands(common.commands); }
            else throw new ArgumentException("Select an EventDefinition or CommonEventDefinition.", nameof(value));
            EditorUtility.SetDirty(value);
        }

        public static List<string> CollectErrors()
        {
            var errors = new List<string>();
            var definitionIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var pageIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var commandIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/NoSleep" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<EventDefinition>(path);
                if (definition == null) { errors.Add(path + ": 이벤트 에셋을 읽을 수 없습니다."); continue; }
                foreach (string error in definition.ValidateIdentity()) errors.Add(path + ": " + error);
                if (definition.pages != null)
                    for (int index = 0; index < definition.pages.Count; index++)
                        foreach (string error in ValidateDefinitionPage(definition, index))
                            errors.Add(path + ": 페이지 " + (index + 1) + " / " + error);
                Unique(definition.definitionId, path, "definitionId", definitionIds, errors);
                if (definition.pages == null) continue;
                foreach (var page in definition.pages)
                {
                    if (page == null) continue;
                    Unique(page.pageId, path, "pageId", pageIds, errors);
                    CollectCommands(page.commands, path, commandIds, errors);
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:CommonEventDefinition", new[] { "Assets/NoSleep" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<CommonEventDefinition>(path);
                if (definition == null) { errors.Add(path + ": 공통 이벤트 에셋을 읽을 수 없습니다."); continue; }
                Unique(definition.commonId, path, "commonId", definitionIds, errors);
                CollectCommands(definition.commands, path, commandIds, errors);
                errors.AddRange(ValidateCommon(definition).Select(error => path + ": " + error));
            }
            return errors;
        }

        public static List<string> ValidateCommon(CommonEventDefinition definition)
        {
            var errors = new List<string>();
            if (definition == null) { errors.Add("공통 이벤트 데이터가 없습니다."); return errors; }
            errors.AddRange(definition.ValidateIdentity());
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (definition.parameters != null)
                foreach (var parameter in definition.parameters)
                {
                    if (parameter == null || string.IsNullOrWhiteSpace(parameter.name)) { errors.Add("매개변수 이름이 비어 있습니다."); continue; }
                    if (!names.Add(parameter.name.Trim())) errors.Add("중복 매개변수: " + parameter.name);
                    if (!Enum.IsDefined(typeof(EventParameterType), parameter.type)) errors.Add("알 수 없는 매개변수 타입: " + parameter.name);
                }
            try { EventProgram.ValidateAndIndex(definition.commands); }
            catch (ArgumentException error) { errors.Add(error.Message); }
            errors.AddRange(ReachableCommonIdentityErrors(definition.commands));
            // The runtime compiler repairs absent IDs. An editor diagnostic must
            // gate every reachable asset before invoking it, including unused
            // common roots whose commands have not yet been upgraded.
            if (errors.Count > 0) return errors.Distinct().ToList();
            // Validate even unused common assets: a cycle must not stay hidden until a caller is authored.
            var probe = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                probe.pages.Add(new EventPage { commands = new List<EventCommand>
                { new EventCommand { type = EventCommandType.CallCommon, commonEvent = definition } } });
                probe.EnsureStableIds();
                try { EventProgram.ValidateForExecution(probe, 0, false); }
                catch (ArgumentException error) { if (!errors.Contains(error.Message)) errors.Add(error.Message); }
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            return errors;
        }

        public static List<string> ValidateDefinitionPage(EventDefinition definition, int pageIndex)
        {
            var errors = new List<string>();
            if (definition == null) { errors.Add("이벤트 데이터가 없습니다."); return errors; }
            errors.AddRange(definition.ValidateIdentity());
            if (definition.pages == null || pageIndex < 0 || pageIndex >= definition.pages.Count || definition.pages[pageIndex] == null)
            { errors.Add("검사할 이벤트 페이지가 없습니다: " + (pageIndex + 1)); return errors.Distinct().ToList(); }
            var page = definition.pages[pageIndex];
            errors.AddRange(ReachableCommonIdentityErrors(page.commands));
            if (errors.Count > 0) return errors.Distinct().ToList();
            try { EventProgram.ValidateForExecution(definition, pageIndex, page.trigger == EventTrigger.Parallel); }
            catch (ArgumentException error) { errors.Add(error.Message); }
            return errors;
        }

        static List<string> ReachableCommonIdentityErrors(List<EventCommand> rootCommands)
        {
            var errors = new List<string>();
            var visited = new HashSet<CommonEventDefinition>();
            var pending = new Queue<CommonEventDefinition>();
            void Enqueue(List<EventCommand> commands)
            {
                if (commands == null) return;
                foreach (var command in commands)
                    if (command?.type == EventCommandType.CallCommon && command.commonEvent != null && visited.Add(command.commonEvent))
                        pending.Enqueue(command.commonEvent);
            }
            Enqueue(rootCommands);
            int checkedAssets = 0;
            while (pending.Count > 0)
            {
                if (++checkedAssets > 1024) { errors.Add("공통 이벤트 ID 검사 그래프가 1,024개 에셋 한도를 초과했습니다."); break; }
                var common = pending.Dequeue();
                string name = AssetDatabase.GetAssetPath(common);
                if (string.IsNullOrEmpty(name)) name = string.IsNullOrEmpty(common.name) ? common.displayName : common.name;
                foreach (string error in common.ValidateIdentity()) errors.Add("공통 이벤트 '" + name + "': " + error);
                Enqueue(common.commands);
            }
            return errors;
        }

        static void CollectCommands(List<EventCommand> commands, string path, Dictionary<string, string> ids, List<string> errors)
        {
            if (commands == null) return;
            foreach (var command in commands) if (command != null) Unique(command.commandId, path, "commandId", ids, errors);
        }
        static void Unique(string id, string path, string field, Dictionary<string, string> ids, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id)) { errors.Add(path + ": " + field + "가 비어 있습니다. Upgrade Event Identities를 실행하세요."); return; }
            if (ids.TryGetValue(id, out string previous))
                errors.Add(path + ": " + field + " '" + id + "' 중복. 기존 에셋: " + previous);
            else ids.Add(id, path);
        }
        static void RenewCommands(List<EventCommand> commands)
        { if (commands != null) foreach (var command in commands) if (command != null) command.commandId = NewId(); }
        static string NewId() => Guid.NewGuid().ToString("N");
    }
}
