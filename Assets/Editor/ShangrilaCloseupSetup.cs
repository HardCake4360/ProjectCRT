using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShangrilaCloseupSetup
{
    [MenuItem("Tools/ProjectCRT/Configure Shangrila Conversation Shots")]
    public static void Build()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Outdoor_BarStreetTest.unity")throw new Exception("Open outdoor scene first.");
        var player=UnityEngine.Object.FindFirstObjectByType<StreetWalkContext>().player;
        var camera=player.GetComponentInChildren<Camera>();
        var report=new System.Collections.Generic.List<string>();
        foreach(var npc in UnityEngine.Object.FindObjectsByType<NPC_script>(FindObjectsSortMode.None))
        {
            var animator=npc.GetComponentInChildren<Animator>();
            Transform head=animator!=null&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Head):null;
            var renderers=npc.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            Vector3 face=head!=null?head.position+Vector3.up*0.08f:new Vector3(bounds.center.x,bounds.max.y-0.15f,bounds.center.z);
            var shot=npc.GetComponentInChildren<ShangrilaConversationShot>(true);
            if(shot==null){var go=new GameObject(npc.name+" - Conversation Shot");go.transform.SetParent(npc.transform);shot=go.AddComponent<ShangrilaConversationShot>();}
            shot.gameObject.SetActive(false);shot.output=camera;shot.fieldOfView=34;
            shot.focusTarget=head;
            // Slight three-quarter view, at eye level, leaving the lower frame for dialogue.
            Vector3 forward=npc.transform.forward;
            bool rebecca=npc.name=="rebeca";
            shot.transform.position=face+forward*(rebecca?0.86f:1.0f)+npc.transform.right*(rebecca?-0.48f:0.08f)+Vector3.up*0.02f;
            var rotation=Quaternion.LookRotation(face-Vector3.up*0.10f-shot.transform.position);
            shot.transform.rotation=Quaternion.LookRotation(face-Vector3.up*0.10f+(rotation*Vector3.right)*0.12f-shot.transform.position);
            var light=shot.GetComponentInChildren<Light>(true);
            if(light==null){var fill=new GameObject("Conversation soft face light");fill.transform.SetParent(shot.transform,false);light=fill.AddComponent<Light>();}
            light.type=LightType.Spot;light.color=new Color(1f,0.86f,0.73f);light.intensity=2.2f;light.range=3;light.spotAngle=75;light.innerSpotAngle=45;light.shadows=LightShadows.None;
            var so=new SerializedObject(npc);so.FindProperty("useCloseupCamera").boolValue=true;so.FindProperty("closeupCameraBehaviour").objectReferenceValue=shot;so.FindProperty("lookAtTargetOverride").objectReferenceValue=camera.transform;so.ApplyModifiedPropertiesWithoutUndo();
            report.Add(npc.name+" bounds="+bounds+" head="+face+" shot="+shot.transform.position+" rotation="+shot.transform.eulerAngles);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllLines("Logs/ShangrilaCloseupSetup.txt",report);
        ConfigurePresentation();
    }
    [MenuItem("Tools/ProjectCRT/Configure Conversation Window Fade")]
    public static void ConfigurePresentation()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Outdoor_BarStreetTest.unity")throw new Exception("Open outdoor scene first.");
        foreach(var ui in UnityEngine.Object.FindObjectsByType<DialogueUIManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(ui.canvas!=null && ui.canvas.GetComponent<ConversationCanvasFade>()==null)ui.canvas.gameObject.AddComponent<ConversationCanvasFade>();
        foreach(var ui in UnityEngine.Object.FindObjectsByType<InvestigationInteractionUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var so=new SerializedObject(ui);var canvas=so.FindProperty("rootCanvas").objectReferenceValue as Canvas;
            if(canvas!=null && canvas.GetComponent<ConversationCanvasFade>()==null)canvas.gameObject.AddComponent<ConversationCanvasFade>();
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
}
