using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep.Editor
{
    public enum EventTemplateKind { NpcDialogue, OneTimeReward, ConditionalBranch, StayAwake, ContactCombat, CommonCall }

    [Serializable]
    public sealed class EventTemplateReferences
    {
        public string speaker = "NPC";
        public string text = "대사를 입력하세요.";
        public string rewardItemId = "reward_item";
        public int rewardItemAmount = 1;
        public int rewardGold = 10;
        public StateCondition condition = new StateCondition { kind = ConditionKind.Switch, key = "door_unlocked", expected = true };
        public MapDefinition destinationMap;
        public Vector2Int destinationCell;
        public NightActivityDefinition nightActivity;
        public EnemyDefinition enemy;
        public CommonEventDefinition commonEvent;
    }

    [CreateAssetMenu(menuName = "No Sleep/Authoring/Event Template", fileName = "EventTemplate")]
    public sealed class EventTemplateDefinition : ScriptableObject
    {
        public string displayName = "Event Template";
        [TextArea(2, 8)] public string description;
        public List<EventPage> pages = new List<EventPage>();

        public List<string> GetDiagnostics()
        {
            var errors = new List<string>();
            if (pages == null || pages.Count == 0) { errors.Add("템플릿에 페이지가 없습니다."); return errors; }
            for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var page = pages[pageIndex];
                if (page == null) { errors.Add($"페이지 {pageIndex + 1}: 페이지 데이터가 없습니다."); continue; }
                if (page.commands == null) { errors.Add($"페이지 {pageIndex + 1}: 명령 목록이 없습니다."); continue; }
                for (int commandIndex = 0; commandIndex < page.commands.Count; commandIndex++)
                {
                    var command = page.commands[commandIndex];
                    string prefix = $"페이지 {pageIndex + 1} 명령 {commandIndex + 1}: ";
                    if (command == null) { errors.Add(prefix + "명령 데이터가 없습니다."); continue; }
                    if (command.type == EventCommandType.TransferPlayer && command.map == null) errors.Add(prefix + "목적지 맵(map)을 연결하세요.");
                    if (command.type == EventCommandType.TransferPlayer && command.map != null && !command.map.IsWalkable(command.cell)) errors.Add(prefix + "목적지 타일(cell)이 범위 밖이거나 이동 불가입니다.");
                    if (command.type == EventCommandType.StartEncounter && command.enemy == null) errors.Add(prefix + "접촉할 적(enemy)을 연결하세요.");
                    if (command.type == EventCommandType.StayAwake && command.nightActivity == null) errors.Add(prefix + "밤샘 활동(nightActivity)을 연결하세요.");
                    if (command.type == EventCommandType.CallCommon && command.commonEvent == null) errors.Add(prefix + "공통 이벤트(commonEvent)를 연결하세요.");
                }
            }
            var probe = CreateInstance<EventDefinition>();
            var detachedCommons = new Dictionary<CommonEventDefinition, CommonEventDefinition>();
            try
            {
                // Validation may assign missing stable IDs; use detached copies so
                // inspecting a template cannot mutate its authoring data.
                foreach (var page in pages) probe.pages.Add(EventEditOperations.Clone(page));
                DetachCommonGraph(probe, detachedCommons);
                probe.EnsureStableIds();
                for (int index = 0; index < probe.pages.Count; index++)
                    try { EventProgram.ValidateForExecution(probe, index, probe.pages[index]?.trigger == EventTrigger.Parallel); }
                    catch (ArgumentException error) { errors.Add($"페이지 {index + 1}: {error.Message}"); }
            }
            catch (ArgumentException error) { errors.Add("템플릿: " + error.Message); }
            finally
            {
                DestroyImmediate(probe);
                foreach (var common in detachedCommons.Values) DestroyImmediate(common);
            }
            return errors;
        }

        static void DetachCommonGraph(EventDefinition probe, Dictionary<CommonEventDefinition, CommonEventDefinition> detached)
        {
            var pending = new Queue<CommonEventDefinition>();
            void Rewrite(List<EventCommand> commands)
            {
                if (commands == null) return;
                foreach (var command in commands)
                {
                    if (command?.commonEvent == null) continue;
                    var original = command.commonEvent;
                    if (!detached.TryGetValue(original, out var copy))
                    {
                        if (detached.Count >= 1024) throw new ArgumentException("Common event graph exceeds the template validation budget.");
                        copy = CreateInstance<CommonEventDefinition>();
                        copy.commonId = original.commonId; copy.displayName = original.displayName;
                        copy.parameters = EventEditOperations.Clone(original.parameters);
                        copy.commands = EventEditOperations.Clone(original.commands);
                        detached.Add(original, copy); pending.Enqueue(original);
                    }
                    command.commonEvent = copy;
                }
            }
            foreach (var page in probe.pages) if (page != null) Rewrite(page.commands);
            while (pending.Count > 0) Rewrite(detached[pending.Dequeue()].commands);
        }
    }
}
