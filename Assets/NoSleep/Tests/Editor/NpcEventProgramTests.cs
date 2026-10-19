using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class NpcEventProgramTests
    {
        [Test]
        public void MovementInstructionsKeepEveryExistingSerializedInstructionNumber()
        {
            string[] original = { "ShowDialogue", "ShowChoices", "SetSwitch", "SetVariable", "AddVariable", "SetSelfSwitch", "AddItem", "AddGold", "Wait", "TransferPlayer",
                "IfCondition", "Jump", "Label", "End", "AdvanceTime", "SetObjective", "RecordClue", "JoinCompanion", "LeaveCompanion", "ClearParty", "UnlockAction", "UseItem", "StayAwake", "StartEncounter", "ReachDestination",
                "CallCommon", "RequestForeground", "GrantOnce", "WaitGameMinutes", "Checkpoint" };
            for (int index = 0; index < original.Length; index++) Assert.That((int)Enum.Parse(typeof(EventCommandType), original[index]), Is.EqualTo(index));
            Assert.That((int)EventCommandType.MoveNpc, Is.EqualTo(original.Length));
        }

        [TestCase(EventCommandType.MoveNpc)]
        [TestCase(EventCommandType.FaceNpc)]
        [TestCase(EventCommandType.StopNpc)]
        [TestCase(EventCommandType.ResumeNpc)]
        public void BackgroundCommonCannotTakeOwnershipOfNpcMovement(EventCommandType type)
        {
            var common = ScriptableObject.CreateInstance<CommonEventDefinition>();
            var definition = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                common.commands.Add(new EventCommand { type = type, cell = Vector2Int.one }); common.EnsureStableIds();
                definition.pages.Add(new EventPage { trigger = EventTrigger.Parallel, commands = new List<EventCommand> { new EventCommand { type = EventCommandType.CallCommon, commonEvent = common } } }); definition.EnsureStableIds();
                Assert.That(Assert.Throws<ArgumentException>(() => EventProgram.ValidateForExecution(definition, 0, true)).Message, Does.Contain("not safe"));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(common); }
        }

        [Test]
        public void InvalidDestinationFailsValidationBeforeAnyInstructionCanRun()
        {
            Assert.Throws<ArgumentException>(() => EventProgram.ValidateAndIndex(new List<EventCommand> {
                new EventCommand { type = EventCommandType.AddGold, intValue = 50 },
                new EventCommand { type = EventCommandType.MoveNpc, cell = new Vector2Int(-1, 2) } }));
        }

        [Test]
        public void UnconfiguredMovementCannotFinishRunOnceOrPayFollowingReward()
        {
            var host = new GameObject("NPC command failure"); host.SetActive(false);
            var session = host.AddComponent<GameSession>(); var runner = host.AddComponent<EventRunner>(); runner.Configure(session, null);
            var definition = ScriptableObject.CreateInstance<EventDefinition>();
            try
            {
                definition.pages.Add(new EventPage { runOnce = true, commands = new List<EventCommand> {
                    new EventCommand { type = EventCommandType.MoveNpc, cell = Vector2Int.one }, new EventCommand { type = EventCommandType.AddGold, intValue = 50 } } }); definition.EnsureStableIds();
                using (var execution = runner.CreateExecution(definition, "test/npc"))
                {
                    while (execution.MoveNext(out _)) { }
                    Assert.That(execution.Error, Is.TypeOf<InvalidOperationException>()); Assert.That(execution.Succeeded, Is.False);
                }
                Assert.That(session.State.player.gold, Is.Zero); Assert.That(definition.IsPageCompleted(session.State, "test/npc", 0), Is.False);
            }
            finally { runner.Cancel(); UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
