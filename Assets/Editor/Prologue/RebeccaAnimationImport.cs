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
    public static partial class RebeccaAnimationImport
    {
        public const string Source="Assets/ThirdParty/CharacterAnimations/QuaterniusUAL1/Source/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx";
        public const string Folder="Assets/Settings/Prologue/RebeccaAnimationTest";
        public const string Source2="Assets/ThirdParty/CharacterAnimations/QuaterniusUAL2/Source/Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx";
        public const string MocapFolder="Assets/ThirdParty/CharacterAnimations/AndersonRohrCrowdReactions/Source/mocap/iClone/";
        public static readonly string[] MocapSources={MocapFolder+"16_Scratch_Neck_iclone.fbx",MocapFolder+"21_Holding_Cup_Nodding_iclone.fbx",MocapFolder+"22_Pointing_And_Laughing_iclone.fbx"};
        public static void BatchMocapAudit()
        {
            try{
                Directory.CreateDirectory("Logs/RebeccaAnimationAssets");AssetDatabase.Refresh();var report=new List<string>();
                foreach(var path in MocapSources){var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();report.Add(path+" avatar="+(avatar!=null?$"valid {avatar.isValid} human {avatar.isHuman}":"none"));foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")))report.Add("CLIP "+clip.name+" length="+clip.length+" human="+clip.isHumanMotion);}
                File.WriteAllLines("Logs/RebeccaAnimationAssets/mocap-rig-audit.txt",report);File.WriteAllText("Logs/RebeccaAnimationAssets/mocap-audit-status.txt","PASS");EditorApplication.Exit(0);
            }catch(Exception e){File.WriteAllText("Logs/RebeccaAnimationAssets/mocap-audit-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchSamples()
        {
            try{
                Directory.CreateDirectory("Logs/RebeccaAnimationAssets/Previews");AssetDatabase.Refresh();
                foreach(var path in new[]{Source,Source2}){var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();}
                EditorSceneManager.OpenScene(SC23CutsceneAuthoring.BarScene);var stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var actor=stage.FindActor("rebecca");var model=actor.transform.Find("Model");model.localScale=Vector3.one;model.SetPositionAndRotation(new Vector3(-4.6f,.215f,11.93f),Quaternion.Euler(0,180,0));
                actor.animator.enabled=false;var animator=model.GetComponentInChildren<Animator>(true);animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
                var restPos=animator.transform.localPosition;var restRot=animator.transform.localRotation;var restScale=animator.transform.localScale;
                var graph=PlayableGraph.Create("Rebecca free motion audition");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var output=AnimationPlayableOutput.Create(graph,"Humanoid retarget",animator);graph.Play();
                var lines=new List<string>();
                foreach(var pair in new[]{(Source,"Idle_Loop"),(Source,"Idle_Talking_Loop"),(Source,"Walk_Loop"),(Source2,"Idle_Rail_Loop"),(Source2,"Yes"),(Source2,"Idle_No_Loop")}){
                    var clip=AssetDatabase.LoadAllAssetsAtPath(pair.Item1).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__")&&c.name.EndsWith("|"+pair.Item2));var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);output.SetSourcePlayable(playable);playable.SetTime(clip.length*.35f);graph.Evaluate(0);
                    animator.transform.localPosition=restPos;animator.transform.localRotation=restRot;animator.transform.localScale=restScale;
                    var head=animator.GetBoneTransform(HumanBodyBones.Head);var l=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var r=animator.GetBoneTransform(HumanBodyBones.RightFoot);lines.Add(pair.Item2+" head="+head.position+" feet="+l.position+" / "+r.position);
                    stage.FindShot("sc03-rebecca-close").Sample().Apply(stage.output);SC23Verification.Capture(stage.output,"Rebecca-sample-"+pair.Item2);playable.Destroy();
                }
                graph.Destroy();File.WriteAllLines("Logs/RebeccaAnimationAssets/sample-metrics.txt",lines);File.WriteAllText("Logs/RebeccaAnimationAssets/sample-status.txt","PASS");EditorApplication.Exit(0);
            }catch(Exception e){File.WriteAllText("Logs/RebeccaAnimationAssets/sample-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BatchAudit()
        {
            try{
                Directory.CreateDirectory("Logs/RebeccaAnimationAssets");AssetDatabase.Refresh();var importer=(ModelImporter)AssetImporter.GetAtPath(Source);importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();
                var donor=AssetDatabase.LoadAssetAtPath<GameObject>(Source);var avatar=AssetDatabase.LoadAllAssetsAtPath(Source).OfType<Avatar>().FirstOrDefault();var lines=new List<string>{"DONOR AVATAR "+(avatar!=null?$"{avatar.name} valid={avatar.isValid} human={avatar.isHuman}":"missing")};
                foreach(var c in AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")))lines.Add("CLIP "+c.name+" length="+c.length+" human="+c.isHumanMotion);
                EditorSceneManager.OpenScene(SC23CutsceneAuthoring.BarScene);var actor=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>().FindActor("rebecca");
                foreach(var a in actor.GetComponentsInChildren<Animator>(true))lines.Add("TARGET ANIMATOR "+a.name+" avatar="+(a.avatar!=null?$"{a.avatar.name} valid={a.avatar.isValid} human={a.avatar.isHuman}":"none"));
                foreach(var t in donor.GetComponentsInChildren<Transform>(true))lines.Add("DONOR BONE "+t.name);
                File.WriteAllLines("Logs/RebeccaAnimationAssets/rig-audit.txt",lines);File.WriteAllText("Logs/RebeccaAnimationAssets/audit-status.txt","PASS");EditorApplication.Exit(0);
            }catch(Exception e){File.WriteAllText("Logs/RebeccaAnimationAssets/audit-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
