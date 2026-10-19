using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class EventTests
    {
        [Test]
        public void LastMatchingPageWinsAndCompletedPageDoesNotExposeEarlierPage()
        {
            var definition = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                var state = new GameState();
                definition.pages.Add(new EventPage());
                definition.pages.Add(new EventPage
                {
                    runOnce = true,
                    conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.Switch, key = "quest" } }
                });
                Assert.That(definition.ResolvePageIndex(state, "map/npc"), Is.EqualTo(0));
                state.SetSwitch("quest", true);
                Assert.That(definition.ResolvePageIndex(state, "map/npc"), Is.EqualTo(1));
                state.SetSelfSwitch("map/npc", EventDefinition.CompletionKey(1), true);
                Assert.That(definition.ResolvePageIndex(state, "map/npc"), Is.EqualTo(1));
                Assert.That(definition.IsPageCompleted(state, "map/npc", 1), Is.True);
                Assert.That(definition.IsPageCompleted(state, "map/other", 1), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void ProgramRejectsMissingTargetsAndDuplicateLabelsBeforeExecution()
        {
            Assert.Throws<ArgumentException>(() => EventProgram.ValidateAndIndex(new[]
            {
                new EventCommand { type = EventCommandType.SetSwitch, key = "reward", boolValue = true },
                new EventCommand { type = EventCommandType.Jump, targetLabel = "missing" }
            }));
            Assert.Throws<ArgumentException>(() => EventProgram.ValidateAndIndex(new[]
            {
                new EventCommand { type = EventCommandType.Label, label = "done" },
                new EventCommand { type = EventCommandType.Label, label = " done " }
            }));
        }

        [Test]
        public void ChoiceTargetsCanContinueOrJumpToValidatedLabel()
        {
            var labels = EventProgram.ValidateAndIndex(new[]
            {
                new EventCommand
                {
                    type = EventCommandType.ShowChoices,
                    choices = new List<EventChoice>
                    {
                        new EventChoice { text = "Continue" },
                        new EventChoice { text = "Leave", targetLabel = " done " }
                    }
                },
                new EventCommand { type = EventCommandType.Label, label = "done" },
                new EventCommand { type = EventCommandType.End }
            });
            Assert.That(labels["done"], Is.EqualTo(1));
        }

        [Test]
        public void ProgramRejectsInvalidWaitsAndReservedCompletionKeys()
        {
            Assert.Throws<ArgumentException>(() => EventProgram.ValidateAndIndex(new[]
            { new EventCommand { type = EventCommandType.Wait, seconds = float.NaN } }));
            Assert.Throws<ArgumentException>(() => EventProgram.ValidateAndIndex(new[]
            { new EventCommand { type = EventCommandType.Wait, seconds = -1 } }));
            Assert.Throws<ArgumentException>(() => EventProgram.ValidateAndIndex(new[]
            { new EventCommand { type = EventCommandType.SetSelfSwitch, key = EventDefinition.CompletionKey(0) } }));
        }
    }
}
