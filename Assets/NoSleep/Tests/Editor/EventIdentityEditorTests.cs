using System.Collections.Generic;
using NoSleep.Editor;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class EventIdentityEditorTests
    {
        readonly List<ScriptableObject> created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created) Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void FirstUpgradeCapturesLegacyIndicesBeforeInsertionAndReordering()
        {
            var definition = Create<EventDefinition>();
            definition.definitionId = null;
            var reward = new EventPage { name = "reward", pageId = null, legacyPageIndex = -1, runOnce = true };
            var later = new EventPage { name = "later", pageId = null, legacyPageIndex = -1, runOnce = true };
            reward.commands.Add(new EventCommand { type = EventCommandType.AddItem, key = "key", intValue = 1, commandId = null });
            definition.pages = new List<EventPage> { reward, later };
            Assert.That(EventIdentityUpgrader.UpgradeDefinition(definition), Is.True);
            string definitionId = definition.definitionId, rewardId = reward.pageId, commandId = reward.commands[0].commandId;
            Assert.That(reward.legacyPageIndex, Is.EqualTo(0));
            Assert.That(later.legacyPageIndex, Is.EqualTo(1));
            var newPage = new EventPage { pageId = null, legacyPageIndex = -1 };
            definition.pages = new List<EventPage> { newPage, later, reward };
            EventIdentityUpgrader.UpgradeDefinition(definition);
            Assert.That(newPage.legacyPageIndex, Is.EqualTo(-1));
            Assert.That(reward.legacyPageIndex, Is.EqualTo(0));
            Assert.That(later.legacyPageIndex, Is.EqualTo(1));
            Assert.That(reward.pageId, Is.EqualTo(rewardId));
            Assert.That(reward.commands[0].commandId, Is.EqualTo(commandId));
            Assert.That(definition.definitionId, Is.EqualTo(definitionId));
            Assert.That(EventIdentityUpgrader.UpgradeDefinition(definition), Is.False);
        }

        [Test]
        public void InspectorCopyRepairRetainsOriginalIdentityAndRenewsCopiedPageCommands()
        {
            var definition = Create<EventDefinition>();
            definition.pages.Add(new EventPage { name = "original", commands = new List<EventCommand>
            {
                new EventCommand { type = EventCommandType.Label, label = "reward" },
                new EventCommand { type = EventCommandType.ShowDialogue, speaker = "NPC", text = "A preserved line." }
            } });
            EventIdentityUpgrader.UpgradeDefinition(definition);
            var original = definition.pages[0];
            string originalId = original.pageId, commandId = original.commands[1].commandId;
            var copy = JsonUtility.FromJson<EventPage>(JsonUtility.ToJson(original));
            definition.pages.Add(copy);
            Assert.That(EventIdentityUpgrader.RepairCopiedEntries(definition, false), Is.EqualTo(1));
            Assert.That(original.pageId, Is.EqualTo(originalId));
            Assert.That(original.commands[1].commandId, Is.EqualTo(commandId));
            Assert.That(definition.pages[1].pageId, Is.Not.EqualTo(originalId));
            Assert.That(definition.pages[1].legacyPageIndex, Is.EqualTo(-1));
            Assert.That(definition.pages[1].commands[1].commandId, Is.Not.EqualTo(commandId));
            Assert.That(definition.pages[1].commands[0].label, Is.EqualTo("reward"));
            Assert.That(definition.pages[1].commands[1].text, Is.EqualTo("A preserved line."));
            Assert.That(definition.ValidateIdentity(), Is.Empty);
        }

        [Test]
        public void CopiedCommandRepairDoesNotRewriteTheOriginalCommand()
        {
            var definition = Create<EventDefinition>();
            var command = new EventCommand { type = EventCommandType.SetVariable, key = "score", intValue = 3 };
            definition.pages.Add(new EventPage { commands = new List<EventCommand> { command } });
            EventIdentityUpgrader.UpgradeDefinition(definition);
            string originalId = command.commandId;
            definition.pages[0].commands.Add(command);
            EventIdentityUpgrader.RepairCopiedEntries(definition, false);
            Assert.That(definition.pages[0].commands[0].commandId, Is.EqualTo(originalId));
            Assert.That(definition.pages[0].commands[1].commandId, Is.Not.EqualTo(originalId));
            Assert.That(definition.pages[0].commands[1].key, Is.EqualTo("score"));
            Assert.That(definition.pages[0].commands[1].intValue, Is.EqualTo(3));
        }

        [Test]
        public void ExplicitCopiedAssetRegenerationPreservesPageAndLabelContent()
        {
            var original = Create<EventDefinition>();
            original.pages.Add(new EventPage { name = "conversation", commands = new List<EventCommand>
            {
                new EventCommand { type = EventCommandType.Label, label = "again" },
                new EventCommand { type = EventCommandType.ShowDialogue, text = "Keep this dialogue." }
            } });
            EventIdentityUpgrader.UpgradeDefinition(original);
            var copy = Create<EventDefinition>(); JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), copy);
            string originalId = original.definitionId, pageId = original.pages[0].pageId;
            EventIdentityUpgrader.RegenerateCopiedAsset(copy);
            Assert.That(original.definitionId, Is.EqualTo(originalId));
            Assert.That(copy.definitionId, Is.Not.EqualTo(originalId));
            Assert.That(copy.pages[0].pageId, Is.Not.EqualTo(pageId));
            Assert.That(copy.pages[0].legacyPageIndex, Is.EqualTo(-1));
            Assert.That(copy.pages[0].name, Is.EqualTo("conversation"));
            Assert.That(copy.pages[0].commands[0].label, Is.EqualTo("again"));
            Assert.That(copy.pages[0].commands[1].text, Is.EqualTo("Keep this dialogue."));
        }

        [Test]
        public void CommonUpgradeAndCommandCopyRepairKeepParameterDefinitions()
        {
            var common = Create<CommonEventDefinition>();
            common.commonId = null;
            common.parameters.Add(new EventParameter { name = "amount", type = EventParameterType.Int, intValue = 5 });
            var command = new EventCommand { type = EventCommandType.AddGold, intParameter = "amount", commandId = null };
            common.commands.Add(command);
            Assert.That(EventIdentityUpgrader.UpgradeCommon(common), Is.True);
            string commonId = common.commonId, commandId = command.commandId;
            common.commands.Add(command);
            Assert.That(EventIdentityUpgrader.RepairCopiedCommands(common, false), Is.EqualTo(1));
            Assert.That(common.commonId, Is.EqualTo(commonId));
            Assert.That(common.commands[0].commandId, Is.EqualTo(commandId));
            Assert.That(common.commands[1].commandId, Is.Not.EqualTo(commandId));
            Assert.That(common.parameters[0].intValue, Is.EqualTo(5));
            Assert.That(EventIdentityUpgrader.UpgradeCommon(common), Is.False);
        }

        T Create<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>(); created.Add(value); return value;
        }
    }
}
