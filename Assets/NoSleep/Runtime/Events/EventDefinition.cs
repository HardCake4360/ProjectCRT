using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    public enum EventTrigger { Action, PlayerTouch, Autorun, Parallel }
    public enum EventCommandType
    {
        ShowDialogue, ShowChoices, SetSwitch, SetVariable, AddVariable,
        SetSelfSwitch, AddItem, AddGold, Wait, TransferPlayer,
        IfCondition, Jump, Label, End,
        AdvanceTime, SetObjective, RecordClue, JoinCompanion, LeaveCompanion,
        ClearParty, UnlockAction, UseItem, StayAwake, StartEncounter, ReachDestination,
        CallCommon, RequestForeground, GrantOnce, WaitGameMinutes, Checkpoint,
        MoveNpc, FaceNpc, StopNpc, ResumeNpc
    }

    [Serializable]
    public sealed class EventChoice
    {
        public string text;
        public string targetLabel;
    }

    // A linear instruction stream keeps Unity serialization and branch editing predictable.
    [Serializable]
    public sealed class EventCommand
    {
        public string commandId;
        public EventCommandType type;
        public string speaker;
        [TextArea(2, 6)] public string text;
        public string key;
        public string label;
        public string targetLabel;
        public int intValue;
        public bool boolValue = true;
        public float seconds;
        public StateCondition condition = new StateCondition();
        public MapDefinition map;
        public Vector2Int cell;
        public List<EventChoice> choices = new List<EventChoice>();
        public ItemEffectDefinition item;
        public NightActivityDefinition nightActivity;
        public EnemyDefinition enemy;
        public CommonEventDefinition commonEvent;
        public List<EventArgument> arguments = new List<EventArgument>();
        public EventDefinition foregroundEvent;
        public List<StateMutation> mutations = new List<StateMutation>();
        public string intParameter;
        public string boolParameter;
        public bool useLocalCondition;
        public EventLocalCondition localCondition = new EventLocalCondition();
    }

    [Serializable]
    public sealed class EventPage
    {
        public string pageId;
        public int legacyPageIndex = -1;
        public string name = "Page";
        public List<StateCondition> conditions = new List<StateCondition>();
        public EventTrigger trigger;
        public bool runOnce;
        public bool visible = true;
        public bool blocksMovement = true;
        public List<EventCommand> commands = new List<EventCommand>();
        public bool parallelRepeat;
        [Min(0.01f)] public float parallelIntervalSeconds = 0.25f;

        public bool Matches(GameState state, string eventKey)
        {
            if (conditions == null) return true;
            foreach (var condition in conditions)
                if (condition == null || !condition.Evaluate(state, eventKey)) return false;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "No Sleep/Event", fileName = "Event")]
    public sealed class EventDefinition : ScriptableObject
    {
        public string definitionId;
        public string displayName = "Event";
        public GameObject prefab;
        public Color fallbackColor = new Color(0.65f, 0.4f, 0.8f);
        public List<EventPage> pages = new List<EventPage>();

        // Capture the original order exactly once, before authors begin reordering pages.
        public bool EnsureStableIds()
        {
            bool firstConversion = string.IsNullOrWhiteSpace(definitionId);
            bool changed = false;
            if (firstConversion) { definitionId = Guid.NewGuid().ToString("N"); changed = true; }
            if (pages == null) return changed;
            for (int index = 0; index < pages.Count; index++)
            {
                var page = pages[index];
                if (page == null) continue;
                if (firstConversion) { page.legacyPageIndex = index; changed = true; }
                if (string.IsNullOrWhiteSpace(page.pageId))
                {
                    page.pageId = Guid.NewGuid().ToString("N");
                    if (!firstConversion) page.legacyPageIndex = -1;
                    changed = true;
                }
                if (EventProgram.EnsureCommandIds(page.commands)) changed = true;
            }
            return changed;
        }

        public List<string> ValidateIdentity()
        {
            var errors = new List<string>();
            if (!EventProgram.ValidIdentity(definitionId)) errors.Add("Event definition ID is missing or contains '/' or ':'.");
            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            var legacyIndices = new HashSet<int>();
            if (pages == null) { errors.Add("Event pages are missing."); return errors; }
            for (int index = 0; index < pages.Count; index++)
            {
                var page = pages[index];
                if (page == null) { errors.Add($"Page {index + 1} is missing."); continue; }
                if (!EventProgram.ValidIdentity(page.pageId) || !pageIds.Add(page.pageId)) errors.Add($"Page {index + 1} has a missing or duplicate page ID.");
                if (page.legacyPageIndex < -1 || (page.legacyPageIndex >= 0 && !legacyIndices.Add(page.legacyPageIndex)))
                    errors.Add($"Page {index + 1} has an invalid or duplicate captured legacy index.");
                EventProgram.ValidateCommandIdentities(page.commands, errors, $"Page {index + 1}");
                if (float.IsNaN(page.parallelIntervalSeconds) || float.IsInfinity(page.parallelIntervalSeconds) || page.parallelIntervalSeconds <= 0)
                    errors.Add($"Page {index + 1} needs a finite positive parallel interval.");
            }
            return errors;
        }

        public bool ValidateIdentity(out string error)
        { var errors = ValidateIdentity(); error = string.Join("\n", errors); return errors.Count == 0; }

        public int ResolvePageIndex(GameState state, string eventKey)
        {
            if (pages == null) return -1;
            for (int index = pages.Count - 1; index >= 0; index--)
                if (pages[index] != null && pages[index].Matches(state, eventKey)) return index;
            return -1;
        }

        public EventPage ResolvePage(GameState state, string eventKey)
        {
            int index = ResolvePageIndex(state, eventKey);
            return index < 0 ? null : pages[index];
        }

        // This name is reserved by the interpreter. Completion survives saving and map reloads.
        public static string CompletionKey(int pageIndex) => "__nosleep_completed_page_" + pageIndex;
        public string StableCompletionKey(string pageId) => "__nosleep_completed_" + definitionId.Length + ":" + definitionId + ":" + pageId;

        public bool IsPageCompleted(GameState state, string eventKey, int pageIndex)
        {
            if (state == null || string.IsNullOrWhiteSpace(eventKey) || pages == null || pageIndex < 0 ||
                pageIndex >= pages.Count || pages[pageIndex] == null || !pages[pageIndex].runOnce) return false;
            EnsureStableIds();
            var page = pages[pageIndex];
            string stableKey = StableCompletionKey(page.pageId);
            if (state.HasSelfSwitch(eventKey, stableKey)) return state.GetSelfSwitch(eventKey, stableKey);
            return page.legacyPageIndex >= 0 && state.GetSelfSwitch(eventKey, CompletionKey(page.legacyPageIndex));
        }
    }

    public interface IEventPresentation
    {
        IEnumerator ShowDialogue(string speaker, string text);
        IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> onSelected);
    }

    public static partial class EventProgram
    {
        public static bool ValidIdentity(string id) => !string.IsNullOrWhiteSpace(id) && id == id.Trim() && !id.Contains("/") && !id.Contains(":");
        public static bool EnsureCommandIds(List<EventCommand> commands)
        {
            bool changed = false;
            if (commands != null) foreach (var command in commands)
                if (command != null && string.IsNullOrWhiteSpace(command.commandId)) { command.commandId = Guid.NewGuid().ToString("N"); changed = true; }
            return changed;
        }
        internal static void ValidateCommandIdentities(List<EventCommand> commands, List<string> errors, string owner)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (commands == null) { errors.Add(owner + " command list is missing."); return; }
            for (int index = 0; index < commands.Count; index++)
                if (commands[index] == null || !ValidIdentity(commands[index].commandId) || !ids.Add(commands[index].commandId))
                    errors.Add($"{owner} command {index + 1} has a missing or duplicate command ID.");
        }
        public static Dictionary<string, int> ValidateAndIndex(IReadOnlyList<EventCommand> commands)
        {
            if (commands == null) throw new ArgumentException("Event command list is missing.");
            var labels = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                if (command == null) throw new ArgumentException($"Command {index + 1} is missing.");
                if (!Enum.IsDefined(typeof(EventCommandType), command.type))
                    throw new ArgumentException($"Command {index + 1} has an unknown instruction.");
                if (command.type != EventCommandType.Label) continue;
                string label = RequireKey(command.label, $"Command {index + 1} label");
                if (labels.ContainsKey(label)) throw new ArgumentException($"Duplicate event label '{label}'.");
                labels.Add(label, index);
            }

            for (int index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                switch (command.type)
                {
                    case EventCommandType.Jump:
                    case EventCommandType.IfCondition:
                        RequireTarget(command.targetLabel, labels);
                        if (command.type == EventCommandType.IfCondition && !command.useLocalCondition && command.condition == null)
                            throw new ArgumentException($"Command {index + 1} condition is missing.");
                        break;
                    case EventCommandType.ShowChoices:
                        if (command.choices == null || command.choices.Count == 0)
                            throw new ArgumentException($"Command {index + 1} needs at least one choice.");
                        foreach (var choice in command.choices)
                        {
                            if (choice == null || string.IsNullOrWhiteSpace(choice.text))
                                throw new ArgumentException($"Command {index + 1} contains an empty choice.");
                            if (!string.IsNullOrWhiteSpace(choice.targetLabel)) RequireTarget(choice.targetLabel, labels);
                        }
                        break;
                    case EventCommandType.SetSwitch:
                    case EventCommandType.SetVariable:
                    case EventCommandType.AddVariable:
                    case EventCommandType.SetSelfSwitch:
                    case EventCommandType.AddItem:
                    case EventCommandType.SetObjective:
                    case EventCommandType.RecordClue:
                    case EventCommandType.JoinCompanion:
                    case EventCommandType.LeaveCompanion:
                    case EventCommandType.UnlockAction:
                        RequireKey(command.key, $"Command {index + 1} state key");
                        if (command.type == EventCommandType.SetSelfSwitch && command.key.Trim().StartsWith("__nosleep_", StringComparison.Ordinal))
                            throw new ArgumentException("Self-switch names beginning with '__nosleep_' are reserved.");
                        break;
                    case EventCommandType.AdvanceTime:
                    case EventCommandType.WaitGameMinutes:
                        if (command.intValue < 0) throw new ArgumentException($"Command {index + 1} time cannot be negative.");
                        break;
                    case EventCommandType.CallCommon:
                        if (command.commonEvent == null) throw new ArgumentException($"Command {index + 1} common event is missing.");
                        break;
                    case EventCommandType.RequestForeground:
                        if (command.foregroundEvent == null) throw new ArgumentException($"Command {index + 1} foreground event is missing.");
                        break;
                    case EventCommandType.GrantOnce:
                        if (command.mutations == null || command.mutations.Count == 0 || command.mutations.Count > 64)
                            throw new ArgumentException($"Command {index + 1} needs 1 to 64 grant mutations.");
                        break;
                    case EventCommandType.UseItem:
                        if (command.item == null) throw new ArgumentException($"Command {index + 1} item effect is missing.");
                        break;
                    case EventCommandType.StayAwake:
                        if (command.nightActivity == null) throw new ArgumentException($"Command {index + 1} night activity is missing.");
                        break;
                    case EventCommandType.StartEncounter:
                        if (command.enemy == null) throw new ArgumentException($"Command {index + 1} enemy is missing.");
                        if (!string.IsNullOrWhiteSpace(command.key) && command.key.Trim().StartsWith("__nosleep_", StringComparison.Ordinal))
                            throw new ArgumentException("Encounter completion key is reserved.");
                        break;
                    case EventCommandType.Wait:
                        if (float.IsNaN(command.seconds) || float.IsInfinity(command.seconds) || command.seconds < 0)
                            throw new ArgumentException($"Command {index + 1} needs a finite, nonnegative wait.");
                        break;
                    case EventCommandType.TransferPlayer:
                        if (command.map == null) throw new ArgumentException($"Command {index + 1} destination map is missing.");
                        break;
                    case EventCommandType.MoveNpc:
                        if (command.cell.x < 0 || command.cell.y < 0) throw new ArgumentException($"Command {index + 1} NPC destination cell cannot be negative.");
                        break;
                }
            }
            return labels;
        }

        static string RequireKey(string key, string description)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(description + " cannot be empty.");
            return key.Trim();
        }

        static void RequireTarget(string target, Dictionary<string, int> labels)
        {
            target = RequireKey(target, "Jump target");
            if (!labels.ContainsKey(target)) throw new ArgumentException($"Event label '{target}' does not exist.");
        }
    }
}
