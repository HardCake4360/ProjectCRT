using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using UnityEditor;
using UnityEngine;

namespace ProjectCRT.Prologue.Editor
{
    public static partial class SC23CutsceneAuthoring
    {
        static void BuildRoom()
        {
            Open(RoomScene);san=Character("san","Assets/resources/fbx/San/San.fbx",1.49f);GameplayClips();
            SaveClip("SanSofaSit",san,3.2f,t=>{if(t<1.2f)Walk(san,new[]{sofaFeet,sofaEnd},1.2f,t);else Blend(san,()=>Stand(san,sofaEnd,180),()=>Seated(sofaSeat,180,t),Mathf.Clamp01((t-1.2f)/1.25f));});
            SaveClip("SanSofaRest",san,3,t=>{Seated(sofaSeat,180,t);RotateWorld(san.Bone("spine2"),Vector3.right,-7);});
            SaveClip("SanSofaClock",san,3,t=>{Seated(sofaSeat,180,t);RotateWorld(san.Bone("head"),Vector3.up,-20);RotateWorld(san.Bone("head"),Vector3.right,-12);});
            SaveClip("SanSofaStand",san,2.4f,t=>Blend(san,()=>Seated(sofaSeat,180,t),()=>Stand(san,sofaEnd,180),t/1.8f));
            Mark("sofa_approach_sc02",sofaFeet,180);Mark("sofa_standing_sc02",sofaEnd,180);Mark("clock_sc02",new Vector3(-.7f,1.9f,2.985f));
            Shot("sc02-gameplay",new Vector3(.25f,3.65f,-2.8f),new Vector3(0,.35f,.25f),68);
            Shot("sc02-sofa",new Vector3(.15f,1.65f,.25f),sofaSeat+new Vector3(.1f,.40f,-.05f),49);
            Shot("sc02-clock",new Vector3(-.1f,1.6f,-.8f),new Vector3(-1.25f,1.1f,2.4f),55);
            var seq=ScriptableObject.CreateInstance<CutsceneSequence>();seq.sequenceId="sc02-unpacking-evening";seq.fadeDuration=.55f;seq.exitMode=CutsceneExitMode.Return;
            Beat(seq.turns,"sc02-sit","CAM01 · 소파에 앉아 기대기",3.2f,Cam("sc02-sofa",CutsceneCameraMode.Blend,1),Act("san","SanSofaSit",false));
            var source=JsonUtility.FromJson<DialogueSource>(File.ReadAllText(Folder+"/SC02_DialogueSource.json"));
            for(int i=0;i<source.lines.Length;i++){
                if(i==2)Beat(seq.turns,"sc02-pause","시계를 본 뒤 잠깐의 공백",1.15f);
                var line=source.lines[i];seq.turns.Add(new CutsceneTurn{id="sc02-d"+(i+1).ToString("00"),label="산의 생각 · "+line.text,speakerId="san",text=line.text,dialogueKind=CutsceneDialogueKind.Thought,nameMode=CutsceneNameMode.Hidden,voiceMode=CutsceneVoiceMode.Silent,charsPerSecond=24,punctuationPause=.2f,minimumDuration=.5f,camera=i==1?Cam("sc02-clock",CutsceneCameraMode.Blend,.85f):new CutsceneCameraCue(),actors=new List<CutsceneActorCue>{Act("san",i==0?"SanSofaRest":"SanSofaClock",true,i==0?null:"clock_sc02")}});
            }
            Beat(seq.turns,"sc02-stand","CAM03 · 일어서서 고정 시점으로 복귀",2.4f,Cam("sc02-gameplay",CutsceneCameraMode.Blend,2.3f),Act("san","SanSofaStand",false));
            Store(seq,RoomSequence);seq=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(RoomSequence);
            var playback=Configure(seq,true,new Vector3(.35f,.025f,-.15f),0);playback.sofaApproach=stage.FindSpawn("sofa_approach_sc02");playback.afterSofa=stage.FindSpawn("sofa_standing_sc02");playback.boxes=Enumerable.Range(1,5).Select(i=>Find("Moving box "+i).transform).ToArray();
            foreach(var furniture in new[]{"Sofa - rest scene","Bed","Coffee table","Wardrobe"})foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.Equals(furniture,StringComparison.OrdinalIgnoreCase)))AddBoundsCollider(t.gameObject);
            Finish(seq,RoomScene,"sc02-gameplay");
        }
        static void AddBoundsCollider(GameObject go)
        {
            if(go.GetComponentsInChildren<Collider>().Length>0)return;var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length==0)return;var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);var c=new GameObject(go.name+" collision").AddComponent<BoxCollider>();c.transform.position=b.center;c.size=b.size;
        }
        static Rig Prop(GameObject go,string id)
        {
            var parent=new GameObject(id+" performance");var wrapper=new GameObject("Model");wrapper.transform.SetParent(parent.transform,false);go.transform.SetParent(wrapper.transform,true);var animator=parent.AddComponent<Animator>();var actor=parent.AddComponent<CutsceneActor>();actor.id=id;actor.animator=animator;var rig=new Rig{root=parent,model=wrapper.transform,actor=actor};rig.CaptureRest();stage.actors.Add(actor);return rig;
        }
        static void BarPoses()
        {
            GameplayClips();
            SaveClip("SanBarEntry",san,4.5f,t=>{Walk(san,new[]{new Vector3(-5.442f,.215f,5.7f),new Vector3(-5.442f,.215f,7.9f),barStand},4.5f,t);RotateWorld(san.Bone("head"),Vector3.up,Mathf.Sin(t*1.5f)*18);});
            SaveClip("SanBarStand",san,2.4f,t=>Stand(san,barStand,0,t));
            SaveClip("SanBarTalkStand",san,2.4f,t=>Stand(san,barStand,0,t,true));
            SaveClip("SanBarSit",san,2.4f,t=>Blend(san,()=>Stand(san,barStand,0),()=>Seated(barSeat,0,t),t/1.8f));
            SaveClip("SanBarSeat",san,2.8f,t=>Seated(barSeat,0,t));SaveClip("SanBarTalk",san,2.8f,t=>Seated(barSeat,0,t,true));
            SaveClip("SanBarLook",san,3,t=>{Seated(barSeat,0,t);RotateWorld(san.Bone("head"),Vector3.up,Mathf.Sin(t/3*Mathf.PI)*38);});
            SaveClip("SanBarRise",san,2.3f,t=>Blend(san,()=>Seated(barSeat,0,t),()=>Stand(san,barStand,90),t/1.8f));
            SaveClip("JeremyCounterIdle",jeremy,2.8f,t=>Stand(jeremy,jCounter,180,t));SaveClip("JeremyCounterTalk",jeremy,2.8f,t=>Stand(jeremy,jCounter,205,t,true));
            SaveClip("JeremyPoint",jeremy,2,t=>{LocalStand(jeremy,t,true);float u=Mathf.Sin(Mathf.Clamp01(t/2)*Mathf.PI);RotateWorld(jeremy.Bone("J_ArmR"),Vector3.right,-45*u);RotateWorld(jeremy.Bone("J_ElbowR"),Vector3.right,-20*u);jeremy.model.SetPositionAndRotation(jCounter,Quaternion.Euler(0,205,0));});
            SaveClip("JeremyBow",jeremy,1.4f,t=>{LocalStand(jeremy,t);RotateWorld(jeremy.Bone("J_Spine"),Vector3.right,Mathf.Sin(t/1.4f*Mathf.PI)*18);jeremy.model.SetPositionAndRotation(jCounter,Quaternion.Euler(0,205,0));});
            var jPath=new[]{jCounter,new Vector3(-.55f,.215f,11.93f),new Vector3(-.55f,.215f,9.5f),new Vector3(1.8f,.215f,8.2f),new Vector3(3.45f,.215f,8.2f),new Vector3(3.45f,.205f,12.15f),found};
            SaveClip("JeremyLeave",jeremy,12,t=>Walk(jeremy,jPath,12,t));
            SaveClip("JeremyHidden",jeremy,1,t=>{jeremy.model.position=found;jeremy.model.localScale=Vector3.one*.001f;});
            SaveClip("JeremyFallen",jeremy,2,t=>{LocalStand(jeremy,0);RotateWorld(jeremy.Bone("J_KneeL"),Vector3.right,-22);RotateWorld(jeremy.Bone("J_ArmR"),Vector3.up,25);jeremy.model.SetPositionAndRotation(found,Quaternion.Euler(0,-20,84));});
            SaveClip("RebeccaHidden",rebecca,1,t=>{rebecca.model.position=new Vector3(-1.4f,.215f,12.55f);rebecca.model.localScale=Vector3.one*.001f;});
            SaveClip("RebeccaEnter",rebecca,3.6f,t=>{Walk(rebecca,new[]{new Vector3(-1.45f,.215f,12.45f),rCounter},3.6f,t);TwoBone(rebecca.Bone("upper_arm.R"),rebecca.Bone("forearm.R"),rebecca.Bone("hand.R"),rebecca.model.position+rebecca.model.rotation*new Vector3(.13f,.97f,.32f),rebecca.model.right);});
            SaveClip("RebeccaCounterIdle",rebecca,2.8f,t=>Stand(rebecca,rCounter,180,t));SaveClip("RebeccaCounterTalk",rebecca,2.8f,t=>Stand(rebecca,rCounter,180,t,true));
            SaveClip("RebeccaLean",rebecca,2.8f,t=>Stand(rebecca,rCounter,180,t,false,true));SaveClip("RebeccaLeanTalk",rebecca,2.8f,t=>Stand(rebecca,rCounter,180,t,true,true));
            SaveClip("RebeccaPlaceBottle",rebecca,2.4f,t=>{LocalStand(rebecca,t);float u=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/1.4f));TwoBone(rebecca.Bone("upper_arm.R"),rebecca.Bone("forearm.R"),rebecca.Bone("hand.R"),Vector3.Lerp(new Vector3(.13f,.97f,.32f),new Vector3(.25f,.99f,.72f),u),Vector3.right);rebecca.model.SetPositionAndRotation(rCounter,Quaternion.Euler(0,180,0));});
            SaveClip("RebeccaGlass",rebecca,2.4f,t=>{LocalStand(rebecca,t);TwoBone(rebecca.Bone("upper_arm.L"),rebecca.Bone("forearm.L"),rebecca.Bone("hand.L"),new Vector3(-.18f,1.02f,.45f),Vector3.left);rebecca.model.SetPositionAndRotation(rCounter,Quaternion.Euler(0,180,0));});
            SaveClip("RebeccaNoise",rebecca,2.5f,t=>{LocalStand(rebecca,t);RotateWorld(rebecca.Bone("head"),Vector3.up,-Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.8f))*38);rebecca.model.SetPositionAndRotation(rCounter,Quaternion.Euler(0,180,0));});
            var rPath=new[]{rCounter,new Vector3(-.55f,.215f,11.93f),new Vector3(-.55f,.215f,9.5f),new Vector3(1.8f,.215f,8.2f),new Vector3(3.45f,.205f,8.25f)};
            var sPath=new[]{barStand,new Vector3(-.8f,.215f,9.15f),new Vector3(1.8f,.215f,8.25f),new Vector3(3.45f,.205f,8.25f)};
            SaveClip("RebeccaLeaveCounter",rebecca,9,t=>Walk(rebecca,rPath,9,t));SaveClip("SanFollowCounter",san,9,t=>Walk(san,sPath,9,Mathf.Max(0,t-1)));
            SaveClip("RebeccaHallWalk",rebecca,5.5f,t=>Walk(rebecca,new[]{new Vector3(3.45f,.205f,8.25f),new Vector3(3.45f,.205f,11.85f),threshold},5.5f,t));
            SaveClip("SanHallWalk",san,5.5f,t=>Walk(san,new[]{new Vector3(3.45f,.205f,8.25f),sanThreshold},5.5f,Mathf.Max(0,t-.5f)));
            SaveClip("RebeccaThreshold",rebecca,2.8f,t=>Stand(rebecca,threshold,85,t));SaveClip("SanThreshold",san,2.8f,t=>Stand(san,sanThreshold,48,t));
            SaveClip("RebeccaPushDoor",rebecca,2.8f,t=>{LocalStand(rebecca,t);float u=Mathf.Sin(Mathf.Clamp01(t/2.8f)*Mathf.PI);TwoBone(rebecca.Bone("upper_arm.R"),rebecca.Bone("forearm.R"),rebecca.Bone("hand.R"),new Vector3(.1f,1.03f,.3f+u*.25f),Vector3.right);rebecca.model.SetPositionAndRotation(threshold,Quaternion.Euler(0,85,0));});
            SaveClip("RebeccaShock",rebecca,3.5f,t=>{Stand(rebecca,threshold,85,t);RotateWorld(rebecca.Bone("head"),Vector3.right,Mathf.SmoothStep(0,1,t/1.3f)*9);RotateWorld(rebecca.Bone("spine.002"),Vector3.right,-3);});
        }
        static void BuildBar()
        {
            Open(BarScene);san=Character("san","Assets/resources/fbx/San/San.fbx",1.49f);rebecca=Character("rebecca","Assets/resources/3Dmodels/rebecca/rebecca.fbx",1.55f);jeremy=Character("jeremy","Assets/resources/3Dmodels/jeremy/jeremy.fbx",1.40f);BarPoses();
            Mark("sc03-outside",new Vector3(-5.442f,.215f,5.7f));
            Shot("sc03-gameplay",new Vector3(-8.5f,3.2f,1.6f),new Vector3(-5.4f,.9f,5.45f),61);
            Shot("sc03-entrance",new Vector3(-5.44f,1.72f,7.10f),new Vector3(-4.35f,1.20f,10.5f),66);
            Shot("sc03-greeting",new Vector3(-5.95f,1.65f,9.02f),new Vector3(-3.9f,1.4f,11.1f),59);
            Shot("sc03-two-jeremy",new Vector3(-1.65f,1.75f,9.05f),new Vector3(-4.12f,1.48f,10.75f),58);
            Shot("sc03-rebecca-enter",new Vector3(-5.65f,1.67f,9.7f),new Vector3(-3.5f,1.4f,11.95f),53);
            Shot("sc03-rebecca-close",new Vector3(-4.7f,1.60f,10.65f),rCounter+new Vector3(0,1.23f,0),37);
            Shot("sc03-san-close",new Vector3(-4.08f,1.85f,10.8f),barSeat+new Vector3(0,.47f,0),46);
            Shot("sc03-three",new Vector3(-6.9f,2.05f,8.15f),new Vector3(-4.0f,1.42f,10.95f),62);
            Shot("sc03-later-two",new Vector3(-2.2f,1.9f,9.1f),new Vector3(-4.8f,1.45f,10.7f),55);
            Shot("sc03-later-wide",new Vector3(-.7f,2.15f,8.4f),new Vector3(-4.45f,1.36f,10.75f),60);
            Shot("sc03-noise",new Vector3(-5.45f,1.65f,10.45f),rCounter+new Vector3(0,1.25f,0),39);
            Shot("sc03-leave",new Vector3(-6.2f,2,8.6f),new Vector3(-1.8f,1.05f,10.25f),72);
            Shot("sc03-threshold",new Vector3(3.36f,1.67f,10.75f),new Vector3(4.1f,1.1f,12.25f),56);
            Shot("sc03-over-shoulder",new Vector3(3.2f,1.67f,11.9f),new Vector3(5.85f,.48f,12.15f),60);
            Shot("sc03-shock",new Vector3(4.7f,1.58f,12.0f),threshold+new Vector3(0,1.28f,0),43);
            var track=new GameObject("PATH - sc03-private-hall").AddComponent<CutscenePath>();track.id="sc03-private-hall";track.fieldOfView=67;track.lookAt=rebecca.actor.lookAtBone;track.lookAtOffset=new Vector3(0,-.15f,0);track.knots=new[]{new Vector3(3.03f,1.93f,7.65f),new Vector3(3.03f,1.93f,8.1f),new Vector3(3.03f,1.93f,8.8f),new Vector3(3.03f,1.93f,9.5f)}.Select((p,i)=>{var t=new GameObject("Knot "+i).transform;t.SetParent(track.transform);t.position=p;return t;}).ToArray();stage.paths.Add(track);
            var door=Prop(Find("Room 02 - independent open hinged door"),"room02_door");var hinge=door.model.GetChild(0);SaveClip("RoomDoorAjar",door,1,t=>hinge.localRotation=Quaternion.Euler(0,103,0));SaveClip("RoomDoorOpen",door,2.8f,t=>hinge.localRotation=Quaternion.Euler(0,Mathf.Lerp(103,175,Mathf.SmoothStep(0,1,t/2.4f)),0));
            var entryDoor=HingedDoor("Shangrila entrance door",new Vector3(-6.02f,.215f,6.47f),1.16f,2.10f);
            var entry=Prop(entryDoor,"entrance_door");var entryHinge=entry.model.GetChild(0);
            SaveClip("EntranceDoorClosed",entry,1,t=>{});
            SaveClip("EntranceDoorEnter",entry,4.5f,t=>{float angle=t<2.7f?Mathf.Lerp(0,-102,Mathf.SmoothStep(0,1,t/1.1f)):Mathf.Lerp(-102,0,Mathf.SmoothStep(0,1,(t-2.7f)/1.3f));entryHinge.localRotation=Quaternion.Euler(0,angle,0);});
            var service=HingedDoor("Backbar service door",new Vector3(-2.0f,.215f,12.82f),.9f,2.12f);
            var serviceDoor=Prop(service,"service_door");var serviceHinge=serviceDoor.model.GetChild(0);
            SaveClip("ServiceDoorClosed",serviceDoor,1,t=>{});SaveClip("ServiceDoorEnter",serviceDoor,3.6f,t=>serviceHinge.localRotation=Quaternion.Euler(0,-Mathf.Sin(Mathf.Clamp01(t/3.6f)*Mathf.PI)*78,0));
            var bottle=Prop(Find("Rebecca bottle - pickup"),"rebecca_bottle");SaveClip("BottleHidden",bottle,1,t=>bottle.model.localScale=Vector3.one*.001f);
            SaveClip("BottleCarry",bottle,3.6f,t=>{clips["RebeccaEnter"].SampleAnimation(rebecca.root,t);var hand=rebecca.Bone("hand.R");bottle.model.position=hand.position+Vector3.up*.04f-bottle.model.GetChild(0).localPosition;});
            rebecca.RestoreRest();SaveClip("BottlePlace",bottle,2.4f,t=>{var target=rCounter+new Vector3(-.25f,1.02f,-.72f);var start=rCounter+new Vector3(-.13f,1.01f,-.32f);bottle.model.position=Vector3.Lerp(start,target,Mathf.SmoothStep(0,1,t/1.4f))-bottle.model.GetChild(0).localPosition;});
            var liquid=Find("Partly emptied drink");liquid.transform.parent.gameObject.SetActive(true);var timeProps=Prop(liquid,"later_drink");SaveClip("DrinkBefore",timeProps,1,t=>timeProps.model.localScale=Vector3.one*.001f);SaveClip("DrinkLater",timeProps,1,t=>{});
            var seq=ScriptableObject.CreateInstance<CutsceneSequence>();seq.sequenceId="sc03-shangrila-reunion";seq.fadeDuration=.6f;seq.turns=BarTurns();Store(seq,BarSequence);seq=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(BarSequence);
            clips["JeremyCounterIdle"].SampleAnimation(jeremy.root,0);clips["RebeccaHidden"].SampleAnimation(rebecca.root,0);clips["BottleHidden"].SampleAnimation(bottle.root,0);clips["RoomDoorAjar"].SampleAnimation(door.root,0);clips["DrinkBefore"].SampleAnimation(timeProps.root,0);clips["EntranceDoorClosed"].SampleAnimation(entry.root,0);clips["ServiceDoorClosed"].SampleAnimation(serviceDoor.root,0);
            var playback=Configure(seq,false,new Vector3(-5.442f,.215f,4.1f),0);playback.entrance=stage.FindSpawn("sc03-outside");
            Finish(seq,BarScene,"sc03-gameplay");
        }
        static GameObject HingedDoor(string name,Vector3 position,float width,float height)
        {
            var root=new GameObject(name);root.transform.position=position;var leaf=GameObject.CreatePrimitive(PrimitiveType.Cube);leaf.name="Independent hinged leaf";leaf.transform.SetParent(root.transform,false);leaf.transform.localPosition=new Vector3(width*.5f,height*.5f,0);leaf.transform.localScale=new Vector3(width,height,.055f);
            string path=Folder+"/Materials/Prologue dark oak doors.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.13f,.085f,.055f));mat.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(mat,path);}leaf.GetComponent<Renderer>().sharedMaterial=mat;
            var handle=GameObject.CreatePrimitive(PrimitiveType.Cube);handle.name="Handle";handle.transform.SetParent(root.transform,false);handle.transform.localPosition=new Vector3(width*.87f,1.02f,-.06f);handle.transform.localScale=new Vector3(.11f,.035f,.055f);handle.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(handle.GetComponent<Collider>());return root;
        }
    }
}
