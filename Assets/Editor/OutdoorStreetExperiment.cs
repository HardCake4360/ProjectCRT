using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class OutdoorStreetExperiment
{
    const string Source = "Assets/ThirdParty/McPatoBuildings/DoriHome2.fbx";
    const string Folder = "Assets/Settings/OutdoorStreet";
    const string ScenePath = "Assets/Scenes/Outdoor_BarStreetTest.unity";
    [InitializeOnLoadMethod] static void Watch()
    {
        EditorApplication.update -= CheckPlay;
        if (SessionState.GetBool("OutdoorStreet.PlayCheck",false)) EditorApplication.update += CheckPlay;
    }
    [MenuItem("Tools/ProjectCRT/Open Outdoor Street Test")]
    public static void Open() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }
    public static void Build()
    {
        if (File.Exists(ScenePath)) throw new Exception("Outdoor scene exists; refusing to overwrite it.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Folder); Directory.CreateDirectory("Logs/OutdoorStreet"); AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Source);
        importer.generateSecondaryUV = true;
        importer.isReadable = true;
        importer.SaveAndReimport();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        root.name = "McPato - Buildings and Facades (CC BY 4.0)";
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        float scale = 52f / Mathf.Max(bounds.size.x, bounds.size.z);
        root.transform.localScale *= scale;
        bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        root.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var mapping = new Dictionary<Material,Material>();
        foreach (var renderer in renderers)
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var src = slots[i]; if (src == null) continue;
                if (!mapping.TryGetValue(src, out var lit))
                {
                    lit = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = src.name + "_OutdoorLit" };
                    string name = src.name.ToLowerInvariant();
                    string texture = name.Contains("facade2") ? "atlas_facade2.png" : name.Contains("facade") ? "atlas_facade.png" : name.Contains("concrete") ? "Asphalt010_1K-PNG_Color.png" : name.Contains("street") ? "atlas_street.png" : name.Contains("grass") ? "grass.png" : null;
                    if (texture != null) lit.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/McPatoBuildings/Textures/" + texture));
                    lit.SetColor("_BaseColor", name.Contains("glass") ? new Color(0.19f,0.23f,0.29f) : Color.white);
                    lit.SetFloat("_Smoothness", name.Contains("glass") ? 0.35f : 0.05f);
                    lit.SetFloat("_Metallic", 0);
                    lit.SetFloat("_Cull", 0); lit.doubleSidedGI = true;
                    if (name.Contains("emit")) { lit.EnableKeyword("_EMISSION"); lit.SetColor("_EmissionColor", new Color(1,0.6f,0.2f)*1.5f); }
                    AssetDatabase.CreateAsset(lit, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + lit.name + ".mat")); mapping[src] = lit;
                }
                slots[i] = lit;
            }
            renderer.sharedMaterials = slots;
            renderer.receiveShadows = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && renderer.GetComponent<Collider>() == null) renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
        var sun = new GameObject("Moonlight - cold world light").AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = new Color(0.48f,0.65f,1); sun.intensity = 0.45f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(32,-35,0); sun.shadowStrength = 0.8f;
        RenderSettings.sun = sun;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.15f,0.22f,0.35f);
        RenderSettings.ambientEquatorColor = new Color(0.075f,0.12f,0.21f);
        RenderSettings.ambientGroundColor = new Color(0.035f,0.045f,0.075f);
        RenderSettings.reflectionIntensity = 0.15f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.105f,0.15f,0.225f); RenderSettings.fogDensity = 0.025f;
        var look = new GameObject("Outdoor Retro Look").AddComponent<BarRetroLook>();
        look.saturation = 0.8f; look.tint = Color.white; look.horizontalBlur = 0.25f; look.contrast = 0.97f;
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
        camera.fieldOfView = 60; camera.nearClipPlane = 0.08f; camera.farClipPlane = 120;
        camera.GetUniversalAdditionalCameraData().SetRenderer(1); camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position = new Vector3(32,27,-32); camera.transform.LookAt(new Vector3(0,2,0));
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        File.WriteAllLines("Logs/OutdoorStreet/model-audit.txt", renderers.Select(r=>$"{r.name}|center={r.bounds.center}|size={r.bounds.size}|rotation={r.transform.eulerAngles}|materials={string.Join(",",r.sharedMaterials.Select(m=>m.name))}"));
        Capture("overview");
        File.WriteAllText("Logs/OutdoorStreet/status.txt", "IMPORTED: " + renderers.Length + " meshes; scale="+scale);
        Finish();
        Polish();
    }
    static void Capture(string name)
    {
        var source = Camera.main; var go = new GameObject("Bar Retro Verification"); var camera = go.AddComponent<Camera>();
        camera.CopyFrom(source); camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation); camera.enabled=false;
        camera.GetUniversalAdditionalCameraData().SetRenderer(1); camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var rt = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);rt.Create(); var old=RenderTexture.active;
        try { RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt}); RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes("Logs/OutdoorStreet/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        } finally {RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
    static Material Surface(string name, Color color, float emission = 0)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name };
        material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",0.12f); material.SetFloat("_Metallic",0);
        if (emission>0) {material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*emission);}
        AssetDatabase.CreateAsset(material,Folder+"/"+name+".mat");return material;
    }
    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static Light Lamp(Transform parent,string name,Vector3 position,Color color,float intensity,float range,LightType type=LightType.Point)
    {
        var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=position;
        var light=go.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;light.range=range;
        light.shadows=LightShadows.Soft;light.shadowBias=0.025f;light.shadowNormalBias=0.1f;
        var settings=new SerializedObject(light.GetUniversalAdditionalLightData());settings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();
        return light;
    }
    static void Label(Transform parent,string name,string text,Vector3 position,float size,Color color)
    {
        var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=position;
        var label=go.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
        label.characterSize=size;label.fontSize=64;label.color=color;
    }
    static void Finish()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath) throw new Exception("Open the outdoor scene first.");
        if (GameObject.Find("Outdoor Lighting and Bar Entrance")!=null) throw new Exception("Street dressing already exists.");
        var root=GameObject.Find("McPato - Buildings and Facades (CC BY 4.0)");
        // Match the imported doors and lampposts to a human-scale street.
        root.transform.position*=1.5f;root.transform.localScale*=1.5f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
        var rig=new GameObject("Outdoor Lighting and Bar Entrance").transform;
        foreach (var fixture in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Circle.")))
        {
            var bounds=fixture.bounds;
            Vector3 position=new Vector3(bounds.center.x,bounds.max.y-0.12f,bounds.center.z+(bounds.center.z>0?-0.27f:0.27f));
            var light=Lamp(rig,"Streetlamp "+fixture.name+" - warm pool",position,new Color(1,0.68f,0.35f),8,13,LightType.Spot);
            light.spotAngle=74;light.innerSpotAngle=48;
            light.transform.LookAt(position+Vector3.down*5+Vector3.forward*(position.z>0?-1:1));
        }
        var plaster=Surface("Bar Plaster",new Color(0.34f,0.32f,0.29f));
        var wood=Surface("Bar Dark Timber",new Color(0.09f,0.045f,0.025f));
        var stone=Surface("Bar Steps",new Color(0.24f,0.26f,0.28f));
        var amber=Surface("Bar Window Glow",new Color(1,0.54f,0.18f),1.4f);
        var dark=Surface("Bar Sign Backing",new Color(0.028f,0.035f,0.045f));
        var entrance=new GameObject("Bar Entrance - standalone visual vestibule").transform;entrance.SetParent(rig);
        Box(entrance,"Threshold",new Vector3(0.2f,0.19f,5.05f),new Vector3(4.1f,0.18f,1.4f),stone);
        Box(entrance,"Interior Floor",new Vector3(0.2f,0.22f,7.1f),new Vector3(3.8f,0.15f,4),wood);
        Box(entrance,"Rear Wall",new Vector3(0.2f,1.8f,9.15f),new Vector3(3.8f,3.2f,0.2f),plaster);
        Box(entrance,"Left Wall",new Vector3(-1.7f,1.8f,7.1f),new Vector3(0.2f,3.2f,4.2f),plaster);
        Box(entrance,"Right Wall",new Vector3(2.1f,1.8f,7.1f),new Vector3(0.2f,3.2f,4.2f),plaster);
        Box(entrance,"Roof",new Vector3(0.2f,3.45f,7.1f),new Vector3(4.2f,0.2f,4.4f),wood);
        Box(entrance,"Facade Left",new Vector3(-1.05f,1.8f,5.05f),new Vector3(1.3f,3.2f,0.22f),plaster);
        Box(entrance,"Facade Right",new Vector3(1.45f,1.8f,5.05f),new Vector3(1.3f,3.2f,0.22f),plaster);
        Box(entrance,"Door Header",new Vector3(0.2f,2.95f,5.05f),new Vector3(1.2f,0.9f,0.22f),wood);
        Box(entrance,"Left Door Frame",new Vector3(-0.43f,1.4f,4.9f),new Vector3(0.12f,2.4f,0.25f),wood);
        Box(entrance,"Right Door Frame",new Vector3(0.83f,1.4f,4.9f),new Vector3(0.12f,2.4f,0.25f),wood);
        Box(entrance,"Left Lit Window",new Vector3(-1.05f,1.75f,4.9f),new Vector3(0.68f,1.0f,0.07f),amber);
        Box(entrance,"Right Lit Window",new Vector3(1.45f,1.75f,4.9f),new Vector3(0.68f,1.0f,0.07f),amber);
        Box(entrance,"Window Left Mullion",new Vector3(-1.05f,1.75f,4.84f),new Vector3(0.045f,1.0f,0.05f),wood);
        Box(entrance,"Window Right Mullion",new Vector3(1.45f,1.75f,4.84f),new Vector3(0.045f,1.0f,0.05f),wood);
        Box(entrance,"BAR Sign",new Vector3(0.2f,3.1f,4.78f),new Vector3(3.0f,0.62f,0.16f),dark);
        Label(entrance,"BAR Lettering","B A R",new Vector3(0.2f,3.1f,4.68f),0.16f,new Color(1,0.78f,0.46f));
        Label(entrance,"OPEN Lettering","OPEN",new Vector3(1.45f,1.75f,4.8f),0.065f,new Color(0.25f,0.09f,0.025f));
        Box(entrance,"Interior Counter",new Vector3(0.2f,0.85f,8.2f),new Vector3(2.9f,1.1f,0.6f),wood);
        Box(entrance,"Interior Backlit Panel",new Vector3(0.2f,1.7f,8.99f),new Vector3(2.5f,0.55f,0.05f),amber);
        var interior=Lamp(entrance,"Bar interior warm light",new Vector3(0.2f,2.65f,6.7f),new Color(1,0.63f,0.3f),2,5);
        var spill=Lamp(entrance,"Doorway spill onto pavement",new Vector3(0.2f,2.4f,5.2f),new Color(1,0.61f,0.28f),5,7,LightType.Spot);
        spill.spotAngle=85;spill.innerSpotAngle=55;spill.transform.LookAt(new Vector3(0.2f,0.1f,2.7f));
        var camera=Camera.main;camera.transform.position=new Vector3(-19,1.85f,-0.6f);camera.transform.LookAt(new Vector3(5,1.5f,3));
        camera.gameObject.AddComponent<OutdoorStreetViewer>();
        RenderSettings.fogDensity=0.021f;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        Capture("street");
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        camera.transform.position=new Vector3(-4.7f,1.75f,-1.7f);camera.transform.LookAt(new Vector3(0.2f,1.6f,5.1f));Capture("bar_entrance");
        camera.transform.position=new Vector3(14,1.85f,-1.2f);camera.transform.LookAt(new Vector3(-28,1.5f,0));Capture("fog_distance");
        camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Verify();
    }
    static void Polish()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath) throw new Exception("Open outdoor scene first.");
        var lens=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Streetlamp Warm Lens.mat") ?? Surface("Streetlamp Warm Lens",new Color(1,0.74f,0.35f),3);
        var rig=GameObject.Find("Outdoor Lighting and Bar Entrance").transform;
        foreach (var light in rig.GetComponentsInChildren<Light>().Where(l=>l.name.StartsWith("Streetlamp")))
        {
            light.intensity=14;
            if (light.transform.Find("Visible lamp lens")!=null) continue;
            var bulb=GameObject.CreatePrimitive(PrimitiveType.Sphere);bulb.name="Visible lamp lens";bulb.transform.SetParent(light.transform);
            bulb.transform.position=light.transform.position;bulb.transform.localScale=Vector3.one*0.14f;
            bulb.GetComponent<Renderer>().sharedMaterial=lens;UnityEngine.Object.DestroyImmediate(bulb.GetComponent<Collider>());
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        CaptureViews();Verify();
    }
    static void CaptureViews()
    {
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        Capture("street");
        camera.transform.position=new Vector3(-4.7f,1.75f,-1.7f);camera.transform.LookAt(new Vector3(0.2f,1.6f,5.1f));Capture("bar_entrance");
        camera.transform.position=new Vector3(14,1.85f,-1.2f);camera.transform.LookAt(new Vector3(-28,1.5f,0));Capture("fog_distance");
        camera.transform.SetPositionAndRotation(position,rotation);
    }
    [MenuItem("Tools/ProjectCRT/Check Outdoor Street Play Mode")]
    static void PlayCheck()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath) throw new Exception("Wrong scene for play check.");
        SessionState.SetBool("OutdoorStreet.PlayCheck",true);SessionState.SetInt("OutdoorStreet.PlayFrames",0);
        EditorApplication.update -= CheckPlay;EditorApplication.update += CheckPlay;
        EditorApplication.isPlaying=true;
    }
    static void CheckPlay()
    {
        if (!SessionState.GetBool("OutdoorStreet.PlayCheck",false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        int frames=SessionState.GetInt("OutdoorStreet.PlayFrames",0)+1;SessionState.SetInt("OutdoorStreet.PlayFrames",frames);
        if (frames<20) return;
        SessionState.SetBool("OutdoorStreet.PlayCheck",false);
        try
        {
            if (!HasActiveController(Camera.main)) throw new Exception("Player/viewer not active in Play Mode.");
            Capture("playmode");File.WriteAllText("Logs/OutdoorStreet/playcheck.txt","PASS: Play Mode camera/controller active, scene rendered after 20 editor updates.");
        }
        catch(Exception e) {File.WriteAllText("Logs/OutdoorStreet/playcheck.txt","FAIL: "+e);Debug.LogException(e);}
        finally {EditorApplication.update-=CheckPlay;EditorApplication.isPlaying=false;}
    }
    [MenuItem("Tools/ProjectCRT/Verify Outdoor Street Test")]
    public static void VerifyFromMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Verify();
    }
    public static void Verify()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var renderers=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>()).ToArray();
        if (renderers.Any(r=>r.sharedMaterials.Any(m=>m==null || !m.shader.isSupported || ShaderUtil.ShaderHasError(m.shader)))) throw new Exception("Missing or unsupported material.");
        if (!HasActiveController(Camera.main)) throw new Exception("Player/viewer camera was not saved.");
        int spots=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l=>l.type==LightType.Spot);
        if (!RenderSettings.fog || spots<6) throw new Exception("Fog or street lights were not saved.");
        foreach (var material in AssetDatabase.FindAssets("t:Material",new[]{Folder}).Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))))
            if ((material.name.Contains("facade")||material.name.Contains("street")) && material.GetTexture("_BaseMap")==null) throw new Exception("Atlas texture was not assigned: "+material.name);
        File.WriteAllText("Logs/OutdoorStreet/status.txt",$"COMPLETE: scene reload verified; {renderers.Length} renderers; {spots} spot lights; cold directional world light; exponential-squared distance fog; active player/viewer; source textures present.");
        Debug.Log("OUTDOOR_STREET_VERIFIED");
    }
    static bool HasActiveController(Camera camera)
    {
        if(camera==null)return false;
        var viewer=camera.GetComponent<OutdoorStreetViewer>();
        var player=camera.GetComponentInParent<PlayerControler>();
        return (viewer!=null && viewer.isActiveAndEnabled) || (player!=null && player.isActiveAndEnabled);
    }
}
