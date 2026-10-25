using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Cutscenes.Editor
{
    public static class CutsceneSceneVerificationMenu
    {
        const string Active="ProjectCRT.CutsceneSceneVerification.Active";
        const string Started="ProjectCRT.CutsceneSceneVerification.Started";
        [MenuItem("Tools/ProjectCRT/Cutscenes/Verify Scene Loading and Timeline")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(SceneManager.GetActiveScene().path!="Assets/Scenes/Shangrila_CutsceneTest.unity")throw new InvalidOperationException("Open Shangrila_CutsceneTest before verification.");
            Directory.CreateDirectory("Logs/Cutscenes");SessionState.SetBool(Active,true);SessionState.SetFloat(Started,(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Watch;EditorApplication.update+=Watch;EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Restore(){EditorApplication.update-=Watch;if(SessionState.GetBool(Active,false))EditorApplication.update+=Watch;}
        static void Watch()
        {
            if(!SessionState.GetBool(Active,false)){EditorApplication.update-=Watch;return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Started,0)>120){Finish("Verification timed out after 120 seconds.",Array.Empty<string>());return;}
            if(!EditorApplication.isPlaying)return;
            var probe=UnityEngine.Object.FindFirstObjectByType<CutsceneSceneVerification>();
            if(probe==null){new GameObject("Cutscene scene loading verification").AddComponent<CutsceneSceneVerification>();return;}
            if(probe.Done)Finish(probe.Failure,probe.Checks);
        }
        static void Finish(string failure,IEnumerable<string> checks)
        {
            Directory.CreateDirectory("Logs/Cutscenes");File.WriteAllText("Logs/Cutscenes/scene-verification.txt",(string.IsNullOrEmpty(failure)?"PASS":"FAIL: "+failure)+"\n"+string.Join("\n",checks));
            SessionState.SetBool(Active,false);EditorApplication.update-=Watch;EditorApplication.isPlaying=false;
        }
    }
}
