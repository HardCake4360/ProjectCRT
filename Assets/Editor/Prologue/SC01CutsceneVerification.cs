using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ProjectCRT.Prologue.Editor
{
    public static class SC01CutsceneVerification
    {
        public static void BatchPreview()
        {
            try
            {
                Directory.CreateDirectory("Logs/SC01/Previews");EditorSceneManager.OpenScene(SC01CutsceneAuthoring.ScenePath);
                var stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var sequence=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SC01CutsceneAuthoring.SequencePath);
                var errors=stage.Validate(sequence);if(errors.Count>0)throw new Exception(string.Join("\n",errors));
                var lines=new List<string>();
                var transforms=stage.actors.SelectMany(a=>a.GetComponentsInChildren<Transform>(true)).Distinct().ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
                stage.Capture(stage.output,true);
                var view=stage.GetComponent<CutsceneView>();view.Begin();
                var canvas=stage.GetComponentInChildren<Canvas>(true);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=stage.output;canvas.planeDistance=.15f;
                foreach(var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)){ps.Simulate(1.2f,true,true);ps.Pause();}
                for(int i=0;i<sequence.turns.Count;i++)
                {
                    var turn=sequence.turns[i];float time=turn.hasDialogue?stage.CameraDuration(turn)+1.8f:Mathf.Max(.1f,stage.PreviewDuration(turn)*.65f);
                    stage.Reconstruct(sequence,i,time);view.SetLine(CutsceneTextUtility.SpeakerName(turn,stage),turn.text,turn.dialogueKind);view.Reveal(9999);
                    foreach(var group in canvas.GetComponentsInChildren<CanvasGroup>(true))group.alpha=group.name=="Dialogue"&&turn.hasDialogue?1:0;
                    Canvas.ForceUpdateCanvases();stage.output.scene=stage.gameObject.scene;
                    lines.Add(i+" | "+turn.id+" | "+turn.text+" | San head="+stage.FindActor("san").lookAtBone.position.ToString("F3"));
                    Capture(stage.output,$"{i+1:00}-{turn.id}");
                }
                stage.Restore();
                foreach(var pair in transforms)
                    if(Vector3.Distance(pair.Key.localPosition,pair.Value.Item1)>.0001f||Quaternion.Angle(pair.Key.localRotation,pair.Value.Item2)>.05f||Vector3.Distance(pair.Key.localScale,pair.Value.Item3)>.0001f)throw new Exception("Preview did not restore "+pair.Key.name);
                lines.Add("PASS: all turns reconstructed and rendered; captured actor transforms restored.");
                File.WriteAllLines("Logs/SC01/preview-validation.txt",lines);File.WriteAllText("Logs/SC01/preview-status.txt","PASS");EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText("Logs/SC01/preview-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        internal static void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1280,720,24);target.Create();var before=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
                var png=new Texture2D(1280,720,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();File.WriteAllBytes("Logs/SC01/Previews/"+name+".png",png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);
            }
            finally{RenderTexture.active=before;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
