using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class NpcLabBuilder
    {
        public const string LabScene = "Assets/NoSleep/Scenes/NoSleepNpcLab.unity";
        public const string Data = "Assets/NoSleep/Data/NpcLab/";
        [MenuItem("No Sleep/Create NPC Lab")]
        public static void BuildAndValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode before creating the NPC lab.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EventIdentityUpgrader.UpgradeAll();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LabScene) != null)
            {
                EditorSceneManager.OpenScene(LabScene); AddToBuild(); DemoBuilder.ValidateContent();
                Debug.Log("NO_SLEEP_NPC_LAB_OPENED: existing scene and assets preserved."); return;
            }
            foreach (string folder in new[] { "Maps", "Tiles", "Materials", "Events", "Common", "Npcs", "Routes", "Rules" }) Folder(Data + folder);
            Folder("Assets/NoSleep/Scenes");
            var floor = Asset<TileDefinition>("Tiles/Floor", tile => { tile.walkable = true; tile.height = 0.12f; tile.fallbackColor = new Color(.3f, .34f, .36f); });
            var wall = Asset<TileDefinition>("Tiles/Wall", tile => { tile.walkable = false; tile.height = 1.5f; tile.fallbackColor = new Color(.16f, .2f, .24f); });
            var main = Map("Main", "npc_lab_main", "움직이는 보관실", 18, 14, floor, wall, out bool newMain);
            var annex = Map("Annex", "npc_lab_annex", "옆 작업실", 14, 10, floor, wall, out bool newAnnex);
            var crossA = Route("CrossA", true, (4, 5), (10, 5), (10, 7), (4, 7));
            var crossB = Route("CrossB", true, (10, 5), (4, 5), (4, 3), (10, 3));
            var day = Route("DayPatrol", true, (4, 10), (8, 10), (8, 11), (4, 11));
            var enemyRoute = Route("EnemyPatrol", true, (11, 9), (14, 9), (14, 7), (11, 7));
            var profileA = Profile("CrossA", "교차 담당 A", crossA, new Color(.35f, .7f, .7f));
            var profileB = Profile("CrossB", "교차 담당 B", crossB, new Color(.85f, .62f, .3f));
            var worker = Asset<NpcDefinition>("Npcs/ScheduleWorker", npc =>
            {
                npc.displayName = "시간표 담당"; npc.route = day; npc.speed = 1.4f;
                npc.schedules.Add(new NpcScheduleEntry { startMinute = 0, endMinute = 720, route = day });
                npc.schedules.Add(new NpcScheduleEntry { startMinute = 720, endMinute = 1440, stationCell = new Vector2Int(13, 10), yaw = 180 });
            });
            var annexKeeper = Profile("AnnexKeeper", "작업실 담당", null, new Color(.5f, .7f, .42f));
            var enemy = Asset<EnemyDefinition>("Rules/PatrolEnemy", value =>
            { value.enemyId = "npc_lab_patrol_enemy"; value.displayName = "연습용 그림자"; value.baseHealth = 18; value.baseAttack = 3; });
            var monster = Asset<NpcDefinition>("Npcs/PatrolEnemy", npc =>
            {
                npc.displayName = "순찰하는 그림자"; npc.route = enemyRoute; npc.enemy = enemy; npc.speed = 1.25f;
                npc.fallbackColor = new Color(.65f, .3f, .4f); npc.detectionRadius = 4; npc.leashRadius = 6; npc.contactRadius = 1.05f;
            });
            var common = Asset<CommonEventDefinition>("Common/Greeting", value =>
            {
                value.displayName = "움직이는 담당자의 공통 인사";
                value.parameters.Add(new EventParameter { name = "actor", type = EventParameterType.String, stringValue = "담당자" });
                value.commands = Commands(Say("{param:actor}", "대화하는 동안 움직임을 멈춘다. 내 인사 기록은 다른 담당자와 따로 남긴다."),
                    new EventCommand { type = EventCommandType.SetSelfSwitch, key = "greeted", boolValue = true });
            });
            var eventA = Dialogue("CrossA", profileA.displayName, common);
            var eventB = Dialogue("CrossB", profileB.displayName, common);
            var workerEvent = Dialogue("ScheduleWorker", worker.displayName, common);
            var annexEvent = Dialogue("AnnexKeeper", annexKeeper.displayName, common);
            var enemyEvent = Asset<EventDefinition>("Events/PatrolEnemy", definition =>
            {
                definition.displayName = enemy.displayName; definition.fallbackColor = monster.fallbackColor;
                definition.pages.Add(new EventPage { trigger = EventTrigger.PlayerTouch, blocksMovement = false,
                    commands = Commands(new EventCommand { type = EventCommandType.StartEncounter, enemy = enemy, key = "defeated" }) });
                definition.pages.Add(new EventPage { visible = false, blocksMovement = false,
                    conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.SelfSwitch, key = "defeated" } }, commands = Commands() });
            });
            var blocker = Asset<EventDefinition>("Events/Blocker", definition =>
            {
                definition.displayName = "움직이지 않는 보관함"; definition.fallbackColor = new Color(.5f, .47f, .42f);
                definition.pages.Add(new EventPage { blocksMovement = true, commands = Commands(Say("", "담당자들은 이 보관함을 피해 지나간다. 통로에 서면 길을 기다리거나 다시 찾는다.")) });
            });
            var panel = Asset<EventDefinition>("Events/ControlPanel", definition =>
            {
                definition.displayName = "시간과 이동 장치";
                definition.pages.Add(new EventPage { commands = Commands(
                    new EventCommand { type = EventCommandType.ShowChoices, text = "담당자들의 시간표와 움직임을 확인한다.", choices = new List<EventChoice> {
                        new EventChoice { text = "네 시간을 보낸다", targetLabel = "time" },
                        new EventChoice { text = "교차 담당 A를 멈춘다", targetLabel = "stop" },
                        new EventChoice { text = "교차 담당 A를 재개한다", targetLabel = "resume" },
                        new EventChoice { text = "옆 작업실로 간다", targetLabel = "annex" },
                        new EventChoice { text = "그만둔다", targetLabel = "end" } } },
                    Label("time"), new EventCommand { type = EventCommandType.AdvanceTime, intValue = 240 }, End(),
                    Label("stop"), new EventCommand { type = EventCommandType.StopNpc, key = "cross_a", boolValue = true }, End(),
                    Label("resume"), new EventCommand { type = EventCommandType.ResumeNpc, key = "cross_a" }, End(),
                    Label("annex"), Transfer(annex), End(), Label("end"), End()) });
            });
            var exit = Door("Exit", "옆 작업실로 가는 문", annex);
            var returnDoor = Door("Return", "보관실로 돌아가는 문", main);
            var intro = Asset<EventDefinition>("Events/Introduction", definition =>
            {
                definition.displayName = "움직이는 보관실 첫 방문";
                definition.pages.Add(new EventPage { trigger = EventTrigger.Autorun, runOnce = true, visible = false, blocksMovement = false,
                    commands = Commands(Say("", "두 담당자가 같은 통로를 지난다. 보관함과 사람을 피해 움직이고, 말을 걸면 멈춘다. 뒤쪽 시간표 담당자는 정오부터 다른 자리에 선다. 그림자는 가까이 다가가면 따라온다.")) });
            });
            if (newMain)
            {
                main.events.AddRange(new[] { Place("cross_a", 4, 5, eventA, profileA), Place("cross_b", 10, 5, eventB, profileB),
                    Place("worker", 4, 10, workerEvent, worker), Place("patrol_enemy", 11, 9, enemyEvent, monster),
                    Place("blocker", 7, 5, blocker), Place("panel", 3, 4, panel), Place("exit", 3, 11, exit), Place("intro", 1, 1, intro) });
                EditorUtility.SetDirty(main);
            }
            if (newAnnex)
            {
                annex.events.AddRange(new[] { Place("keeper", 8, 5, annexEvent, annexKeeper), Place("return", 3, 6, returnDoor) });
                EditorUtility.SetDirty(annex);
            }
            var settings = Asset<SurvivalSettings>("Rules/Settings", value =>
            { value.profileId = "no_sleep_npc_lab"; value.timeMode = TimeMode.AuthoredCosts; value.fatiguePerMinute = 0; });
            var stimulant = Asset<ItemEffectDefinition>("Rules/Stimulant", value => { value.itemId = "npc_lab_stimulant"; value.displayName = "실험용 각성제"; });
            AssetDatabase.SaveAssets(); EventIdentityUpgrader.UpgradeAll();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.52f, .56f, .6f);
            var light = new GameObject("NPC Lab Daylight", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(55, -30, 0);
            var root = new GameObject("No Sleep NPC Lab"); var bootstrap = root.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = main; bootstrap.maps = new List<MapDefinition> { main, annex }; bootstrap.survivalSettings = settings;
            bootstrap.stimulant = stimulant; bootstrap.showTitleScreen = true; bootstrap.playerMaterial = PlayerMaterial();
            EditorSceneManager.SaveScene(scene, LabScene); AddToBuild(); DemoBuilder.ValidateContent();
            Debug.Log("NO_SLEEP_NPC_LAB_CREATED: " + LabScene);
        }
        static NpcRouteDefinition Route(string file, bool repeat, params (int x, int y)[] points) => Asset<NpcRouteDefinition>("Routes/" + file, route =>
        {
            route.repeat = repeat; route.points = points.Select(cell => new NpcRoutePoint { cell = new Vector2Int(cell.x, cell.y), waitSeconds = .35f }).ToList();
        });
        static NpcDefinition Profile(string file, string name, NpcRouteDefinition route, Color color) => Asset<NpcDefinition>("Npcs/" + file, npc =>
        { npc.displayName = name; npc.route = route; npc.fallbackColor = color; npc.speed = 1.5f; });
        static EventDefinition Dialogue(string file, string speaker, CommonEventDefinition common) => Asset<EventDefinition>("Events/" + file, definition =>
        {
            definition.displayName = speaker; definition.pages.Add(new EventPage { commands = Commands(new EventCommand {
                type = EventCommandType.CallCommon, commonEvent = common, arguments = new List<EventArgument> {
                    new EventArgument { name = "actor", type = EventParameterType.String, stringValue = speaker } } }) });
            definition.pages.Add(new EventPage { conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.SelfSwitch, key = "greeted" } },
                commands = Commands(Say(speaker, "내 인사는 이미 기록했다. 다시 내 길을 이어가겠다.")) });
        });
        static EventDefinition Door(string file, string title, MapDefinition destination) => Asset<EventDefinition>("Events/" + file, definition =>
        { definition.displayName = title; definition.pages.Add(new EventPage { blocksMovement = false, commands = Commands(Transfer(destination)) }); });
        static MapDefinition Map(string file, string id, string title, int width, int height, TileDefinition floor, TileDefinition wall, out bool created)
        {
            created = AssetDatabase.LoadMainAssetAtPath(Data + "Maps/" + file + ".asset") == null;
            return Asset<MapDefinition>("Maps/" + file, map =>
            {
                map.mapId = id; map.displayName = title; map.Resize(width, height); map.Fill(floor); map.palette.Add(wall);
                map.spawnCell = new Vector2Int(2, 2);
                for (int x = 0; x < width; x++) { map.SetTile(new Vector2Int(x, 0), wall); map.SetTile(new Vector2Int(x, height - 1), wall); }
                for (int y = 0; y < height; y++) { map.SetTile(new Vector2Int(0, y), wall); map.SetTile(new Vector2Int(width - 1, y), wall); }
            });
        }
        static T Asset<T>(string path, Action<T> initialize) where T : ScriptableObject
        {
            path = Data + path + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<T>(path); if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Preserving unexpected asset: " + path);
            var value = ScriptableObject.CreateInstance<T>(); initialize(value);
            if (value is NpcDefinition npc) npc.EnsureStableIds(); else if (value is NpcRouteDefinition route) route.EnsureStableIds();
            AssetDatabase.CreateAsset(value, path); EditorUtility.SetDirty(value); return value;
        }
        static Material PlayerMaterial()
        {
            string path = Data + "Materials/Player.mat"; var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Unexpected material: " + path);
            var value = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.86f, .82f, .67f) };
            AssetDatabase.CreateAsset(value, path); return value;
        }
        static void AddToBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(scene => scene.path == LabScene)) { scenes.Add(new EditorBuildSettingsScene(LabScene, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
        }
        static void Folder(string path)
        {
            path = path.TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        static MapEventPlacement Place(string id, int x, int y, EventDefinition definition, NpcDefinition npc = null) =>
            new MapEventPlacement { id = id, cell = new Vector2Int(x, y), definition = definition, npc = npc };
        static EventCommand Transfer(MapDefinition map) => new EventCommand { type = EventCommandType.TransferPlayer, map = map, cell = map.spawnCell };
        static EventCommand Say(string speaker, string text) => new EventCommand { type = EventCommandType.ShowDialogue, speaker = speaker, text = text };
        static EventCommand Label(string label) => new EventCommand { type = EventCommandType.Label, label = label };
        static EventCommand End() => new EventCommand { type = EventCommandType.End };
        static List<EventCommand> Commands(params EventCommand[] commands)
        { var result = commands.ToList(); if (result.LastOrDefault()?.type != EventCommandType.End) result.Add(End()); return result; }
    }
}
