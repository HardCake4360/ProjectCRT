using System;
using System.Collections;
using System.IO;
using ProjectCRT.Cutscenes;
using TMPro;
using UnityEngine;

namespace ProjectCRT.Cases
{
    /// <summary>Authoritative arrival milestone, interaction permissions, and a single case save slot.</summary>
    public sealed class ShangrilaCaseManager : MonoBehaviour
    {
        public StreetWalkContext walking;
        public CutsceneDirector director;
        public CutsceneSequence introduction;
        public InvestigationItemDatabase itemDatabase;
        public TMP_Text objectiveText;
        public TMP_Text saveText;
        public PlayerControler Player => walking != null ? walking.player : null;
        public bool Ready { get; private set; }
        public bool IntroductionCompleted => data != null && introduction != null && data.completedCutsceneIds.Contains(introduction.sequenceId);
        public bool InteractionsUnlocked => Ready && IntroductionCompleted && !awaitingIntroduction && !CutsceneDirector.IsActive;
        public ShangrilaCasePhase Phase => IntroductionCompleted ? ShangrilaCasePhase.FreeInvestigation : ShangrilaCasePhase.StreetArrival;
        public int IntroductionStartCount { get; private set; }
        public string LastError { get; private set; }
        public string SavePath
        {
            get
            {
#if UNITY_EDITOR
                string testPath = UnityEditor.SessionState.GetString("ProjectCRT.CaseVerification.Path", "");
                if (!string.IsNullOrEmpty(testPath)) return testPath;
#endif
                return Path.Combine(Application.persistentDataPath, "shangrila_case_v1.json");
            }
        }
        ShangrilaCaseSave data;
        ShangrilaCaseStore store;
        InvestigationInventoryManager inventory;
        IDisposable inventorySession;
        bool awaitingIntroduction, applying, protectBrokenSave;
        Vector3 entryPosition;
        float entryYaw, entryPitch;
        string message;
        float messageUntil;

        IEnumerator Start()
        {
            // Existing scene managers and the persistent inventory finish binding first.
            yield return null;
            if (walking == null || Player == null || director == null || introduction == null ||
                string.IsNullOrWhiteSpace(introduction.sequenceId) || itemDatabase == null ||
                !string.IsNullOrEmpty(introduction.stageScene) || introduction.exitMode != CutsceneExitMode.Return)
            { LastError = "Arrival setup needs a local return cutscene, player and item database."; Debug.LogError(LastError, this); yield break; }
            inventory = InvestigationInventoryManager.GetOrCreateInstance();
            inventorySession = inventory.BeginCaseSession(ShangrilaCaseSave.CaseId, itemDatabase);
            store = new ShangrilaCaseStore(SavePath);
            data = ShangrilaCaseSave.New(gameObject.scene.path);
            director.Committed += OnCommitted; director.Finished += OnFinished;
            var result = store.Load(gameObject.scene.path, out var loaded, out var error);
            if (result == CaseLoadResult.Loaded || result == CaseLoadResult.RecoveredBackup)
            {
                Apply(loaded);
                // Repair a recovered slot without replacing the good backup with the broken primary.
                protectBrokenSave = result == CaseLoadResult.RecoveredBackup;
                Notice(protectBrokenSave ? "백업에서 복구했습니다. 저장 파일을 확인해 주세요." : "진행 상태를 불러왔습니다.");
            }
            else
            {
                Player.RestoreWalkingPose(walking.streetSpawn.position, walking.streetSpawn.eulerAngles.y, 0);
                if (result == CaseLoadResult.Invalid)
                { protectBrokenSave = true; LastError = error; Notice("저장 파일을 읽을 수 없습니다. 원본 보호를 위해 저장을 중지했습니다."); }
            }
            Ready = true;
            inventory.InventoryChanged += OnInventoryChanged;
            UpdateHud();
        }

        public bool TryStartIntroduction()
        {
            if (!Ready || IntroductionCompleted || awaitingIntroduction || director.IsPlaying || !CanUseSlot()) return false;
            entryPosition = Player.transform.position; entryYaw = Player.transform.eulerAngles.y; entryPitch = Player.ViewPitch;
            awaitingIntroduction = true;
            if (!director.Play(introduction))
            { awaitingIntroduction = false; LastError = director.LastError; Notice("입장 연출을 시작하지 못했습니다. 입구로 다시 들어와 주세요."); return false; }
            IntroductionStartCount++; return true;
        }
        void OnCommitted(CutsceneResult result)
        {
            if (!awaitingIntroduction || (result != CutsceneResult.Completed && result != CutsceneResult.Skipped)) return;
            if (!IntroductionCompleted) data.completedCutsceneIds.Add(introduction.sequenceId);
            // Commit before fade-in: quitting here must never replay a completed introduction.
            data.hasPlayerPose = true; data.playerPosition = entryPosition; data.playerYaw = entryYaw; data.playerPitch = entryPitch;
            SaveInternal(false);
        }
        void OnFinished(CutsceneResult result)
        {
            if (!awaitingIntroduction) return;
            awaitingIntroduction = false;
            if (IntroductionCompleted)
            {
                bool saved = SaveInternal(true);
                Notice(saved ? "이제 사람들과 대화하고 주변 물건을 조사할 수 있습니다." : "대화·조사는 해금됐지만 저장하지 못했습니다. 저장 파일 상태를 확인해 주세요.");
            }
            else Notice("입장 연출이 중단되었습니다. 입구로 다시 들어오면 재시도합니다.");
        }

        bool CanUseSlot() => Player != null && !CutsceneDirector.IsActive && !Player.cameraTransitionInProgress &&
            NpcInvestigationController.CurrentInteraction == null &&
            (DialogueManager.Instance == null || !DialogueManager.Instance.IsDialogueRunning) &&
            MainLoop.Instance != null && MainLoop.Instance.MainLoopState == MainState.Main;

        public bool SaveProgress()
        {
            if (!Ready || !CanUseSlot()) { Notice("걷는 상태에서 저장할 수 있습니다."); return false; }
            bool saved = SaveInternal(true);
            if (saved) Notice("진행 상태를 저장했습니다.");
            return saved;
        }
        bool SaveInternal(bool capturePlayer)
        {
            if (protectBrokenSave) { Notice("저장 파일 보호 중: 원본과 백업을 확인해 주세요."); return false; }
            data.inventory = inventory.ToSaveData();
            if (capturePlayer)
            {
                data.hasPlayerPose = true; data.playerPosition = Player.transform.position;
                data.playerYaw = Player.transform.eulerAngles.y; data.playerPitch = Player.ViewPitch;
            }
            if (store.Save(data, out var error)) { LastError = null; return true; }
            LastError = error; Notice("저장하지 못했습니다. 저장 공간과 파일 상태를 확인해 주세요.");
            Debug.LogWarning("[ShangrilaCase] Save failed: " + error, this); return false;
        }
        public bool LoadProgress()
        {
            if (!Ready || !CanUseSlot()) { Notice("걷는 상태에서 불러올 수 있습니다."); return false; }
            var result = store.Load(gameObject.scene.path, out var loaded, out var error);
            if (result == CaseLoadResult.Missing) { Notice("아직 저장한 진행 상태가 없습니다."); return false; }
            if (result == CaseLoadResult.Invalid)
            { LastError = error; Notice("저장 파일을 읽을 수 없어 현재 진행을 유지합니다."); return false; }
            protectBrokenSave = result == CaseLoadResult.RecoveredBackup;
            Apply(loaded);
            Notice(protectBrokenSave ? "백업에서 복구했습니다. 저장 파일을 확인해 주세요." : "진행 상태를 불러왔습니다."); return true;
        }
        void Apply(ShangrilaCaseSave loaded)
        {
            applying = true;
            try
            {
                data = loaded; inventory.ApplySaveData(data.inventory);
                foreach (var context in FindObjectsByType<InvestigationContextBuilder>(FindObjectsSortMode.None))
                    if (context.gameObject.scene == gameObject.scene) context.ClearRuntimeDiscoveries();
                if (data.hasPlayerPose) Player.RestoreWalkingPose(data.playerPosition, data.playerYaw, data.playerPitch);
                else Player.RestoreWalkingPose(walking.streetSpawn.position, walking.streetSpawn.eulerAngles.y, 0);
            }
            finally { applying = false; }
        }
        void OnInventoryChanged()
        {
            if (Ready && !applying && IntroductionCompleted && !CutsceneDirector.IsActive) SaveInternal(true);
        }
        void Update()
        {
            if (!Ready) return;
            if (Input.GetKeyDown(KeyCode.F5)) SaveProgress();
            if (Input.GetKeyDown(KeyCode.F9)) LoadProgress();
            UpdateHud();
        }
        void UpdateHud()
        {
            bool visible = Ready && !CutsceneDirector.IsActive && MainLoop.Instance != null && MainLoop.Instance.MainLoopState == MainState.Main;
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(visible);
                objectiveText.text = IntroductionCompleted ? "주변 조사 · 사람들과 대화하거나 안내문과 메뉴를 살펴보자" : "첫 방문 · 샹그리아 안으로 들어가자";
            }
            if (saveText != null)
            {
                saveText.gameObject.SetActive(visible);
                saveText.text = Time.unscaledTime < messageUntil ? message : "F5 저장 · F9 불러오기 · Tab 조사 기록";
            }
        }
        void Notice(string text) { message = text; messageUntil = Time.unscaledTime + 5; }
        void OnApplicationPause(bool paused) { if (paused && Ready && CanUseSlot()) SaveInternal(true); }
        void OnApplicationQuit() { if (Ready && CanUseSlot()) SaveInternal(true); }
        void OnDestroy()
        {
            if (director != null) { director.Committed -= OnCommitted; director.Finished -= OnFinished; }
            if (inventory != null) inventory.InventoryChanged -= OnInventoryChanged;
            inventorySession?.Dispose();
        }
    }
}
