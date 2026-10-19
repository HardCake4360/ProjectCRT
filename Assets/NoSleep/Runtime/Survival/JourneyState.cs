using System;
using System.Collections.Generic;

namespace NoSleep
{
    [Serializable]
    public sealed class JournalRecord
    {
        public string id;
        public string title;
        public string text;
    }

    [Serializable]
    public sealed class MemoryRecord
    {
        public string id;
        public string title;
        public string text;
    }

    [Serializable]
    public sealed class JourneyState
    {
        public string objectiveId = "find_stimulants";
        public string objectiveTitle = "병원에서 각성제를 확보한다";
        public int stage;
        public List<string> clueIds = new List<string>();
        public List<JournalRecord> journal = new List<JournalRecord>();
        public List<MemoryRecord> memories = new List<MemoryRecord>();
        public bool reachedDestination;

        public void Normalize()
        {
            objectiveId = objectiveId?.Trim() ?? string.Empty;
            objectiveTitle ??= string.Empty;
            stage = Math.Max(0, stage);
            clueIds = PartyState.CleanIds(clueIds);
            journal ??= new List<JournalRecord>();
            memories ??= new List<MemoryRecord>();
            var journalIds = new HashSet<string>(StringComparer.Ordinal);
            journal.RemoveAll(record => record == null || string.IsNullOrWhiteSpace(record.id) || !journalIds.Add(record.id.Trim()));
            foreach (var record in journal)
            {
                record.id = record.id.Trim(); record.title ??= string.Empty; record.text ??= string.Empty;
            }
            var memoryIds = new HashSet<string>(StringComparer.Ordinal);
            memories.RemoveAll(record => record == null || string.IsNullOrWhiteSpace(record.id) || !memoryIds.Add(record.id.Trim()));
            foreach (var record in memories)
            {
                record.id = record.id.Trim(); record.title ??= string.Empty; record.text ??= string.Empty;
            }
        }
    }
}
