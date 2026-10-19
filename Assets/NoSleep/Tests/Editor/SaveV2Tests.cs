using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class SaveV2Tests
    {
        string temporaryDirectory;
        SaveSlotRepository repository;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NoSleepSaveV2Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            repository = new SaveSlotRepository(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            string resolved = Path.GetFullPath(temporaryDirectory);
            string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That(resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved).StartsWith("NoSleepSaveV2Tests-", StringComparison.Ordinal), Is.True);
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        static SaveData CreateSave(string mapId = "hospital")
        {
            return new SaveData { mapId = mapId, mapDisplayName = "병원", objectiveTitle = "각성제 확보",
                position = new Vector3(2, 0.5f, 3), yaw = 90, state = new GameState() };
        }

        [Test]
        public void OriginalV1FileMigratesWithoutLosingProgress()
        {
            const string legacy = "{\"version\":1,\"mapId\":\"home\",\"position\":{\"x\":2,\"y\":0,\"z\":3},\"yaw\":45," +
                "\"state\":{\"player\":{\"name\":\"Sleepless\",\"maxHealth\":100,\"health\":67,\"maxSanity\":100,\"sanity\":52,\"gold\":20}," +
                "\"switches\":[{\"key\":\"door-open\",\"value\":true}],\"selfSwitches\":[{\"key\":\"8:home:npc:A\",\"value\":true}]," +
                "\"variables\":[{\"key\":\"chapter\",\"value\":2}],\"inventory\":[{\"key\":\"brass-key\",\"value\":1}]}}";
            // Use the original filename so migration exercises file lookup as well as JSON.
            File.WriteAllText(Path.Combine(temporaryDirectory, "slot-0.json"), legacy);
            SaveData loaded = repository.Read(0);
            Assert.That(loaded.version, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(loaded.mapId, Is.EqualTo("home"));
            Assert.That(loaded.position, Is.EqualTo(new Vector3(2, 0, 3)));
            Assert.That(loaded.yaw, Is.EqualTo(45));
            Assert.That(loaded.state.player.health, Is.EqualTo(67));
            Assert.That(loaded.state.player.sanity, Is.EqualTo(52));
            Assert.That(loaded.state.player.gold, Is.EqualTo(20));
            Assert.That(loaded.state.player.strength, Is.EqualTo(12));
            Assert.That(loaded.state.GetSwitch("door-open"), Is.True);
            Assert.That(loaded.state.GetSelfSwitch("home:npc", "A"), Is.True);
            Assert.That(loaded.state.GetVariable("chapter"), Is.EqualTo(2));
            Assert.That(loaded.state.GetItemCount("brass-key"), Is.EqualTo(1));
            Assert.That(loaded.state.survival, Is.Not.Null);
            Assert.That(loaded.state.journey, Is.Not.Null);
            Assert.That(loaded.state.party, Is.Not.Null);
            Assert.That(loaded.state.unlockedActions, Is.Not.Null);
            Assert.That(loaded.state.unlockedActions, Is.EquivalentTo(new[] { "strike", "guard", "flee" }));
            Assert.That(loaded.state.survival.elapsedMinutes, Is.Zero);
            Assert.That(loaded.state.survival.startHour, Is.EqualTo(8));
            Assert.That(loaded.savedUtcTicks, Is.Zero);
            Assert.That(File.ReadAllText(repository.GetPath(0)), Is.EqualTo(legacy));
        }

        [Test]
        public void LegacyObjectMustBeLoadedBeforeItCanBeWrittenInV2Format()
        {
            SaveData data = CreateSave();
            data.version = 1;
            Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
            SaveData migrated = SaveService.Deserialize(JsonUtility.ToJson(data));
            Assert.That(SaveService.Deserialize(SaveService.Serialize(migrated)).version, Is.EqualTo(SaveService.CurrentVersion));
        }

        [Test]
        public void V2RoundTripPreservesSurvivalJourneyPartyGrowthAndMetadata()
        {
            SaveData original = CreateSave();
            original.savedUtcTicks = new DateTime(2026, 10, 4, 3, 0, 0, DateTimeKind.Utc).Ticks;
            original.state.survival.elapsedMinutes = 180;
            original.state.survival.startHour = 11;
            original.state.survival.fatigue = 42.5f;
            original.state.survival.awakenedUntilMinutes = 210;
            original.state.survival.awakeningRelief = 12.5f;
            original.state.survival.baselineMaxHealth = 100;
            original.state.survival.baselineStrength = 12;
            original.state.survival.initialized = true;
            original.state.journey.objectiveId = "cross_town";
            original.state.journey.objectiveTitle = "역으로 향한다";
            original.state.journey.stage = 2;
            original.state.journey.reachedDestination = true;
            original.state.journey.clueIds.Add("hospital-record");
            original.state.journey.journal.Add(new JournalRecord { id = "record", title = "병원 기록", text = "어제의 흔적" });
            original.state.journey.memories.Add(new MemoryRecord { id = "companion-memory", title = "약속", text = "같이 돌아가자" });
            original.state.party.knownCompanions.Add("guide");
            original.state.party.activeCompanions.Add("guide");
            original.state.unlockedActions.Add("focus");
            original.state.player.strength = 9;
            original.state.SetSwitch("puzzle-open", true);
            original.state.SetSelfSwitch("hospital:cache", "A", true);
            original.state.SetVariable("chapter", 3);
            original.state.AddItem("stimulant", 2);

            SaveData loaded = SaveService.Deserialize(SaveService.Serialize(original));
            Assert.That(loaded.mapDisplayName, Is.EqualTo(original.mapDisplayName));
            Assert.That(loaded.objectiveTitle, Is.EqualTo(original.objectiveTitle));
            Assert.That(loaded.savedUtcTicks, Is.EqualTo(original.savedUtcTicks));
            Assert.That(loaded.state.survival.elapsedMinutes, Is.EqualTo(180));
            Assert.That(loaded.state.survival.startHour, Is.EqualTo(11));
            Assert.That(loaded.state.survival.fatigue, Is.EqualTo(42.5f));
            Assert.That(loaded.state.survival.awakenedUntilMinutes, Is.EqualTo(210));
            Assert.That(loaded.state.survival.awakeningRelief, Is.EqualTo(12.5f));
            Assert.That(loaded.state.survival.baselineStrength, Is.EqualTo(12));
            Assert.That(loaded.state.survival.initialized, Is.True);
            Assert.That(loaded.state.journey.objectiveId, Is.EqualTo("cross_town"));
            Assert.That(loaded.state.journey.objectiveTitle, Is.EqualTo("역으로 향한다"));
            Assert.That(loaded.state.journey.stage, Is.EqualTo(2));
            Assert.That(loaded.state.journey.reachedDestination, Is.True);
            Assert.That(loaded.state.journey.clueIds, Does.Contain("hospital-record"));
            Assert.That(loaded.state.journey.journal[0].text, Is.EqualTo("어제의 흔적"));
            Assert.That(loaded.state.journey.memories[0].text, Is.EqualTo("같이 돌아가자"));
            Assert.That(loaded.state.party.knownCompanions, Does.Contain("guide"));
            Assert.That(loaded.state.party.activeCompanions, Does.Contain("guide"));
            Assert.That(loaded.state.unlockedActions, Does.Contain("focus"));
            Assert.That(loaded.state.player.strength, Is.EqualTo(9));
            Assert.That(loaded.state.GetSwitch("puzzle-open"), Is.True);
            Assert.That(loaded.state.GetSelfSwitch("hospital:cache", "A"), Is.True);
            Assert.That(loaded.state.GetVariable("chapter"), Is.EqualTo(3));
            Assert.That(loaded.state.GetItemCount("stimulant"), Is.EqualTo(2));
        }

        [Test]
        public void WriteStampsUtcWithoutMutatingCallerOrNormalizeChangingLiveState()
        {
            SaveData original = CreateSave();
            original.mapDisplayName = null;
            original.state.player.health = 500;
            long before = DateTime.UtcNow.Ticks;
            repository.Write(0, original);
            long after = DateTime.UtcNow.Ticks;
            SaveData loaded = repository.Read(0);
            Assert.That(loaded.savedUtcTicks, Is.InRange(before, after));
            Assert.That(loaded.mapDisplayName, Is.EqualTo("hospital"));
            Assert.That(loaded.state.player.health, Is.EqualTo(100));
            Assert.That(original.savedUtcTicks, Is.Zero);
            Assert.That(original.mapDisplayName, Is.Null);
            Assert.That(original.state.player.health, Is.EqualTo(500));
        }

        [Test]
        public void SuppliedUtcTimestampIsPreserved()
        {
            SaveData original = CreateSave();
            original.savedUtcTicks = new DateTime(2026, 10, 3, 13, 0, 0, DateTimeKind.Utc).Ticks;
            repository.Write(0, original);
            Assert.That(repository.Read(0).savedUtcTicks, Is.EqualTo(original.savedUtcTicks));
        }

        [TestCase(-1L)]
        [TestCase(long.MaxValue)]
        public void InvalidTimestampFailsBeforeWriting(long ticks)
        {
            SaveData original = CreateSave();
            original.savedUtcTicks = ticks;
            Assert.Throws<InvalidDataException>(() => repository.Write(0, original));
            Assert.That(File.Exists(repository.GetPath(0)), Is.False);
        }

        [Test]
        public void ListingShowsSixEmptySlotsAndOriginalFileNames()
        {
            var slots = repository.ListSlots();
            Assert.That(slots.Count, Is.EqualTo(6));
            for (int slot = 0; slot < slots.Count; slot++)
            {
                Assert.That(slots[slot].slot, Is.EqualTo(slot));
                Assert.That(slots[slot].exists, Is.False);
                Assert.That(slots[slot].valid, Is.False);
                Assert.That(slots[slot].hasBackup, Is.False);
                Assert.That(slots[slot].error, Is.Empty);
            }
            Assert.That(Path.GetFileName(repository.GetPath(0)), Is.EqualTo("slot-0.json"));
        }

        [Test]
        public void ListingContinuesAcrossValidCorruptAndRecoverableSlots()
        {
            repository.Write(0, CreateSave());
            File.WriteAllText(repository.GetPath(1), "corrupt");
            repository.Write(2, CreateSave("home"));
            repository.Write(2, CreateSave("street"));
            File.WriteAllText(repository.GetPath(2), "corrupt");
            repository.Write(3, CreateSave());
            File.WriteAllText(repository.GetPath(3) + ".bak", "corrupt backup");
            var slots = repository.ListSlots();
            Assert.That(slots.Count, Is.EqualTo(6));
            Assert.That(slots[0].valid, Is.True);
            Assert.That(slots[0].data.mapId, Is.EqualTo("hospital"));
            Assert.That(slots[1].exists, Is.True);
            Assert.That(slots[1].valid, Is.False);
            Assert.That(slots[1].error, Is.Not.Empty);
            Assert.That(slots[2].valid, Is.False);
            Assert.That(slots[2].hasBackup, Is.True);
            Assert.That(slots[2].backupValid, Is.True);
            Assert.That(slots[3].valid, Is.True);
            Assert.That(slots[3].hasBackup, Is.True);
            Assert.That(slots[3].backupValid, Is.False);
            Assert.That(slots[3].error, Does.Contain("Backup:"));
            Assert.That(slots[4].exists, Is.False);
        }

        [Test]
        public void DirectoryInPlaceOfFileBecomesPerSlotError()
        {
            Directory.CreateDirectory(repository.GetPath(1));
            var slots = repository.ListSlots();
            Assert.That(slots[1].exists, Is.True);
            Assert.That(slots[1].valid, Is.False);
            Assert.That(slots[1].error, Does.Contain("directory"));
            Assert.That(slots[2].exists, Is.False);
        }

        [Test]
        public void FileInPlaceOfSaveDirectoryBecomesPerSlotError()
        {
            string path = Path.Combine(temporaryDirectory, "not-a-directory");
            File.WriteAllText(path, "occupied");
            var invalidRepository = new SaveSlotRepository(path);
            var slots = invalidRepository.ListSlots();
            Assert.That(slots.Count, Is.EqualTo(6));
            foreach (SaveSlotInfo info in slots)
            {
                Assert.That(info.valid, Is.False);
                Assert.That(info.error, Does.Contain("Save directory is a file"));
            }
        }

        [Test]
        public void BackupCanBeReadEvenWhenPrimaryIsMissing()
        {
            repository.Write(0, CreateSave("home"));
            repository.Write(0, CreateSave("street"));
            File.Delete(repository.GetPath(0));
            SaveSlotInfo info = repository.Inspect(0);
            Assert.That(info.exists, Is.False);
            Assert.That(info.hasBackup, Is.True);
            Assert.That(info.backupValid, Is.True);
            Assert.That(repository.Read(0, true).mapId, Is.EqualTo("home"));
            Assert.That(File.Exists(repository.GetPath(0)), Is.False);
        }

        [Test]
        public void BackupReadNeverOverwritesPrimaryOrBackup()
        {
            repository.Write(0, CreateSave("home"));
            repository.Write(0, CreateSave("street"));
            string primary = File.ReadAllText(repository.GetPath(0));
            string backup = File.ReadAllText(repository.GetPath(0) + ".bak");
            SaveData recovered = repository.Read(0, true);
            recovered.mapId = "other";
            recovered.state.player.health = 1;
            Assert.That(File.ReadAllText(repository.GetPath(0)), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(repository.GetPath(0) + ".bak"), Is.EqualTo(backup));
            Assert.That(repository.Read(0).mapId, Is.EqualTo("street"));
        }

        [Test]
        public void FailedIoWritePreservesPrimaryAndPreviousBackup()
        {
            repository.Write(0, CreateSave("home"));
            repository.Write(0, CreateSave("street"));
            string primary = File.ReadAllText(repository.GetPath(0));
            string backup = File.ReadAllText(repository.GetPath(0) + ".bak");
            Directory.CreateDirectory(repository.GetPath(0) + ".tmp");
            Assert.Catch<Exception>(() => repository.Write(0, CreateSave("hospital")));
            Assert.That(File.ReadAllText(repository.GetPath(0)), Is.EqualTo(primary));
            Assert.That(File.ReadAllText(repository.GetPath(0) + ".bak"), Is.EqualTo(backup));
        }

        [TestCase(-1)]
        [TestCase(100)]
        public void SlotOutOfRangeFails(int slot)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => repository.GetPath(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => repository.Inspect(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => repository.Read(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => repository.Write(slot, CreateSave()));
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void SlotCountOutOfRangeFails(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => repository.ListSlots(count));
        }

        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("[{\"version\":2,\"mapId\":\"home\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0,\"state\":{}}]")]
        [TestCase("{\"version\":2,\"version\":2,\"mapId\":\"home\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0,\"state\":{}}")]
        [TestCase("{\"version\":2,\"mapId\":\"home\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0,\"state\":{}}{}")]
        [TestCase("{\"version\":2,\"mapId\":\"home\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0,\"state\":[]}")]
        public void InvalidRootStructuresCannotBecomePlayableSaves(string json)
        {
            Assert.Throws<InvalidDataException>(() => SaveService.Deserialize(json));
        }
    }
}
