using System.Linq;
using NoSleep.Editor;
using NUnit.Framework;
using UnityEditor;

namespace NoSleep.Tests
{
    public sealed class SystemsLabContentTests
    {
        const string Data = "Assets/NoSleep/Data/SystemsLab/";

        [Test]
        public void TwoCallersShareOneTypedCommonEventButKeepSeparateSelfSwitchPages()
        {
            var common = Load<CommonEventDefinition>("Common/SharedWelcome");
            var first = Load<EventDefinition>("Events/KeeperA");
            var second = Load<EventDefinition>("Events/KeeperB");
            foreach (var definition in new[] { first, second })
            {
                var call = definition.pages[0].commands.Single();
                Assert.That(call.type, Is.EqualTo(EventCommandType.CallCommon));
                Assert.That(call.commonEvent, Is.SameAs(common));
                Assert.That(call.arguments.Select(argument => argument.name), Is.EquivalentTo(new[] { "visitor", "number" }));
                Assert.DoesNotThrow(() => EventProgram.ValidateForExecution(definition, 0, false));
            }
            var state = new GameState(); state.SetSelfSwitch("systems_lab/keeper_a", "A", true);
            Assert.That(first.ResolvePageIndex(state, "systems_lab/keeper_a"), Is.EqualTo(1));
            Assert.That(second.ResolvePageIndex(state, "systems_lab/keeper_b"), Is.EqualTo(0));
            Assert.That(first.definitionId, Is.Not.EqualTo(second.definitionId));
            Assert.That(EventIdentityUpgrader.ValidateCommon(common), Is.Empty);
        }

        [TestCase("WatchA")]
        [TestCase("WatchB")]
        public void BackgroundWatchUsesAtomicGrantDurableWaitCheckpointAndQueuedForeground(string file)
        {
            var definition = Load<EventDefinition>("Events/" + file);
            var page = definition.pages.Single();
            Assert.That(page.trigger, Is.EqualTo(EventTrigger.Parallel));
            Assert.That(page.visible, Is.False);
            Assert.That(page.blocksMovement, Is.False);
            Assert.That(page.runOnce, Is.True);
            Assert.That(page.commands.Select(command => command.type), Is.EqualTo(new[]
            { EventCommandType.GrantOnce, EventCommandType.WaitGameMinutes, EventCommandType.Checkpoint, EventCommandType.RequestForeground }));
            Assert.That(page.commands[1].intValue, Is.EqualTo(10));
            Assert.That(page.commands[0].mutations.Count, Is.EqualTo(2));
            Assert.That(page.commands[3].foregroundEvent.pages[0].runOnce, Is.True);
            Assert.DoesNotThrow(() => EventProgram.ValidateForExecution(definition, 0, true));
            var state = new GameState();
            Assert.That(page.Matches(state, "watch"), Is.False);
            state.SetSwitch("lab_watch_enabled", true);
            Assert.That(page.Matches(state, "watch"), Is.True);
        }

        [Test]
        public void LabSceneConnectsBothMapsAndIncludesAllTypedPrograms()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(SystemsLabBuilder.LabScene), Is.Not.Null);
            var main = Load<MapDefinition>("Maps/Main"); var annex = Load<MapDefinition>("Maps/Annex");
            Assert.That(main.Validate(), Is.Empty); Assert.That(annex.Validate(), Is.Empty);
            Assert.That(main.mapId, Is.Not.EqualTo(annex.mapId));
            var transfers = main.events.Concat(annex.events).SelectMany(placement => placement.definition.pages)
                .SelectMany(page => page.commands).Where(command => command.type == EventCommandType.TransferPlayer).ToArray();
            Assert.That(transfers.Any(command => command.map == annex), Is.True);
            Assert.That(transfers.Any(command => command.map == main), Is.True);
            Assert.That(transfers.All(command => command.map.IsWalkable(command.cell)), Is.True);
            var dependencies = AssetDatabase.GetDependencies(SystemsLabBuilder.LabScene, true);
            Assert.That(dependencies, Does.Contain(Data + "Common/SharedWelcome.asset"));
            Assert.That(dependencies, Does.Contain(Data + "Events/NoticeA.asset"));
            Assert.That(dependencies, Does.Contain(Data + "Events/NoticeB.asset"));
            Assert.That(Load<SurvivalSettings>("Rules/Settings").timeMode, Is.EqualTo(TimeMode.AuthoredCosts));
        }

        [Test]
        public void GeneratedAndUpgradedContentHasNoDuplicateOrMissingIdentities()
            => Assert.That(EventIdentityUpgrader.CollectErrors(), Is.Empty);

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(Data + path + ".asset");
            Assert.That(value, Is.Not.Null, "Create the Systems Lab before content tests: " + path);
            return value;
        }
    }
}
