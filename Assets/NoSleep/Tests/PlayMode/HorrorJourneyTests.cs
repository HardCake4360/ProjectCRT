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
    /// <summary>Runs generated authored content through the runtime interpreter and real save slots.</summary>
    public sealed class HorrorJourneyTests
    {
        const string DataRoot = "Assets/NoSleep/Data/Horror/";
        readonly List<Camera> previousCameras = new List<Camera>();
        readonly List<bool> cameraEnabled = new List<bool>();
        readonly List<AudioListener> previousListeners = new List<AudioListener>();
        readonly List<bool> listenerEnabled = new List<bool>();
        Scene previousScene, testScene;
        GameObject host;
        GameSession session;
        MapDefinition apartment, hospital, transit, approach;
        SurvivalSettings settings;
        ItemEffectDefinition stimulant;
        EnemyDefinition lateEnemy;
        ImmediatePresentation presentation;
        string temporaryDirectory;
        float previousTimeScale;
        bool initialized;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (GameSession.Instance != null) Assert.Ignore("Horror journey tests require a scene without a running GameSession.");
            initialized = true;
            previousTimeScale = Time.timeScale; Time.timeScale = 1;
            previousScene = SceneManager.GetActiveScene();
            testScene = SceneManager.CreateScene("No Sleep Authored Horror Journey Test " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(testScene);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                previousCameras.Add(camera); cameraEnabled.Add(camera.enabled); camera.enabled = false;
            }
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                previousListeners.Add(listener); listenerEnabled.Add(listener.enabled); listener.enabled = false;
            }
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "NoSleepHorrorJourneyTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            apartment = RequiredAsset<MapDefinition>("Maps/Apartment");
            hospital = RequiredAsset<MapDefinition>("Maps/Hospital");
            transit = RequiredAsset<MapDefinition>("Maps/Transit");
            approach = RequiredAsset<MapDefinition>("Maps/Approach");
            settings = RequiredAsset<SurvivalSettings>("Rules/SurvivalSettings");
            stimulant = RequiredAsset<ItemEffectDefinition>("Rules/Stimulant");
            lateEnemy = RequiredAsset<EnemyDefinition>("Enemies/FadingPresence");
            Assert.That(settings.timeMode, Is.EqualTo(TimeMode.AuthoredCosts), "The authored journey requires the generated authored-time profile.");
            host = new GameObject("No Sleep Authored Horror Journey Test"); host.SetActive(false);
            NoSleepBootstrap bootstrap = host.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = apartment;
            bootstrap.maps = new List<MapDefinition> { apartment, hospital, transit, approach };
            bootstrap.survivalSettings = settings; bootstrap.stimulant = stimulant;
            bootstrap.companions = new List<CompanionDefinition> { RequiredAsset<CompanionDefinition>("Companions/Min"), RequiredAsset<CompanionDefinition>("Companions/Jun") };
            bootstrap.showTitleScreen = true;
            host.SetActive(true);
            session = host.GetComponent<GameSession>();
            session.ConfigureSaveDirectory(temporaryDirectory);
            presentation = new ImmediatePresentation();
            session.Events.Configure(session, presentation);
            session.Combat.Configure(session, presentation);
            Assert.That(session.IsReady, Is.True);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!initialized) yield break;
            if (session != null && session.Events != null) session.Events.Cancel();
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
                Assert.That(Path.GetFileName(path).StartsWith("NoSleepHorrorJourneyTests-", StringComparison.Ordinal), Is.True);
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            initialized = false;
        }

        [UnityTest]
        public IEnumerator AuthoredJourneyResumesMidwayAndReachesDayFourteenAloneWithMemories()
        {
            Assert.That(session.UI.IsTitleOpen, Is.True);
            Assert.That(session.IsInputBlocked, Is.True);
            Assert.That(presentation.DialogueTexts, Is.Empty, "The news must wait behind the title.");
            Assert.That(Placement(apartment, "news").definition.IsPageCompleted(session.State, Key(apartment, "news"), 0), Is.False);
            yield return BeginJourney();
            int introLines = presentation.DialogueTexts.Count;
            Assert.That(introLines, Is.EqualTo(3));
            Assert.That(session.TransferPlayer(apartment, apartment.spawnCell), Is.True);
            yield return WaitForIdle();
            Assert.That(presentation.DialogueTexts.Count, Is.EqualTo(introLines), "Rebuilding the apartment must not replay its completed news.");
            int initialMaxHealth = session.State.player.maxHealth, initialStrength = session.State.player.strength;
            float initialEnemyScale = SurvivalRules.EnemyScale(session.State, settings);

            yield return RunPlacement(apartment, "hospital_door");
            yield return RunPlacement(hospital, "medicine");
            yield return RunPlacement(hospital, "doctor");
            yield return RunPlacement(hospital, "min");
            yield return RunPlacement(hospital, "transit_door");
            Assert.That(session.Map.Map, Is.SameAs(transit));
            Assert.That(session.State.GetItemCount("stimulant"), Is.EqualTo(3));
            Assert.That(session.State.journey.clueIds, Is.EquivalentTo(new[] { "hospital_account" }));
            Assert.That(session.State.party.activeCompanions, Is.EquivalentTo(new[] { "min" }));
            int savedMinutes = session.State.survival.elapsedMinutes;
            float savedFatigue = session.State.survival.fatigue;
            Vector3 savedPosition = session.Player.transform.position;
            Assert.That(session.Save(3), Is.True);
            string savedFile = File.ReadAllText(session.SavePath(3));

            // Make real progress and spend an item, then resume the earlier authored snapshot.
            Assert.That(session.UseStimulant(), Is.True);
            yield return RunPlacement(transit, "jun");
            presentation.NightChoice = 0;
            yield return RunPlacement(transit, "night");
            Assert.That(session.State.journey.memories.Count, Is.EqualTo(1));
            Assert.That(session.Load(3), Is.True);
            yield return WaitForIdle();
            Assert.That(session.Map.Map, Is.SameAs(transit));
            Assert.That(session.Player.transform.position.x, Is.EqualTo(savedPosition.x).Within(0.001f));
            Assert.That(session.Player.transform.position.z, Is.EqualTo(savedPosition.z).Within(0.001f));
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(savedMinutes));
            Assert.That(session.State.survival.fatigue, Is.EqualTo(savedFatigue));
            Assert.That(session.State.GetItemCount("stimulant"), Is.EqualTo(3));
            Assert.That(session.State.journey.memories, Is.Empty);
            Assert.That(session.State.party.activeCompanions, Is.EquivalentTo(new[] { "min" }));
            Assert.That(session.State.unlockedActions, Does.Not.Contain("observe"));
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("follow_accounts"));
            Assert.That(File.ReadAllText(session.SavePath(3)), Is.EqualTo(savedFile));

            yield return RunPlacement(transit, "return_door");
            yield return RunPlacement(hospital, "medicine");
            yield return RunPlacement(hospital, "doctor");
            yield return RunPlacement(hospital, "min");
            yield return RunPlacement(hospital, "transit_door");
            Assert.That(session.State.GetItemCount("stimulant"), Is.EqualTo(3), "Reloading and revisiting the cabinet cannot duplicate medicine.");
            Assert.That(session.State.journey.clueIds, Is.EquivalentTo(new[] { "hospital_account" }));
            Assert.That(session.State.journey.journal.Count, Is.EqualTo(1));
            Assert.That(session.State.party.activeCompanions, Is.EquivalentTo(new[] { "min" }));
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(savedMinutes), "Completed travel pages cannot charge their original cost twice.");
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("follow_accounts"));

            yield return RunPlacement(transit, "jun");
            yield return RunPlacement(transit, "night");
            int afterNight = session.State.survival.elapsedMinutes;
            Assert.That(session.State.journey.memories[0].id, Is.EqualTo("band_night"));
            Assert.That(session.State.journey.memories[0].text, Does.Contain("기억"));
            yield return RunPlacement(transit, "night");
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(afterNight));
            Assert.That(session.State.journey.memories.Count, Is.EqualTo(1));
            yield return RunPlacement(transit, "onward");
            Assert.That(session.Map.Map, Is.SameAs(approach));
            Assert.That(session.State.party.activeCompanions, Is.Empty);
            Assert.That(session.State.party.knownCompanions, Is.EquivalentTo(new[] { "min", "jun" }));
            Assert.That(session.State.journey.memories.Count, Is.EqualTo(1));
            Assert.That(session.State.unlockedActions, Does.Contain("aggressive"));
            Assert.That(session.State.unlockedActions, Does.Contain("observe"));
            yield return RunPlacement(approach, "destination");
            Assert.That(SurvivalRules.Day(session.State, settings), Is.EqualTo(14));
            Assert.That(session.State.journey.reachedDestination, Is.True);
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("destination_reached"));
            Assert.That(session.State.journey.stage, Is.EqualTo(4));
            Assert.That(session.State.player.maxHealth, Is.LessThan(initialMaxHealth));
            Assert.That(session.State.player.strength, Is.LessThan(initialStrength));
            Assert.That(SurvivalRules.EnemyScale(session.State, settings), Is.LessThan(initialEnemyScale));
            var lateBattle = new CombatRules(session.State, lateEnemy, settings, stimulant);
            Assert.That(lateBattle.EnemyMaxHealth, Is.LessThan(lateEnemy.baseHealth));
            Assert.That(lateBattle.EnemyAttack, Is.LessThan(lateEnemy.baseAttack));
            Assert.That(lateBattle.GetAvailableActions(), Does.Contain(CombatAction.Aggressive));
            Assert.That(lateBattle.GetAvailableActions(), Does.Contain(CombatAction.Observe));
            Assert.That(session.Save(4), Is.True);
            Assert.That(session.Load(4), Is.True);
            yield return WaitForIdle();
            int arrivedMinutes = session.State.survival.elapsedMinutes;
            yield return RunPlacement(approach, "destination");
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(arrivedMinutes), "Arrival cannot add another day after save/load.");
            Assert.That(session.State.party.activeCompanions, Is.Empty);
            Assert.That(session.State.journey.memories.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AuthoredGatesRequireMedicineTwoAccountsAndACompanionNight()
        {
            yield return BeginJourney();
            yield return RunPlacement(apartment, "hospital_door");
            int before = session.State.survival.elapsedMinutes;
            yield return RunPlacement(hospital, "transit_door");
            Assert.That(session.Map.Map, Is.SameAs(hospital));
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(before));
            Assert.That(session.State.GetSelfSwitch(Key(hospital, "transit_door"), "A"), Is.False);

            yield return RunPlacement(hospital, "medicine");
            yield return RunPlacement(hospital, "transit_door");
            before = session.State.survival.elapsedMinutes;
            yield return RunPlacement(transit, "night");
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(before));
            Assert.That(session.State.GetSwitch("night_completed"), Is.False);
            Assert.That(session.State.journey.memories, Is.Empty);
            yield return RunPlacement(transit, "onward");
            Assert.That(session.Map.Map, Is.SameAs(transit));
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(before));

            yield return RunPlacement(transit, "return_door");
            yield return RunPlacement(hospital, "doctor");
            yield return RunPlacement(hospital, "transit_door");
            yield return RunPlacement(transit, "jun");
            before = session.State.survival.elapsedMinutes;
            yield return RunPlacement(transit, "onward");
            Assert.That(session.Map.Map, Is.SameAs(transit), "Both accounts alone cannot bypass the night activity.");
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(before));
            Assert.That(session.State.GetSelfSwitch(Key(transit, "onward"), "A"), Is.False);
            presentation.NightChoice = 0;
            yield return RunPlacement(transit, "night");
            Assert.That(session.State.GetSwitch("night_completed"), Is.True);
            yield return RunPlacement(transit, "onward");
            Assert.That(session.Map.Map, Is.SameAs(approach));
        }

        [UnityTest]
        public IEnumerator RollerCoasterChoiceCreatesItsOwnMemoryAndSurvivesCompanionSeparation()
        {
            yield return BeginJourney();
            yield return RunPlacement(apartment, "hospital_door");
            yield return RunPlacement(hospital, "medicine");
            yield return RunPlacement(hospital, "doctor");
            yield return RunPlacement(hospital, "min");
            yield return RunPlacement(hospital, "transit_door");
            yield return RunPlacement(transit, "jun");
            presentation.NightChoice = 1;
            yield return RunPlacement(transit, "night");
            Assert.That(session.State.journey.memories.Count, Is.EqualTo(1));
            MemoryRecord memory = session.State.journey.memories[0];
            Assert.That(memory.id, Is.EqualTo("roller_coaster_night"));
            Assert.That(memory.title, Is.EqualTo("끝없이 돌던 밤"));
            Assert.That(memory.text, Does.Contain("함께"));
            Assert.That(session.State.GetSwitch("night_completed"), Is.True);
            Assert.That(SurvivalRules.Hour(session.State, settings), Is.EqualTo(6));
            yield return RunPlacement(transit, "onward");
            Assert.That(session.State.party.activeCompanions, Is.Empty);
            Assert.That(session.State.journey.memories.Count, Is.EqualTo(1));
            Assert.That(session.State.journey.memories[0].id, Is.EqualTo("roller_coaster_night"));
            Assert.That(session.Save(1), Is.True);
            Assert.That(session.Load(1), Is.True);
            yield return WaitForIdle();
            Assert.That(session.State.party.activeCompanions, Is.Empty);
            Assert.That(session.State.journey.memories[0].text, Is.EqualTo(memory.text));
        }

        IEnumerator BeginJourney()
        {
            Assert.That(session.NewGame(), Is.True);
            yield return WaitForIdle();
            Assert.That(session.UI.IsTitleOpen, Is.False);
            Assert.That(Placement(apartment, "news").definition.IsPageCompleted(session.State, Key(apartment, "news"), 0), Is.True);
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("find_stimulants"));
        }

        IEnumerator RunPlacement(MapDefinition map, string id)
        {
            yield return WaitForIdle();
            Assert.That(session.Map.Map, Is.SameAs(map), "Run the event from its authored map: " + id);
            MapEventPlacement placement = Placement(map, id);
            Assert.That(session.Events.TryRun(placement.definition, Key(map, id)), Is.True, "Could not run authored placement: " + id);
            yield return WaitForIdle();
        }

        IEnumerator WaitForIdle()
        {
            // Allow newly loaded autorun placements their first Update before checking idle.
            yield return null;
            int frames = 0;
            while (session.Events.IsRunning && frames++ < 200) yield return null;
            Assert.That(session.Events.IsRunning, Is.False, "Authored event failed to finish within its frame budget.");
        }

        static string Key(MapDefinition map, string id) => map.mapId + "/" + id;
        static MapEventPlacement Placement(MapDefinition map, string id)
        {
            MapEventPlacement placement = map.events.Find(value => value != null && value.id == id);
            Assert.That(placement, Is.Not.Null, "Generated placement is required: " + map.mapId + "/" + id);
            Assert.That(placement.definition, Is.Not.Null);
            return placement;
        }
        static T RequiredAsset<T>(string relative) where T : Object
        {
            string path = DataRoot + relative + ".asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Generate and validate the horror prototype before running authored-content tests: " + path);
            return asset;
        }

        sealed class ImmediatePresentation : IEventPresentation
        {
            public int NightChoice;
            public readonly List<string> DialogueTexts = new List<string>();
            public IEnumerator ShowDialogue(string speaker, string text)
            {
                DialogueTexts.Add(text); yield return null;
            }
            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> selected)
            {
                Assert.That(NightChoice, Is.InRange(0, choices.Count - 1));
                yield return null; selected(NightChoice);
            }
        }
    }
}
#endif
