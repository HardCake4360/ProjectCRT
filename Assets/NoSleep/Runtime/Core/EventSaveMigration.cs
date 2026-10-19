using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NoSleep
{
    public static class EventSaveMigration
    {
        const string LegacyCompletionPrefix = "__nosleep_completed_page_";

        public static void ValidateCheckpoints(GameState state, IEnumerable<MapDefinition> maps)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.ValidateEventProgress();
            if (state.eventCheckpoints == null || state.eventCheckpoints.Count == 0) return;
            var scopes = new Dictionary<string, (EventDefinition definition, EventPage page)>(StringComparer.Ordinal);
            if (maps != null)
                foreach (MapDefinition map in maps)
                {
                    if (map == null || map.events == null) continue;
                    foreach (MapEventPlacement placement in map.events)
                    {
                        if (placement?.definition?.pages == null) continue;
                        EventDefinition definition = placement.definition;
                        string eventKey = (map.mapId + "/" + placement.id).Trim();
                        foreach (EventPage page in definition.pages)
                        {
                            if (page == null) continue;
                            string scope = eventKey + "/" + definition.definitionId + "/" + page.pageId;
                            if (scopes.ContainsKey(scope)) throw new InvalidDataException("Ambiguous checkpoint scope: " + scope);
                            scopes.Add(scope, (definition, page));
                        }
                    }
                }
            foreach (EventCheckpointState checkpoint in state.eventCheckpoints)
            {
                if (checkpoint == null) continue;
                string scope = checkpoint.scope.Trim();
                if (!scopes.TryGetValue(scope, out var content))
                    throw new InvalidDataException("Unknown event checkpoint scope: " + scope);
                if (!content.definition.ValidateIdentity(out string error))
                    throw new InvalidDataException("Invalid checkpoint content identity: " + scope + " / " + error);
                if (content.page.trigger != EventTrigger.Parallel || content.page.commands == null)
                    throw new InvalidDataException("Checkpoint does not belong to a root parallel page: " + scope);
                EventCommand command = content.page.commands.Find(value => value != null && value.commandId == checkpoint.checkpointId.Trim());
                if (command == null || (command.type != EventCommandType.Checkpoint && command.type != EventCommandType.WaitGameMinutes))
                    throw new InvalidDataException("Checkpoint resume command has been removed or changed: " + scope + " / " + checkpoint.checkpointId);
            }
        }

        /// <summary>Converts only completed legacy pages using their captured original indices.</summary>
        public static void Migrate(GameState state, IEnumerable<MapDefinition> maps)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.selfSwitches == null || state.selfSwitches.Count == 0) return;
            var completed = new List<(string encodedKey, string eventKey, int originalIndex)>();
            foreach (BoolEntry entry in state.selfSwitches)
            {
                if (entry == null || !entry.value || string.IsNullOrWhiteSpace(entry.key)) continue;
                if (!TryDecodeSelfKey(entry.key, out string eventKey, out string switchKey))
                {
                    if (entry.key.Contains(":" + LegacyCompletionPrefix))
                        throw new InvalidDataException("Malformed legacy completion key: " + entry.key);
                    continue;
                }
                if (!switchKey.StartsWith(LegacyCompletionPrefix, StringComparison.Ordinal)) continue;
                if (!int.TryParse(switchKey.Substring(LegacyCompletionPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int originalIndex))
                    throw new InvalidDataException("Malformed legacy page index: " + switchKey);
                completed.Add((entry.key, eventKey, originalIndex));
            }
            if (completed.Count == 0) return;

            var placements = new Dictionary<string, EventDefinition>(StringComparer.Ordinal);
            if (maps != null)
                foreach (MapDefinition map in maps)
                {
                    if (map == null || map.events == null) continue;
                    foreach (MapEventPlacement placement in map.events)
                    {
                        if (placement == null || placement.definition == null) continue;
                        string key = (map.mapId + "/" + placement.id).Trim();
                        if (placements.ContainsKey(key)) throw new InvalidDataException("Ambiguous event placement: " + key);
                        placements.Add(key, placement.definition);
                    }
                }

            // Resolve every old reference before replacing the list. A migration error
            // must leave the candidate unchanged and cannot partially grant completion.
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in completed)
            {
                if (!placements.TryGetValue(entry.eventKey, out EventDefinition definition))
                    throw new InvalidDataException("Unknown completed event placement: " + entry.eventKey);
                if (string.IsNullOrWhiteSpace(definition.definitionId) || definition.pages == null)
                    throw new InvalidDataException("Event identity migration has not been prepared: " + entry.eventKey);
                EventPage original = null;
                foreach (EventPage page in definition.pages)
                {
                    if (page == null || page.legacyPageIndex != entry.originalIndex) continue;
                    if (original != null) throw new InvalidDataException("Ambiguous legacy page index: " + entry.eventKey);
                    original = page;
                }
                if (original == null || string.IsNullOrWhiteSpace(original.pageId))
                    throw new InvalidDataException("No captured page mapping for " + entry.eventKey + " / " + entry.originalIndex);
                replacements[entry.encodedKey] = EncodeSelfKey(entry.eventKey, definition.StableCompletionKey(original.pageId));
            }

            var result = new List<BoolEntry>();
            var existing = new Dictionary<string, BoolEntry>(StringComparer.Ordinal);
            foreach (BoolEntry entry in state.selfSwitches)
            {
                if (entry != null && entry.key != null && replacements.ContainsKey(entry.key)) continue;
                if (entry == null) { result.Add(null); continue; }
                var copy = new BoolEntry { key = entry.key, value = entry.value };
                result.Add(copy);
                if (copy.key != null && !existing.ContainsKey(copy.key)) existing.Add(copy.key, copy);
            }
            foreach (string stableKey in replacements.Values)
            {
                // A present stable false is an intentional reset and takes precedence
                // over a stale legacy true; it must not be resurrected during saving.
                if (existing.ContainsKey(stableKey)) continue;
                var value = new BoolEntry { key = stableKey, value = true };
                existing.Add(stableKey, value); result.Add(value);
            }
            state.selfSwitches = result;
        }

        static string EncodeSelfKey(string eventKey, string switchKey)
        {
            string scope = eventKey.Trim();
            return scope.Length + ":" + scope + ":" + switchKey.Trim();
        }
        static bool TryDecodeSelfKey(string encoded, out string eventKey, out string switchKey)
        {
            eventKey = switchKey = null;
            int colon = encoded.IndexOf(':');
            if (colon < 1 || !int.TryParse(encoded.Substring(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out int length) || length < 1) return false;
            int start = colon + 1;
            long end = (long)start + length;
            if (end >= encoded.Length || encoded[(int)end] != ':') return false;
            eventKey = encoded.Substring(start, length);
            switchKey = encoded.Substring((int)end + 1);
            return switchKey.Length > 0;
        }
    }
}
