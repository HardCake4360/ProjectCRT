using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InvestigationHardeningVerification
{
    private const string Key = "ProjectCRT.InvestigationHardeningVerification";
    private static InvestigationHardeningProbe probe;
    [Serializable] private sealed class Report { public bool passed; public string[] checks; public string error; }

    public static void Run()
    {
        if (EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path.StartsWith("Assets/NoSleep/")))
            throw new Exception("An RPG scene is still enabled in the build.");
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name.StartsWith("NoSleep.")))
            throw new Exception("RPG assemblies are still active.");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".started", (float)EditorApplication.timeSinceStartup);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        WatchAfterReload();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void WatchAfterReload()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) { EditorApplication.update -= Tick; return; }
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".started", 0) > 120)
        { Finish(false, "Verification timed out."); return; }
        if (!EditorApplication.isPlaying) return;
        if (probe == null) { probe = new GameObject("Investigation verification").AddComponent<InvestigationHardeningProbe>(); return; }
        if (probe.Done) Finish(string.IsNullOrEmpty(probe.Failure), probe.Failure);
    }

    private static void Finish(bool success, string error)
    {
        SessionState.SetBool(Key, false);
        string path = Path.GetFullPath("Logs/InvestigationHardeningVerification.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(new Report { passed = success, checks = probe != null ? probe.Passed.ToArray() : Array.Empty<string>(), error = error }, true));
        Debug.Log($"Investigation verification: {success}, report={path}");
        EditorApplication.Exit(success ? 0 : 1);
    }
}
