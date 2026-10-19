using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoSleep.Tests
{
    public sealed class EventRunnerTests
    {
        GameObject host;
        GameSession session;
        EventRunner runner;
        EventDefinition definition;
        FakePresentation presentation;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("EventRunner test");
            // The interpreter needs state only. Keep the session inactive so its scene singleton
            // cannot conflict with a game scene left open while running the test suite.
            var stateHost = new GameObject("Isolated test state");
            stateHost.SetActive(false);
            stateHost.transform.SetParent(host.transform, false);
            session = stateHost.AddComponent<GameSession>();
            runner = host.AddComponent<EventRunner>();
            presentation = new FakePresentation();
            runner.Configure(session, presentation);
            definition = ScriptableObject.CreateInstance<EventDefinition>();
            definition.pages.Add(new EventPage());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (runner != null) runner.Cancel();
            if (host != null) UnityEngine.Object.Destroy(host);
            if (definition != null) UnityEngine.Object.Destroy(definition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExecutesStateChangesFalseBranchAndSelectedChoice()
        {
            presentation.SelectedChoice = 1;
            definition.pages[0].commands = new List<EventCommand>
            {
                new EventCommand { type = EventCommandType.SetSwitch, key = "quest", boolValue = true },
                new EventCommand { type = EventCommandType.SetVariable, key = "score", intValue = 2 },
                new EventCommand { type = EventCommandType.AddVariable, key = "score", intValue = 3 },
                new EventCommand { type = EventCommandType.IfCondition, targetLabel = "choose",
                    condition = new StateCondition { kind = ConditionKind.Variable, key = "score", value = 6 } },
                new EventCommand { type = EventCommandType.SetVariable, key = "score", intValue = 999 },
                new EventCommand { type = EventCommandType.Label, label = "choose" },
                new EventCommand { type = EventCommandType.ShowDialogue, speaker = "NPC", text = "Hello" },
                new EventCommand { type = EventCommandType.ShowChoices, text = "Take the note?",
                    choices = new List<EventChoice>
                    {
                        new EventChoice { text = "No" },
                        new EventChoice { text = "Yes", targetLabel = "reward" }
                    } },
                new EventCommand { type = EventCommandType.SetSwitch, key = "wrongChoice", boolValue = true },
                new EventCommand { type = EventCommandType.End },
                new EventCommand { type = EventCommandType.Label, label = "reward" },
                new EventCommand { type = EventCommandType.AddItem, key = "note", intValue = 2 },
                new EventCommand { type = EventCommandType.AddGold, intValue = 17 },
                new EventCommand { type = EventCommandType.SetSelfSwitch, key = "A", boolValue = true },
                new EventCommand { type = EventCommandType.End }
            };
            int completed = 0;
            runner.Completed += () => completed++;
            Assert.That(runner.TryRun(definition, "test/npc"), Is.True);
            yield return FinishWithin(10);
            Assert.That(session.State.GetSwitch("quest"), Is.True);
            Assert.That(session.State.GetVariable("score"), Is.EqualTo(5));
            Assert.That(session.State.GetSwitch("wrongChoice"), Is.False);
            Assert.That(session.State.GetItemCount("note"), Is.EqualTo(2));
            Assert.That(session.State.player.gold, Is.EqualTo(17));
            Assert.That(session.State.GetSelfSwitch("test/npc", "A"), Is.True);
            Assert.That(presentation.DialogueCount, Is.EqualTo(1));
            Assert.That(presentation.ChoicePrompt, Is.EqualTo("Take the note?"));
            Assert.That(completed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RunOnceCompletionPersistsThroughStateSerializationAndRejectsReplay()
        {
            definition.pages[0].runOnce = true;
            definition.pages[0].commands.Add(new EventCommand { type = EventCommandType.AddGold, intValue = 5 });
            Assert.That(runner.TryRun(definition, "test/chest"), Is.True);
            yield return FinishWithin(5);
            var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(session.State));
            restored.Normalize();
            Assert.That(definition.IsPageCompleted(restored, "test/chest", 0), Is.True);
            Assert.That(definition.IsPageCompleted(restored, "test/otherChest", 0), Is.False);
            Assert.That(runner.TryRun(definition, "test/chest"), Is.False);
            Assert.That(session.State.player.gold, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator CancelWhileWaitingDisposesPresentationAndDoesNotComplete()
        {
            presentation.HoldDialogue = true;
            definition.pages[0].runOnce = true;
            definition.pages[0].commands.Add(new EventCommand { type = EventCommandType.ShowDialogue, text = "Wait" });
            definition.pages[0].commands.Add(new EventCommand { type = EventCommandType.SetSwitch, key = "afterWait" });
            int completed = 0;
            runner.Completed += () => completed++;
            Assert.That(runner.TryRun(definition, "test/wait"), Is.True);
            yield return null;
            Assert.That(runner.IsRunning, Is.True);
            runner.Cancel();
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(presentation.DialogueDisposed, Is.True);
            Assert.That(session.State.GetSwitch("afterWait"), Is.False);
            Assert.That(definition.IsPageCompleted(session.State, "test/wait", 0), Is.False);
            Assert.That(completed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PresentationFailureReleasesRunnerAndDoesNotMarkCompletion()
        {
            presentation.ThrowDialogue = true;
            definition.pages[0].runOnce = true;
            definition.pages[0].commands.Add(new EventCommand { type = EventCommandType.ShowDialogue });
            int failures = 0;
            runner.Failed += error => failures++;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: presentation fault"));
            Assert.That(runner.TryRun(definition, "test/fault"), Is.True);
            yield return FinishWithin(10);
            Assert.That(presentation.DialogueDisposed, Is.True);
            Assert.That(definition.IsPageCompleted(session.State, "test/fault", 0), Is.False);
            Assert.That(failures, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InfiniteJumpFailsWithinEightyFramesAndReleasesRunner()
        {
            definition.pages[0].commands.Add(new EventCommand { type = EventCommandType.Label, label = "loop" });
            definition.pages[0].commands.Add(new EventCommand { type = EventCommandType.Jump, targetLabel = "loop" });
            int failures = 0;
            runner.Failed += error => failures++;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Event exceeded its instruction budget"));
            Assert.That(runner.TryRun(definition, "test/loop"), Is.True);
            yield return FinishWithin(80);
            Assert.That(failures, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TouchEnteredDuringAnotherEventRetriesOnceAndRearmsAfterFullExit()
        {
            AttachRunnerToSession();
            definition.pages[0].commands.Add(new EventCommand { type = EventCommandType.Wait, seconds = 60 });
            var touchDefinition = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                touchDefinition.pages.Add(new EventPage { trigger = EventTrigger.PlayerTouch,
                    commands = new List<EventCommand> { new EventCommand { type = EventCommandType.AddVariable, key = "touchCount", intValue = 1 } } });
                var eventHost = new GameObject("Touch event");
                eventHost.transform.SetParent(host.transform);
                var eventObject = eventHost.AddComponent<EventObject>();
                eventObject.Configure(session, touchDefinition, "test/touch");
                Collider body = CreatePlayerCollider(out var player);
                var extraHost = new GameObject("Second player collider");
                extraHost.transform.SetParent(player.transform);
                Collider extra = extraHost.AddComponent<BoxCollider>();

                Assert.That(runner.TryRun(definition, "test/busy"), Is.True);
                eventObject.SendMessage("OnTriggerEnter", body);
                eventObject.SendMessage("OnTriggerEnter", extra);
                yield return null;
                Assert.That(session.State.GetVariable("touchCount"), Is.Zero);
                runner.Cancel();
                eventObject.SendMessage("OnTriggerStay", body);
                Assert.That(session.State.GetVariable("touchCount"), Is.EqualTo(1));
                eventObject.SendMessage("OnTriggerStay", body);
                eventObject.SendMessage("OnTriggerStay", extra);
                Assert.That(session.State.GetVariable("touchCount"), Is.EqualTo(1));
                eventObject.SendMessage("OnTriggerExit", body);
                eventObject.SendMessage("OnTriggerStay", extra);
                Assert.That(session.State.GetVariable("touchCount"), Is.EqualTo(1));
                eventObject.SendMessage("OnTriggerExit", extra);
                eventObject.SendMessage("OnTriggerEnter", body);
                Assert.That(session.State.GetVariable("touchCount"), Is.EqualTo(2));
            }
            finally { UnityEngine.Object.Destroy(touchDefinition); }
        }

        [UnityTest]
        public IEnumerator ActionPageEntryDoesNotQueueTouchWhenPageChangesInsideTrigger()
        {
            AttachRunnerToSession();
            definition.pages[0].commands.Add(new EventCommand { type = EventCommandType.AddVariable, key = "touchCount", intValue = 1 });
            var eventHost = new GameObject("Touch page change");
            eventHost.transform.SetParent(host.transform);
            var eventObject = eventHost.AddComponent<EventObject>();
            eventObject.Configure(session, definition, "test/pageChange");
            Collider body = CreatePlayerCollider(out _);
            eventObject.SendMessage("OnTriggerEnter", body);
            definition.pages[0].trigger = EventTrigger.PlayerTouch;
            session.State.NotifyChanged();
            eventObject.SendMessage("OnTriggerStay", body);
            Assert.That(session.State.GetVariable("touchCount"), Is.Zero);
            eventObject.SendMessage("OnTriggerExit", body);
            eventObject.SendMessage("OnTriggerEnter", body);
            Assert.That(session.State.GetVariable("touchCount"), Is.EqualTo(1));
            yield return null;
        }

        void AttachRunnerToSession()
        {
            // Wire only the interpreter dependency; full Configure requires a map, camera and UI.
            typeof(GameSession).GetProperty(nameof(GameSession.Events)).GetSetMethod(true)
                .Invoke(session, new object[] { runner });
        }

        Collider CreatePlayerCollider(out GameObject player)
        {
            player = new GameObject("Touch test player");
            player.transform.SetParent(host.transform);
            player.AddComponent<PlayerController>();
            return player.GetComponent<CharacterController>();
        }

        IEnumerator FinishWithin(int frames)
        {
            for (int frame = 0; frame < frames && runner.IsRunning; frame++) yield return null;
            Assert.That(runner.IsRunning, Is.False, "Event did not finish within the frame budget.");
        }

        sealed class FakePresentation : IEventPresentation
        {
            public int SelectedChoice;
            public int DialogueCount;
            public string ChoicePrompt;
            public bool HoldDialogue;
            public bool ThrowDialogue;
            public bool DialogueDisposed;

            public IEnumerator ShowDialogue(string speaker, string text)
            {
                try
                {
                    DialogueCount++;
                    do { yield return null; } while (HoldDialogue);
                    if (ThrowDialogue) throw new InvalidOperationException("presentation fault");
                }
                finally { DialogueDisposed = true; }
            }

            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> onSelected)
            {
                ChoicePrompt = prompt;
                yield return null;
                onSelected(SelectedChoice);
            }
        }
    }
}
