using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class EventSchedulerTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        GameObject host;
        GameSession session;
        MapDefinition map, other;
        SurvivalSettings settings;
        Scene oldScene, scene;
        string directory;
        readonly List<Behaviour> hidden = new List<Behaviour>();
        [UnitySetUp]
        public IEnumerator Setup()
        {
            Assert.That(GameSession.Instance, Is.Null);
            oldScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Scheduler tests " + Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(scene);
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (camera.enabled) { hidden.Add(camera); camera.enabled = false; }
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (listener.enabled) { hidden.Add(listener); listener.enabled = false; }
            var floor = Asset<TileDefinition>();
            map = Asset<MapDefinition>(); map.mapId = "scheduler"; map.Resize(6, 6); map.Fill(floor); map.spawnCell = new Vector2Int(2, 2);
            other = Asset<MapDefinition>(); other.mapId = "elsewhere"; other.Resize(6, 6); other.Fill(floor); other.spawnCell = new Vector2Int(2, 2);
            settings = Asset<SurvivalSettings>(); settings.timeMode = TimeMode.AuthoredCosts; settings.fatiguePerMinute = 0;
            host = new GameObject("Scheduler test bootstrap"); host.SetActive(false);
            var bootstrap = host.AddComponent<NoSleepBootstrap>(); bootstrap.startingMap = map; bootstrap.maps = new List<MapDefinition> { map, other };
            bootstrap.survivalSettings = settings; host.SetActive(true);
            session = host.GetComponent<GameSession>(); session.UI.CloseMenus();
            directory = Path.Combine(Path.GetTempPath(), "NoSleepSchedulerTests-" + Guid.NewGuid().ToString("N")); session.ConfigureSaveDirectory(directory);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            session?.Scheduler?.CancelAll(); if (host != null) Object.Destroy(host); yield return null;
            foreach (var asset in assets) if (asset != null) Object.Destroy(asset); assets.Clear();
            if (oldScene.IsValid() && oldScene.isLoaded) SceneManager.SetActiveScene(oldScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var component in hidden) if (component != null) component.enabled = true; hidden.Clear();
            if (directory != null && Directory.Exists(directory))
            {
                Assert.That(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(directory, true);
            }
        }
        T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
        EventDefinition Parallel(params EventCommand[] commands)
        {
            var definition = Asset<EventDefinition>(); definition.pages.Add(new EventPage { trigger = EventTrigger.Parallel, visible = false, blocksMovement = false, commands = new List<EventCommand>(commands) });
            definition.EnsureStableIds(); return definition;
        }
        EventObject Place(EventDefinition definition, string id = "observer")
        {
            map.events.Add(new MapEventPlacement { id = id, cell = new Vector2Int(4, 4), definition = definition });
            session.TransferPlayer(map, map.spawnCell);
            foreach (var owner in session.Map.EventObjects) if (owner.EventKey == map.mapId + "/" + id) return owner;
            throw new Exception("Missing placement");
        }
        EventCommand Grant(int gold) => new EventCommand { type = EventCommandType.GrantOnce, mutations = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddGold, intValue = gold } } };
        [UnityTest]
        public IEnumerator BackgroundWaitLeavesInputAndSaveAvailableAndResumesOriginalDeadline()
        {
            var definition = Parallel(Grant(7), new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 10 }, Grant(11));
            definition.pages[0].runOnce = true; Place(definition);
            yield return null; yield return null;
            Assert.That(session.State.player.gold, Is.EqualTo(7)); Assert.That(session.IsInputBlocked, Is.False);
            Assert.That(session.Events.IsRunning, Is.False); Assert.That(session.Scheduler.BackgroundContexts.Count, Is.EqualTo(1));
            Assert.That(session.State.eventCheckpoints.Count, Is.EqualTo(1));
            int due = session.State.eventCheckpoints[0].dueElapsedMinutes;
            Assert.That(session.Save(), Is.True);
            var oldContext = session.Scheduler.BackgroundContexts[0];
            SurvivalRules.AdvanceTime(session.State, 3, settings);
            Assert.That(session.Load(), Is.True); yield return null; yield return null;
            Assert.That(oldContext.IsCancelled, Is.True); Assert.That(session.State.player.gold, Is.EqualTo(7));
            Assert.That(session.State.eventCheckpoints[0].dueElapsedMinutes, Is.EqualTo(due));
            SurvivalRules.AdvanceTime(session.State, 10, settings); yield return null; yield return null;
            Assert.That(session.State.player.gold, Is.EqualTo(18));
            Assert.That(definition.IsPageCompleted(session.State, "scheduler/observer", 0), Is.True);
            Assert.That(session.Save(), Is.True); Assert.That(session.Load(), Is.True); yield return null;
            Assert.That(session.State.player.gold, Is.EqualTo(18));
        }
        [UnityTest]
        public IEnumerator ForegroundRequestWaitsForMenuAndRevalidatesSourcePage()
        {
            var response = Asset<EventDefinition>(); response.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.AddGold, intValue = 50 } } });
            var definition = Parallel(new EventCommand { type = EventCommandType.RequestForeground, foregroundEvent = response });
            Place(definition);
            session.UI.ShowStatus();
            Assert.That(session.Scheduler.EnqueueForeground(response, "scheduler/observer"), Is.True);
            Assert.That(session.Scheduler.EnqueueForeground(response, "scheduler/observer"), Is.True);
            Assert.That(session.Scheduler.QueuedCount, Is.EqualTo(1));
            yield return null; Assert.That(session.State.player.gold, Is.Zero);
            definition.pages.Add(new EventPage { conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.Switch, key = "changed", expected = true } }, commands = new List<EventCommand>() }); definition.EnsureStableIds();
            session.State.SetSwitch("changed", true); session.UI.CloseMenus(); yield return null;
            Assert.That(session.Scheduler.QueuedCount, Is.Zero); Assert.That(session.State.player.gold, Is.Zero);
        }
        [UnityTest]
        public IEnumerator MapTransferCancelsOldJobsAndQueueButForegroundKeepsCallerKey()
        {
            var wait = Parallel(new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 100 }, Grant(99));
            Place(wait); yield return null; yield return null;
            var old = session.Scheduler.BackgroundContexts[0];
            var request = Asset<EventDefinition>(); request.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.AddGold, intValue = 20 } } });
            session.UI.ShowStatus(); Assert.That(session.Scheduler.EnqueueForeground(request, "scheduler/observer"), Is.True);
            Assert.That(session.TransferPlayer(other, other.spawnCell), Is.True);
            Assert.That(old.IsCancelled, Is.True); Assert.That(session.Scheduler.QueuedCount, Is.Zero); session.UI.CloseMenus();
            var transfer = Asset<EventDefinition>(); transfer.pages.Add(new EventPage { commands = new List<EventCommand> {
                new EventCommand { type = EventCommandType.TransferPlayer, map = map, cell = map.spawnCell },
                new EventCommand { type = EventCommandType.SetSelfSwitch, key = "A", boolValue = true } } });
            Assert.That(session.Events.TryRun(transfer, "elsewhere/door"), Is.True);
            yield return null;
            Assert.That(session.State.GetSelfSwitch("elsewhere/door", "A"), Is.True);
            Assert.That(session.State.GetSelfSwitch("scheduler/door", "A"), Is.False); Assert.That(session.State.player.gold, Is.Zero);
        }
        [UnityTest]
        public IEnumerator PauseStepAndCancelReleaseInputWithoutCompletingCancelledProgram()
        {
            var definition = Parallel(Grant(5), Grant(7), new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 100 }); Place(definition);
            var completedForeground = Asset<EventDefinition>(); completedForeground.pages.Add(new EventPage { commands = new List<EventCommand>() });
            Assert.That(session.Events.TryRun(completedForeground, "scheduler/finished"), Is.True);
            Assert.That(session.Events.CurrentContext.IsComplete, Is.True);
            session.Scheduler.SetPaused(true); yield return null;
            Assert.That(session.State.player.gold, Is.Zero); Assert.That(session.IsInputBlocked, Is.True);
            session.Scheduler.Step(); Assert.That(session.State.player.gold, Is.EqualTo(5));
            yield return null; Assert.That(session.State.player.gold, Is.EqualTo(5));
            session.Scheduler.Step(); Assert.That(session.State.player.gold, Is.EqualTo(12));
            session.Scheduler.CancelAll(); Assert.That(session.IsInputBlocked, Is.False);
            Assert.That(definition.IsPageCompleted(session.State, "scheduler/observer", 0), Is.False);
        }
        [UnityTest]
        public IEnumerator RepeatingProgramsShareFrameBudgetAndNeverBlockMovement()
        {
            var loop = Parallel(new EventCommand { type = EventCommandType.Label, label = "again" }, new EventCommand { type = EventCommandType.Jump, targetLabel = "again" });
            for (int index = 0; index < 12; index++) map.events.Add(new MapEventPlacement { id = "loop" + index.ToString("00"), definition = loop, cell = new Vector2Int(4, 4) });
            session.TransferPlayer(map, map.spawnCell); yield return null;
            int total = 0; foreach (var context in session.Scheduler.BackgroundContexts) total += context.ExecutedInstructions;
            Assert.That(total, Is.GreaterThan(0).And.LessThanOrEqualTo(128)); Assert.That(session.IsInputBlocked, Is.False);
            yield return null;
            int advanced = 0; foreach (var context in session.Scheduler.BackgroundContexts) if (context.ExecutedInstructions > 0) advanced++;
            Assert.That(advanced, Is.EqualTo(12), "A busy early registration must not starve later programs.");
        }
        [UnityTest]
        public IEnumerator NewGameInvalidatesOldExecutionAndResetsAtomicCompletion()
        {
            var definition = Parallel(Grant(9), new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 100 }, Grant(99)); Place(definition); yield return null; yield return null;
            var old = session.Scheduler.BackgroundContexts[0]; int epoch = session.ExecutionEpoch;
            Assert.That(session.NewGame(), Is.True); Assert.That(session.ExecutionEpoch, Is.GreaterThan(epoch));
            Assert.That(old.IsCancelled, Is.True); old.MoveNext(out _); Assert.That(session.State.player.gold, Is.Zero);
            yield return null; yield return null; Assert.That(session.State.player.gold, Is.EqualTo(9));
        }
        [UnityTest]
        public IEnumerator PendingRequestBlocksSaveUntilItIsDelivered()
        {
            Place(Parallel(new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 100 })); yield return null;
            var request = Asset<EventDefinition>(); request.pages.Add(new EventPage { runOnce = true, commands = new List<EventCommand> { new EventCommand { type = EventCommandType.AddGold, intValue = 13 } } });
            session.UI.ShowStatus(); Assert.That(session.Scheduler.EnqueueForeground(request, "scheduler/observer"), Is.True);
            Assert.That(session.Save(), Is.False); Assert.That(File.Exists(session.SavePath()), Is.False);
            session.UI.CloseMenus(); yield return null; yield return null;
            Assert.That(session.Scheduler.QueuedCount, Is.Zero); Assert.That(session.State.player.gold, Is.EqualTo(13));
            Assert.That(session.Save(), Is.True); Assert.That(session.Load(), Is.True); yield return null;
            Assert.That(session.State.player.gold, Is.EqualTo(13));
        }
        [UnityTest]
        public IEnumerator FailedTransferRestoresTheOriginalRequestQueueAndMap()
        {
            Place(Parallel(new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 100 })); yield return null;
            var blocker = Asset<EventDefinition>(); blocker.pages.Add(new EventPage()); blocker.EnsureStableIds();
            for (int y = 0; y < other.height; y++) for (int x = 0; x < other.width; x++)
                other.events.Add(new MapEventPlacement { id = "block_" + x + "_" + y, definition = blocker, cell = new Vector2Int(x, y) });
            var request = Asset<EventDefinition>(); request.pages.Add(new EventPage { commands = new List<EventCommand>() });
            session.UI.ShowStatus(); Assert.That(session.Scheduler.EnqueueForeground(request, "scheduler/observer"), Is.True);
            var state = session.State; Vector3 position = session.Player.transform.position;
            Assert.That(session.TransferPlayer(other, other.spawnCell), Is.False);
            Assert.That(session.Map.Map, Is.SameAs(map)); Assert.That(session.State, Is.SameAs(state));
            Assert.That(session.Player.transform.position, Is.EqualTo(position)); Assert.That(session.Scheduler.QueuedCount, Is.EqualTo(1));
            yield return null; Assert.That(session.Scheduler.QueuedCount, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator FailedNewGameRebuildsPagesUsingTheOriginalState()
        {
            session.State.SetSwitch("open", true);
            var blocker = Asset<EventDefinition>(); blocker.pages.Add(new EventPage { conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.Switch, key = "open", expected = false } } }); blocker.EnsureStableIds();
            for (int y = 0; y < map.height; y++) for (int x = 0; x < map.width; x++)
                map.events.Add(new MapEventPlacement { id = "block_" + x + "_" + y, definition = blocker, cell = new Vector2Int(x, y) });
            Assert.That(session.TransferPlayer(map, map.spawnCell), Is.True);
            var state = session.State; Vector3 position = session.Player.transform.position;
            Assert.That(session.NewGame(), Is.False); Assert.That(session.State, Is.SameAs(state));
            Assert.That(session.Player.transform.position, Is.EqualTo(position));
            Assert.That(session.Map.CanOccupy(position, session.Player.Radius), Is.True);
            foreach (var owner in session.Map.EventObjects) Assert.That(owner.ActivePageIndex, Is.EqualTo(-1));
            yield return null;
        }
    }
}
