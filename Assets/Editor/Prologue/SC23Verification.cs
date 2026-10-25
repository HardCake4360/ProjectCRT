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
namespace ProjectCRT.Prologue.Editor
{
    public static class SC23Verification
    {
        [Serializable]class ExpectedLines{public ExpectedLine[] lines;}
        [Serializable]class ExpectedLine{public string text,speaker;}
        public static void BatchBuildAndPreview(){SC23CutsceneAuthoring.BuildRoomAndBar();BatchPreview();}
        public static void BatchPreview()
        {
            try{
                Directory.CreateDirectory("Logs/SC23/Previews");var checks=new List<string>();
                foreach(bool room in new[]{true,false}){
                    EditorSceneManager.OpenScene(room?SC23CutsceneAuthoring.RoomScene:SC23CutsceneAuthoring.BarScene);var stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var seq=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(room?SC23CutsceneAuthoring.RoomSequence:SC23CutsceneAuthoring.BarSequence);
                    var errors=stage.Validate(seq);if(errors.Count>0)throw new Exception(string.Join("\n",errors));
                    var source=JsonUtility.FromJson<ExpectedLines>(File.ReadAllText(SC23CutsceneAuthoring.Folder+(room?"/SC02_DialogueSource.json":"/SC03_DialogueSource.json")));var authored=seq.turns.Where(t=>t.hasDialogue).ToArray();if(authored.Length!=source.lines.Length)throw new Exception("Original dialogue count changed");for(int i=0;i<authored.Length;i++)if(authored[i].text!=source.lines[i].text||authored[i].speakerId!=source.lines[i].speaker)throw new Exception("Original dialogue changed at "+i);
                    var poses=stage.actors.SelectMany(a=>a.GetComponentsInChildren<Transform>(true)).Distinct().ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
                    Capture(stage.output,room?"SC02-gameplay":"SC03-gameplay");
                    stage.Capture(stage.output,true);var view=stage.GetComponent<CutsceneView>();view.Begin();var canvas=stage.GetComponentInChildren<Canvas>(true);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=stage.output;canvas.planeDistance=.15f;
                    for(int i=0;i<seq.turns.Count;i++){
                        var turn=seq.turns[i];float t=turn.hasDialogue?stage.CameraDuration(turn)+1.8f:Mathf.Max(.1f,stage.PreviewDuration(turn)*.85f);stage.Reconstruct(seq,i,t);view.SetLine(CutsceneTextUtility.SpeakerName(turn,stage),turn.text,turn.dialogueKind);view.Reveal(9999);
                        foreach(var group in canvas.GetComponentsInChildren<CanvasGroup>(true))group.alpha=group.name=="Dialogue"&&turn.hasDialogue?1:0;Canvas.ForceUpdateCanvases();
                        if(turn.hasDialogue){var actor=stage.FindActor(turn.speakerId);var vp=stage.output.WorldToViewportPoint(actor.lookAtBone.position);checks.Add(turn.id+" | head="+vp.ToString("F3")+" | name="+CutsceneTextUtility.SpeakerName(turn,stage)+" | "+turn.text);}
                        Capture(stage.output,$"{(room?"SC02":"SC03")}-{i+1:000}-{turn.id}");
                    }
                    stage.Restore();foreach(var p in poses)if(Vector3.Distance(p.Key.localPosition,p.Value.Item1)>.0001f||Quaternion.Angle(p.Key.localRotation,p.Value.Item2)>.05f||Vector3.Distance(p.Key.localScale,p.Value.Item3)>.0001f)throw new Exception("Preview did not restore "+p.Key.name);
                    checks.Add("PASS "+seq.sequenceId+": "+seq.turns.Count+" turns reconstructed and actor transforms restored.");
                }
                File.WriteAllLines("Logs/SC23/preview-validation.txt",checks);File.WriteAllText("Logs/SC23/preview-status.txt","PASS");EditorApplication.Exit(0);
            }catch(Exception e){File.WriteAllText("Logs/SC23/preview-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void Capture(Camera camera,string name)
        {
            // Manual render requests in one editor tick reuse stale GPU skinning buffers.
            // Render an exact CPU snapshot of the current bone matrices for each inspection frame.
            var snapshots=new List<(SkinnedMeshRenderer source,GameObject go,Mesh mesh)>();
            foreach(var skin in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))if(skin.enabled&&skin.sharedMesh!=null&&skin.bones.Length>0){
                var source=skin.sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);var vertices=source.vertices;var normals=source.normals;var weights=source.boneWeights;var poses=source.bindposes;var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*poses[i]).ToArray();
                for(int i=0;i<vertices.Length;i++){var w=weights[i];Vector3 point=Vector3.zero,normal=Vector3.zero;void Add(int bone,float weight){if(weight<=0)return;point+=matrices[bone].MultiplyPoint3x4(vertices[i])*weight;if(normals.Length==vertices.Length)normal+=matrices[bone].inverse.transpose.MultiplyVector(normals[i])*weight;}Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);vertices[i]=point;if(normals.Length==vertices.Length)normals[i]=normal.normalized;}
                mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;mesh.RecalculateBounds();var go=new GameObject("Inspection skin snapshot",typeof(MeshFilter),typeof(MeshRenderer)){hideFlags=HideFlags.HideAndDontSave};go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;go.layer=skin.gameObject.layer;snapshots.Add((skin,go,mesh));skin.enabled=false;
            }
            var target=new RenderTexture(1280,720,24);target.Create();var before=RenderTexture.active;
            try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;var png=new Texture2D(1280,720,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();File.WriteAllBytes("Logs/SC23/Previews/"+name+".png",png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);}
            finally{RenderTexture.active=before;target.Release();UnityEngine.Object.DestroyImmediate(target);foreach(var entry in snapshots){entry.source.enabled=true;UnityEngine.Object.DestroyImmediate(entry.go);UnityEngine.Object.DestroyImmediate(entry.mesh);}}
        }
    }
}
