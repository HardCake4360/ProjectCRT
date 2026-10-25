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

namespace ProjectCRT.Prologue.Editor
{
    public static partial class SC23CutsceneAuthoring
    {
        public const string Folder="Assets/Settings/Prologue/SC23";
        public const string RoomScene="Assets/Scenes/ProjectCRT_SC02_SanRoom.unity",BarScene="Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity";
        public const string RoomSequence=Folder+"/SC02_UnpackingAndEvening.asset",BarSequence=Folder+"/SC03_ShangrilaReunion.asset";
        static CutsceneStage stage;
        static Rig san,rebecca,jeremy;
        static readonly Dictionary<string,AnimationClip> clips=new();
        static readonly List<string> report=new();
        static AnimationClip walk;
        static Vector3 sofaFeet=new(-1.8f,.025f,1.6f),sofaSeat=new(-1.8f,.48f,2.35f),sofaEnd=new(-1.8f,.025f,1.65f);
        static Vector3 barSeat=new(-5,.94f,9.75f),barStand=new(-5,.215f,9.15f),rCounter=new(-4.6f,.215f,11.93f),jCounter=new(-3.15f,.215f,11.93f);
        static Vector3 threshold=new(3.67f,.205f,12.25f),sanThreshold=new(3.35f,.205f,11.3f),found=new(6.6f,.42f,12.15f);
        [Serializable]class DialogueSource{public DialogueLine[] lines;}
        [Serializable]class DialogueLine{public int camera;public string speaker,text;}
        public static void BatchBuild()
        {
            try{BuildRoomAndBar();EditorApplication.Exit(0);}
            catch(Exception e){File.WriteAllText("Logs/SC23/build-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BuildRoomAndBar(){Directory.CreateDirectory("Logs/SC23");BuildRoom();BuildBar();AssetDatabase.SaveAssets();File.WriteAllLines("Logs/SC23/authoring.txt",report);File.WriteAllText("Logs/SC23/build-status.txt","PASS");}
        static void Open(string path)
        {
            Directory.CreateDirectory(Folder+"/Animation");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();EditorSceneManager.OpenScene(path);clips.Clear();
            stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var preview=stage.GetComponent<PrologueBackgroundPreview>();preview.enabled=false;
            var viewer=stage.output.GetComponent<OutdoorStreetViewer>();if(viewer!=null)viewer.enabled=false;
            foreach(var a in stage.actors)if(a!=null)a.gameObject.SetActive(false);stage.actors.Clear();
            var originalWalk=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Settings/Prologue/SC01/Animation/SanWalk_RigPathAdapted.anim");
            var adapted=new AnimationClip{name="San original walk - normalized prologue hierarchy",frameRate=24};
            foreach(var binding in AnimationUtility.GetCurveBindings(originalWalk)){var b=binding;b.path=b.path.Replace("Model/","Model/Source/");AnimationUtility.SetEditorCurve(adapted,b,AnimationUtility.GetEditorCurve(originalWalk,binding));}
            string walkPath=Folder+"/Animation/SanOriginalWalkAdapted.anim";walk=AssetDatabase.LoadAssetAtPath<AnimationClip>(walkPath);if(walk==null){AssetDatabase.CreateAsset(adapted,walkPath);walk=adapted;}else{EditorUtility.CopySerialized(adapted,walk);UnityEngine.Object.DestroyImmediate(adapted);}
        }
        static Rig Character(string id,string source,float height)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);instance.transform.localScale=Vector3.one;
            foreach(var a in instance.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var model=new GameObject("Model");instance.name="Source";instance.transform.SetParent(model.transform,false);
            var renderers=instance.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float scale=height/bounds.size.y;instance.transform.localScale*=scale;
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var head=instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="head");
            instance.transform.position-=new Vector3(head!=null?head.position.x:bounds.center.x,bounds.min.y,head!=null?head.position.z:bounds.center.z);
            var root=new GameObject(id+" - original character performance");model.transform.SetParent(root.transform,false);
            var animator=root.AddComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
            var actor=root.AddComponent<CutsceneActor>();actor.animator=animator;actor.id=id;actor.displayName=id=="san"?"산":id=="rebecca"?"레베카":"제레미";
            actor.voice=AssetDatabase.LoadAssetAtPath<CutsceneVoiceProfile>(id=="san"?"Assets/Settings/Prologue/SC01/Voices/San.asset":"Assets/Settings/Cutscenes/Voices/"+(id=="rebecca"?"Rebecca":"Jeremy")+".asset");
            var rig=new Rig{root=root,model=model.transform,actor=actor};
            if(id!="san")foreach(var renderer in model.GetComponentsInChildren<Renderer>(true)){
                var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++){var replacement=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/BarRetro/Lighting/Materials/"+materials[i].name+"_BakedLit.mat");if(replacement!=null)materials[i]=replacement;}renderer.sharedMaterials=materials;
            }
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*4);}
            if(id=="san")
            {
                CalibrateSan(rig.model);actor.lookAtBone=rig.Bone("head");
            }
            else if(id=="rebecca")
            {
                // Lower the original bind-pose arms with the existing skeleton; do not modify the FBX or imported clips.
                foreach(string side in new[]{"L","R"}){float sign=side=="L"?1:-1;var shoulder=rig.Bone("upper_arm."+side);var elbow=rig.Bone("forearm."+side);var hand=rig.Bone("hand."+side);TwoBone(shoulder,elbow,hand,shoulder.position+new Vector3(sign*.035f,-.48f,.08f),new Vector3(sign,.1f,.2f));}
                actor.lookAtBone=rig.Bone("head");
            }
            else MakeJeremyRig(rig);
            actor.lookAtWeight=.38f;actor.calibrateLookAxis=false;actor.localLookAxis=actor.lookAtBone.InverseTransformDirection(rig.model.forward);rig.CaptureRest();stage.actors.Add(actor);return rig;
        }
        static void CalibrateSan(Transform model)
        {
            foreach(var r in model.GetComponentsInChildren<Renderer>(true)){var slots=r.sharedMaterials;for(int i=0;i<slots.Length;i++){var path="Assets/Settings/Prologue/SC01/San palette - "+slots[i].name+".mat";var replacement=AssetDatabase.LoadAssetAtPath<Material>(path);if(replacement!=null)slots[i]=replacement;}r.sharedMaterials=slots;}
        }
        static void MakeJeremyRig(Rig rig)
        {
            // A derived skin for the unrigged original mesh enables simple limbs without changing its geometry/materials.
            Transform Bone(string name,Transform parent,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=position;return t;}
            var hips=Bone("J_Hips",rig.model,new Vector3(0,.70f,0));var spine=Bone("J_Spine",hips,new Vector3(0,.94f,0));var head=Bone("J_Head",spine,new Vector3(0,1.23f,0));
            var bones=new List<Transform>{hips,spine,head};
            foreach(int side in new[]{-1,1}){
                string suffix=side<0?"L":"R";var thigh=Bone("J_Thigh"+suffix,hips,new Vector3(side*.075f,.69f,0));var knee=Bone("J_Knee"+suffix,thigh,new Vector3(side*.075f,.36f,0));var foot=Bone("J_Foot"+suffix,knee,new Vector3(side*.075f,.07f,.035f));bones.Add(thigh);bones.Add(knee);bones.Add(foot);
                var arm=Bone("J_Arm"+suffix,spine,new Vector3(side*.15f,1.04f,0));var elbow=Bone("J_Elbow"+suffix,arm,new Vector3(side*.18f,.85f,.025f));var hand=Bone("J_Hand"+suffix,elbow,new Vector3(side*.18f,.67f,.06f));bones.Add(arm);bones.Add(elbow);bones.Add(hand);
            }
            foreach(var filter in rig.model.GetComponentsInChildren<MeshFilter>(true).ToArray()){
                var original=filter.GetComponent<MeshRenderer>();if(original==null)continue;var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Jeremy original geometry - articulated "+filter.name;
                var points=mesh.vertices.Select(p=>rig.model.InverseTransformPoint(filter.transform.TransformPoint(p))).ToArray();mesh.vertices=points;mesh.normals=mesh.normals.Select(n=>rig.model.InverseTransformDirection(filter.transform.TransformDirection(n))).ToArray();
                var weights=new BoneWeight[points.Length];for(int i=0;i<points.Length;i++){var p=points[i];int side=p.x<0?3:9;int bone;
                    if(p.y>1.12f)bone=2;else if(p.y<.70f)bone=side+(p.y<.22f?2:p.y<.45f?1:0);else if(Mathf.Abs(p.x)>.125f&&p.y<1.08f)bone=side+3+(p.y<.78f?2:p.y<.94f?1:0);else bone=p.y>.82f?1:0;
                    weights[i]=new BoneWeight{boneIndex0=bone,weight0=1};}
                mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*rig.model.localToWorldMatrix).ToArray();mesh.RecalculateBounds();string path=Folder+"/Animation/JeremyDerived_"+filter.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject("Articulated "+filter.name);go.transform.SetParent(rig.model,false);var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=original.sharedMaterials;skin.bones=bones.ToArray();skin.rootBone=hips;skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(new Vector3(0,.7f,0),Vector3.one*4);original.enabled=false;
            }
            rig.actor.lookAtBone=head;
        }
        static void LocalStand(Rig rig,float time,bool talking=false,bool lean=false)
        {
            var head=rig.actor.lookAtBone;
            if(rig==san){foreach(string side in new[]{"L","R"}){float sign=side=="L"?-1:1;var arm=rig.Bone("arm1."+side);TwoBone(arm,rig.Bone("elbow."+side),rig.Bone("hand."+side),arm.position+new Vector3(sign*.025f,-.42f,.04f),Vector3.right*sign);}RotateWorld(rig.Bone("spine2"),Vector3.right,Mathf.Sin(time*2)*.6f);}
            else if(rig==rebecca){if(lean){var hand=rig.Bone("hand.R");TwoBone(rig.Bone("upper_arm.R"),rig.Bone("forearm.R"),hand,new Vector3(.18f,.99f,.38f),Vector3.right);RotateWorld(rig.Bone("spine.002"),Vector3.right,8);}}
            else RotateWorld(rig.Bone("J_Spine"),Vector3.right,Mathf.Sin(time*2)*.7f);
            if(talking&&head!=null){RotateWorld(head,Vector3.right,Mathf.Sin(time*5)*2.1f);RotateWorld(head,Vector3.up,Mathf.Sin(time*2.6f)*1.5f);}
        }
        static void Stand(Rig rig,Vector3 position,float yaw,float time=0,bool talking=false,bool lean=false){LocalStand(rig,time,talking,lean);rig.model.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));}
        static void Seated(Vector3 seat,float yaw,float time,bool talking=false)
        {
            var hip=(san.Bone("leg1.L").position+san.Bone("leg1.R").position)*.5f;san.model.position-=hip;
            foreach(string side in new[]{"L","R"}){var upper=san.Bone("leg1."+side);TwoBone(upper,san.Bone("knee."+side),san.Bone("feet."+side),new Vector3(upper.position.x,-.39f,.34f),Vector3.forward);Aim(san.Bone("feet."+side),san.Bone("toe."+side),Vector3.forward);float sign=side=="L"?-1:1;TwoBone(san.Bone("arm1."+side),san.Bone("elbow."+side),san.Bone("hand."+side),new Vector3(sign*.14f,-.08f,.25f),Vector3.right*sign);}
            RotateWorld(san.Bone("spine2"),Vector3.right,Mathf.Sin(time*2)*.6f);if(talking)Nod(san.Bone("head"),Mathf.Sin(time*5)*2);
            var rotation=Quaternion.Euler(0,yaw,0);san.model.SetPositionAndRotation(seat+rotation*san.model.position,rotation);
        }
        static void Walk(Rig rig,Vector3[] points,float duration,float time)
        {
            if(rig==san)walk.SampleAnimation(rig.root,Mathf.Repeat(time,walk.length));
            else if(rig==rebecca){float swing=Mathf.Sin(time*8)*22;RotateWorld(rig.Bone("thigh.L"),Vector3.right,swing);RotateWorld(rig.Bone("thigh.R"),Vector3.right,-swing);RotateWorld(rig.Bone("shin.L"),Vector3.right,-Mathf.Max(0,swing));RotateWorld(rig.Bone("shin.R"),Vector3.right,-Mathf.Max(0,-swing));RotateWorld(rig.Bone("upper_arm.L"),Vector3.right,-swing*.5f);RotateWorld(rig.Bone("upper_arm.R"),Vector3.right,swing*.5f);}
            else{float swing=Mathf.Sin(time*8)*22;RotateWorld(rig.Bone("J_ThighL"),Vector3.right,swing);RotateWorld(rig.Bone("J_ThighR"),Vector3.right,-swing);RotateWorld(rig.Bone("J_KneeL"),Vector3.right,-Mathf.Max(0,swing));RotateWorld(rig.Bone("J_KneeR"),Vector3.right,-Mathf.Max(0,-swing));RotateWorld(rig.Bone("J_ArmL"),Vector3.right,-swing*.5f);RotateWorld(rig.Bone("J_ArmR"),Vector3.right,swing*.5f);}
            float total=0;for(int i=1;i<points.Length;i++)total+=Vector3.Distance(points[i-1],points[i]);float distance=Mathf.Clamp01(time/duration)*total;
            for(int i=1;i<points.Length;i++){float length=Vector3.Distance(points[i-1],points[i]);if(distance<=length||i==points.Length-1){var direction=points[i]-points[i-1];rig.model.SetPositionAndRotation(Vector3.Lerp(points[i-1],points[i],length>.0001f?distance/length:1),Quaternion.LookRotation(direction));return;}distance-=length;}
        }
        static void Blend(Rig rig,Action a,Action b,float t){a();var first=Capture(rig);rig.RestoreRest();b();var last=Capture(rig);ApplyLerp(first,last,Mathf.SmoothStep(0,1,t));}
        static void Mark(string id,Vector3 position,float yaw=0){var marker=new GameObject("MARK - "+id).transform;marker.position=position;marker.rotation=Quaternion.Euler(0,yaw,0);stage.spawns.Add(new CutsceneSpawn{id=id,target=marker});}
        static GameObject Find(string name)=>UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name==name).gameObject;
        static CutsceneCameraPreset Shot(string id,Vector3 position,Vector3 target,float fov=50)
        {
            var s=new GameObject("CAM - "+id).AddComponent<CutsceneCameraPreset>();s.id=id;s.transform.position=position;s.transform.LookAt(target);s.fieldOfView=fov;stage.shots.Add(s);return s;
        }
        static CutsceneCameraCue Cam(string id,CutsceneCameraMode mode=CutsceneCameraMode.Cut,float duration=.65f)=>new CutsceneCameraCue{mode=mode,shotId=id,duration=duration};
        static CutsceneActorCue Act(string id,string clip,bool loop=true,string look=null)=>new CutsceneActorCue{actorId=id,clip=clips[clip],loop=loop,changeLookAt=true,lookAtId=look??""};
        static CutsceneTurn Beat(List<CutsceneTurn> list,string id,string label,float duration,CutsceneCameraCue cam=null,params CutsceneActorCue[] actors){var t=new CutsceneTurn{id=id,label=label,hasDialogue=false,minimumDuration=duration,autoDelay=0,advance=CutsceneAdvanceMode.Auto,camera=cam??new CutsceneCameraCue(),actors=actors.ToList()};list.Add(t);return t;}
        static void Store(CutsceneSequence sequence,string path){var existing=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(path);if(existing!=null){EditorUtility.CopySerialized(sequence,existing);UnityEngine.Object.DestroyImmediate(sequence);}else AssetDatabase.CreateAsset(sequence,path);}
        static SC23PlaybackController Configure(CutsceneSequence sequence,bool room,Vector3 spawn,float yaw)
        {
            var view=stage.gameObject.AddComponent<CutsceneView>();view.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/resources/font/NeoDunggeunmoPro-Regular 1 SDF.asset");stage.gameObject.AddComponent<CutsceneVoicePlayer>();var director=stage.gameObject.AddComponent<CutsceneDirector>();director.stage=stage;director.view=view;director.defaultSequence=sequence;
            var motor=san.root.AddComponent<PrologueSanMotor>();var cc=san.root.GetComponent<CharacterController>();cc.height=1.45f;cc.center=Vector3.up*.75f;cc.radius=.18f;cc.stepOffset=.18f;cc.skinWidth=.015f;motor.model=san.model;motor.view=stage.output;motor.idle=clips["SanGameplayIdle"];motor.walk=clips["SanGameplayWalk"];motor.unpack=clips["SanGameplayUnpack"];
            motor.initialPosition=spawn;motor.initialYaw=yaw;san.root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);motor.idle.SampleAnimation(san.root,0);san.model.SetPositionAndRotation(spawn,Quaternion.Euler(0,yaw,0));
            var playback=stage.gameObject.AddComponent<SC23PlaybackController>();playback.director=director;playback.sequence=sequence;playback.san=motor;playback.isRoom=room;playback.font=view.font;playback.boxes=Array.Empty<Transform>();
            AudioSource Audio(string name,float volume,bool loop=true){var a=stage.gameObject.AddComponent<AudioSource>();a.playOnAwake=false;a.loop=loop;a.volume=volume;a.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Settings/Prologue/SC01/Audio/"+name+".wav")??AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Audio/"+name+".wav");return a;}
            playback.rain=Audio("Rain",room?.035f:.17f);playback.jazz=Audio("Jazz",.065f);playback.crowd=Audio("Crowd",.035f);playback.effects=Audio("Unpack",.3f,false);playback.unpackSound=playback.effects.clip;playback.doorOpen=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Settings/Prologue/SC01/Audio/DoorOpen.wav");playback.doorClose=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Settings/Prologue/SC01/Audio/DoorClose.wav");playback.thud=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Audio/Thud.wav");
            return playback;
        }
        static void GameplayClips()
        {
            SaveClip("SanGameplayIdle",san,2.4f,t=>Stand(san,Vector3.zero,0,t));SaveClip("SanGameplayWalk",san,walk.length,t=>{walk.SampleAnimation(san.root,t);san.model.localPosition=Vector3.zero;san.model.localRotation=Quaternion.identity;});
            SaveClip("SanGameplayUnpack",san,1.2f,t=>{LocalStand(san,t);float u=Mathf.Sin(t/1.2f*Mathf.PI);RotateWorld(san.Bone("spine2"),Vector3.right,u*35);foreach(string side in new[]{"L","R"}){float sign=side=="L"?-1:1;TwoBone(san.Bone("arm1."+side),san.Bone("elbow."+side),san.Bone("hand."+side),new Vector3(sign*.14f,.65f,.38f),Vector3.right*sign);}});
        }
        static void Finish(CutsceneSequence seq,string path,string gameplayShot)
        {
            var errors=stage.Validate(seq);if(errors.Count>0)throw new Exception(string.Join("\n",errors));stage.FindShot(gameplayShot).Sample().Apply(stage.output);EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);AssetDatabase.SaveAssets();report.Add(path+" | turns="+seq.turns.Count+" | original dialogue="+seq.turns.Count(t=>t.hasDialogue)+" | actors="+stage.actors.Count);
        }
    }
}
