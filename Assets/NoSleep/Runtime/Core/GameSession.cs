using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoSleep
{
    public enum SaveRequestStatus { None, Pending, Completed, Failed, Cancelled }

    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public GameState State { get; private set; } = new GameState();
        public TileMapRuntime Map { get; private set; }
        public PlayerController Player { get; private set; }
        public EventRunner Events { get; private set; }
        public EventScheduler Scheduler { get; private set; }
        public NpcMovementService Npcs { get; private set; }
        public int ExecutionEpoch { get; private set; }
        public GameUI UI { get; private set; }
        public CombatRunner Combat { get; private set; }
        public SurvivalSettings SurvivalSettings { get; private set; }
        public ItemEffectDefinition Stimulant { get; private set; }
        public bool HorrorEnabled => SurvivalSettings != null;
        public bool IsGameplayBusy => (Events != null && Events.IsRunning) || (Combat != null && Combat.IsRunning);
        public bool IsInputBlocked => HasPendingSave || IsGameplayBusy || (Scheduler != null && Scheduler.Paused) || (UI != null && UI.IsModalOpen) || State.player.health <= 0;
        public bool IsReady => Map != null && Map.Map != null && Player != null && UI != null;
        public SaveSlotRepository SaveSlots { get; private set; }
        public bool HasPendingSave => pendingSaveSlot >= 0;
        public int PendingSaveSlot => pendingSaveSlot;
        public SaveRequestStatus SaveStatus { get; private set; }
        public event Action<int, bool> SaveCompleted;
        int pendingSaveSlot = -1;
        readonly List<MapDefinition> catalog = new List<MapDefinition>();
        public IReadOnlyList<MapDefinition> Maps => catalog;
        public string InputBlockReason => !IsReady ? "Session is not ready" : State.player.health <= 0 ? "Player defeated" :
            HasPendingSave ? "Saving at an NPC cell boundary" :
            Scheduler != null && Scheduler.Paused ? "Debugger paused" : Events != null && Events.IsRunning ? "Foreground event" :
            Combat != null && Combat.IsRunning ? "Encounter" : UI.IsModalOpen ? "UI modal" : "";
        public string SaveBlockReason => !IsReady ? "Session is not ready" : IsGameplayBusy || UI.IsDialogueOpen ? "Foreground event or encounter" :
            State.player.health <= 0 ? "Player defeated" : UI.IsTitleOpen ? "Title screen" :
            HasPendingSave ? "Save request already pending" :
            Scheduler != null && !Scheduler.IsAtSafeBoundary ? "Event state mutation in progress" :
            Scheduler != null && Scheduler.QueuedCount > 0 ? "Pending foreground requests" : "";
        public string ExecutionStatus => Scheduler?.Status ?? "Scheduler unavailable";
        readonly List<CompanionDefinition> companionCatalog = new List<CompanionDefinition>();
        MapDefinition startingMap;
        string startingName = "잠들지 못한 사람";
        float minuteAccumulator;
        public event Action StateReplaced;

        void Awake()
        {
            if (Instance != null && Instance != this) { Debug.LogError("Only one GameSession is allowed.", this); enabled = false; return; }
            Instance = this;
            ConfigureSaveDirectory(Path.Combine(Application.persistentDataPath, "NoSleep", "Saves"));
        }
        public void Configure(TileMapRuntime map, PlayerController player, EventRunner events, GameUI ui, IEnumerable<MapDefinition> maps)
        {
            Map = map; Player = player; Events = events; UI = ui;
            catalog.Clear(); if (maps != null) foreach (var entry in maps) if (entry != null && !catalog.Contains(entry)) catalog.Add(entry);
            Map.Configure(this); Player.Configure(this); UI.Configure(this); Events.Configure(this, UI);
            Combat = GetComponent<CombatRunner>();
            if (Combat == null) Combat = gameObject.AddComponent<CombatRunner>();
            Combat.Configure(this, UI);
            Scheduler = GetComponent<EventScheduler>();
            if (Scheduler == null) Scheduler = gameObject.AddComponent<EventScheduler>();
            Scheduler.Configure(this);
            Npcs = GetComponent<NpcMovementService>();
            if (Npcs == null) Npcs = gameObject.AddComponent<NpcMovementService>();
            Npcs.Configure(this);
        }
        public void ConfigureSaveDirectory(string directory) { SaveSlots = new SaveSlotRepository(directory); }
        public void ConfigureHorror(SurvivalSettings settings, ItemEffectDefinition stimulant)
        {
            SurvivalSettings = settings; Stimulant = stimulant;
            if (settings != null) SurvivalRules.Initialize(State, settings);
            minuteAccumulator = 0;
        }
        public void ConfigureNewGame(MapDefinition map, string playerName)
        { startingMap = map; startingName = playerName; }
        public void ConfigureCompanions(IEnumerable<CompanionDefinition> companions)
        { companionCatalog.Clear(); if (companions != null) foreach (var companion in companions) if (companion != null) companionCatalog.Add(companion); }
        public CompanionDefinition FindCompanion(string id) => companionCatalog.Find(x => x.companionId == id);
        public string CompanionName(string id) => FindCompanion(id)?.displayName ?? id;
        public bool CanLoadMap(string id) => catalog.Exists(x => x != null && x.mapId == id);
        public bool UseStimulant()
        {
            bool used = !IsGameplayBusy && HorrorEnabled && SurvivalRules.UseItem(State, Stimulant, SurvivalSettings);
            ShowNotice(used ? "각성제를 사용했다. 잠시 눈이 맑아진다." : "소지품과 남은 체력을 확인하세요."); return used;
        }
        public bool NewGame()
        {
            if (!IsReady || IsGameplayBusy || startingMap == null || (Scheduler != null && !Scheduler.IsAtSafeBoundary)) return false;
            try
            {
                var candidate = new GameState(); candidate.player.name = startingName;
                if (HorrorEnabled) SurvivalRules.Initialize(candidate, SurvivalSettings);
                if (!startingMap.IsWalkable(startingMap.spawnCell)) throw new InvalidDataException("New-game spawn is not walkable.");
                var maps = CatalogWith(startingMap);
                Vector3 local = PrepareDestination(candidate, startingMap, startingMap.CellToWorld(startingMap.spawnCell), maps);
                if (!CommitMap(candidate, startingMap, Map.transform.TransformPoint(local), 0, true)) return false;
                minuteAccumulator = 0; UI.CloseMenus(); ShowNotice("새 여정을 시작합니다."); return true;
            }
            catch (Exception ex) when (IsSaveFailure(ex))
            { Debug.LogWarning("New game failed: " + ex.Message, this); ShowNotice("새 게임 실패: " + ex.Message); return false; }
        }
        void Update()
        {
            if (!IsReady || !HorrorEnabled || IsInputBlocked || SurvivalSettings.timeMode != TimeMode.Realtime) return;
            float interval = SurvivalSettings.realSecondsPerGameMinute;
            if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0) return;
            minuteAccumulator += Time.deltaTime;
            int minutes = (int)Math.Min(int.MaxValue, Math.Floor((double)minuteAccumulator / interval));
            if (minutes > 0) { minuteAccumulator = (float)(minuteAccumulator - minutes * (double)interval); SurvivalRules.AdvanceTime(State, minutes, SurvivalSettings); }
        }
        public bool TransferPlayer(MapDefinition destination, Vector2Int cell)
        {
            if (destination == null || !destination.IsWalkable(cell) || Map == null || Player == null)
            { ShowNotice("이동할 수 없는 목적지입니다."); return false; }
            try
            {
                GameState candidate = CloneState(State); Npcs?.SnapshotInto(candidate);
                var maps = CatalogWith(destination);
                Vector3 local = PrepareDestination(candidate, destination, destination.CellToWorld(cell), maps);
                return CommitMap(candidate, destination, Map.transform.TransformPoint(local), Player.transform.eulerAngles.y, false);
            }
            catch (Exception ex) when (IsSaveFailure(ex))
            { ShowNotice("맵 이동 실패: " + ex.Message); return false; }
        }
        public void ShowNotice(string text) { if (UI != null) UI.Notice(text); else Debug.Log(text, this); }
        public string SavePath(int slot = 0)
        {
            return SaveSlots.GetPath(slot);
        }
        public bool Save(int slot = 0)
        {
            if (HasPendingSave && slot == pendingSaveSlot) return true;
            if (!string.IsNullOrEmpty(SaveBlockReason))
            {
                ShowNotice(Scheduler != null && Scheduler.QueuedCount > 0 ?
                    "메뉴를 닫고 예약된 대사를 확인한 뒤 저장하세요." : "플레이 중 대사와 전투가 끝난 뒤 저장할 수 있습니다.");
                return false;
            }
            try
            {
                SaveSlots.GetPath(slot); // Reject an invalid slot before freezing movement.
                if (Npcs != null && Npcs.HasActiveSegments)
                {
                    pendingSaveSlot = slot; SaveStatus = SaveRequestStatus.Pending;
                    Npcs.BeginSaveDrain(); ShowNotice("현재 이동 칸을 마친 뒤 저장합니다. (슬롯 " + (slot + 1) + ")"); return true;
                }
                return WriteSave(slot);
            }
            catch (Exception ex) when (IsSaveFailure(ex))
            { SaveStatus = SaveRequestStatus.Failed; Debug.LogWarning("Save failed: " + ex.Message, this); ShowNotice("저장 실패: " + ex.Message); PublishSaveCompleted(slot, false); return false; }
        }

        void LateUpdate()
        {
            if (!HasPendingSave || (Npcs != null && !Npcs.IsAtCellBoundary)) return;
            if (!IsReady || IsGameplayBusy || UI.IsDialogueOpen || State.player.health <= 0 || UI.IsTitleOpen ||
                (Scheduler != null && (!Scheduler.IsAtSafeBoundary || Scheduler.QueuedCount > 0)))
            { CancelPendingSave(true); return; }
            int slot = pendingSaveSlot; pendingSaveSlot = -1;
            try { WriteSave(slot); }
            finally { Npcs?.EndSaveDrain(); }
        }

        bool WriteSave(int slot)
        {
            try
            {
                // Runtime interpolation never enters the file. SnapshotInto writes
                // only the independent candidate, at the boundary reached by the drain.
                var snapshot = SaveService.Deserialize(JsonUtility.ToJson(new SaveData { state = State, mapId = Map.Map.mapId, mapDisplayName = Map.Map.displayName,
                    objectiveTitle = HorrorEnabled ? State.journey.objectiveTitle : "", position = Player.transform.position, yaw = Player.transform.eulerAngles.y }));
                Npcs?.SnapshotInto(snapshot.state);
                EventSaveMigration.Migrate(snapshot.state, catalog); EventSaveMigration.ValidateCheckpoints(snapshot.state, catalog);
                NpcSaveValidation.Validate(snapshot.state, catalog, SurvivalSettings);
                var resolved = NpcSaveValidation.ResolveMapStates(snapshot.state, Map.Map, SurvivalSettings);
                Vector3 local = Map.transform.InverseTransformPoint(snapshot.position);
                if (!NpcSaveValidation.CanPlayerOccupy(snapshot.state, Map.Map, local, LocalRadius, resolved))
                    throw new InvalidDataException("Player and NPC snapshot cannot occupy the same cell.");
                SaveSlots.Write(slot, snapshot); SaveStatus = SaveRequestStatus.Completed;
                ShowNotice("저장했습니다. (슬롯 " + (slot + 1) + ")"); PublishSaveCompleted(slot, true); return true;
            }
            catch (Exception ex) when (IsSaveFailure(ex))
            { SaveStatus = SaveRequestStatus.Failed; Debug.LogWarning("Save failed: " + ex.Message, this); ShowNotice("저장 실패: " + ex.Message); PublishSaveCompleted(slot, false); return false; }
        }

        public void CancelPendingSave(bool notify = false)
        {
            if (!HasPendingSave) return;
            int slot = pendingSaveSlot; pendingSaveSlot = -1; SaveStatus = SaveRequestStatus.Cancelled;
            Npcs?.EndSaveDrain();
            if (notify) ShowNotice("맵/게임 상태 변경으로 예약 저장을 취소했습니다.");
            PublishSaveCompleted(slot, false);
        }
        public bool Load(int slot = 0, bool backup = false)
        {
            if (!IsReady || IsGameplayBusy || UI.IsDialogueOpen || (Scheduler != null && !Scheduler.IsAtSafeBoundary))
            { ShowNotice("이벤트 종료 후 불러올 수 있습니다."); return false; }
            try
            {
                var data = SaveSlots.Read(slot, backup);
                var destination = catalog.Find(x => x != null && x.mapId == data.mapId);
                if (destination == null) throw new InvalidDataException("Unknown map: " + data.mapId);
                var cell = destination.WorldToCell(Map.transform.InverseTransformPoint(data.position));
                if (!destination.IsWalkable(cell)) throw new InvalidDataException("Saved position is no longer walkable.");
                EventSaveMigration.Migrate(data.state, catalog);
                EventSaveMigration.ValidateCheckpoints(data.state, catalog);
                if (HorrorEnabled) SurvivalRules.Initialize(data.state, SurvivalSettings);
                Vector3 local = PrepareDestination(data.state, destination, Map.transform.InverseTransformPoint(data.position), catalog);
                if (!CommitMap(data.state, destination, Map.transform.TransformPoint(local), data.yaw, true))
                    throw new InvalidDataException("Saved map could not be committed.");
                minuteAccumulator = 0; UI.CloseMenus();
                if (State.player.health <= 0) UI.ShowGameOver();
                ShowNotice(backup ? "이전 저장 백업을 불러왔습니다." : "저장을 불러왔습니다."); return true;
            }
            catch (Exception ex) when (IsSaveFailure(ex))
            {
                Debug.LogWarning("Load failed: " + ex.Message, this); ShowNotice("불러오기 실패: " + ex.Message); return false;
            }
        }
        static GameState CloneState(GameState source)
        {
            GameState candidate = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(source));
            candidate.Normalize(); return candidate;
        }

        List<MapDefinition> CatalogWith(MapDefinition destination)
        {
            var maps = new List<MapDefinition>(catalog);
            if (destination != null && !maps.Contains(destination)) maps.Add(destination);
            return maps;
        }

        float LocalRadius
        {
            get
            {
                float scale = Mathf.Min(Mathf.Abs(Map.transform.lossyScale.x), Mathf.Abs(Map.transform.lossyScale.z));
                return Player.Radius / Mathf.Max(0.0001f, scale);
            }
        }

        Vector3 PrepareDestination(GameState candidate, MapDefinition destination, Vector3 requestedLocal, IEnumerable<MapDefinition> maps)
        {
            NpcSaveValidation.Validate(candidate, maps, SurvivalSettings);
            var resolved = NpcSaveValidation.ResolveMapStates(candidate, destination, SurvivalSettings);
            foreach (NpcState npc in resolved) candidate.SetNpcState(npc, false);
            if (!NpcSaveValidation.TryFindPlayerPosition(candidate, destination, requestedLocal, LocalRadius, resolved, out Vector3 local))
                throw new InvalidDataException("Destination has no safe player cell.");
            return local;
        }

        bool CommitMap(GameState candidate, MapDefinition destination, Vector3 position, float yaw, bool replaceState)
        {
            GameState previousState = State;
            MapDefinition previousMap = Map.Map;
            Vector3 previousPosition = Player.transform.position;
            float previousYaw = Player.transform.eulerAngles.y;
            int previousEpoch = ExecutionEpoch;
            bool previousSchedulerPaused = Scheduler?.Paused == true;
            float previousAccumulator = minuteAccumulator;
            var previousNpcStates = new List<NpcState>();
            foreach (NpcState npc in State.npcStates) previousNpcStates.Add(npc.Clone());
            object npcRuntime = Npcs?.CaptureRuntime();
            object pendingRequests = Scheduler?.CapturePendingRequests();
            bool swapped = false;
            try
            {
                Npcs?.OnMapChanging();
                if (replaceState) { State = candidate; ExecutionEpoch++; swapped = true; }
                else
                {
                    // A transfer is still inside the foreground caller's program.
                    // Keep its captured GameState object and merge only NPC checkpoints.
                    State.npcStates = candidate.npcStates;
                }
                Map.Load(destination); // Also binds the destination's NPC actors.
                Player.Warp(position, yaw);
                if (replaceState)
                {
                    Scheduler?.CancelAll();
                }
                else Scheduler?.OnMapChanging();
                // Map construction registers its new owners before the successful
                // transition retires the old generation. Rebind them immediately
                // so requests made in this frame retain their source-page guard.
                foreach (EventObject owner in Map.EventObjects) Scheduler?.Observe(owner);
                if (replaceState) StateReplaced?.Invoke();
                if (!catalog.Contains(destination)) catalog.Add(destination);
                CancelPendingSave();
                var camera = GetComponentInChildren<QuarterViewCamera>(); if (camera != null) camera.Snap();
                return true;
            }
            catch
            {
                State = previousState; State.npcStates = previousNpcStates; ExecutionEpoch = previousEpoch;
                minuteAccumulator = previousAccumulator;
                Map.Load(previousMap); Player.Warp(previousPosition, previousYaw);
                Npcs?.RestoreRuntime(npcRuntime);
                if (HasPendingSave) Npcs?.BeginSaveDrain();
                Scheduler?.RestorePendingRequests(pendingRequests);
                Scheduler?.SetPaused(previousSchedulerPaused);
                if (swapped) StateReplaced?.Invoke();
                throw;
            }
        }

        static bool IsSaveFailure(Exception error) => error is IOException || error is InvalidDataException ||
            error is UnauthorizedAccessException || error is System.Security.SecurityException ||
            error is ArgumentException || error is InvalidOperationException;

        void PublishSaveCompleted(int slot, bool success)
        {
            if (SaveCompleted == null) return;
            foreach (Action<int, bool> listener in SaveCompleted.GetInvocationList())
                try { listener(slot, success); }
                catch (Exception error) { Debug.LogWarning("Save completion listener failed: " + error.Message, this); }
        }

        void OnEnable()
        {
            if (Npcs != null && Map != null && Map.Map != null) Npcs.BindMap(Map, Map.NpcActors);
        }
        void OnDisable()
        {
            CancelPendingSave(); Npcs?.CancelAll();
        }
        void OnDestroy() { pendingSaveSlot = -1; if (Instance == this) Instance = null; }
    }
}

