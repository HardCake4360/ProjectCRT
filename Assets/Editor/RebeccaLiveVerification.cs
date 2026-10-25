using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectCRT.Cases;

// Explicit opt-in only: this menu performs one billed NPC turn against the local server.
[InitializeOnLoad]
public static class RebeccaLiveVerification
{
    const string Key = "ProjectCRT.RebeccaLiveVerification";
    static RebeccaLiveVerification()
    {
        EditorApplication.update += Watch;
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false) || !SessionState.GetBool(Key + ".attached", false) ||
                !File.Exists("Logs/NpcJointTurn/unity-live-ui.json")) return;
            var probe = UnityEngine.Object.FindFirstObjectByType<RebeccaLiveVerificationProbe>();
            if (probe != null) probe.InspectExistingResult();
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                SessionState.SetBool(Key + ".attached", false);
                SessionState.EraseString("ProjectCRT.CaseVerification.Path");
            }
        };
    }

    [MenuItem("Tools/ProjectCRT/Verify Rebecca Live API (one billed turn)")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Shangrila_CaseTest.unity");
        var manager = UnityEngine.Object.FindFirstObjectByType<ShangrilaCaseManager>();
        string path = Path.GetFullPath("Logs/NpcJointTurn/case-live-" + Guid.NewGuid().ToString("N") + ".json");
        var fixture = ShangrilaCaseSave.New(scene.path);
        fixture.completedCutsceneIds.Add(manager.introduction.sequenceId);
        if (!new ShangrilaCaseStore(path).Save(fixture, out string error)) throw new Exception(error);
        SessionState.SetString("ProjectCRT.CaseVerification.Path", path);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".attached", false);
        EditorApplication.isPlaying = true;
    }

    static void Watch()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying ||
            EditorApplication.isCompiling || SessionState.GetBool(Key + ".attached", false)) return;
        SessionState.SetBool(Key + ".attached", true);
        new GameObject("Rebecca live API verification").AddComponent<RebeccaLiveVerificationProbe>();
    }
}
