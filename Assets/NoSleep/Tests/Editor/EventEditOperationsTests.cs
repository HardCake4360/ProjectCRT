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
    public sealed class EventEditOperationsTests
    {
        readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
        string folder;
        EventDefinition owner;
        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            owner = Asset<EventDefinition>(); owner.pages.Add(new EventPage());
        }
        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
            foreach (var value in temporary) if (value != null && !EditorUtility.IsPersistent(value)) UnityEngine.Object.DestroyImmediate(value);
            temporary.Clear(); folder = null;
        }
        T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); temporary.Add(value); return value; }
        T Persistent<T>(string name) where T : ScriptableObject
        {
            if (folder == null)
            {
                string namePart = "__NoSleepEventEditTests_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", namePart); folder = "Assets/" + namePart;
            }
            var value = Asset<T>(); AssetDatabase.CreateAsset(value, folder + "/" + name + ".asset"); return value;
        }
        static EventCommand Label(string name) => new EventCommand { type = EventCommandType.Label, label = name };
        static EventCommand Variable(string key) => new EventCommand { type = EventCommandType.SetVariable, key = key, intValue = 1 };
        List<EventCommand> Commands => owner.pages[0].commands;

        [Test]
        public void PasteRemapsInternalJumpConditionAndChoiceTargetsAndDeepCopiesTheirData()
        {
            Commands.Add(Label("branch")); Commands.Add(Label("branch_copy")); owner.EnsureStableIds();
            var source = new List<EventCommand>
            {
                Label("branch"),
                new EventCommand { type = EventCommandType.IfCondition, targetLabel = "branch", condition = new StateCondition { kind = ConditionKind.Switch, key = "open" } },
                new EventCommand { type = EventCommandType.Jump, targetLabel = "branch" },
                new EventCommand { type = EventCommandType.ShowChoices, choices = new List<EventChoice> { new EventChoice { text = "선택", targetLabel = "branch" } } }
            };
            EventProgram.EnsureCommandIds(source); string[] originalIds = source.Select(command => command.commandId).ToArray();
            var positions = EventEditOperations.InsertCommands(owner, 0, 2, source);
            Assert.That(positions, Is.EqualTo(new[] { 2, 3, 4, 5 }));
            Assert.That(Commands[2].label, Is.EqualTo("branch_copy2"));
            Assert.That(Commands[3].targetLabel, Is.EqualTo(Commands[2].label));
            Assert.That(Commands[4].targetLabel, Is.EqualTo(Commands[2].label));
            Assert.That(Commands[5].choices[0].targetLabel, Is.EqualTo(Commands[2].label));
            for (int index = 0; index < source.Count; index++) Assert.That(Commands[index + 2].commandId, Is.Not.EqualTo(originalIds[index]));
            Commands[3].condition.key = "changed"; Commands[5].choices[0].text = "changed";
            Assert.That(source[1].condition.key, Is.EqualTo("open")); Assert.That(source[3].choices[0].text, Is.EqualTo("선택"));
            Assert.That(source[0].label, Is.EqualTo("branch"));
        }

        [Test]
        public void ExternalTargetsRequireExplicitPermissionAndAnExistingDestinationLabel()
        {
            Commands.Add(Label("outside"));
            string before = JsonUtility.ToJson(owner);
            var branch = new[] { new EventCommand { type = EventCommandType.Jump, targetLabel = "outside" } };
            Assert.Throws<ArgumentException>(() => EventEditOperations.InsertCommands(owner, 0, 0, branch));
            Assert.That(JsonUtility.ToJson(owner), Is.EqualTo(before));
            EventEditOperations.InsertCommands(owner, 0, 0, branch, true);
            Assert.That(Commands[0].targetLabel, Is.EqualTo("outside"));
            Assert.Throws<ArgumentException>(() => EventEditOperations.InsertCommands(owner, 0, 0,
                new[] { new EventCommand { type = EventCommandType.Jump, targetLabel = "absent" } }, true));
        }

        [Test]
        public void FailedPasteDoesNotPartiallyMutateIdsOrConsumeThePreviousUndoOperation()
        {
            EventEditOperations.InsertCommands(owner, 0, 0, new[] { Variable("seed") });
            string afterSeed = JsonUtility.ToJson(owner);
            Assert.Throws<ArgumentException>(() => EventEditOperations.InsertCommands(owner, 0, 1, new[] { Label("duplicate"), Label("duplicate") }));
            Assert.That(JsonUtility.ToJson(owner), Is.EqualTo(afterSeed));
            Undo.PerformUndo();
            Assert.That(Commands, Is.Empty); Assert.That(owner.definitionId, Is.Null.Or.Empty);
        }

        [Test]
        public void UndoRedoRestoresTheEntireInsertAndItsInitialIdentityUpgrade()
        {
            Commands.Add(Variable("original")); string before = JsonUtility.ToJson(owner);
            EventEditOperations.InsertCommands(owner, 0, 1, new[] { Variable("new") });
            string after = JsonUtility.ToJson(owner);
            Assert.That(owner.pages[0].legacyPageIndex, Is.Zero); Assert.That(owner.ValidateIdentity(), Is.Empty);
            Undo.PerformUndo(); Assert.That(JsonUtility.ToJson(owner), Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(JsonUtility.ToJson(owner), Is.EqualTo(after));
        }

        [Test]
        public void MoveUsesOriginalInsertionBoundaryAndPreservesSelectedOrderAndIds()
        {
            foreach (string name in new[] { "a", "b", "c", "d", "e" }) Commands.Add(Variable(name));
            owner.EnsureStableIds(); var ids = Commands.ToDictionary(command => command.key, command => command.commandId);
            var positions = EventEditOperations.MoveCommands(owner, 0, new[] { 3, 1, 1 }, 5);
            Assert.That(positions, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(Commands.Select(command => command.key), Is.EqualTo(new[] { "a", "c", "e", "b", "d" }));
            foreach (var command in Commands) Assert.That(command.commandId, Is.EqualTo(ids[command.key]));
            Undo.PerformUndo(); Assert.That(Commands.Select(command => command.key), Is.EqualTo(new[] { "a", "b", "c", "d", "e" }));
            Undo.PerformRedo(); Assert.That(Commands.Select(command => command.key), Is.EqualTo(new[] { "a", "c", "e", "b", "d" }));
        }

        [Test]
        public void RemovingAReferencedLabelIsAtomicButRemovingItsBranchesTogetherIsAllowed()
        {
            Commands.Add(new EventCommand { type = EventCommandType.ShowChoices, choices = new List<EventChoice> { new EventChoice { text = "경로", targetLabel = "destination" } } });
            Commands.Add(Label("destination")); Commands.Add(Variable("remaining")); owner.EnsureStableIds();
            string before = JsonUtility.ToJson(owner);
            Assert.Throws<ArgumentException>(() => EventEditOperations.RemoveCommands(owner, 0, new[] { 1 }));
            Assert.That(JsonUtility.ToJson(owner), Is.EqualTo(before));
            EventEditOperations.RemoveCommands(owner, 0, new[] { 0, 1 }); Assert.That(Commands.Count, Is.EqualTo(1));
            Undo.PerformUndo(); Assert.That(JsonUtility.ToJson(owner), Is.EqualTo(before));
        }

        [Test]
        public void SemanticPlaceholdersAndEmptyDraftTargetsRemainEditable()
        {
            EventEditOperations.InsertCommands(owner, 0, 0, new[]
            {
                new EventCommand { type = EventCommandType.SetVariable }, new EventCommand { type = EventCommandType.StartEncounter },
                new EventCommand { type = EventCommandType.StayAwake }, new EventCommand { type = EventCommandType.Jump }, Label("")
            });
            Assert.That(Commands.Count, Is.EqualTo(5)); Assert.That(owner.ValidateIdentity(), Is.Empty);
            Assert.Throws<ArgumentException>(() => EventProgram.ValidateForExecution(owner, 0));
        }

        [Test]
        public void InvalidRecordsEnumsAndIndicesAreRejectedBeforeIdentityUpgrade()
        {
            string before = JsonUtility.ToJson(owner);
            Assert.Throws<ArgumentException>(() => EventEditOperations.InsertCommands(owner, 0, 0, new EventCommand[] { null }));
            Assert.Throws<ArgumentException>(() => EventEditOperations.InsertCommands(owner, 0, 0, new[] { new EventCommand { type = (EventCommandType)999 } }));
            Assert.Throws<ArgumentOutOfRangeException>(() => EventEditOperations.InsertCommands(owner, 0, 1, new[] { Variable("x") }));
            Assert.Throws<ArgumentOutOfRangeException>(() => EventEditOperations.RemoveCommands(owner, 0, new[] { 0 }));
            Assert.That(JsonUtility.ToJson(owner), Is.EqualTo(before));
        }

        [Test]
        public void PageCopyGetsNewIdentitiesWhileMoveKeepsCapturedLegacyIndices()
        {
            var enemy = Asset<EnemyDefinition>();
            owner.pages[0].name = "original"; owner.pages[0].conditions.Add(new StateCondition { kind = ConditionKind.Switch, key = "present" });
            Commands.Add(new EventCommand { type = EventCommandType.StartEncounter, enemy = enemy });
            owner.pages.Add(new EventPage { name = "second" });
            EventEditOperations.MovePage(owner, 0, 1);
            var original = owner.pages[1]; string pageId = original.pageId, commandId = original.commands[0].commandId;
            Assert.That(original.legacyPageIndex, Is.Zero); Assert.That(owner.pages[0].legacyPageIndex, Is.EqualTo(1));
            int copyIndex = EventEditOperations.DuplicatePage(owner, 1); var copy = owner.pages[copyIndex];
            Assert.That(copy.pageId, Is.Not.EqualTo(pageId)); Assert.That(copy.legacyPageIndex, Is.EqualTo(-1));
            Assert.That(copy.commands[0].commandId, Is.Not.EqualTo(commandId)); Assert.That(copy.commands[0].enemy, Is.SameAs(enemy));
            copy.conditions[0].key = "copy"; Assert.That(original.conditions[0].key, Is.EqualTo("present"));
            Undo.PerformUndo(); Assert.That(owner.pages.Count, Is.EqualTo(2));
            Assert.That(owner.pages[1].pageId, Is.EqualTo(pageId)); Undo.PerformRedo(); Assert.That(owner.pages[2].pageId, Is.EqualTo(copy.pageId));
        }

        [Test]
        public void AddAndRemovePageAreUndoableAndNewPagesNeverCaptureLegacyIndices()
        {
            int index = EventEditOperations.AddPage(owner);
            Assert.That(index, Is.EqualTo(1)); Assert.That(owner.pages[0].legacyPageIndex, Is.Zero); Assert.That(owner.pages[1].legacyPageIndex, Is.EqualTo(-1));
            string addedId = owner.pages[1].pageId;
            EventEditOperations.RemovePage(owner, 1); Assert.That(owner.pages.Count, Is.EqualTo(1));
            Undo.PerformUndo(); Assert.That(owner.pages[1].pageId, Is.EqualTo(addedId));
            Undo.PerformUndo(); Assert.That(owner.pages.Count, Is.EqualTo(1)); Assert.That(owner.definitionId, Is.Null.Or.Empty);
        }

        [Test]
        public void CommonCommandOperationsUseTheSameUndoAndIdentityContractWithoutReadSideEffects()
        {
            var common = Asset<CommonEventDefinition>();
            Assert.That(EventEditOperations.GetCommands(common, -1), Is.SameAs(common.commands)); Assert.That(common.commonId, Is.Null.Or.Empty);
            EventEditOperations.InsertCommands(common, -1, 0, new[] { Variable("first"), Variable("second") });
            string id = common.commands[0].commandId; Assert.That(common.ValidateIdentity(), Is.Empty);
            EventEditOperations.MoveCommands(common, 0, new[] { 0 }, 2); Assert.That(common.commands[1].commandId, Is.EqualTo(id));
            Undo.PerformUndo(); Assert.That(common.commands[0].commandId, Is.EqualTo(id));
            Undo.PerformUndo(); Assert.That(common.commands, Is.Empty); Assert.That(common.commonId, Is.Null.Or.Empty);
        }

        [Test]
        public void ClipboardRoundtripPreservesPersistentAssetReferencesAndTypedNestedFields()
        {
            var map = Persistent<MapDefinition>("Map"); var item = Persistent<ItemEffectDefinition>("Item");
            var night = Persistent<NightActivityDefinition>("Night"); var enemy = Persistent<EnemyDefinition>("Enemy");
            var common = Persistent<CommonEventDefinition>("Common"); var foreground = Persistent<EventDefinition>("Foreground");
            Commands.Add(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common, map = map, item = item,
                nightActivity = night, enemy = enemy, foregroundEvent = foreground,
                arguments = new List<EventArgument> { new EventArgument { name = "amount", type = EventParameterType.Int, intValue = 13 } },
                mutations = new List<StateMutation> { new StateMutation { kind = StateMutationKind.AddItem, key = "medicine", intValue = 2 } } });
            owner.EnsureStableIds(); string originalId = Commands[0].commandId;
            var decoded = EventEditOperations.ReadClipboard(EventEditOperations.CopyCommands(owner, 0, new[] { 0 }));
            EventEditOperations.InsertCommands(owner, 0, 1, decoded);
            var copy = Commands[1]; Assert.That(copy.commandId, Is.Not.EqualTo(originalId));
            Assert.That(copy.map, Is.SameAs(map)); Assert.That(copy.item, Is.SameAs(item)); Assert.That(copy.nightActivity, Is.SameAs(night));
            Assert.That(copy.enemy, Is.SameAs(enemy)); Assert.That(copy.commonEvent, Is.SameAs(common)); Assert.That(copy.foregroundEvent, Is.SameAs(foreground));
            Assert.That(copy.arguments[0].intValue, Is.EqualTo(13)); Assert.That(copy.mutations[0].key, Is.EqualTo("medicine"));
            copy.arguments[0].intValue = 99; Assert.That(Commands[0].arguments[0].intValue, Is.EqualTo(13));
        }

        [Test]
        public void ClipboardRejectsMissingPersistentReferencesAndTransientReferences()
        {
            var enemy = Persistent<EnemyDefinition>("MissingEnemy"); Commands.Add(new EventCommand { type = EventCommandType.StartEncounter, enemy = enemy });
            string payload = EventEditOperations.CopyCommands(owner, 0, new[] { 0 });
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(enemy));
            Assert.Throws<ArgumentException>(() => EventEditOperations.ReadClipboard(payload));
            Commands[0].enemy = Asset<EnemyDefinition>();
            Assert.Throws<ArgumentException>(() => EventEditOperations.CopyCommands(owner, 0, new[] { 0 }));
            Assert.Throws<ArgumentException>(() => EventEditOperations.ReadClipboard("{}"));
        }

        [Test]
        public void ClipboardLabelsRemapWhenPastingFromAnEventToACommonProgram()
        {
            Commands.Add(Label("end")); Commands.Add(new EventCommand { type = EventCommandType.Jump, targetLabel = "end" });
            var common = Asset<CommonEventDefinition>(); common.commands.Add(Label("end"));
            var decoded = EventEditOperations.ReadClipboard(EventEditOperations.CopyCommands(owner, 0, new[] { 0, 1 }));
            EventEditOperations.InsertCommands(common, -1, 1, decoded);
            Assert.That(common.commands[1].label, Is.EqualTo("end_copy")); Assert.That(common.commands[2].targetLabel, Is.EqualTo("end_copy"));
            Assert.That(common.ValidateIdentity(), Is.Empty);
        }

        [Test]
        public void FactoryDraftDiagnosticsIdentifyTheExactMissingReferenceFields()
        {
            foreach (EventTemplateKind kind in Enum.GetValues(typeof(EventTemplateKind)))
            {
                var template = EventTemplates.Create(kind); temporary.Add(template); var errors = template.GetDiagnostics();
                if (kind == EventTemplateKind.NpcDialogue || kind == EventTemplateKind.OneTimeReward) Assert.That(errors, Is.Empty);
                else
                {
                    string field = kind == EventTemplateKind.ConditionalBranch ? "map" : kind == EventTemplateKind.StayAwake ? "nightActivity" : kind == EventTemplateKind.ContactCombat ? "enemy" : "commonEvent";
                    Assert.That(errors.Any(error => error.Contains("명령") && error.Contains("(" + field + ")")), Is.True, kind.ToString());
                }
            }
        }

        [Test]
        public void AllSixLinkedTemplatesValidateAndApplyWithoutReusingTemplateIdentities()
        {
            var tile = Asset<TileDefinition>(); var map = Asset<MapDefinition>(); map.Resize(2, 2); map.Fill(tile);
            var common = Asset<CommonEventDefinition>();
            common.parameters.Add(new EventParameter { name = "who", type = EventParameterType.String, stringValue = "NPC" });
            common.parameters.Add(new EventParameter { name = "amount", type = EventParameterType.Int, intValue = 4 });
            common.parameters.Add(new EventParameter { name = "allowed", type = EventParameterType.Bool, boolValue = true });
            common.commands.Add(new EventCommand { type = EventCommandType.ShowDialogue, speaker = "{param:who}", text = "대화" });
            var refs = new EventTemplateReferences { destinationMap = map, destinationCell = Vector2Int.one,
                nightActivity = Asset<NightActivityDefinition>(), enemy = Asset<EnemyDefinition>(), commonEvent = common };
            foreach (EventTemplateKind kind in Enum.GetValues(typeof(EventTemplateKind)))
            {
                var template = EventTemplates.Create(kind, refs); temporary.Add(template);
                Assert.That(template.GetDiagnostics(), Is.Empty, kind.ToString());
                int insertion = owner.pages.Count; EventEditOperations.InsertPages(owner, insertion, template.pages);
                Assert.That(owner.pages[insertion].pageId, Is.Not.EqualTo(template.pages[0].pageId));
                Assert.That(owner.pages[insertion].legacyPageIndex, Is.EqualTo(-1));
                Assert.That(owner.pages[insertion].commands[0].commandId, Is.Not.EqualTo(template.pages[0].commands[0].commandId));
                if (kind == EventTemplateKind.CommonCall)
                {
                    var args = template.pages[0].commands[0].arguments;
                    Assert.That(args.Select(argument => argument.type), Is.EqualTo(new[] { EventParameterType.String, EventParameterType.Int, EventParameterType.Bool }));
                }
            }
            Assert.That(common.commonId, Is.Null.Or.Empty, "Read-only template validation must not upgrade a referenced authoring asset.");
            Assert.That(owner.ValidateIdentity(), Is.Empty);
        }

        [Test]
        public void GeneratedRewardAndCommonTemplatesExecuteThroughTheRealInterpreter()
        {
            var host = new GameObject("Template interpreter test"); temporary.Add(host); host.SetActive(false);
            var session = host.AddComponent<GameSession>(); var runner = host.AddComponent<EventRunner>();
            runner.Configure(session, new ImmediatePresentation());
            var common = Asset<CommonEventDefinition>(); common.parameters.Add(new EventParameter { name = "amount", type = EventParameterType.Int, intValue = 17 });
            common.commands.Add(new EventCommand { type = EventCommandType.SetVariable, key = "fromTemplate", intParameter = "amount" });
            var references = new EventTemplateReferences { commonEvent = common, rewardGold = 8, rewardItemId = "medicine", rewardItemAmount = 2 };
            var reward = EventTemplates.Create(EventTemplateKind.OneTimeReward, references); temporary.Add(reward);
            var call = EventTemplates.Create(EventTemplateKind.CommonCall, references); temporary.Add(call);
            owner.pages.Clear(); EventEditOperations.InsertPages(owner, 0, reward.pages);
            using (var context = runner.CreateExecution(owner, "test/reward", 0))
            { Assert.That(context.MoveNext(out _), Is.False); Assert.That(context.Succeeded, Is.True); Assert.That(context.Error, Is.Null); }
            Assert.That(session.State.player.gold, Is.EqualTo(8)); Assert.That(session.State.GetItemCount("medicine"), Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => runner.CreateExecution(owner, "test/reward", 0));
            owner.pages.Clear(); EventEditOperations.InsertPages(owner, 0, call.pages);
            using (var context = runner.CreateExecution(owner, "test/common", 0))
            { Assert.That(context.MoveNext(out _), Is.False); Assert.That(context.Succeeded, Is.True); Assert.That(context.Error, Is.Null); }
            Assert.That(session.State.GetVariable("fromTemplate"), Is.EqualTo(17));
        }

        sealed class ImmediatePresentation : IEventPresentation
        {
            public IEnumerator ShowDialogue(string speaker, string text) { yield break; }
            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> selected) { selected(0); yield break; }
        }
    }
}
