using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

public static class StreetWalkSetup
{
    const string ScenePath="Assets/Scenes/Outdoor_BarStreetTest.unity";
    [Serializable] class Geometry { public Vector3[] vertices; public int[] triangles; }
    [InitializeOnLoadMethod] static void Boot()
    {
        EditorApplication.update-=WatchTest;
        if(SessionState.GetBool("StreetWalk.Test",false)) EditorApplication.update+=WatchTest;
    }
    static void Audit()
    {
        var root=GameObject.Find("Shangrila - user blockout");
        var mesh=root.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="shangria");
        var geometry=new Geometry{vertices=mesh.sharedMesh.vertices.Select(v=>mesh.transform.TransformPoint(v)).ToArray(),triangles=mesh.sharedMesh.triangles};
        Directory.CreateDirectory("Logs/StreetWalk");File.WriteAllText("Logs/StreetWalk/interior.json",JsonUtility.ToJson(geometry));
        Physics.SyncTransforms();
        var lines=new System.Collections.Generic.List<string>();
        for(float z=7;z<=20;z+=0.5f)for(float x=-9;x<=9;x+=0.5f)
        {
            if(Physics.Raycast(new Vector3(x,2.2f,z),Vector3.down,out var hit,4))
                lines.Add($"{x},{z},{hit.point.y},{hit.normal.y},{hit.collider.name}");
        }
        File.WriteAllLines("Logs/StreetWalk/floors.csv",lines);
    }
    [MenuItem("Tools/ProjectCRT/Configure Street Walking")]
    public static void Build()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new Exception("Open the outdoor scene first.");
        if(UnityEngine.Object.FindFirstObjectByType<StreetWalkContext>()!=null)throw new Exception("Street walking is already configured.");
        var model=GameObject.Find("Shangrila - user blockout");
        Undo.RecordObject(model.transform,"Turn Shangrila entrance toward street");
        model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0));
        var renderers=model.GetComponentsInChildren<MeshRenderer>();
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        model.transform.position=new Vector3(0.2f-bounds.center.x,0.2f-bounds.min.y,5.05f-bounds.min.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        // The wide open doorway in the blockout is on its original +Z side.
        Vector3 doorway=model.transform.TransformPoint(new Vector3(5.64f,0,3.4f));
        var bar=renderers.First(r=>r.name=="shangria");
        var rig=new GameObject("Street Walking - Player and Spawn Points");
        var context=rig.AddComponent<StreetWalkContext>();
        context.interiorBounds=bar.bounds;
        context.streetSpawn=Marker(rig.transform,"Street Spawn",new Vector3(-11,Ground(new Vector3(-11,0,0))+1.23f,0));
        context.streetSpawn.rotation=Quaternion.LookRotation(new Vector3(doorway.x,context.streetSpawn.position.y,doorway.z)-context.streetSpawn.position);
        context.entranceOutside=Marker(rig.transform,"Entrance Outside",new Vector3(doorway.x,Ground(doorway-Vector3.forward*2.5f)+1.23f,doorway.z-2.5f));
        context.entranceInside=Marker(rig.transform,"Entrance Inside",new Vector3(doorway.x,Ground(doorway+Vector3.forward*2)+1.23f,doorway.z+2));
        var oldCamera=Camera.main;
        if(oldCamera!=null){oldCamera.name="Free Camera (disabled reference)";oldCamera.tag="Untagged";oldCamera.gameObject.SetActive(false);}
        var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Player.prefab"));
        player.name="Player - Street Walking";player.transform.SetPositionAndRotation(context.streetSpawn.position,context.streetSpawn.rotation);
        var cc=player.GetComponent<CharacterController>();cc.height=1.33f;cc.radius=0.24f;cc.center=new Vector3(0,-0.55f,0);cc.skinWidth=0.025f;cc.stepOffset=0.25f;
        var pc=player.GetComponent<PlayerControler>();context.player=pc;
        var camera=player.GetComponentInChildren<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;camera.farClipPlane=120;camera.nearClipPlane=0.08f;camera.allowMSAA=false;
        camera.GetUniversalAdditionalCameraData().SetRenderer(1);camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera.GetUniversalAdditionalCameraData());
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        PrefabUtility.RecordPrefabInstancePropertyModifications(cc);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
        var canvas=new GameObject("Street Walk HUD",typeof(Canvas),typeof(CanvasScaler));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder=50;var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        context.locationText=Text(canvas.transform,"Location",new Vector2(0,1),new Vector2(150,-34),new Vector2(280,44),24);
        context.controlsText=Text(canvas.transform,"Controls",new Vector2(0.5f,0),new Vector2(0,28),new Vector2(850,45),19);
        context.controlsText.alignment=TextAlignmentOptions.Center;
        var hint=Text(canvas.transform,"Interaction Hint",new Vector2(0.5f,0.5f),new Vector2(0,-60),new Vector2(600,45),22);hint.enabled=false;hint.alignment=TextAlignmentOptions.Center;
        var settings=new SerializedObject(pc);settings.FindProperty("speed").floatValue=3.5f;settings.FindProperty("rayDistance").floatValue=2;
        settings.FindProperty("hintText").objectReferenceValue=hint;settings.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(pc);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/SingletonObjs/MainLoop.prefab"));
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/SingletonObjs/InputManger.prefab"));
        var lights=GameObject.Find("Shangrila - placement lighting");
        if(lights!=null)
        {
            var inside=lights.transform.Find("Warm interior light");inside.position=new Vector3(bar.bounds.center.x,2.35f,bar.bounds.center.z);
            var spill=lights.transform.Find("Entrance spill toward street");spill.position=new Vector3(doorway.x,2.45f,doorway.z+0.4f);spill.LookAt(new Vector3(doorway.x,0.2f,doorway.z-3));
        }
        // Invisible edge guards keep the inspection route on the finite street model.
        Guard(rig.transform,"West boundary",new Vector3(-38.5f,2,0),new Vector3(0.3f,5,40));
        Guard(rig.transform,"East boundary",new Vector3(38.5f,2,0),new Vector3(0.3f,5,40));
        Guard(rig.transform,"South boundary",new Vector3(0,2,-19),new Vector3(78,5,0.3f));
        Guard(rig.transform,"North boundary",new Vector3(0,2,20),new Vector3(78,5,0.3f));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/StreetWalk/setup.txt",$"Configured existing Player prefab. Open doorway at {doorway}; outside={context.entranceOutside.position}; inside={context.entranceInside.position}; no scene loading or teleport required.");
    }
    static float Ground(Vector3 point)
    {
        Physics.SyncTransforms();
        var hits=Physics.RaycastAll(new Vector3(point.x,2.25f,point.z),Vector3.down,5)
            .Where(h=>h.collider.GetComponentInParent<PlayerControler>()==null && h.normal.y>0.5f).OrderBy(h=>h.distance).ToArray();
        if(hits.Length==0)throw new Exception("No floor at "+point);
        var hit=hits[0];
        return hit.point.y;
    }
    static Transform Marker(Transform parent,string name,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent);t.position=position;return t;}
    static void Guard(Transform parent,string name,Vector3 position,Vector3 size){var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=position;go.AddComponent<BoxCollider>().size=size;}
    static TextMeshProUGUI Text(Transform parent,string name,Vector2 anchor,Vector2 offset,Vector2 size,float fontSize)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var text=go.AddComponent<TextMeshProUGUI>();
        text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/resources/font/NeoDunggeunmoPro-Regular 1 SDF.asset");text.fontSize=fontSize;text.color=new Color(0.92f,0.92f,0.85f);text.raycastTarget=false;
        var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=offset;rect.sizeDelta=size;return text;
    }
    [MenuItem("Tools/ProjectCRT/Verify Street Walking and Entry")]
    public static void Test()
    {
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Open outdoor scene first.");
        SessionState.SetBool("StreetWalk.Test",true);SessionState.SetFloat("StreetWalk.Started",(float)EditorApplication.timeSinceStartup);
        EditorApplication.update-=WatchTest;EditorApplication.update+=WatchTest;EditorApplication.isPlaying=true;
    }
    static void WatchTest()
    {
        if(!SessionState.GetBool("StreetWalk.Test",false)){EditorApplication.update-=WatchTest;return;}
        if(EditorApplication.timeSinceStartup-SessionState.GetFloat("StreetWalk.Started",0)>100){FinishTest("Verification timeout",Array.Empty<string>());return;}
        if(!EditorApplication.isPlaying)return;
        var probe=UnityEngine.Object.FindFirstObjectByType<StreetWalkVerification>();
        if(probe==null){new GameObject("Street walking verification").AddComponent<StreetWalkVerification>();return;}
        if(probe.Done)FinishTest(probe.Failure,probe.Checks.ToArray());
    }
    static void FinishTest(string failure,string[] checks)
    {
        File.WriteAllText("Logs/StreetWalk/verification.txt",(string.IsNullOrEmpty(failure)?"PASS":"FAIL: "+failure)+"\n"+string.Join("\n",checks));
        SessionState.SetBool("StreetWalk.Test",false);EditorApplication.update-=WatchTest;EditorApplication.isPlaying=false;
    }
}
 
 
 
