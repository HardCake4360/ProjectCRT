using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoSleep
{
    public sealed class NoSleepBootstrap : MonoBehaviour
    {
        public MapDefinition startingMap;
        public List<MapDefinition> maps = new List<MapDefinition>();
        public Material playerMaterial;
        public string playerName = "잠들지 못한 사람";
        public SurvivalSettings survivalSettings;
        public ItemEffectDefinition stimulant;
        public List<CompanionDefinition> companions = new List<CompanionDefinition>();
        public bool showTitleScreen;
        [Header("Presentation and saves")]
        public string gameTitle = "NO SLEEP";
        public string gameTagline = "잠들 수 없는 세상에서, 다음 아침까지.";
        [TextArea] public string openingNotice;
        [Tooltip("A single folder name below persistentDataPath. Each game can keep independent saves.")]
        public string saveFolderName = "NoSleep";

        void Awake()
        {
            if (startingMap == null) { Debug.LogError("No Sleep requires a starting map. Use No Sleep/Create Demo.", this); enabled = false; return; }
            if (string.IsNullOrWhiteSpace(saveFolderName) || saveFolderName == "." || saveFolderName == ".." ||
                saveFolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || saveFolderName.Contains('/') || saveFolderName.Contains('\\'))
            { Debug.LogError("Save folder must be a single valid folder name.", this); enabled = false; return; }
            var session = gameObject.AddComponent<GameSession>();
            session.ConfigureSaveDirectory(Path.Combine(Application.persistentDataPath, saveFolderName, "Saves"));
            var runner = gameObject.AddComponent<EventRunner>();
            var ui = gameObject.AddComponent<GameUI>();
            ui.gameTitle = gameTitle; ui.gameTagline = gameTagline;
            var mapObject = new GameObject("World"); mapObject.transform.SetParent(transform, false);
            var world = mapObject.AddComponent<TileMapRuntime>();
            var playerObject = new GameObject("Player");
            playerObject.transform.SetParent(transform, false);
            var controller = playerObject.AddComponent<CharacterController>(); controller.height = 1.8f; controller.radius = 0.23f;
            controller.center = Vector3.up * 0.9f; controller.stepOffset = 0.15f; controller.skinWidth = 0.02f; controller.minMoveDistance = 0;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule); visual.name = "Visual"; visual.transform.SetParent(playerObject.transform, false);
            visual.transform.localPosition = Vector3.up * 0.9f; visual.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            Destroy(visual.GetComponent<Collider>()); if (playerMaterial != null) visual.GetComponent<Renderer>().sharedMaterial = playerMaterial;
            var player = playerObject.AddComponent<PlayerController>();
            var cameraObject = new GameObject("Quarter View Camera", typeof(Camera), typeof(AudioListener), typeof(QuarterViewCamera));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera"; var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.045f, 0.065f); camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
            var follow = cameraObject.GetComponent<QuarterViewCamera>(); follow.target = player.transform;
            session.State.player.name = playerName;
            if (!maps.Contains(startingMap)) maps.Add(startingMap);
            session.Configure(world, player, runner, ui, maps);
            session.ConfigureNewGame(startingMap, playerName);
            session.ConfigureCompanions(companions);
            session.ConfigureHorror(survivalSettings, stimulant);
            if (survivalSettings != null) playerObject.AddComponent<PartyFollowers>().Configure(session, playerMaterial);
            if (!session.TransferPlayer(startingMap, startingMap.spawnCell)) { Debug.LogError("Invalid demo spawn.", this); return; }
            follow.Snap();
            if (showTitleScreen) ui.ShowTitle();
            else ui.Notice(!string.IsNullOrWhiteSpace(openingNotice) ? openingNotice :
                survivalSettings != null ? "다음 목표를 확인하고 주변을 살펴보세요." : "한밤중의 복도. 안내인과 대화하고 문을 열어 보세요.");
        }
    }
}
