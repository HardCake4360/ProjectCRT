using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectCRT.Cutscenes;
namespace ProjectCRT.Prologue.Editor
{
    public static class RebeccaAnimationVerification
    {
        public static void BatchPreview()
        {
            try{
                Directory.CreateDirectory("Logs/RebeccaAnimationAssets/Previews");EditorSceneManager.OpenScene(SC23CutsceneAuthoring.BarScene);var stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var seq=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SC23CutsceneAuthoring.BarSequence);var actor=stage.FindActor("rebecca");var errors=stage.Validate(seq);if(errors.Count>0)throw new Exception(string.Join("\n",errors));
                var original=actor.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));var lines=new List<string>();
                CheckBakedMotion(stage,seq,lines);
                foreach(var p in original){p.Key.localPosition=p.Value.Item1;p.Key.localRotation=p.Value.Item2;p.Key.localScale=p.Value.Item3;}
                stage.Capture(stage.output,true);var view=stage.GetComponent<CutsceneView>();view.Begin();var canvas=stage.GetComponentInChildren<Canvas>(true);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=stage.output;canvas.planeDistance=.15f;
                var selected=new HashSet<string>{"sc03-rebecca-enter","sc03-d13","sc03-d16","sc03-d22","sc03-d37","sc03-d39","sc03-d50","sc03-d60","sc03-leave-counter","sc03-hall-walk","sc03-door-open","sc03-d76"};
                foreach(var turn in seq.turns.Where(t=>t.hasDialogue&&t.speakerId=="rebecca"&&new[]{"참 너답네.","그럴 줄 알았어.","왜, 안 어울려?","너라면 잘할 거야."}.Contains(t.text)))selected.Add(turn.id);
                for(int i=0;i<seq.turns.Count;i++){
                    var turn=seq.turns[i];float time=turn.hasDialogue?stage.CameraDuration(turn)+1.5f:stage.PreviewDuration(turn)*.7f;stage.Reconstruct(seq,i,time);view.SetLine(CutsceneTextUtility.SpeakerName(turn,stage),turn.text,turn.dialogueKind);view.Reveal(9999);foreach(var g in canvas.GetComponentsInChildren<CanvasGroup>(true))g.alpha=g.name=="Dialogue"&&turn.hasDialogue?1:0;Canvas.ForceUpdateCanvases();
                    if(selected.Contains(turn.id)){SC23Verification.Capture(stage.output,"Rebecca-new-"+turn.id);lines.Add(turn.id+" head="+actor.lookAtBone.position+" frame="+stage.output.WorldToViewportPoint(actor.lookAtBone.position)+" clip="+string.Join(",",turn.actors.Where(c=>c.actorId=="rebecca"&&c.clip!=null).Select(c=>c.clip.name)));}
                    foreach(var t in actor.GetComponentsInChildren<Transform>(true))if(!float.IsFinite(t.position.x)||!float.IsFinite(t.position.y)||!float.IsFinite(t.position.z))throw new Exception("Invalid animation transform: "+t.name);
                }
                stage.Restore();foreach(var p in original)if(Vector3.Distance(p.Key.localPosition,p.Value.Item1)>.0001f||Quaternion.Angle(p.Key.localRotation,p.Value.Item2)>.05f||Vector3.Distance(p.Key.localScale,p.Value.Item3)>.0001f)throw new Exception("Preview restoration failed: "+p.Key.name);
                File.WriteAllLines("Logs/RebeccaAnimationAssets/preview-validation.txt",lines);File.WriteAllText("Logs/RebeccaAnimationAssets/preview-status.txt","PASS");EditorApplication.Exit(0);
            }catch(Exception e){File.WriteAllText("Logs/RebeccaAnimationAssets/preview-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void CheckBakedMotion(CutsceneStage stage,CutsceneSequence sequence,List<string> lines)
        {
            var actor=stage.FindActor("rebecca");var model=actor.transform.Find("Model");
            var nodes=actor.GetComponentsInChildren<Transform>(true);
            var saved=nodes.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            void Reset(){foreach(var p in saved){p.Key.localPosition=p.Value.Item1;p.Key.localRotation=p.Value.Item2;p.Key.localScale=p.Value.Item3;}}
            var clips=AssetDatabase.FindAssets("t:AnimationClip",new[]{RebeccaAnimationImport.Folder}).Select(id=>AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
            if(clips.Length!=18)throw new Exception("Expected 15 Rebecca motions and three bottle tracks, found "+clips.Length);
            foreach(var clip in clips.Where(c=>!c.name.StartsWith("Bottle"))){
                var bindings=AnimationUtility.GetCurveBindings(clip);
                if(clip.isHumanMotion||!bindings.Any(b=>b.path.Contains("metarig")&&AnimationUtility.GetEditorCurve(clip,b).keys.Any(k=>Mathf.Abs(k.value-AnimationUtility.GetEditorCurve(clip,b).keys[0].value)>.001f)))throw new Exception("Motion was not baked onto Rebecca's existing bones: "+clip.name);
                var original=AssetDatabase.LoadAssetAtPath<AnimationClip>(SC23CutsceneAuthoring.Folder+"/Animation/"+clip.name+".anim");
                if(original!=null)foreach(float t in new[]{0f,clip.length*.5f,clip.length}){
                    Reset();original.SampleAnimation(actor.gameObject,t);var position=model.position;var rotation=model.rotation;
                    Reset();clip.SampleAnimation(actor.gameObject,t);
                    if(Vector3.Distance(position,model.position)>.002f||Quaternion.Angle(rotation,model.rotation)>.1f)throw new Exception("Authored placement changed: "+clip.name);
                }
            }
            var carry=clips.Single(c=>c.name=="BottleCarry");var enter=clips.Single(c=>c.name=="RebeccaEnter");var bottle=stage.FindActor("rebecca_bottle");var bottleModel=bottle.transform.Find("Model");
            var bottleSaved=bottle.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            var hand=nodes.Single(t=>t.name=="hand.R");float error=0;
            for(int i=0;i<=36;i++){
                Reset();enter.SampleAnimation(actor.gameObject,i*.1f);carry.SampleAnimation(bottle.gameObject,i*.1f);
                error=Mathf.Max(error,Vector3.Distance(bottleModel.GetChild(0).position,hand.position+Vector3.up*.04f));
            }
            foreach(var clip in clips.Where(c=>c.name.StartsWith("RebeccaCounterCarry")))for(int i=0;i<=28;i++){
                Reset();clip.SampleAnimation(actor.gameObject,i*.1f);clips.Single(c=>c.name==(clip.name.EndsWith("Idle")?"BottleCounterHoldIdle":"BottleCounterHold")).SampleAnimation(bottle.gameObject,i*.1f);
                error=Mathf.Max(error,Vector3.Distance(bottleModel.GetChild(0).position,hand.position+Vector3.up*.04f));
            }
            foreach(var p in bottleSaved){p.Key.localPosition=p.Value.Item1;p.Key.localRotation=p.Value.Item2;p.Key.localScale=p.Value.Item3;}
            Reset();if(error>.005f)throw new Exception("Bottle lost the carrying hand: "+error);
            foreach(var expected in new[]{("sc03-d20","RebeccaLightLaugh"),("sc03-d37","RebeccaLightLaugh"),("sc03-d42","RebeccaNeckScratch"),("sc03-d61","RebeccaWarmAgree")})
                if(!sequence.turns.Single(t=>t.id==expected.Item1).actors.Any(c=>c.actorId=="rebecca"&&c.clip!=null&&c.clip.name==expected.Item2&&!c.loop))throw new Exception("Reaction cue missing: "+expected.Item1);
            lines.Add("PASS: 15 baked bone motions; preserved authored walking/standing placement; four mocap reaction cues; bottle maximum hand error during entry and early dialogue="+error);
        }
    }
}
