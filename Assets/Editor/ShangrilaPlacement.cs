using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ShangrilaPlacement
{
    const string ModelPath = "Assets/resources/3Dmodels/maps/shangrila.fbx";
    public static void Audit()
    {
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        try
        {
            File.WriteAllLines("Logs/ShangrilaModel.txt",instance.GetComponentsInChildren<Transform>(true).Select(t=>
            {
                var r=t.GetComponent<Renderer>();return $"{t.name} pos={t.position} local={t.localPosition} rotation={t.eulerAngles}"+(r!=null?$" center={r.bounds.center} size={r.bounds.size} materials={string.Join(",",r.sharedMaterials.Select(m=>m!=null?m.name+":"+m.shader.name:"null"))}":"");
            }));
        }
        finally {UnityEngine.Object.DestroyImmediate(instance);}
    }
    [MenuItem("Tools/ProjectCRT/Place Shangrila in Outdoor Scene")]
    public static void Place()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Outdoor_BarStreetTest.unity") throw new Exception("Open Outdoor_BarStreetTest before placing Shangrila.");
        if(GameObject.Find("Shangrila - user blockout")!=null) throw new Exception("Shangrila is already placed; existing placement has been preserved.");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        model.name="Shangrila - user blockout";Undo.RegisterCreatedObjectUndo(model,"Place Shangrila");
        var renderers=model.GetComponentsInChildren<MeshRenderer>();
        Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        model.transform.position=new Vector3(0.2f-bounds.center.x,0.2f-bounds.min.y,5.05f-bounds.min.z);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        bounds=renderers[0].bounds;foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        foreach(var renderer in renderers)
        {
            var filter=renderer.GetComponent<MeshFilter>();
            if(filter!=null && renderer.GetComponent<Collider>()==null) renderer.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
        }
        var placeholder=GameObject.Find("Bar Entrance - standalone visual vestibule");
        if(placeholder!=null) {Undo.RecordObject(placeholder,"Disable placeholder bar");placeholder.SetActive(false);}
        var source=GameObject.Find("McPato - Buildings and Facades (CC BY 4.0)");
        var hidden=new System.Collections.Generic.List<string>();
        if(source!=null) foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>())
        {
            if(renderer.name.StartsWith("Circle.") || renderer.name=="Plane.028" || renderer.bounds.max.y<0.75f) continue;
            if(!renderer.bounds.Intersects(bounds)) continue;
            Undo.RecordObject(renderer.gameObject,"Clear Shangrila footprint");renderer.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);hidden.Add(renderer.name);
        }
        var bar=renderers.FirstOrDefault(r=>r.name=="shangria") ?? renderers[0];
        var lighting=new GameObject("Shangrila - placement lighting");Undo.RegisterCreatedObjectUndo(lighting,"Shangrila lighting");
        var inside=new GameObject("Warm interior light").AddComponent<Light>();inside.transform.SetParent(lighting.transform);
        inside.transform.position=new Vector3(bar.bounds.center.x,bar.bounds.min.y+2.15f,bar.bounds.center.z);
        inside.type=LightType.Point;inside.color=new Color(1,0.69f,0.4f);inside.intensity=3;inside.range=11;inside.shadows=LightShadows.Soft;
        var spill=new GameObject("Entrance spill toward street").AddComponent<Light>();spill.transform.SetParent(lighting.transform);
        spill.transform.position=new Vector3(bar.bounds.center.x,bar.bounds.min.y+2.25f,bar.bounds.min.z+0.2f);
        spill.type=LightType.Spot;spill.color=new Color(1,0.68f,0.37f);spill.intensity=6;spill.range=10;spill.spotAngle=95;spill.innerSpotAngle=65;spill.shadows=LightShadows.Soft;
        spill.transform.LookAt(new Vector3(bar.bounds.center.x,0.25f,bar.bounds.min.z-3.5f));
        foreach(var light in new[]{inside,spill})
        {
            light.shadowBias=0.025f;light.shadowNormalBias=0.1f;
            var data=new SerializedObject(light.GetUniversalAdditionalLightData());data.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=0;data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Selection.activeGameObject=model;
        Capture(bounds);
        File.WriteAllText("Logs/ShangrilaPlacement.txt",$"PLACED: {ModelPath}\nScene: {scene.path}\nPosition: {model.transform.position}\nScale: {model.transform.localScale}\nBounds: {bounds}\nDisabled placeholder and overlapping buildings: {string.Join(", ",hidden)}\nOriginal FBX/materials retained; colliders added as scene overrides.");
        Debug.Log("SHANGRILA_PLACEMENT_OK");
    }
    [MenuItem("Tools/ProjectCRT/Verify Shangrila Placement")]
    public static void Verify()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Outdoor_BarStreetTest.unity") throw new Exception("Open Outdoor_BarStreetTest first.");
        var model=GameObject.Find("Shangrila - user blockout");
        if(model==null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model)!=ModelPath) throw new Exception("Placed source model is missing.");
        var renderers=model.GetComponentsInChildren<MeshRenderer>();
        Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        if(model.GetComponentsInChildren<MeshCollider>().Length!=2) throw new Exception("Expected both model mesh colliders.");
        Capture(bounds);File.AppendAllText("Logs/ShangrilaPlacement.txt","\nVERIFIED: source FBX reference and both mesh colliders.");
    }
    static void Capture(Bounds bounds)
    {
        var go=new GameObject("Bar Retro Verification");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);
        camera.enabled=false;camera.GetUniversalAdditionalCameraData().SetRenderer(1);camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        camera.transform.position=new Vector3(-13,2.0f,0);camera.transform.LookAt(new Vector3(bounds.center.x,2.4f,bounds.min.z+5));camera.fieldOfView=70;
        var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();var previous=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            File.WriteAllBytes("Logs/ShangrilaPlacement.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally {RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);}
    }
}
