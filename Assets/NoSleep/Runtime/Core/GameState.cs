using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoSleep
{
    [Serializable]
    public sealed class BoolEntry { public string key; public bool value; }
    [Serializable]
    public sealed class IntEntry { public string key; public int value; }

    [Serializable]
    public sealed class PlayerStats
    {
        public string name = "Player";
        public int maxHealth = 100;
        public int health = 100;
        public int maxSanity = 100;
        public int sanity = 100;
        public int gold;
        public int strength = 12;
    }

    [Serializable]
    public sealed class GameState
    {
        public PlayerStats player = new PlayerStats();
        public List<BoolEntry> switches = new List<BoolEntry>();
        public List<BoolEntry> selfSwitches = new List<BoolEntry>();
        public List<IntEntry> variables = new List<IntEntry>();
        public List<IntEntry> inventory = new List<IntEntry>();
        public SurvivalState survival = new SurvivalState();
        public JourneyState journey = new JourneyState();
        public PartyState party = new PartyState();
        public List<string> unlockedActions = new List<string> { "strike", "guard", "flee" };
        public List<string> committedTransactions = new List<string>();
        public List<EventCheckpointState> eventCheckpoints = new List<EventCheckpointState>();
        public List<NpcState> npcStates = new List<NpcState>();
        [NonSerialized] public Action Changed;

        public bool GetSwitch(string key) => ReadBool(switches, key);
        public bool GetSelfSwitch(string eventKey, string key) => ReadBool(selfSwitches, SelfKey(eventKey, key));
        public bool HasSelfSwitch(string eventKey, string key)
        {
            string id = SelfKey(eventKey, key);
            return selfSwitches != null && selfSwitches.Exists(entry => entry != null && entry.key == id);
        }
        public int GetVariable(string key) => ReadInt(variables, key);
        public int GetItemCount(string key) => ReadInt(inventory, key);
        public void SetSwitch(string key, bool value) { WriteBool(switches, Key(key), value); NotifyChanged(); }
        public void SetSelfSwitch(string eventKey, string key, bool value) { WriteBool(selfSwitches, SelfKey(eventKey, key), value); NotifyChanged(); }
        public void SetVariable(string key, int value) { WriteInt(variables, Key(key), value); NotifyChanged(); }
        public void AddVariable(string key, int value) => SetVariable(key, checked(GetVariable(key) + value));
        public void AddItem(string key, int amount)
        {
            WriteInt(inventory, Key(key), (int)Math.Max(0L, Math.Min(int.MaxValue, (long)GetItemCount(key) + amount)));
            NotifyChanged();
        }
        public void AddGold(int amount)
        {
            player.gold = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)player.gold + amount));
            NotifyChanged();
        }
        public void NotifyChanged() => Changed?.Invoke();
        public NpcState GetNpcState(string mapId, string placementId)
        {
            NpcState state = npcStates?.Find(value => value != null && value.mapId == mapId && value.placementId == placementId);
            return state?.Clone();
        }
        public void SetNpcState(NpcState state, bool notify = true)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            NpcState.ValidateRecords(new[] { state });
            npcStates ??= new List<NpcState>();
            NpcState.ValidateRecords(npcStates);
            int index = npcStates.FindIndex(value => value.mapId == state.mapId && value.placementId == state.placementId);
            if (index < 0) npcStates.Add(state.Clone()); else npcStates[index] = state.Clone();
            if (notify) NotifyChanged();
        }
        public bool TryCommitGrant(string transactionKey, IReadOnlyList<StateMutation> mutations, string eventKey = null)
        {
            string id = Key(transactionKey);
            if (committedTransactions != null && committedTransactions.Exists(value => value != null && value.Trim() == id)) return false;
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));
            // Validate and execute against a detached state. No live field or callback is
            // touched until every effect and the durable completion record are ready.
            var snapshot = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(this));
            snapshot.Normalize();
            foreach (StateMutation mutation in mutations)
            {
                if (mutation == null) throw new ArgumentException("A transaction effect is missing.", nameof(mutations));
                mutation.Apply(snapshot, eventKey);
            }
            snapshot.committedTransactions.Add(id);
            CopyDurableState(snapshot);
            NotifyChanged();
            return true;
        }
        public EventCheckpointState GetEventCheckpoint(string scope)
        {
            string id = Key(scope);
            EventCheckpointState entry = eventCheckpoints?.Find(value => value != null && value.scope == id);
            // Callers resume from a snapshot; writing a checkpoint goes through Set.
            return entry == null ? null : new EventCheckpointState { scope = entry.scope,
                checkpointId = entry.checkpointId, dueElapsedMinutes = entry.dueElapsedMinutes };
        }
        public void SetEventCheckpoint(string scope, string checkpointId, int dueElapsedMinutes)
        {
            string id = Key(scope), checkpoint = Key(checkpointId);
            if (dueElapsedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(dueElapsedMinutes));
            eventCheckpoints ??= new List<EventCheckpointState>();
            EventCheckpointState existing = eventCheckpoints.Find(value => value != null && value.scope == id);
            if (existing != null && existing.checkpointId == checkpoint && existing.dueElapsedMinutes == dueElapsedMinutes) return;
            if (existing != null) eventCheckpoints.Remove(existing);
            eventCheckpoints.Add(new EventCheckpointState { scope = id, checkpointId = checkpoint, dueElapsedMinutes = dueElapsedMinutes });
            NotifyChanged();
        }
        public void ClearEventCheckpoint(string scope)
        {
            string id = Key(scope);
            if (eventCheckpoints != null && eventCheckpoints.RemoveAll(value => value != null && value.scope == id) > 0) NotifyChanged();
        }
        public void Normalize()
        {
            player ??= new PlayerStats(); switches ??= new List<BoolEntry>();
            selfSwitches ??= new List<BoolEntry>(); variables ??= new List<IntEntry>(); inventory ??= new List<IntEntry>();
            survival ??= new SurvivalState(); journey ??= new JourneyState(); party ??= new PartyState();
            unlockedActions ??= new List<string> { "strike", "guard", "flee" };
            survival.Normalize(); journey.Normalize(); party.Normalize();
            unlockedActions = PartyState.CleanIds(unlockedActions);
            NormalizeEventProgress();
            npcStates ??= new List<NpcState>();
            NpcState.ValidateRecords(npcStates);
            var npcCopies = new List<NpcState>(); foreach (NpcState npc in npcStates) npcCopies.Add(npc.Clone()); npcStates = npcCopies;
            player.maxHealth = Math.Max(1, player.maxHealth); player.maxSanity = Math.Max(1, player.maxSanity);
            player.health = Mathf.Clamp(player.health, 0, player.maxHealth);
            player.sanity = Mathf.Clamp(player.sanity, 0, player.maxSanity);
            player.gold = Math.Max(0, player.gold);
            player.strength = Math.Max(1, player.strength);
            foreach (var entry in inventory) if (entry != null) entry.value = Math.Max(0, entry.value);
        }
        void NormalizeEventProgress()
        {
            committedTransactions ??= new List<string>();
            var transactions = new HashSet<string>(StringComparer.Ordinal);
            var cleaned = new List<string>();
            foreach (string transaction in committedTransactions)
            {
                if (string.IsNullOrWhiteSpace(transaction)) throw new InvalidDataException("A committed transaction ID is missing.");
                if (transactions.Add(transaction.Trim())) cleaned.Add(transaction.Trim());
            }
            committedTransactions = cleaned;
            eventCheckpoints ??= new List<EventCheckpointState>();
            var checkpoints = new Dictionary<string, EventCheckpointState>(StringComparer.Ordinal);
            var result = new List<EventCheckpointState>();
            foreach (EventCheckpointState checkpoint in eventCheckpoints)
            {
                if (checkpoint == null) continue;
                if (string.IsNullOrWhiteSpace(checkpoint.scope) || string.IsNullOrWhiteSpace(checkpoint.checkpointId) || checkpoint.dueElapsedMinutes < 0)
                    throw new InvalidDataException("An event checkpoint is invalid.");
                string scope = checkpoint.scope.Trim(), id = checkpoint.checkpointId.Trim();
                if (checkpoints.TryGetValue(scope, out EventCheckpointState previous))
                {
                    if (previous.checkpointId != id || previous.dueElapsedMinutes != checkpoint.dueElapsedMinutes)
                        throw new InvalidDataException("Conflicting event checkpoints: " + scope);
                    continue;
                }
                var value = new EventCheckpointState { scope = scope, checkpointId = id, dueElapsedMinutes = checkpoint.dueElapsedMinutes };
                checkpoints.Add(scope, value); result.Add(value);
            }
            eventCheckpoints = result;
        }
        internal void ValidateEventProgress()
        {
            // NormalizeEventProgress replaces its collections and does not mutate
            // individual records, so validating these references leaves the live state intact.
            var candidate = new GameState { committedTransactions = committedTransactions, eventCheckpoints = eventCheckpoints };
            candidate.NormalizeEventProgress();
        }
        void CopyDurableState(GameState source)
        {
            player = source.player; switches = source.switches; selfSwitches = source.selfSwitches;
            variables = source.variables; inventory = source.inventory; survival = source.survival;
            journey = source.journey; party = source.party; unlockedActions = source.unlockedActions;
            committedTransactions = source.committedTransactions; eventCheckpoints = source.eventCheckpoints;
            npcStates = source.npcStates;
        }
        static string Key(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("State key cannot be empty.", nameof(key));
            return key.Trim();
        }
        // Length prefix prevents collisions between map/event IDs and switch names.
        static string SelfKey(string eventKey, string key) => Key(eventKey).Length + ":" + Key(eventKey) + ":" + Key(key);
        static bool ReadBool(List<BoolEntry> list, string key)
        {
            if (list == null || string.IsNullOrWhiteSpace(key)) return false;
            var entry = list.Find(x => x != null && x.key == key.Trim()); return entry != null && entry.value;
        }
        static int ReadInt(List<IntEntry> list, string key)
        {
            if (list == null || string.IsNullOrWhiteSpace(key)) return 0;
            var entry = list.Find(x => x != null && x.key == key.Trim()); return entry?.value ?? 0;
        }
        static void WriteBool(List<BoolEntry> list, string key, bool value)
        {
            var entry = list.Find(x => x != null && x.key == key);
            if (entry == null) { entry = new BoolEntry { key = key }; list.Add(entry); } entry.value = value;
        }
        static void WriteInt(List<IntEntry> list, string key, int value)
        {
            var entry = list.Find(x => x != null && x.key == key);
            if (entry == null) { entry = new IntEntry { key = key }; list.Add(entry); } entry.value = value;
        }
    }
}
