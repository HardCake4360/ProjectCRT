using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ProjectCRT.Cutscenes;
namespace ProjectCRT.Prologue.Editor
{
    /// <summary>Offline Humanoid retargeting, baked back to the existing reversible generic cutscene rig.</summary>
    public static partial class RebeccaAnimationImport
    {
        struct Pose
        {
            public Transform t;public Vector3 p,s;public Quaternion q;
            public void Apply(){t.localPosition=p;t.localRotation=q;t.localScale=s;}
        }
        static Pose[] Snapshot(Transform model)=>model.GetComponentsInChildren<Transform>(true).Select(t=>new Pose{t=t,p=t.localPosition,q=t.localRotation,s=t.localScale}).ToArray();
        static void Restore(Pose[] pose){foreach(var p in pose)p.Apply();}
        static readonly Dictionary<string,AnimationClip> motions=new();
        static readonly List<string> motionReport=new();
        static AnimationClip Motion(string id)
        {
            if(motions.TryGetValue(id,out var clip))return clip;
            var mocap=id=="MocapScratch"?MocapSources[0]:id=="MocapAgree"?MocapSources[1]:id=="MocapLaugh"?MocapSources[2]:null;
            if(mocap!=null){clip=AssetDatabase.LoadAllAssetsAtPath(mocap).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__")&&c.isHumanMotion);motions[id]=clip;return clip;}
            foreach(var path in new[]{Source,Source2}){clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__")&&c.name.EndsWith("|"+id));if(clip!=null){motions[id]=clip;return clip;}}
            throw new Exception("Downloaded animation is missing: "+id);
        }
        public static void BatchBuild()
        {
            try{Build();File.WriteAllText("Logs/RebeccaAnimationAssets/build-status.txt","PASS");EditorApplication.Exit(0);}
            catch(Exception e){File.WriteAllText("Logs/RebeccaAnimationAssets/build-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchBuildAndPreview()
        {
            try{Build();File.WriteAllText("Logs/RebeccaAnimationAssets/build-status.txt","PASS");}
            catch(Exception e){File.WriteAllText("Logs/RebeccaAnimationAssets/build-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);return;}
            RebeccaAnimationVerification.BatchPreview();
        }
        public static void Build()
        {
            Directory.CreateDirectory("Logs/RebeccaAnimationAssets");Directory.CreateDirectory(Folder+"/Animation");AssetDatabase.Refresh();motions.Clear();motionReport.Clear();
            foreach(var path in new[]{Source,Source2}.Concat(MocapSources)){var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(importer.animationType!=ModelImporterAnimationType.Human){importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();}var a=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();if(a==null||!a.isHuman||!a.isValid)throw new Exception("Source is not a valid Humanoid: "+path);}
            EditorSceneManager.OpenScene(SC23CutsceneAuthoring.BarScene);var stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var sequence=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SC23CutsceneAuthoring.BarSequence);var actor=stage.FindActor("rebecca");
            var originalScenePose=Snapshot(actor.transform);var model=actor.transform.Find("Model");model.localScale=Vector3.one;model.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var human=model.GetComponentsInChildren<Animator>(true).Single(a=>a.avatar!=null&&a.avatar.isHuman);actor.animator.enabled=false;human.enabled=true;human.applyRootMotion=false;human.fireEvents=false;human.cullingMode=AnimatorCullingMode.AlwaysAnimate;human.Rebind();
            var rest=Snapshot(model);var rootPose=rest.Single(p=>p.t==human.transform);var hips=human.GetBoneTransform(HumanBodyBones.Hips);
            var spine=human.GetBoneTransform(HumanBodyBones.Spine);var head=human.GetBoneTransform(HumanBodyBones.Head);
            var left=human.GetBoneTransform(HumanBodyBones.LeftFoot);var right=human.GetBoneTransform(HumanBodyBones.RightFoot);
            var graph=PlayableGraph.Create("Rebecca offline Humanoid bake");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var output=AnimationPlayableOutput.Create(graph,"Retarget original Rebecca",human);graph.Play();
            var playables=new Dictionary<AnimationClip,AnimationClipPlayable>();
            void Sample(AnimationClip clip,float time)
            {
                if(!playables.TryGetValue(clip,out var playable)){playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);playables[clip]=playable;}
                output.SetSourcePlayable(playable);playable.SetTime(Mathf.Clamp(time,0,clip.length));graph.Evaluate(0);rootPose.Apply();
            }
            AnimationClip Bake(string name,string sourceId,float duration=0,float strength=1,bool preserveRightArm=false,string variant="",string outputName=null,float sourceOffset=0)
            {
                var motion=Motion(sourceId);var placement=AssetDatabase.LoadAssetAtPath<AnimationClip>(SC23CutsceneAuthoring.Folder+"/Animation/"+name+".anim");if(placement==null)throw new Exception("Missing authored placement "+name);
                float length=duration>0?duration:placement.length;int count=Mathf.CeilToInt(length*30)+1;var frames=new Pose[count][];
                Restore(rest);Sample(Motion("Idle_Loop"),0);var idlePose=Snapshot(model);
                int cycles=Mathf.Max(1,Mathf.RoundToInt(length/motion.length));
                if(sourceId.StartsWith("Walk")){
                    float min=float.PositiveInfinity,max=float.NegativeInfinity;
                    for(int k=0;k<=30;k++){Restore(rest);Sample(motion,k*motion.length/30);float z=left.position.z-hips.position.z;min=Mathf.Min(min,z);max=Mathf.Max(max,z);}
                    float distance=0;Vector3 previous=Vector3.zero;
                    for(int k=0;k<=90;k++){Restore(rest);placement.SampleAnimation(actor.gameObject,k*length/90);if(k>0)distance+=Vector3.Distance(previous,model.position);previous=model.position;}
                    float stride=Mathf.Max(.35f,2*(max-min));cycles=Mathf.Max(1,Mathf.RoundToInt(distance/stride));motionReport.Add(name+" stride="+stride+" path="+distance+" cycles="+cycles);
                }
                for(int i=0;i<count;i++){
                    float t=i*length/(count-1);Restore(rest);placement.SampleAnimation(actor.gameObject,t);var position=model.position;var rotation=model.rotation;var oldPose=Snapshot(model);
                    model.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.localScale=Vector3.one;
                    float sampleTime=sourceId.Contains("Loop")?Mathf.Repeat(t/length*cycles*motion.length,motion.length):Mathf.Min(sourceOffset+t,motion.length);
                    Sample(motion,sampleTime);
                    float amount=strength;
                    if(sourceId.StartsWith("Mocap"))amount*=Mathf.SmoothStep(0,1,Mathf.Min(t/.22f,(length-t)/.3f));
                    if(amount<1){var sampled=Snapshot(model);for(int b=0;b<sampled.Length;b++){var p=sampled[b];if(p.t==model||p.t==human.transform)continue;p.t.localRotation=Quaternion.Slerp(idlePose[b].q,p.q,amount);p.t.localPosition=Vector3.Lerp(idlePose[b].p,p.p,amount);}}
                    // Only the actor placement owns translation; preserve the natural small hip shift.
                    float footHeight=Mathf.Min(left.position.y,right.position.y);hips.position+=Vector3.up*(.055f-footHeight);
                    if(preserveRightArm){
                        foreach(var p in oldPose)if(UnderRightArm(p.t))p.Apply();
                        var upper=human.GetBoneTransform(HumanBodyBones.RightUpperArm);float sign=Mathf.Sign(upper.position.x);
                        ArmIK(upper,human.GetBoneTransform(HumanBodyBones.RightLowerArm),human.GetBoneTransform(HumanBodyBones.RightHand),new Vector3(sign*.13f,.97f,.32f),Vector3.right*sign);
                    }
                    if(variant=="listen")head.localRotation=Quaternion.Slerp(idlePose.First(p=>p.t==head).q,head.localRotation,.38f);
                    if(variant=="lean-talk"){
                        var talking=Snapshot(model);Sample(Motion("Idle_Rail_Loop"),Mathf.Repeat(t,Motion("Idle_Rail_Loop").length));var leaned=Snapshot(model);
                        for(int b=0;b<leaned.Length;b++){var p=leaned[b];if(p.t==model||p.t==human.transform)continue;if(p.t==spine||p.t.IsChildOf(spine)){p.t.localRotation=Quaternion.Slerp(p.q,talking[b].q,.36f);p.t.localPosition=Vector3.Lerp(p.p,talking[b].p,.36f);}else talking[b].Apply();}
                        footHeight=Mathf.Min(left.position.y,right.position.y);hips.position+=Vector3.up*(.055f-footHeight);
                    }
                    if(sourceId=="Idle_Rail_Loop"||variant=="lean-talk"){
                        var upper=human.GetBoneTransform(HumanBodyBones.RightUpperArm);var forearm=human.GetBoneTransform(HumanBodyBones.RightLowerArm);var hand=human.GetBoneTransform(HumanBodyBones.RightHand);float sign=Mathf.Sign(upper.position.x);ArmIK(upper,forearm,hand,new Vector3(sign*.18f,.99f,.43f),Vector3.right*sign);
                    }
                    if(variant=="nod-only"){
                        var reaction=Snapshot(model);foreach(var p in idlePose)if(p.t!=model&&p.t!=human.transform)p.Apply();
                        foreach(var p in reaction)if(p.t==head||p.t.name.StartsWith("neck"))p.Apply();
                    }
                    model.SetPositionAndRotation(position,rotation);model.localScale=Vector3.one;frames[i]=Snapshot(model);
                }
                var baked=SaveBaked(outputName??name,actor,frames,length);motionReport.Add((outputName??name)+" <= "+Path.GetFileName(AssetDatabase.GetAssetPath(motion))+" :: "+motion.name+" | duration="+length+" | strength="+strength+" | right arm contact="+preserveRightArm);return baked;
            }
            var replacements=new Dictionary<AnimationClip,AnimationClip>();
            void Replace(string name,string source,float strength=1,bool rightArm=false,string variant="")
            {
                var original=AssetDatabase.LoadAssetAtPath<AnimationClip>(SC23CutsceneAuthoring.Folder+"/Animation/"+name+".anim");replacements[original]=Bake(name,source,0,strength,rightArm,variant);
            }
            try{
                Replace("RebeccaCounterIdle","Idle_Loop",.75f);Replace("RebeccaCounterTalk","Idle_Talking_Loop",.72f);Replace("RebeccaLean","Idle_Rail_Loop",.62f);Replace("RebeccaLeanTalk","Idle_Talking_Loop",.65f,false,"lean-talk");
                Replace("RebeccaEnter","Walk_Loop",1,true);Replace("RebeccaLeaveCounter","Walk_Loop");Replace("RebeccaHallWalk","Walk_Formal_Loop");
                Replace("RebeccaThreshold","Idle_Loop",.35f,false,"listen");
                var nod=Bake("RebeccaCounterTalk","Yes",1.45f,.65f,outputName:"RebeccaBriefAgree");
                var no=Bake("RebeccaCounterTalk","Idle_No_Loop",1.9f,.6f,outputName:"RebeccaBriefNo");
                var scratch=Bake("RebeccaCounterTalk","MocapScratch",2.9f,.68f,outputName:"RebeccaNeckScratch",sourceOffset:Motion("MocapScratch").length*.3f);
                var laugh=Bake("RebeccaCounterTalk","MocapLaugh",2.7f,.52f,outputName:"RebeccaLightLaugh",sourceOffset:Motion("MocapLaugh").length*.32f);
                var warmNod=Bake("RebeccaCounterTalk","MocapAgree",2.3f,.75f,variant:"nod-only",outputName:"RebeccaWarmAgree",sourceOffset:Motion("MocapAgree").length*.33f);
                var carryTalk=Bake("RebeccaCounterTalk","Idle_Talking_Loop",strength:.72f,preserveRightArm:true,outputName:"RebeccaCounterCarryTalk");
                var carryIdle=Bake("RebeccaCounterIdle","Idle_Loop",strength:.75f,preserveRightArm:true,outputName:"RebeccaCounterCarryIdle");
                foreach(var turn in sequence.turns)foreach(var cue in turn.actors.Where(c=>c.actorId=="rebecca"&&c.clip!=null))if(replacements.TryGetValue(cue.clip,out var updated))cue.clip=updated;
                foreach(var pose in actor.poses)if(pose.clip!=null&&replacements.TryGetValue(pose.clip,out var updated))pose.clip=updated;
                bool holdingBottle=false;
                foreach(var turn in sequence.turns){
                    if(turn.id=="sc03-rebecca-enter"){holdingBottle=true;continue;}
                    if(turn.actors.Any(c=>c.actorId=="rebecca"&&c.clip!=null&&c.clip.name=="RebeccaPlaceBottle"))holdingBottle=false;
                    if(holdingBottle)foreach(var cue in turn.actors.Where(c=>c.actorId=="rebecca"&&c.clip!=null)){
                        if(cue.clip.name=="RebeccaCounterTalk")cue.clip=carryTalk;
                        if(cue.clip.name=="RebeccaCounterIdle")cue.clip=carryIdle;
                    }
                }
                foreach(var turn in sequence.turns.Where(t=>t.hasDialogue&&t.speakerId=="rebecca")){
                    var cue=turn.actors.FirstOrDefault(c=>c.actorId=="rebecca");if(cue==null)continue;
                    if(turn.text=="응. 꽤 오래된 친구야."||turn.text=="맞아. 성실한 애야. 가끔은 너무 성실해서 탈이지만."){cue.clip=nod;cue.loop=false;}
                    if(turn.text=="말린다고 들었을 것 같아?"){cue.clip=no;cue.loop=false;}
                    if(turn.text=="왜, 안 어울려?"){cue.clip=scratch;cue.loop=false;turn.waitForActors=true;}
                    if(turn.text=="참 너답네."||turn.text=="그럴 줄 알았어."){cue.clip=laugh;cue.loop=false;turn.waitForActors=true;}
                    if(turn.text=="너라면 잘할 거야."){cue.clip=warmNod;cue.loop=false;}
                }
                // The carry prop follows the retargeted original right hand, rather than the previous take.
                var bottle=stage.FindActor("rebecca_bottle");var bottleRoot=bottle.transform.Find("Model");var bottlePose=Snapshot(bottleRoot);
                AnimationClip BottleTrack(string name,AnimationClip body){
                    var frames=new Pose[Mathf.CeilToInt(body.length*30)+1][];
                    for(int i=0;i<frames.Length;i++){float t=i*body.length/(frames.Length-1);Restore(rest);body.SampleAnimation(actor.gameObject,t);Restore(bottlePose);bottleRoot.localScale=Vector3.one;bottleRoot.position=human.GetBoneTransform(HumanBodyBones.RightHand).position+Vector3.up*.04f-bottleRoot.GetChild(0).localPosition;frames[i]=Snapshot(bottleRoot);}
                    return SaveBaked(name,bottle,frames,body.length);
                }
                var carry=BottleTrack("BottleCarry",replacements[AssetDatabase.LoadAssetAtPath<AnimationClip>(SC23CutsceneAuthoring.Folder+"/Animation/RebeccaEnter.anim")]);
                var hold=BottleTrack("BottleCounterHold",carryTalk);
                var holdIdle=BottleTrack("BottleCounterHoldIdle",carryIdle);
                foreach(var turn in sequence.turns){
                    foreach(var cue in turn.actors.Where(c=>c.actorId=="rebecca_bottle"&&c.clip!=null&&c.clip.name=="BottleCarry"))cue.clip=carry;
                    var holdingBody=turn.actors.FirstOrDefault(c=>c.actorId=="rebecca"&&(c.clip==carryTalk||c.clip==carryIdle));
                    if(holdingBody!=null){turn.actors.RemoveAll(c=>c.actorId=="rebecca_bottle");turn.actors.Add(new CutsceneActorCue{actorId="rebecca_bottle",clip=holdingBody.clip==carryTalk?hold:holdIdle,loop=true});}
                }
                Restore(bottlePose);EditorUtility.SetDirty(sequence);
            }finally{graph.Destroy();human.enabled=false;actor.animator.enabled=true;Restore(originalScenePose);}
            var errors=stage.Validate(sequence);if(errors.Count>0)throw new Exception(string.Join("\n",errors));EditorUtility.SetDirty(sequence);EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);EditorSceneManager.SaveScene(stage.gameObject.scene);AssetDatabase.SaveAssets();File.WriteAllLines("Logs/RebeccaAnimationAssets/motion-mapping.txt",motionReport);
        }
        static bool UnderRightArm(Transform t){for(var p=t;p!=null;p=p.parent)if(p.name=="upper_arm.R")return true;return false;}
        static void ArmIK(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole)
        {
            var origin=upper.position;var delta=target-origin;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);var forward=delta.normalized;var bend=Vector3.ProjectOnPlane(pole,forward).normalized;float along=(a*a+d*d-b*b)/(2*d);var elbow=origin+forward*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-origin)*upper.rotation;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,origin+forward*d-lower.position)*lower.rotation;
        }
        static AnimationClip SaveBaked(string name,CutsceneActor actor,Pose[][] frames,float length)
        {
            var clip=new AnimationClip{name=name,frameRate=30};
            for(int b=0;b<frames[0].Length;b++){
                string path=AnimationUtility.CalculateTransformPath(frames[0][b].t,actor.transform);
                for(int channel=0;channel<10;channel++){
                    var values=new float[frames.Length];var previous=frames[0][b].q;
                    for(int i=0;i<frames.Length;i++){var p=frames[i][b];var q=p.q;if(Quaternion.Dot(previous,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;values[i]=channel<3?p.p[channel]:channel<7?q[channel-3]:p.s[channel-7];}
                    bool constant=values.All(v=>Mathf.Abs(v-values[0])<.0000001f);var keys=constant?new[]{new Keyframe(0,values[0]),new Keyframe(length,values[0])}:values.Select((v,i)=>new Keyframe(i*length/(values.Length-1),v)).ToArray();var curve=new AnimationCurve(keys);
                    for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
                    string property=channel<3?"m_LocalPosition."+"xyz"[channel]:channel<7?"m_LocalRotation."+"xyzw"[channel-3]:"m_LocalScale."+"xyz"[channel-7];AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
                }
            }
            clip.EnsureQuaternionContinuity();string asset=Folder+"/Animation/"+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);if(existing==null){AssetDatabase.CreateAsset(clip,asset);return clip;}EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);return existing;
        }
    }
}
