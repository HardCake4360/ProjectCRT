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
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class ProjectCrtRpgTests
    {
        const string Data = "Assets/NoSleep/Data/ProjectCRT/";
        GameObject host;
        GameSession session;
        MapDefinition office, street;
        Scene previous, scene;
        float previousTimeScale;
        string directory;
        ImmediatePresentation presentation;
        readonly Dictionary<Camera, bool> cameras = new Dictionary<Camera, bool>();
        readonly Dictionary<AudioListener, bool> listeners = new Dictionary<AudioListener, bool>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(GameSession.Instance, Is.Null, "Use an empty test scene.");
            previous = SceneManager.GetActiveScene(); previousTimeScale = Time.timeScale; Time.timeScale = 0;
            scene = SceneManager.CreateScene("CRT RPG Test " + Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(scene);
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) { cameras.Add(camera, camera.enabled); camera.enabled = false; }
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) { listeners.Add(listener, listener.enabled); listener.enabled = false; }
            directory = Path.Combine(Path.GetTempPath(), "CrtRpgTests-" + Guid.NewGuid().ToString("N"));
            office = AssetDatabase.LoadAssetAtPath<MapDefinition>(Data + "Maps/Office.asset");
            street = AssetDatabase.LoadAssetAtPath<MapDefinition>(Data + "Maps/Street.asset");
            Assert.That(office, Is.Not.Null); Assert.That(street, Is.Not.Null);
            host = new GameObject("CRT RPG Test"); host.SetActive(false);
            var bootstrap = host.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = office; bootstrap.maps = new List<MapDefinition> { office, street };
            bootstrap.gameTitle = "PROJECT CRT"; bootstrap.gameTagline = "단서를 찾고, 이야기를 연결한다.";
            bootstrap.saveFolderName = "ProjectCRT_RPG"; bootstrap.showTitleScreen = true;
            host.SetActive(true); session = host.GetComponent<GameSession>();
            Assert.That(session.SavePath(), Does.Contain("ProjectCRT_RPG"));
            session.ConfigureSaveDirectory(directory); session.UI.ShowTitle();
            presentation = new ImmediatePresentation(); session.Events.Configure(session, presentation);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (session != null) session.Events.Cancel();
            if (host != null) Object.Destroy(host);
            yield return null;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var pair in cameras) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (var pair in listeners) if (pair.Key != null) pair.Key.enabled = pair.Value;
            cameras.Clear(); listeners.Clear(); Time.timeScale = previousTimeScale;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                string path = Path.GetFullPath(directory), root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("CrtRpgTests-", StringComparison.Ordinal)) Directory.Delete(path, true);
            }
        }

        [UnityTest]
        public IEnumerator TitleAndNewGameUseCrtPresentationAndKeepSurvivalDisabled()
        {
            Assert.That(session.IsReady, Is.True); Assert.That(session.HorrorEnabled, Is.False);
            Assert.That(session.UI.IsTitleOpen, Is.True); Assert.That(session.IsInputBlocked, Is.True);
            Assert.That(host.transform.Find("NoSleep Canvas/Title Screen/Title").GetComponent<Text>().text, Is.EqualTo("PROJECT CRT"));
            yield return Begin();
            Assert.That(session.UI.IsTitleOpen, Is.False);
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("crt_rpg_investigate"));
            Assert.That(session.State.survival.elapsedMinutes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ClueAndNpcStateSurviveSaveMapRoundTripAndNewGameReset()
        {
            yield return Begin(); yield return Run("crt_exit");
            Assert.That(session.Map.Map, Is.SameAs(street));
            yield return Run("crt_receipt"); yield return Run("crt_receipt");
            Assert.That(session.State.GetItemCount("crt_rpg_receipt"), Is.EqualTo(1));
            Assert.That(session.State.journey.clueIds, Does.Contain("crt_rpg_receipt"));
            yield return Run("crt_witness");
            Assert.That(session.State.GetSwitch("crt_rpg_witness_spoken"), Is.True);
            Assert.That(session.Save(0), Is.True); Vector3 savedPosition = session.Player.transform.position;
            yield return Run("crt_return"); yield return Run("crt_terminal");
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("crt_rpg_review"));
            Assert.That(session.Load(0), Is.True); yield return Idle();
            Assert.That(session.Map.Map, Is.SameAs(street)); Assert.That(session.Player.transform.position, Is.EqualTo(savedPosition));
            Assert.That(session.State.GetSwitch("crt_rpg_witness_spoken"), Is.True);
            Assert.That(session.State.npcStates.Count, Is.GreaterThan(0));
            yield return Run("crt_receipt");
            Assert.That(session.State.GetItemCount("crt_rpg_receipt"), Is.EqualTo(1));
            Assert.That(session.NewGame(), Is.True); yield return Idle();
            Assert.That(session.Map.Map, Is.SameAs(office));
            Assert.That(session.State.GetItemCount("crt_rpg_receipt"), Is.Zero);
            Assert.That(session.State.GetSwitch("crt_rpg_witness_spoken"), Is.False);
        }

        [UnityTest]
        public IEnumerator DeclinedWitnessChoiceAndMissingTestimonyDoNotCompleteInvestigation()
        {
            yield return Begin(); yield return Run("crt_exit");
            presentation.Choice = 1; yield return Run("crt_witness");
            Assert.That(session.State.GetSwitch("crt_rpg_witness_spoken"), Is.False);
            Assert.That(session.State.journey.clueIds, Does.Not.Contain("crt_rpg_testimony"));
            yield return Run("crt_receipt"); yield return Run("crt_return"); yield return Run("crt_terminal");
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("crt_rpg_investigate"));
            yield return Run("crt_exit"); presentation.Choice = 0; yield return Run("crt_witness");
            yield return Run("crt_return"); yield return Run("crt_terminal");
            Assert.That(session.State.journey.objectiveId, Is.EqualTo("crt_rpg_review"));
        }

        IEnumerator Begin() { Assert.That(session.NewGame(), Is.True); yield return Idle(); }
        IEnumerator Run(string id)
        {
            yield return Idle(); var map = session.Map.Map; var placement = map.events.Find(value => value.id == id);
            Assert.That(placement, Is.Not.Null, id);
            Assert.That(session.Events.TryRun(placement.definition, map.mapId + "/" + id), Is.True, id); yield return Idle();
        }
        IEnumerator Idle()
        {
            yield return null; int frames = 0;
            while (session.Events.IsRunning && frames++ < 200) yield return null;
            Assert.That(session.Events.IsRunning, Is.False);
        }
        sealed class ImmediatePresentation : IEventPresentation
        {
            public int Choice;
            public IEnumerator ShowDialogue(string speaker, string text) { yield return null; }
            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> selected) { yield return null; selected(Choice); }
            public void Notice(string text) { }
        }
    }
}
#endif
