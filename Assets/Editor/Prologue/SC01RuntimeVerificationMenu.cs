using System;
using System.IO;
using ProjectCRT.Prologue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectCRT.Prologue.Editor
{
    public static class SC01RuntimeVerificationMenu
    {
        const string Active="SC01.Verification.Active",Started="SC01.Verification.Started",Stopping="SC01.Verification.Stopping";
        public static void BatchRun()
        {
            Directory.CreateDirectory("Logs/SC01");EditorSceneManager.OpenScene(SC01CutsceneAuthoring.ScenePath);
            SessionState.SetBool(Active,true);SessionState.SetBool(Stopping,false);SessionState.SetFloat(Started,(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Watch;EditorApplication.update+=Watch;EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod]static void Restore(){if(SessionState.GetBool(Active,false)){EditorApplication.update-=Watch;EditorApplication.update+=Watch;}}
        static void Watch()
        {
            if(!SessionState.GetBool(Active,false)){EditorApplication.update-=Watch;return;}
            if(SessionState.GetBool(Stopping,false))
            {if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Active,false);EditorApplication.Exit(File.ReadAllText("Logs/SC01/runtime-status.txt").StartsWith("PASS")?0:1);}return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Started,0)>210){Finish("Timed out after 210 seconds","");return;}
            if(!EditorApplication.isPlaying)return;
            var probe=UnityEngine.Object.FindFirstObjectByType<SC01RuntimeVerification>();
            if(probe==null){new GameObject("SC01 playback verification").AddComponent<SC01RuntimeVerification>();return;}
            if(probe.Done)Finish(probe.Failure,string.Join("\n",probe.Checks));
        }
        static void Finish(string error,string checks)
        {
            File.WriteAllText("Logs/SC01/runtime-status.txt",string.IsNullOrEmpty(error)?"PASS":"FAIL: "+error);File.WriteAllText("Logs/SC01/runtime-validation.txt",checks);
            SessionState.SetBool(Stopping,true);EditorApplication.isPlaying=false;
        }
    }
}
