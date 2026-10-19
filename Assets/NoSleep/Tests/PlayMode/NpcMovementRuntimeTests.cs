using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class NpcMovementRuntimeTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        GameObject host;
        GameSession session;
        TileMapRuntime runtime;
        NpcMovementService service;
        PlayerController player;
        MapDefinition map;
        T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        void Set(string property, object value) => typeof(GameSession).GetProperty(property).GetSetMethod(true).Invoke(session, new[] { value });
        void Create(int width = 8, int height = 6)
        {
            map = Asset<MapDefinition>(); map.mapId = "movement"; map.Resize(width, height); map.Fill(Asset<TileDefinition>());
            host = new GameObject("Isolated NPC movement"); host.SetActive(false); session = host.AddComponent<GameSession>();
            var mapHost = new GameObject("Map"); mapHost.transform.SetParent(host.transform); runtime = mapHost.AddComponent<TileMapRuntime>();
            var playerHost = new GameObject("Player"); playerHost.transform.SetParent(host.transform); player = playerHost.AddComponent<PlayerController>(); player.Configure(session); player.transform.position = Vector3.zero;
            service = host.AddComponent<NpcMovementService>(); service.Configure(session);
            Set(nameof(GameSession.Map), runtime); Set(nameof(GameSession.Player), player); Set(nameof(GameSession.Npcs), service); runtime.Configure(session);
        }
        NpcDefinition Npc(string id, Vector2Int cell, NpcRouteDefinition route = null)
        {
            var npc = Asset<NpcDefinition>(); npc.npcId = "definition_" + id; npc.speed = 1; npc.route = route; npc.maxBlockedRetries = 2; npc.retryInterval = .05f;
            map.events.Add(new MapEventPlacement { id = id, npc = npc, cell = cell }); return npc;
        }
        NpcRouteDefinition Route(params Vector2Int[] cells)
        {
            var route = Asset<NpcRouteDefinition>(); route.EnsureStableIds(); foreach (var cell in cells) route.points.Add(new NpcRoutePoint { cell = cell }); return route;
        }
        IEnumerator Pump(IEnumerator operation, int limit = 100)
        {
            for (int i = 0; i < limit; i++) { if (!operation.MoveNext()) { (operation as IDisposable)?.Dispose(); yield break; } service.Tick(.25f); yield return null; }
            (operation as IDisposable)?.Dispose(); Assert.Fail("NPC operation exceeded the bounded pump budget.");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { service?.CancelAll(); if (host != null) Object.Destroy(host); yield return null; foreach (var asset in assets) if (asset != null) Object.Destroy(asset); assets.Clear(); yield return null; }

        [UnityTest] public IEnumerator SegmentReservesBothCellsAndSelfExclusionPreservesWalkability()
        {
            Create(); Npc("a", new Vector2Int(2, 2)); runtime.Load(map); var actor = service.Find("a");
            var operation = service.MoveTo("a", new Vector2Int(3, 2), true); Assert.That(operation.MoveNext(), Is.True); service.Tick(.01f);
            Assert.That(actor.HasSegment, Is.True); Assert.That(service.IsCellOccupied(new Vector2Int(2, 2)), Is.True); Assert.That(service.IsCellOccupied(new Vector2Int(3, 2)), Is.True);
            Assert.That(runtime.IsWalkable(new Vector2Int(2, 2)), Is.False); Assert.That(runtime.IsWalkable(new Vector2Int(3, 2)), Is.False);
            Assert.That(runtime.IsWalkable(new Vector2Int(2, 2), actor), Is.True); Assert.That(runtime.IsWalkable(new Vector2Int(3, 2), actor), Is.True);
            (operation as IDisposable).Dispose(); Assert.That(service.IsAtCellBoundary, Is.True); Assert.That(actor.Cell, Is.EqualTo(new Vector2Int(2, 2))); Assert.That(service.IsCellOccupied(new Vector2Int(3, 2)), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator TwoNpcSwapCannotOverlapAndReturnsBoundedFailure()
        {
            Create(5, 1); Npc("a", new Vector2Int(2, 0)); Npc("b", new Vector2Int(3, 0)); runtime.Load(map);
            var left = NpcMoveResult.Busy; var right = NpcMoveResult.Busy;
            var a = service.MoveTo("a", new Vector2Int(3, 0), false, result => left = result); var b = service.MoveTo("b", new Vector2Int(2, 0), false, result => right = result);
            bool aliveA = true, aliveB = true;
            for (int i = 0; i < 30 && (aliveA || aliveB); i++) { if (aliveA) aliveA = a.MoveNext(); if (aliveB) aliveB = b.MoveNext(); service.Tick(.1f); yield return null; }
            (a as IDisposable)?.Dispose(); (b as IDisposable)?.Dispose(); Assert.That(aliveA || aliveB, Is.False);
            Assert.That(left, Is.EqualTo(NpcMoveResult.Blocked)); Assert.That(right, Is.EqualTo(NpcMoveResult.Blocked));
            Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(2, 0))); Assert.That(service.Find("b").Cell, Is.EqualTo(new Vector2Int(3, 0))); Assert.That(service.IsAtCellBoundary, Is.True);
        }
        [UnityTest] public IEnumerator SaveDrainCompletesCurrentSegmentAndFreezesNewRouteSteps()
        {
            Create(); var route = Route(new Vector2Int(2, 2), new Vector2Int(5, 2)); Npc("a", new Vector2Int(2, 2), route); runtime.Load(map);
            service.Tick(.1f); service.Tick(.1f); service.Tick(.1f); var actor = service.Find("a"); Assert.That(actor.HasSegment, Is.True);
            service.BeginSaveDrain(); service.Tick(10); Assert.That(actor.Cell, Is.EqualTo(new Vector2Int(3, 2))); Assert.That(service.IsAtCellBoundary, Is.True);
            for (int i = 0; i < 5; i++) service.Tick(10); Assert.That(actor.Cell, Is.EqualTo(new Vector2Int(3, 2)));
            var detached = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(session.State)); service.SnapshotInto(detached);
            Assert.That(detached.GetNpcState(map.mapId, "a").cell, Is.EqualTo(new Vector2Int(3, 2)));
            service.EndSaveDrain(); service.Tick(.1f); Assert.That(actor.HasSegment, Is.True); yield return null;
        }
        [UnityTest] public IEnumerator RuntimeRollbackRestoresSegmentPoseReservationsAndDetachedSavedRecords()
        {
            Create(); Npc("a", new Vector2Int(2, 2), Route(new Vector2Int(2, 2), new Vector2Int(5, 2))); runtime.Load(map);
            service.Tick(.1f); service.Tick(.1f); service.Tick(.2f); var actor = service.Find("a"); Assert.That(actor.HasSegment, Is.True);
            object snapshot = service.CaptureRuntime(); Vector3 pose = actor.transform.position; var source = actor.Cell; var destination = actor.ReservedDestination;
            session.State.SetNpcState(new NpcState { mapId = "other", placementId = "new", npcId = "new", cell = Vector2Int.one });
            runtime.Load(map); service.RestoreRuntime(snapshot); actor = service.Find("a");
            Assert.That(actor.transform.position, Is.EqualTo(pose)); Assert.That(actor.HasSegment, Is.True); Assert.That(actor.Cell, Is.EqualTo(source)); Assert.That(actor.ReservedDestination, Is.EqualTo(destination));
            Assert.That(service.IsCellOccupied(source), Is.True); Assert.That(service.IsCellOccupied(destination), Is.True); Assert.That(session.State.GetNpcState("other", "new"), Is.Null); yield return null;
        }
        [UnityTest] public IEnumerator PersistentMoveWinsOverScheduleAndResumeRestoresAutomaticRoute()
        {
            Create(); var route = Route(new Vector2Int(2, 2), new Vector2Int(4, 2)); Npc("a", new Vector2Int(2, 2), route); runtime.Load(map);
            NpcMoveResult result = NpcMoveResult.Busy; yield return Pump(service.MoveTo("a", new Vector2Int(2, 3), true, r => result = r));
            Assert.That(result, Is.EqualTo(NpcMoveResult.Succeeded)); session.State.survival.elapsedMinutes = 100; service.Tick(10);
            Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(2, 3))); Assert.That(service.Find("a").PersistentOverride, Is.True);
            Assert.That(service.Resume("a"), Is.True); service.Tick(.1f); Assert.That(service.Find("a").HasSegment, Is.True); yield return null;
        }
        [UnityTest] public IEnumerator OccupiedTimeJumpKeepsSourceAndStopsAfterRetryBudget()
        {
            Create(); var npc = Npc("a", new Vector2Int(2, 2)); npc.schedules.Add(new NpcScheduleEntry { scheduleId = "late", startMinute = 540, endMinute = 1440, stationCell = new Vector2Int(4, 2) });
            Npc("b", new Vector2Int(4, 2)); runtime.Load(map); session.State.survival.elapsedMinutes = 120;
            for (int i = 0; i < 12; i++) service.Tick(.1f); var actor = service.Find("a");
            Assert.That(actor.Cell, Is.EqualTo(new Vector2Int(2, 2))); Assert.That(actor.Status, Is.EqualTo("Blocked")); Assert.That(actor.ScheduleId, Is.Empty); Assert.That(actor.LastEvaluatedMinutes, Is.Zero); Assert.That(service.IsAtCellBoundary, Is.True); yield return null;
        }
        [UnityTest] public IEnumerator LineOfSightIsBlockedByWallsAndTouchingDiagonalCorners()
        {
            Create(); Npc("a", new Vector2Int(2, 2)); var wall = Asset<TileDefinition>(); wall.walkable = false; map.SetTile(new Vector2Int(3, 2), wall); runtime.Load(map);
            Assert.That(service.HasLineOfSight(new Vector2Int(2, 2), new Vector2Int(4, 2)), Is.False);
            Assert.That(service.HasLineOfSight(new Vector2Int(2, 2), new Vector2Int(3, 3)), Is.False);
            Assert.That(service.HasLineOfSight(new Vector2Int(2, 2), new Vector2Int(2, 4)), Is.True); yield return null;
        }
        [UnityTest] public IEnumerator MapChangeCancelsOldOperationWithoutWritingOldNpcIntoNewState()
        {
            Create(); Npc("a", new Vector2Int(2, 2)); runtime.Load(map); var result = NpcMoveResult.Busy;
            var operation = service.MoveTo("a", new Vector2Int(5, 2), true, r => result = r); Assert.That(operation.MoveNext(), Is.True); service.Tick(.1f);
            var replacement = new GameState(); Set(nameof(GameSession.State), replacement); service.OnMapChanging(); Assert.That(operation.MoveNext(), Is.False);
            Assert.That(result, Is.EqualTo(NpcMoveResult.Cancelled)); Assert.That(replacement.npcStates, Is.Empty); Assert.That(service.IsAtCellBoundary, Is.True); yield return null;
        }

        EventExecutionContext PausedContext(EventCommand command, out EventRunner runner, out EventScheduler scheduler)
        {
            runner = host.AddComponent<EventRunner>(); runner.Configure(session, null); Set(nameof(GameSession.Events), runner);
            scheduler = host.AddComponent<EventScheduler>(); scheduler.Configure(session); Set(nameof(GameSession.Scheduler), scheduler); scheduler.SetPaused(true);
            var definition = Asset<EventDefinition>(); definition.pages.Add(new EventPage { commands = new List<EventCommand> { command,
                new EventCommand { type = EventCommandType.AddGold, intValue = 99 } } }); definition.EnsureStableIds();
            var context = runner.CreateExecution(definition, map.mapId + "/a");
            typeof(EventRunner).GetProperty(nameof(EventRunner.CurrentContext)).GetSetMethod(true).Invoke(runner, new object[] { context });
            typeof(EventRunner).GetProperty(nameof(EventRunner.IsRunning)).GetSetMethod(true).Invoke(runner, new object[] { true });
            return context;
        }

        [UnityTest] public IEnumerator DebuggerStepCompletesOnlyItsOwnedMoveAndDoesNotRunFollowingReward()
        {
            Create(); Npc("a", new Vector2Int(2, 2)); Npc("b", new Vector2Int(2, 4), Route(new Vector2Int(2, 4), new Vector2Int(5, 4))); runtime.Load(map);
            service.Tick(.1f); service.Tick(.1f); Assert.That(service.Find("b").HasSegment, Is.True); Vector3 pausedBackground = service.Find("b").transform.position;
            using (var context = PausedContext(new EventCommand { type = EventCommandType.MoveNpc, key = "a", cell = new Vector2Int(3, 2), boolValue = true }, out var runner, out var scheduler))
            {
                Assert.That(context.MoveNext(out _), Is.True); service.Tick(10); Assert.That(service.Find("a").HasSegment, Is.False);
                context.Step(); Assert.That(context.MoveNext(out _), Is.True); service.Tick(.1f); service.Tick(1);
                context.MoveNext(out _);
                Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(3, 2))); Assert.That(service.Find("b").transform.position, Is.EqualTo(pausedBackground));
                Assert.That(context.ExecutedInstructions, Is.EqualTo(1)); Assert.That(session.State.player.gold, Is.Zero); Assert.That(context.IsComplete, Is.False);
                context.Cancel(); runner.Cancel(); scheduler.SetPaused(false);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator SteppedFaceFinishesExistingSegmentAndRestoresAutomaticRoute()
        {
            Create(); Npc("a", new Vector2Int(2, 2), Route(new Vector2Int(2, 2), new Vector2Int(5, 2))); runtime.Load(map); service.Tick(.1f); service.Tick(.1f);
            Assert.That(service.Find("a").HasSegment, Is.True);
            using (var context = PausedContext(new EventCommand { type = EventCommandType.FaceNpc, key = "a", intValue = 135, boolValue = false }, out var runner, out var scheduler))
            {
                context.Step(); context.MoveNext(out _); service.Tick(1); context.MoveNext(out _);
                Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(3, 2))); Assert.That(service.Find("a").Yaw, Is.EqualTo(135));
                Assert.That(service.Find("a").PersistentOverride, Is.False); Assert.That(session.State.player.gold, Is.Zero);
                context.Cancel(); runner.Cancel(); scheduler.SetPaused(false); service.Tick(.1f); Assert.That(service.Find("a").HasSegment, Is.True);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator DestinationPageChangingDuringSegmentRollsBackAndFailsExplicitMove()
        {
            Create(); Npc("a", new Vector2Int(2, 2)); var definition = Asset<EventDefinition>();
            definition.pages.Add(new EventPage { visible = false, blocksMovement = false });
            definition.pages.Add(new EventPage { conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.Switch, key = "closed" } } }); definition.EnsureStableIds();
            map.events.Add(new MapEventPlacement { id = "door", definition = definition, cell = new Vector2Int(3, 2) }); runtime.Load(map);
            var result = NpcMoveResult.Busy; var operation = service.MoveTo("a", new Vector2Int(3, 2), true, r => result = r);
            Assert.That(operation.MoveNext(), Is.True); service.Tick(.1f); service.Tick(.2f); Assert.That(service.Find("a").HasSegment, Is.True);
            session.State.SetSwitch("closed", true);
            // The fixture is inactive; reconfigure mirrors the active object's Changed subscription.
            runtime.EventObjects[0].Configure(session, definition, map.mapId + "/door");
            service.Tick(10); Assert.That(operation.MoveNext(), Is.False);
            Assert.That(result, Is.EqualTo(NpcMoveResult.Blocked)); Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(service.IsAtCellBoundary, Is.True); Assert.That(service.IsCellOccupied(new Vector2Int(3, 2)), Is.False); yield return null;
        }

        [UnityTest] public IEnumerator ModalPausesRouteButSaveDrainFinishesCurrentSegment()
        {
            Create(); Npc("a", new Vector2Int(2, 2), Route(new Vector2Int(2, 2), new Vector2Int(5, 2))); runtime.Load(map); service.Tick(.1f); service.Tick(.1f); service.Tick(.2f);
            var ui = host.AddComponent<GameUI>(); Set(nameof(GameSession.UI), ui);
            typeof(GameUI).GetField("dialogueOpen", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, true);
            Vector3 pose = service.Find("a").transform.position; service.Tick(10); Assert.That(service.Find("a").transform.position, Is.EqualTo(pose));
            service.BeginSaveDrain(); service.Tick(10); Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(3, 2))); Assert.That(service.IsAtCellBoundary, Is.True);
            service.EndSaveDrain(); service.Tick(10); Assert.That(service.Find("a").HasSegment, Is.False); yield return null;
        }

        [UnityTest] public IEnumerator DestroyingOnlyMapCancelsServiceAndPendingOperation()
        {
            Create(); Npc("a", new Vector2Int(2, 2)); runtime.Load(map); var result = NpcMoveResult.Busy;
            var operation = service.MoveTo("a", new Vector2Int(4, 2), false, r => result = r); operation.MoveNext(); service.Tick(.1f);
            runtime.transform.SetParent(null); // Unity sends OnDestroy to components which have been active.
            Object.Destroy(runtime.gameObject); yield return null; Assert.That(service.Actors, Is.Empty); Assert.That(operation.MoveNext(), Is.False);
            Assert.That(result, Is.EqualTo(NpcMoveResult.Cancelled)); Assert.That(service.IsAtCellBoundary, Is.True); service.Tick(10);
        }

        [UnityTest] public IEnumerator BlockedStationResumeSurvivesSavedCloneAndReturnsAfterObstacleLeaves()
        {
            Create(); var npc = Npc("a", new Vector2Int(2, 2));
            npc.schedules.Add(new NpcScheduleEntry { scheduleId = "station", stationCell = new Vector2Int(4, 2) });
            Npc("b", new Vector2Int(4, 2)); runtime.Load(map);
            Assert.That(service.TryFace("a", 90, true), Is.True); Assert.That(service.Find("a").PersistentOverride, Is.True);
            Assert.That(service.Resume("a"), Is.True);
            for (int i = 0; i < 8; i++) service.Tick(.1f);
            Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(2, 2))); Assert.That(service.Find("a").Status, Is.EqualTo("Blocked"));
            var saved = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(session.State)); service.SnapshotInto(saved);
            var checkpoint = saved.GetNpcState(map.mapId, "a"); Assert.That(checkpoint.persistentOverride, Is.False); Assert.That(checkpoint.scheduleId, Is.Empty);
            service.OnMapChanging(); Set(nameof(GameSession.State), saved); runtime.Load(map);
            for (int i = 0; i < 8; i++) service.Tick(.1f); Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(2, 2)));
            yield return Pump(service.MoveTo("b", new Vector2Int(5, 2), true));
            // A subsequent load retries the durable, unfinished station placement.
            var retryState = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(session.State)); service.SnapshotInto(retryState);
            service.OnMapChanging(); Set(nameof(GameSession.State), retryState); runtime.Load(map);
            Assert.That(service.Find("a").Cell, Is.EqualTo(new Vector2Int(4, 2))); Assert.That(service.Find("a").ScheduleId, Is.EqualTo("station"));
            Assert.That(service.Find("a").PersistentOverride, Is.False); Assert.That(session.State.GetNpcState(map.mapId, "a").scheduleId, Is.EqualTo("station")); yield return null;
        }
    }
}
