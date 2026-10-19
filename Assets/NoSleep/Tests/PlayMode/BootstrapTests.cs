using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class BootstrapTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        readonly List<Camera> previousCameras = new List<Camera>();
        readonly List<bool> cameraEnabled = new List<bool>();
        readonly List<AudioListener> previousListeners = new List<AudioListener>();
        readonly List<bool> listenerEnabled = new List<bool>();
        Scene previousScene;
        Scene testScene;
        TileDefinition floor;
        MapDefinition firstMap, secondMap;
        GameObject bootstrapRoot;
        float previousTimeScale;
        bool initialized;
        Keyboard movementKeyboard;
        Keyboard previousKeyboard;
        InputSettings.BackgroundBehavior previousBackgroundBehavior;
        InputSettings.UpdateMode previousUpdateMode;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        bool movementInputConfigured;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Do not remove or replace a game someone is running when tests are invoked manually.
            if (GameSession.Instance != null) Assert.Ignore("Bootstrap tests require a scene without a running GameSession.");
            initialized = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            previousScene = SceneManager.GetActiveScene();
            testScene = SceneManager.CreateScene("No Sleep Bootstrap Test " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(testScene);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                previousCameras.Add(camera);
                cameraEnabled.Add(camera.enabled);
                camera.enabled = false;
            }
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                previousListeners.Add(listener);
                listenerEnabled.Add(listener.enabled);
                listener.enabled = false;
            }
            floor = CreateAsset<TileDefinition>();
            floor.displayName = "Test floor";
            firstMap = CreateMap("test_corridor", new Vector2Int(2, 2));
            secondMap = CreateMap("test_room", new Vector2Int(3, 3));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!initialized) yield break;
            RemoveMovementKeyboard();
            RestoreMovementInputSettings();
            if (bootstrapRoot != null) Object.Destroy(bootstrapRoot);
            yield return null;
            foreach (ScriptableObject asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
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
            previousCameras.Clear(); cameraEnabled.Clear();
            previousListeners.Clear(); listenerEnabled.Clear();
            Time.timeScale = previousTimeScale;
            initialized = false;
        }

        [UnityTest]
        public IEnumerator Bootstrap_CreatesReadySessionPlayerUIAndQuarterViewCamera()
        {
            GameSession session = StartBootstrap();
            yield return null;
            Assert.That(session, Is.SameAs(GameSession.Instance));
            Assert.That(session.IsReady, Is.True);
            Assert.That(session.Map.Map, Is.SameAs(firstMap));
            Assert.That(session.UI, Is.Not.Null);
            Assert.That(session.UI.IsModalOpen, Is.False);
            Assert.That(session.Map.WorldToCell(session.Player.transform.position), Is.EqualTo(firstMap.spawnCell));
            Assert.That(session.Map.CanOccupy(session.Player.transform.position, session.Player.Radius), Is.True);
            CharacterController controller = session.Player.GetComponent<CharacterController>();
            Assert.That(controller.enabled, Is.True);
            Assert.That(controller.height, Is.EqualTo(1.8f).Within(0.001f));
            Assert.That(controller.radius, Is.EqualTo(0.23f).Within(0.001f));
            Assert.That(Camera.main, Is.Not.Null);
            QuarterViewCamera follow = Camera.main.GetComponent<QuarterViewCamera>();
            Assert.That(follow, Is.Not.Null);
            Assert.That(follow.target, Is.SameAs(session.Player.transform));
            Assert.That(Camera.main.orthographic, Is.True);
        }

        [UnityTest]
        public IEnumerator KeyboardMovement_ApproachesBlockedTileWithoutCrossing_AndMovesThroughClearTerrain()
        {
            var wall = CreateAsset<TileDefinition>();
            wall.displayName = "Movement test wall";
            wall.walkable = false;
            wall.height = 1.65f;
            firstMap.SetTile(new Vector2Int(2, 3), wall);
            GameSession session = StartBootstrap();
            yield return null;
            QuarterViewCamera camera = Camera.main.GetComponent<QuarterViewCamera>();
            camera.yaw = 0f;
            camera.Snap();
            Assert.That(session.TransferPlayer(firstMap, new Vector2Int(2, 2)), Is.True);
            yield return null;
            Vector3 initial = session.Player.transform.position;
            ConfigureMovementInputSettings();
            previousKeyboard = Keyboard.current;
            movementKeyboard = InputSystem.AddDevice<Keyboard>("No Sleep Movement Test Keyboard");
            InputSystem.EnableDevice(movementKeyboard);
            movementKeyboard.MakeCurrent();
            try
            {
                InputSystem.QueueStateEvent(movementKeyboard, new KeyboardState(Key.W));
                yield return null;
                Assert.That(movementKeyboard.enabled, Is.True, "The fixture keyboard must remain enabled in background batch runs.");
                Assert.That(Keyboard.current, Is.SameAs(movementKeyboard), "GameInput must use the fixture keyboard.");
                Assert.That(movementKeyboard.wKey.isPressed, Is.True, "The dynamic input update must consume the queued W state.");
                Assert.That(GameInput.Move.y, Is.GreaterThan(0.5f), "Forward input must reach the actual gameplay input adapter.");
                Assert.That(session.IsInputBlocked, Is.False, "A UI or event lock must not suppress the movement test.");
                Assert.That(session.Player.isActiveAndEnabled, Is.True);
                yield return new WaitForSeconds(0.28f);
                InputSystem.QueueStateEvent(movementKeyboard, new KeyboardState());
                yield return null;
                Vector3 atWall = session.Player.transform.position;
                Assert.That(atWall.z, Is.GreaterThan(initial.z + 0.05f), "Injected forward input must cause actual movement before reaching the wall.");
                Assert.That(atWall.z, Is.LessThanOrEqualTo(2.5f - session.Player.Radius + 0.025f), "The controller's radius must remain outside the blocked tile.");
                Assert.That(atWall.x, Is.EqualTo(initial.x).Within(0.025f), "A camera yaw of zero must not introduce lateral drift.");
                Assert.That(session.Map.WorldToCell(atWall), Is.EqualTo(new Vector2Int(2, 2)));
                Assert.That(session.Map.CanOccupy(atWall, session.Player.Radius), Is.True);

                InputSystem.QueueStateEvent(movementKeyboard, new KeyboardState(Key.D));
                yield return new WaitForSeconds(0.22f);
                InputSystem.QueueStateEvent(movementKeyboard, new KeyboardState());
                yield return null;
                Vector3 afterRight = session.Player.transform.position;
                Assert.That(afterRight.x, Is.GreaterThan(atWall.x + 0.2f), "The same controller must still move along unobstructed ground.");
                Assert.That(afterRight.z, Is.EqualTo(atWall.z).Within(0.025f));
                Assert.That(session.Map.CanOccupy(afterRight, session.Player.Radius), Is.True);
            }
            finally { RemoveMovementKeyboard(); RestoreMovementInputSettings(); }
        }

        [UnityTest]
        public IEnumerator Transfer_DuringRunningEvent_ReplacesMapWithoutLeavingOldGeometryActive()
        {
            GameSession session = StartBootstrap();
            yield return null;
            GameObject oldGeometry = session.Map.transform.GetChild(0).gameObject;
            var definition = CreateAsset<EventDefinition>();
            definition.pages.Add(new EventPage
            {
                commands = new List<EventCommand>
                {
                    new EventCommand { type = EventCommandType.TransferPlayer, map = secondMap, cell = secondMap.spawnCell },
                    new EventCommand { type = EventCommandType.Wait, seconds = 0.15f }
                }
            });
            Assert.That(session.Events.TryRun(definition, "test/transfer"), Is.True);
            Assert.That(session.Events.IsRunning, Is.True);
            Assert.That(session.IsInputBlocked, Is.True);
            Assert.That(session.Map.Map, Is.SameAs(secondMap));
            Assert.That(session.Map.WorldToCell(session.Player.transform.position), Is.EqualTo(secondMap.spawnCell));
            Assert.That(oldGeometry.activeSelf, Is.False, "Outgoing geometry must stop colliding before deferred destruction.");
            yield return null;
            Assert.That(oldGeometry == null, Is.True);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (session.Events.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.Events.IsRunning, Is.False);
            Assert.That(session.IsInputBlocked, Is.False);
            Assert.That(session.Map.transform.childCount, Is.EqualTo(1));
            Assert.That(session.Map.CanOccupy(session.Player.transform.position, session.Player.Radius), Is.True);
        }

        [UnityTest]
        public IEnumerator Transfer_ToMapWhoseEventsBlockEveryCell_RestoresPreviousMapAndPlayer()
        {
            GameSession session = StartBootstrap();
            yield return null;
            Vector3 previousPosition = session.Player.transform.position;
            GameState previousState = session.State;
            var blocked = CreateMap("test_blocked", new Vector2Int(2, 2));
            var barrier = CreateAsset<EventDefinition>();
            barrier.pages.Add(new EventPage { visible = false, blocksMovement = true });
            for (int y = 0; y < blocked.height; y++)
                for (int x = 0; x < blocked.width; x++)
                    blocked.events.Add(new MapEventPlacement { id = "barrier_" + x + "_" + y, cell = new Vector2Int(x, y), definition = barrier });
            Assert.That(blocked.IsWalkable(blocked.spawnCell), Is.True, "The rejection must come from live event collision, rather than missing terrain.");
            Assert.That(session.TransferPlayer(blocked, blocked.spawnCell), Is.False);
            Assert.That(session.Map.Map, Is.SameAs(firstMap));
            Assert.That(session.State, Is.SameAs(previousState));
            Assert.That(session.Player.transform.position, Is.EqualTo(previousPosition));
            Assert.That(session.Player.GetComponent<CharacterController>().enabled, Is.True);
            yield return null;
            Assert.That(session.Map.transform.childCount, Is.EqualTo(1));
            Assert.That(session.Map.CanOccupy(session.Player.transform.position, session.Player.Radius), Is.True);
        }

        [UnityTest]
        public IEnumerator LiveSessionSnapshot_RoundTripsMapPositionAndStateWithoutWritingUserSaves()
        {
            GameSession session = StartBootstrap();
            yield return null;
            session.State.SetSwitch("test_switch", true);
            session.State.SetVariable("test_variable", 17);
            session.State.AddItem("test_key", 2);
            session.State.AddGold(23);
            session.State.player.health = 41;
            var snapshot = new SaveData
            {
                mapId = session.Map.Map.mapId,
                position = session.Player.transform.position,
                yaw = session.Player.transform.eulerAngles.y,
                state = session.State
            };
            SaveData restored = SaveService.Deserialize(SaveService.Serialize(snapshot));
            session.State.SetSwitch("test_switch", false);
            session.State.AddItem("test_key", -2);
            Assert.That(restored.mapId, Is.EqualTo(firstMap.mapId));
            Assert.That(restored.position, Is.EqualTo(snapshot.position));
            Assert.That(restored.state, Is.Not.SameAs(session.State));
            Assert.That(restored.state.GetSwitch("test_switch"), Is.True);
            Assert.That(restored.state.GetVariable("test_variable"), Is.EqualTo(17));
            Assert.That(restored.state.GetItemCount("test_key"), Is.EqualTo(2));
            Assert.That(restored.state.player.gold, Is.EqualTo(23));
            Assert.That(restored.state.player.health, Is.EqualTo(41));
        }

        private GameSession StartBootstrap()
        {
            bootstrapRoot = new GameObject("No Sleep Bootstrap Test");
            bootstrapRoot.SetActive(false);
            NoSleepBootstrap bootstrap = bootstrapRoot.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = firstMap;
            bootstrap.maps = new List<MapDefinition> { firstMap, secondMap };
            bootstrap.playerName = "Test player";
            bootstrapRoot.SetActive(true);
            return bootstrapRoot.GetComponent<GameSession>();
        }

        private MapDefinition CreateMap(string id, Vector2Int spawn)
        {
            MapDefinition map = CreateAsset<MapDefinition>();
            map.mapId = id;
            map.displayName = id;
            map.Resize(6, 6);
            map.Fill(floor);
            map.spawnCell = spawn;
            return map;
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private void RemoveMovementKeyboard()
        {
            // Only remove the device created by this fixture; preserve all user input devices.
            if (movementKeyboard != null && movementKeyboard.added) InputSystem.RemoveDevice(movementKeyboard);
            movementKeyboard = null;
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            previousKeyboard = null;
        }

        private void ConfigureMovementInputSettings()
        {
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousUpdateMode = InputSystem.settings.updateMode;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            movementInputConfigured = true;
            // A graphics batch editor has no focused Game view. Route synthetic input through
            // the same dynamic update as PlayerController without depending on window focus.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        }

        private void RestoreMovementInputSettings()
        {
            if (!movementInputConfigured) return;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            InputSystem.settings.updateMode = previousUpdateMode;
            movementInputConfigured = false;
        }
    }
}
