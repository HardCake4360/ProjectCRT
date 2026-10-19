using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoSleep.Editor
{
    public static class DemoBuilder
    {
        public const string DemoScene = "Assets/NoSleep/Scenes/NoSleepDemo.unity";
        const string Data = "Assets/NoSleep/Data/";

        [MenuItem("No Sleep/Create or Open Demo")]
        public static void CreateDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene) != null)
            {
                EditorSceneManager.OpenScene(DemoScene);
                Debug.Log("Opened existing No Sleep demo. Your edited data was preserved."); return;
            }
            EnsureFolder("Assets/NoSleep/Scenes"); EnsureFolder(Data + "Materials"); EnsureFolder(Data + "Tiles");
            EnsureFolder(Data + "Maps"); EnsureFolder(Data + "Events");
            var floor = Tile("Floor", "복도 지면", true, 0.15f, new Color(0.23f, 0.32f, 0.35f));
            var wall = Tile("Wall", "벽", false, 1.65f, new Color(0.09f, 0.16f, 0.19f));
            var rug = Tile("Rug", "오래된 카펫", true, 0.025f, new Color(0.32f, 0.21f, 0.23f));
            var roomFloor = Tile("RoomFloor", "방 지면", true, 0.15f, new Color(0.29f, 0.27f, 0.24f));
            var corridor = NewMap("Corridor", "corridor", "한밤중의 복도", 16, 12, floor, wall, rug);
            var room = NewMap("QuietRoom", "quiet_room", "조용한 방", 12, 10, roomFloor, wall, rug);
            for (int x = 2; x <= 10; x++) if (x != 6) corridor.SetTile(new Vector2Int(x, 6), wall);
            for (int x = 2; x <= 6; x++) for (int y = 2; y <= 4; y++) corridor.SetTile(new Vector2Int(x, y), rug, TileLayer.Decoration);
            for (int x = 3; x <= 8; x++) for (int y = 4; y <= 6; y++) room.SetTile(new Vector2Int(x, y), rug, TileLayer.Decoration);
            corridor.spawnCell = new Vector2Int(3, 3); room.spawnCell = new Vector2Int(3, 3);
            var intro = NewEvent("Introduction", "복도의 속삭임", new Color(0.8f, 0.75f, 0.45f));
            intro.pages.Add(new EventPage
            {
                name = "첫 방문", trigger = EventTrigger.Autorun, runOnce = true, visible = false, blocksMovement = false,
                commands = new List<EventCommand> { Say("", "모두 잠든 시간. 당신만 이 복도를 걷고 있다.\nWASD로 이동하고 E로 대화하세요. Tab으로 상태를 확인할 수 있습니다.") }
            });
            var guide = NewEvent("Guide", "잠들지 않는 안내인", new Color(0.36f, 0.72f, 0.67f));
            guide.pages.Add(new EventPage
            {
                name = "열쇠를 건네기 전", commands = new List<EventCommand>
                {
                    Say("안내인", "또 잠들지 못했나요? 복도 끝의 방에는 아직 불이 켜져 있어요."),
                    new EventCommand { type = EventCommandType.ShowChoices, text = "안내인이 작은 열쇠를 내밉니다.", choices = new List<EventChoice>
                    {
                        new EventChoice { text = "열쇠를 받는다", targetLabel = "accept" },
                        new EventChoice { text = "나중에 돌아온다", targetLabel = "leave" }
                    } },
                    Label("accept"), new EventCommand { type = EventCommandType.AddItem, key = "old_key", intValue = 1 },
                    new EventCommand { type = EventCommandType.SetSwitch, key = "guide_gave_key", boolValue = true },
                    Say("안내인", "오래된 열쇠를 받았습니다. 북동쪽 문을 열어 보세요."), End(),
                    Label("leave"), Say("안내인", "언제든 돌아오세요. 저는 여기 있어요.")
                }
            });
            guide.pages.Add(new EventPage
            {
                name = "열쇠를 건넨 뒤", conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.Switch, key = "guide_gave_key", expected = true } },
                commands = new List<EventCommand> { Say("안내인", "열쇠는 잘 챙겼나요? 상태창에서 소지품을 확인할 수 있어요.") }
            });
            var box = NewEvent("SupplyBox", "낡은 보관함", new Color(0.84f, 0.62f, 0.32f));
            box.pages.Add(new EventPage
            {
                name = "처음 열기", commands = new List<EventCommand>
                {
                    Say("", "보관함 안에서 동전 20개를 찾았습니다."),
                    new EventCommand { type = EventCommandType.AddGold, intValue = 20 },
                    new EventCommand { type = EventCommandType.SetSelfSwitch, key = "A", boolValue = true }
                }
            });
            box.pages.Add(new EventPage
            {
                name = "이미 열림", conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.SelfSwitch, key = "A", expected = true } },
                commands = new List<EventCommand> { Say("", "보관함은 비어 있습니다.") }
            });
            var door = NewEvent("LockedDoor", "조용한 방으로 가는 문", new Color(0.53f, 0.42f, 0.66f));
            door.pages.Add(new EventPage
            {
                name = "열쇠 확인", blocksMovement = false, commands = new List<EventCommand>
                {
                    new EventCommand { type = EventCommandType.IfCondition, condition = new StateCondition { kind = ConditionKind.Item, key = "old_key", value = 1 }, targetLabel = "locked" },
                    Say("", "오래된 열쇠로 문을 열었습니다."),
                    new EventCommand { type = EventCommandType.TransferPlayer, map = room, cell = room.spawnCell }, End(),
                    Label("locked"), Say("", "문이 잠겨 있습니다. 안내인이 열쇠를 가지고 있을지도 모릅니다.")
                }
            });
            var exit = NewEvent("ReturnDoor", "복도로 돌아가는 문", new Color(0.53f, 0.42f, 0.66f));
            exit.pages.Add(new EventPage { name = "복도로 돌아가기", blocksMovement = false, commands = new List<EventCommand>
            { new EventCommand { type = EventCommandType.TransferPlayer, map = corridor, cell = new Vector2Int(12, 8) } } });
            var whisper = NewEvent("Whisper", "누군가의 목소리", Color.white);
            whisper.pages.Add(new EventPage
            {
                name = "발걸음에 반응", trigger = EventTrigger.PlayerTouch, runOnce = true, visible = false, blocksMovement = false,
                commands = new List<EventCommand>
                { Say("???", "아직 잠들면 안 돼."), new EventCommand { type = EventCommandType.AddVariable, key = "heard_whispers", intValue = 1 } }
            });
            var diary = NewEvent("Diary", "펼쳐진 일기", new Color(0.4f, 0.55f, 0.8f));
            diary.pages.Add(new EventPage { commands = new List<EventCommand>
            { Say("일기", "첫 번째 밤. 문은 열리지만, 출구는 아직 보이지 않는다.\n이 맵과 대사는 No Sleep 제작 도구에서 수정할 수 있습니다.") } });
            corridor.events.Add(Placement("intro", new Vector2Int(1, 1), intro));
            corridor.events.Add(Placement("guide", new Vector2Int(5, 3), guide));
            corridor.events.Add(Placement("supply_box", new Vector2Int(9, 3), box));
            corridor.events.Add(Placement("room_door", new Vector2Int(13, 8), door));
            room.events.Add(Placement("return_door", new Vector2Int(5, 2), exit));
            room.events.Add(Placement("whisper", new Vector2Int(4, 4), whisper));
            room.events.Add(Placement("diary", new Vector2Int(7, 5), diary));
            var playerMaterial = Material("Player", new Color(0.92f, 0.88f, 0.72f));
            foreach (var asset in new UnityEngine.Object[] { corridor, room, intro, guide, box, door, exit, whisper, diary }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.67f);
            var lightObject = new GameObject("Moonlight", typeof(Light)); var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.color = new Color(0.78f, 0.86f, 1); light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -35, 0);
            var root = new GameObject("No Sleep"); var bootstrap = root.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = corridor; bootstrap.maps = new List<MapDefinition> { corridor, room }; bootstrap.playerMaterial = playerMaterial;
            EditorSceneManager.SaveScene(scene, DemoScene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(x => x.path == DemoScene)) { scenes.Add(new EditorBuildSettingsScene(DemoScene, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            Selection.activeObject = root;
            Debug.Log("NO_SLEEP_DEMO_CREATED: " + DemoScene);
        }

        [MenuItem("No Sleep/Validate Content")]
        public static void ValidateContent()
        {
            var errors = new List<string>(); var mapIds = new HashSet<string>(StringComparer.Ordinal);
            errors.AddRange(HorrorContentValidator.CollectErrors());
            errors.AddRange(NpcContentValidator.CollectErrors());
            errors.AddRange(EventIdentityUpgrader.CollectErrors());
            var references = ContentReferenceIndex.Build();
            errors.AddRange(references.GetDiagnostics().Where(value => value.Severity == ContentDiagnosticSeverity.Error).Select(value => value.ToString()));
            errors.AddRange(MapConnectionAnalyzer.Collect(references.Maps, references.Registries).Diagnostics
                .Where(value => value.Severity == ContentDiagnosticSeverity.Error).Select(value => value.ToString()));
            foreach (string guid in AssetDatabase.FindAssets("t:EventTemplateDefinition", new[] { "Assets/NoSleep" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (string error in AssetDatabase.LoadAssetAtPath<EventTemplateDefinition>(path).GetDiagnostics()) errors.Add(path + ": " + error);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:MapDefinition", new[] { "Assets/NoSleep" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(path);
                if (!mapIds.Add(map.mapId)) errors.Add(path + ": 중복된 맵 ID " + map.mapId);
                foreach (var error in map.Validate()) errors.Add(path + ": " + error);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:EventDefinition", new[] { "Assets/NoSleep" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var definition = AssetDatabase.LoadAssetAtPath<EventDefinition>(path);
                for (int i = 0; i < definition.pages.Count; i++)
                    try
                    {
                        var page = definition.pages[i];
                        if (page == null) throw new ArgumentException("빈 이벤트 페이지");
                        EventProgram.ValidateAndIndex(page.commands);
                        foreach (var command in page.commands)
                            if (command.type == EventCommandType.TransferPlayer && !command.map.IsWalkable(command.cell))
                                errors.Add(path + ": 페이지 " + i + "의 맵 이동 목적지가 이동 불가");
                    }
                    catch (Exception ex) { errors.Add(path + ": 페이지 " + i + " / " + ex.Message); }
            }
            if (errors.Count > 0)
            {
                foreach (var error in errors) Debug.LogError(error);
                if (Application.isBatchMode) throw new InvalidOperationException("No Sleep content validation failed.");
            }
            else Debug.Log("NO_SLEEP_CONTENT_VALID: all maps and event programs passed.");
        }
        public static void BuildAndValidate() { CreateDemo(); EventIdentityUpgrader.UpgradeAll(); ValidateContent(); }
        static MapDefinition NewMap(string file, string id, string display, int width, int height, TileDefinition floor, TileDefinition wall, TileDefinition rug)
        {
            var map = CreateAsset<MapDefinition>(Data + "Maps/" + file + ".asset"); map.mapId = id; map.displayName = display;
            map.palette = new List<TileDefinition> { floor, wall, rug }; map.Resize(width, height); map.Fill(floor);
            for (int x = 0; x < width; x++) { map.SetTile(new Vector2Int(x, 0), wall); map.SetTile(new Vector2Int(x, height - 1), wall); }
            for (int y = 0; y < height; y++) { map.SetTile(new Vector2Int(0, y), wall); map.SetTile(new Vector2Int(width - 1, y), wall); }
            return map;
        }
        static TileDefinition Tile(string file, string display, bool walkable, float height, Color color)
        {
            var tile = CreateAsset<TileDefinition>(Data + "Tiles/" + file + ".asset"); tile.displayName = display;
            tile.walkable = walkable; tile.height = height; tile.fallbackColor = color; tile.material = Material(file, color);
            EditorUtility.SetDirty(tile); return tile;
        }
        static Material Material(string file, Color color)
        {
            string path = Data + "Materials/" + file + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static EventDefinition NewEvent(string file, string display, Color color)
        { var value = CreateAsset<EventDefinition>(Data + "Events/" + file + ".asset"); value.displayName = display; value.fallbackColor = color; return value; }
        static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Preserving existing asset: " + path + ". Open your existing map instead.");
            var value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
        }
        static MapEventPlacement Placement(string id, Vector2Int cell, EventDefinition definition) => new MapEventPlacement { id = id, cell = cell, definition = definition };
        static EventCommand Say(string speaker, string text) => new EventCommand { type = EventCommandType.ShowDialogue, speaker = speaker, text = text };
        static EventCommand Label(string label) => new EventCommand { type = EventCommandType.Label, label = label };
        static EventCommand End() => new EventCommand { type = EventCommandType.End };
        static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
