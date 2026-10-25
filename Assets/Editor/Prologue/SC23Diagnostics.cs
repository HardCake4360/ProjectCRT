using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ProjectCRT.Prologue.Editor
{
    public static class SC23Diagnostics
    {
        public static void BatchInspect()
        {
            try{
                EditorSceneManager.OpenScene(SC23CutsceneAuthoring.BarScene);var stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();var sequence=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(SC23CutsceneAuthoring.BarSequence);stage.Capture(stage.output,true);stage.Reconstruct(sequence,sequence.turns.FindIndex(t=>t.id=="sc03-d16"),2);var lines=new List<string>();
                foreach(var actor in stage.actors.Where(a=>a.lookAtBone!=null)){
                    lines.Add(actor.id+" HEAD "+actor.lookAtBone.position+" actor="+actor.transform.position);
                    foreach(var t in actor.GetComponentsInChildren<Transform>(true).Take(9))lines.Add("TREE "+t.name+" pos="+t.position+" scale="+t.lossyScale);
                    foreach(var r in actor.GetComponentsInChildren<Renderer>(true)){
                        lines.Add(r.GetType().Name+" "+r.name+" enabled="+r.enabled+" bounds="+r.bounds+" local="+r.transform.localPosition);
                        if(r is SkinnedMeshRenderer skin){lines.Add("ROOTBONE "+(skin.rootBone==null?"none":skin.rootBone.name)+" bones="+skin.bones.Length);var m=new Mesh();skin.BakeMesh(m);var v=m.vertices.Select(p=>skin.transform.TransformPoint(p)).ToArray();lines.Add("BAKED bounds "+v.Aggregate(new Bounds(v[0],Vector3.zero),(b,p)=>{b.Encapsulate(p);return b;}));UnityEngine.Object.DestroyImmediate(m);}
                    }
                }
                File.WriteAllLines("Logs/SC23/mesh-diagnostics.txt",lines);stage.Restore();EditorApplication.Exit(0);
            }catch(Exception e){File.WriteAllText("Logs/SC23/diagnostics-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
