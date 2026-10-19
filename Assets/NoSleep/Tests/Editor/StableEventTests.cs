using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class StableEventTests
    {
        EventDefinition definition;
        [SetUp] public void SetUp() { definition = ScriptableObject.CreateInstance<EventDefinition>(); }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(definition); }

        [Test]
        public void FirstConversionCapturesOriginalOrderAndReorderingPreservesEveryIdentity()
        {
            var first = new EventPage { runOnce = true };
            var second = new EventPage { runOnce = true, commands = { new EventCommand { type = EventCommandType.End } } };
            definition.pages.Add(first); definition.pages.Add(second);
            Assert.That(definition.EnsureStableIds(), Is.True);
            string definitionId = definition.definitionId, pageId = second.pageId, commandId = second.commands[0].commandId;
            definition.pages.Reverse();
            Assert.That(definition.EnsureStableIds(), Is.False);
            Assert.That(definition.definitionId, Is.EqualTo(definitionId));
            Assert.That(second.pageId, Is.EqualTo(pageId));
            Assert.That(second.commands[0].commandId, Is.EqualTo(commandId));
            Assert.That(second.legacyPageIndex, Is.EqualTo(1));
            Assert.That(first.legacyPageIndex, Is.Zero);
            Assert.That(definition.ValidateIdentity(), Is.Empty);
        }

        [Test]
        public void InsertedPageCannotInheritAnOldIndexCompletion()
        {
            var old = new EventPage { runOnce = true };
            definition.pages.Add(old); definition.EnsureStableIds();
            var added = new EventPage { runOnce = true };
            definition.pages.Insert(0, added);
            definition.EnsureStableIds();
            var state = new GameState();
            state.SetSelfSwitch("map/npc", EventDefinition.CompletionKey(0), true);
            Assert.That(added.legacyPageIndex, Is.EqualTo(-1));
            Assert.That(old.legacyPageIndex, Is.Zero);
            Assert.That(definition.IsPageCompleted(state, "map/npc", 0), Is.False);
            Assert.That(definition.IsPageCompleted(state, "map/npc", 1), Is.True);
        }

        [Test]
        public void StableCompletionSurvivesReorderAndExplicitFalseOverridesLegacyTrue()
        {
            var completed = new EventPage { runOnce = true };
            definition.pages.Add(completed); definition.pages.Add(new EventPage { runOnce = true });
            definition.EnsureStableIds();
            var state = new GameState(); string key = definition.StableCompletionKey(completed.pageId);
            state.SetSelfSwitch("map/npc", key, true);
            definition.pages.Reverse();
            Assert.That(definition.IsPageCompleted(state, "map/npc", 1), Is.True);
            Assert.That(definition.IsPageCompleted(state, "map/npc", 0), Is.False);
            state.SetSelfSwitch("map/npc", EventDefinition.CompletionKey(0), true);
            state.SetSelfSwitch("map/npc", key, false);
            Assert.That(definition.IsPageCompleted(state, "map/npc", 1), Is.False);
        }

        [Test]
        public void CompletionKeyIncludesDefinitionEvenWhenPageIdsMatch()
        {
            var other = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                definition.pages.Add(new EventPage { runOnce = true }); other.pages.Add(new EventPage { runOnce = true });
                definition.EnsureStableIds(); other.EnsureStableIds();
                other.pages[0].pageId = definition.pages[0].pageId;
                var state = new GameState();
                state.SetSelfSwitch("map/npc", definition.StableCompletionKey(definition.pages[0].pageId), true);
                Assert.That(other.StableCompletionKey(other.pages[0].pageId), Is.Not.EqualTo(definition.StableCompletionKey(definition.pages[0].pageId)));
                Assert.That(other.IsPageCompleted(state, "map/npc", 0), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(other); }
        }

        [Test]
        public void DuplicatePageCommandAndCapturedLegacyIdsAreRejected()
        {
            definition.pages.Add(new EventPage { commands = { new EventCommand { type = EventCommandType.End }, new EventCommand { type = EventCommandType.Label, label = "end" } } });
            definition.pages.Add(new EventPage()); definition.EnsureStableIds();
            definition.pages[1].pageId = definition.pages[0].pageId;
            definition.pages[1].legacyPageIndex = definition.pages[0].legacyPageIndex;
            definition.pages[0].commands[1].commandId = definition.pages[0].commands[0].commandId;
            var errors = definition.ValidateIdentity();
            Assert.That(errors.Any(error => error.Contains("page ID")), Is.True);
            Assert.That(errors.Any(error => error.Contains("legacy index")), Is.True);
            Assert.That(errors.Any(error => error.Contains("command ID")), Is.True);
            Assert.Throws<ArgumentException>(() => EventProgram.ValidateForExecution(definition, 0));
        }

        [Test]
        public void EnsureDoesNotSilentlyRepairCopiedNonemptyIds()
        {
            definition.pages.Add(new EventPage()); definition.EnsureStableIds();
            var duplicate = new EventPage { pageId = definition.pages[0].pageId, legacyPageIndex = -1 };
            definition.pages.Add(duplicate);
            Assert.That(definition.EnsureStableIds(), Is.False);
            Assert.That(definition.ValidateIdentity(), Is.Not.Empty);
        }
    }
}
