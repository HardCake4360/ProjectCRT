using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class SystemsLabBuilder
    {
        public const string LabScene = "Assets/NoSleep/Scenes/NoSleepSystemsLab.unity";
        public const string Data = "Assets/NoSleep/Data/SystemsLab/";

        [MenuItem("No Sleep/Create Systems Lab")]
        public static void BuildAndValidate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Freeze original legacy mappings before introducing or reordering any new page.
            EventIdentityUpgrader.UpgradeAll();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScene) != null)
            {
                EditorSceneManager.OpenScene(LabScene);
                AddLabToBuild();
                DemoBuilder.ValidateContent();
                Debug.Log("NO_SLEEP_SYSTEMS_LAB_OPENED: existing scene and data preserved.");
                return;
            }
            foreach (string folder in new[] { "Materials", "Tiles", "Maps", "Events", "Common", "Rules" }) EnsureFolder(Data + folder);
            EnsureFolder("Assets/NoSleep/Scenes");
            var settings = Asset<SurvivalSettings>("Rules/Settings", value =>
            { value.profileId = "no_sleep_systems_lab"; value.timeMode = TimeMode.AuthoredCosts; value.fatiguePerMinute = 0; });
            var stimulant = Asset<ItemEffectDefinition>("Rules/Stimulant", value =>
            { value.itemId = "lab_stimulant"; value.displayName = "실험용 각성제"; });
            var floor = Tile("Floor", true, 0.15f, new Color(0.28f, 0.35f, 0.36f));
            var wall = Tile("Wall", false, 1.6f, new Color(0.11f, 0.17f, 0.2f));
            var main = Map("Main", "systems_lab", "깨어 있는 보관실", floor, wall, out bool newMain);
            var annex = Map("Annex", "systems_lab_annex", "옆 보관실", floor, wall, out bool newAnnex);
            var welcome = Asset<CommonEventDefinition>("Common/SharedWelcome", definition =>
            {
                definition.displayName = "보관인의 공통 응대";
                definition.parameters = new List<EventParameter>
                {
                    new EventParameter { name = "visitor", type = EventParameterType.String, stringValue = "보관인" },
                    new EventParameter { name = "number", type = EventParameterType.Int, intValue = 1 }
                };
                definition.commands = Commands(
                    Say("{param:visitor}", "나는 {param:number}번 보관인이야. 내 표식도 하나 챙겨 가. 서로의 보관함은 따로 관리하고 있어."),
                    new EventCommand { type = EventCommandType.GrantOnce, mutations = new List<StateMutation>
                    {
                        new StateMutation { kind = StateMutationKind.AddItem, key = "lab_token", intValue = 1 },
                        new StateMutation { kind = StateMutationKind.SetSelfSwitch, key = "A", boolValue = true }
                    } });
            });
            var npcA = Npc("KeeperA", "보관인 A", 1, welcome, new Color(0.37f, 0.68f, 0.62f));
            var npcB = Npc("KeeperB", "보관인 B", 2, welcome, new Color(0.75f, 0.55f, 0.34f));
            var noticeA = Notice("NoticeA", "첫 번째 표시등", "첫 번째 장치의 표시등이 켜졌다. 기다리던 표식은 이미 챙겨 두었다.", "lab_notice_a");
            var noticeB = Notice("NoticeB", "두 번째 표시등", "두 번째 표시등도 켜졌다. 두 장치의 알림을 차례로 확인했다.", "lab_notice_b");
            var watchA = Watch("WatchA", noticeA, "lab_marker_a");
            var watchB = Watch("WatchB", noticeB, "lab_marker_b");
            var panel = Asset<EventDefinition>("Events/ControlPanel", definition =>
            {
                definition.displayName = "보관실 장치"; definition.fallbackColor = new Color(0.56f, 0.68f, 0.33f);
                definition.pages.Add(new EventPage { commands = Commands(
                    new EventCommand { type = EventCommandType.ShowChoices, text = "두 표시등은 장치를 켠 뒤 십 분이 지나면 반응한다.", choices = new List<EventChoice>
                    {
                        new EventChoice { text = "장치를 켠다", targetLabel = "activate" },
                        new EventChoice { text = "십 분을 보낸다", targetLabel = "advance" },
                        new EventChoice { text = "옆 보관실로 간다", targetLabel = "annex" },
                        new EventChoice { text = "그만둔다", targetLabel = "leave" }
                    } },
                    Label("activate"), new EventCommand { type = EventCommandType.SetSwitch, key = "lab_watch_enabled", boolValue = true },
                    Say("", "장치를 켰다. 기다리는 동안 다른 사람과 이야기하거나 표식을 살펴볼 수 있다."), End(),
                    Label("advance"), new EventCommand { type = EventCommandType.AdvanceTime, intValue = 10 }, End(),
                    Label("annex"), Transfer(annex, annex.spawnCell), End(), Label("leave"), End()) });
            });
            var intro = Asset<EventDefinition>("Events/Introduction", definition =>
            {
                definition.displayName = "보관실 첫 방문";
                definition.pages.Add(new EventPage { trigger = EventTrigger.Autorun, runOnce = true, visible = false, blocksMovement = false,
                    commands = Commands(Say("", "깨어 있는 두 보관인이 있다. 각자 표식을 하나씩 건넨다. 뒤쪽 장치의 두 표시등은 시간을 보내면 반응한다.")) });
            });
            var returnDoor = Asset<EventDefinition>("Events/ReturnDoor", definition =>
            {
                definition.displayName = "보관실로 돌아가는 문";
                definition.pages.Add(new EventPage { blocksMovement = false, commands = Commands(Transfer(main, main.spawnCell)) });
            });
            var annexPanel = Asset<EventDefinition>("Events/AnnexClock", definition =>
            {
                definition.displayName = "옆 보관실의 시계";
                definition.pages.Add(new EventPage { commands = Commands(Say("", "시계를 보며 십 분을 보낸다."),
                    new EventCommand { type = EventCommandType.AdvanceTime, intValue = 10 }) });
            });
            if (newMain) Place(main, ("intro", 1, 1, intro), ("keeper_a", 5, 3, npcA), ("keeper_b", 8, 3, npcB),
                ("panel", 6, 6, panel), ("watch_a", 2, 1, watchA), ("watch_b", 3, 1, watchB));
            if (newAnnex) Place(annex, ("return", 9, 6, returnDoor), ("clock", 5, 5, annexPanel));
            AssetDatabase.SaveAssets();
            EventIdentityUpgrader.UpgradeAll();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.58f);
            var lightObject = new GameObject("Soft Daylight", typeof(Light)); var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55, -35, 0);
            var root = new GameObject("No Sleep Systems Lab"); var bootstrap = root.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = main; bootstrap.maps = new List<MapDefinition> { main, annex };
            bootstrap.survivalSettings = settings; bootstrap.stimulant = stimulant;
            bootstrap.playerMaterial = Material("Player", new Color(0.86f, 0.82f, 0.66f)); bootstrap.showTitleScreen = true;
            EditorSceneManager.SaveScene(scene, LabScene);
            AddLabToBuild();
            Selection.activeObject = root; DemoBuilder.ValidateContent();
            Debug.Log("NO_SLEEP_SYSTEMS_LAB_CREATED: " + LabScene);
        }

        static void AddLabToBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(value => value.path == LabScene))
            { scenes.Add(new EditorBuildSettingsScene(LabScene, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
        }

        static EventDefinition Npc(string file, string name, int number, CommonEventDefinition common, Color color)
            => Asset<EventDefinition>("Events/" + file, definition =>
            {
                definition.displayName = name; definition.fallbackColor = color;
                definition.pages.Add(new EventPage { commands = Commands(new EventCommand
                {
                    type = EventCommandType.CallCommon, commonEvent = common, arguments = new List<EventArgument>
                    {
                        new EventArgument { name = "visitor", type = EventParameterType.String, stringValue = name },
                        new EventArgument { name = "number", type = EventParameterType.Int, intValue = number }
                    }
                }) });
                definition.pages.Add(new EventPage { conditions = new List<StateCondition>
                { new StateCondition { kind = ConditionKind.SelfSwitch, key = "A" } },
                    commands = Commands(Say(name, "내 표식은 이미 건넸어. 다른 보관인에게도 물어봐.")) });
            });
        static EventDefinition Notice(string file, string name, string text, string switchId)
            => Asset<EventDefinition>("Events/" + file, definition =>
            {
                definition.displayName = name;
                definition.pages.Add(new EventPage { runOnce = true, visible = false, blocksMovement = false,
                    commands = Commands(Say("", text), new EventCommand { type = EventCommandType.SetSwitch, key = switchId, boolValue = true }) });
            });
        static EventDefinition Watch(string file, EventDefinition notice, string itemId)
            => Asset<EventDefinition>("Events/" + file, definition =>
            {
                definition.displayName = file;
                definition.pages.Add(new EventPage { trigger = EventTrigger.Parallel, runOnce = true, visible = false, blocksMovement = false,
                    conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.Switch, key = "lab_watch_enabled" } },
                    commands = Commands(new EventCommand { type = EventCommandType.GrantOnce, mutations = new List<StateMutation>
                    {
                        new StateMutation { kind = StateMutationKind.AddItem, key = itemId, intValue = 1 },
                        new StateMutation { kind = StateMutationKind.AddVariable, key = "lab_reward_count", intValue = 1 }
                    } }, new EventCommand { type = EventCommandType.WaitGameMinutes, intValue = 10 },
                    new EventCommand { type = EventCommandType.Checkpoint },
                    new EventCommand { type = EventCommandType.RequestForeground, foregroundEvent = notice }) });
            });
        static MapDefinition Map(string file, string id, string display, TileDefinition floor, TileDefinition wall, out bool created)
        {
            created = AssetDatabase.LoadMainAssetAtPath(Data + "Maps/" + file + ".asset") == null;
            return Asset<MapDefinition>("Maps/" + file, map =>
            {
                map.mapId = id; map.displayName = display; map.palette = new List<TileDefinition> { floor, wall };
                map.Resize(14, 10); map.Fill(floor); map.spawnCell = new Vector2Int(2, 3);
                for (int x = 0; x < map.width; x++) { map.SetTile(new Vector2Int(x, 0), wall); map.SetTile(new Vector2Int(x, 9), wall); }
                for (int y = 0; y < map.height; y++) { map.SetTile(new Vector2Int(0, y), wall); map.SetTile(new Vector2Int(13, y), wall); }
            });
        }
        static void Place(MapDefinition map, params (string id, int x, int y, EventDefinition definition)[] placements)
        {
            foreach (var placement in placements) map.events.Add(new MapEventPlacement
            { id = placement.id, cell = new Vector2Int(placement.x, placement.y), definition = placement.definition });
            EditorUtility.SetDirty(map);
        }
        static TileDefinition Tile(string file, bool walkable, float height, Color color)
            => Asset<TileDefinition>("Tiles/" + file, tile => { tile.displayName = file; tile.walkable = walkable;
                tile.height = height; tile.fallbackColor = color; tile.material = Material(file, color); });
        static Material Material(string file, Color color)
        {
            string path = Data + "Materials/" + file + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Unexpected asset type: " + path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var value = new Material(shader) { color = color }; AssetDatabase.CreateAsset(value, path); return value;
        }
        static T Asset<T>(string relative, Action<T> initialize) where T : ScriptableObject
        {
            string path = Data + relative + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path); if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Unexpected asset type: " + path);
            var value = ScriptableObject.CreateInstance<T>(); initialize(value); AssetDatabase.CreateAsset(value, path);
            EditorUtility.SetDirty(value); return value;
        }
        static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        static List<EventCommand> Commands(params EventCommand[] commands) => new List<EventCommand>(commands);
        static EventCommand Say(string speaker, string text) => new EventCommand { type = EventCommandType.ShowDialogue, speaker = speaker, text = text };
        static EventCommand Label(string name) => new EventCommand { type = EventCommandType.Label, label = name };
        static EventCommand End() => new EventCommand { type = EventCommandType.End };
        static EventCommand Transfer(MapDefinition map, Vector2Int cell) => new EventCommand { type = EventCommandType.TransferPlayer, map = map, cell = cell };
    }
}
