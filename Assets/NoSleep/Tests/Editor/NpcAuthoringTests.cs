using System.Collections.Generic;
using System.Linq;
using NoSleep.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoSleep.Tests
{
    public sealed class NpcAuthoringTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        T Make<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        MapDefinition Map()
        {
            var tile = Make<TileDefinition>(); tile.walkable = true;
            var map = Make<MapDefinition>(); map.mapId = System.Guid.NewGuid().ToString("N"); map.Resize(5, 5); map.Fill(tile); return map;
        }
        NpcDefinition Npc()
        { var npc = Make<NpcDefinition>(); npc.EnsureStableIds(); return npc; }
        NpcRouteDefinition Route(params Vector2Int[] points)
        {
            var route = Make<NpcRouteDefinition>(); route.EnsureStableIds();
            route.points = points.Select(cell => new NpcRoutePoint { cell = cell }).ToList(); return route;
        }
        [TearDown] public void Cleanup()
        { foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset); assets.Clear(); }

        [Test]
        public void ReadOnlyValidationAndCatalogNeverIssueMissingNpcRouteOrScheduleIdentities()
        {
            var npc = Make<NpcDefinition>(); var route = Make<NpcRouteDefinition>();
            route.points.Add(new NpcRoutePoint { cell = Vector2Int.one }); npc.route = route; npc.schedules.Add(new NpcScheduleEntry());
            var diagnostics = NpcContentValidator.CollectDiagnostics(assets);
            Assert.That(diagnostics.Any(row => row.Severity == ContentDiagnosticSeverity.Error), Is.True);
            ContentReferenceIndex.Build(assets);
            Assert.That(npc.npcId, Is.Null.Or.Empty); Assert.That(route.routeId, Is.Null.Or.Empty);
            Assert.That(npc.schedules[0].scheduleId, Is.Null.Or.Empty);
        }
        [Test]
        public void DuplicatedDefinitionAndRouteIdsAreReportedWithoutRewritingThem()
        {
            var first = Npc(); var second = Npc(); second.npcId = first.npcId;
            var routeA = Route(Vector2Int.one); var routeB = Route(Vector2Int.one); routeB.routeId = routeA.routeId;
            var diagnostics = NpcContentValidator.CollectDiagnostics(assets);
            Assert.That(diagnostics.Any(row => row.Owner == second && row.Message.Contains("중복")), Is.True);
            Assert.That(diagnostics.Any(row => row.Owner == routeB && row.Message.Contains("중복")), Is.True);
            Assert.That(first.npcId, Is.EqualTo(second.npcId)); Assert.That(routeA.routeId, Is.EqualTo(routeB.routeId));
        }
        [Test]
        public void NpcOnlyStationaryPlacementNeedsNoEventDefinitionOrRoute()
        {
            var map = Map(); var npc = Npc(); map.events.Add(new MapEventPlacement { id = "stationary", cell = Vector2Int.one, npc = npc });
            Assert.That(NpcContentValidator.CollectDiagnostics(assets), Is.Empty);
            var index = ContentReferenceIndex.Build(assets);
            Assert.That(index.GetDiagnostics().Any(row => row.Severity == ContentDiagnosticSeverity.Error), Is.False);
            CollectionAssert.Contains(index.Npcs, npc); CollectionAssert.Contains(index.NpcPlacementIds, "stationary");
            Assert.That(index.References.Any(row => row.Owner == map && row.Target == npc && row.Category == ContentReferenceCategory.Npc), Is.True);
        }
        [Test]
        public void SharedRouteIsCheckedAgainstEveryMapWhereItsNpcIsPlaced()
        {
            var first = Map(); var second = Map(); var npc = Npc(); var cell = new Vector2Int(2, 2); npc.route = Route(cell);
            first.events.Add(new MapEventPlacement { id = "a", cell = Vector2Int.one, npc = npc });
            second.events.Add(new MapEventPlacement { id = "b", cell = Vector2Int.one, npc = npc }); second.SetTile(cell, null);
            var diagnostic = NpcContentValidator.CollectDiagnostics(assets).Single(row => row.Message.Contains("경유지"));
            Assert.That(diagnostic.Owner, Is.SameAs(npc.route)); Assert.That(diagnostic.Message, Does.Contain(second.mapId));
        }
        [Test]
        public void InvalidScheduleStationAndTimingAreReported()
        {
            var map = Map(); var npc = Npc();
            npc.schedules.Add(new NpcScheduleEntry { firstDay = 3, lastDay = 1, startMinute = 500, endMinute = 500, stationCell = new Vector2Int(20, 20) });
            npc.EnsureStableIds(); map.events.Add(new MapEventPlacement { id = "worker", cell = Vector2Int.one, npc = npc });
            var diagnostics = NpcContentValidator.CollectDiagnostics(assets);
            Assert.That(diagnostics.Any(row => row.Owner == npc && row.Message.Contains("day/time")), Is.True);
            Assert.That(diagnostics.Any(row => row.Owner == npc && row.Message.Contains("정지점")), Is.True);
        }
        [Test]
        public void EnemyProfileRequiresATouchProgramWhileOrdinaryNpcDoesNot()
        {
            var map = Map(); var npc = Npc(); npc.enemy = Make<EnemyDefinition>();
            var placement = new MapEventPlacement { id = "monster", cell = Vector2Int.one, npc = npc }; map.events.Add(placement);
            Assert.That(NpcContentValidator.CollectDiagnostics(assets).Any(row => row.Message.Contains("PlayerTouch")), Is.True);
            var definition = Make<EventDefinition>(); definition.pages.Add(new EventPage { trigger = EventTrigger.PlayerTouch }); placement.definition = definition;
            Assert.That(NpcContentValidator.CollectDiagnostics(assets).Any(row => row.Message.Contains("PlayerTouch")), Is.False);
        }
        [Test]
        public void MapCopyAndPlacementCopyPreserveNpcProfileButIssueIndependentPlacementIds()
        {
            var map = Map(); var npc = Npc(); var definition = Make<EventDefinition>();
            map.events.Add(new MapEventPlacement { id = "npc", cell = Vector2Int.one, npc = npc, definition = definition });
            var ids = MapEditOperations.CloneEvents(map, new[] { "npc" }, Vector2Int.right);
            Assert.That(ids.Single(), Is.Not.EqualTo("npc")); Assert.That(map.events[1].npc, Is.SameAs(npc));
            var copy = MapEditOperations.CloneMap(map); assets.Add(copy);
            Assert.That(copy.mapId, Is.Not.EqualTo(map.mapId)); Assert.That(copy.events[0].npc, Is.SameAs(npc));
            Assert.That(copy.events[0].definition, Is.SameAs(definition)); Assert.That(copy.events[0].id, Is.Not.EqualTo("npc"));
        }
        [Test]
        public void BatchNpcOnlyPlacementsKeepProfileAndUniqueIds()
        {
            var map = Map(); var npc = Npc();
            var ids = MapEditOperations.BatchPlaceEvents(map, new[] { Vector2Int.one, new Vector2Int(2, 1) }, null, npc: npc);
            Assert.That(ids.Distinct().Count(), Is.EqualTo(2)); Assert.That(map.events.All(value => value.npc == npc && value.definition == null), Is.True);
        }
        [Test]
        public void CopiedScheduleReceivesIndependentIdentityWhileOriginalAndNpcIdentityRemain()
        {
            var npc = Npc(); var schedule = new NpcScheduleEntry { scheduleId = "original", stationCell = Vector2Int.one };
            npc.schedules.Add(schedule); npc.schedules.Add(schedule); string originalNpcId = npc.npcId;
            LogAssert.Expect(LogType.Warning, "Copied NPC schedule received a new identity; the first entry was preserved.");
            Assert.That(NpcAuthoringIdentity.EnsureIds(npc), Is.True);
            Assert.That(npc.npcId, Is.EqualTo(originalNpcId)); Assert.That(npc.schedules[0].scheduleId, Is.EqualTo("original"));
            Assert.That(npc.schedules[1].scheduleId, Is.Not.EqualTo("original")); Assert.That(npc.schedules[1], Is.Not.SameAs(schedule));
        }
        [Test]
        public void AuthoringIdentityIssuanceCanBeUndoneWithoutAffectingAnotherAsset()
        {
            var npc = Make<NpcDefinition>(); var route = Route(Vector2Int.one); npc.route = route; string routeId = route.routeId;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Assert.That(NpcAuthoringIdentity.EnsureIds(npc), Is.True); Undo.FlushUndoRecordObjects(); Undo.RevertAllDownToGroup(group);
            Assert.That(npc.npcId, Is.Null.Or.Empty); Assert.That(route.routeId, Is.EqualTo(routeId));
        }
        [Test]
        public void GeneratedNpcLabHasAllRouteBindingsSharedDialogueAndBothMapsConnected()
        {
            var main = AssetDatabase.LoadAssetAtPath<MapDefinition>(NpcLabBuilder.Data + "Maps/Main.asset");
            var annex = AssetDatabase.LoadAssetAtPath<MapDefinition>(NpcLabBuilder.Data + "Maps/Annex.asset");
            Assert.That(main, Is.Not.Null, "Build the NPC lab before project integration tests."); Assert.That(annex, Is.Not.Null);
            var a = main.events.Single(value => value.id == "cross_a"); var b = main.events.Single(value => value.id == "cross_b");
            Assert.That(a.npc, Is.Not.Null); Assert.That(b.npc, Is.Not.Null); Assert.That(a.npc.route.repeat && b.npc.route.repeat, Is.True);
            Assert.That(a.definition.pages[0].commands[0].commonEvent, Is.SameAs(b.definition.pages[0].commands[0].commonEvent));
            Assert.That(main.events.Single(value => value.id == "worker").npc.schedules.Count, Is.EqualTo(2));
            Assert.That(main.events.Single(value => value.id == "patrol_enemy").npc.enemy, Is.Not.Null);
            var stop = main.events.Single(value => value.id == "panel").definition.pages[0].commands.Single(value => value.type == EventCommandType.StopNpc);
            Assert.That(stop.key, Is.EqualTo("cross_a")); Assert.That(stop.boolValue, Is.True, "The lab must exercise a saved stationary override.");
            Assert.That(main.events.Single(value => value.id == "exit").definition.pages[0].commands[0].map, Is.SameAs(annex));
            Assert.That(NpcContentValidator.CollectErrors(), Is.Empty);
        }
    }
}
