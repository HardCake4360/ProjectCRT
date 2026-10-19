using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoSleep.Editor
{
    public sealed class EventEditorDiagnostic
    { public int PageIndex, CommandIndex; public string Message; }
    public static class EventEditorDiagnostics
    {
        public static List<EventEditorDiagnostic> Collect(ScriptableObject owner)
        {
            var result = new List<EventEditorDiagnostic>();
            if (owner is EventDefinition definition)
            {
                var identityErrors = definition.ValidateIdentity();
                foreach (string error in identityErrors) result.Add(Row(-1, -1, error));
                if (identityErrors.Count > 0) return result;
                if (definition.pages != null) for (int page = 0; page < definition.pages.Count; page++)
                {
                    foreach (string error in EventIdentityUpgrader.ValidateDefinitionPage(definition, page))
                        result.Add(Row(page, CommandLocation(error), "페이지 " + (page + 1) + ": " + error));
                    AddBranchDiagnostics(result, definition.pages[page]?.commands, page);
                }
            }
            else if (owner is CommonEventDefinition common)
            {
                foreach (string error in EventIdentityUpgrader.ValidateCommon(common)) result.Add(Row(-1, CommandLocation(error), error));
                AddBranchDiagnostics(result, common.commands, -1);
            }
            return result;
        }
        static EventEditorDiagnostic Row(int page, int command, string text) => new EventEditorDiagnostic { PageIndex = page, CommandIndex = command, Message = text };
        static int CommandLocation(string text)
        { var match = Regex.Match(text, @"Command (\d+)"); return match.Success && int.TryParse(match.Groups[1].Value, out int index) ? index - 1 : -1; }
        static void AddBranchDiagnostics(List<EventEditorDiagnostic> result, List<EventCommand> commands, int page)
        {
            if (commands == null) return;
            var labels = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < commands.Count; index++)
                if (commands[index]?.type == EventCommandType.Label)
                {
                    string value = commands[index].label?.Trim();
                    if (string.IsNullOrEmpty(value) || !labels.Add(value)) result.Add(Row(page, index, "명령 " + (index + 1) + ": 빈/중복 Label"));
                }
            for (int index = 0; index < commands.Count; index++)
            {
                var command = commands[index]; if (command == null) continue;
                if ((command.type == EventCommandType.Jump || command.type == EventCommandType.IfCondition) && !labels.Contains(command.targetLabel?.Trim() ?? ""))
                    result.Add(Row(page, index, "명령 " + (index + 1) + ": 연결되지 않은 분기 대상 " + command.targetLabel));
                if (command.type == EventCommandType.ShowChoices && command.choices != null) foreach (var choice in command.choices)
                    if (choice != null && !string.IsNullOrWhiteSpace(choice.targetLabel) && !labels.Contains(choice.targetLabel.Trim()))
                        result.Add(Row(page, index, "명령 " + (index + 1) + ": 연결되지 않은 선택지 " + choice.targetLabel));
            }
        }
        public static string Summary(EventCommand command)
        {
            if (command == null) return "빈 명령";
            string detail = command.type == EventCommandType.Label ? command.label :
                command.type == EventCommandType.Jump || command.type == EventCommandType.IfCondition ? "→ " + command.targetLabel :
                command.type == EventCommandType.CallCommon ? command.commonEvent?.displayName :
                command.type == EventCommandType.RequestForeground ? command.foregroundEvent?.displayName :
                command.type == EventCommandType.TransferPlayer ? command.map?.displayName + " " + command.cell :
                !string.IsNullOrEmpty(command.text) ? command.text : command.key;
            detail = (detail ?? "").Replace('\n', ' ').Replace('\r', ' ');
            if (detail.Length > 48) detail = detail.Substring(0, 48) + "…";
            return command.type + (string.IsNullOrEmpty(detail) ? "" : " · " + detail);
        }
        public static int Indent(IReadOnlyList<EventCommand> commands, int index)
        {
            int indentation = 0;
            for (int before = 0; before < index; before++)
            {
                var command = commands[before]; if (command?.type != EventCommandType.IfCondition || string.IsNullOrWhiteSpace(command.targetLabel)) continue;
                int target = -1;
                for (int scan = before + 1; scan < commands.Count; scan++) if (commands[scan]?.type == EventCommandType.Label && commands[scan].label == command.targetLabel) { target = scan; break; }
                if (target > index) indentation++;
            }
            return indentation;
        }
        public static Vector2Int BranchRange(IReadOnlyList<EventCommand> commands, int index)
        {
            if (commands == null || index < 0 || index >= commands.Count) throw new ArgumentOutOfRangeException(nameof(index));
            int first = index, last = index;
            bool changed;
            do
            {
                int previousFirst = first, previousLast = last;
                var targets = new HashSet<string>(StringComparer.Ordinal);
                for (int scan = first; scan <= last; scan++)
                {
                    var command = commands[scan]; if (command == null) continue;
                    if ((command.type == EventCommandType.Jump || command.type == EventCommandType.IfCondition) && !string.IsNullOrWhiteSpace(command.targetLabel)) targets.Add(command.targetLabel.Trim());
                    if (command.type == EventCommandType.ShowChoices && command.choices != null) foreach (var choice in command.choices)
                        if (choice != null && !string.IsNullOrWhiteSpace(choice.targetLabel)) targets.Add(choice.targetLabel.Trim());
                }
                foreach (string target in targets)
                    for (int location = 0; location < commands.Count; location++)
                    {
                        if (commands[location]?.type != EventCommandType.Label || commands[location].label?.Trim() != target) continue;
                        first = Math.Min(first, location); int end = location;
                        while (end + 1 < commands.Count && commands[end]?.type != EventCommandType.End && commands[end + 1]?.type != EventCommandType.Label) end++;
                        last = Math.Max(last, end); break;
                    }
                changed = previousFirst != first || previousLast != last;
            } while (changed);
            return new Vector2Int(first, last);
        }
    }
}
