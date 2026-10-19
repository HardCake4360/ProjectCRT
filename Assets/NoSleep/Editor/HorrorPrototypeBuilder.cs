using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class HorrorPrototypeBuilder
    {
        public const string PrototypeScene = "Assets/NoSleep/Scenes/NoSleepHorrorPrototype.unity";
        public const string Data = "Assets/NoSleep/Data/Horror/";

        // Adjustable prototype assumptions: authored travel costs, a fourteen-day calendar,
        // and a small contact battle. Names, dialogue, separation, and destination do not resolve lore.
        [MenuItem("No Sleep/Create Horror Prototype")]
        public static void BuildAndValidate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EventIdentityUpgrader.UpgradeAll();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeScene) != null)
            {
                EditorSceneManager.OpenScene(PrototypeScene);
                DemoBuilder.ValidateContent();
                Debug.Log("NO_SLEEP_HORROR_OPENED: existing scene and authored assets preserved.");
                return;
            }
            foreach (string folder in new[] { "Materials", "Tiles", "Maps", "Events", "Rules", "Companions", "Enemies" })
                EnsureFolder(Data + folder);
            EnsureFolder("Assets/NoSleep/Scenes");

            var settings = Asset<SurvivalSettings>("Rules/SurvivalSettings", value => value.profileId = "no_sleep_horror_prototype");
            var stimulant = Asset<ItemEffectDefinition>("Rules/Stimulant", value => { value.itemId = "stimulant"; value.displayName = "각성제"; });
            var band = Asset<NightActivityDefinition>("Rules/BandNight", value =>
            {
                value.activityId = "band_night"; value.displayName = "엉망진창 밤샘 연주";
                value.description = "박자도 음정도 제각각인 연주를 계속했다. 누구도 잠들지 않았고, 다 같이 웃었다.";
                value.memoryTitle = "우리가 만든 소음"; value.memoryText = "악기를 다룰 줄 아는 사람은 없었다. 그래도 그 밤의 소리는 오래 기억할 것 같다.";
            });
            var roller = Asset<NightActivityDefinition>("Rules/RollerCoasterNight", value =>
            {
                value.activityId = "roller_coaster_night"; value.displayName = "해 뜰 때까지 롤러코스터";
                value.description = "텅 빈 놀이공원에서 같은 코스를 몇 번이고 돌았다. 아침이 올 때까지 함께 소리를 질렀다.";
                value.memoryTitle = "끝없이 돌던 밤"; value.memoryText = "처음엔 무서웠고, 나중엔 우스웠다. 모두 함께 해가 뜨는 것을 보았다.";
            });
            var min = Asset<CompanionDefinition>("Companions/Min", value =>
            { value.companionId = "min"; value.displayName = "민"; value.description = "병원에서 만난 동료. 함께 다음 장소로 향한다."; value.color = new Color(0.37f, 0.68f, 0.62f); });
            var jun = Asset<CompanionDefinition>("Companions/Jun", value =>
            { value.companionId = "jun"; value.displayName = "준"; value.description = "환승 구역에서 만난 동료. 낯선 존재를 관찰하는 법을 함께 익힌다."; value.color = new Color(0.78f, 0.55f, 0.3f); });
            var hallwayEnemy = Asset<EnemyDefinition>("Enemies/HallwayPresence", value =>
            { value.enemyId = "hallway_presence"; value.displayName = "복도의 낯선 형체"; value.baseHealth = 22; value.baseAttack = 5; });
            var lateEnemy = Asset<EnemyDefinition>("Enemies/FadingPresence", value =>
            { value.enemyId = "fading_presence"; value.displayName = "흐려지는 형체"; value.baseHealth = 30; value.baseAttack = 8; });

            var floor = Tile("Floor", "낡은 실내 바닥", true, 0.15f, new Color(0.24f, 0.28f, 0.3f));
            var hospitalFloor = Tile("HospitalFloor", "병원 바닥", true, 0.15f, new Color(0.31f, 0.36f, 0.36f));
            var wall = Tile("Wall", "실내 벽", false, 1.7f, new Color(0.1f, 0.16f, 0.19f));
            var accent = Tile("Accent", "낡은 카펫", true, 0.025f, new Color(0.35f, 0.23f, 0.23f));
            var apartment = Map("Apartment", "horror_apartment", "뉴스가 켜진 방", 12, 10, floor, wall, accent, out bool newApartment);
            var hospital = Map("Hospital", "horror_hospital", "병원 대기 구역", 18, 12, hospitalFloor, wall, accent, out bool newHospital);
            var transit = Map("Transit", "horror_transit", "환승 구역과 밤샘 합주실", 20, 14, floor, wall, accent, out bool newTransit);
            var approach = Map("Approach", "horror_approach", "증언이 가리킨 통로", 18, 10, hospitalFloor, wall, accent, out bool newApproach);

            var news = Event("News", "긴급 뉴스", Color.white, definition => definition.pages.Add(new EventPage
            {
                name = "처음 듣는 경고", trigger = EventTrigger.Autorun, runOnce = true, visible = false, blocksMovement = false,
                commands = Commands(
                    Say("", "며칠간의 밤샘 작업을 끝내고 푹 잤다. 막 눈을 뜬 순간, 텔레비전에서 같은 경고가 반복되고 있었다."),
                    Say("긴급 뉴스", "잠든 사람들이 깨어나지 않고 있습니다. 절대 잠들지 마십시오."),
                    Say("", "당장은 정신이 맑다. 하지만 밤이 오기 전에, 버틸 방법을 찾아야 한다."),
                    Objective("find_stimulants", "병원에서 각성제를 확보한다", 0))
            }));
            var apartmentExit = Event("ApartmentExit", "병원으로 향하는 문", new Color(0.6f, 0.5f, 0.7f), definition =>
            {
                definition.pages.Add(new EventPage { name = "병원으로 출발", blocksMovement = false, commands = Commands(
                    Say("", "가까운 병원까지 가 보자. 거리에는 깨어 있는 사람도 드물다."), Time(60), Self("A"), Transfer(hospital, hospital.spawnCell)) });
                definition.pages.Add(new EventPage { name = "다시 병원으로", conditions = Conditions(SelfCondition("A")), blocksMovement = false,
                    commands = Commands(Transfer(hospital, hospital.spawnCell)) });
            });
            var cabinet = Event("MedicineCabinet", "약품 보관함", new Color(0.72f, 0.64f, 0.34f), definition =>
            {
                definition.pages.Add(new EventPage { name = "각성제 확보", commands = Commands(
                    Say("", "남아 있는 각성제 세 개를 챙겼다. 몸에 부담이 가지만, 졸음을 잠시 밀어낼 수는 있다."),
                    new EventCommand { type = EventCommandType.AddItem, key = "stimulant", intValue = 3 }, Switch("meds_found"),
                    Objective("gather_accounts", "깨어 있는 사람들에게 현상에 관한 이야기를 듣는다", 1), Self("A")) });
                definition.pages.Add(new EventPage { name = "이미 챙긴 약품", conditions = Conditions(SelfCondition("A")), commands = Commands(Say("", "보관함에는 더 남은 약이 없다.")) });
            });
            var doctor = Event("Doctor", "깨어 있는 의료인", new Color(0.7f, 0.8f, 0.82f), definition =>
            {
                definition.pages.Add(new EventPage { name = "병원의 증언", commands = Commands(
                    Say("의료인", "연락이 끊기기 전, 여러 병원에서 같은 좌표 이야기가 나왔어요. 원인은 몰라도, 그쪽에서 무언가 시작된 것 같습니다."),
                    Clue("hospital_account", "병원에서 들은 좌표", "연락이 끊기기 전 여러 병원에서 같은 좌표가 언급되었다."), Self("A")) });
                definition.pages.Add(new EventPage { name = "증언을 들은 뒤", conditions = Conditions(SelfCondition("A")),
                    commands = Commands(Say("의료인", "다른 사람에게도 물어봐 주세요. 혼자 가진 이야기만으로는 알 수 있는 게 적으니까요.")) });
            });
            var minEvent = Event("Min", "민", min.color, definition =>
            {
                definition.pages.Add(new EventPage { name = "민과 동행", commands = Commands(
                    Say("민", "나도 여기서 계속 버틸 수는 없어. 같이 가자. 무서워도, 급하게 밀어붙여야 할 때는 내가 옆에 있을게."),
                    Join("min"), Unlock("aggressive"), Self("A")) });
                definition.pages.Add(new EventPage { name = "민과 이야기", conditions = Conditions(SelfCondition("A")), commands = Commands(Say("민", "악기는 못 다루는데, 소리 크게 내는 건 자신 있어.")) });
            });
            var hospitalExit = Event("HospitalExit", "환승 구역으로 향하는 통로", new Color(0.59f, 0.48f, 0.68f), definition =>
            {
                definition.pages.Add(new EventPage { name = "약품을 확보하고 출발", blocksMovement = false, commands = Commands(
                    Check(SwitchCondition("meds_found"), "need_medicine"), Say("", "다음 사람들을 찾아 이동한다. 길을 돌아가는 동안 날이 두 번 바뀌었다."),
                    Time(2880), Objective("follow_accounts", "환승 구역에서 두 번째 증언을 찾고 함께 밤을 견딘다", 2), Self("A"), Transfer(transit, transit.spawnCell), End(),
                    Label("need_medicine"), Say("", "다음 구간으로 가기 전에 병원에서 버틸 수단을 확보하자.")) });
                definition.pages.Add(new EventPage { name = "이미 지나간 통로", conditions = Conditions(SelfCondition("A")), blocksMovement = false,
                    commands = Commands(Transfer(transit, transit.spawnCell)) });
            });
            var hospitalMonster = Monster("HospitalMonster", hallwayEnemy, new Color(0.68f, 0.3f, 0.38f));
            var junEvent = Event("Jun", "준", jun.color, definition =>
            {
                definition.pages.Add(new EventPage { name = "다른 설명과 같은 좌표", commands = Commands(
                    Say("준", "나는 소리가 문제라고 생각해. 그런데 소리를 들었다는 사람들이 가리키는 곳이 모두 같았어. 너희가 들은 좌표와도 겹치네."),
                    Clue("transit_account", "환승 구역에서 들은 방향", "소리에 관한 다른 추측도 병원의 증언과 같은 곳을 가리킨다."),
                    Say("준", "같이 가도 될까? 저 형체들은 천천히 보면 움직임이 보여. 서두르기 전에 관찰해 보자."),
                    Join("jun"), Unlock("observe"), Self("A")) });
                definition.pages.Add(new EventPage { name = "준과 이야기", conditions = Conditions(SelfCondition("A")), commands = Commands(Say("준", "꼭 잘해야만 같이 할 수 있는 건 아니잖아. 오늘 밤도 그렇게 버텨 보자.")) });
            });
            var returnDoor = Event("TransitReturn", "병원으로 돌아가는 통로", new Color(0.59f, 0.48f, 0.68f), definition =>
                definition.pages.Add(new EventPage { blocksMovement = false, commands = Commands(Transfer(hospital, new Vector2Int(14, 9))) }));
            var night = Event("NightGathering", "함께 밤을 버틸 장소", new Color(0.55f, 0.7f, 0.34f), definition =>
            {
                definition.pages.Add(new EventPage { name = "밤샘 방법 고르기", commands = Commands(
                    Check(new StateCondition { kind = ConditionKind.Companion, key = "min" }, "check_jun"), Jump("choose"),
                    Label("check_jun"), Check(new StateCondition { kind = ConditionKind.Companion, key = "jun" }, "alone"),
                    Label("choose"), new EventCommand { type = EventCommandType.ShowChoices, text = "같이 웃으며 버틸 밤이 필요하다.", choices = new List<EventChoice>
                    {
                        new EventChoice { text = "합주실에서 밤새 연주한다", targetLabel = "band" },
                        new EventChoice { text = "텅 빈 놀이공원에서 롤러코스터를 탄다", targetLabel = "roller" },
                        new EventChoice { text = "먼저 다른 이야기를 더 듣는다", targetLabel = "leave" }
                    } },
                    Label("band"), Say("", "박자도 음정도 제각각. 누가 먼저 웃었는지 모를 정도로 다 같이 웃었다."),
                    new EventCommand { type = EventCommandType.StayAwake, nightActivity = band }, Switch("night_completed"), Self("A"), End(),
                    Label("roller"), Say("", "같은 코스를 돌고 또 돌았다. 목이 쉴 때까지 소리를 지르다 보니 아침빛이 들어왔다."),
                    new EventCommand { type = EventCommandType.StayAwake, nightActivity = roller }, Switch("night_completed"), Self("A"), End(),
                    Label("alone"), Say("", "누군가와 함께 버틸 수 있다면 좋겠다. 아직 깨어 있는 사람들을 찾아보자."), End(),
                    Label("leave"), Say("", "밤을 버틸 곳은 기억해 두었다.")) });
                definition.pages.Add(new EventPage { name = "좋은 기억으로 남은 밤", conditions = Conditions(SelfCondition("A")),
                    commands = Commands(Say("", "형편없는 연주든 끝없는 롤러코스터든, 같이 웃던 밤은 좋은 기억으로 남았다.")) });
            });
            var transitMonster = Monster("TransitMonster", hallwayEnemy, new Color(0.55f, 0.29f, 0.58f));
            var onward = Event("TransitExit", "증언이 가리킨 곳으로", new Color(0.59f, 0.48f, 0.68f), definition =>
            {
                definition.pages.Add(new EventPage { name = "단서와 밤을 지나 긴 여정으로", blocksMovement = false, commands = Commands(
                    Check(new StateCondition { kind = ConditionKind.Clue, key = "hospital_account" }, "need_clues"),
                    Check(new StateCondition { kind = ConditionKind.Clue, key = "transit_account" }, "need_clues"),
                    Check(SwitchCondition("night_completed"), "need_night"),
                    Say("", "설명은 달랐지만, 모두 같은 곳을 가리켰다. 중간 지점들을 지나며 길고 흐릿한 날들이 이어졌다."), Time(9 * 1440),
                    Say("", "이제 마지막 통로에는 혼자 서 있다. 함께 웃었던 밤의 기억은 남아 있다."),
                    new EventCommand { type = EventCommandType.ClearParty }, Objective("reach_destination", "증언이 가리킨 곳에 도달한다", 3), Self("A"),
                    Transfer(approach, approach.spawnCell), End(), Label("need_clues"), Say("", "병원과 환승 구역의 이야기를 함께 들어야 다음 방향을 찾을 수 있다."), End(),
                    Label("need_night"), Say("", "긴 이동에 앞서 동료들과 함께 오늘 밤을 견디자.")) });
                definition.pages.Add(new EventPage { name = "이미 정한 방향", conditions = Conditions(SelfCondition("A")), blocksMovement = false,
                    commands = Commands(Transfer(approach, approach.spawnCell)) });
            });
            var arrival = Event("SoloArrival", "혼자 남은 발걸음", Color.white, definition => definition.pages.Add(new EventPage
            { name = "마지막 구간", trigger = EventTrigger.Autorun, runOnce = true, visible = false, blocksMovement = false,
                commands = Commands(Say("", "몸은 전보다 약해졌다. 그래도 함께 익힌 행동과 들었던 이야기는 잊지 않았다.")) }));
            var approachMonster = Monster("ApproachMonster", lateEnemy, new Color(0.43f, 0.34f, 0.49f));
            var destination = Event("Destination", "증언이 가리킨 장소", new Color(0.74f, 0.73f, 0.56f), definition =>
            {
                definition.pages.Add(new EventPage { name = "목적지에 도달", blocksMovement = false, commands = Commands(
                    Say("", "마지막 통로를 지나며 또 하루가 바뀌었다."), Time(1440),
                    Say("", "여기가 모두의 증언이 가리켰던 곳이다. 이곳에 도착했다는 사실만큼은 확실하다."),
                    new EventCommand { type = EventCommandType.ReachDestination }, Objective("destination_reached", "증언이 가리킨 장소에 도달했다", 4), Self("A")) });
                definition.pages.Add(new EventPage { name = "도착한 뒤", conditions = Conditions(SelfCondition("A")), blocksMovement = false,
                    commands = Commands(Say("", "함께 버틴 밤들을 떠올리며 잠시 숨을 고른다.")) });
            });

            if (newApartment) Place(apartment, ("news", 1, 1, news), ("hospital_door", 9, 6, apartmentExit));
            if (newHospital)
            {
                for (int y = 2; y <= 9; y++) if (y != 6) hospital.SetTile(new Vector2Int(8, y), wall);
                Place(hospital, ("medicine", 5, 3, cabinet), ("doctor", 5, 7, doctor), ("min", 11, 3, minEvent),
                    ("hallway_monster", 13, 6, hospitalMonster), ("transit_door", 15, 9, hospitalExit));
            }
            if (newTransit) Place(transit, ("return_door", 2, 9, returnDoor), ("jun", 5, 5, junEvent), ("night", 11, 4, night),
                ("transit_monster", 13, 9, transitMonster), ("onward", 17, 10, onward));
            if (newApproach) Place(approach, ("arrival", 1, 1, arrival), ("fading_monster", 11, 4, approachMonster), ("destination", 15, 6, destination));
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.45f, 0.48f);
            var lightObject = new GameObject("Pale Daylight", typeof(Light)); var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.15f; light.color = new Color(0.79f, 0.87f, 0.9f); light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55, -35, 0);
            var root = new GameObject("No Sleep Horror Prototype"); var bootstrap = root.AddComponent<NoSleepBootstrap>();
            bootstrap.startingMap = apartment; bootstrap.maps = new List<MapDefinition> { apartment, hospital, transit, approach };
            bootstrap.playerMaterial = Material("Player", new Color(0.88f, 0.82f, 0.66f));
            bootstrap.survivalSettings = settings; bootstrap.stimulant = stimulant;
            bootstrap.companions = new List<CompanionDefinition> { min, jun }; bootstrap.showTitleScreen = true;
            EditorSceneManager.SaveScene(scene, PrototypeScene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(value => value.path == PrototypeScene))
            { scenes.Add(new EditorBuildSettingsScene(PrototypeScene, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            Selection.activeObject = root;
            EventIdentityUpgrader.UpgradeAll();
            DemoBuilder.ValidateContent();
            Debug.Log("NO_SLEEP_HORROR_CREATED: " + PrototypeScene);
        }

        static EventDefinition Monster(string file, EnemyDefinition enemy, Color color)
            => Event(file, enemy.displayName, color, definition =>
            {
                definition.pages.Add(new EventPage { name = "눈앞의 위협", trigger = EventTrigger.PlayerTouch,
                    commands = Commands(new EventCommand { type = EventCommandType.StartEncounter, enemy = enemy, key = "defeated" }) });
                definition.pages.Add(new EventPage { name = "물러난 형체", conditions = Conditions(SelfCondition("defeated")),
                    visible = false, blocksMovement = false, commands = Commands(End()) });
            });

        static EventDefinition Event(string file, string display, Color color, Action<EventDefinition> initialize)
            => Asset<EventDefinition>("Events/" + file, definition => { definition.displayName = display; definition.fallbackColor = color; initialize(definition); });
        static MapDefinition Map(string file, string id, string display, int width, int height,
            TileDefinition floor, TileDefinition wall, TileDefinition accent, out bool created)
        {
            string path = Data + "Maps/" + file + ".asset";
            created = AssetDatabase.LoadMainAssetAtPath(path) == null;
            return Asset<MapDefinition>("Maps/" + file, map =>
            {
                map.mapId = id; map.displayName = display; map.palette = new List<TileDefinition> { floor, wall, accent };
                map.Resize(width, height); map.Fill(floor); map.spawnCell = new Vector2Int(2, 3);
                for (int x = 0; x < width; x++) { map.SetTile(new Vector2Int(x, 0), wall); map.SetTile(new Vector2Int(x, height - 1), wall); }
                for (int y = 0; y < height; y++) { map.SetTile(new Vector2Int(0, y), wall); map.SetTile(new Vector2Int(width - 1, y), wall); }
                for (int x = 3; x <= 5; x++) map.SetTile(new Vector2Int(x, 2), accent, TileLayer.Decoration);
            });
        }
        static void Place(MapDefinition map, params (string id, int x, int y, EventDefinition definition)[] placements)
        {
            foreach (var placement in placements)
                map.events.Add(new MapEventPlacement { id = placement.id, cell = new Vector2Int(placement.x, placement.y), definition = placement.definition });
            EditorUtility.SetDirty(map);
        }
        static TileDefinition Tile(string file, string display, bool walkable, float height, Color color)
            => Asset<TileDefinition>("Tiles/" + file, tile =>
            { tile.displayName = display; tile.walkable = walkable; tile.height = height; tile.fallbackColor = color; tile.material = Material(file, color); });
        static Material Material(string file, Color color)
        {
            string path = Data + "Materials/" + file + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Unexpected asset type: " + path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader) { color = color }; AssetDatabase.CreateAsset(material, path); return material;
        }
        static T Asset<T>(string relativePath, Action<T> initialize) where T : ScriptableObject
        {
            string path = Data + relativePath + ".asset";
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
        static List<EventCommand> Commands(params EventCommand[] values) => new List<EventCommand>(values);
        static List<StateCondition> Conditions(params StateCondition[] values) => new List<StateCondition>(values);
        static StateCondition SelfCondition(string key) => new StateCondition { kind = ConditionKind.SelfSwitch, key = key };
        static StateCondition SwitchCondition(string key) => new StateCondition { kind = ConditionKind.Switch, key = key };
        static EventCommand Say(string speaker, string text) => new EventCommand { type = EventCommandType.ShowDialogue, speaker = speaker, text = text };
        static EventCommand Objective(string id, string title, int stage) => new EventCommand { type = EventCommandType.SetObjective, key = id, text = title, intValue = stage };
        static EventCommand Clue(string id, string title, string text) => new EventCommand { type = EventCommandType.RecordClue, key = id, speaker = title, text = text };
        static EventCommand Self(string key) => new EventCommand { type = EventCommandType.SetSelfSwitch, key = key, boolValue = true };
        static EventCommand Switch(string key) => new EventCommand { type = EventCommandType.SetSwitch, key = key, boolValue = true };
        static EventCommand Join(string id) => new EventCommand { type = EventCommandType.JoinCompanion, key = id };
        static EventCommand Unlock(string id) => new EventCommand { type = EventCommandType.UnlockAction, key = id };
        static EventCommand Time(int minutes) => new EventCommand { type = EventCommandType.AdvanceTime, intValue = minutes };
        static EventCommand Transfer(MapDefinition map, Vector2Int cell) => new EventCommand { type = EventCommandType.TransferPlayer, map = map, cell = cell };
        static EventCommand Check(StateCondition condition, string target) => new EventCommand { type = EventCommandType.IfCondition, condition = condition, targetLabel = target };
        static EventCommand Label(string label) => new EventCommand { type = EventCommandType.Label, label = label };
        static EventCommand Jump(string target) => new EventCommand { type = EventCommandType.Jump, targetLabel = target };
        static EventCommand End() => new EventCommand { type = EventCommandType.End };
    }
}
