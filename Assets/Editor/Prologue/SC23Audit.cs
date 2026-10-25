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
    public static class SC23Audit
    {
        public static void Run()
        {
            try {
                Directory.CreateDirectory("Logs/SC23");var lines=new List<string>();
                foreach(var path in new[]{"Assets/Scenes/ProjectCRT_SC02_SanRoom.unity","Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity"}){
                    EditorSceneManager.OpenScene(path);lines.Add("SCENE "+path);
                    foreach(var actor in UnityEngine.Object.FindObjectsByType<CutsceneActor>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
                        lines.Add("ACTOR "+actor.id+" "+actor.transform.position+" scale="+actor.transform.lossyScale);
                        foreach(var t in actor.GetComponentsInChildren<Transform>(true))lines.Add("BONE "+AnimationUtility.CalculateTransformPath(t,actor.transform)+" "+t.position.ToString("F3"));
                        foreach(var r in actor.GetComponentsInChildren<Renderer>(true))lines.Add("MESH "+r.name+" bounds="+r.bounds+" materials="+string.Join(";",r.sharedMaterials.Select(m=>m==null?"null":m.name+" "+m.shader.name)));
                    }
                    foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t.name.Contains("Door")||t.name.Contains("door")||t.name.Contains("Entrance")||t.name.Contains("Spawn")||t.name.Contains("room02"))lines.Add("LANDMARK "+t.name+" "+t.position.ToString("F3")+" rotation="+t.eulerAngles);
                }
                File.WriteAllLines("Logs/SC23/audit.txt",lines);EditorApplication.Exit(0);
            }catch(Exception e){File.WriteAllText("Logs/SC23/audit-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
