using System;
using System.Collections.Generic;
using System.Linq;
using NoSleep;
using NoSleep.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCRT.RPG.Editor
{
    // Kept below Assets/NoSleep so the imported browsers and validators include CRT content.
    public static class RpgPrototypeBuilder
    {
        public const string ScenePath = "Assets/NoSleep/Scenes/ProjectCRT_RPG.unity";
        public const string DataPath = "Assets/NoSleep/Data/ProjectCRT";
        public const string OfficeId = "crt_rpg_office";
        public const string StreetId = "crt_rpg_street";
        public const string EvidenceId = "crt_rpg_receipt";
        public const string WitnessSwitch = "crt_rpg_witness_spoken";
        public const string SaveFolder = "ProjectCRT_RPG";

        [MenuItem("Project CRT/RPG/Create or Open Prototype")]
        public static void CreateOrOpen()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                AddBuildScene();
                Debug.Log("CRT_RPG_OPENED: existing scene and authored assets preserved.");
                return;
            }
            // A failed earlier generation must never overwrite an author's assets on retry.
            if (AssetDatabase.IsValidFolder(DataPath) && AssetDatabase.FindAssets("", new[] { DataPath }).Length > 0)
                throw new InvalidOperationException("CRT RPG data already exists. Restore the scene or open the existing maps; generation preserves authored data.");
            Folder(DataPath + "/Maps"); Folder(DataPath + "/Events"); Folder(DataPath + "/Tiles");
            Folder(DataPath + "/Materials"); Folder(DataPath + "/NPCs"); Folder("Assets/NoSleep/Scenes");
            var floor = Tile("OfficeFloor", "사무실 바닥", true, .15f, new Color(.22f, .26f, .27f));
            var asphalt = Tile("StreetFloor", "골목 바닥", true, .15f, new Color(.12f, .17f, .20f));
            var wall = Tile("Wall", "벽", false, 1.6f, new Color(.09f, .13f, .15f));
            var accent = Tile("Accent", "표식", true, .025f, new Color(.25f, .43f, .39f));
            var office = Map("Office", OfficeId, "조사 사무실", 14, 11, floor, wall, accent);
            var street = Map("Street", StreetId, "사무실 앞 골목", 16, 12, asphalt, wall, accent);
            office.spawnCell = new Vector2Int(3, 3); street.spawnCell = new Vector2Int(3, 3);
            for (int x = 2; x <= 5; x++) for (int y = 2; y <= 4; y++) office.SetTile(new Vector2Int(x, y), accent, TileLayer.Decoration);
            for (int x = 2; x <= 13; x++) street.SetTile(new Vector2Int(x, 3), accent, TileLayer.Decoration);

            var intro = Event("Introduction", "조사 시작", Color.white,
                new EventPage { name = "첫 방문", trigger = EventTrigger.Autorun, visible = false, blocksMovement = false, runOnce = true,
                    commands = Commands(Objective("crt_rpg_investigate", "골목의 목격자와 대화하고 영수증을 조사한다"),
                        Say("", "조사 사무실. 책상 옆 문을 통해 골목으로 나갈 수 있다.\nWASD로 이동하고 E로 조사한다. 단서와 대화 기록은 J에서 확인한다.")) });
            var terminal = Event("Terminal", "사무실 단말기", new Color(.25f, .7f, .6f),
                new EventPage { name = "조사 전", commands = Commands(Say("단말기", "골목의 목격자에게 이야기를 듣고, 남겨진 영수증을 찾아보자.")) },
                new EventPage { name = "단서 확보", conditions = Conditions(Item(EvidenceId)), commands = Commands(
                    new EventCommand { type = EventCommandType.IfCondition, condition = Switch(WitnessSwitch), targetLabel = "need_witness" },
                    Objective("crt_rpg_review", "목격자 진술과 영수증을 확보했다"),
                    Say("단말기", "진술과 영수증을 확보했다. 다음 조사로 넘어갈 준비가 됐다.\nF5로 현재 조사와 위치를 저장할 수 있다."), End(),
                    Label("need_witness"), Say("단말기", "영수증은 확보했다. 골목의 목격자에게도 이야기를 들어 보자.")) });
            var receipt = Event("Receipt", "바닥의 영수증", new Color(.85f, .73f, .43f),
                new EventPage { name = "단서 조사", commands = Commands(
                    Say("", "젖은 영수증에 방문 시간이 남아 있다. 단서로 보관한다."),
                    new EventCommand { type = EventCommandType.GrantOnce, mutations = new List<StateMutation>
                    { new StateMutation { kind = StateMutationKind.AddItem, key = EvidenceId, intValue = 1 },
                      new StateMutation { kind = StateMutationKind.SetSelfSwitch, key = "A", boolValue = true } } },
                    new EventCommand { type = EventCommandType.RecordClue, key = EvidenceId, speaker = "영수증", text = "골목에서 찾은 영수증. 방문 시간이 기록되어 있다." }) },
                new EventPage { name = "조사 완료", conditions = Conditions(new StateCondition { kind = ConditionKind.SelfSwitch, key = "A" }),
                    commands = Commands(Say("", "영수증은 이미 보관했다. J에서 기록을 확인할 수 있다.")) });
            var witness = Event("Witness", "골목의 목격자", new Color(.38f, .64f, .78f),
                new EventPage { name = "첫 대화", commands = Commands(
                    Say("목격자", "무언가를 찾고 있나요? 저쪽에 영수증이 떨어져 있었어요."),
                    new EventCommand { type = EventCommandType.ShowChoices, text = "무엇을 물어볼까?", choices = new List<EventChoice>
                    { new EventChoice { text = "본 것을 물어본다", targetLabel = "ask" }, new EventChoice { text = "다음에 이야기한다", targetLabel = "leave" } } },
                    Label("ask"), Say("목격자", "누군가 급히 골목을 빠져나갔어요. 영수증의 시간이 도움이 될지도 모르겠네요."),
                    new EventCommand { type = EventCommandType.SetSwitch, key = WitnessSwitch },
                    new EventCommand { type = EventCommandType.RecordClue, key = "crt_rpg_testimony", speaker = "목격자 진술", text = "누군가 급히 골목을 빠져나갔다. 영수증과 함께 확인해 보자." }, End(),
                    Label("leave"), Say("목격자", "필요하면 다시 말을 걸어 주세요.")) },
                new EventPage { name = "진술 후", conditions = Conditions(Switch(WitnessSwitch)),
                    commands = Commands(Say("목격자", "사무실 단말기에서 진술과 단서를 정리해 보세요.")) });
            var npc = Asset<NpcDefinition>(DataPath + "/NPCs/Witness.asset");
            npc.npcId = "crt_rpg_witness"; npc.displayName = witness.displayName; npc.fallbackColor = witness.fallbackColor;
            var toStreet = Event("ToStreet", "골목으로 나가는 문", new Color(.34f, .65f, .61f),
                new EventPage { name = "골목으로", blocksMovement = false, commands = Commands(Transfer(street, street.spawnCell)) });
            var toOffice = Event("ToOffice", "사무실로 돌아가는 문", new Color(.34f, .65f, .61f),
                new EventPage { name = "사무실로", blocksMovement = false, commands = Commands(Transfer(office, new Vector2Int(9, 3))) });
            office.events.Add(Place("crt_intro", 1, 1, intro)); office.events.Add(Place("crt_terminal", 5, 6, terminal));
            office.events.Add(Place("crt_exit", 10, 3, toStreet));
            street.events.Add(Place("crt_return", 2, 3, toOffice)); street.events.Add(Place("crt_receipt", 11, 6, receipt));
            var person = Place("crt_witness", 6, 5, witness); person.npc = npc; street.events.Add(person);
            var registry = Asset<StateKeyRegistry>(DataPath + "/StateKeys.asset");
            registry.switchKeys.Add(WitnessSwitch); registry.itemIds.Add(EvidenceId);
            var playerMaterial = Material("Investigator", new Color(.79f, .87f, .78f));
            foreach (var asset in AssetDatabase.FindAssets("t:Object", new[] { DataPath }).Select(guid => AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid))))
                if (asset != null) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.53f, .60f, .64f);
            var light = new GameObject("Street Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.15f; light.color = new Color(.78f, .87f, 1f);
            light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(50, -35, 0);
            var root = new GameObject("Project CRT RPG"); var bootstrap = root.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = office; bootstrap.maps = new List<MapDefinition> { office, street };
            bootstrap.playerMaterial = playerMaterial; bootstrap.playerName = "조사관";
            bootstrap.gameTitle = "PROJECT CRT"; bootstrap.gameTagline = "단서를 찾고, 이야기를 연결한다.";
            bootstrap.saveFolderName = SaveFolder; bootstrap.showTitleScreen = true;
            bootstrap.openingNotice = "골목에서 목격자와 대화하고 단서를 조사하세요.";
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Unable to save CRT RPG scene.");
            AddBuildScene(); Selection.activeObject = root;
            Debug.Log("CRT_RPG_CREATED: " + ScenePath);
        }

        [MenuItem("Project CRT/RPG/Map Editor")]
        public static void Maps() => MapEditorWindow.Open();
        [MenuItem("Project CRT/RPG/Event Editor")]
        public static void Events() => EventEditorWindow.OpenMenu();
        [MenuItem("Project CRT/RPG/Content Browser")]
        public static void Browser() => EditorApplication.ExecuteMenuItem("No Sleep/Content Browser");
        [MenuItem("Project CRT/RPG/Map Connections")]
        public static void Connections() => EditorApplication.ExecuteMenuItem("No Sleep/Map Connections");
        [MenuItem("Project CRT/RPG/Event Debugger")]
        public static void Debugger() => EditorApplication.ExecuteMenuItem("No Sleep/Event Debugger");
        [MenuItem("Project CRT/RPG/Validate Content")]
        public static void Validate() => DemoBuilder.ValidateContent();
        public static void BuildAndValidate() { CreateOrOpen(); Validate(); }

        static void AddBuildScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(value => value.path == ScenePath))
            { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
        }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        static T Asset<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Preserving existing asset: " + path);
            var value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
        }
        static Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is required.");
            var value = new Material(shader) { color = color }; AssetDatabase.CreateAsset(value, DataPath + "/Materials/" + name + ".mat"); return value;
        }
        static TileDefinition Tile(string name, string display, bool walkable, float height, Color color)
        {
            var tile = Asset<TileDefinition>(DataPath + "/Tiles/" + name + ".asset");
            tile.displayName = display; tile.walkable = walkable; tile.height = height; tile.fallbackColor = color; tile.material = Material(name, color); return tile;
        }
        static MapDefinition Map(string name, string id, string display, int width, int height, TileDefinition floor, TileDefinition wall, TileDefinition accent)
        {
            var map = Asset<MapDefinition>(DataPath + "/Maps/" + name + ".asset"); map.mapId = id; map.displayName = display;
            map.palette = new List<TileDefinition> { floor, wall, accent }; map.Resize(width, height); map.Fill(floor);
            for (int x = 0; x < width; x++) { map.SetTile(new Vector2Int(x, 0), wall); map.SetTile(new Vector2Int(x, height - 1), wall); }
            for (int y = 0; y < height; y++) { map.SetTile(new Vector2Int(0, y), wall); map.SetTile(new Vector2Int(width - 1, y), wall); }
            return map;
        }
        static EventDefinition Event(string name, string display, Color color, params EventPage[] pages)
        {
            var value = Asset<EventDefinition>(DataPath + "/Events/" + name + ".asset");
            value.displayName = display; value.fallbackColor = color; value.pages.AddRange(pages); value.EnsureStableIds(); return value;
        }
        static List<EventCommand> Commands(params EventCommand[] commands) => commands.ToList();
        static List<StateCondition> Conditions(params StateCondition[] conditions) => conditions.ToList();
        static StateCondition Item(string key) => new StateCondition { kind = ConditionKind.Item, key = key };
        static StateCondition Switch(string key) => new StateCondition { kind = ConditionKind.Switch, key = key };
        static EventCommand Say(string speaker, string text) => new EventCommand { type = EventCommandType.ShowDialogue, speaker = speaker, text = text };
        static EventCommand Objective(string key, string text) => new EventCommand { type = EventCommandType.SetObjective, key = key, text = text };
        static EventCommand Label(string label) => new EventCommand { type = EventCommandType.Label, label = label };
        static EventCommand End() => new EventCommand { type = EventCommandType.End };
        static EventCommand Transfer(MapDefinition map, Vector2Int cell) => new EventCommand { type = EventCommandType.TransferPlayer, map = map, cell = cell };
        static MapEventPlacement Place(string id, int x, int y, EventDefinition definition) => new MapEventPlacement { id = id, cell = new Vector2Int(x, y), definition = definition };
    }
}
