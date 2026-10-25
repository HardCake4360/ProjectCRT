using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectCRT.Cutscenes;
using ProjectCRT.Prologue;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Prologue.Editor
{
    public static class PrologueBackgroundBuilder
    {
        const string Settings="Assets/Settings/Prologue";
        const string Sources="Assets/ThirdParty/Prologue";
        const string Furniture=Sources+"/FurnitureFree/Source/psx-furniture-free/fbx/";
        const string House=Sources+"/McPatoInterior/Source/DoriHome.fbx";
        const string Bar=Sources+"/McPatoBar/Source/bar_end.fbx";
        public static readonly string[] Scenes={"Assets/Scenes/ProjectCRT_SC01_TaxiArrival.unity","Assets/Scenes/ProjectCRT_SC02_SanRoom.unity","Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity"};
        static readonly Dictionary<string,Material> materials=new();
        static TMP_FontAsset font;
        static Transform set;
        static CutsceneStage stage;
        static PrologueBackgroundPreview preview;
        static readonly List<string> report=new();

        public static void BatchBuild()
        {
            try { var host=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(host,"Assets/Scenes/__PrologueAuthoringHost.unity");Build();File.WriteAllText("Logs/PrologueAssets/batch-status.txt","PASS");EditorApplication.Exit(0); }
            catch(Exception e){Directory.CreateDirectory("Logs/PrologueAssets");File.WriteAllText("Logs/PrologueAssets/batch-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }

        [MenuItem("Tools/ProjectCRT/Prologue/Build Background Scenes")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(Scenes.Any(File.Exists))throw new InvalidOperationException("A prologue scene already exists. Existing authored scenes will not be overwritten.");
            Directory.CreateDirectory(Settings);Directory.CreateDirectory("Logs/PrologueAssets/Previews");AssetDatabase.Refresh();
            materials.Clear();report.Clear();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/resources/font/NeoDunggeunmoPro-Regular 1 SDF.asset");
            Scene original=SceneManager.GetActiveScene();bool originalDirty=original.isDirty;
            BuildTaxi();BuildRoom();BuildBar();
            var build=EditorBuildSettings.scenes.ToList();foreach(var path in Scenes)if(!build.Any(s=>s.path==path))build.Add(new EditorBuildSettingsScene(path,true));
            EditorBuildSettings.scenes=build.ToArray();AssetDatabase.SaveAssets();
            File.WriteAllLines("Logs/PrologueAssets/layout-report.txt",report);
            if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
            if(!originalDirty && !SceneManager.GetActiveScene().isDirty)EditorSceneManager.OpenScene(Scenes[2],OpenSceneMode.Single);
        }
        static Scene NewScene(int index,string label)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            SetupStage(label);return scene;
        }
        static void SetupStage(string label)
        {
            var root=new GameObject(label+" - Background Stage");stage=root.AddComponent<CutsceneStage>();stage.stageId=label;
            preview=root.AddComponent<PrologueBackgroundPreview>();preview.sceneLabel=label+" BACKGROUND LAYOUT";preview.stage=stage;
            var camera=new GameObject("Background Inspection Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.035f;camera.farClipPlane=350;camera.fieldOfView=55;
            camera.allowMSAA=false;camera.GetUniversalAdditionalCameraData().SetRenderer(1);camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            camera.gameObject.AddComponent<AudioListener>();camera.gameObject.AddComponent<OutdoorStreetViewer>();stage.output=camera;
            var look=new GameObject("Prologue Retro Look").AddComponent<BarRetroLook>();look.saturation=.8f;look.horizontalBlur=.12f;look.contrast=1.01f;
            preview.preservedCast=Group("Original cast - staging references (performance pending)");preview.preservedCast.gameObject.SetActive(false);
            set=Group("Environment");
        }
        static void Atmosphere(bool indoor)
        {
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=indoor?new Color(.24f,.21f,.19f):new Color(.2f,.27f,.35f);
            RenderSettings.ambientEquatorColor=indoor?new Color(.12f,.12f,.14f):new Color(.13f,.18f,.25f);
            RenderSettings.ambientGroundColor=new Color(.04f,.045f,.06f);RenderSettings.reflectionIntensity=.15f;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=indoor?.007f:.013f;
            RenderSettings.fogColor=indoor?new Color(.12f,.12f,.145f):new Color(.17f,.23f,.3f);
            stage.output.backgroundColor=RenderSettings.fogColor;
            var light=Lamp("World - overcast evening",new Vector3(0,8,0),new Color(.6f,.72f,.88f),indoor?.28f:.7f,LightType.Directional,100);
            light.transform.rotation=Quaternion.Euler(32,-35,0);RenderSettings.sun=light;
        }
        static void BuildTaxi()
        {
            var scene=NewScene(0,"SC-01 Taxi Arrival");Atmosphere(false);
            set=Group("SET A - Elevated city approach");
            Road(Vector3.zero,4,115,true);
            var taxi=Taxi("Hero taxi - elevated road",new Vector3(0,4.05f,0),0);
            Skyline(Vector3.zero,4);
            Box("City ground below elevated road",new Vector3(0,-3,25),new Vector3(180,.5f,180),Surface("City ground",new Color(.075f,.09f,.11f)));
            Sign("LOS SIMULA",new Vector3(-5.6f,8.2f,23),new Vector2(4.5f,1),new Color(.45f,.7f,.76f),0);
            for(int z=-45;z<=45;z+=18){StreetLamp(new Vector3(-5,4,z),true);StreetLamp(new Vector3(5,4,z),false);}
            // A static wet-road placement pass; motion, weather timing and performance come later.
            for(int i=0;i<8;i++)Box("Wet road sheen",new Vector3((i%2==0?-2.5f:2.8f),4.012f,-40+i*11),new Vector3(1.2f,.009f,4),Surface("Wet asphalt sheen",new Color(.16f,.21f,.24f),.72f));
            set=Group("SET B - Stationary interior filming rig");var cabin=Taxi("Interior taxi - same hero model",new Vector3(90,4.05f,0),0);
            Road(new Vector3(90,0,0),4,32,false);Skyline(new Vector3(90,0,0),3);
            var cabinBounds=Bounds(cabin);float cabinFloor=4.05f;
            Box("CD player - dashboard insert",new Vector3(89.92f,cabinFloor+.95f,.5f),new Vector3(.24f,.1f,.08f),Surface("Dark plastic",new Color(.055f,.065f,.07f)));
            Box("CD slot",new Vector3(89.92f,cabinFloor+.97f,.45f),new Vector3(.17f,.007f,.006f),Surface("Slot black",Color.black));
            Box("CD case",new Vector3(90.35f,cabinFloor+.91f,.52f),new Vector3(.14f,.012f,.13f),Surface("CD case",new Color(.24f,.29f,.31f)));
            Cylinder("Disc",new Vector3(90.35f,cabinFloor+.92f,.5f),new Vector3(.115f,.001f,.115f),Surface("Disc silver",new Color(.65f,.67f,.61f),.7f));
            Marker("driver_seat",new Vector3(89.58f,cabinFloor+.6f,.07f));Marker("san_rear_right",new Vector3(90.42f,cabinFloor+.6f,-1.0f));
            Lamp("Cabin dashboard soft fill",new Vector3(90,cabinFloor+1.08f,.65f),new Color(.72f,.78f,.67f),.15f,LightType.Point,3);
            set=Group("SET C - Residential arrival");Road(new Vector3(180,0,0),0,38,false);
            Taxi("Arrival taxi",new Vector3(179,0,-1),180);
            Box("Pavement",new Vector3(186,.12f,2),new Vector3(4,.24f,20),Surface("Pavement",new Color(.25f,.27f,.28f)));
            Box("Residential ground",new Vector3(185,-.5f,0),new Vector3(70,.5f,85),Surface("Pavement",new Color(.25f,.27f,.28f)));
            var building=Model("Assets/ThirdParty/McPatoBuildings/DoriHome2.fbx","Existing McPato residential frontage",new Vector3(203,.24f,10),180,48);
            Box("Apartment entrance wing",new Vector3(189,4,2),new Vector3(2,8,8),Surface("Apartment concrete",new Color(.31f,.33f,.34f)));
            for(int floor=0;floor<3;floor++)for(int window=0;window<3;window++)Box("Apartment window",new Vector3(187.98f,1.3f+floor*2.3f,-.4f+window*2.5f),new Vector3(.025f,1.15f,1.25f),Surface("Dusk windows",new Color(.13f,.2f,.24f),.2f,.12f));
            Sign("APARTMENTS  /  07",new Vector3(187.9f,2.9f,2),new Vector2(2.2f,.43f),new Color(.69f,.67f,.49f),90);
            Door("Residential entrance",new Vector3(187.7f,.24f,2),90,1.3f,2.5f,Surface("Residential door",new Color(.16f,.2f,.23f)));
            StreetLamp(new Vector3(185,0,-3),true);
            Part(House,"bag","San travel bag",new Vector3(181.1f,.05f,-2),0,.48f);
            Actor("san","산","Assets/resources/fbx/San/San.fbx",new Vector3(181.5f,0,-1.5f),90);
            Shot("01 City and elevated taxi",new Vector3(10,10,-17),new Vector3(0,5,6),53);
            Shot("02 Behind driver - interior",new Vector3(90.0f,cabinFloor+1.27f,-.55f),new Vector3(89.85f,cabinFloor+1.04f,.7f),65);
            Shot("03 San rear-right profile",new Vector3(89.75f,cabinFloor+1.15f,-.95f),new Vector3(90.45f,cabinFloor+1.09f,-1.0f),50);
            Shot("04 Dashboard and CD",new Vector3(90.15f,cabinFloor+1.25f,.2f),new Vector3(89.95f,cabinFloor+.96f,.65f),58);
            Shot("05 San looking outside",new Vector3(89.72f,cabinFloor+1.16f,-1.35f),new Vector3(90.42f,cabinFloor+1.08f,-.95f),52);
            Shot("06 Taxi entering city",new Vector3(-9,9,20),new Vector3(0,5,3),50);
            Shot("07 Residential arrival",new Vector3(173,3.5f,-9),new Vector3(184,2.1f,2),58);
            Shot("08 From taxi to entrance",new Vector3(179,1.8f,-7),new Vector3(187.5f,1.7f,2),55);
            Rain(new Vector3(0,13,0),new Vector3(30,1,70));Rain(new Vector3(85,11,0),new Vector3(8,1,25));Rain(new Vector3(95,11,0),new Vector3(8,1,25));Rain(new Vector3(180,10,0),new Vector3(25,1,28));
            Finish(scene,0,new[]{0,1,3,6});
        }
        static void BuildRoom()
        {
            var scene=NewScene(1,"SC-02 San Room");Atmosphere(true);
            set=Group("Room shell - compact first apartment");
            Material plaster=Surface("Room plaster",new Color(.4f,.42f,.43f));Material wood=Textured("Floor worn wood",Texture("McPatoInterior","woodfloor"),Color.white);
            Box("Floor",new Vector3(0,-.12f,0),new Vector3(7,.24f,6),wood);
            Box("North wall",new Vector3(0,1.5f,3.1f),new Vector3(7.2f,3,.18f),plaster);
            Box("West wall",new Vector3(-3.6f,1.5f,0),new Vector3(.18f,3,6.2f),plaster);
            Box("East wall - lower",new Vector3(3.6f,.55f,0),new Vector3(.18f,1.1f,6.2f),plaster);
            Box("East wall - header",new Vector3(3.6f,2.8f,0),new Vector3(.18f,.4f,6.2f),plaster);
            Box("East wall - corner A",new Vector3(3.6f,1.85f,-2.2f),new Vector3(.18f,1.5f,1.8f),plaster);
            Box("East wall - corner B",new Vector3(3.6f,1.85f,2.2f),new Vector3(.18f,1.5f,1.8f),plaster);
            Box("Window - blue evening",new Vector3(3.59f,1.85f,0),new Vector3(.025f,1.5f,2.6f),Surface("Blue window",new Color(.16f,.28f,.38f),.2f,.2f));
            for(float z=-1.3f;z<=1.3f;z+=1.3f)Box("Window frame",new Vector3(3.5f,1.85f,z),new Vector3(.1f,1.65f,.055f),Surface("Window frame",new Color(.12f,.14f,.17f)));
            Box("South wall - left",new Vector3(-1.25f,1.5f,-3.1f),new Vector3(4.7f,3,.18f),plaster);
            Box("South wall - right",new Vector3(3.1f,1.5f,-3.1f),new Vector3(.9f,3,.18f),plaster);
            Box("South door header",new Vector3(1.9f,2.75f,-3.1f),new Vector3(1.6f,.5f,.18f),plaster);
            Door("Apartment exit - independent hinge",new Vector3(1.9f,0,-3.08f),0,1.5f,2.5f,Surface("Apartment door",new Color(.24f,.19f,.15f)));
            set=Group("Furniture and moving-in props");
            Furn("sofa_red","Sofa - rest scene",new Vector3(-1.8f,0,2.4f),180);
            Furn("rug_braided","Small woven rug",new Vector3(-1.4f,.005f,1),0);
            Furn("coffee_table_oak","Coffee table",new Vector3(-1.6f,0,.7f),0);
            Furn("floor_lamp","Sofa reading lamp",new Vector3(-3.05f,0,2.1f),0);
            Furn("bed_single_oak","Bed",new Vector3(2.7f,0,1.8f),180);
            Furn("wardrobe_oak","Wardrobe",new Vector3(-3.05f,0,-1.7f),90);
            Furn("tv","Existing-era CRT",new Vector3(-3.0f,.62f,.5f),90);
            Furn("nightstand_oak","TV stand",new Vector3(-3.0f,0,.5f),90);
            var clock=Furn("wall_clock","Wall clock - 19 35",new Vector3(-.7f,1.9f,2.985f),180);
            var hour=clock.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.Contains("hour"));
            var minute=clock.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.Contains("minute"));
            if(hour!=null)hour.localRotation=Quaternion.Euler(0,0,107.5f);if(minute!=null)minute.localRotation=Quaternion.Euler(0,0,210);
            for(int i=0;i<5;i++){var box=Furn(i==2?"box_open":"box","Moving box "+(i+1),new Vector3(-1.25f+(i%3)*.85f,0,-1.6f+(i/3)*.75f),i*17);box.transform.localScale*=i==4?1.3f:1.05f;}
            Part(House,"bag","Travel bag",new Vector3(2.4f,0,-2.15f),30,.5f);
            Furn("book_stack","Unpacked books",new Vector3(-1.2f,.4f,-1.6f),20);
            Furn("mug","Mug",new Vector3(-1.7f,.45f,.7f),0);
            Lamp("Warm room ceiling",new Vector3(-.4f,2.65f,0),new Color(1,.77f,.52f),4.2f,LightType.Point,7);
            Lamp("Reading light",new Vector3(-2.95f,1.43f,2.1f),new Color(1,.65f,.35f),1.1f,LightType.Point,3);
            Lamp("Cold window spill",new Vector3(3.1f,2.15f,0),new Color(.46f,.66f,.94f),2.0f,LightType.Point,5);
            Actor("san","산","Assets/resources/fbx/San/San.fbx",new Vector3(.3f,0,-.3f),0);
            Marker("san_sofa_seat",new Vector3(-1.8f,.38f,2.15f));Marker("moving_box_interaction",new Vector3(.5f,0,-.5f));Marker("room_exit",new Vector3(1.9f,0,-2.5f));
            Shot("01 Fixed gameplay - moving in",new Vector3(.5f,2.55f,-2.85f),new Vector3(-.35f,.7f,.7f),64);
            Shot("02 Sofa rest",new Vector3(.1f,1.65f,.05f),new Vector3(-1.7f,.98f,2.2f),50);
            Shot("03 San and clock",new Vector3(.8f,1.65f,.8f),new Vector3(-1.4f,1.35f,2.7f),56);
            Finish(scene,1,new[]{0,1,2});
        }
        static void BuildBar()
        {
            if(!AssetDatabase.CopyAsset("Assets/Scenes/Outdoor_BarStreetTest.unity",Scenes[2]))throw new Exception("Could not copy existing Shangrila scene.");
            var scene=EditorSceneManager.OpenScene(Scenes[2],OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var retained=new List<GameObject>();
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.name.StartsWith("McPato")||root.name.StartsWith("Moonlight")||root.name.StartsWith("Outdoor Retro")||root.name.StartsWith("Outdoor Lighting")||root.name.StartsWith("Shangrila - user")||root.name.StartsWith("Shangrila - placement"))retained.Add(root);
                else root.SetActive(false);
            }
            SetupStage("SC-03 Shangrila Reunion");Atmosphere(true);
            var preserved=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Shangrila - Bar NPCs");
            if(preserved!=null)
            {
                foreach(var npc in preserved.GetComponentsInChildren<NPC_script>(true))
                {
                    npc.transform.SetParent(preview.preservedCast,true);
                    var actor=npc.GetComponent<CutsceneActor>()??npc.gameObject.AddComponent<CutsceneActor>();
                    bool rebecca=npc.name.ToLowerInvariant().Contains("reb");actor.id=rebecca?"rebecca":"jeremy";actor.displayName=rebecca?"레베카":"제레미";stage.actors.Add(actor);
                }
            }
            Actor("san","산","Assets/resources/fbx/San/San.fbx",new Vector3(-3.7f,.2f,8.7f),0);
            set=Group("Cutscene dressing - upper shell and ceiling");
            var ceiling=Surface("Bar dark ceiling",new Color(.075f,.065f,.06f));
            Box("Main hall ceiling",new Vector3(-3,2.78f,9.85f),new Vector3(11.5f,.08f,6.5f),ceiling);
            Box("Private hall ceiling",new Vector3(3.45f,2.78f,10.5f),new Vector3(1.85f,.08f,7.8f),ceiling);
            Box("Room 02 ceiling",new Vector3(6.8f,2.78f,12.5f),new Vector3(4.8f,.08f,3.7f),ceiling);
            var upper=Surface("Bar upper walls",new Color(.2f,.17f,.15f));
            Box("North wall upper infill",new Vector3(-3,2.33f,12.95f),new Vector3(11.5f,.9f,.09f),upper);
            Box("East wall upper infill",new Vector3(2.45f,2.33f,10.1f),new Vector3(.09f,.9f,5.8f),upper);
            var floor=Textured("Bar floor timber",Texture("McPatoInterior","woodfloor"),new Color(.38f,.31f,.25f));
            floor.mainTextureScale=new Vector2(5,3);
            Box("Main hall floor finish",new Vector3(-3.0f,.207f,9.85f),new Vector3(10.8f,.012f,6.0f),floor);
            Box("Room 02 floor finish",new Vector3(6.7f,.207f,12.5f),new Vector3(4.1f,.012f,3.05f),floor);
            set=Group("Main hall - camera and acting clearance");
            // Work within the blockout's existing public room; preserve the source FBX and all its walls.
            var counterMat=Textured("Bar worn wood",Texture("McPatoInterior","wood_table_worn_diff_1k"),new Color(.65f,.58f,.48f));
            Box("Counter body",new Vector3(-3.45f,.65f,11.2f),new Vector3(4.5f,.9f,.62f),counterMat);
            Box("Counter top",new Vector3(-3.45f,1.14f,11.15f),new Vector3(4.7f,.09f,.88f),counterMat);
            for(int i=0;i<3;i++)
            {
                Vector3 p=new Vector3(-5.0f+i*1.45f,.2f,9.75f);
                Cylinder("Bar stool "+(i+1)+" seat",p+Vector3.up*.64f,new Vector3(.43f,.055f,.43f),counterMat);
                Cylinder("Bar stool pedestal",p+Vector3.up*.32f,new Vector3(.07f,.32f,.07f),Surface("Black stool metal",new Color(.055f,.06f,.065f),.3f));
                Cylinder("Bar stool foot",p+Vector3.up*.035f,new Vector3(.34f,.035f,.34f),Surface("Black stool metal",new Color(.055f,.06f,.065f),.3f));
            }
            for(int level=0;level<2;level++)
            {
                float y=1.65f+level*.5f;Box("Backbar shelf "+level,new Vector3(-3.45f,y,12.55f),new Vector3(4.4f,.07f,.26f),counterMat);
                for(int i=0;i<8;i++)Part(Bar,"bottle.00"+(5+i%5),"Shelf bottle "+level+"-"+i,new Vector3(-5.3f+i*.53f,y+.04f,12.55f),i*31,.26f);
            }
            for(int i=0;i<3;i++)Part(Bar,"Circle.030","Counter glass "+i,new Vector3(-4.4f+i*.55f,1.19f,11),0,.16f);
            Part(Bar,"bottle.005","Rebecca bottle - pickup",new Vector3(-1.7f,1.19f,11.25f),12,.28f);
            Furn("plate","Small snack plate",new Vector3(-3.5f,1.19f,10.98f),0);
            // Same glass, authored later-time liquid level; staged off until time-passage cue is made.
            var later=Group("After time passage - optional prop state");later.gameObject.SetActive(false);
            var drink=Cylinder("Partly emptied drink",new Vector3(-4.4f,1.22f,11),new Vector3(.052f,.018f,.052f),Surface("Amber drink",new Color(.43f,.22f,.065f),.4f));drink.transform.SetParent(later,true);
            Sign("SHANGRILA",new Vector3(-3.5f,2.18f,12.72f),new Vector2(3,.38f),new Color(.74f,.48f,.32f),180);
            Furn("table_pine","Main hall table",new Vector3(.4f,.2f,8.1f),0);
            Furn("chair_ladder_pine","Guest chair A",new Vector3(-.45f,.2f,8.1f),90);
            Furn("chair_ladder_pine","Guest chair B",new Vector3(1.2f,.2f,8.1f),270);
            Furn("mug","Table glass",new Vector3(.4f,.95f,8.1f),0);
            Lamp("Counter - warm practical",new Vector3(-3.5f,2.35f,10.3f),new Color(1,.68f,.38f),4,LightType.Point,7);
            Lamp("Backbar - soft amber",new Vector3(-3.8f,2.05f,12.25f),new Color(1,.56f,.27f),2,LightType.Point,4);
            set=Group("Private hall and Room 02 - door and furniture");
            var wallMat=Surface("Private hall muted plaster",new Color(.25f,.25f,.27f));
            // Camera marks and partitions stay inside the existing shell; all additions are separate scene objects.
            Door("Room 02 - independent open hinged door",new Vector3(4.35f,.2f,12.2f),90,.85f,2.1f,Surface("Private room door",new Color(.16f,.12f,.105f)),85);
            Sign("02",new Vector3(4.13f,2,12.95f),new Vector2(.32f,.32f),new Color(.79f,.72f,.49f),90);
            Furn("sofa_red","Room 02 sofa",new Vector3(8.0f,.2f,13.2f),90);
            Furn("coffee_table_oak","Room 02 table",new Vector3(6.9f,.2f,12.25f),0);
            Lamp("Private hall - low warm pool",new Vector3(3.4f,2.35f,11.7f),new Color(.91f,.61f,.35f),2.4f,LightType.Point,5);
            Lamp("Room 02 - quiet cold fill",new Vector3(6.0f,2.2f,13.2f),new Color(.52f,.65f,.78f),1.8f,LightType.Point,4);
            Marker("san_counter_seat",new Vector3(-4.5f,.88f,9.75f));Marker("rebecca_counter",new Vector3(-3.8f,.2f,11.95f));Marker("jeremy_counter",new Vector3(-1.9f,.2f,11.95f));
            Marker("private_hall_entry",new Vector3(3.45f,.2f,7.7f));Marker("room02_threshold",new Vector3(3.6f,.2f,12.2f));Marker("jeremy_discovery_floor",new Vector3(5.7f,.2f,12.45f));
            Shot("01 Entrance to main hall",new Vector3(-5.44f,1.65f,7.1f),new Vector3(-2.7f,1.1f,11.35f),66);
            Shot("02 Counter greeting",new Vector3(-4.9f,1.65f,8.4f),new Vector3(-2.3f,1.45f,11.7f),53);
            Shot("03 San and Jeremy",new Vector3(-.6f,1.6f,9.25f),new Vector3(-3.5f,1.4f,10.8f),57);
            Shot("04 Rebecca emerges",new Vector3(-4.8f,1.65f,9.15f),new Vector3(-3.8f,1.5f,12),43);
            Shot("05 Reunion closeup coverage",new Vector3(-5.5f,1.6f,10.3f),new Vector3(-3.8f,1.45f,11.95f),40);
            Shot("06 Three-person counter wide",new Vector3(-5.7f,2,7.8f),new Vector3(-3.25f,1.35f,11.3f),62);
            Shot("07 Time passage counter",new Vector3(-.7f,1.65f,9.25f),new Vector3(-4.0f,1.35f,10.8f),55);
            Shot("08 Rebecca hears the sound",new Vector3(-4.7f,1.65f,10.25f),new Vector3(-3.8f,1.5f,11.95f),38);
            Shot("09 Leaving the counter",new Vector3(-5.4f,1.8f,7.7f),new Vector3(.5f,1.2f,9.6f),62);
            Shot("10 Private hall tracking entry",new Vector3(3.35f,1.65f,8.1f),new Vector3(3.45f,1.3f,12.7f),55);
            Shot("11 Room 02 over shoulder",new Vector3(3.7f,1.65f,12.2f),new Vector3(5.7f,.55f,12.45f),58);
            Shot("12 Rebecca discovery closeup",new Vector3(5.4f,1.65f,12.5f),new Vector3(4.1f,1.5f,12.2f),36);
            set=Group("Street rain outside Shangrila");Rain(new Vector3(0,12,-4),new Vector3(32,1,16));
            Finish(scene,2,new[]{0,5,9,10});
        }
        static void Finish(Scene scene,int index,int[] captures)
        {
            preview.SelectShot(0);stage.output.scene=scene;
            foreach(var camera in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))
                if(camera!=stage.output)camera.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,Scenes[index]);AssetDatabase.SaveAssets();
            int missing=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if(missing>0)throw new Exception("Missing scripts in "+Scenes[index]);
            foreach(int shot in captures){preview.SelectShot(shot);Capture(scene,$"SC0{index+1}-{shot+1:00}",false);}
            if(index==2)Capture(scene,"SC03-floorplan",true);
            preview.SelectShot(0);EditorSceneManager.SaveScene(scene);
            report.Add($"{Scenes[index]} | cameras={stage.shots.Count} | original actors={stage.actors.Count} | renderers="+scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Renderer>(true).Length)+" | missing scripts="+missing);
            EditorSceneManager.CloseScene(scene,true);
        }
        static void Capture(Scene scene,string name,bool top)
        {
            var camera=stage.output;var old=CutsceneCameraState.From(camera);bool ortho=camera.orthographic;
            if(top){camera.transform.position=new Vector3(.2f,2.6f,10.3f);camera.transform.rotation=Quaternion.Euler(90,0,0);camera.orthographic=true;camera.orthographicSize=7;}
            var texture=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);texture.Create();var active=RenderTexture.active;
            try
            {
                camera.scene=scene;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});RenderTexture.active=texture;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                File.WriteAllBytes("Logs/PrologueAssets/Previews/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{RenderTexture.active=active;texture.Release();UnityEngine.Object.DestroyImmediate(texture);camera.orthographic=ortho;old.Apply(camera);}
        }
        static Transform Group(string name)
        {
            return new GameObject(name).transform;
        }
        static GameObject Model(string path,string name,Vector3 position,float yaw,float width=0,bool preserveMaterials=false)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new Exception("Missing model: "+path);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,SceneManager.GetActiveScene());model.name=name;model.transform.SetParent(set,true);model.transform.rotation=Quaternion.Euler(0,yaw,0);
            if(width>0){var bounds=Bounds(model);model.transform.localScale*=width/Mathf.Max(bounds.size.x,bounds.size.z);}
            var b=Bounds(model);model.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);
            if(!preserveMaterials)ConvertMaterials(model,path);
            foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            return model;
        }
        static GameObject Furn(string id,string name,Vector3 position,float yaw)
        {
            return Model(Furniture+id+".fbx",name,position,yaw);
        }
        static GameObject Part(string path,string id,string name,Vector3 position,float yaw,float height)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new Exception(path);
            var original=asset.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name==id);
            if(original==null)throw new Exception("Missing part "+id+" in "+path);
            var go=UnityEngine.Object.Instantiate(original.gameObject);go.name=name;go.transform.SetParent(set,false);
            go.transform.rotation=Quaternion.Euler(0,yaw,0)*original.transform.rotation;go.transform.localScale=original.transform.lossyScale;
            var b=Bounds(go);if(height>0)go.transform.localScale*=height/Mathf.Max(.001f,b.size.y);
            b=Bounds(go);go.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);ConvertMaterials(go,path);return go;
        }
        static void ConvertMaterials(GameObject model,string path)
        {
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var source=slots[i];if(source==null)continue;
                    string key=path+"|"+source.name;
                    if(!materials.TryGetValue(key,out var mat))
                    {
                        var texture=source.mainTexture as Texture2D;string raw=source.name.Replace(".EMIT","");
                        if(texture==null&&path.Contains("BricchiCar")&&!source.name.ToLowerInvariant().Contains("window"))texture=Texture("BricchiCar","White Car Texture");
                        if(texture==null)texture=FindTextureForMaterial(path,raw);
                        Color tint=texture!=null?Color.white:(source.HasProperty("_Color")?source.GetColor("_Color"):new Color(.55f,.55f,.55f));
                        if(path.Contains("BricchiCar")&&!source.name.ToLowerInvariant().Contains("window"))tint=new Color(.94f,.8f,.52f);
                        mat=Textured("Imported "+Path.GetFileNameWithoutExtension(path)+" "+source.name,texture,tint);
                        if(source.name.ToLowerInvariant().Contains("window"))
                        {
                            mat.SetColor("_BaseColor",new Color(.48f,.62f,.65f,.17f));mat.SetFloat("_Surface",1);mat.SetFloat("_ZWrite",0);
                            mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;
                        }
                        materials[key]=mat;
                    }
                    slots[i]=mat;
                }
                renderer.sharedMaterials=slots;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
        static Texture2D FindTextureForMaterial(string model,string material)
        {
            string directory=model.Contains("/Prologue/")?model.Substring(0,model.IndexOf("/Source/",StringComparison.Ordinal)):Path.GetDirectoryName(model);
            if(!Directory.Exists(directory))return null;
            string normalize(string value)=>new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            var target=normalize(material);
            var file=Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Where(p=>new[]{".png",".jpg",".jpeg",".tga"}.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .OrderBy(p=>p).FirstOrDefault(p=>normalize(Path.GetFileNameWithoutExtension(p))==target);
            if(file==null&&material=="planta")file=Directory.GetFiles(directory,"plantas.png",SearchOption.AllDirectories).FirstOrDefault();
            return file!=null?AssetDatabase.LoadAssetAtPath<Texture2D>(file.Replace('\\','/')):null;
        }
        static Texture2D Texture(string pack,string id)
        {
            string folder=Sources+"/"+pack;
            var file=Directory.GetFiles(folder,"*",SearchOption.AllDirectories).FirstOrDefault(p=>Path.GetFileNameWithoutExtension(p).Equals(id,StringComparison.OrdinalIgnoreCase)&&new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(p).ToLowerInvariant()));
            return file==null?null:AssetDatabase.LoadAssetAtPath<Texture2D>(file.Replace('\\','/'));
        }
        static Material Surface(string name,Color color,float smoothness=0,float emission=0)
        {
            string key="surface|"+name;if(materials.TryGetValue(key,out var existing))return existing;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smoothness);material.SetFloat("_Metallic",0);
            if(emission>0){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*emission);}
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Settings+"/"+SafeName(name)+".mat"));materials[key]=material;return material;
        }
        static Material Textured(string name,Texture2D texture,Color tint)
        {
            string key="textured|"+name;if(materials.TryGetValue(key,out var existing))return existing;
            var mat=Surface(name,tint,.06f);if(texture!=null)mat.SetTexture("_BaseMap",texture);materials[key]=mat;return mat;
        }
        static string SafeName(string name)=>string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
        static Bounds Bounds(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return new Bounds(go.transform.position,Vector3.one);
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return bounds;
        }
        static GameObject Box(string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(set,true);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static GameObject Cylinder(string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(set,true);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static Light Lamp(string name,Vector3 position,Color color,float intensity,LightType type,float range)
        {
            var go=new GameObject(name);go.transform.position=position;if(set!=null)go.transform.SetParent(set,true);
            var light=go.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.Soft;light.shadowBias=.025f;light.shadowNormalBias=.1f;return light;
        }
        static void StreetLamp(Vector3 position,bool inward)
        {
            var metal=Surface("Streetlamp charcoal",new Color(.085f,.1f,.12f));Cylinder("Streetlamp pole",position+Vector3.up*2.8f,new Vector3(.09f,2.8f,.09f),metal);
            Box("Lamp head",position+Vector3.up*5.5f,new Vector3(.8f,.13f,.4f),Surface("Lamp amber",new Color(.82f,.62f,.35f),0,1));
            var spot=Lamp("Warm streetlamp pool",position+Vector3.up*5.4f,new Color(1,.72f,.4f),5,LightType.Spot,12);spot.spotAngle=78;spot.innerSpotAngle=45;spot.transform.rotation=Quaternion.Euler(80,inward?90:-90,0);
        }
        static void Rain(Vector3 position,Vector3 size)
        {
            var go=new GameObject("Rain - background weather");go.transform.SetParent(set,true);go.transform.position=position;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.startLifetime=1.4f;main.startSpeed=0;main.startSize=.018f;main.maxParticles=1800;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.rateOverTime=850;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=size;
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=-.6f;velocity.y=-10;velocity.z=.3f;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=9;renderer.velocityScale=.05f;
            const string key="rain particles";
            if(!materials.TryGetValue(key,out var mat)){mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mat.name=key;mat.SetColor("_BaseColor",new Color(.55f,.66f,.74f,.25f));mat.SetFloat("_Surface",1);mat.SetFloat("_ZWrite",0);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;AssetDatabase.CreateAsset(mat,Settings+"/Rain.mat");materials[key]=mat;}
            renderer.sharedMaterial=mat;ps.Simulate(1.3f,true,true);ps.Pause();
        }
        static void Sign(string text,Vector3 position,Vector2 size,Color color,float yaw)
        {
            var board=Box(text+" sign",position,new Vector3(size.x,size.y,.05f),Surface("Sign backing",new Color(.065f,.08f,.09f)));board.transform.rotation=Quaternion.Euler(0,yaw,0);
            var label=new GameObject("Lettering",typeof(TextMeshPro)).GetComponent<TextMeshPro>();label.transform.SetParent(board.transform,false);
            label.transform.localScale=new Vector3(1/size.x,1/size.y,20);label.transform.localPosition=new Vector3(0,0,-.6f);
            label.rectTransform.sizeDelta=size*.9f;label.font=font;label.text=text;label.fontSize=Mathf.Min(2,size.y*3);label.alignment=TextAlignmentOptions.Center;label.color=color;label.raycastTarget=false;
        }
        static void Door(string name,Vector3 center,float yaw,float width,float height,Material material,float open=0)
        {
            var hinge=new GameObject(name).transform;hinge.SetParent(set);hinge.SetPositionAndRotation(center+Quaternion.Euler(0,yaw,0)*Vector3.left*width*.5f,Quaternion.Euler(0,yaw+open,0));
            var leaf=Box("Door leaf",Vector3.zero,new Vector3(width,height,.065f),material);leaf.transform.SetParent(hinge,true);leaf.transform.localPosition=new Vector3(width*.5f,height*.5f,0);leaf.transform.localRotation=Quaternion.identity;
            var handle=Box("Handle",Vector3.zero,new Vector3(.13f,.035f,.09f),Surface("Brass handles",new Color(.46f,.37f,.22f),.4f));handle.transform.SetParent(hinge,true);handle.transform.localPosition=new Vector3(width*.87f,height*.48f,-.075f);handle.transform.localRotation=Quaternion.identity;
        }
        static GameObject Taxi(string name,Vector3 position,float yaw)
        {
            var taxi=Model(Sources+"/BricchiCar/Source/Car.fbx",name,position,yaw,4.7f);var b=Bounds(taxi);
            var old=set;set=taxi.transform;Sign("TAXI",new Vector3(b.center.x,b.max.y+.13f,b.center.z),new Vector2(.56f,.21f),new Color(1,.81f,.31f),yaw);set=old;
            return taxi;
        }
        static void Road(Vector3 origin,float y,float length,bool elevated)
        {
            var road=Textured("Wet road surface",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/McPatoBuildings/Textures/Asphalt010_1K-PNG_Color.png"),new Color(.4f,.45f,.5f));road.SetFloat("_Smoothness",.5f);
            Box("Road slab",origin+new Vector3(0,y-.25f,0),new Vector3(11,.5f,length),road);
            for(int z=-(int)length/2;z<length/2;z+=8)Box("Lane dash",origin+new Vector3(0,y+.012f,z),new Vector3(.09f,.014f,3.5f),Surface("Worn road markings",new Color(.62f,.62f,.48f)));
            for(int side=-1;side<=1;side+=2)
            {
                Box("Concrete road barrier",origin+new Vector3(side*5.25f,y+.43f,0),new Vector3(.32f,.86f,length),Surface("Barrier concrete",new Color(.26f,.29f,.33f)));
                if(elevated)for(int z=-(int)length/2;z<length/2;z+=14)Box("Elevated road pier",origin+new Vector3(side*3.6f,y*.5f-1,z),new Vector3(.75f,y+2,.9f),Surface("Barrier concrete",new Color(.26f,.29f,.33f)));
            }
        }
        static void Skyline(Vector3 origin,int count)
        {
            var files=Directory.GetFiles(Sources+"/ElegantCrowSkyscrapers/Source","*.fbx",SearchOption.AllDirectories).OrderBy(p=>p).ToArray();
            for(int side=-1;side<=1;side+=2)for(int i=0;i<count;i++)
            {
                var file=files[(i*3+(side<0?0:2))%files.Length].Replace('\\','/');float width=12+i%3*3;
                Model(file,"City skyline "+side+"-"+i,origin+new Vector3(side*(24+i%2*12),-2,14+i*23),i*19,width);
            }
        }
        static void Actor(string id,string displayName,string path,Vector3 position,float yaw)
        {
            var go=Model(path,displayName+" - original model reference",position,yaw,0,true);go.transform.SetParent(preview.preservedCast,true);
            var actor=go.GetComponent<CutsceneActor>()??go.AddComponent<CutsceneActor>();actor.id=id;actor.displayName=displayName;actor.animator=go.GetComponentInChildren<Animator>(true);stage.actors.Add(actor);
        }
        static void Marker(string id,Vector3 position)
        {
            var marker=new GameObject("MARK - "+id).transform;marker.SetParent(stage.transform,true);marker.position=position;stage.spawns.Add(new CutsceneSpawn{id=id,target=marker});
        }
        static void Shot(string id,Vector3 position,Vector3 target,float fov)
        {
            var marker=new GameObject("CAM - "+id).transform;marker.SetParent(stage.transform,true);marker.position=position;marker.LookAt(target);
            var shot=marker.gameObject.AddComponent<CutsceneCameraPreset>();shot.id=id;shot.fieldOfView=fov;stage.shots.Add(shot);
        }
    }
}
