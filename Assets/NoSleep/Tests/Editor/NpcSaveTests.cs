using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class NpcSaveTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        MapDefinition map;
        NpcDefinition npc;
        NpcRouteDefinition route;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            var floor = Asset<TileDefinition>();
            map = Asset<MapDefinition>(); map.mapId = "npc_map"; map.Resize(5, 5); map.Fill(floor);
            route = Asset<NpcRouteDefinition>(); route.routeId = "patrol";
            route.points.Add(new NpcRoutePoint { cell = new Vector2Int(1, 1), yaw = 90 });
            route.points.Add(new NpcRoutePoint { cell = new Vector2Int(3, 1), yaw = 180 });
            npc = Asset<NpcDefinition>(); npc.npcId = "keeper"; npc.route = route;
            npc.schedules.Add(new NpcScheduleEntry { scheduleId = "night", startMinute = 1200, endMinute = 1440, stationCell = new Vector2Int(3, 3) });
            map.events.Add(new MapEventPlacement { id = "a", npc = npc, cell = new Vector2Int(1, 1) });
            state = new GameState(); state.survival.elapsedMinutes = 60;
        }

        [TearDown]
        public void TearDown() { foreach (ScriptableObject asset in assets) if (asset != null) Object.DestroyImmediate(asset); assets.Clear(); }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void LiteralLegacySchemasIntroduceNoNpcRecordsAndKeepTheirProgress(int version)
        {
            string json = "{\"version\":" + version + ",\"mapId\":\"npc_map\",\"position\":{\"x\":2,\"y\":0,\"z\":2},\"yaw\":45," +
                "\"state\":{\"player\":{\"name\":\"Old\",\"maxHealth\":100,\"health\":73,\"maxSanity\":100,\"sanity\":61,\"gold\":9,\"strength\":10}," +
                "\"switches\":[{\"key\":\"door\",\"value\":true}],\"selfSwitches\":[{\"key\":\"npc_map/a:A\",\"value\":true}],\"variables\":[{\"key\":\"chapter\",\"value\":2}],\"inventory\":[{\"key\":\"key\",\"value\":1}]," +
                "\"survival\":{\"elapsedMinutes\":60,\"fatigue\":12,\"initialized\":true,\"startHour\":8,\"baselineMaxHealth\":100,\"baselineStrength\":12}," +
                "\"journey\":{\"clueIds\":[\"ledger\"],\"objectiveId\":\"station\",\"objectiveTitle\":\"Station\"}," +
                "\"committedTransactions\":[\"old/grant\"],\"eventCheckpoints\":[{\"scope\":\"old/page\",\"checkpointId\":\"wait\",\"dueElapsedMinutes\":90}]}}";
            SaveData loaded = SaveService.Deserialize(json);
            Assert.That(loaded.version, Is.EqualTo(4)); Assert.That(loaded.sourceVersion, Is.EqualTo(version));
            Assert.That(loaded.state.npcStates, Is.Empty); Assert.That(loaded.state.player.health, Is.EqualTo(73));
            Assert.That(loaded.state.GetSwitch("door"), Is.True); Assert.That(loaded.state.GetVariable("chapter"), Is.EqualTo(2));
            Assert.That(loaded.state.GetItemCount("key"), Is.EqualTo(1));
            Assert.That(loaded.state.survival.elapsedMinutes, Is.EqualTo(version == 1 ? 0 : 60));
            Assert.That(loaded.state.player.strength, Is.EqualTo(version == 1 ? 12 : 10));
            Assert.That(loaded.state.committedTransactions.Count, Is.EqualTo(version == 3 ? 1 : 0));
            Assert.That(loaded.state.eventCheckpoints.Count, Is.EqualTo(version == 3 ? 1 : 0));
        }

        [Test]
        public void V4RoundTripKeepsRouteFacingScheduleAndIndependentPlacementState()
        {
            NpcState saved = Record("a", new Vector2Int(2, 1)); saved.yaw = 270; saved.waypointIndex = 1;
            saved.persistentOverride = true; saved.scheduleId = "night"; saved.lastEvaluatedMinutes = 37;
            state.SetNpcState(saved, false); state.SetNpcState(Record("b", new Vector2Int(3, 2)), false);
            string before = JsonUtility.ToJson(state);
            string json = SaveService.Serialize(new SaveData { mapId = map.mapId, state = state, position = new Vector3(2, 0, 2) });
            SaveData loaded = SaveService.Deserialize(json);
            NpcState copy = loaded.state.GetNpcState(map.mapId, "a");
            Assert.That(copy.cell, Is.EqualTo(saved.cell)); Assert.That(copy.yaw, Is.EqualTo(270));
            Assert.That(copy.waypointIndex, Is.EqualTo(1)); Assert.That(copy.routeId, Is.EqualTo("patrol"));
            Assert.That(copy.scheduleId, Is.EqualTo("night")); Assert.That(copy.lastEvaluatedMinutes, Is.EqualTo(37));
            Assert.That(copy.persistentOverride, Is.True); Assert.That(loaded.state.npcStates.Count, Is.EqualTo(2));
            copy.cell = Vector2Int.zero; Assert.That(loaded.state.GetNpcState(map.mapId, "a").cell, Is.EqualTo(saved.cell));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before), "Serialization cannot modify the caller's checkpoints.");
        }

        [Test]
        public void DuplicateNullAndImpossibleNpcCheckpointsAreRejectedInsteadOfNormalizedAway()
        {
            NpcState valid = Record("a", new Vector2Int(1, 1));
            state.npcStates.Add(valid); state.npcStates.Add(valid.Clone());
            Assert.Throws<InvalidDataException>(() => state.Normalize());
            state.npcStates = new List<NpcState> { null }; Assert.Throws<InvalidDataException>(() => state.Normalize());
            valid.routeId = ""; valid.waypointIndex = 1; state.npcStates = new List<NpcState> { valid };
            Assert.Throws<InvalidDataException>(() => state.Normalize());
            valid.waypointIndex = 0; valid.yaw = float.NaN; Assert.Throws<InvalidDataException>(() => state.Normalize());
        }

        [TestCase("map")] [TestCase("placement")] [TestCase("npc")] [TestCase("route")] [TestCase("schedule")] [TestCase("waypoint")] [TestCase("future")]
        public void RemovedContentAndInvalidCheckpointsFailReadOnlyValidation(string kind)
        {
            NpcState saved = Record("a", new Vector2Int(1, 1));
            switch (kind)
            {
                case "map": saved.mapId = "removed"; break;
                case "placement": saved.placementId = "removed"; break;
                case "npc": saved.npcId = "removed"; break;
                case "route": saved.routeId = "removed"; break;
                case "schedule": saved.scheduleId = "removed"; break;
                case "waypoint": saved.waypointIndex = 2; break;
                case "future": saved.lastEvaluatedMinutes = 61; break;
            }
            state.SetNpcState(saved, false); string before = JsonUtility.ToJson(state);
            Assert.Throws<InvalidDataException>(() => NpcSaveValidation.Validate(state, new[] { map }));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        [Test]
        public void SimultaneousNpcAndStaticEventCollisionIsRejectedWithoutMovingActors()
        {
            map.events.Add(new MapEventPlacement { id = "b", npc = npc, cell = new Vector2Int(3, 3) });
            NpcState a = Record("a", new Vector2Int(2, 2)), b = Record("b", new Vector2Int(2, 2));
            a.persistentOverride = b.persistentOverride = true;
            state.SetNpcState(a, false); state.SetNpcState(b, false);
            Assert.Throws<InvalidDataException>(() => NpcSaveValidation.Validate(state, new[] { map }));
            state.npcStates.RemoveAt(1); map.events.RemoveAt(1);
            var wall = Asset<EventDefinition>(); wall.pages.Add(new EventPage { blocksMovement = true }); wall.EnsureStableIds();
            map.events.Add(new MapEventPlacement { id = "wall", definition = wall, cell = new Vector2Int(2, 2) });
            Assert.Throws<InvalidDataException>(() => NpcSaveValidation.Validate(state, new[] { map }));
            Assert.That(state.GetNpcState(map.mapId, "a").cell, Is.EqualTo(new Vector2Int(2, 2)));
        }

        [Test]
        public void PlayerFallbackUsesNearestRowMajorCellAndHonorsNonblockingNpc()
        {
            NpcState saved = Record("a", new Vector2Int(2, 2)); saved.persistentOverride = true;
            var resolved = new[] { saved }; Vector3 requested = map.CellToWorld(saved.cell);
            Assert.That(NpcSaveValidation.TryFindPlayerPosition(state, map, requested, 0.23f, resolved, out Vector3 result), Is.True);
            Assert.That(map.WorldToCell(result), Is.EqualTo(new Vector2Int(2, 1)));
            npc.blocksMovement = false;
            Assert.That(NpcSaveValidation.TryFindPlayerPosition(state, map, requested, 0.23f, resolved, out result), Is.True);
            Assert.That(result, Is.EqualTo(requested));
            npc.blocksMovement = true;
            var hidden = Asset<EventDefinition>(); hidden.pages.Add(new EventPage { visible = false, blocksMovement = false }); hidden.EnsureStableIds();
            map.events[0].definition = hidden;
            Assert.That(NpcSaveValidation.CanPlayerOccupy(state, map, requested, 0.23f, resolved), Is.True);
        }

        NpcState Record(string placement, Vector2Int cell) => new NpcState { mapId = map.mapId, placementId = placement, npcId = npc.npcId, cell = cell, routeId = route.routeId };
        T Asset<T>() where T : ScriptableObject { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
    }
}
