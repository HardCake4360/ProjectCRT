using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ProjectCRT.Prologue.Editor
{
    public static class SC23RuntimeVerificationMenu
    {
        const string Active="SC23.Verify.Active",Stopping="SC23.Verify.Stopping",Started="SC23.Verify.Started";
        public static void BatchRun(){Directory.CreateDirectory("Logs/SC23");EditorSceneManager.OpenScene(SC23CutsceneAuthoring.RoomScene);SessionState.SetBool(Active,true);SessionState.SetBool(Stopping,false);SessionState.SetFloat(Started,(float)EditorApplication.timeSinceStartup);EditorApplication.update-=Watch;EditorApplication.update+=Watch;EditorApplication.isPlaying=true;}
        [InitializeOnLoadMethod]static void Restore(){if(SessionState.GetBool(Active,false)){EditorApplication.update-=Watch;EditorApplication.update+=Watch;}}
        static void Watch(){if(!SessionState.GetBool(Active,false)){EditorApplication.update-=Watch;return;}if(SessionState.GetBool(Stopping,false)){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool(Active,false);EditorApplication.Exit(File.ReadAllText("Logs/SC23/runtime-status.txt").StartsWith("PASS")?0:1);}return;}if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Started,0)>300){Finish("Timed out after 300 seconds","");return;}if(!EditorApplication.isPlaying)return;var p=UnityEngine.Object.FindFirstObjectByType<SC23RuntimeVerification>();if(p==null){new GameObject("SC02 SC03 playback verification").AddComponent<SC23RuntimeVerification>();return;}if(p.Done)Finish(p.Failure,string.Join("\n",p.Checks));}
        static void Finish(string error,string checks){File.WriteAllText("Logs/SC23/runtime-status.txt",string.IsNullOrEmpty(error)?"PASS":"FAIL: "+error);File.WriteAllText("Logs/SC23/runtime-validation.txt",checks);SessionState.SetBool(Stopping,true);EditorApplication.isPlaying=false;}
    }
}
