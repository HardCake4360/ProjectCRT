using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NoSleep.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class AuthoringRuntimeTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        GameObject host;
        GameSession session;
        EventRunner runner;
        ImmediatePresentation ui;
        [SetUp]
        public void Setup()
        {
            host = new GameObject("Authoring interpretation test"); host.SetActive(false);
            session = host.AddComponent<GameSession>(); runner = host.AddComponent<EventRunner>();
            ui = new ImmediatePresentation(); runner.Configure(session, ui);
        }
        [TearDown]
        public void Cleanup()
        { runner.Cancel(); UnityEngine.Object.DestroyImmediate(host); foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset); assets.Clear(); }
        T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        void Run(EventDefinition definition, string key)
        {
            using (var execution = runner.CreateExecution(definition, key))
            {
                int pumps = 0; while (execution.MoveNext(out _) && pumps++ < 100) { }
                Assert.That(execution.Error, Is.Null); Assert.That(execution.Succeeded, Is.True);
            }
        }
        [TestCase(0, 5)]
        [TestCase(1, 2)]
        public void CopiedChoiceBlockExecutesTheSelectedRemappedBranch(int choice, int gold)
        {
            var source = Asset<EventDefinition>(); source.pages.Add(new EventPage { commands = new List<EventCommand>
            {
                new EventCommand { type = EventCommandType.ShowChoices, text = "Choose", choices = new List<EventChoice> {
                    new EventChoice { text = "Five", targetLabel = "gain" }, new EventChoice { text = "Two", targetLabel = "leave" } } },
                new EventCommand { type = EventCommandType.Label, label = "gain" },
                new EventCommand { type = EventCommandType.AddGold, intValue = 5 }, new EventCommand { type = EventCommandType.End },
                new EventCommand { type = EventCommandType.Label, label = "leave" },
                new EventCommand { type = EventCommandType.AddGold, intValue = 2 }, new EventCommand { type = EventCommandType.End }
            } }); source.EnsureStableIds();
            var target = Asset<EventDefinition>(); target.pages.Add(new EventPage()); target.EnsureStableIds();
            string copied = EventEditOperations.CopyCommands(source, 0, Enumerable.Range(0, source.pages[0].commands.Count).ToArray());
            EventEditOperations.InsertCommands(target, 0, 0, EventEditOperations.ReadClipboard(copied));
            ui.Choice = choice; Run(target, "authoring/copied");
            Assert.That(session.State.player.gold, Is.EqualTo(gold));
            Assert.That(target.pages[0].commands.Select(command => command.commandId).Intersect(source.pages[0].commands.Select(command => command.commandId)), Is.Empty);
        }
        [Test]
        public void TemplateCompletionSurvivesPageReorderingAndSaveRoundtrip()
        {
            var template = EventTemplates.Create(EventTemplateKind.OneTimeReward, new EventTemplateReferences { rewardGold = 17, rewardItemId = "token", rewardItemAmount = 1 }); assets.Add(template);
            var definition = Asset<EventDefinition>();
            EventEditOperations.InsertPages(definition, 0, template.pages);
            string pageId = definition.pages[0].pageId, commandId = definition.pages[0].commands[0].commandId;
            Run(definition, "authoring/reward");
            var save = SaveService.Deserialize(SaveService.Serialize(new SaveData { mapId = "authoring", state = session.State }));
            EventEditOperations.AddPage(definition);
            definition.pages[1].conditions.Add(new StateCondition { kind = ConditionKind.Switch, key = "never", expected = true });
            EventEditOperations.MovePage(definition, 0, 1);
            Assert.That(definition.pages[1].pageId, Is.EqualTo(pageId)); Assert.That(definition.pages[1].commands[0].commandId, Is.EqualTo(commandId));
            Assert.That(definition.IsPageCompleted(save.state, "authoring/reward", 1), Is.True);
            Assert.That(save.state.player.gold, Is.EqualTo(17)); Assert.That(save.state.GetItemCount("token"), Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => runner.CreateExecution(definition, "authoring/reward"));
        }
        [Test]
        public void BranchSelectionIncludesNestedAndBackwardTargetsBeforeCopying()
        {
            var definition = Asset<EventDefinition>(); definition.pages.Add(new EventPage { commands = new List<EventCommand>
            {
                new EventCommand { type = EventCommandType.Label, label = "earlier" },
                new EventCommand { type = EventCommandType.Jump, targetLabel = "later" },
                new EventCommand { type = EventCommandType.Label, label = "chosen" },
                new EventCommand { type = EventCommandType.Jump, targetLabel = "earlier" },
                new EventCommand { type = EventCommandType.Label, label = "later" }, new EventCommand { type = EventCommandType.End }
            } }); definition.EnsureStableIds();
            var range = EventEditorDiagnostics.BranchRange(definition.pages[0].commands, 3);
            Assert.That(range, Is.EqualTo(new Vector2Int(0, 5)));
            var destination = Asset<EventDefinition>(); destination.pages.Add(new EventPage()); destination.EnsureStableIds();
            EventEditOperations.InsertCommands(destination, 0, 0, definition.pages[0].commands.GetRange(range.x, range.y - range.x + 1));
            Assert.DoesNotThrow(() => EventProgram.ValidateForExecution(destination, 0));
        }
        [Test]
        public void MissingPageRepairRetainsOtherIdentitiesAndCanBeUndone()
        {
            var definition = Asset<EventDefinition>(); definition.pages.Add(new EventPage()); definition.EnsureStableIds();
            string pageId = definition.pages[0].pageId; definition.pages.Add(null);
            EventEditorWindow.RepairMissingLists(definition);
            Assert.That(definition.pages[0].pageId, Is.EqualTo(pageId));
            Assert.That(definition.pages[1].pageId, Is.Not.Empty); Assert.That(definition.pages[1].legacyPageIndex, Is.EqualTo(-1));
            string repairedId = definition.pages[1].pageId;
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(definition.pages[0].pageId, Is.EqualTo(pageId));
            // Unity restores inline serializable null entries as empty records.
            // The repaired identity must still be removed by the single Undo.
            Assert.That(definition.pages[1]?.pageId, Is.Null.Or.Empty);
            Undo.PerformRedo(); Assert.That(definition.pages[1].pageId, Is.EqualTo(repairedId));
        }
        [Test]
        public void SequentialTemplatesInCommonExecuteBeforeItsTerminalReturn()
        {
            var common = Asset<CommonEventDefinition>(); common.commands.Add(new EventCommand { type = EventCommandType.End }); common.EnsureStableIds();
            var reward = EventTemplates.Create(EventTemplateKind.OneTimeReward, new EventTemplateReferences { rewardGold = 3, rewardItemAmount = 0 }); assets.Add(reward);
            EventEditorWindow.InsertCommonTemplate(common, reward);
            EventEditorWindow.InsertCommonTemplate(common, reward);
            Assert.That(common.commands.Count(command => command.type == EventCommandType.End), Is.EqualTo(1));
            var caller = Asset<EventDefinition>(); caller.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.CallCommon, commonEvent = common } } }); caller.EnsureStableIds();
            Run(caller, "authoring/common");
            Assert.That(session.State.player.gold, Is.EqualTo(6));
        }
        [Test]
        public void DiagnosticNavigationLocatesMissingChoiceDestinationWithoutChangingDraftIds()
        {
            var definition = Asset<EventDefinition>(); definition.pages.Add(new EventPage { commands = new List<EventCommand>
            { new EventCommand { type = EventCommandType.ShowChoices, text = "Broken", choices = new List<EventChoice> { new EventChoice { text = "Go", targetLabel = "removed" } } } } });
            definition.EnsureStableIds(); string before = JsonUtility.ToJson(definition);
            var diagnostics = EventEditorDiagnostics.Collect(definition);
            Assert.That(diagnostics.Exists(row => row.PageIndex == 0 && row.CommandIndex == 0 && row.Message.Contains("removed")), Is.True);
            Assert.That(JsonUtility.ToJson(definition), Is.EqualTo(before));
        }
        sealed class ImmediatePresentation : IEventPresentation
        {
            public int Choice;
            public IEnumerator ShowDialogue(string speaker, string text) { yield break; }
            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> selected) { selected(Choice); yield break; }
        }
    }
}
