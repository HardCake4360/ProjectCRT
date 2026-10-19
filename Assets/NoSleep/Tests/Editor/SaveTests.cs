using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class SaveTests
    {
        string temporaryDirectory;

        [SetUp]
        public void CreateTemporaryDirectory()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NoSleepSaveTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void RemoveTemporaryDirectory()
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
        }

        static SaveData CreateSave()
        {
            var state = new GameState();
            state.player.name = "Sleepless";
            state.player.health = 72;
            state.player.sanity = 48;
            state.AddGold(15);
            state.SetSwitch("door-open", true);
            state.SetSelfSwitch("home:npc", "A", true);
            state.SetVariable("chapter", 2);
            state.AddItem("brass-key", 1);
            return new SaveData { mapId = "home", position = new Vector3(2.5f, 0.75f, -4), yaw = 135, state = state };
        }

        [Test]
        public void RoundTripPreservesMapPositionStatsAndEventState()
        {
            var original = CreateSave();
            int notifications = 0;
            original.state.Changed += () => notifications++;
            SaveData restored = SaveService.Deserialize(SaveService.Serialize(original));
            Assert.That(restored.version, Is.EqualTo(SaveService.CurrentVersion));
            Assert.That(restored.mapId, Is.EqualTo("home"));
            Assert.That(restored.position, Is.EqualTo(original.position));
            Assert.That(restored.yaw, Is.EqualTo(135));
            Assert.That(restored.state.player.name, Is.EqualTo("Sleepless"));
            Assert.That(restored.state.player.health, Is.EqualTo(72));
            Assert.That(restored.state.player.sanity, Is.EqualTo(48));
            Assert.That(restored.state.player.gold, Is.EqualTo(15));
            Assert.That(restored.state.GetSwitch("door-open"), Is.True);
            Assert.That(restored.state.GetSelfSwitch("home:npc", "A"), Is.True);
            Assert.That(restored.state.GetVariable("chapter"), Is.EqualTo(2));
            Assert.That(restored.state.GetItemCount("brass-key"), Is.EqualTo(1));
            Assert.That(restored.state.Changed, Is.Null);
            Assert.That(notifications, Is.Zero);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase("{")]
        [TestCase("{}")]
        [TestCase("{\"version\":1,\"mapId\":\"home\"}")]
        [TestCase("{\"version\":1,\"state\":{}}")]
        [TestCase("{\"version\":1,\"mapId\":\"home\",\"state\":{}}")]
        [TestCase("{\"version\":1,\"mapId\":\"home\",\"state\":{},\"position\":{\"x\":0,\"y\":0,\"z\":0}}")]
        [TestCase("{\"version\":1,\"mapId\":\"home\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0}")]
        [TestCase("{\"mapId\":\"home\",\"state\":{},\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0}")]
        [TestCase("{\"version\":1,\"mapId\":\"home\",\"state\":null,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0}")]
        [TestCase("{\"version\":1,\"mapId\":\"home\",\"other\":{\"state\":{}},\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0}")]
        public void InvalidOrIncompleteJsonFails(string json)
        {
            Assert.Throws<InvalidDataException>(() => SaveService.Deserialize(json));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(5)]
        public void UnsupportedVersionsFailForSerializationAndLoading(int version)
        {
            var data = CreateSave();
            data.version = version;
            Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
            Assert.Throws<InvalidDataException>(() => SaveService.Deserialize(JsonUtility.ToJson(data)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void MissingMapIdentifierFails(string mapId)
        {
            var data = CreateSave();
            data.mapId = mapId;
            Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
            Assert.Throws<InvalidDataException>(() => SaveService.Deserialize(JsonUtility.ToJson(data)));
        }

        [Test]
        public void MissingGameStateAndNonfiniteCoordinatesFail()
        {
            Assert.Throws<InvalidDataException>(() => SaveService.Serialize(null));
            var data = CreateSave();
            data.state = null;
            Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
            foreach (float number in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                data = CreateSave();
                data.position.x = number;
                Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
                data = CreateSave();
                data.position.y = number;
                Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
                data = CreateSave();
                data.position.z = number;
                Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
                data = CreateSave();
                data.yaw = number;
                Assert.Throws<InvalidDataException>(() => SaveService.Serialize(data));
            }
        }

        [Test]
        public void LoadingNormalizesInvalidStatsAndMissingCollections()
        {
            var data = CreateSave();
            data.state.player.health = 999;
            data.state.player.sanity = -1;
            data.state.player.gold = -3;
            data.state.switches = null;
            data.state.selfSwitches = null;
            data.state.variables = null;
            data.state.inventory = null;
            var restored = SaveService.Deserialize(JsonUtility.ToJson(data));
            Assert.That(restored.state.player.health, Is.EqualTo(100));
            Assert.That(restored.state.player.sanity, Is.Zero);
            Assert.That(restored.state.player.gold, Is.Zero);
            Assert.That(restored.state.switches, Is.Not.Null);
            Assert.That(restored.state.selfSwitches, Is.Not.Null);
            Assert.That(restored.state.variables, Is.Not.Null);
            Assert.That(restored.state.inventory, Is.Not.Null);
        }

        [Test]
        public void OverwritingRetainsPreviousSaveAsReadableBackupForRollback()
        {
            string path = Path.Combine(temporaryDirectory, "slots", "slot-01.json");
            var first = CreateSave();
            SaveService.Write(path, first);
            Assert.That(SaveService.Read(path).mapId, Is.EqualTo("home"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);

            var second = CreateSave();
            second.mapId = "street";
            second.state.SetVariable("chapter", 3);
            SaveService.Write(path, second);
            Assert.That(SaveService.Read(path).mapId, Is.EqualTo("street"));
            var previous = SaveService.Read(path + ".bak");
            Assert.That(previous.mapId, Is.EqualTo("home"));
            Assert.That(previous.state.GetVariable("chapter"), Is.EqualTo(2));
            Assert.That(File.Exists(path + ".tmp"), Is.False);

            SaveService.Write(path, previous);
            Assert.That(SaveService.Read(path).mapId, Is.EqualTo("home"));
            Assert.That(SaveService.Read(path + ".bak").mapId, Is.EqualTo("street"));
        }

        [Test]
        public void InvalidWriteDoesNotReplaceCurrentSaveOrBackup()
        {
            string path = Path.Combine(temporaryDirectory, "slot-01.json");
            var data = CreateSave();
            SaveService.Write(path, data);
            string original = File.ReadAllText(path);
            data.version = -1;
            Assert.Throws<InvalidDataException>(() => SaveService.Write(path, data));
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            Assert.That(File.Exists(path + ".bak"), Is.False);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void MissingFileReadReportsMissingFile()
        {
            Assert.Throws<FileNotFoundException>(() => SaveService.Read(Path.Combine(temporaryDirectory, "missing.json")));
        }
    }
}
