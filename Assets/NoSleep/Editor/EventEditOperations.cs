using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class EventEditOperations
    {
        const string ClipboardFormat = "NoSleep.EventCommands";
        const int ClipboardVersion = 1, MaximumClipboardCommands = 10000, MaximumClipboardCharacters = 4 * 1024 * 1024;
        static readonly FieldInfo[] ReferenceFields = typeof(EventCommand).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)).ToArray();

        public static List<EventCommand> GetCommands(ScriptableObject owner, int pageIndex)
        {
            if (owner is EventDefinition definition)
            {
                RequirePage(definition, pageIndex);
                return definition.pages[pageIndex].commands ?? throw new ArgumentException("Page command list is missing.");
            }
            if (owner is CommonEventDefinition common && common != null && (pageIndex == -1 || pageIndex == 0))
                return common.commands ?? throw new ArgumentException("Common command list is missing.");
            throw new ArgumentException("Select an event page or a common event.", nameof(owner));
        }

        public static IReadOnlyList<int> InsertCommands(ScriptableObject owner, int pageIndex, int insertIndex,
            IReadOnlyList<EventCommand> source, bool allowExternalTargets = false)
        {
            RequireEditable();
            var current = GetCommands(owner, pageIndex);
            RequireBoundary(insertIndex, current.Count);
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copies = PrepareCopies(source, Labels(current), allowExternalTargets);
            if (copies.Count == 0) return Array.Empty<int>();
            var candidate = new List<EventCommand>(current); candidate.InsertRange(insertIndex, copies);
            ValidateStructure(candidate, true);
            Mutate(owner, "명령 붙여넣기/추가", () => SetCommands(owner, pageIndex, candidate));
            return Enumerable.Range(insertIndex, copies.Count).ToArray();
        }

        // destinationIndex is a boundary in the original list, before removal.
        public static IReadOnlyList<int> MoveCommands(ScriptableObject owner, int pageIndex, IReadOnlyList<int> indices, int destinationIndex)
        {
            RequireEditable();
            var current = GetCommands(owner, pageIndex); RequireBoundary(destinationIndex, current.Count);
            var selection = Selection(indices, current.Count);
            if (selection.Count == 0) return Array.Empty<int>();
            var selected = selection.Select(index => current[index]).ToList();
            var set = new HashSet<int>(selection);
            var candidate = current.Where((command, index) => !set.Contains(index)).ToList();
            int insertion = destinationIndex - selection.Count(index => index < destinationIndex);
            candidate.InsertRange(insertion, selected); ValidateStructure(candidate, true);
            if (!candidate.SequenceEqual(current)) Mutate(owner, "명령 순서 변경", () => SetCommands(owner, pageIndex, candidate));
            return Enumerable.Range(insertion, selected.Count).ToArray();
        }

        public static void RemoveCommands(ScriptableObject owner, int pageIndex, IReadOnlyList<int> indices)
        {
            RequireEditable();
            var current = GetCommands(owner, pageIndex); var selection = Selection(indices, current.Count);
            if (selection.Count == 0) return;
            var set = new HashSet<int>(selection);
            var candidate = current.Where((command, index) => !set.Contains(index)).ToList();
            ValidateStructure(candidate, true);
            Mutate(owner, "명령 삭제", () => SetCommands(owner, pageIndex, candidate));
        }

        public static int AddPage(EventDefinition owner)
        {
            RequireDefinition(owner);
            return InsertPages(owner, owner.pages.Count, new[] { new EventPage() })[0];
        }
        public static int DuplicatePage(EventDefinition owner, int pageIndex)
        {
            RequirePage(owner, pageIndex);
            var copy = Clone(owner.pages[pageIndex]); copy.name = (copy.name ?? "Page") + " (복제)";
            return InsertPages(owner, pageIndex + 1, new[] { copy })[0];
        }
        public static IReadOnlyList<int> InsertPages(EventDefinition owner, int insertIndex, IReadOnlyList<EventPage> source)
        {
            RequireEditable(); RequireDefinition(owner); RequireBoundary(insertIndex, owner.pages.Count);
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copies = new List<EventPage>();
            foreach (var page in source)
            {
                if (page == null) throw new ArgumentException("A source page is missing.");
                var copy = Clone(page); copy.pageId = Guid.NewGuid().ToString("N"); copy.legacyPageIndex = -1;
                copy.commands = PrepareCopies(page.commands ?? throw new ArgumentException("Source page commands are missing."), new HashSet<string>(StringComparer.Ordinal), false);
                copies.Add(copy);
            }
            if (copies.Count == 0) return Array.Empty<int>();
            var candidate = new List<EventPage>(owner.pages); candidate.InsertRange(insertIndex, copies);
            ValidatePages(candidate);
            Mutate(owner, "페이지 추가/복제", () => owner.pages = candidate);
            return Enumerable.Range(insertIndex, copies.Count).ToArray();
        }
        // Page movement uses its final index, so Up/Down can pass index +/- 1.
        public static void MovePage(EventDefinition owner, int pageIndex, int destinationIndex)
        {
            RequireEditable(); RequirePage(owner, pageIndex); RequirePage(owner, destinationIndex);
            if (pageIndex == destinationIndex) return;
            var candidate = new List<EventPage>(owner.pages); var page = candidate[pageIndex];
            candidate.RemoveAt(pageIndex); candidate.Insert(destinationIndex, page); ValidatePages(candidate);
            Mutate(owner, "페이지 순서 변경", () => owner.pages = candidate);
        }
        public static void RemovePage(EventDefinition owner, int pageIndex)
        {
            RequireEditable(); RequirePage(owner, pageIndex);
            var candidate = new List<EventPage>(owner.pages); candidate.RemoveAt(pageIndex); ValidatePages(candidate);
            Mutate(owner, "페이지 삭제", () => owner.pages = candidate);
        }

        public static string CopyCommands(ScriptableObject owner, int pageIndex, IReadOnlyList<int> indices)
        {
            var current = GetCommands(owner, pageIndex); var selection = Selection(indices, current.Count);
            if (selection.Count == 0) throw new ArgumentException("Select commands to copy.");
            if (selection.Count > MaximumClipboardCommands) throw new ArgumentException("Too many clipboard commands.");
            var envelope = new ClipboardEnvelope();
            foreach (int index in selection) envelope.commands.Add(Clone(current[index]));
            ValidateStructure(envelope.commands, false);
            for (int index = 0; index < envelope.commands.Count; index++)
                foreach (var field in ReferenceFields)
                {
                    var reference = field.GetValue(envelope.commands[index]) as UnityEngine.Object;
                    field.SetValue(envelope.commands[index], null);
                    if (reference == null) continue;
                    if (!EditorUtility.IsPersistent(reference)) throw new ArgumentException("Clipboard reference must be a saved asset: " + field.Name);
                    envelope.references.Add(new ClipboardReference { commandIndex = index, field = field.Name,
                        globalId = GlobalObjectId.GetGlobalObjectIdSlow(reference).ToString() });
                }
            string payload = JsonUtility.ToJson(envelope);
            if (payload.Length > MaximumClipboardCharacters) throw new ArgumentException("Clipboard payload is too large.");
            return payload;
        }

        public static IReadOnlyList<EventCommand> ReadClipboard(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload) || payload.Length > MaximumClipboardCharacters) throw new ArgumentException("Clipboard payload is missing or too large.");
            ClipboardEnvelope envelope;
            try { envelope = JsonUtility.FromJson<ClipboardEnvelope>(payload); }
            catch (Exception error) { throw new ArgumentException("Clipboard is not valid event JSON.", error); }
            if (envelope == null || envelope.format != ClipboardFormat || envelope.version != ClipboardVersion ||
                envelope.commands == null || envelope.commands.Count == 0 || envelope.commands.Count > MaximumClipboardCommands || envelope.references == null)
                throw new ArgumentException("Clipboard is not a supported No Sleep command block.");
            ValidateStructure(envelope.commands, false);
            // Do not trust embedded instance IDs. Resolve only persistent IDs in
            // the explicit reference table, which survives editor restarts.
            foreach (var command in envelope.commands) foreach (var field in ReferenceFields) field.SetValue(command, null);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in envelope.references)
            {
                if (record == null || record.commandIndex < 0 || record.commandIndex >= envelope.commands.Count ||
                    !seen.Add(record.commandIndex + ":" + record.field)) throw new ArgumentException("Clipboard reference record is invalid or duplicated.");
                var field = ReferenceFields.FirstOrDefault(candidate => candidate.Name == record.field);
                if (field == null || string.IsNullOrWhiteSpace(record.globalId) || !GlobalObjectId.TryParse(record.globalId, out var id)) throw new ArgumentException("Clipboard asset reference is invalid: " + record.field);
                var reference = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                if (reference == null || !EditorUtility.IsPersistent(reference) || !field.FieldType.IsInstanceOfType(reference))
                    throw new ArgumentException("Clipboard asset is missing or has another type: " + record.field);
                field.SetValue(envelope.commands[record.commandIndex], reference);
            }
            return envelope.commands;
        }

        internal static T Clone<T>(T value) => (T)CloneValue(value);
        static object CloneValue(object value)
        {
            if (value == null || value is UnityEngine.Object || value is string || value.GetType().IsValueType) return value;
            Type type = value.GetType();
            if (value is IList list)
            {
                var copy = (IList)Activator.CreateInstance(type); foreach (var item in list) copy.Add(CloneValue(item)); return copy;
            }
            var result = Activator.CreateInstance(type);
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)) field.SetValue(result, CloneValue(field.GetValue(value)));
            return result;
        }
        static List<EventCommand> PrepareCopies(IReadOnlyList<EventCommand> source, HashSet<string> occupied, bool allowExternalTargets)
        {
            ValidateStructure(source, false);
            var copies = source.Select(Clone).ToList();
            var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
            var used = new HashSet<string>(occupied, StringComparer.Ordinal);
            foreach (var command in copies)
            {
                command.commandId = Guid.NewGuid().ToString("N");
                if (command.type != EventCommandType.Label || string.IsNullOrWhiteSpace(command.label)) continue;
                string original = command.label.Trim(), candidate = original; int suffix = 1;
                while (used.Contains(candidate)) candidate = original + "_copy" + (suffix++ == 1 ? "" : (suffix - 1).ToString());
                mapping.Add(original, candidate); used.Add(candidate); command.label = candidate;
            }
            string Remap(string target)
            {
                if (string.IsNullOrWhiteSpace(target)) return target;
                target = target.Trim();
                if (mapping.TryGetValue(target, out string remapped)) return remapped;
                if (allowExternalTargets && occupied.Contains(target)) return target;
                throw new ArgumentException("The copied block targets an external label: " + target + ". Copy its label too or explicitly allow an existing destination label.");
            }
            foreach (var command in copies)
            {
                if (command.type == EventCommandType.Jump || command.type == EventCommandType.IfCondition) command.targetLabel = Remap(command.targetLabel);
                if (command.type == EventCommandType.ShowChoices && command.choices != null)
                    foreach (var choice in command.choices) choice.targetLabel = Remap(choice.targetLabel);
            }
            return copies;
        }
        static HashSet<string> Labels(IReadOnlyList<EventCommand> commands)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var command in commands)
                if (command != null && command.type == EventCommandType.Label && !string.IsNullOrWhiteSpace(command.label))
                    if (!result.Add(command.label.Trim())) throw new ArgumentException("Duplicate label: " + command.label.Trim());
            return result;
        }
        static void ValidateStructure(IReadOnlyList<EventCommand> commands, bool requireResolvedTargets)
        {
            if (commands == null) throw new ArgumentException("Command list is missing.");
            foreach (var command in commands)
            {
                if (command == null || !Enum.IsDefined(typeof(EventCommandType), command.type)) throw new ArgumentException("A command is missing or has an unknown type.");
                if (command.choices != null && command.choices.Any(choice => choice == null)) throw new ArgumentException("A choice record is missing.");
                if (command.arguments != null && command.arguments.Any(argument => argument == null)) throw new ArgumentException("An argument record is missing.");
                if (command.mutations != null && command.mutations.Any(mutation => mutation == null)) throw new ArgumentException("A grant effect record is missing.");
                ValidateEnums(command);
            }
            var labels = Labels(commands);
            if (!requireResolvedTargets) return;
            void RequireTarget(string target)
            {
                if (!string.IsNullOrWhiteSpace(target) && !labels.Contains(target.Trim())) throw new ArgumentException("Deleting or moving this block leaves a missing label: " + target.Trim());
            }
            foreach (var command in commands)
            {
                if (command.type == EventCommandType.Jump || command.type == EventCommandType.IfCondition) RequireTarget(command.targetLabel);
                if (command.type == EventCommandType.ShowChoices && command.choices != null) foreach (var choice in command.choices) RequireTarget(choice.targetLabel);
            }
        }
        static void ValidatePages(IReadOnlyList<EventPage> pages)
        {
            foreach (var page in pages)
            {
                if (page == null || !Enum.IsDefined(typeof(EventTrigger), page.trigger)) throw new ArgumentException("A page is missing or has an unknown trigger.");
                if (page.conditions != null && page.conditions.Any(condition => condition == null)) throw new ArgumentException("A page condition record is missing.");
                ValidateEnums(page);
                ValidateStructure(page.commands, true);
            }
        }
        static void ValidateEnums(object value)
        {
            if (value == null || value is UnityEngine.Object || value is string) return;
            Type type = value.GetType();
            if (type.IsEnum)
            { if (!Enum.IsDefined(type, value)) throw new ArgumentException("An event record has an unknown enum value: " + type.Name); return; }
            if (type.IsValueType) return;
            if (value is IList list) { foreach (var item in list) ValidateEnums(item); return; }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)) ValidateEnums(field.GetValue(value));
        }
        static List<int> Selection(IReadOnlyList<int> indices, int count)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            var result = indices.Distinct().OrderBy(index => index).ToList();
            if (result.Any(index => index < 0 || index >= count)) throw new ArgumentOutOfRangeException(nameof(indices));
            return result;
        }
        static void SetCommands(ScriptableObject owner, int pageIndex, List<EventCommand> commands)
        {
            if (owner is EventDefinition definition) definition.pages[pageIndex].commands = commands;
            else ((CommonEventDefinition)owner).commands = commands;
        }
        static void Mutate(ScriptableObject owner, string name, Action apply)
        {
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(name);
            Undo.RegisterCompleteObjectUndo(owner, name);
            // Upgrade the original order inside the same Undo transaction, before
            // insertion/removal can change the legacy index mapping.
            if (owner is EventDefinition definition) definition.EnsureStableIds();
            else ((CommonEventDefinition)owner).EnsureStableIds();
            apply(); EditorUtility.SetDirty(owner); Undo.CollapseUndoOperations(group);
        }
        static void RequireEditable()
        { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Event assets cannot be edited during Play mode."); }
        static void RequireDefinition(EventDefinition owner)
        { if (owner == null || owner.pages == null) throw new ArgumentException("Event pages are missing."); }
        static void RequirePage(EventDefinition owner, int pageIndex)
        {
            RequireDefinition(owner);
            if (pageIndex < 0 || pageIndex >= owner.pages.Count || owner.pages[pageIndex] == null) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }
        static void RequireBoundary(int index, int count)
        { if (index < 0 || index > count) throw new ArgumentOutOfRangeException(nameof(index)); }

        [Serializable] sealed class ClipboardEnvelope
        {
            public string format = ClipboardFormat;
            public int version = ClipboardVersion;
            public List<EventCommand> commands = new List<EventCommand>();
            public List<ClipboardReference> references = new List<ClipboardReference>();
        }
        [Serializable] sealed class ClipboardReference { public int commandIndex; public string field; public string globalId; }
    }
}
