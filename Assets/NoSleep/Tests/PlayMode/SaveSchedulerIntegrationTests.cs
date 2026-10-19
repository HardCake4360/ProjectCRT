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
    public sealed class SaveSchedulerIntegrationTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        readonly List<Camera> previousCameras = new List<Camera>();
        readonly List<bool> cameraEnabled = new List<bool>();
        readonly List<AudioListener> previousListeners = new List<AudioListener>();
        readonly List<bool> listenerEnabled = new List<bool>();
        Scene previousScene, testScene;
        GameObject host;
        GameSession session;
        MapDefinition map;
        EventDefinition legacyEvent, queuedEvent;
        string temporaryDirectory;
        float previousTimeScale;
        bool initialized;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (GameSession.Instance != null) Assert.Ignore("Save scheduler tests require a scene without a running GameSession.");
            initialized = true;
            previousTimeScale = Time.timeScale; Time.timeScale = 1;
            previousScene = SceneManager.GetActiveScene();
            testScene = SceneManager.CreateScene("No Sleep Save Scheduler Test " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(testScene);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                previousCameras.Add(camera); cameraEnabled.Add(camera.enabled); camera.enabled = false;
            }
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                previousListeners.Add(listener); listenerEnabled.Add(listener.enabled); listener.enabled = false;
            }
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NoSleepSaveSchedulerTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            var floor = CreateAsset<TileDefinition>();
            map = CreateAsset<MapDefinition>(); map.mapId = "save_scheduler"; map.displayName = "Save scheduler";
            map.Resize(8, 8); map.Fill(floor); map.spawnCell = new Vector2Int(2, 2);
            var watcher = CreateAsset<EventDefinition>();
            watcher.pages.Add(new EventPage { trigger = EventTrigger.Parallel, visible = false, blocksMovement = false,
                commands = new List<EventCommand> { new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 20 } } });
            watcher.EnsureStableIds();
            map.events.Add(new MapEventPlacement { id = "watcher", cell = new Vector2Int(5, 5), definition = watcher });
            legacyEvent = CreateAsset<EventDefinition>();
            legacyEvent.pages.Add(new EventPage { runOnce = true, visible = false, blocksMovement = false,
                commands = new List<EventCommand> { new EventCommand { type = EventCommandType.AddGold, intValue = 20 } } });
            legacyEvent.EnsureStableIds();
            map.events.Add(new MapEventPlacement { id = "legacy", cell = new Vector2Int(6, 6), definition = legacyEvent });
            var callback = CreateAsset<EventDefinition>();
            callback.pages.Add(new EventPage { trigger = EventTrigger.Parallel, visible = false, blocksMovement = false,
                conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.Switch, key = "run_callback" } },
                commands = new List<EventCommand> { new EventCommand { type = EventCommandType.GrantOnce,
                    mutations = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddGold, intValue = 1 } } } } });
            callback.EnsureStableIds();
            map.events.Add(new MapEventPlacement { id = "callback", cell = new Vector2Int(6, 5), definition = callback });
            queuedEvent = CreateAsset<EventDefinition>();
            queuedEvent.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.Wait, seconds = 20 } } });
            queuedEvent.EnsureStableIds();
            host = new GameObject("No Sleep Save Scheduler Test"); host.SetActive(false);
            NoSleepBootstrap bootstrap = host.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = map; bootstrap.maps = new List<MapDefinition> { map }; bootstrap.showTitleScreen = false;
            host.SetActive(true); session = host.GetComponent<GameSession>();
            session.ConfigureSaveDirectory(temporaryDirectory);
            Assert.That(session.IsReady, Is.True);
            for (int frame = 0; frame < 15 && session.Scheduler.BackgroundContexts.Count == 0; frame++) yield return null;
            Assert.That(session.Scheduler.BackgroundContexts.Count, Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!initialized) yield break;
            if (session != null && session.Scheduler != null) session.Scheduler.CancelAll();
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
            string path = Path.GetFullPath(temporaryDirectory);
            string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(path.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(path).StartsWith("NoSleepSaveSchedulerTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(path)) Directory.Delete(path, true);
            initialized = false;
        }

        [UnityTest]
        public IEnumerator FailedContentAndCheckpointLoadsKeepCurrentContextAndQueuedRequests()
        {
            session.UI.ShowStatus();
            Assert.That(session.Scheduler.EnqueueForeground(queuedEvent, "save_scheduler/request"), Is.True);
            Assert.That(session.Scheduler.QueuedCount, Is.EqualTo(1));
            var currentContext = session.Scheduler.BackgroundContexts[0];
            GameState currentState = session.State;
            Vector3 position = session.Player.transform.position;
            int epoch = session.ExecutionEpoch;
            var invalidCheckpoint = new GameState();
            invalidCheckpoint.SetEventCheckpoint("removed/npc/definition/page", "removed-command", 100);
            var invalidCompletion = new GameState();
            invalidCompletion.SetSelfSwitch("removed/npc", EventDefinition.CompletionKey(0), true);
            foreach (GameState invalid in new[] { invalidCheckpoint, invalidCompletion })
            {
                session.SaveSlots.Write(1, new SaveData { mapId = map.mapId, position = position, state = invalid });
                LogAssert.Expect(LogType.Warning, new Regex("Load failed:"));
                Assert.That(session.Load(1), Is.False);
                Assert.That(session.State, Is.SameAs(currentState));
                Assert.That(session.Map.Map, Is.SameAs(map));
                Assert.That(session.Player.transform.position, Is.EqualTo(position));
                Assert.That(session.ExecutionEpoch, Is.EqualTo(epoch));
                Assert.That(session.Scheduler.BackgroundContexts.Count, Is.EqualTo(1));
                Assert.That(session.Scheduler.BackgroundContexts[0], Is.SameAs(currentContext));
                Assert.That(currentContext.IsCancelled, Is.False);
                Assert.That(session.Scheduler.QueuedCount, Is.EqualTo(1));
            }
            yield return null;
            Assert.That(session.Scheduler.BackgroundContexts[0], Is.SameAs(currentContext));
            Assert.That(session.Scheduler.QueuedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SavingLegacyCompletionMigratesOnlyTheDetachedCandidate()
        {
            session.UI.ShowStatus();
            string eventKey = "save_scheduler/legacy";
            session.State.SetSelfSwitch(eventKey, EventDefinition.CompletionKey(0), true);
            string before = JsonUtility.ToJson(session.State);
            int notifications = 0; Action listener = () => notifications++;
            session.State.Changed += listener;
            try
            {
                Assert.That(session.Save(0), Is.True);
                Assert.That(JsonUtility.ToJson(session.State), Is.EqualTo(before));
                Assert.That(notifications, Is.Zero);
                Assert.That(session.State.HasSelfSwitch(eventKey, EventDefinition.CompletionKey(0)), Is.True);
                SaveData saved = session.SaveSlots.Read(0);
                Assert.That(saved.version, Is.EqualTo(SaveService.CurrentVersion));
                Assert.That(saved.state.HasSelfSwitch(eventKey, EventDefinition.CompletionKey(0)), Is.False);
                Assert.That(saved.state.GetSelfSwitch(eventKey, legacyEvent.StableCompletionKey(legacyEvent.pages[0].pageId)), Is.True);
                Assert.That(session.State.HasSelfSwitch(eventKey, legacyEvent.StableCompletionKey(legacyEvent.pages[0].pageId)), Is.False);
            }
            finally { session.State.Changed -= listener; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CheckpointAndCompletionMigrationFailuresDoNotReplaceExistingSaveOrBackup()
        {
            session.UI.ShowStatus();
            Assert.That(session.Save(0), Is.True);
            session.State.AddGold(5);
            Assert.That(session.Save(0), Is.True);
            string primary = File.ReadAllText(session.SavePath(0));
            string backup = File.ReadAllText(session.SavePath(0) + ".bak");
            session.State.SetEventCheckpoint("removed/npc/definition/page", "missing-command", 100);
            string before = JsonUtility.ToJson(session.State);
            LogAssert.Expect(LogType.Warning, new Regex("Save failed:"));
            Assert.That(session.Save(0), Is.False);
            Assert.That(File.ReadAllText(session.SavePath(0)), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(session.SavePath(0) + ".bak"), Is.EqualTo(backup));
            Assert.That(JsonUtility.ToJson(session.State), Is.EqualTo(before));

            session.State.ClearEventCheckpoint("removed/npc/definition/page");
            session.State.SetSelfSwitch("removed/npc", EventDefinition.CompletionKey(0), true);
            before = JsonUtility.ToJson(session.State);
            LogAssert.Expect(LogType.Warning, new Regex("Save failed:"));
            Assert.That(session.Save(0), Is.False);
            Assert.That(File.ReadAllText(session.SavePath(0)), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(session.SavePath(0) + ".bak"), Is.EqualTo(backup));
            Assert.That(JsonUtility.ToJson(session.State), Is.EqualTo(before));
            Assert.That(File.Exists(session.SavePath(0) + ".tmp"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveAndLoadInsideParallelChangedCallbackAreBlockedUntilTheSafeBoundary()
        {
            Assert.That(session.Save(0), Is.True);
            string saved = File.ReadAllText(session.SavePath(0));
            session.State.SetSwitch("run_callback", true);
            GameState current = session.State;
            int callbacks = 0;
            bool saveResult = true, loadResult = true, safeBoundary = true;
            Action listener = () =>
            {
                if (callbacks > 0 || session.State.player.gold != 1) return;
                callbacks++;
                safeBoundary = session.Scheduler.IsAtSafeBoundary;
                saveResult = session.Save(0);
                loadResult = session.Load(0);
            };
            current.Changed += listener;
            try
            {
                for (int frame = 0; frame < 15 && callbacks == 0; frame++) yield return null;
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(safeBoundary, Is.False);
                Assert.That(saveResult, Is.False);
                Assert.That(loadResult, Is.False);
                Assert.That(session.State, Is.SameAs(current));
                Assert.That(session.State.player.gold, Is.EqualTo(1));
                Assert.That(File.ReadAllText(session.SavePath(0)), Is.EqualTo(saved));
                Assert.That(session.Scheduler.IsAtSafeBoundary, Is.True);
                Assert.That(session.Save(0), Is.True, "The completed background transaction must allow saving on the next safe boundary.");
            }
            finally { current.Changed -= listener; }
        }

        T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset;
        }
    }
}
