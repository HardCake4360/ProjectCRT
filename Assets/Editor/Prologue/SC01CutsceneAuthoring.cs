using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectCRT.Prologue.Editor
{
    public static partial class SC01CutsceneAuthoring
    {
        public const string ScenePath="Assets/Scenes/ProjectCRT_SC01_TaxiArrival.unity";
        public const string Folder="Assets/Settings/Prologue/SC01";
        public const string SequencePath=Folder+"/SC01_RoadToLosSimula.asset";
        public static void BatchAudit()
        {
            try
            {
                Directory.CreateDirectory("Logs/SC01");EditorSceneManager.OpenScene(ScenePath);
                var san=UnityEngine.Object.FindObjectsByType<CutsceneActor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(a=>a.id=="san");
                var lines=new List<string>();
                foreach(var t in san.GetComponentsInChildren<Transform>(true))lines.Add(AnimationUtility.CalculateTransformPath(t,san.transform)+" | local="+t.localPosition.ToString("F4")+" | rotation="+t.localRotation.ToString("F4")+" | world="+t.position.ToString("F4"));
                foreach(var r in san.GetComponentsInChildren<Renderer>(true))lines.Add("MATERIAL "+r.name+" "+string.Join(";",r.sharedMaterials.Select(m=>m.name+" / "+m.shader.name)));
                foreach(var path in new[]{"Assets/resources/fbx/San/anim/walking.anim","Assets/resources/fbx/San/anim/stopWalking.anim","Assets/resources/fbx/San/anim/San_opening_walk.fbx"})
                    foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())lines.Add("CLIP "+path+" | "+clip.name+" | "+clip.length+" | bindings="+string.Join(";",AnimationUtility.GetCurveBindings(clip).Select(b=>b.path+":"+b.propertyName).Distinct().Take(8)));
                File.WriteAllLines("Logs/SC01/rig-audit.txt",lines);EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText("Logs/SC01/audit-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
