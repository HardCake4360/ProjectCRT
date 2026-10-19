using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class NpcMovementTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        MapDefinition Map(int width = 6, int height = 6)
        {
            var map = Asset<MapDefinition>(); map.mapId = "paths"; map.Resize(width, height); map.Fill(Asset<TileDefinition>()); return map;
        }
        NpcRouteDefinition Route(params Vector2Int[] cells)
        {
            var route = Asset<NpcRouteDefinition>(); route.EnsureStableIds();
            foreach (var cell in cells) route.points.Add(new NpcRoutePoint { cell = cell });
            return route;
        }
        [TearDown] public void Cleanup() { foreach (var asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }

        [Test] public void PathIsDeterministicFourWayAndAvoidsOccupiedCells()
        {
            var map = Map();
            Assert.That(NpcPathfinding.TryFindPath(map, Vector2Int.one, new Vector2Int(3, 1), c => c == new Vector2Int(2, 1), 100, out var path), Is.True);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(3, 1) }, path);
            Vector2Int previous = Vector2Int.one;
            foreach (var cell in path) { Assert.That(Mathf.Abs(cell.x - previous.x) + Mathf.Abs(cell.y - previous.y), Is.EqualTo(1)); previous = cell; }
        }
        [Test] public void SearchBudgetAndOccupiedDestinationFailWithoutPartialPath()
        {
            var map = Map();
            Assert.That(NpcPathfinding.TryFindPath(map, Vector2Int.zero, new Vector2Int(5, 5), null, 3, out var path), Is.False);
            Assert.That(path, Is.Empty);
            Assert.That(NpcPathfinding.TryFindPath(map, Vector2Int.zero, Vector2Int.right, c => c == Vector2Int.right, 100, out path), Is.False);
            Assert.That(path, Is.Empty);
        }
        [Test] public void StableIdsSurviveReorderAndReadonlyValidationDoesNotIssueIds()
        {
            var npc = Asset<NpcDefinition>(); npc.schedules.Add(new NpcScheduleEntry()); npc.schedules.Add(new NpcScheduleEntry());
            Assert.That(npc.Validate(), Is.Not.Empty); Assert.That(npc.npcId, Is.Null); Assert.That(npc.schedules[0].scheduleId, Is.Null);
            npc.EnsureStableIds(); string id = npc.npcId, first = npc.schedules[0].scheduleId;
            npc.schedules.Reverse(); Assert.That(npc.EnsureStableIds(), Is.False);
            Assert.That(npc.npcId, Is.EqualTo(id)); Assert.That(npc.schedules[1].scheduleId, Is.EqualTo(first));
        }
        [Test] public void MidnightScheduleUsesPreviousDayAndLastMatchingConditionsWin()
        {
            var npc = Asset<NpcDefinition>(); var state = new GameState(); state.survival.startHour = 0; state.survival.initialized = true; state.survival.elapsedMinutes = 1440 + 30;
            var night = new NpcScheduleEntry { firstDay = 1, lastDay = 1, startMinute = 23 * 60, endMinute = 60 };
            var gated = new NpcScheduleEntry { firstDay = 1, startMinute = 0, endMinute = 1440, conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.Switch, key = "work" } } };
            npc.schedules.Add(night); npc.schedules.Add(gated); npc.EnsureStableIds();
            Assert.That(npc.ResolveSchedule(state, "paths/npc", null), Is.SameAs(night));
            state.SetSwitch("work", true); Assert.That(npc.ResolveSchedule(state, "paths/npc", null), Is.SameAs(gated));
            state.survival.elapsedMinutes = 2 * 1440 + 30; state.SetSwitch("work", false); Assert.That(npc.ResolveSchedule(state, "paths/npc", null), Is.Null);
        }
        [Test] public void BindingRetainsProgressAndPersistentOverrideAcrossLargeTimeJump()
        {
            var map = Map(); var npc = Asset<NpcDefinition>(); npc.route = Route(new Vector2Int(1, 1), new Vector2Int(4, 1)); npc.EnsureStableIds();
            var placement = new MapEventPlacement { id = "worker", cell = Vector2Int.one, npc = npc }; map.events.Add(placement);
            var state = new GameState(); state.survival.elapsedMinutes = 10;
            state.SetNpcState(new NpcState { mapId = map.mapId, placementId = placement.id, npcId = npc.npcId, cell = new Vector2Int(3, 1), routeId = npc.route.routeId, waypointIndex = 1, lastEvaluatedMinutes = 9 });
            var resolved = NpcMovementService.ResolveBindingState(state, map, placement, null);
            Assert.That(resolved.cell, Is.EqualTo(new Vector2Int(3, 1))); Assert.That(resolved.waypointIndex, Is.EqualTo(1));
            var saved = state.GetNpcState(map.mapId, placement.id); saved.persistentOverride = true; state.SetNpcState(saved); state.survival.elapsedMinutes = 200;
            resolved = NpcMovementService.ResolveBindingState(state, map, placement, null);
            Assert.That(resolved.cell, Is.EqualTo(saved.cell)); Assert.That(resolved.lastEvaluatedMinutes, Is.EqualTo(9));
        }
        [Test] public void OccupiedScheduleStartPreservesPoseAndCheckpointWithoutMutatingState()
        {
            var map = Map(); var npc = Asset<NpcDefinition>(); npc.EnsureStableIds();
            var schedule = new NpcScheduleEntry { scheduleId = "shift", stationCell = new Vector2Int(4, 4) }; npc.schedules.Add(schedule);
            var placement = new MapEventPlacement { id = "worker", cell = Vector2Int.one, npc = npc }; map.events.Add(placement);
            var state = new GameState(); state.survival.elapsedMinutes = 100;
            state.SetNpcState(new NpcState { mapId = map.mapId, placementId = placement.id, npcId = npc.npcId, cell = Vector2Int.one, yaw = 135, lastEvaluatedMinutes = 10 });
            string before = JsonUtility.ToJson(state);
            var blocked = NpcMovementService.ResolveBindingState(state, map, placement, null, c => c == schedule.stationCell);
            Assert.That(blocked.cell, Is.EqualTo(Vector2Int.one)); Assert.That(blocked.yaw, Is.EqualTo(135)); Assert.That(blocked.scheduleId, Is.Empty); Assert.That(blocked.lastEvaluatedMinutes, Is.EqualTo(10));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
            var free = NpcMovementService.ResolveBindingState(state, map, placement, null, c => false);
            Assert.That(free.cell, Is.EqualTo(schedule.stationCell)); Assert.That(free.scheduleId, Is.EqualTo("shift")); Assert.That(free.lastEvaluatedMinutes, Is.EqualTo(100));
        }
        [Test] public void HiddenDefeatedPageMakesPlacementDormantAndNpcOnlyRemainsActive()
        {
            var map = Map(); var npc = Asset<NpcDefinition>(); npc.EnsureStableIds(); var definition = Asset<EventDefinition>();
            definition.pages.Add(new EventPage()); definition.pages.Add(new EventPage { visible = false, blocksMovement = false, conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.SelfSwitch, key = "defeated" } } }); definition.EnsureStableIds();
            var placement = new MapEventPlacement { id = "enemy", npc = npc, definition = definition }; var state = new GameState();
            Assert.That(NpcMovementService.IsPlacementActive(state, map, placement), Is.True);
            state.SetSelfSwitch(map.mapId + "/enemy", "defeated", true); Assert.That(NpcMovementService.IsPlacementActive(state, map, placement), Is.False);
            placement.definition = null; Assert.That(NpcMovementService.IsPlacementActive(state, map, placement), Is.True);
        }
        [Test] public void MapValidatesNpcOnlyPlacementAndEveryReferencedRouteAgainstTerrain()
        {
            var map = Map(); var npc = Asset<NpcDefinition>(); npc.EnsureStableIds(); npc.route = Route(Vector2Int.one, new Vector2Int(99, 99));
            map.events.Add(new MapEventPlacement { id = "npc_only", npc = npc, cell = Vector2Int.one });
            var errors = map.Validate(); Assert.That(errors.Exists(error => error.Contains("99")), Is.True);
            Assert.That(errors.Exists(error => error.Contains("정의를 지정")), Is.False);
        }
    }
}
