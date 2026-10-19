using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public enum ContentDiagnosticSeverity { Warning, Error }
    public enum ContentReferenceCategory { Switch, Variable, Item, SelfSwitch, Companion, CommonEvent, Event, Map, Clue, Action, Objective, Asset, Npc, NpcRoute, NpcPlacement }

    public sealed class ContentReference
    {
        public ScriptableObject Owner { get; internal set; }
        public int PageIndex { get; internal set; } = -1;
        public int CommandIndex { get; internal set; } = -1;
        public ContentReferenceCategory Category { get; internal set; }
        public string Key { get; internal set; }
        public string Message { get; internal set; }
        public bool IsWrite { get; internal set; }
        public UnityEngine.Object Target { get; internal set; }
    }

    public sealed class ContentDiagnostic
    {
        public ContentDiagnosticSeverity Severity { get; internal set; }
        public ContentReference Reference { get; internal set; }
        public string Message { get; internal set; }
        public ScriptableObject Owner => Reference?.Owner;
        public int PageIndex => Reference?.PageIndex ?? -1;
        public int CommandIndex => Reference?.CommandIndex ?? -1;
        public override string ToString() => (Owner != null ? Owner.name + ": " : "") + Message;
    }

    // A read-only snapshot of authoring assets. Builds never issue IDs or normalize assets.
    public sealed class ContentReferenceIndex
    {
        readonly List<ContentReference> references = new List<ContentReference>();
        readonly List<ContentDiagnostic> diagnostics = new List<ContentDiagnostic>();
        readonly HashSet<ScriptableObject> assets = new HashSet<ScriptableObject>();
        readonly Dictionary<ContentReferenceCategory, HashSet<string>> writers = new Dictionary<ContentReferenceCategory, HashSet<string>>();
        readonly Dictionary<ContentReferenceCategory, HashSet<string>> declared = new Dictionary<ContentReferenceCategory, HashSet<string>>();
        readonly Dictionary<ContentReferenceCategory, HashSet<string>> keys = new Dictionary<ContentReferenceCategory, HashSet<string>>();
        public IReadOnlyList<string> SwitchKeys { get; private set; }
        public IReadOnlyList<string> VariableKeys { get; private set; }
        public IReadOnlyList<string> ItemIds { get; private set; }
        public IReadOnlyList<CompanionDefinition> Companions { get; private set; }
        public IReadOnlyList<CommonEventDefinition> CommonEvents { get; private set; }
        public IReadOnlyList<MapDefinition> Maps { get; private set; }
        public IReadOnlyList<StateKeyRegistry> Registries { get; private set; }
        public IReadOnlyList<NpcDefinition> Npcs { get; private set; }
        public IReadOnlyList<NpcRouteDefinition> NpcRoutes { get; private set; }
        public IReadOnlyList<string> NpcPlacementIds { get; private set; }
        public IReadOnlyList<ContentReference> References => references;
        public IReadOnlyList<ContentDiagnostic> GetDiagnostics() => diagnostics;

        public static ContentReferenceIndex Build() => Build(LoadAssets());
        public static ContentReferenceIndex Build(IEnumerable<ScriptableObject> source)
        {
            var index = new ContentReferenceIndex();
            if (source != null) foreach (var asset in source) if (asset != null) index.assets.Add(asset);
            index.Companions = index.assets.OfType<CompanionDefinition>().OrderBy(x => x.companionId, StringComparer.Ordinal).ToArray();
            index.CommonEvents = index.assets.OfType<CommonEventDefinition>().OrderBy(x => x.commonId, StringComparer.Ordinal).ToArray();
            index.Maps = index.assets.OfType<MapDefinition>().OrderBy(x => x.mapId, StringComparer.Ordinal).ToArray();
            index.Registries = index.assets.OfType<StateKeyRegistry>().OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
            index.Npcs = index.assets.OfType<NpcDefinition>().OrderBy(x => x.npcId, StringComparer.Ordinal).ToArray();
            index.NpcRoutes = index.assets.OfType<NpcRouteDefinition>().OrderBy(x => x.routeId, StringComparer.Ordinal).ToArray();
            index.NpcPlacementIds = index.Maps.Where(map => map.events != null).SelectMany(map => map.events)
                .Where(placement => placement?.npc != null).Select(placement => placement.id).Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            foreach (var registry in index.Registries) index.ScanRegistry(registry);
            foreach (var item in index.assets.OfType<ItemEffectDefinition>()) index.Declare(ContentReferenceCategory.Item, item.itemId);
            foreach (var companion in index.Companions) index.Declare(ContentReferenceCategory.Companion, companion.companionId);
            foreach (var definition in index.assets.OfType<EventDefinition>()) index.ScanEvent(definition);
            foreach (var common in index.CommonEvents) index.ScanCommands(common, -1, common.commands);
            foreach (var map in index.Maps) index.ScanMap(map);
            foreach (var npc in index.Npcs) index.ScanNpc(npc);
            index.CheckKeys();
            index.SwitchKeys = index.SortedKeys(ContentReferenceCategory.Switch);
            index.VariableKeys = index.SortedKeys(ContentReferenceCategory.Variable);
            index.ItemIds = index.SortedKeys(ContentReferenceCategory.Item);
            return index;
        }

        public static IEnumerable<ScriptableObject> LoadAssets()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/NoSleep" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) yield return asset;
            }
        }
        IReadOnlyList<string> SortedKeys(ContentReferenceCategory category) =>
            Set(keys, category).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        static HashSet<string> Set(Dictionary<ContentReferenceCategory, HashSet<string>> dictionary, ContentReferenceCategory category)
        {
            if (!dictionary.TryGetValue(category, out var values)) dictionary.Add(category, values = new HashSet<string>(StringComparer.Ordinal));
            return values;
        }
        static bool Dynamic(string value) => (value ?? "").Contains("{param:");
        void Declare(ContentReferenceCategory category, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || Dynamic(key)) return;
            Set(declared, category).Add(key.Trim()); Set(keys, category).Add(key.Trim());
        }
        ContentReference Add(ScriptableObject owner, int page, int command, ContentReferenceCategory category,
            string key, string message, bool write = false, UnityEngine.Object target = null)
        {
            var reference = new ContentReference { Owner = owner, PageIndex = page, CommandIndex = command,
                Category = category, Key = key ?? "", Message = message, IsWrite = write, Target = target };
            references.Add(reference);
            if (!string.IsNullOrWhiteSpace(key) && !Dynamic(key))
            {
                Set(keys, category).Add(key.Trim());
                if (write) Set(writers, category).Add(key.Trim());
            }
            return reference;
        }
        void Report(ContentReference reference, string message, ContentDiagnosticSeverity severity = ContentDiagnosticSeverity.Error) =>
            diagnostics.Add(new ContentDiagnostic { Reference = reference, Message = message, Severity = severity });
        void ScanRegistry(StateKeyRegistry registry)
        {
            RegisterList(ContentReferenceCategory.Switch, registry.switchKeys);
            RegisterList(ContentReferenceCategory.Variable, registry.variableKeys);
            RegisterList(ContentReferenceCategory.Item, registry.itemIds);
            void RegisterList(ContentReferenceCategory category, List<string> values)
            {
                if (values == null) return;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string key in values)
                {
                    var row = Add(registry, -1, -1, category, key, "Declared key");
                    if (string.IsNullOrWhiteSpace(key) || Dynamic(key) || !seen.Add(key.Trim()))
                        Report(row, "선언 키가 비어 있거나 중복되었거나 지역 인자 토큰을 포함합니다.", ContentDiagnosticSeverity.Warning);
                    else Declare(category, key);
                }
            }
        }
        void ScanEvent(EventDefinition definition)
        {
            if (definition.pages == null)
            { Report(Add(definition, -1, -1, ContentReferenceCategory.Asset, "", "Pages"), "이벤트 페이지 목록이 없습니다."); return; }
            for (int pageIndex = 0; pageIndex < definition.pages.Count; pageIndex++)
            {
                var page = definition.pages[pageIndex];
                if (page == null)
                { Report(Add(definition, pageIndex, -1, ContentReferenceCategory.Asset, "", "Page"), "페이지 데이터가 없습니다."); continue; }
                if (page.conditions != null) foreach (var condition in page.conditions) ScanCondition(definition, pageIndex, -1, condition, "Page condition");
                ScanCommands(definition, pageIndex, page.commands);
            }
        }
        void ScanCommands(ScriptableObject owner, int pageIndex, List<EventCommand> commands)
        {
            if (commands == null)
            { Report(Add(owner, pageIndex, -1, ContentReferenceCategory.Asset, "", "Commands"), "명령 목록이 없습니다."); return; }
            var labels = new HashSet<string>(commands.Where(x => x != null && x.type == EventCommandType.Label).Select(x => (x.label ?? "").Trim()), StringComparer.Ordinal);
            for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
            {
                var command = commands[commandIndex];
                if (command == null)
                { Report(Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Asset, "", "Command"), "명령 데이터가 없습니다."); continue; }
                string message = command.type.ToString();
                switch (command.type)
                {
                    case EventCommandType.SetSwitch: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Switch, command.key, message, true); break;
                    case EventCommandType.SetVariable:
                    case EventCommandType.AddVariable: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Variable, command.key, message, true); break;
                    case EventCommandType.AddItem: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Item, command.key, message, true); break;
                    case EventCommandType.SetSelfSwitch: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.SelfSwitch, command.key, message, true); break;
                    case EventCommandType.SetObjective: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Objective, command.key, message, true); break;
                    case EventCommandType.RecordClue: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Clue, command.key, message, true); break;
                    case EventCommandType.UnlockAction: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Action, command.key, message, true); break;
                    case EventCommandType.JoinCompanion:
                    case EventCommandType.LeaveCompanion: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Companion, command.key, message); break;
                    case EventCommandType.IfCondition:
                        if (!command.useLocalCondition) ScanCondition(owner, pageIndex, commandIndex, command.condition, "IfCondition");
                        CheckTarget(command.targetLabel);
                        break;
                    case EventCommandType.Jump: CheckTarget(command.targetLabel); break;
                    case EventCommandType.ShowChoices:
                        if (command.choices != null) foreach (var choice in command.choices)
                            if (choice != null && !string.IsNullOrWhiteSpace(choice.targetLabel)) CheckTarget(choice.targetLabel);
                        break;
                    case EventCommandType.CallCommon:
                        AssetReference(ContentReferenceCategory.CommonEvent, command.commonEvent, command.commonEvent?.commonId, "공통 이벤트");
                        break;
                    case EventCommandType.RequestForeground:
                        AssetReference(ContentReferenceCategory.Event, command.foregroundEvent, command.foregroundEvent?.definitionId, "전경 이벤트");
                        break;
                    case EventCommandType.TransferPlayer:
                        AssetReference(ContentReferenceCategory.Map, command.map, command.map?.mapId, "목적지 맵");
                        if (command.map != null && (!command.map.IsInBounds(command.cell) || !command.map.IsWalkable(command.cell)))
                            Report(references[references.Count - 1], "이동 목적지가 범위 밖이거나 이동 가능한 타일이 아닙니다: " + command.cell);
                        break;
                    case EventCommandType.UseItem:
                        AssetReference(ContentReferenceCategory.Item, command.item, command.item?.itemId, "아이템 효과");
                        break;
                    case EventCommandType.StayAwake: AssetReference(ContentReferenceCategory.Asset, command.nightActivity, command.nightActivity?.activityId, "밤샘 활동"); break;
                    case EventCommandType.StartEncounter: AssetReference(ContentReferenceCategory.Asset, command.enemy, command.enemy?.enemyId, "적 정의"); break;
                    case EventCommandType.MoveNpc:
                    case EventCommandType.FaceNpc:
                    case EventCommandType.StopNpc:
                    case EventCommandType.ResumeNpc:
                        var npcReference = Add(owner, pageIndex, commandIndex, ContentReferenceCategory.NpcPlacement, command.key, message);
                        if (!string.IsNullOrWhiteSpace(command.key) && !Dynamic(command.key) && !NpcPlacementIds.Contains(command.key.Trim()))
                            Report(npcReference, "일치하는 NPC 배치 ID가 없습니다. 현재 맵의 배치 ID 또는 호출자 문맥을 확인하세요: " + command.key, ContentDiagnosticSeverity.Warning);
                        break;
                    case EventCommandType.GrantOnce:
                        if (command.mutations != null) foreach (var mutation in command.mutations)
                        {
                            if (mutation == null) continue;
                            switch (mutation.kind)
                            {
                                case StateMutationKind.SetSwitch: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Switch, mutation.key, "GrantOnce", true); break;
                                case StateMutationKind.SetVariable:
                                case StateMutationKind.AddVariable: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Variable, mutation.key, "GrantOnce", true); break;
                                case StateMutationKind.AddItem: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Item, mutation.key, "GrantOnce", true); break;
                                case StateMutationKind.SetSelfSwitch: Add(owner, pageIndex, commandIndex, ContentReferenceCategory.SelfSwitch, mutation.key, "GrantOnce", true); break;
                            }
                        }
                        break;
                }
                void CheckTarget(string label)
                {
                    if (!labels.Contains((label ?? "").Trim()))
                        Report(Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Asset, label, "Label target"), "분기 대상 라벨이 없습니다: " + label);
                }
                void AssetReference(ContentReferenceCategory category, ScriptableObject target, string id, string label)
                {
                    var row = Add(owner, pageIndex, commandIndex, category, id, message, false, target);
                    if (target == null) Report(row, label + " 참조가 없습니다.");
                    else if (!assets.Contains(target)) Report(row, label + " 참조가 Assets/NoSleep 콘텐츠 목록에 없습니다.", category == ContentReferenceCategory.Map ? ContentDiagnosticSeverity.Error : ContentDiagnosticSeverity.Warning);
                }
            }
            // General semantic failures retain a navigable asset/page row. Per-command
            // missing references and labels above have their exact command location.
            try { EventProgram.ValidateAndIndex(commands); }
            catch (ArgumentException error)
            {
                var match = Regex.Match(error.Message, @"Command (\d+)");
                int commandIndex = match.Success ? int.Parse(match.Groups[1].Value) - 1 : -1;
                Report(Add(owner, pageIndex, commandIndex, ContentReferenceCategory.Asset, "", "Program"), error.Message);
            }
        }
        void ScanCondition(ScriptableObject owner, int page, int command, StateCondition condition, string message)
        {
            if (condition == null) { Report(Add(owner, page, command, ContentReferenceCategory.Asset, "", message), "조건 데이터가 없습니다."); return; }
            ContentReferenceCategory category;
            switch (condition.kind)
            {
                case ConditionKind.Switch: category = ContentReferenceCategory.Switch; break;
                case ConditionKind.Variable: category = ContentReferenceCategory.Variable; break;
                case ConditionKind.Item: category = ContentReferenceCategory.Item; break;
                case ConditionKind.SelfSwitch: category = ContentReferenceCategory.SelfSwitch; break;
                case ConditionKind.Companion: category = ContentReferenceCategory.Companion; break;
                case ConditionKind.Clue: category = ContentReferenceCategory.Clue; break;
                case ConditionKind.ActionUnlocked: category = ContentReferenceCategory.Action; break;
                default: return;
            }
            Add(owner, page, command, category, condition.key, message);
        }
        void ScanMap(MapDefinition map)
        {
            if (map.events == null) return;
            foreach (var placement in map.events)
            {
                if (placement == null) continue;
                if (placement.definition != null || placement.npc == null)
                {
                    var row = Add(map, -1, -1, ContentReferenceCategory.Event, placement.definition?.definitionId,
                        "배치 " + placement.id + " @ " + placement.cell, false, placement.definition);
                    if (placement.definition == null) Report(row, "배치 '" + placement.id + "'의 이벤트 정의가 없습니다.");
                    else if (!assets.Contains(placement.definition)) Report(row, "배치 이벤트 정의가 Assets/NoSleep 콘텐츠 목록에 없습니다.", ContentDiagnosticSeverity.Warning);
                }
                if (placement.npc != null) Add(map, -1, -1, ContentReferenceCategory.Npc, placement.npc.npcId,
                    "NPC 배치 " + placement.id + " @ " + placement.cell, false, placement.npc);
            }
        }
        void ScanNpc(NpcDefinition npc)
        {
            Route(npc.route, "기본 NPC 경로");
            if (npc.enemy != null) Add(npc, -1, -1, ContentReferenceCategory.Asset, npc.enemy.enemyId, "적 프로필", false, npc.enemy);
            if (npc.schedules != null) foreach (var schedule in npc.schedules)
            {
                if (schedule == null) continue;
                Route(schedule.route, "일과 경로 · " + schedule.scheduleId);
                if (schedule.conditions != null) foreach (var condition in schedule.conditions) ScanCondition(npc, -1, -1, condition, "NPC 일과 조건");
            }
            void Route(NpcRouteDefinition route, string message)
            {
                if (route == null) return;
                var row = Add(npc, -1, -1, ContentReferenceCategory.NpcRoute, route.routeId, message, false, route);
                if (!assets.Contains(route)) Report(row, "NPC 경로가 Assets/NoSleep 콘텐츠 목록 밖에 있습니다.", ContentDiagnosticSeverity.Warning);
            }
        }
        void CheckKeys()
        {
            foreach (var row in references)
            {
                if (row.Category != ContentReferenceCategory.Switch && row.Category != ContentReferenceCategory.Variable &&
                    row.Category != ContentReferenceCategory.Item && row.Category != ContentReferenceCategory.Companion) continue;
                if (row.Owner is StateKeyRegistry || row.Target != null || Dynamic(row.Key)) continue;
                if (string.IsNullOrWhiteSpace(row.Key))
                { Report(row, "상태 키가 비어 있습니다."); continue; }
                string key = row.Key.Trim();
                bool known = Set(declared, row.Category).Contains(key) || Set(writers, row.Category).Contains(key);
                if (!row.IsWrite && !known)
                    Report(row, "선언 또는 쓰기 사용처가 없는 키입니다. 오타인지 확인하세요: " + key, ContentDiagnosticSeverity.Warning);
            }
        }
    }
}
