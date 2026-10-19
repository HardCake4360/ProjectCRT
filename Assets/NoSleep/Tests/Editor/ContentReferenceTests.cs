using System.Collections.Generic;
using System.Linq;
using NoSleep.Editor;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class ContentReferenceTests
    {
        readonly List<ScriptableObject> owned = new List<ScriptableObject>();
        T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); owned.Add(asset); return asset;
        }
        EventDefinition Event(params EventCommand[] commands)
        {
            var asset = Make<EventDefinition>(); asset.pages.Add(new EventPage { commands = commands.ToList() }); return asset;
        }
        MapDefinition Map(string id)
        {
            var tile = Make<TileDefinition>(); tile.walkable = true;
            var map = Make<MapDefinition>(); map.mapId = id; map.Resize(4, 4); map.Fill(tile); return map;
        }
        static void Place(MapDefinition map, EventDefinition definition) =>
            map.events.Add(new MapEventPlacement { id = "door", cell = new Vector2Int(1, 1), definition = definition });
        [TearDown] public void Cleanup()
        {
            foreach (var asset in owned) if (asset != null) Object.DestroyImmediate(asset);
            owned.Clear();
        }

        [Test]
        public void CatalogIncludesDeclarationsCommandsAtomicMutationsAndAssetIds()
        {
            var registry = Make<StateKeyRegistry>(); registry.switchKeys.Add("declared"); registry.itemIds.Add("declared_item");
            var common = Make<CommonEventDefinition>();
            common.commands.Add(new EventCommand { type = EventCommandType.GrantOnce, mutations = new List<StateMutation> {
                new StateMutation { kind = StateMutationKind.SetSwitch, key = "grant_switch" },
                new StateMutation { kind = StateMutationKind.AddVariable, key = "grant_count", intValue = 1 },
                new StateMutation { kind = StateMutationKind.AddItem, key = "token", intValue = 1 } } });
            var definition = Event(new EventCommand { type = EventCommandType.SetSwitch, key = "written" },
                new EventCommand { type = EventCommandType.CallCommon, commonEvent = common });
            var companion = Make<CompanionDefinition>(); companion.companionId = "min";
            var item = Make<ItemEffectDefinition>(); item.itemId = "medicine";
            var index = ContentReferenceIndex.Build(owned);
            CollectionAssert.AreEquivalent(new[] { "declared", "written", "grant_switch" }, index.SwitchKeys);
            CollectionAssert.Contains(index.VariableKeys, "grant_count");
            CollectionAssert.AreEquivalent(new[] { "declared_item", "medicine", "token" }, index.ItemIds);
            CollectionAssert.Contains(index.Companions, companion); CollectionAssert.Contains(index.CommonEvents, common);
            var atomic = index.References.Single(x => x.Owner == common && x.Category == ContentReferenceCategory.Variable);
            Assert.That(atomic.PageIndex, Is.EqualTo(-1)); Assert.That(atomic.CommandIndex, Is.EqualTo(0)); Assert.That(atomic.IsWrite, Is.True);
            Assert.That(index.GetDiagnostics().Any(x => x.Severity == ContentDiagnosticSeverity.Error), Is.False);
            Assert.That(definition.definitionId, Is.Null.Or.Empty, "Read-only indexing must not issue save identities.");
            Assert.That(common.commonId, Is.Null.Or.Empty);
        }

        [Test]
        public void UnknownReadIsWarningWhileWrittenAndDeclaredKeysRemainUsable()
        {
            var registry = Make<StateKeyRegistry>(); registry.switchKeys.Add("reserved_for_story");
            var definition = Event(new EventCommand { type = EventCommandType.SetSwitch, key = "door_open" });
            definition.pages[0].conditions.Add(new StateCondition { kind = ConditionKind.Switch, key = "door_opne" });
            definition.pages[0].conditions.Add(new StateCondition { kind = ConditionKind.Switch, key = "door_open" });
            definition.pages[0].conditions.Add(new StateCondition { kind = ConditionKind.Switch, key = "reserved_for_story" });
            var index = ContentReferenceIndex.Build(owned);
            var warnings = index.GetDiagnostics().Where(x => x.Severity == ContentDiagnosticSeverity.Warning).ToArray();
            Assert.That(warnings.Length, Is.EqualTo(1)); Assert.That(warnings[0].Reference.Key, Is.EqualTo("door_opne"));
            Assert.That(index.GetDiagnostics().Any(x => x.Severity == ContentDiagnosticSeverity.Error), Is.False);
        }

        [Test]
        public void MissingReferencesAndLabelsRetainExactCommandNavigation()
        {
            var definition = Event(new EventCommand { type = EventCommandType.TransferPlayer },
                new EventCommand { type = EventCommandType.CallCommon }, new EventCommand { type = EventCommandType.Jump, targetLabel = "absent" });
            var diagnostics = ContentReferenceIndex.Build(owned).GetDiagnostics();
            for (int command = 0; command < 3; command++)
                Assert.That(diagnostics.Any(x => x.Severity == ContentDiagnosticSeverity.Error && x.Owner == definition &&
                    x.PageIndex == 0 && x.CommandIndex == command), Is.True);
        }

        [Test]
        public void ParameterizedKeysAreNotInventedAsGlobalCatalogEntries()
        {
            var common = Make<CommonEventDefinition>();
            common.parameters.Add(new EventParameter { name = "flag", type = EventParameterType.String, stringValue = "story_flag" });
            common.commands.Add(new EventCommand { type = EventCommandType.SetSwitch, key = "{param:flag}" });
            var index = ContentReferenceIndex.Build(owned);
            Assert.That(index.SwitchKeys, Is.Empty);
            Assert.That(index.References.Single().Key, Is.EqualTo("{param:flag}"));
            Assert.That(index.GetDiagnostics(), Is.Empty);
        }

        [Test]
        public void TrimmedLabelsMatchRuntimeValidationRatherThanProduceFalseBrokenReferences()
        {
            Event(new EventCommand { type = EventCommandType.Label, label = " target " },
                new EventCommand { type = EventCommandType.Jump, targetLabel = "target" });
            Assert.That(ContentReferenceIndex.Build(owned).GetDiagnostics(), Is.Empty);
        }

        [Test]
        public void GraphFollowsNestedCommonAndForegroundTransfersWithTheirOriginalMapContext()
        {
            var a = Map("a"); var b = Map("b"); var c = Map("c");
            var inner = Make<CommonEventDefinition>(); inner.commands.Add(new EventCommand { type = EventCommandType.TransferPlayer, map = b, cell = b.spawnCell });
            var outer = Make<CommonEventDefinition>(); outer.commands.Add(new EventCommand { type = EventCommandType.CallCommon, commonEvent = inner });
            var foreground = Event(new EventCommand { type = EventCommandType.TransferPlayer, map = c, cell = c.spawnCell });
            var definition = Event(new EventCommand { type = EventCommandType.CallCommon, commonEvent = outer },
                new EventCommand { type = EventCommandType.RequestForeground, foregroundEvent = foreground });
            Place(a, definition);
            var result = MapConnectionAnalyzer.Collect(new[] { a, b, c });
            Assert.That(result.Connections.Count, Is.EqualTo(2));
            Assert.That(result.Connections.All(x => x.Source == a && x.PlacementId == "door"), Is.True);
            var nested = result.Connections.Single(x => x.Destination == b);
            Assert.That(nested.Owner, Is.SameAs(inner)); Assert.That(nested.PageIndex, Is.EqualTo(-1)); Assert.That(nested.CommandIndex, Is.Zero);
            Assert.That(nested.IsConditional, Is.False);
            Assert.That(result.Connections.Single(x => x.Destination == c).IsConditional, Is.True);
            Assert.That(result.Diagnostics.Any(x => x.Severity == ContentDiagnosticSeverity.Error), Is.False);
        }

        [Test]
        public void IntendedOneWaySuppressesReturnWarningButNeverConditionalWarning()
        {
            var a = Map("a"); var b = Map("b");
            var definition = Event(new EventCommand { type = EventCommandType.TransferPlayer, map = b, cell = b.spawnCell });
            definition.pages[0].conditions.Add(new StateCondition { kind = ConditionKind.Switch, key = "story" }); Place(a, definition);
            var before = MapConnectionAnalyzer.Collect(new[] { a, b });
            Assert.That(before.Diagnostics.Any(x => x.Message.Contains("돌아오는")), Is.True);
            var registry = Make<StateKeyRegistry>(); registry.mapLinks.Add(new MapLinkPolicy { source = a, destination = b, allowOneWay = true });
            var after = MapConnectionAnalyzer.Collect(new[] { a, b }, new[] { registry });
            Assert.That(after.Diagnostics.Any(x => x.Message.Contains("돌아오는")), Is.False);
            Assert.That(after.Diagnostics.Any(x => x.Message.Contains("조건부")), Is.True);
        }

        [Test]
        public void RequiredConnectionAbsenceIsAnErrorAtItsAuthoringRegistry()
        {
            var a = Map("a"); var b = Map("b");
            var registry = Make<StateKeyRegistry>(); registry.mapLinks.Add(new MapLinkPolicy { source = a, destination = b, requireConnection = true });
            var diagnostic = MapConnectionAnalyzer.Collect(new[] { a, b }, new[] { registry }).Diagnostics.Single();
            Assert.That(diagnostic.Severity, Is.EqualTo(ContentDiagnosticSeverity.Error));
            Assert.That(diagnostic.Owner, Is.SameAs(registry)); Assert.That(diagnostic.Message, Does.Contain("필수 연결"));
        }

        [Test]
        public void UnknownMapAndInvalidDestinationAreErrorsWithoutExecutingOrChangingEvents()
        {
            var a = Map("a"); var b = Map("b");
            var definition = Event(new EventCommand { type = EventCommandType.TransferPlayer, map = b, cell = new Vector2Int(20, 20) }); Place(a, definition);
            Assert.That(MapConnectionAnalyzer.Collect(new[] { a }).Diagnostics.Any(x => x.Message.Contains("목록")), Is.True);
            var result = MapConnectionAnalyzer.Collect(new[] { a, b });
            Assert.That(result.Diagnostics.Any(x => x.Severity == ContentDiagnosticSeverity.Error && x.Owner == definition && x.CommandIndex == 0), Is.True);
            Assert.That(definition.pages[0].commands[0].cell, Is.EqualTo(new Vector2Int(20, 20)));
            b.SetTile(b.spawnCell, null);
            Assert.That(MapConnectionAnalyzer.Collect(new[] { a, b }).Diagnostics.Any(x => x.Owner == b && x.Message.Contains("시작점")), Is.True);
        }

        [Test]
        public void RecursiveSharedProgramsProduceBoundedGraphWarning()
        {
            var a = Map("a"); var common = Make<CommonEventDefinition>();
            common.commands.Add(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common });
            Place(a, Event(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common }));
            var result = MapConnectionAnalyzer.Collect(new[] { a });
            Assert.That(result.Connections, Is.Empty);
            Assert.That(result.Diagnostics.Single().Severity, Is.EqualTo(ContentDiagnosticSeverity.Warning));
        }
    }
}
