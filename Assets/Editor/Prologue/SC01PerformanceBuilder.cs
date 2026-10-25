using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using ProjectCRT.Prologue;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Prologue.Editor
{
    public static partial class SC01CutsceneAuthoring
    {
        static CutsceneStage stage;
        static Rig san,driver;
        static readonly Dictionary<string,AnimationClip> clips=new();
        static Vector3 seatB=new(90.43f,4.74f,-.50f),driverB=new(89.57f,4.73f,.04f);
        static Vector3 seatC=new(179.43f,.69f,-1.50f),driverC=new(178.57f,.68f,-.96f);
        static Vector3 curb=new(180.6f,.01f,-1.8f),entrance=new(187.25f,.25f,2);
        static readonly List<string> authorReport=new();

        public static void BatchBuild()
        {
            try{Build();File.WriteAllText("Logs/SC01/build-status.txt","PASS");EditorApplication.Exit(0);}
            catch(Exception e){Directory.CreateDirectory("Logs/SC01");File.WriteAllText("Logs/SC01/build-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [MenuItem("Tools/ProjectCRT/Prologue/Author SC01 Performance")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(File.Exists(SequencePath))throw new InvalidOperationException("SC01 is already authored; edit its sequence and clips instead of rebuilding.");
            Directory.CreateDirectory(Folder+"/Animation");Directory.CreateDirectory(Folder+"/Voices");Directory.CreateDirectory("Logs/SC01");AssetDatabase.Refresh();clips.Clear();authorReport.Clear();
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();
            var preview=stage.GetComponent<PrologueBackgroundPreview>();preview.enabled=false;
            var viewer=stage.output.GetComponent<OutdoorStreetViewer>();if(viewer!=null)viewer.enabled=false;
            var oldSan=stage.actors.Single(a=>a.id=="san");var model=oldSan.gameObject;
            model.transform.SetParent(null,true);UnityEngine.Object.DestroyImmediate(oldSan);stage.actors.Clear();
            san=MakeRig("San - original model performance","san","산",model);
            CalibrateSanMaterials(san.model);
            san.model.localScale=Vector3.one*1.12f;san.CaptureRest();
            san.actor.lookAtBone=san.Bone("head");san.actor.lookAtWeight=.55f;
            driver=MakeDriver();driver.actor.lookAtBone=driver.Bone("Head");driver.actor.lookAtWeight=.45f;
            stage.actors.Add(san.actor);stage.actors.Add(driver.actor);
            san.actor.voice=Voice("San","산",163,.96f);driver.actor.voice=Voice("Driver","기사",112,.9f);
            Marker("window_right",new Vector3(93,5.15f,-.9f));Marker("road_forward",new Vector3(89.6f,5.3f,12));Marker("mirror_rear",new Vector3(90.2f,5.2f,-1));Marker("driver_face",new Vector3(89.57f,5.28f,.05f));Marker("san_face",new Vector3(90.43f,5.2f,-.94f));Marker("curb",curb);Marker("apartment_entry",entrance);
            MakeSanClips();MakeDriverClips();MakeVehicleClips();MakeDiscAndBag();MakeCameraWork();var filmingSets=MakeSceneryClips();
            var sequence=ScriptableObject.CreateInstance<CutsceneSequence>();sequence.sequenceId="sc01-road-to-los-simula";sequence.fadeDuration=1.2f;
            sequence.exitMode=CutsceneExitMode.LoadScene;sequence.destinationScene="Assets/Scenes/ProjectCRT_SC02_SanRoom.unity";sequence.allowSkip=true;sequence.beginWithBlack=true;
            sequence.turns=CreateTurns();
            foreach(var p in Directory.GetFiles(Folder+"/Audio","*.wav"))sequence.preloadAudio.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\','/')));
            AssetDatabase.CreateAsset(sequence,SequencePath);
            var view=stage.gameObject.AddComponent<CutsceneView>();view.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/resources/font/NeoDunggeunmoPro-Regular 1 SDF.asset");
            stage.gameObject.AddComponent<CutsceneVoicePlayer>();var director=stage.gameObject.AddComponent<CutsceneDirector>();director.stage=stage;director.view=view;director.defaultSequence=sequence;
            var playback=stage.gameObject.AddComponent<SC01PlaybackController>();playback.director=director;playback.sequence=sequence;playback.inspection=preview;playback.viewer=viewer;
            playback.filmingSets=filmingSets;
            playback.rain=Audio("Rain",.15f);playback.engine=Audio("Engine",.12f);playback.wipers=Audio("Wipers",.13f);playback.jazz=Audio("Jazz",.13f);playback.effects=stage.gameObject.AddComponent<AudioSource>();playback.effects.playOnAwake=false;playback.effects.volume=.25f;
            playback.cdClick=LoadAudio("CD");playback.doorOpen=LoadAudio("DoorOpen");playback.doorClose=LoadAudio("DoorClose");
            var errors=stage.Validate(sequence);if(errors.Count>0)throw new Exception(string.Join("\n",errors));
            // A readable resting state when the scene is opened outside Play Mode.
            foreach(var cue in sequence.turns[0].actors)if(cue.clip!=null)cue.clip.SampleAnimation(stage.FindActor(cue.actorId).gameObject,0);
            stage.shots[0].Sample().Apply(stage.output);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            authorReport.Add("Turns="+sequence.turns.Count+"; dialogue="+sequence.turns.Count(t=>t.hasDialogue)+"; paths="+stage.paths.Count+"; actors="+stage.actors.Count);
            authorReport.Add("San FBX and original animations retained. Driver is an articulated temporary scene model. No existing character FBX/importer was modified.");
            File.WriteAllLines("Logs/SC01/authoring.txt",authorReport);
        }
        static CutsceneVoiceProfile Voice(string id,string label,float hz,float pitch)
        {
            var voice=ScriptableObject.CreateInstance<CutsceneVoiceProfile>();voice.displayName=label;voice.pitch=pitch;voice.volume=.23f;voice.minimumInterval=.055f;voice.pitchVariation=.035f;voice.blips=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Audio/"+id+"Blip.wav")};
            AssetDatabase.CreateAsset(voice,Folder+"/Voices/"+id+".asset");return voice;
        }
        static AudioClip LoadAudio(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Audio/"+name+".wav");
        static AudioSource Audio(string name,float volume){var a=stage.gameObject.AddComponent<AudioSource>();a.clip=LoadAudio(name);a.loop=true;a.playOnAwake=false;a.volume=volume;return a;}
        static Rig MakeRig(string name,string id,string label,GameObject model)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(model))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(model),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var root=new GameObject(name);model.name="Model";model.transform.SetParent(root.transform,false);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            foreach(var a in model.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var animator=root.AddComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var actor=root.AddComponent<CutsceneActor>();actor.id=id;actor.displayName=label;actor.animator=animator;return new Rig{root=root,model=model.transform,actor=actor};
        }
        static Rig MakeDriver()
        {
            var body=new GameObject("Model");var skin=Mat("Driver skin",new Color(.52f,.40f,.29f));var coat=Mat("Driver charcoal jacket",new Color(.16f,.19f,.19f));var dark=Mat("Driver dark details",new Color(.035f,.04f,.04f));var gray=Mat("Driver gray hair",new Color(.3f,.3f,.28f));
            Primitive(body.transform,"Torso",new Vector3(0,.23f,0),new Vector3(.35f,.4f,.23f),coat);
            Primitive(body.transform,"Collar",new Vector3(0,.41f,.04f),new Vector3(.18f,.07f,.16f),Mat("Driver collar",new Color(.47f,.43f,.34f)));
            var head=new GameObject("Head").transform;head.SetParent(body.transform);head.localPosition=new Vector3(0,.51f,.015f);
            Primitive(head,"Face",Vector3.zero,new Vector3(.235f,.255f,.22f),skin,PrimitiveType.Sphere);
            Primitive(head,"Gray hair",new Vector3(0,.07f,-.045f),new Vector3(.24f,.11f,.17f),gray);
            Primitive(head,"Cap",new Vector3(0,.135f,0),new Vector3(.265f,.065f,.25f),dark);Primitive(head,"Cap brim",new Vector3(0,.105f,.12f),new Vector3(.27f,.025f,.14f),dark);
            Primitive(head,"Nose",new Vector3(0,-.01f,.118f),new Vector3(.055f,.065f,.05f),skin);
            Primitive(head,"Moustache",new Vector3(0,-.058f,.112f),new Vector3(.115f,.028f,.022f),gray);
            for(int side=-1;side<=1;side+=2)
            {
                Primitive(head,"Eye",new Vector3(side*.052f,.026f,.102f),new Vector3(.018f,.012f,.016f),dark);
                var shoulder=new GameObject(side<0?"Upper.L":"Upper.R").transform;shoulder.SetParent(body.transform);shoulder.localPosition=new Vector3(side*.18f,.36f,.02f);
                Primitive(shoulder,"Sleeve",new Vector3(0,-.11f,0),new Vector3(.095f,.22f,.095f),coat);
                var elbow=new GameObject(side<0?"Elbow.L":"Elbow.R").transform;elbow.SetParent(shoulder);elbow.localPosition=new Vector3(0,-.22f,0);
                Primitive(elbow,"Forearm",new Vector3(0,-.12f,0),new Vector3(.075f,.24f,.075f),coat);
                var hand=new GameObject(side<0?"Hand.L":"Hand.R").transform;hand.SetParent(elbow);hand.localPosition=new Vector3(0,-.24f,0);Primitive(hand,"Palm",Vector3.zero,new Vector3(.075f,.095f,.06f),skin);
                Primitive(body.transform,"Trousers",new Vector3(side*.105f,-.12f,.12f),new Vector3(.16f,.19f,.4f),dark);
            }
            var rig=MakeRig("Taxi driver - temporary articulated model","driver","기사",body);rig.CaptureRest();return rig;
        }
        static void MakeSanClips()
        {
            SaveClip("SanSeatB",san,3,t=>SanSeated(seatB,t,false));SaveClip("SanTalkB",san,2.4f,t=>SanSeated(seatB,t,true));
            SaveClip("SanNodB",san,1.6f,t=>{SanSeated(seatB,t,false);Nod(san.Bone("head"),Mathf.Sin(t/1.6f*Mathf.PI)*8);});
            SaveClip("SanSeatC",san,2.5f,t=>SanSeated(seatC,t,false));SaveClip("SanTalkC",san,2.4f,t=>SanSeated(seatC,t,true));
            SaveClip("SanArrive",san,3,t=>SanSeated(seatC+Vector3.back*(8*(1-Mathf.SmoothStep(0,1,t/3))),t,false));
            SaveClip("SanPay",san,1.5f,t=>{SanSeated(seatC,t,false);float u=Mathf.Sin(t/1.5f*Mathf.PI);RotateWorld(san.Bone("spine2"),Vector3.right,u*15);TwoBone(san.Bone("arm1.R"),san.Bone("elbow.R"),san.Bone("hand.R"),seatC+new Vector3(-.05f,.18f,.1f+u*.4f),Vector3.right);});
            var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/resources/fbx/San/anim/walking.anim");
            var retarget=new AnimationClip{name="SanWalk_RigPathAdapted",frameRate=24};
            foreach(var binding in AnimationUtility.GetCurveBindings(walk)){var b=binding;b.path="Model/Armature.001"+(binding.path.StartsWith("san")?binding.path.Substring(3):"/"+binding.path);AnimationUtility.SetEditorCurve(retarget,b,AnimationUtility.GetEditorCurve(walk,binding));}
            AssetDatabase.CreateAsset(retarget,Folder+"/Animation/SanWalk_RigPathAdapted.anim");
            SaveClip("SanExit",san,4.2f,t=>
            {
                float u=Mathf.SmoothStep(0,1,t/4.2f);SanSeated(Vector3.zero,t,false);var seated=Capture(san);
                san.RestoreRest();StandAt(curb,Quaternion.Euler(0,75,0));var standing=Capture(san);
                // First turn sideways, then rise outside the open door.
                san.RestoreRest();SanSeated(Vector3.zero,t,false);san.model.rotation=Quaternion.Euler(0,Mathf.Lerp(0,80,Mathf.Clamp01(u*2)),0);san.model.position+=Vector3.Lerp(seatC,curb-Vector3.up*.65f,Mathf.Clamp01(u*1.5f));
                var side=Capture(san);ApplyLerp(side,standing,Mathf.SmoothStep(0,1,Mathf.Clamp01((u-.28f)/.72f)));
            });
            SaveClip("SanLookCity",san,2.8f,t=>{StandAt(curb,Quaternion.Euler(0,55,0));RotateWorld(san.Bone("head"),Vector3.up,Mathf.Sin(t/2.8f*Mathf.PI)*24);});
            SaveClip("SanPickup",san,2.8f,t=>
            {
                StandAt(curb,Quaternion.Euler(0,85,0));float bend=Mathf.Sin(Mathf.Clamp01(t/2.8f)*Mathf.PI)*55;RotateWorld(san.Bone("spine2"),san.model.right,bend);
                TwoBone(san.Bone("arm1.R"),san.Bone("elbow.R"),san.Bone("hand.R"),new Vector3(181,.36f,-2),Vector3.back);
            });
            SaveClip("SanWalkEntry",san,7,t=>
            {
                retarget.SampleAnimation(san.root,Mathf.Repeat(t,retarget.length));
                float u=Mathf.Clamp01(t/7);Vector3 pos=Vector3.Lerp(curb,entrance,u);san.model.SetPositionAndRotation(pos,Quaternion.LookRotation((entrance-curb).normalized));
                // Keep the carry arm close to the handle while the existing walk plays.
                var shoulder=san.Bone("arm1.R");TwoBone(shoulder,san.Bone("elbow.R"),san.Bone("hand.R"),pos+new Vector3(.1f,.65f,-.28f),Vector3.back);
            });
            authorReport.Add("Existing walking.anim was copied with san/... bindings remapped to Model/Armature.001/...; no original clip was edited.");
        }
        static void SanSeated(Vector3 seat,float time,bool talking)
        {
            var hip=(san.Bone("leg1.L").position+san.Bone("leg1.R").position)*.5f;
            san.model.position-=hip;
            foreach(string side in new[]{"L","R"})
            {
                var upper=san.Bone("leg1."+side);var lower=san.Bone("knee."+side);var foot=san.Bone("feet."+side);float x=upper.position.x;
                TwoBone(upper,lower,foot,new Vector3(x,-.39f,.34f),Vector3.forward);
                Aim(foot,san.Bone("toe."+side),Vector3.forward);
                float sign=side=="L"?-1:1;
                TwoBone(san.Bone("arm1."+side),san.Bone("elbow."+side),san.Bone("hand."+side),new Vector3(sign*.13f,-.08f,.25f),Vector3.right*sign);
            }
            float breath=Mathf.Sin(time*2.2f)*.6f;RotateWorld(san.Bone("spine2"),Vector3.right,breath);
            if(talking){Nod(san.Bone("head"),Mathf.Sin(time*5)*2.2f);RotateWorld(san.Bone("spine3"),Vector3.up,Mathf.Sin(time*2.5f)*1.4f);}
            san.model.position+=seat;
        }
        static void StandAt(Vector3 position,Quaternion rotation){san.model.SetPositionAndRotation(position,rotation);}
        static void MakeDriverClips()
        {
            foreach(bool arrival in new[]{false,true})
            {
                string suffix=arrival?"C":"B";var seat=arrival?driverC:driverB;
                SaveClip("DriverDrive"+suffix,driver,3,t=>DriverPose(seat,t,false));SaveClip("DriverTalk"+suffix,driver,2.8f,t=>DriverPose(seat,t,true));
            }
            SaveClip("DriverMirror",driver,2.8f,t=>{DriverPose(driverB,t,true);RotateWorld(driver.Bone("Head"),Vector3.up,Mathf.Sin(t/2.8f*Mathf.PI)*22);});
            SaveClip("DriverArrive",driver,3,t=>DriverPose(driverC+Vector3.back*(8*(1-Mathf.SmoothStep(0,1,t/3))),t,false));
            SaveClip("DriverLeave",driver,7,t=>DriverPose(driverC+Vector3.forward*(Mathf.Max(0,t-.8f)*2.7f),t,false));
            SaveClip("DriverCD",driver,4.8f,t=>
            {
                DriverPose(driverB,t,false);float reach=Mathf.Sin(Mathf.Clamp01(t/4.8f)*Mathf.PI);
                Vector3 target=Vector3.Lerp(driverB+new Vector3(.1f,.23f,.43f),new Vector3(90.22f,5.01f,.5f),reach);
                TwoBone(driver.Bone("Upper.R"),driver.Bone("Elbow.R"),driver.Bone("Hand.R"),target,Vector3.right);
                RotateWorld(driver.Bone("Head"),Vector3.up,reach*18);Nod(driver.Bone("Head"),reach*12);
            });
            SaveClip("DriverVolume",driver,1.5f,t=>{DriverPose(driverB,t,false);TwoBone(driver.Bone("Upper.R"),driver.Bone("Elbow.R"),driver.Bone("Hand.R"),new Vector3(89.97f,5,.49f),Vector3.right);});
        }
        static void DriverPose(Vector3 seat,float time,bool talk)
        {
            driver.model.position=seat;
            foreach(string side in new[]{"L","R"})TwoBone(driver.Bone("Upper."+side),driver.Bone("Elbow."+side),driver.Bone("Hand."+side),seat+new Vector3(side=="L"?-.1f:.1f,.22f,.44f),side=="L"?Vector3.left:Vector3.right);
            if(talk)Nod(driver.Bone("Head"),Mathf.Sin(time*5)*2);else RotateWorld(driver.Bone("Head"),Vector3.up,Mathf.Sin(time*1.4f)*1.2f);
        }
        static void MakeVehicleClips()
        {
            var hero=Find("Hero taxi - elevated road");var heroRig=MakeRig("Taxi exterior motion","taxi_road","",hero);heroRig.model.position=new Vector3(.08f,4.82f,.376f);heroRig.CaptureRest();stage.actors.Add(heroRig.actor);
            var start=heroRig.model.localPosition;
            SaveClip("TaxiEstablish",heroRig,5,t=>heroRig.model.localPosition=start+Vector3.forward*Mathf.Lerp(-10,0,t/5));
            SaveClip("TaxiCity",heroRig,6,t=>heroRig.model.localPosition=start+Vector3.forward*(t/6*19));
            var arrival=Find("Arrival taxi");arrival.transform.rotation=Quaternion.identity;var arriveRig=MakeRig("Taxi arrival and departure","taxi_arrival","",arrival);arriveRig.model.position=new Vector3(179.08f,.77f,-.624f);
            var door=arriveRig.model.GetComponentsInChildren<Transform>().Single(t=>t.name=="R_Door_Back");
            var hinge=new GameObject("RearRightDoorHinge").transform;hinge.SetParent(arriveRig.model,true);hinge.position=new Vector3(179.82f,.75f,-1.08f);door.SetParent(hinge,true);
            arriveRig.CaptureRest();stage.actors.Add(arriveRig.actor);var astart=arriveRig.model.localPosition;
            SaveClip("TaxiArrive",arriveRig,3,t=>arriveRig.model.localPosition=astart+Vector3.back*(8*(1-Mathf.SmoothStep(0,1,t/3))));
            SaveClip("TaxiDoorOpen",arriveRig,4.2f,t=>hinge.localRotation=Quaternion.Euler(0,-68*Mathf.SmoothStep(0,1,t/.8f),0));
            SaveClip("TaxiLeave",arriveRig,7,t=>{hinge.localRotation=Quaternion.Euler(0,Mathf.Lerp(-68,0,Mathf.Clamp01(t/.6f)),0);arriveRig.model.localPosition=astart+Vector3.forward*(Mathf.Max(0,t-.8f)*2.7f);});
            var arrivalSet=arrival.transform.parent; // Remove only scene dressing that blocked the passenger's path to the pavement.
            foreach(var obj in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="Concrete road barrier"&&t.position.x>170))obj.gameObject.SetActive(false);
            var cabin=Find("Interior taxi - same hero model").transform;
            var wiperModel=new GameObject("Wiper assembly");var wr=MakeRig("Taxi wipers","wipers","",wiperModel);stage.actors.Add(wr.actor);
            var black=Mat("Wiper rubber",new Color(.025f,.03f,.035f));
            var pivots=new List<Transform>();
            for(int i=0;i<2;i++)
            {
                var pivot=new GameObject("Pivot "+i).transform;pivot.SetParent(wr.model,false);pivot.position=new Vector3(89.58f+i*.62f,5.16f,1.02f);pivot.rotation=Quaternion.Euler(-30,0,0);
                Primitive(pivot,"Wiper arm",new Vector3(.2f,0,0),new Vector3(.4f,.012f,.016f),black);Primitive(pivot,"Wiper blade",new Vector3(.4f,0,0),new Vector3(.015f,.012f,.34f),black);pivots.Add(pivot);
            }
            wr.CaptureRest();var rotations=pivots.Select(p=>p.localRotation).ToArray();
            SaveClip("Wipers",wr,1.8f,t=>{for(int i=0;i<pivots.Count;i++)pivots[i].localRotation=rotations[i]*Quaternion.Euler(0,Mathf.Lerp(-12,70,(1-Mathf.Cos(t/1.8f*Mathf.PI*2))*.5f),0);});
            // Cabin mirror is separate so the driver has a readable glance target.
            Primitive(cabin,"Rear view mirror",new Vector3(0,.52f,.6f),new Vector3(.28f,.075f,.035f),Mat("Mirror dark silver",new Color(.3f,.38f,.4f)));
        }
        static void MakeDiscAndBag()
        {
            var disc=Find("Disc");var discRig=MakeRig("CD insertion prop","disc","",disc);discRig.model.position=new Vector3(90.35f,4.97f,.5f);discRig.CaptureRest();stage.actors.Add(discRig.actor);
            var p=discRig.model.localPosition;
            SaveClip("DiscInsert",discRig,4.8f,t=>
            {
                float u=Mathf.Clamp01((t-.5f)/2.5f);discRig.model.localPosition=Vector3.Lerp(p,new Vector3(89.92f,5.015f,.49f),Mathf.SmoothStep(0,1,u));discRig.model.localScale*=Mathf.Lerp(1,.001f,Mathf.Clamp01((t-2.9f)/.5f));
            });
            var bag=Find("San travel bag");var bagRig=MakeRig("San travel bag performance","bag","",bag);var size=bagRig.model.localScale;bagRig.model.position=new Vector3(181,.02f,-2);bagRig.CaptureRest();stage.actors.Add(bagRig.actor);
            SaveClip("BagPickup",bagRig,2.8f,t=>bagRig.model.localPosition=Vector3.Lerp(new Vector3(181,.02f,-2),curb+new Vector3(.1f,.18f,-.28f),Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.9f)/1.6f))));
            SaveClip("BagWalk",bagRig,7,t=>bagRig.model.localPosition=Vector3.Lerp(curb,entrance,Mathf.Clamp01(t/7))+new Vector3(.1f,.18f+Mathf.Sin(t*9)*.018f,-.28f));
        }
        static void MakeCameraWork()
        {
            SetShot(2,new Vector3(90.05f,5.3f,.18f),new Vector3(90.43f,5.1f,-.50f),58,san.Bone("head"),new Vector3(0,-.08f,0));
            SetShot(4,new Vector3(90.0f,5.26f,.04f),new Vector3(90.43f,5.1f,-.50f),60,san.Bone("head"),new Vector3(0,-.08f,0));
            SetShot(6,new Vector3(184,2.15f,-5.7f),new Vector3(179.65f,1,-1.6f),55);
            SetShot(7,new Vector3(178.65f,1.7f,-5.1f),new Vector3(183.5f,1.05f,.2f),55);
            var cityTarget=Marker("taxi_city_target",new Vector3(.08f,5,12));
            Path("city_follow",new[]{new Vector3(-8,8,16),new Vector3(-9,8.5f,21),new Vector3(-10,9,28)},cityTarget,51);
            Path("entry_follow",new[]{new Vector3(178.65f,1.7f,-5.1f),new Vector3(180.5f,1.75f,-4),new Vector3(182.8f,1.8f,-2.8f)},san.Bone("head"),52);
            var fill=new GameObject("San cabin soft fill").AddComponent<Light>();fill.type=LightType.Point;fill.color=new Color(.58f,.72f,.87f);fill.intensity=.8f;fill.range=2.2f;fill.transform.position=new Vector3(90.1f,5.4f,.25f);
        }
        static Transform[] MakeSceneryClips()
        {
            var weather=new GameObject("SC01 continuous weather").transform;
            foreach(var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))ps.transform.SetParent(weather,true);
            var groups=SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.name.StartsWith("SET ")).OrderBy(g=>g.name).Select(g=>g.transform).ToArray();
            if(groups.Length!=3)throw new Exception("Expected three SC01 filming sets.");
            var container=new GameObject("Model");var rig=MakeRig("SC01 filming scenery","scenery","",container);stage.actors.Add(rig.actor);
            foreach(var group in groups)group.SetParent(rig.model,true);rig.CaptureRest();
            for(int active=0;active<3;active++)
            {
                string id="Set"+(char)('A'+active);var clip=new AnimationClip{name=id,frameRate=20};
                for(int i=0;i<groups.Length;i++)
                {
                    string path=AnimationUtility.CalculateTransformPath(groups[i],rig.root.transform);
                    for(int axis=0;axis<3;axis++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalPosition."+"xyz"[axis]),AnimationCurve.Constant(0,.1f,axis==1&&i!=active?-1000:0));
                }
                AssetDatabase.CreateAsset(clip,Folder+"/Animation/"+id+".anim");clips[id]=clip;
            }
            return groups;
        }
        static void SetShot(int index,Vector3 pos,Vector3 target,float fov,Transform look=null,Vector3 offset=default)
        {
            var shot=stage.shots[index];shot.transform.position=pos;shot.transform.LookAt(target);shot.fieldOfView=fov;shot.lookAt=look;shot.lookAtOffset=offset;
        }
        static void Path(string id,Vector3[] points,Transform target,float fov)
        {
            var go=new GameObject("PATH - "+id);go.transform.SetParent(stage.transform);var path=go.AddComponent<CutscenePath>();path.id=id;path.lookAt=target;path.fieldOfView=fov;
            path.knots=points.Select((p,i)=>{var t=new GameObject("Knot "+i).transform;t.SetParent(go.transform);t.position=p;return t;}).ToArray();stage.paths.Add(path);
        }
        static Transform Marker(string id,Vector3 pos)
        {
            var old=stage.FindSpawn(id);if(old!=null){old.position=pos;return old;}
            var t=new GameObject("MARK - "+id).transform;t.SetParent(stage.transform);t.position=pos;stage.spawns.Add(new CutsceneSpawn{id=id,target=t});return t;
        }
        static GameObject Find(string name)=>UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name==name).gameObject;
        static Material Mat(string name,Color color)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.color=color;mat.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat");return mat;
        }
        static GameObject Primitive(Transform parent,string name,Vector3 position,Vector3 scale,Material material,PrimitiveType kind=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(kind);go.name=name;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
    }
}
