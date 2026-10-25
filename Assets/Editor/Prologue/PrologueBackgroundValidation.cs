using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProjectCRT.Prologue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectCRT.Prologue.Editor
{
    public static class PrologueBackgroundValidation
    {
        [Serializable] class Geometry { public Vector3[] vertices; public int[] triangles; }
        public static void BatchInspect()
        {
            try
            {
                var lines=new List<string>();Directory.CreateDirectory("Logs/PrologueAssets/Previews");
                foreach(var path in PrologueBackgroundBuilder.Scenes)
                {
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                    var preview=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PrologueBackgroundPreview>(true)).Single();
                    int missing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    if(missing!=0)throw new Exception(path+" missing scripts");
                    var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Where(r=>r.gameObject.activeInHierarchy).ToArray();
                    foreach(var r in renderers)foreach(var mat in r.sharedMaterials)if(mat==null||mat.shader==null||!mat.shader.isSupported)throw new Exception(path+" invalid material: "+r.name);
                    lines.Add(path+" | missing scripts=0 | active renderers="+renderers.Length+" | shots="+preview.stage.shots.Count+" | actors="+string.Join(",",preview.stage.actors.Select(a=>a.id)));
                    Physics.SyncTransforms();
                    for(int i=0;i<preview.stage.shots.Count;i++)
                    {
                        preview.SelectShot(i);var camera=preview.stage.output;camera.scene=scene;
                        var hits=Physics.RaycastAll(camera.transform.position,camera.transform.forward,20).OrderBy(h=>h.distance);
                        lines.Add(preview.stage.shots[i].id+" | camera="+camera.transform.position.ToString("F3")+" | forward hits="+string.Join("; ",hits.Take(5).Select(h=>h.collider.name+"@"+h.distance.ToString("F2"))));
                        Capture(camera,"SC0"+(Array.IndexOf(PrologueBackgroundBuilder.Scenes,path)+1)+"-"+(i+1).ToString("00"));
                    }
                    if(path.Contains("SC03"))
                    {
                        var mesh=renderers.Select(r=>r.GetComponent<MeshFilter>()).First(m=>m!=null&&m.name=="shangria");
                        File.WriteAllText("Logs/PrologueAssets/current-interior.json",JsonUtility.ToJson(new Geometry{vertices=mesh.sharedMesh.vertices.Select(v=>mesh.transform.TransformPoint(v)).ToArray(),triangles=mesh.sharedMesh.triangles}));
                    }
                    if(path.Contains("SC02"))foreach(var m in renderers.Where(r=>r.name.Contains("wall_clock")))
                    {
                        var mesh=m.GetComponent<MeshFilter>();lines.Add("CLOCK "+m.name+" pos="+m.transform.position+" localrot="+m.transform.localEulerAngles+" vertices="+string.Join(";",mesh.sharedMesh.vertices.Take(10).Select(v=>v.ToString("F3"))));
                    }
                    preview.SelectShot(0);
                }
                File.WriteAllLines("Logs/PrologueAssets/validation.txt",lines);File.WriteAllText("Logs/PrologueAssets/inspect-status.txt","PASS");EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText("Logs/PrologueAssets/inspect-status.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1280,720,24);target.Create();var old=RenderTexture.active;
            try
            {
                foreach(var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)){ps.Simulate(1.3f,true,true);ps.Pause();}
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/PrologueAssets/Previews/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{RenderTexture.active=old;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
