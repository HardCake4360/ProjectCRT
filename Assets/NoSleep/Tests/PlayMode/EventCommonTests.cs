using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoSleep.Tests
{
    public sealed class EventCommonTests
    {
        GameObject host;
        GameSession session;
        EventRunner runner;
        FakePresentation presentation;
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        readonly List<EventExecutionContext> contexts = new List<EventExecutionContext>();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Common event test");
            var stateHost = new GameObject("Isolated common event state");
            stateHost.SetActive(false); stateHost.transform.SetParent(host.transform);
            session = stateHost.AddComponent<GameSession>();
            runner = host.AddComponent<EventRunner>();
            presentation = new FakePresentation(); runner.Configure(session, presentation);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            runner.Cancel(); foreach (var context in contexts) context.Dispose(); contexts.Clear();
            UnityEngine.Object.Destroy(host);
            foreach (var asset in assets) UnityEngine.Object.Destroy(asset); assets.Clear();
            yield return null;
        }
        T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
        EventDefinition Definition(params EventCommand[] commands)
        {
            var definition = Asset<EventDefinition>();
            definition.pages.Add(new EventPage { commands = new List<EventCommand>(commands) }); return definition;
        }
        EventExecutionContext Context(EventDefinition definition, string key, bool background = false)
        { var context = runner.CreateExecution(definition, key, 0, background); contexts.Add(context); return context; }
        static void Finish(EventExecutionContext context, int pumps = 100)
        {
            for (int count = 0; count < pumps && context.MoveNext(out _); count++) { }
            Assert.That(context.IsComplete, Is.True, "Execution did not finish within its bounded pump allowance.");
            Assert.That(context.Error, Is.Null);
        }

        [UnityTest]
        public IEnumerator TypedArgumentsDialogueConditionAndCommonEndReturnToTheCaller()
        {
            var common = Asset<CommonEventDefinition>();
            common.parameters.Add(new EventParameter { name = "who", type = EventParameterType.String });
            common.parameters.Add(new EventParameter { name = "amount", type = EventParameterType.Int });
            common.parameters.Add(new EventParameter { name = "allowed", type = EventParameterType.Bool });
            common.commands = new List<EventCommand>
            {
                new EventCommand { type = EventCommandType.ShowDialogue, speaker = "{param:who}", text = "수량 {param:amount}, 허용 {param:allowed}" },
                new EventCommand { type = EventCommandType.IfCondition, useLocalCondition = true,
                    localCondition = new EventLocalCondition { parameter = "allowed", type = EventParameterType.Bool }, targetLabel = "return" },
                new EventCommand { type = EventCommandType.SetVariable, key = "given", intParameter = "amount" },
                new EventCommand { type = EventCommandType.SetSelfSwitch, key = "met", boolParameter = "allowed" },
                new EventCommand { type = EventCommandType.Label, label = "return" },
                new EventCommand { type = EventCommandType.End },
                new EventCommand { type = EventCommandType.AddGold, intValue = 999 }
            };
            var call = new EventCommand { type = EventCommandType.CallCommon, commonEvent = common, arguments = new List<EventArgument>
            {
                new EventArgument { name = "who", type = EventParameterType.String, stringValue = "경비" },
                new EventArgument { name = "amount", type = EventParameterType.Int, intValue = 7 },
                new EventArgument { name = "allowed", type = EventParameterType.Bool, boolValue = true }
            } };
            var definition = Definition(call, new EventCommand { type = EventCommandType.AddGold, intValue = 3 });
            definition.pages[0].runOnce = true;
            Assert.That(runner.TryRun(definition, "town/guard"), Is.True);
            yield return null;
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(session.State.GetVariable("given"), Is.EqualTo(7));
            Assert.That(session.State.GetSelfSwitch("town/guard", "met"), Is.True);
            Assert.That(session.State.player.gold, Is.EqualTo(3));
            Assert.That(presentation.Dialogues, Has.Member("경비|수량 7, 허용 true"));
            Assert.That(definition.IsPageCompleted(session.State, "town/guard", 0), Is.True);

            call.arguments[2].boolValue = false;
            session.State.SetVariable("given", 0);
            Assert.That(runner.TryRun(definition, "town/otherGuard"), Is.True);
            yield return null;
            Assert.That(session.State.GetVariable("given"), Is.Zero);
            Assert.That(session.State.GetSelfSwitch("town/otherGuard", "met"), Is.False);
            Assert.That(session.State.player.gold, Is.EqualTo(6));
        }

        [Test]
        public void IndependentBackgroundContextsKeepCallerLocalsAndSelfSwitchesSeparate()
        {
            var common = Asset<CommonEventDefinition>();
            common.parameters.Add(new EventParameter { name = "key", type = EventParameterType.String });
            common.parameters.Add(new EventParameter { name = "value", type = EventParameterType.Int });
            common.commands.Add(new EventCommand { type = EventCommandType.SetVariable, key = "{param:key}", intParameter = "value" });
            common.commands.Add(new EventCommand { type = EventCommandType.SetSelfSwitch, key = "visited" });
            EventDefinition Make(string key, int number)
            {
                var definition = Definition(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common, arguments = new List<EventArgument>
                { new EventArgument { name = "key", type = EventParameterType.String, stringValue = key }, new EventArgument { name = "value", type = EventParameterType.Int, intValue = number } } });
                definition.pages[0].trigger = EventTrigger.Parallel; return definition;
            }
            var a = Context(Make("a", 11), "map/a", true); var b = Context(Make("b", 22), "map/b", true);
            a.MaxInstructionsPerPump = b.MaxInstructionsPerPump = 1;
            for (int count = 0; count < 8 && (!a.IsComplete || !b.IsComplete); count++)
            { a.MoveNext(out _); Assert.That(a.InstructionsLastPump, Is.LessThanOrEqualTo(1)); b.MoveNext(out _); Assert.That(b.InstructionsLastPump, Is.LessThanOrEqualTo(1)); }
            Assert.That(a.Succeeded && b.Succeeded, Is.True);
            Assert.That(session.State.GetVariable("a"), Is.EqualTo(11));
            Assert.That(session.State.GetVariable("b"), Is.EqualTo(22));
            Assert.That(session.State.GetSelfSwitch("map/a", "visited"), Is.True);
            Assert.That(session.State.GetSelfSwitch("map/b", "visited"), Is.True);
            Assert.That(a.Scope, Is.Not.EqualTo(b.Scope));
        }

        [Test]
        public void RecursiveAndUnsafeCommonProgramsAreRejectedBeforeAnyStateMutation()
        {
            var a = Asset<CommonEventDefinition>(); var b = Asset<CommonEventDefinition>();
            a.commands.Add(new EventCommand { type = EventCommandType.CallCommon, commonEvent = b });
            b.commands.Add(new EventCommand { type = EventCommandType.CallCommon, commonEvent = a });
            var definition = Definition(new EventCommand { type = EventCommandType.SetSwitch, key = "shouldNotRun" }, new EventCommand { type = EventCommandType.CallCommon, commonEvent = a });
            Assert.Throws<ArgumentException>(() => Context(definition, "map/npc"));
            b.commands.Clear(); b.commands.Add(new EventCommand { type = EventCommandType.ShowDialogue, text = "Unsafe background UI" });
            definition.pages[0].trigger = EventTrigger.Parallel;
            Assert.Throws<ArgumentException>(() => Context(definition, "map/npc", true));
            Assert.That(session.State.GetSwitch("shouldNotRun"), Is.False);
            b.commands.Clear(); b.commands.Add(new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 5 });
            Assert.Throws<ArgumentException>(() => Context(definition, "map/npc", true), "A durable wait cannot silently discard a common call stack.");
        }

        [Test]
        public void GrantOnceCommitsOneBatchAndDistinguishesCallsitesAndCallers()
        {
            var common = Asset<CommonEventDefinition>();
            common.commands.Add(new EventCommand { type = EventCommandType.GrantOnce, mutations = new List<StateMutation>
            { new StateMutation { kind = StateMutationKind.AddGold, intValue = 5 }, new StateMutation { kind = StateMutationKind.AddItem, key = "key", intValue = 1 } } });
            var definition = Definition(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common }, new EventCommand { type = EventCommandType.CallCommon, commonEvent = common });
            definition.pages[0].trigger = EventTrigger.Parallel;
            int changed = 0; session.State.Changed += () => changed++;
            Finish(Context(definition, "map/a", true));
            Assert.That(session.State.player.gold, Is.EqualTo(10)); Assert.That(session.State.GetItemCount("key"), Is.EqualTo(2));
            Assert.That(changed, Is.EqualTo(2)); Assert.That(session.State.committedTransactions.Count, Is.EqualTo(2));
            Finish(Context(definition, "map/a", true)); Assert.That(changed, Is.EqualTo(2));
            Finish(Context(definition, "map/b", true)); Assert.That(session.State.player.gold, Is.EqualTo(20));
            Assert.That(session.State.committedTransactions.Count, Is.EqualTo(4));
        }

        [Test]
        public void ReplacingCommonAssetAtTheSameCallsiteUsesItsOwnGrantNamespace()
        {
            var first = Asset<CommonEventDefinition>(); var replacement = Asset<CommonEventDefinition>();
            first.commands.Add(new EventCommand { type = EventCommandType.GrantOnce,
                mutations = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddGold, intValue = 5 } } });
            first.EnsureStableIds();
            replacement.commands.Add(new EventCommand { type = EventCommandType.GrantOnce, commandId = first.commands[0].commandId,
                mutations = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddGold, intValue = 2 } } });
            var call = new EventCommand { type = EventCommandType.CallCommon, commonEvent = first };
            var definition = Definition(call); definition.pages[0].trigger = EventTrigger.Parallel;
            Finish(Context(definition, "map/commonReplace", true));
            call.commonEvent = replacement;
            Finish(Context(definition, "map/commonReplace", true));
            Assert.That(session.State.player.gold, Is.EqualTo(7));
            Assert.That(session.State.committedTransactions.Count, Is.EqualTo(2));
            Assert.That(session.State.committedTransactions[0], Does.Contain(first.commonId));
            Assert.That(session.State.committedTransactions[1], Does.Contain(replacement.commonId));
        }

        [Test]
        public void NestedCallForwardsTypedParametersAndRejectsWrongArgumentTypes()
        {
            var outer = Asset<CommonEventDefinition>(); var inner = Asset<CommonEventDefinition>();
            outer.parameters.Add(new EventParameter { name = "count", type = EventParameterType.Int });
            inner.parameters.Add(new EventParameter { name = "received", type = EventParameterType.Int });
            inner.commands.Add(new EventCommand { type = EventCommandType.SetVariable, key = "forwarded", intParameter = "received" });
            outer.commands.Add(new EventCommand { type = EventCommandType.CallCommon, commonEvent = inner,
                arguments = new List<EventArgument> { new EventArgument { name = "received", fromParameter = "count" } } });
            var argument = new EventArgument { name = "count", type = EventParameterType.Int, intValue = 19 };
            var definition = Definition(new EventCommand { type = EventCommandType.CallCommon, commonEvent = outer,
                arguments = new List<EventArgument> { argument } });
            Finish(Context(definition, "map/forward"));
            Assert.That(session.State.GetVariable("forwarded"), Is.EqualTo(19));
            argument.type = EventParameterType.String; argument.stringValue = "wrong";
            Assert.Throws<ArgumentException>(() => Context(definition, "map/forward"));
            Assert.That(session.State.GetVariable("forwarded"), Is.EqualTo(19));
        }

        [Test]
        public void CancellingDuringAtomicGrantNotificationCannotRunFollowingCommands()
        {
            var definition = Definition(new EventCommand { type = EventCommandType.GrantOnce,
                mutations = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddGold, intValue = 9 } } },
                new EventCommand { type = EventCommandType.SetSwitch, key = "afterCancellation" });
            definition.pages[0].trigger = EventTrigger.Parallel; definition.pages[0].runOnce = true;
            var context = Context(definition, "map/callbackCancel", true);
            session.State.Changed += () => context.Cancel();
            Assert.That(context.MoveNext(out _), Is.False);
            Assert.That(context.IsCancelled, Is.True); Assert.That(context.Error, Is.Null);
            Assert.That(session.State.player.gold, Is.EqualTo(9)); Assert.That(session.State.committedTransactions.Count, Is.EqualTo(1));
            Assert.That(session.State.GetSwitch("afterCancellation"), Is.False);
            Assert.That(definition.IsPageCompleted(session.State, "map/callbackCancel", 0), Is.False);
        }

        [Test]
        public void RejectedForegroundRequestLeavesRunOncePageIncompleteAndSkipsLaterCommands()
        {
            var foreground = Definition(new EventCommand { type = EventCommandType.End });
            var definition = Definition(new EventCommand { type = EventCommandType.RequestForeground, foregroundEvent = foreground },
                new EventCommand { type = EventCommandType.GrantOnce, mutations = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddGold, intValue = 99 } } });
            definition.pages[0].trigger = EventTrigger.Parallel; definition.pages[0].runOnce = true;
            var context = Context(definition, "map/rejected", true); Finish(context);
            Assert.That(context.Succeeded, Is.False); Assert.That(session.State.player.gold, Is.Zero);
            Assert.That(definition.IsPageCompleted(session.State, "map/rejected", 0), Is.False);
            Assert.That(string.Join("\n", context.Trace), Does.Contain("Foreground request rejected"));
        }

        [Test]
        public void CallDepthCountsTheRootFrameAndRejectsTheSeventeenthFrame()
        {
            var chain = new List<CommonEventDefinition>();
            for (int index = 0; index < 16; index++) chain.Add(Asset<CommonEventDefinition>());
            for (int index = 0; index < 15; index++) chain[index].commands.Add(new EventCommand { type = EventCommandType.CallCommon, commonEvent = chain[index + 1] });
            var definition = Definition(new EventCommand { type = EventCommandType.CallCommon, commonEvent = chain[0] });
            var error = Assert.Throws<ArgumentException>(() => Context(definition, "map/depth"));
            Assert.That(error.Message, Does.Contain("depth exceeds 16"));
            chain[14].commands.Clear();
            var context = Context(definition, "map/depth"); Finish(context);
            Assert.That(context.Succeeded, Is.True);
        }

        [Test]
        public void SavedGameMinuteWaitResumesExistingDeadlineWithoutRepeatingRewards()
        {
            var definition = Definition(
                new EventCommand { type = EventCommandType.GrantOnce, mutations = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddGold, intValue = 9 } } },
                new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 10 },
                new EventCommand { type = EventCommandType.SetSwitch, key = "finished" });
            definition.pages[0].trigger = EventTrigger.Parallel;
            var first = Context(definition, "map/timer", true);
            Assert.That(first.MoveNext(out _), Is.True); Assert.That(session.State.player.gold, Is.EqualTo(9));
            var checkpoint = session.State.GetEventCheckpoint(first.Scope); Assert.That(checkpoint.dueElapsedMinutes, Is.EqualTo(10));
            first.Cancel(); session.State.survival.elapsedMinutes = 6;
            var resumed = Context(definition, "map/timer", true);
            Assert.That(resumed.MoveNext(out _), Is.True);
            Assert.That(session.State.GetEventCheckpoint(resumed.Scope).dueElapsedMinutes, Is.EqualTo(10));
            Assert.That(session.State.player.gold, Is.EqualTo(9)); Assert.That(session.State.GetSwitch("finished"), Is.False);
            session.State.survival.elapsedMinutes = 10; Finish(resumed);
            Assert.That(session.State.GetSwitch("finished"), Is.True);
            Assert.That(session.State.GetEventCheckpoint(resumed.Scope), Is.Null);
        }

        [Test]
        public void ExplicitCheckpointResumesAfterMarkerAndInvalidSavedCommandIsRejected()
        {
            var definition = Definition(new EventCommand { type = EventCommandType.SetVariable, key = "before", intValue = 5 },
                new EventCommand { type = EventCommandType.Checkpoint }, new EventCommand { type = EventCommandType.SetSwitch, key = "after" });
            definition.pages[0].trigger = EventTrigger.Parallel;
            var first = Context(definition, "map/checkpoint", true); first.MaxInstructionsPerPump = 2;
            Assert.That(first.MoveNext(out _), Is.True); Assert.That(session.State.GetSwitch("after"), Is.False);
            first.Cancel(); session.State.SetVariable("before", 99);
            var resumed = Context(definition, "map/checkpoint", true); Finish(resumed);
            Assert.That(session.State.GetVariable("before"), Is.EqualTo(99)); Assert.That(session.State.GetSwitch("after"), Is.True);
            session.State.SetEventCheckpoint(first.Scope, "removedCommand", 0);
            Assert.Throws<ArgumentException>(() => Context(definition, "map/checkpoint", true));
        }

        [Test]
        public void PauseAndStepExecuteOneCommandAcrossCommonCallBoundaries()
        {
            var common = Asset<CommonEventDefinition>();
            common.commands.Add(new EventCommand { type = EventCommandType.SetVariable, key = "inner", intValue = 4 });
            common.commands.Add(new EventCommand { type = EventCommandType.End });
            var context = Context(Definition(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common }, new EventCommand { type = EventCommandType.AddGold, intValue = 2 }), "map/step");
            context.Paused = true; Assert.That(context.MoveNext(out _), Is.True); Assert.That(context.InstructionsLastPump, Is.Zero);
            context.Step(); Assert.That(context.MoveNext(out _), Is.True); Assert.That(context.InstructionsLastPump, Is.EqualTo(1)); Assert.That(session.State.GetVariable("inner"), Is.Zero);
            context.Step(); Assert.That(context.MoveNext(out _), Is.True); Assert.That(context.InstructionsLastPump, Is.EqualTo(1)); Assert.That(session.State.GetVariable("inner"), Is.EqualTo(4));
            context.MoveNext(out _); Assert.That(context.InstructionsLastPump, Is.Zero); Assert.That(session.State.player.gold, Is.Zero);
            context.Step(); context.MoveNext(out _); Assert.That(context.InstructionsLastPump, Is.EqualTo(1)); Assert.That(session.State.player.gold, Is.Zero);
            context.Step(); context.MoveNext(out _); Assert.That(context.InstructionsLastPump, Is.EqualTo(1));
            Assert.That(context.Succeeded, Is.True); Assert.That(session.State.player.gold, Is.EqualTo(2));
        }

        [Test]
        public void NestedLoopSharesInstructionBudgetAndLeavesDiagnosticCallStack()
        {
            var common = Asset<CommonEventDefinition>();
            common.commands.Add(new EventCommand { type = EventCommandType.Label, label = "loop" });
            common.commands.Add(new EventCommand { type = EventCommandType.Jump, targetLabel = "loop" });
            var context = Context(Definition(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common }, new EventCommand { type = EventCommandType.AddGold, intValue = 999 }), "map/loop");
            for (int pump = 0; pump < 80 && context.MoveNext(out _); pump++) Assert.That(context.InstructionsLastPump, Is.LessThanOrEqualTo(128));
            Assert.That(context.IsComplete, Is.True); Assert.That(context.Succeeded, Is.False);
            Assert.That(context.Error, Is.TypeOf<InvalidOperationException>()); Assert.That(context.Error.Message, Does.Contain("instruction budget"));
            Assert.That(context.CallStack.Count, Is.EqualTo(2)); Assert.That(context.Trace.Count, Is.LessThanOrEqualTo(256));
            Assert.That(session.State.player.gold, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CancellingCommonDialogueDisposesNestedPresentationAndLeavesRunOnceReplayable()
        {
            presentation.BlockDialogue = true;
            var common = Asset<CommonEventDefinition>(); common.commands.Add(new EventCommand { type = EventCommandType.ShowDialogue, text = "waiting" });
            var definition = Definition(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common }, new EventCommand { type = EventCommandType.AddGold, intValue = 10 });
            definition.pages[0].runOnce = true;
            Assert.That(runner.TryRun(definition, "map/cancel"), Is.True); yield return null;
            Assert.That(runner.IsRunning, Is.True); runner.Cancel();
            Assert.That(presentation.DisposedDialogues, Is.EqualTo(1)); Assert.That(runner.CurrentContext.IsCancelled, Is.True);
            Assert.That(runner.IsRunning, Is.False); Assert.That(definition.IsPageCompleted(session.State, "map/cancel", 0), Is.False);
            Assert.That(session.State.player.gold, Is.Zero);
        }

        [Test]
        public void CompiledSnapshotCannotBeChangedByEditingSourceCommandsMidExecution()
        {
            var definition = Definition(new EventCommand { type = EventCommandType.SetVariable, key = "snapshot", intValue = 7 });
            var context = Context(definition, "map/snapshot");
            definition.pages[0].commands[0].intValue = 100; definition.pages[0].commands.Clear();
            Finish(context); Assert.That(session.State.GetVariable("snapshot"), Is.EqualTo(7));
        }

        sealed class FakePresentation : IEventPresentation
        {
            public readonly List<string> Dialogues = new List<string>();
            public bool BlockDialogue;
            public int DisposedDialogues;
            public IEnumerator ShowDialogue(string speaker, string text)
            {
                Dialogues.Add(speaker + "|" + text);
                try { while (BlockDialogue) yield return null; }
                finally { DisposedDialogues++; }
            }
            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> onSelected) { onSelected(0); yield break; }
        }
    }
}
