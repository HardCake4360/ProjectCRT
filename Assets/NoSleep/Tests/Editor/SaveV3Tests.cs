using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class SaveV3Tests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        string temporaryDirectory;

        const string V1Fixture = "{\"version\":1,\"mapId\":\"old\",\"position\":{\"x\":2,\"y\":0,\"z\":3},\"yaw\":45," +
            "\"state\":{\"player\":{\"name\":\"Sleepless\",\"maxHealth\":100,\"health\":82,\"maxSanity\":100,\"sanity\":62,\"gold\":13}," +
            "\"switches\":[{\"key\":\"old-door\",\"value\":true}],\"selfSwitches\":[],\"variables\":[{\"key\":\"chapter\",\"value\":2}],\"inventory\":[{\"key\":\"key\",\"value\":1}]}}";
        const string V2Fixture = "{\"version\":2,\"mapId\":\"hospital\",\"position\":{\"x\":2,\"y\":0,\"z\":3},\"yaw\":135," +
            "\"savedUtcTicks\":639267660000000000,\"mapDisplayName\":\"병원\",\"objectiveTitle\":\"다음 증언\"," +
            "\"state\":{\"player\":{\"name\":\"Sleepless\",\"maxHealth\":94,\"health\":67,\"maxSanity\":100,\"sanity\":52,\"gold\":20,\"strength\":10}," +
            "\"switches\":[{\"key\":\"meds_found\",\"value\":true}],\"selfSwitches\":[],\"variables\":[{\"key\":\"chapter\",\"value\":3}],\"inventory\":[{\"key\":\"stimulant\",\"value\":2}]," +
            "\"survival\":{\"elapsedMinutes\":2940,\"startHour\":8,\"fatigue\":42.5,\"awakenedUntilMinutes\":3030,\"awakeningRelief\":17,\"baselineMaxHealth\":100,\"baselineStrength\":12,\"initialized\":true}," +
            "\"journey\":{\"objectiveId\":\"follow_accounts\",\"objectiveTitle\":\"다음 증언\",\"stage\":2,\"clueIds\":[\"hospital_account\"],\"journal\":[{\"id\":\"clue:hospital_account\",\"title\":\"장부\",\"text\":\"흔적\"}],\"memories\":[{\"id\":\"band_night\",\"title\":\"소음\",\"text\":\"함께 웃었다\"}],\"reachedDestination\":false}," +
            "\"party\":{\"knownCompanions\":[\"min\",\"jun\"],\"activeCompanions\":[\"min\"]},\"unlockedActions\":[\"strike\",\"guard\",\"flee\",\"aggressive\",\"observe\"]}}";

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NoSleepSaveV3Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }
        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
            string path = Path.GetFullPath(temporaryDirectory);
            string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(path.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(path).StartsWith("NoSleepSaveV3Tests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        [Test]
        public void OriginalV1SchemaPreservesOldProgressAndRecordsItsSourceVersion()
        {
            SaveData loaded = SaveService.Deserialize(V1Fixture);
            Assert.That(loaded.version, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(loaded.sourceVersion, Is.EqualTo(1));
            Assert.That(loaded.contentVersion, Is.EqualTo(1));
            Assert.That(loaded.state.player.health, Is.EqualTo(82));
            Assert.That(loaded.state.player.strength, Is.EqualTo(12));
            Assert.That(loaded.state.GetSwitch("old-door"), Is.True);
            Assert.That(loaded.state.GetVariable("chapter"), Is.EqualTo(2));
            Assert.That(loaded.state.GetItemCount("key"), Is.EqualTo(1));
            Assert.That(loaded.state.unlockedActions, Is.EquivalentTo(new[] { "strike", "guard", "flee" }));
            Assert.That(loaded.state.committedTransactions, Is.Empty);
            Assert.That(loaded.state.eventCheckpoints, Is.Empty);
        }

        [Test]
        public void OriginalV2SchemaKeepsSurvivalAndEntireJourneyInsteadOfResettingIt()
        {
            string path = Path.Combine(temporaryDirectory, "slot-0.json");
            File.WriteAllText(path, V2Fixture);
            SaveData loaded = SaveService.Read(path);
            Assert.That(loaded.version, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(loaded.sourceVersion, Is.EqualTo(2));
            Assert.That(loaded.state.player.maxHealth, Is.EqualTo(94));
            Assert.That(loaded.state.player.strength, Is.EqualTo(10));
            Assert.That(loaded.state.GetItemCount("stimulant"), Is.EqualTo(2));
            Assert.That(loaded.state.survival.elapsedMinutes, Is.EqualTo(2940));
            Assert.That(loaded.state.survival.fatigue, Is.EqualTo(42.5f));
            Assert.That(loaded.state.survival.awakenedUntilMinutes, Is.EqualTo(3030));
            Assert.That(loaded.state.survival.baselineStrength, Is.EqualTo(12));
            Assert.That(loaded.state.journey.objectiveId, Is.EqualTo("follow_accounts"));
            Assert.That(loaded.state.journey.clueIds, Does.Contain("hospital_account"));
            Assert.That(loaded.state.journey.journal[0].text, Is.EqualTo("흔적"));
            Assert.That(loaded.state.journey.memories[0].text, Is.EqualTo("함께 웃었다"));
            Assert.That(loaded.state.party.knownCompanions, Is.EquivalentTo(new[] { "min", "jun" }));
            Assert.That(loaded.state.party.activeCompanions, Is.EquivalentTo(new[] { "min" }));
            Assert.That(loaded.state.unlockedActions, Does.Contain("observe"));
            Assert.That(loaded.state.committedTransactions, Is.Empty);
            Assert.That(loaded.state.eventCheckpoints, Is.Empty);
            Assert.That(File.ReadAllText(path), Is.EqualTo(V2Fixture), "Migration is a read operation and must preserve the original file.");
        }

        [Test]
        public void V3RoundTripRetainsTransactionAndWaitDeadlineWithoutSerializingRuntimeMetadata()
        {
            var state = new GameState();
            state.survival.elapsedMinutes = 120;
            state.SetEventCheckpoint("map/npc/definition/page", "wait-command", 180);
            Assert.That(state.TryCommitGrant("map/npc/definition/page/grant-command", new[] { new StateMutation { kind = StateMutationKind.AddItem, key = "key", intValue = 1 } }), Is.True);
            var data = new SaveData { mapId = "map", position = new Vector3(1, 0, 2), state = state, sourceVersion = 1 };
            string json = SaveService.Serialize(data);
            Assert.That(json, Does.Not.Contain("sourceVersion"));
            SaveData loaded = SaveService.Deserialize(json);
            Assert.That(loaded.version, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(loaded.sourceVersion, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(loaded.state.GetEventCheckpoint("map/npc/definition/page").dueElapsedMinutes, Is.EqualTo(180));
            Assert.That(loaded.state.GetEventCheckpoint("map/npc/definition/page").checkpointId, Is.EqualTo("wait-command"));
            Assert.That(loaded.state.TryCommitGrant("map/npc/definition/page/grant-command", new[] { new StateMutation { kind = StateMutationKind.AddItem, key = "key", intValue = 1 } }), Is.False);
            Assert.That(loaded.state.GetItemCount("key"), Is.EqualTo(1));
            Assert.That(data.sourceVersion, Is.EqualTo(1));
        }

        [Test]
        public void OneCommitAppliesAllEffectsAndCompletionBeforeItsSingleNotification()
        {
            var state = new GameState();
            int notifications = 0;
            state.Changed += () =>
            {
                notifications++;
                Assert.That(state.player.gold, Is.EqualTo(15));
                Assert.That(state.GetItemCount("key"), Is.EqualTo(1));
                Assert.That(state.GetVariable("score"), Is.EqualTo(4));
                Assert.That(state.GetSelfSwitch("map/npc", "A"), Is.True);
                Assert.That(state.committedTransactions, Does.Contain("grant"));
            };
            var mutations = new[]
            {
                new StateMutation { kind = StateMutationKind.AddGold, intValue = 15 },
                new StateMutation { kind = StateMutationKind.AddItem, key = "key", intValue = 1 },
                new StateMutation { kind = StateMutationKind.SetVariable, key = "score", intValue = 3 },
                new StateMutation { kind = StateMutationKind.AddVariable, key = "score", intValue = 1 },
                new StateMutation { kind = StateMutationKind.SetSelfSwitch, key = "A", boolValue = true }
            };
            Assert.That(state.TryCommitGrant("grant", mutations, "map/npc"), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(state.TryCommitGrant("grant", mutations, "map/npc"), Is.False);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(state.player.gold, Is.EqualTo(15));
        }

        [Test]
        public void InvalidLaterEffectPreservesStateReferencesAndDoesNotMarkTransaction()
        {
            var state = new GameState();
            PlayerStats originalPlayer = state.player;
            string before = JsonUtility.ToJson(state);
            int notifications = 0; state.Changed += () => notifications++;
            Assert.Throws<ArgumentException>(() => state.TryCommitGrant("failed", new[]
            {
                new StateMutation { kind = StateMutationKind.AddGold, intValue = 20 },
                new StateMutation { kind = StateMutationKind.SetVariable, key = " " }
            }));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
            Assert.That(state.player, Is.SameAs(originalPlayer));
            Assert.That(notifications, Is.Zero);
            Assert.That(state.committedTransactions, Does.Not.Contain("failed"));
        }

        [Test]
        public void ReservedProgressEffectRollsBackEarlierRewardsAndCompletion()
        {
            var state = new GameState();
            string before = JsonUtility.ToJson(state);
            int notifications = 0; state.Changed += () => notifications++;
            Assert.Throws<ArgumentException>(() => state.TryCommitGrant("forged", new[]
            {
                new StateMutation { kind = StateMutationKind.AddItem, key = "key", intValue = 1 },
                new StateMutation { kind = StateMutationKind.SetSelfSwitch, key = " __nosleep_completed_forged ", boolValue = true }
            }, "map/npc"));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
            Assert.That(notifications, Is.Zero);
            Assert.That(state.committedTransactions, Does.Not.Contain("forged"));
        }

        [Test]
        public void OverflowAfterAValidEffectRollsBackTheWholeGrant()
        {
            var state = new GameState();
            state.SetVariable("score", int.MaxValue);
            string before = JsonUtility.ToJson(state);
            Assert.Throws<OverflowException>(() => state.TryCommitGrant("overflow", new[]
            {
                new StateMutation { kind = StateMutationKind.SetSwitch, key = "opened", boolValue = true },
                new StateMutation { kind = StateMutationKind.AddVariable, key = "score", intValue = 1 }
            }));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        [Test]
        public void SeparateCallerScopesCanCommitTheSameAuthoredGrantOnceEach()
        {
            var state = new GameState();
            var effects = new[] { new StateMutation { kind = StateMutationKind.AddGold, intValue = 5 } };
            Assert.That(state.TryCommitGrant("map/first/definition/page/grant", effects), Is.True);
            Assert.That(state.TryCommitGrant("map/second/definition/page/grant", effects), Is.True);
            Assert.That(state.TryCommitGrant("map/first/definition/page/grant", effects), Is.False);
            Assert.That(state.player.gold, Is.EqualTo(10));
        }

        [Test]
        public void CapturedLegacyIndexMigratesAfterPageInsertionAndReordering()
        {
            EventDefinition definition = CreateAsset<EventDefinition>();
            definition.pages.Add(new EventPage { name = "First", runOnce = true });
            definition.pages.Add(new EventPage { name = "Reward", runOnce = true });
            definition.EnsureStableIds();
            EventPage reward = definition.pages[1];
            MapDefinition map = CreateMap(definition);
            var state = new GameState();
            state.SetSelfSwitch("map/npc", EventDefinition.CompletionKey(1), true);
            state.SetSelfSwitch("map/npc", "A", true);
            definition.pages.Remove(reward); definition.pages.Insert(0, reward);
            definition.pages.Insert(0, new EventPage { name = "Inserted", runOnce = true });
            definition.EnsureStableIds();
            EventSaveMigration.Migrate(state, new[] { map });
            Assert.That(state.GetSelfSwitch("map/npc", definition.StableCompletionKey(reward.pageId)), Is.True);
            Assert.That(state.HasSelfSwitch("map/npc", EventDefinition.CompletionKey(1)), Is.False);
            Assert.That(state.GetSelfSwitch("map/npc", "A"), Is.True);
            Assert.That(reward.legacyPageIndex, Is.EqualTo(1));
            Assert.That(definition.IsPageCompleted(state, "map/npc", definition.pages.IndexOf(reward)), Is.True);
            Assert.That(definition.IsPageCompleted(state, "map/npc", 0), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void OldSchemaCompletedPageSurvivesBothFormatAndIdentityMigration(int version)
        {
            string mapId = version == 1 ? "old" : "hospital";
            string eventKey = mapId + "/npc";
            string encoded = eventKey.Length + ":" + eventKey + ":" + EventDefinition.CompletionKey(0);
            string fixture = (version == 1 ? V1Fixture : V2Fixture).Replace("\"selfSwitches\":[]",
                "\"selfSwitches\":[{\"key\":\"" + encoded + "\",\"value\":true}]");
            SaveData loaded = SaveService.Deserialize(fixture);
            EventDefinition definition = CreateAsset<EventDefinition>();
            definition.pages.Add(new EventPage { runOnce = true }); definition.EnsureStableIds();
            MapDefinition map = CreateMap(definition); map.mapId = mapId;
            EventSaveMigration.Migrate(loaded.state, new[] { map });
            Assert.That(loaded.sourceVersion, Is.EqualTo(version));
            Assert.That(loaded.version, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(definition.IsPageCompleted(loaded.state, eventKey, 0), Is.True);
            Assert.That(loaded.state.HasSelfSwitch(eventKey, EventDefinition.CompletionKey(0)), Is.False);
        }

        [Test]
        public void StableFalseOverridesAnOldCompletionAndMigrationRemovesTheStaleFlag()
        {
            EventDefinition definition = CreateAsset<EventDefinition>();
            definition.pages.Add(new EventPage { runOnce = true }); definition.EnsureStableIds();
            var state = new GameState();
            state.SetSelfSwitch("map/npc", EventDefinition.CompletionKey(0), true);
            state.SetSelfSwitch("map/npc", definition.StableCompletionKey(definition.pages[0].pageId), false);
            EventSaveMigration.Migrate(state, new[] { CreateMap(definition) });
            Assert.That(definition.IsPageCompleted(state, "map/npc", 0), Is.False);
            Assert.That(state.HasSelfSwitch("map/npc", EventDefinition.CompletionKey(0)), Is.False);
        }

        [Test]
        public void UnknownCompletedPlacementFailsWithoutPartiallyConvertingKnownProgress()
        {
            EventDefinition definition = CreateAsset<EventDefinition>();
            definition.pages.Add(new EventPage { runOnce = true }); definition.EnsureStableIds();
            var state = new GameState();
            state.SetSelfSwitch("map/npc", EventDefinition.CompletionKey(0), true);
            state.SetSelfSwitch("removed/npc", EventDefinition.CompletionKey(0), true);
            string before = JsonUtility.ToJson(state);
            Assert.Throws<InvalidDataException>(() => EventSaveMigration.Migrate(state, new[] { CreateMap(definition) }));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        [Test]
        public void UnknownCapturedIndexFailsInsteadOfUsingTheCurrentPageOrder()
        {
            EventDefinition definition = CreateAsset<EventDefinition>();
            definition.pages.Add(new EventPage { runOnce = true }); definition.EnsureStableIds();
            var state = new GameState();
            state.SetSelfSwitch("map/npc", EventDefinition.CompletionKey(5), true);
            Assert.Throws<InvalidDataException>(() => EventSaveMigration.Migrate(state, new[] { CreateMap(definition) }));
        }

        [Test]
        public void ArbitraryOrdinarySelfSwitchesArePreservedWithoutAContentMapping()
        {
            var state = new GameState();
            state.SetSelfSwitch("home:npc", "A", true);
            string before = JsonUtility.ToJson(state);
            EventSaveMigration.Migrate(state, new MapDefinition[0]);
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        [Test]
        public void ConflictingCheckpointsRejectLoadingAndWritingWithoutChangingLiveState()
        {
            var state = new GameState();
            state.eventCheckpoints.Add(new EventCheckpointState { scope = "scope", checkpointId = "wait", dueElapsedMinutes = 10 });
            state.eventCheckpoints.Add(new EventCheckpointState { scope = "scope", checkpointId = "other", dueElapsedMinutes = 20 });
            var data = new SaveData { mapId = "map", state = state };
            string before = JsonUtility.ToJson(state);
            Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
            Assert.Throws<InvalidDataException>(() => SaveService.Deserialize(JsonUtility.ToJson(data)));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        [Test]
        public void ExactDuplicateProgressNormalizesAndCheckpointSnapshotsCannotMutateTheStoredDeadline()
        {
            var state = new GameState();
            state.committedTransactions.Add(" grant "); state.committedTransactions.Add("grant");
            state.eventCheckpoints.Add(new EventCheckpointState { scope = " scope ", checkpointId = " wait ", dueElapsedMinutes = 10 });
            state.eventCheckpoints.Add(new EventCheckpointState { scope = "scope", checkpointId = "wait", dueElapsedMinutes = 10 });
            state.Normalize();
            Assert.That(state.committedTransactions.Count, Is.EqualTo(1));
            Assert.That(state.eventCheckpoints.Count, Is.EqualTo(1));
            EventCheckpointState read = state.GetEventCheckpoint("scope"); read.dueElapsedMinutes = 999;
            Assert.That(state.GetEventCheckpoint("scope").dueElapsedMinutes, Is.EqualTo(10));
            state.ClearEventCheckpoint("scope");
            Assert.That(state.GetEventCheckpoint("scope"), Is.Null);
        }

        [Test]
        public void CheckpointCatalogResolvesStableCommandAfterRootCommandReordering()
        {
            EventDefinition definition = CreateAsset<EventDefinition>();
            var wait = new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 10 };
            definition.pages.Add(new EventPage { trigger = EventTrigger.Parallel, commands = new List<EventCommand> { wait } });
            definition.EnsureStableIds();
            string scope = "map/npc/" + definition.definitionId + "/" + definition.pages[0].pageId;
            var state = new GameState(); state.SetEventCheckpoint(scope, wait.commandId, 100);
            definition.pages[0].commands.Insert(0, new EventCommand { type = EventCommandType.SetSwitch, key = "start" });
            definition.EnsureStableIds();
            EventSaveMigration.ValidateCheckpoints(state, new[] { CreateMap(definition) });
            Assert.That(state.GetEventCheckpoint(scope).checkpointId, Is.EqualTo(wait.commandId));
            Assert.That(state.GetEventCheckpoint(scope).dueElapsedMinutes, Is.EqualTo(100));
        }

        [TestCase("placement")]
        [TestCase("definition")]
        [TestCase("page")]
        [TestCase("command")]
        [TestCase("command_type")]
        [TestCase("page_trigger")]
        public void RemovedOrChangedCheckpointContentRejectsRestoreWithoutChangingProgress(string changed)
        {
            EventDefinition definition = CreateAsset<EventDefinition>();
            var wait = new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 10 };
            var page = new EventPage { trigger = EventTrigger.Parallel, commands = new List<EventCommand> { wait } };
            definition.pages.Add(page); definition.EnsureStableIds();
            MapDefinition map = CreateMap(definition);
            string scope = "map/npc/" + definition.definitionId + "/" + page.pageId;
            var state = new GameState(); state.SetEventCheckpoint(scope, wait.commandId, 100);
            string before = JsonUtility.ToJson(state);
            switch (changed)
            {
                case "placement": map.events.Clear(); break;
                case "definition": definition.definitionId = Guid.NewGuid().ToString("N"); break;
                case "page": page.pageId = Guid.NewGuid().ToString("N"); break;
                case "command": page.commands.Clear(); break;
                case "command_type": wait.type = EventCommandType.AddGold; break;
                case "page_trigger": page.trigger = EventTrigger.Action; break;
            }
            Assert.Throws<InvalidDataException>(() => EventSaveMigration.ValidateCheckpoints(state, new[] { map }));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(5)]
        public void InvalidAndFutureVersionsCannotReadOrWrite(int version)
        {
            var data = new SaveData { mapId = "map", state = new GameState(), version = version };
            Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
            Assert.Throws<InvalidDataException>(() => SaveService.Deserialize(JsonUtility.ToJson(data)));
        }

        MapDefinition CreateMap(EventDefinition definition)
        {
            MapDefinition map = CreateAsset<MapDefinition>(); map.mapId = "map";
            map.events.Add(new MapEventPlacement { id = "npc", definition = definition }); return map;
        }
        T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset;
        }
    }
}
