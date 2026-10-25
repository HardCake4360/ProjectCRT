using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectCRT.Cutscenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Cutscenes.Editor
{
    public static class CutsceneVerificationMenu
    {
        const string Active="ProjectCRT.CutsceneVerification.Active";
        const string Started="ProjectCRT.CutsceneVerification.Started";
        const string SampleScene="Assets/Scenes/Shangrila_CutsceneTest.unity";
        [MenuItem("Tools/ProjectCRT/Cutscenes/Verify Runtime Lifecycle")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before verification.");
            if(SceneManager.GetActiveScene().path!=SampleScene)throw new InvalidOperationException("Open Shangrila_CutsceneTest before verification.");
            Directory.CreateDirectory("Logs/Cutscenes");
            SessionState.SetBool(Active,true);SessionState.SetFloat(Started,(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Watch;EditorApplication.update+=Watch;EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void RestoreWatch()
        {
            EditorApplication.update-=Watch;if(SessionState.GetBool(Active,false))EditorApplication.update+=Watch;
        }
        static void Watch()
        {
            if(!SessionState.GetBool(Active,false)){EditorApplication.update-=Watch;return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Started,0)>120){Finish("Verification timed out after 120 seconds.",Array.Empty<string>());return;}
            if(!EditorApplication.isPlaying)return;
            var probe=UnityEngine.Object.FindFirstObjectByType<CutsceneVerification>();
            if(probe==null){new GameObject("Cutscene lifecycle verification").AddComponent<CutsceneVerification>();return;}
            if(probe.Done)Finish(probe.Failure,probe.Checks);
        }
        static void Finish(string failure,IEnumerable<string> checks)
        {
            Directory.CreateDirectory("Logs/Cutscenes");
            File.WriteAllText("Logs/Cutscenes/verification.txt",(string.IsNullOrEmpty(failure)?"PASS":"FAIL: "+failure)+"\n"+string.Join("\n",checks));
            SessionState.SetBool(Active,false);EditorApplication.update-=Watch;EditorApplication.isPlaying=false;
        }

        [MenuItem("Tools/ProjectCRT/Cutscenes/Verify Preview Restoration")]
        public static void VerifyPreviewRestoration()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Preview verification requires Edit Mode.");
            var stage=UnityEngine.Object.FindFirstObjectByType<CutsceneStage>();
            var sequence=AssetDatabase.LoadAssetAtPath<CutsceneSequence>("Assets/Settings/Cutscenes/Shangrila_Introduction.asset");
            if(stage==null || stage.output==null || sequence==null)throw new InvalidOperationException("Open the authored cutscene sample scene first.");
            var scene=stage.gameObject.scene;bool wasDirty=scene.isDirty;
            var transforms=stage.actors.Where(a=>a!=null).SelectMany(a=>a.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
            var before=transforms.Select(t=>new TransformState(t)).ToArray();var camera=stage.output;var pose=CutsceneCameraState.From(camera);
            var renderers=stage.actors.Where(a=>a!=null).SelectMany(a=>a.GetComponentsInChildren<Renderer>(true)).Distinct().Select(r=>new RendererState(r)).ToArray();
            var enabled=stage.actors.Where(a=>a!=null).SelectMany(a=>a.GetComponentsInChildren<Behaviour>(true)).Where(b=>b!=null).ToDictionary(b=>b,b=>b.enabled);
            string failure=null;
            try
            {
                var errors=stage.Validate(sequence);if(errors.Count>0)throw new Exception("Invalid sample: "+string.Join("; ",errors));
                stage.Capture(camera,true);
                int index=Mathf.Min(sequence.turns.Count-1,3);stage.Reconstruct(sequence,index,0.45f);
            }
            catch(Exception e){failure=e.ToString();}
            finally {stage.Restore();}
            if(failure==null)
            {
                foreach(var value in before)if(!value.Matches()){failure="Preview changed transform after Stop: "+value.target.name;break;}
                foreach(var value in renderers)if(!value.Matches()){failure="Preview changed expression/material state after Stop: "+value.target.name;break;}
                if(Vector3.Distance(camera.transform.position,pose.position)>0.001f || Quaternion.Angle(camera.transform.rotation,pose.rotation)>0.01f || Mathf.Abs(camera.fieldOfView-pose.fieldOfView)>0.001f)failure="Preview camera state was not restored.";
                foreach(var pair in enabled)if(pair.Key!=null && pair.Key.enabled!=pair.Value){failure="Preview changed behaviour enabled state: "+pair.Key.GetType().Name;break;}
                if(scene.isDirty!=wasDirty)failure="Preview dirtied the authored scene.";
            }
            Directory.CreateDirectory("Logs/Cutscenes");
            File.WriteAllText("Logs/Cutscenes/preview-verification.txt",failure==null?"PASS\nSelected-turn reconstruction restores camera, actor root/bone poses and behaviour states; scene dirty state is unchanged.":"FAIL: "+failure);
            if(failure!=null)throw new Exception(failure);
        }
        readonly struct TransformState
        {
            public readonly Transform target;
            readonly Vector3 position,scale;readonly Quaternion rotation;
            public TransformState(Transform t){target=t;position=t.localPosition;rotation=t.localRotation;scale=t.localScale;}
            public bool Matches()=>target!=null && Vector3.Distance(target.localPosition,position)<0.0001f && Quaternion.Angle(target.localRotation,rotation)<0.01f && Vector3.Distance(target.localScale,scale)<0.0001f;
        }
        readonly struct RendererState
        {
            public readonly Renderer target;
            readonly Material[] materials;readonly float[] weights;
            public RendererState(Renderer renderer)
            {
                target=renderer;materials=renderer.sharedMaterials;
                var skin=renderer as SkinnedMeshRenderer;int count=skin!=null && skin.sharedMesh!=null?skin.sharedMesh.blendShapeCount:0;
                weights=new float[count];for(int i=0;i<count;i++)weights[i]=skin.GetBlendShapeWeight(i);
            }
            public bool Matches()
            {
                if(target==null || !target.sharedMaterials.SequenceEqual(materials))return false;
                var skin=target as SkinnedMeshRenderer;
                for(int i=0;i<weights.Length;i++)if(Mathf.Abs(skin.GetBlendShapeWeight(i)-weights[i])>0.001f)return false;
                return true;
            }
        }
    }
}
