using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShangrilaNpcSetup
{
    const string Street="Assets/Scenes/Outdoor_BarStreetTest.unity";
    [MenuItem("Tools/ProjectCRT/Place Shangrila NPCs")]
    public static void Build()
    {
        var target=SceneManager.GetActiveScene();
        if(target.path!=Street) throw new Exception("Open outdoor scene first.");
        if(GameObject.Find("Shangrila - Bar NPCs")!=null)throw new Exception("NPCs already placed.");
        var parent=new GameObject("Shangrila - Bar NPCs");
        var camera=UnityEngine.Object.FindFirstObjectByType<StreetWalkContext>().player.GetComponentInChildren<Camera>();
        var source=EditorSceneManager.OpenScene("Assets/Scenes/Bar.unity",OpenSceneMode.Additive);
        try
        {
            foreach(var original in source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NPC_script>(true)).Where(n=>n.GetComponent<NpcInvestigationController>()!=null))
            {
                var copy=UnityEngine.Object.Instantiate(original.gameObject);
                copy.name=original.name;
                SceneManager.MoveGameObjectToScene(copy,target); copy.transform.SetParent(parent.transform);
                bool rebecca=original.name=="rebeca";
                copy.transform.SetPositionAndRotation(rebecca?new Vector3(-6.5f,0,10.8f):new Vector3(-0.8f,0,10.5f),Quaternion.LookRotation(rebecca?Vector3.back:Vector3.left));
                var renderers=copy.GetComponentsInChildren<Renderer>();
                copy.transform.position+=Vector3.up*(0.2f-renderers.Min(r=>r.bounds.min.y));
                var npc=copy.GetComponent<NPC_script>();var settings=new SerializedObject(npc);
                settings.FindProperty("useCloseupCamera").boolValue=false;
                settings.FindProperty("closeupCameraBehaviour").objectReferenceValue=null;
                settings.FindProperty("lookAtTargetOverride").objectReferenceValue=camera.transform;
                settings.FindProperty("HintName").stringValue=rebecca?"레베카와 대화":"제레미와 대화";
                settings.ApplyModifiedPropertiesWithoutUndo();
                foreach(var renderer in renderers)
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++) if(materials[i]!=null)
                    {
                        var lit=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/BarRetro/Lighting/Materials/"+materials[i].name+"_BakedLit.mat");
                        if(lit!=null)materials[i]=lit;
                    }
                    renderer.sharedMaterials=materials;
                }
            }
        }
        finally {EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(target);}
        var dialogue=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/SingletonObjs/DialogueUI.prefab"),target);
        foreach(var canvas in dialogue.GetComponentsInChildren<Canvas>(true)) {canvas.sortingOrder=200;PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);}
        var investigation=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/prefab/NpcInvestigationUI.prefab"),target);
        foreach(var canvas in investigation.GetComponentsInChildren<Canvas>(true)){canvas.sortingOrder=210;PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);}
        if(UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>()==null)
        {
            var events=new GameObject("Street Dialogue EventSystem",typeof(UnityEngine.EventSystems.EventSystem));
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        }
        EditorSceneManager.MarkSceneDirty(target);EditorSceneManager.SaveScene(target);
        File.WriteAllText("Logs/ShangrilaNpcSetup.txt","Placed Rebecca and Jeremy; original dialogue/persona/animation preserved. Bound player camera, dialogue canvases and UI input.");
        ShangrilaCloseupSetup.Build();
    }
    [MenuItem("Tools/ProjectCRT/Verify Shangrila NPC Conversations")]
    public static void Test()
    {
        if(SceneManager.GetActiveScene().path!=Street || EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Open outdoor scene in edit mode first.");
        SessionState.SetFloat("ShangrilaNpc.Started",(float)EditorApplication.timeSinceStartup);
        SessionState.SetBool("ShangrilaNpc.Test",true);
        EditorApplication.update-=Watch;EditorApplication.update+=Watch;
        EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod] static void RestoreTest(){if(SessionState.GetBool("ShangrilaNpc.Test",false))EditorApplication.update+=Watch;}
    static void Watch()
    {
        if(!EditorApplication.isPlaying)return;
        var probe=UnityEngine.Object.FindFirstObjectByType<ShangrilaNpcVerification>();
        if(probe==null){new GameObject("NPC verification").AddComponent<ShangrilaNpcVerification>();return;}
        if(!probe.Done)return;
        File.WriteAllText("Logs/ShangrilaNpcVerification.txt",(probe.Failure==null?"PASS":"FAIL: "+probe.Failure)+"\n"+string.Join("\n",probe.Checks));
        SessionState.SetBool("ShangrilaNpc.Test",false);EditorApplication.update-=Watch;EditorApplication.isPlaying=false;
    }
}
