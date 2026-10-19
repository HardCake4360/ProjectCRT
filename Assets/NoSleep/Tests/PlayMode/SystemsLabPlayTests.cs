#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class SystemsLabPlayTests
    {
        const string DataRoot = "Assets/NoSleep/Data/SystemsLab/";
        readonly List<Camera> previousCameras = new List<Camera>();
        readonly List<bool> cameraEnabled = new List<bool>();
        readonly List<AudioListener> previousListeners = new List<AudioListener>();
        readonly List<bool> listenerEnabled = new List<bool>();
        Scene previousScene, testScene;
        GameObject host;
        GameSession session;
        MapDefinition main, annex;
        ImmediatePresentation presentation;
        string temporaryDirectory;
        float previousTimeScale;
        bool initialized;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(GameSession.Instance, Is.Null, "Systems lab tests require an isolated scene without a running session.");
            initialized = true;
            previousTimeScale = Time.timeScale; Time.timeScale = 1;
            previousScene = SceneManager.GetActiveScene();
            testScene = SceneManager.CreateScene("No Sleep Systems Lab Test " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(testScene);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                previousCameras.Add(camera); cameraEnabled.Add(camera.enabled); camera.enabled = false;
            }
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                previousListeners.Add(listener); listenerEnabled.Add(listener.enabled); listener.enabled = false;
            }
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NoSleepSystemsLabTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            main = RequiredAsset<MapDefinition>("Maps/Main"); annex = RequiredAsset<MapDefinition>("Maps/Annex");
            SurvivalSettings settings = RequiredAsset<SurvivalSettings>("Rules/Settings");
            Assert.That(settings.timeMode, Is.EqualTo(TimeMode.AuthoredCosts));
            host = new GameObject("No Sleep Systems Lab Test"); host.SetActive(false);
            NoSleepBootstrap bootstrap = host.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = main; bootstrap.maps = new List<MapDefinition> { main, annex };
            bootstrap.survivalSettings = settings;
            bootstrap.stimulant = RequiredAsset<ItemEffectDefinition>("Rules/Stimulant");
            bootstrap.showTitleScreen = true;
            host.SetActive(true); session = host.GetComponent<GameSession>();
            session.ConfigureSaveDirectory(temporaryDirectory);
            presentation = new ImmediatePresentation();
            session.Events.Configure(session, presentation); session.Combat.Configure(session, presentation);
            Assert.That(session.IsReady, Is.True);
            yield return null; yield return null;
            Assert.That(presentation.Texts, Is.Empty, "The generated introduction must remain behind the title.");
            Assert.That(session.NewGame(), Is.True);
            yield return WaitUntil(() => !session.Events.IsRunning &&
                Placement(main, "intro").definition.IsPageCompleted(session.State, Key(main, "intro"), 0), "The generated introduction did not finish.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!initialized) yield break;
            if (session != null && session.Scheduler != null) session.Scheduler.CancelAll();
            if (host != null) Object.Destroy(host);
            yield return null;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (testScene.IsValid() && testScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(testScene);
                if (unload != null) yield return unload;
            }
            for (int i = 0; i < previousCameras.Count; i++)
                if (previousCameras[i] != null) previousCameras[i].enabled = cameraEnabled[i];
            for (int i = 0; i < previousListeners.Count; i++)
                if (previousListeners[i] != null) previousListeners[i].enabled = listenerEnabled[i];
            previousCameras.Clear(); cameraEnabled.Clear(); previousListeners.Clear(); listenerEnabled.Clear();
            Time.timeScale = previousTimeScale;
            if (!string.IsNullOrEmpty(temporaryDirectory))
            {
                string path = Path.GetFullPath(temporaryDirectory);
                string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.That(path.StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(path).StartsWith("NoSleepSystemsLabTests-", StringComparison.Ordinal), Is.True);
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            initialized = false;
        }

        [UnityTest]
        public IEnumerator CommonKeepersStayIndependentAndWaitingObserversResumeWithoutDuplicateRewardsOrNotices()
        {
            yield return RunPlacement(main, "keeper_a");
            Assert.That(session.State.GetSelfSwitch(Key(main, "keeper_a"), "A"), Is.True);
            Assert.That(session.State.GetSelfSwitch(Key(main, "keeper_b"), "A"), Is.False);
            Assert.That(session.State.GetItemCount("lab_token"), Is.EqualTo(1));
            Assert.That(presentation.Speakers, Does.Contain("보관인 A"));
            Assert.That(presentation.Texts.Exists(text => text.Contains("나는 1번 보관인")), Is.True);
            yield return RunPlacement(main, "keeper_b");
            Assert.That(session.State.GetSelfSwitch(Key(main, "keeper_b"), "A"), Is.True);
            Assert.That(presentation.Speakers, Does.Contain("보관인 B"));
            Assert.That(presentation.Texts.Exists(text => text.Contains("나는 2번 보관인")), Is.True);
            yield return RunPlacement(main, "keeper_a");
            yield return RunPlacement(main, "keeper_b");
            Assert.That(session.State.GetItemCount("lab_token"), Is.EqualTo(2));

            yield return Panel(0);
            yield return WaitingObservers();
            AssertRewards();
            Assert.That(session.State.survival.elapsedMinutes, Is.Zero);
            AssertDeadlines(10);
            Assert.That(session.Save(0), Is.True);
            string originalSave = File.ReadAllText(session.SavePath(0));
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(session.Load(0), Is.True);
                yield return WaitingObservers();
                AssertDeadlines(10); AssertRewards();
                Assert.That(session.State.GetItemCount("lab_token"), Is.EqualTo(2));
                Assert.That(File.ReadAllText(session.SavePath(0)), Is.EqualTo(originalSave));
            }
            yield return Panel(1);
            yield return NoticesFinished();
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(10));
            AssertRewards(); AssertNoticesOnceInOrder();
            Assert.That(session.State.eventCheckpoints, Is.Empty);
            Assert.That(session.Save(0), Is.True);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(session.Load(0), Is.True);
                yield return WaitForIdle();
                for (int frame = 0; frame < 5; frame++) yield return null;
                AssertRewards(); AssertNoticesOnceInOrder();
                Assert.That(session.Scheduler.QueuedCount, Is.Zero);
                Assert.That(session.Scheduler.BackgroundContexts.Count, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator LeavingTheLabCancelsRuntimeObserversAndReturningResumesSavedDeadlines()
        {
            yield return Panel(0);
            yield return WaitingObservers();
            var firstContext = session.Scheduler.BackgroundContexts[0];
            var secondContext = session.Scheduler.BackgroundContexts[1];
            AssertDeadlines(10); AssertRewards();
            yield return Panel(2);
            Assert.That(session.Map.Map, Is.SameAs(annex));
            Assert.That(firstContext.IsCancelled, Is.True);
            Assert.That(secondContext.IsCancelled, Is.True);
            Assert.That(session.Scheduler.BackgroundContexts.Count, Is.Zero);
            Assert.That(session.Scheduler.QueuedCount, Is.Zero);
            AssertDeadlines(10); AssertRewards();
            Assert.That(session.Save(1), Is.True, "A checkpoint owned by another catalog map must remain savable in the annex.");
            Assert.That(session.Load(1), Is.True);
            yield return WaitForIdle();
            Assert.That(session.Map.Map, Is.SameAs(annex));
            Assert.That(session.Scheduler.BackgroundContexts.Count, Is.Zero);
            yield return RunPlacement(annex, "return");
            yield return WaitingObservers();
            Assert.That(session.Map.Map, Is.SameAs(main));
            Assert.That(session.Scheduler.BackgroundContexts[0], Is.Not.SameAs(firstContext));
            Assert.That(session.Scheduler.BackgroundContexts[1], Is.Not.SameAs(secondContext));
            AssertDeadlines(10); AssertRewards();
            Assert.That(session.State.survival.elapsedMinutes, Is.Zero);
            yield return Panel(1);
            yield return NoticesFinished();
            AssertRewards(); AssertNoticesOnceInOrder();
            Assert.That(session.State.eventCheckpoints, Is.Empty);
        }

        IEnumerator Panel(int choice)
        {
            presentation.Choice = choice;
            yield return RunPlacement(main, "panel");
        }
        IEnumerator RunPlacement(MapDefinition map, string id)
        {
            yield return WaitForIdle();
            Assert.That(session.Map.Map, Is.SameAs(map));
            Assert.That(session.Events.TryRun(Placement(map, id).definition, Key(map, id)), Is.True, "Failed authored event: " + Key(map, id));
            yield return WaitForIdle();
        }
        IEnumerator WaitForIdle() => WaitUntil(() => !session.Events.IsRunning, "A foreground lab event did not finish.");
        IEnumerator WaitingObservers() => WaitUntil(() => session.Scheduler.BackgroundContexts.Count == 2 &&
            session.State.eventCheckpoints.Count == 2 && session.State.GetVariable("lab_reward_count") == 2,
            "Both observers must reach their durable game-time wait.");
        IEnumerator NoticesFinished() => WaitUntil(() => session.State.GetSwitch("lab_notice_a") && session.State.GetSwitch("lab_notice_b") &&
            !session.Events.IsRunning && session.Scheduler.QueuedCount == 0 && session.Scheduler.BackgroundContexts.Count == 0,
            "The two queued notices did not finish.");
        IEnumerator WaitUntil(Func<bool> condition, string message)
        {
            yield return null;
            int frames = 0;
            while (!condition() && frames++ < 150) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        void AssertRewards()
        {
            Assert.That(session.State.GetItemCount("lab_marker_a"), Is.EqualTo(1));
            Assert.That(session.State.GetItemCount("lab_marker_b"), Is.EqualTo(1));
            Assert.That(session.State.GetVariable("lab_reward_count"), Is.EqualTo(2));
        }
        void AssertDeadlines(int deadline)
        {
            foreach (string id in new[] { "watch_a", "watch_b" })
            {
                EventDefinition definition = Placement(main, id).definition;
                EventPage page = definition.pages[0];
                string scope = Key(main, id) + "/" + definition.definitionId + "/" + page.pageId;
                EventCheckpointState checkpoint = session.State.GetEventCheckpoint(scope);
                Assert.That(checkpoint, Is.Not.Null, "Missing checkpoint: " + id);
                Assert.That(checkpoint.checkpointId, Is.EqualTo(page.commands.Find(command => command.type == EventCommandType.WaitGameMinutes).commandId));
                Assert.That(checkpoint.dueElapsedMinutes, Is.EqualTo(deadline));
            }
        }
        void AssertNoticesOnceInOrder()
        {
            const string first = "첫 번째 장치의 표시등이 켜졌다";
            const string second = "두 번째 표시등도 켜졌다";
            Assert.That(presentation.Texts.FindAll(text => text.Contains(first)).Count, Is.EqualTo(1));
            Assert.That(presentation.Texts.FindAll(text => text.Contains(second)).Count, Is.EqualTo(1));
            Assert.That(presentation.Texts.FindIndex(text => text.Contains(first)), Is.LessThan(presentation.Texts.FindIndex(text => text.Contains(second))));
        }
        static string Key(MapDefinition map, string id) => map.mapId + "/" + id;
        static MapEventPlacement Placement(MapDefinition map, string id)
        {
            MapEventPlacement placement = map.events.Find(value => value != null && value.id == id);
            Assert.That(placement, Is.Not.Null, "Generated lab placement is required: " + Key(map, id));
            Assert.That(placement.definition, Is.Not.Null);
            return placement;
        }
        static T RequiredAsset<T>(string relative) where T : Object
        {
            string path = DataRoot + relative + ".asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Build and validate the systems lab before running its authored-content tests: " + path);
            return asset;
        }
        sealed class ImmediatePresentation : IEventPresentation
        {
            public int Choice;
            public readonly List<string> Speakers = new List<string>();
            public readonly List<string> Texts = new List<string>();
            public IEnumerator ShowDialogue(string speaker, string text)
            {
                Speakers.Add(speaker); Texts.Add(text); yield return null;
            }
            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> selected)
            {
                Assert.That(Choice, Is.InRange(0, choices.Count - 1));
                yield return null; selected(Choice);
            }
        }
    }
}
#endif
