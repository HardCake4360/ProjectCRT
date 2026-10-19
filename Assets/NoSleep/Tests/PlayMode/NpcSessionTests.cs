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
    public sealed class NpcSessionTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        readonly List<Camera> cameras = new List<Camera>();
        readonly List<bool> cameraEnabled = new List<bool>();
        readonly List<AudioListener> listeners = new List<AudioListener>();
        readonly List<bool> listenerEnabled = new List<bool>();
        Scene previousScene, scene;
        float previousTimeScale;
        string directory;
        GameObject host;
        GameSession session;
        TileDefinition floor;
        MapDefinition first, second;
        NpcDefinition walker, keeper;
        NpcRouteDefinition route;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(GameSession.Instance, Is.Null, "NPC session tests require an isolated test scene.");
            previousTimeScale = Time.timeScale; Time.timeScale = 0;
            previousScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("NoSleep NPC Session " + Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(scene);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            { cameras.Add(camera); cameraEnabled.Add(camera.enabled); camera.enabled = false; }
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            { listeners.Add(listener); listenerEnabled.Add(listener.enabled); listener.enabled = false; }
            directory = Path.Combine(Path.GetTempPath(), "NoSleepNpcSessionTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            floor = Asset<TileDefinition>();
            route = Asset<NpcRouteDefinition>(); route.routeId = "walker_route";
            route.points.Add(new NpcRoutePoint { cell = new Vector2Int(1, 1), yaw = 90 });
            route.points.Add(new NpcRoutePoint { cell = new Vector2Int(3, 1), yaw = 180 });
            walker = Asset<NpcDefinition>(); walker.npcId = "walker"; walker.speed = 1; walker.route = route;
            keeper = Asset<NpcDefinition>(); keeper.npcId = "keeper";
            first = CreateMap("npc_session_main"); second = CreateMap("npc_session_annex");
            first.events.Add(new MapEventPlacement { id = "a", npc = walker, cell = new Vector2Int(1, 1), yaw = 90 });
            first.events.Add(new MapEventPlacement { id = "b", npc = keeper, cell = new Vector2Int(5, 5), yaw = 45 });
            second.events.Add(new MapEventPlacement { id = "a", npc = walker, cell = new Vector2Int(1, 1) });
            host = new GameObject("NoSleep NPC Session Test"); host.SetActive(false);
            var bootstrap = host.AddComponent<NoSleepBootstrap>(); bootstrap.startingMap = first;
            bootstrap.maps = new List<MapDefinition> { first, second }; host.SetActive(true);
            session = host.GetComponent<GameSession>(); session.ConfigureSaveDirectory(directory); session.UI.ShowStatus();
            Assert.That(session.IsReady, Is.True); Assert.That(session.Npcs.Actors.Count, Is.EqualTo(2));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (session != null) { session.CancelPendingSave(); session.Events.Cancel(); }
            if (host != null) Object.Destroy(host); yield return null;
            foreach (ScriptableObject asset in assets) if (asset != null) Object.Destroy(asset); assets.Clear();
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded) { var unloading = SceneManager.UnloadSceneAsync(scene); if (unloading != null) yield return unloading; }
            for (int i = 0; i < cameras.Count; i++) if (cameras[i] != null) cameras[i].enabled = cameraEnabled[i];
            for (int i = 0; i < listeners.Count; i++) if (listeners[i] != null) listeners[i].enabled = listenerEnabled[i];
            cameras.Clear(); cameraEnabled.Clear(); listeners.Clear(); listenerEnabled.Clear(); Time.timeScale = previousTimeScale;
            if (string.IsNullOrEmpty(directory)) yield break;
            string full = Path.GetFullPath(directory), root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(full.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(full).StartsWith("NoSleepNpcSessionTests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }

        [UnityTest]
        public IEnumerator MovingSaveDrainsCurrentSegmentThroughModalAndWritesOnlyCommittedCell()
        {
            NpcActor actor = StartMoving(); Vector2Int destination = actor.ReservedDestination;
            Vector3 partialPose = actor.transform.position;
            session.Npcs.Tick(0.25f);
            Assert.That(actor.transform.position, Is.EqualTo(partialPose), "A status menu pauses ordinary NPC interpolation.");
            int callbacks = 0; session.SaveCompleted += (slot, ok) => { Assert.That(slot, Is.EqualTo(2)); Assert.That(ok, Is.True); callbacks++; };
            Assert.That(session.Save(2), Is.True); Assert.That(session.HasPendingSave, Is.True);
            Assert.That(session.SaveStatus, Is.EqualTo(SaveRequestStatus.Pending)); Assert.That(File.Exists(session.SavePath(2)), Is.False);
            Assert.That(session.Save(2), Is.True, "Repeating the same pending request must not add another write.");
            Assert.That(session.Save(3), Is.False); Assert.That(session.IsInputBlocked, Is.True);
            session.Npcs.Tick(2);
            Assert.That(actor.HasSegment, Is.False); Assert.That(actor.Cell, Is.EqualTo(destination));
            session.Npcs.Tick(2); Assert.That(actor.HasSegment, Is.False, "Drain freezes new segments at the boundary.");
            for (int frame = 0; frame < 4 && session.HasPendingSave; frame++) yield return null;
            Assert.That(session.HasPendingSave, Is.False); Assert.That(session.SaveStatus, Is.EqualTo(SaveRequestStatus.Completed));
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(session.Npcs.IsSaveDrainRequested, Is.False);
            NpcState saved = session.SaveSlots.Read(2).state.GetNpcState(first.mapId, "a");
            Assert.That(saved.cell, Is.EqualTo(destination)); Assert.That(saved.routeId, Is.EqualTo(route.routeId));
            Assert.That(saved.waypointIndex, Is.EqualTo(1));
            Assert.That(session.Load(2), Is.True); session.UI.ShowStatus(); yield return null;
            actor = session.Npcs.Find("a"); Assert.That(actor.Cell, Is.EqualTo(destination));
            Assert.That(actor.transform.position, Is.EqualTo(session.Map.CellToWorld(destination))); Assert.That(actor.HasSegment, Is.False);
            Assert.That(session.Npcs.Actors.Count, Is.EqualTo(2), "Replacement epoch must not cancel newly bound actors on the following frame.");
        }

        [UnityTest]
        public IEnumerator InvalidLoadAndBlockedTransferPreserveStateMotionPendingSaveAndRequestQueue()
        {
            NpcActor actor = StartMoving(); Assert.That(session.Save(2), Is.True); QueueNotice();
            GameState live = session.State; string durable = JsonUtility.ToJson(live);
            Vector3 player = session.Player.transform.position, pose = actor.transform.position;
            Vector2Int destination = actor.ReservedDestination; int epoch = session.ExecutionEpoch;
            GameState invalid = Clone(live); NpcState bad = invalid.GetNpcState(first.mapId, "a"); bad.routeId = "removed_route"; invalid.SetNpcState(bad, false);
            session.SaveSlots.Write(0, new SaveData { state = invalid, mapId = first.mapId, position = player });
            LogAssert.Expect(LogType.Warning, new Regex("Load failed:")); Assert.That(session.Load(0), Is.False);
            AssertLive(live, durable, pose, destination, player, epoch);
            var blocked = Asset<MapDefinition>(); blocked.mapId = "no_player_room"; blocked.Resize(1, 1); blocked.Fill(floor);
            blocked.events.Add(new MapEventPlacement { id = "keeper", npc = keeper, cell = Vector2Int.zero });
            Assert.That(session.TransferPlayer(blocked, Vector2Int.zero), Is.False);
            AssertLive(live, durable, pose, destination, player, epoch);
            Assert.That(session.Maps, Has.No.Member(blocked), "A rejected transfer cannot register content as a side effect.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapRoundTripAndLoadKeepFacingOverridesAndSeparateSharedDefinitionPlacements()
        {
            Assert.That(session.Npcs.TryFace("a", 275, true), Is.True);
            GameState live = session.State;
            Assert.That(session.TransferPlayer(second, second.spawnCell), Is.True); session.UI.ShowStatus();
            Assert.That(session.State, Is.SameAs(live)); Assert.That(session.Npcs.TryFace("a", 35, true), Is.True);
            Assert.That(session.Save(1), Is.True);
            Assert.That(session.TransferPlayer(first, first.spawnCell), Is.True); session.UI.ShowStatus();
            Assert.That(session.Npcs.Find("a").Yaw, Is.EqualTo(275)); Assert.That(session.Npcs.Find("a").PersistentOverride, Is.True);
            Assert.That(session.Load(1), Is.True); session.UI.ShowStatus(); yield return null;
            Assert.That(session.Map.Map, Is.SameAs(second)); Assert.That(session.Npcs.Find("a").Yaw, Is.EqualTo(35));
            Assert.That(session.State.GetNpcState(first.mapId, "a").yaw, Is.EqualTo(275));
            Assert.That(session.State.GetNpcState(second.mapId, "a").routeId, Is.Empty);
            Assert.That(session.Npcs.Find("a").HasSegment, Is.False);
        }

        [UnityTest]
        public IEnumerator TransferInsideForegroundProgramKeepsCapturedStateForFollowingCommands()
        {
            GameState live = session.State; int epoch = session.ExecutionEpoch;
            var definition = Asset<EventDefinition>(); definition.pages.Add(new EventPage { commands = new List<EventCommand> {
                new EventCommand { type = EventCommandType.TransferPlayer, map = second, cell = second.spawnCell },
                new EventCommand { type = EventCommandType.SetVariable, key = "after_transfer", intValue = 7 },
                new EventCommand { type = EventCommandType.End } } }); definition.EnsureStableIds();
            Assert.That(session.Events.TryRun(definition, first.mapId + "/transfer_test"), Is.True);
            Assert.That(session.Map.Map, Is.SameAs(second)); Assert.That(session.State, Is.SameAs(live));
            for (int frame = 0; frame < 10 && session.Events.IsRunning; frame++) yield return null;
            Assert.That(session.Events.IsRunning, Is.False, "The foreground program must finish after its transfer yield.");
            Assert.That(session.State, Is.SameAs(live));
            Assert.That(session.ExecutionEpoch, Is.EqualTo(epoch)); Assert.That(session.State.GetVariable("after_transfer"), Is.EqualTo(7));
            Assert.That(session.Events.CurrentContext.Succeeded, Is.True); session.UI.ShowStatus(); yield return null;
        }

        [UnityTest]
        public IEnumerator FailedSavePreservesFileAndLiveNpcCheckpointAndNewGameCancelsQueuedSave()
        {
            Assert.That(session.Save(0), Is.True); string file = File.ReadAllText(session.SavePath(0));
            string before = JsonUtility.ToJson(session.State); Vector3 pose = session.Npcs.Find("a").transform.position;
            route.points[1].cell = new Vector2Int(99, 99);
            LogAssert.Expect(LogType.Warning, new Regex("Save failed:")); Assert.That(session.Save(0), Is.False);
            Assert.That(File.ReadAllText(session.SavePath(0)), Is.EqualTo(file)); Assert.That(JsonUtility.ToJson(session.State), Is.EqualTo(before));
            Assert.That(session.Npcs.Find("a").transform.position, Is.EqualTo(pose)); route.points[1].cell = new Vector2Int(3, 1);
            StartMoving(); Assert.That(session.Save(2), Is.True); QueueNotice();
            session.State.SetVariable("old_progress", 10); Assert.That(session.NewGame(), Is.True); session.UI.ShowStatus();
            Assert.That(session.HasPendingSave, Is.False); Assert.That(session.SaveStatus, Is.EqualTo(SaveRequestStatus.Cancelled));
            Assert.That(session.Scheduler.QueuedCount, Is.Zero); Assert.That(session.State.GetVariable("old_progress"), Is.Zero);
            Assert.That(session.Npcs.Find("a").Cell, Is.EqualTo(new Vector2Int(1, 1))); Assert.That(session.Npcs.Find("a").HasSegment, Is.False);
            yield return null; yield return null;
            Assert.That(File.Exists(session.SavePath(2)), Is.False); Assert.That(File.ReadAllText(session.SavePath(0)), Is.EqualTo(file));
        }

        [UnityTest]
        public IEnumerator LateCommitFailureRestoresNpcInterpolationQueueAndPendingSave()
        {
            Assert.That(session.Save(0), Is.True); NpcActor actor = StartMoving(); Assert.That(session.Save(2), Is.True); QueueNotice();
            GameState live = session.State; string durable = JsonUtility.ToJson(live);
            Vector3 player = session.Player.transform.position, pose = actor.transform.position;
            Vector2Int destination = actor.ReservedDestination; int epoch = session.ExecutionEpoch;
            bool fail = true; session.StateReplaced += () => { if (fail) { fail = false; throw new InvalidOperationException("Injected late commit failure"); } };
            LogAssert.Expect(LogType.Warning, new Regex("Load failed: Injected late commit failure"));
            Assert.That(session.Load(0), Is.False); AssertLive(live, durable, pose, destination, player, epoch);
            session.Npcs.Tick(0.1f); Assert.That(session.Npcs.Find("a").transform.position.x, Is.GreaterThan(pose.x), "The restored segment must still drain normally.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingSessionCancelsPendingSaveAndReactivationBindsOnlyFreshReservations()
        {
            StartMoving(); Assert.That(session.Save(2), Is.True); session.enabled = false;
            Assert.That(session.HasPendingSave, Is.False); Assert.That(session.SaveStatus, Is.EqualTo(SaveRequestStatus.Cancelled));
            Assert.That(session.Npcs.Actors.Count, Is.Zero); session.enabled = true; session.UI.ShowStatus();
            Assert.That(session.Npcs.Actors.Count, Is.EqualTo(2)); Assert.That(session.Npcs.HasActiveSegments, Is.False);
            yield return null; yield return null; Assert.That(File.Exists(session.SavePath(2)), Is.False);
        }

        NpcActor StartMoving()
        {
            session.UI.CloseMenus(); NpcActor actor = session.Npcs.Find("a");
            for (int pump = 0; pump < 4 && !actor.HasSegment; pump++) session.Npcs.Tick(0);
            Assert.That(actor.HasSegment, Is.True); session.Npcs.Tick(0.2f); session.UI.ShowStatus();
            Assert.That(actor.transform.position, Is.Not.EqualTo(session.Map.CellToWorld(actor.Cell))); return actor;
        }
        void QueueNotice()
        {
            var definition = Asset<EventDefinition>(); definition.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.Wait, seconds = 10 } } });
            definition.EnsureStableIds(); Assert.That(session.Scheduler.EnqueueForeground(definition, first.mapId + "/queued"), Is.True);
            Assert.That(session.Scheduler.QueuedCount, Is.EqualTo(1));
        }
        void AssertLive(GameState state, string durable, Vector3 npcPose, Vector2Int destination, Vector3 player, int epoch)
        {
            Assert.That(session.State, Is.SameAs(state)); Assert.That(JsonUtility.ToJson(session.State), Is.EqualTo(durable));
            Assert.That(session.Map.Map, Is.SameAs(first)); Assert.That(session.Player.transform.position, Is.EqualTo(player));
            NpcActor actor = session.Npcs.Find("a"); Assert.That(actor.transform.position, Is.EqualTo(npcPose));
            Assert.That(actor.HasSegment, Is.True); Assert.That(actor.ReservedDestination, Is.EqualTo(destination));
            Assert.That(session.ExecutionEpoch, Is.EqualTo(epoch)); Assert.That(session.HasPendingSave, Is.True);
            Assert.That(session.PendingSaveSlot, Is.EqualTo(2)); Assert.That(session.SaveStatus, Is.EqualTo(SaveRequestStatus.Pending));
            Assert.That(session.Npcs.IsSaveDrainRequested, Is.True); Assert.That(session.Scheduler.QueuedCount, Is.EqualTo(1));
        }
        static GameState Clone(GameState value) { GameState result = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(value)); result.Normalize(); return result; }
        MapDefinition CreateMap(string id) { var map = Asset<MapDefinition>(); map.mapId = id; map.Resize(7, 7); map.Fill(floor); map.spawnCell = new Vector2Int(3, 3); return map; }
        T Asset<T>() where T : ScriptableObject { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
    }
}
