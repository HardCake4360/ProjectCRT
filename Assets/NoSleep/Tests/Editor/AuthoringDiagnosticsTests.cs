using System;
using System.Collections.Generic;
using System.Linq;
using NoSleep.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class AuthoringDiagnosticsTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        string folder;
        T Asset<T>(string name) where T : ScriptableObject
        { var asset = ScriptableObject.CreateInstance<T>(); asset.name = name; assets.Add(asset); return asset; }
        [TearDown]
        public void TearDown()
        {
            if (folder != null) AssetDatabase.DeleteAsset(folder);
            foreach (var asset in assets) if (asset != null && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear(); folder = null;
        }
        static EventCommand Call(CommonEventDefinition common) => new EventCommand { type = EventCommandType.CallCommon, commonEvent = common };
        static void AssertUnchanged(ScriptableObject asset, string json, bool dirty)
        { Assert.That(JsonUtility.ToJson(asset), Is.EqualTo(json), asset.name); Assert.That(EditorUtility.IsDirty(asset), Is.EqualTo(dirty), asset.name); }

        [Test]
        public void DraftCommonChainDiagnosticsLeaveRootAndAllReachableMissingIdentitiesUnchanged()
        {
            var root = Asset<CommonEventDefinition>("draftRoot");
            var middle = Asset<CommonEventDefinition>("draftMiddle");
            var leaf = Asset<CommonEventDefinition>("draftLeaf");
            root.commands.Add(Call(middle)); middle.commands.Add(Call(leaf));
            leaf.commands.Add(new EventCommand { type = EventCommandType.ShowDialogue, text = "draft" });
            var before = assets.ToDictionary(asset => asset, JsonUtility.ToJson);
            var dirty = assets.ToDictionary(asset => asset, EditorUtility.IsDirty);
            var errors = EventIdentityUpgrader.ValidateCommon(root);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors.Any(error => error.Contains("draftMiddle")), Is.True);
            Assert.That(errors.Any(error => error.Contains("draftLeaf")), Is.True);
            foreach (var asset in assets) AssertUnchanged(asset, before[asset], dirty[asset]);
            Assert.That(root.commonId, Is.Null.Or.Empty); Assert.That(middle.commonId, Is.Null.Or.Empty); Assert.That(leaf.commands[0].commandId, Is.Null.Or.Empty);
        }

        [Test]
        public void ValidDefinitionAndCommonParentDoNotUpgradeAMissingChildIdentityDuringPageDiagnostics()
        {
            var leaf = Asset<CommonEventDefinition>("missingChild"); leaf.commands.Add(new EventCommand { type = EventCommandType.End });
            var middle = Asset<CommonEventDefinition>("validParent"); middle.commands.Add(Call(leaf)); middle.EnsureStableIds();
            var definition = Asset<EventDefinition>("validRoot"); definition.pages.Add(new EventPage { commands = new List<EventCommand> { Call(middle) } }); definition.EnsureStableIds();
            var before = assets.ToDictionary(asset => asset, JsonUtility.ToJson);
            var dirty = assets.ToDictionary(asset => asset, EditorUtility.IsDirty);
            var errors = EventIdentityUpgrader.ValidateDefinitionPage(definition, 0);
            Assert.That(errors.Any(error => error.Contains("missingChild")), Is.True);
            foreach (var asset in assets) AssertUnchanged(asset, before[asset], dirty[asset]);
            Assert.That(leaf.commonId, Is.Null.Or.Empty); Assert.That(leaf.commands[0].commandId, Is.Null.Or.Empty);
        }

        [Test]
        public void InspectingAValidPageCannotRepairAnUnrelatedDraftPageOnTheSameDefinition()
        {
            var definition = Asset<EventDefinition>("mixedPages"); definition.pages.Add(new EventPage()); definition.EnsureStableIds();
            definition.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.End } } });
            string before = JsonUtility.ToJson(definition); bool dirty = EditorUtility.IsDirty(definition);
            Assert.That(EventIdentityUpgrader.ValidateDefinitionPage(definition, 0), Is.Not.Empty);
            AssertUnchanged(definition, before, dirty);
            Assert.That(definition.pages[1].pageId, Is.Null.Or.Empty); Assert.That(definition.pages[1].commands[0].commandId, Is.Null.Or.Empty);
        }

        [Test]
        public void ValidIdentityCyclesStillReportCompilerRecursionWithoutChangingTheirAssets()
        {
            var a = Asset<CommonEventDefinition>("cycleA"); var b = Asset<CommonEventDefinition>("cycleB");
            a.commands.Add(Call(b)); b.commands.Add(Call(a)); a.EnsureStableIds(); b.EnsureStableIds();
            string beforeA = JsonUtility.ToJson(a), beforeB = JsonUtility.ToJson(b);
            bool dirtyA = EditorUtility.IsDirty(a), dirtyB = EditorUtility.IsDirty(b);
            var errors = EventIdentityUpgrader.ValidateCommon(a);
            Assert.That(errors.Any(error => error.Contains("Recursive common event call")), Is.True);
            AssertUnchanged(a, beforeA, dirtyA); AssertUnchanged(b, beforeB, dirtyB);
        }

        [Test]
        public void ProjectCollectorReportsPersistentDraftAssetsWithoutAssigningAnyIdentities()
        {
            string name = "__AuthoringDiagnosticTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/NoSleep", name); folder = "Assets/NoSleep/" + name;
            var child = Asset<CommonEventDefinition>("draftChild"); child.commands.Add(new EventCommand { type = EventCommandType.End });
            AssetDatabase.CreateAsset(child, folder + "/Child.asset");
            var definition = Asset<EventDefinition>("draftDefinition"); definition.pages.Add(new EventPage { commands = new List<EventCommand> { Call(child) } });
            AssetDatabase.CreateAsset(definition, folder + "/Definition.asset");
            string beforeRoot = JsonUtility.ToJson(definition), beforeChild = JsonUtility.ToJson(child);
            bool dirtyRoot = EditorUtility.IsDirty(definition), dirtyChild = EditorUtility.IsDirty(child);
            var errors = EventIdentityUpgrader.CollectErrors();
            Assert.That(errors.Any(error => error.Contains(folder)), Is.True);
            AssertUnchanged(definition, beforeRoot, dirtyRoot); AssertUnchanged(child, beforeChild, dirtyChild);
            Assert.That(definition.definitionId, Is.Null.Or.Empty); Assert.That(child.commonId, Is.Null.Or.Empty);
        }

        [Test]
        public void ValidIdentityBackgroundGraphStillReportsUnsafeDialogueWithoutMutation()
        {
            var common = Asset<CommonEventDefinition>("unsafeCommon"); common.commands.Add(new EventCommand { type = EventCommandType.ShowDialogue, text = "foreground only" }); common.EnsureStableIds();
            var definition = Asset<EventDefinition>("parallelRoot"); definition.pages.Add(new EventPage { trigger = EventTrigger.Parallel, commands = new List<EventCommand> { Call(common) } }); definition.EnsureStableIds();
            string beforeRoot = JsonUtility.ToJson(definition), beforeCommon = JsonUtility.ToJson(common);
            bool dirtyRoot = EditorUtility.IsDirty(definition), dirtyCommon = EditorUtility.IsDirty(common);
            var errors = EventIdentityUpgrader.ValidateDefinitionPage(definition, 0);
            Assert.That(errors.Any(error => error.Contains("not safe")), Is.True);
            AssertUnchanged(definition, beforeRoot, dirtyRoot); AssertUnchanged(common, beforeCommon, dirtyCommon);
        }
    }
}
