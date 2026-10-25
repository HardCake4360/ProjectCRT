using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectCRT.Cases.Editor
{
    public static class ShangrilaCaseVerificationMenu
    {
        const string Active="ProjectCRT.CaseVerification.Active", Phase="ProjectCRT.CaseVerification.Phase";
        const string TestPath="ProjectCRT.CaseVerification.Path", Started="ProjectCRT.CaseVerification.Started";
        const string Restarting="ProjectCRT.CaseVerification.Restarting";
        [MenuItem("Tools/ProjectCRT/Cases/Verify Arrival and Save Across Play Sessions")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!=ShangrilaCaseBuilder.ScenePath)
                throw new InvalidOperationException("Open Shangrila_CaseTest in Edit Mode first.");
            Directory.CreateDirectory("Logs/Cases");
            SessionState.SetString(TestPath,Path.GetFullPath("Logs/Cases/arrival-test-"+Guid.NewGuid().ToString("N")+".json"));
            SessionState.SetBool(Active,true); SessionState.SetInt(Phase,1); SessionState.SetBool(Restarting,false);
            SessionState.SetFloat(Started,(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Watch; EditorApplication.update+=Watch; EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Resume()
        {
            EditorApplication.update-=Watch;
            if(SessionState.GetBool(Active,false))
            {
                if(SessionState.GetInt(Phase,1)==2 && !EditorApplication.isPlayingOrWillChangePlaymode)
                { SessionState.SetBool(Restarting,true); SessionState.SetFloat(Started,(float)EditorApplication.timeSinceStartup); }
                EditorApplication.update+=Watch;
            }
        }
        static void Watch()
        {
            if(!SessionState.GetBool(Active,false)){EditorApplication.update-=Watch;return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Started,0)>200){Finish("Case verification timed out.",Array.Empty<string>());return;}
            if(SessionState.GetBool(Restarting,false))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool(Restarting,false); EditorApplication.isPlaying=true; return;
            }
            if(!EditorApplication.isPlaying)return;
            var probe=UnityEngine.Object.FindFirstObjectByType<ShangrilaCaseVerification>();
            if(probe==null){new GameObject("Case verification").AddComponent<ShangrilaCaseVerification>();return;}
            if(!probe.Done)return;
            if(!string.IsNullOrEmpty(probe.Failure)){Finish(probe.Failure,probe.Checks);return;}
            if(SessionState.GetInt(Phase,1)==1)
            {
                File.WriteAllText("Logs/Cases/verification-first-session.txt","PASS\n"+string.Join("\n",probe.Checks));
                SessionState.SetInt(Phase,2); SessionState.SetBool(Restarting,true); EditorApplication.isPlaying=false;
            }
            else Finish(null,probe.Checks);
        }
        static void Finish(string failure,IEnumerable<string> checks)
        {
            File.WriteAllText("Logs/Cases/verification.txt",(failure==null?"PASS":"FAIL: "+failure)+"\n"+string.Join("\n",checks));
            SessionState.SetBool(Active,false); EditorApplication.update-=Watch;
            EditorApplication.isPlaying=false;
            // Keep the isolated path until shutdown callbacks have finished, never write into the player's slot.
            EditorApplication.delayCall+=ClearOverride;
        }
        static void ClearOverride()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=ClearOverride;return;}
            SessionState.EraseString(TestPath);
        }
    }
}
