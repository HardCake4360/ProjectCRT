using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class SessionProgressTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        readonly List<Camera> previousCameras = new List<Camera>();
        readonly List<bool> cameraEnabled = new List<bool>();
        readonly List<AudioListener> previousListeners = new List<AudioListener>();
        readonly List<bool> listenerEnabled = new List<bool>();
        Scene previousScene, testScene;
        TileDefinition floor;
        MapDefinition firstMap, secondMap;
        SurvivalSettings settings;
        ItemEffectDefinition stimulant;
        GameObject host;
        GameSession session;
        string temporaryDirectory;
        float previousTimeScale;
        bool initialized;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (GameSession.Instance != null) Assert.Ignore("Session progress tests require a scene without a running GameSession.");
            initialized = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            previousScene = SceneManager.GetActiveScene();
            testScene = SceneManager.CreateScene("No Sleep Session Progress Test " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(testScene);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                previousCameras.Add(camera); cameraEnabled.Add(camera.enabled); camera.enabled = false;
            }
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                previousListeners.Add(listener); listenerEnabled.Add(listener.enabled); listener.enabled = false;
            }
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NoSleepSessionProgressTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            floor = CreateAsset<TileDefinition>();
            floor.displayName = "Test floor";
            firstMap = CreateMap("progress_home", new Vector2Int(2, 2));
            secondMap = CreateMap("progress_hospital", new Vector2Int(3, 3));
            settings = CreateAsset<SurvivalSettings>();
            settings.initialHour = 9;
            settings.timeMode = TimeMode.AuthoredCosts;
            stimulant = CreateAsset<ItemEffectDefinition>();
            stimulant.itemId = "stimulant";
            host = new GameObject("No Sleep Session Progress Test");
            host.SetActive(false);
            NoSleepBootstrap bootstrap = host.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = firstMap;
            bootstrap.maps = new List<MapDefinition> { firstMap, secondMap };
            bootstrap.playerName = "Save test player";
            host.SetActive(true);
            session = host.GetComponent<GameSession>();
            session.ConfigureSaveDirectory(temporaryDirectory);
            session.ConfigureHorror(settings, stimulant);
            session.ConfigureNewGame(firstMap, "Save test player");
            session.UI.CloseMenus();
            Assert.That(session.IsReady, Is.True);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!initialized) yield break;
            if (session != null && session.Events != null) session.Events.Cancel();
            if (host != null) Object.Destroy(host);
            yield return null;
            foreach (ScriptableObject asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (testScene.IsValid() && testScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(testScene);
                if (unload != null) yield return unload;
            }
            for (int i = 0; i < previousCameras.Count; i++)
                if (previousCameras[i] != null) previousCameras[i].enabled = cameraEnabled[i];
            for (int i = 0; i < previousListeners.Count; i++)
                if (previousListeners[i] != null) previousListeners[i].enabled = listenerEnabled[i];
            previousCameras.Clear(); cameraEnabled.Clear(); previousListeners.Clear(); listenerEnabled.Clear();
            Time.timeScale = previousTimeScale;
            if (!string.IsNullOrEmpty(temporaryDirectory))
            {
                string path = Path.GetFullPath(temporaryDirectory);
                string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.That(path.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(path).StartsWith("NoSleepSessionProgressTests-", StringComparison.Ordinal), Is.True);
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            initialized = false;
        }

        [UnityTest]
        public IEnumerator SessionSaveAndLoadRestoresEntireJourneyAndSpatialSnapshot()
        {
            SurvivalRules.AdvanceTime(session.State, 1500, settings);
            SurvivalRules.Awaken(session.State, 17, 90);
            SurvivalRules.RecordClue(session.State, "hospital-ledger", "병원 장부", "방문자의 흔적");
            SurvivalRules.SetObjective(session.State, "station", "역으로 향한다", 2);
            SurvivalRules.JoinCompanion(session.State, "guide");
            SurvivalRules.UnlockAction(session.State, "observe");
            session.State.journey.memories.Add(new MemoryRecord { id = "promise", title = "약속", text = "끝까지 함께" });
            session.State.SetSwitch("puzzle-open", true);
            session.State.SetSelfSwitch("progress_hospital:enemy", "defeated", true);
            session.State.SetVariable("chapter", 3);
            session.State.AddItem("stimulant", 2);
            session.State.player.health = 61;
            session.State.player.sanity = 48;
            session.State.AddGold(23);
            Assert.That(session.TransferPlayer(secondMap, secondMap.spawnCell), Is.True);
            Vector3 savedPosition = session.Player.transform.position + new Vector3(0.1f, 0, 0.1f);
            session.Player.Warp(savedPosition, 135);
            GameState before = session.State;
            int maximumHealth = before.player.maxHealth, strength = before.player.strength;
            float fatigue = before.survival.fatigue;
            int startHour = before.survival.startHour;
            Assert.That(session.Save(2), Is.True);
            Assert.That(session.SaveSlots.Inspect(2).data.mapDisplayName, Is.EqualTo(secondMap.displayName));
            Assert.That(session.SaveSlots.Inspect(2).data.objectiveTitle, Is.EqualTo("역으로 향한다"));
            Assert.That(session.SaveSlots.Inspect(2).data.savedUtcTicks, Is.GreaterThan(0));

            SurvivalRules.AdvanceTime(session.State, 300, settings);
            SurvivalRules.ClearParty(session.State);
            session.State.journey.memories.Clear();
            session.State.journey.clueIds.Clear();
            session.State.unlockedActions.Remove("observe");
            session.State.AddItem("stimulant", -2);
            session.State.SetSelfSwitch("progress_hospital:enemy", "defeated", false);
            Assert.That(session.TransferPlayer(firstMap, firstMap.spawnCell), Is.True);
            Assert.That(session.Load(2), Is.True);
            Assert.That(session.State, Is.Not.SameAs(before));
            Assert.That(session.Map.Map, Is.SameAs(secondMap));
            Assert.That(session.Player.transform.position, Is.EqualTo(savedPosition));
            Assert.That(session.Player.transform.eulerAngles.y, Is.EqualTo(135).Within(0.001f));
            Assert.That(session.State.player.health, Is.EqualTo(61));
            Assert.That(session.State.player.maxHealth, Is.EqualTo(maximumHealth));
            Assert.That(session.State.player.strength, Is.EqualTo(strength));
            Assert.That(session.State.player.sanity, Is.EqualTo(48));
            Assert.That(session.State.player.gold, Is.EqualTo(23));
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(1500));
            Assert.That(session.State.survival.startHour, Is.EqualTo(startHour));
            Assert.That(session.State.survival.fatigue, Is.EqualTo(fatigue));
            Assert.That(session.State.survival.awakenedUntilMinutes, Is.EqualTo(1590));
            Assert.That(session.State.survival.awakeningRelief, Is.EqualTo(17));
            Assert.That(session.State.journey.clueIds, Does.Contain("hospital-ledger"));
            Assert.That(session.State.journey.journal[0].text, Is.EqualTo("방문자의 흔적"));
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("station"));
            Assert.That(session.State.journey.objectiveTitle, Is.EqualTo("역으로 향한다"));
            Assert.That(session.State.journey.stage, Is.EqualTo(2));
            Assert.That(session.State.journey.memories[0].text, Is.EqualTo("끝까지 함께"));
            Assert.That(session.State.party.activeCompanions, Does.Contain("guide"));
            Assert.That(session.State.party.knownCompanions, Does.Contain("guide"));
            Assert.That(session.State.unlockedActions, Does.Contain("observe"));
            Assert.That(session.State.GetItemCount("stimulant"), Is.EqualTo(2));
            Assert.That(session.State.GetSwitch("puzzle-open"), Is.True);
            Assert.That(session.State.GetSelfSwitch("progress_hospital:enemy", "defeated"), Is.True);
            Assert.That(session.State.GetVariable("chapter"), Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CorruptAndUnknownMapLoadsKeepCurrentStateMapAndPlayer()
        {
            session.State.SetVariable("live-progress", 42);
            GameState previousState = session.State;
            MapDefinition previousMap = session.Map.Map;
            Vector3 previousPosition = session.Player.transform.position;
            float previousYaw = session.Player.transform.eulerAngles.y;
            File.WriteAllText(session.SavePath(0), "corrupt");
            LogAssert.Expect(LogType.Warning, new Regex("Load failed:"));
            Assert.That(session.Load(0), Is.False);
            AssertUnchanged(previousState, previousMap, previousPosition, previousYaw);
            session.SaveSlots.Write(1, new SaveData { mapId = "missing-map", state = new GameState(), position = previousPosition });
            LogAssert.Expect(LogType.Warning, new Regex("Load failed:"));
            Assert.That(session.Load(1), Is.False);
            AssertUnchanged(previousState, previousMap, previousPosition, previousYaw);
            Assert.That(session.State.GetVariable("live-progress"), Is.EqualTo(42));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BackupLoadRestoresPreviousStateWithoutOverwritingSavedFiles()
        {
            session.State.SetVariable("chapter", 1);
            Assert.That(session.Save(), Is.True);
            session.State.SetVariable("chapter", 2);
            Assert.That(session.TransferPlayer(secondMap, secondMap.spawnCell), Is.True);
            Assert.That(session.Save(), Is.True);
            string primary = File.ReadAllText(session.SavePath());
            string backup = File.ReadAllText(session.SavePath() + ".bak");
            session.State.SetVariable("chapter", 99);
            Assert.That(session.Load(0, true), Is.True);
            Assert.That(session.State.GetVariable("chapter"), Is.EqualTo(1));
            Assert.That(session.Map.Map, Is.SameAs(firstMap));
            Assert.That(File.ReadAllText(session.SavePath()), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(session.SavePath() + ".bak"), Is.EqualTo(backup));
            Assert.That(session.SaveSlots.Read(0).state.GetVariable("chapter"), Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActiveEventDialogueAndCombatEachBlockSaveAndLoad()
        {
            Assert.That(session.Save(), Is.True);
            string initialFile = File.ReadAllText(session.SavePath());
            var definition = CreateAsset<EventDefinition>();
            definition.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.Wait, seconds = 20 } } });
            Assert.That(session.Events.TryRun(definition, "test/wait"), Is.True);
            Assert.That(session.Events.IsRunning, Is.True);
            AssertSaveAndLoadBlocked(initialFile);
            session.Events.Cancel();

            IEnumerator dialogue = session.UI.ShowDialogue("검사", "대사 중에는 저장할 수 없다.");
            try
            {
                Assert.That(dialogue.MoveNext(), Is.True);
                Assert.That(session.UI.IsDialogueOpen, Is.True);
                AssertSaveAndLoadBlocked(initialFile);
            }
            finally { (dialogue as IDisposable)?.Dispose(); }

            var enemy = CreateAsset<EnemyDefinition>();
            enemy.enemyId = "test-enemy";
            IEnumerator combat = session.Combat.Run(enemy, null);
            try
            {
                Assert.That(combat.MoveNext(), Is.True);
                Assert.That(session.Combat.IsRunning, Is.True);
                Assert.That(session.UI.IsDialogueOpen, Is.False, "Combat alone must block persistence before its dialogue begins.");
                AssertSaveAndLoadBlocked(initialFile);
            }
            finally { (combat as IDisposable)?.Dispose(); }
            Assert.That(session.Combat.IsRunning, Is.False);
            Assert.That(session.Save(), Is.True);
            Assert.That(session.Load(), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StatusAndSlotMenusPermitPersistenceAndTitlePermitsContinue()
        {
            session.UI.ShowStatus();
            Assert.That(session.UI.IsModalOpen, Is.True);
            Assert.That(session.IsInputBlocked, Is.True);
            session.State.SetVariable("chapter", 1);
            Assert.That(session.Save(1), Is.True, "Status blocks movement while permitting a save.");
            session.UI.ShowSaveSlots(true);
            Assert.That(session.UI.IsModalOpen, Is.True);
            session.State.SetVariable("chapter", 2);
            Assert.That(session.Save(2), Is.True, "The slot picker must allow the selected save operation.");
            session.UI.ShowSaveSlots(false);
            Assert.That(session.Load(1), Is.True);
            Assert.That(session.State.GetVariable("chapter"), Is.EqualTo(1));
            Assert.That(session.UI.IsModalOpen, Is.False);

            session.UI.ShowTitle();
            Assert.That(session.UI.IsTitleOpen, Is.True);
            Assert.That(session.Save(1), Is.False, "The title cannot save an unstarted game.");
            Assert.That(session.Load(2), Is.True, "Continue may load from the title.");
            Assert.That(session.State.GetVariable("chapter"), Is.EqualTo(2));
            Assert.That(session.UI.IsTitleOpen, Is.False);
            Assert.That(session.IsInputBlocked, Is.False);

            session.State.player.health = 0;
            session.UI.ShowGameOver();
            Assert.That(session.Save(2), Is.False);
            Assert.That(session.Load(1), Is.True, "A healthy slot may restore play after game over.");
            Assert.That(session.State.player.health, Is.GreaterThan(0));
            Assert.That(session.UI.IsModalOpen, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewGameResetsProgressAndBaselineAndReturnsToStartingMap()
        {
            SurvivalRules.AdvanceTime(session.State, 4000, settings);
            SurvivalRules.RecordClue(session.State, "old-clue", "Old", "Old progress");
            SurvivalRules.JoinCompanion(session.State, "guide");
            SurvivalRules.UnlockAction(session.State, "observe");
            session.State.journey.memories.Add(new MemoryRecord { id = "old-memory", title = "Old", text = "Old" });
            session.State.AddItem("stimulant", 5);
            session.State.SetSwitch("old-event", true);
            session.State.SetSelfSwitch("progress_home:old-event", "A", true);
            session.State.player.health = 1;
            Assert.That(session.TransferPlayer(secondMap, secondMap.spawnCell), Is.True);
            GameState previousState = session.State;
            Assert.That(session.Save(), Is.True);
            string saved = File.ReadAllText(session.SavePath());
            session.UI.ShowTitle();
            Assert.That(session.NewGame(), Is.True);
            Assert.That(session.State, Is.Not.SameAs(previousState));
            Assert.That(session.State.player.name, Is.EqualTo("Save test player"));
            Assert.That(session.State.player.health, Is.EqualTo(100));
            Assert.That(session.State.player.maxHealth, Is.EqualTo(100));
            Assert.That(session.State.player.strength, Is.EqualTo(12));
            Assert.That(session.State.survival.elapsedMinutes, Is.Zero);
            Assert.That(session.State.survival.fatigue, Is.Zero);
            Assert.That(session.State.survival.startHour, Is.EqualTo(9));
            Assert.That(session.State.survival.baselineMaxHealth, Is.EqualTo(100));
            Assert.That(session.State.survival.baselineStrength, Is.EqualTo(12));
            Assert.That(session.State.journey.clueIds, Is.Empty);
            Assert.That(session.State.journey.memories, Is.Empty);
            Assert.That(session.State.party.activeCompanions, Is.Empty);
            Assert.That(session.State.GetItemCount("stimulant"), Is.Zero);
            Assert.That(session.State.GetSwitch("old-event"), Is.False);
            Assert.That(session.State.GetSelfSwitch("progress_home:old-event", "A"), Is.False);
            Assert.That(session.State.unlockedActions, Does.Not.Contain("observe"));
            Assert.That(session.Map.Map, Is.SameAs(firstMap));
            Assert.That(session.Map.WorldToCell(session.Player.transform.position), Is.EqualTo(firstMap.spawnCell));
            Assert.That(session.UI.IsModalOpen, Is.False);
            Assert.That(File.ReadAllText(session.SavePath()), Is.EqualTo(saved), "Starting a new game must not erase an existing slot.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealtimeClockPausesForMenusAndEventsAndNewGameDiscardsPartialMinute()
        {
            settings.timeMode = TimeMode.Realtime;
            settings.realSecondsPerGameMinute = 0.02f;
            session.UI.ShowTitle();
            int pausedMinutes = session.State.survival.elapsedMinutes;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(pausedMinutes), "Title time must not consume the player's waking hours.");

            session.UI.CloseMenus();
            session.UI.ShowStatus();
            Assert.That(session.UI.IsModalOpen, Is.True);
            pausedMinutes = session.State.survival.elapsedMinutes;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(pausedMinutes), "Reading status must pause realtime progression.");

            session.UI.ShowSaveSlots(true);
            pausedMinutes = session.State.survival.elapsedMinutes;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(pausedMinutes), "Choosing a save slot must pause realtime progression.");
            session.UI.CloseMenus();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(session.State.survival.elapsedMinutes, Is.GreaterThan(pausedMinutes), "Closing menus must resume the actual session clock.");

            var definition = CreateAsset<EventDefinition>();
            definition.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.Wait, seconds = 20 } } });
            Assert.That(session.Events.TryRun(definition, "clock/wait"), Is.True);
            pausedMinutes = session.State.survival.elapsedMinutes;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(session.Events.IsRunning, Is.True);
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(pausedMinutes), "An authored event owns its time cost while gameplay is locked.");
            session.Events.Cancel();

            // Accumulate less than one long minute. A new game must discard that
            // remainder; lowering the interval afterward must not charge it to the new run.
            settings.realSecondsPerGameMinute = 10f;
            yield return new WaitForSecondsRealtime(0.12f);
            Time.timeScale = 0;
            Assert.That(session.NewGame(), Is.True);
            settings.realSecondsPerGameMinute = 0.02f;
            yield return null;
            yield return null;
            Assert.That(session.State.survival.elapsedMinutes, Is.Zero, "A previous run's partial minute must not advance the new game, even at a shorter interval.");
            Assert.That(session.State.survival.fatigue, Is.Zero);
            Time.timeScale = 1;
            session.UI.ShowStatus();
        }

        void AssertSaveAndLoadBlocked(string file)
        {
            GameState current = session.State;
            Assert.That(session.Save(), Is.False);
            Assert.That(session.Load(), Is.False);
            Assert.That(session.State, Is.SameAs(current));
            Assert.That(File.ReadAllText(session.SavePath()), Is.EqualTo(file));
        }

        void AssertUnchanged(GameState state, MapDefinition map, Vector3 position, float yaw)
        {
            Assert.That(session.State, Is.SameAs(state));
            Assert.That(session.Map.Map, Is.SameAs(map));
            Assert.That(session.Player.transform.position, Is.EqualTo(position));
            Assert.That(session.Player.transform.eulerAngles.y, Is.EqualTo(yaw).Within(0.001f));
        }

        MapDefinition CreateMap(string id, Vector2Int spawn)
        {
            MapDefinition map = CreateAsset<MapDefinition>();
            map.mapId = id; map.displayName = id; map.Resize(6, 6); map.Fill(floor); map.spawnCell = spawn;
            return map;
        }

        T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset;
        }
    }
}
