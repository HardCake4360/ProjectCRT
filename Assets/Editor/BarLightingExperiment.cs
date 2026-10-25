using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BarLightingExperiment
{
    const string ScenePath = "Assets/Scenes/Bar_RetroTest.unity";
    const string Folder = "Assets/Settings/BarRetro/Lighting";
    const string LightingScene = "Assets/Scenes/Bar_RetroLightingTest.unity";
    static Mesh GetMesh(Renderer r) { var filter = r.GetComponent<MeshFilter>(); return filter != null ? filter.sharedMesh : r is SkinnedMeshRenderer skin ? skin.sharedMesh : null; }
    public static void Audit()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Directory.CreateDirectory("Logs/BarLighting");
        var lines = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true))
            .Select(r => $"{r.name}|{r.GetType().Name}|center={r.bounds.center}|size={r.bounds.size}|mesh={AssetDatabase.GetAssetPath(GetMesh(r))}|uv2={GetMesh(r)?.uv2.Length}|materials={string.Join(";",r.sharedMaterials.Where(m=>m!=null).Select(m=>$"{AssetDatabase.GetAssetPath(m)}:{m.shader.name}"))}");
        File.WriteAllLines("Logs/BarLighting/audit.txt", lines);
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            Debug.Log($"BAR_LIGHT_AUDIT {light.name} {light.type} {light.transform.position} {light.intensity}");
        Debug.Log("BAR_LIGHT_AUDIT_OK");
    }

    [MenuItem("Tools/ProjectCRT/Open Bar Lighting Test")]
    public static void OpenLighting()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(LightingScene);
    }

    public static void Build()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your open scene before generating the lighting comparison; unsaved edits have been preserved.");
        if (File.Exists(LightingScene)) throw new InvalidOperationException("Lighting test already exists; refusing to overwrite it.");
        Directory.CreateDirectory("Logs/BarLighting");
        var before = EditorSceneManager.OpenScene(ScenePath);
        Capture("before");
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(Folder + "/Meshes");
        Directory.CreateDirectory(Folder + "/Materials");
        AssetDatabase.Refresh();
        if (!AssetDatabase.CopyAsset(ScenePath, LightingScene)) throw new Exception("Lighting scene copy failed.");
        var scene = EditorSceneManager.OpenScene(LightingScene);
        var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
        var materials = new Dictionary<Material, Material>();
        var meshes = new Dictionary<Mesh, Mesh>();
        int count = 0;
        foreach (var renderer in renderers)
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            var mesh = GetMesh(renderer);
            if (mesh == null || !renderer.gameObject.activeInHierarchy) continue;
            string path = AssetDatabase.GetAssetPath(mesh);
            bool environment = renderer is MeshRenderer && path.Contains("/Maps/") && renderer.GetComponentInParent<Interactable>() == null;
            if (!path.StartsWith("Assets/")) continue;
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var source = slots[i];
                if (source == null || source.shader.name.Contains("Transparent")) continue;
                if (!materials.TryGetValue(source, out var lit))
                {
                    bool toon = source.shader.name.Contains("ToonShader");
                    bool unlit = toon || source.shader.name.Contains("Unlit");
                    lit = unlit ? new Material(Shader.Find("Universal Render Pipeline/Lit")) : new Material(source);
                    lit.name = source.name + "_BakedLit";
                    if (unlit)
                    {
                        Color color = source.HasProperty(toon ? "_Color" : "_BaseColor") ? source.GetColor(toon ? "_Color" : "_BaseColor") : Color.white;
                        color.a = 1;
                        lit.SetColor("_BaseColor", color);
                        string textureSlot = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                        if (source.HasProperty(textureSlot))
                        {
                            lit.SetTexture("_BaseMap", source.GetTexture(textureSlot));
                            lit.SetTextureScale("_BaseMap", source.GetTextureScale(textureSlot));
                            lit.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureSlot));
                        }
                        // Keep hair cards and small character details visible from both sides.
                        lit.SetFloat("_Cull", environment ? 2 : 0);
                        lit.doubleSidedGI = !environment;
                    }
                    if (lit.HasProperty("_Smoothness")) lit.SetFloat("_Smoothness", environment ? 0.12f : 0.05f);
                    if (lit.HasProperty("_Metallic")) lit.SetFloat("_Metallic", 0);
                    AssetDatabase.CreateAsset(lit, AssetDatabase.GenerateUniqueAssetPath(Folder + "/Materials/" + Safe(lit.name) + ".mat"));
                    materials[source] = lit;
                }
                slots[i] = lit;
            }
            renderer.sharedMaterials = slots;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            if (environment)
            {
                if (!meshes.TryGetValue(mesh, out var unwrapped))
                {
                    unwrapped = UnityEngine.Object.Instantiate(mesh);
                    unwrapped.name = mesh.name + "_LightmapUV";
                    if (!Unwrapping.GenerateSecondaryUVSet(unwrapped)) throw new Exception("Could not unwrap " + mesh.name);
                    AssetDatabase.CreateAsset(unwrapped, AssetDatabase.GenerateUniqueAssetPath(Folder + "/Meshes/" + Safe(unwrapped.name) + ".asset"));
                    meshes[mesh] = unwrapped;
                }
                var filter = renderer.GetComponent<MeshFilter>();
                filter.sharedMesh = unwrapped;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.ContributeGI);
                var owner = new SerializedObject(renderer);
                owner.FindProperty("m_ReceiveGI").intValue = (int)ReceiveGI.Lightmaps;
                owner.FindProperty("m_ScaleInLightmap").floatValue = renderer.bounds.size.magnitude < 0.3f ? 0.3f : 1f;
                owner.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
            EditorUtility.SetDirty(renderer);
        }
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            light.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
        var rig = new GameObject("Bar Lighting - Warm Counter and Cool Ambient");
        AddLight(rig.transform, "Warm pendant left (Mixed)", LightType.Point, new Vector3(0, 1.94f, 1.7f), new Color(1, 0.64f, 0.32f), 1.9f, 4.5f, LightmapBakeType.Mixed);
        AddLight(rig.transform, "Warm pendant right (Mixed)", LightType.Point, new Vector3(2.1f, 1.94f, 1.7f), new Color(1, 0.7f, 0.4f), 1.6f, 4.5f, LightmapBakeType.Mixed);
        AddLight(rig.transform, "Counter key (Mixed)", LightType.Point, new Vector3(0.9f, 2.0f, 2.9f), new Color(1, 0.77f, 0.53f), 1.25f, 3.5f, LightmapBakeType.Mixed);
        var area = AddLight(rig.transform, "Counter broad bounce (Baked)", LightType.Rectangle, new Vector3(0.9f, 2.35f, 2.1f), new Color(1, 0.69f, 0.4f), 2.5f, 5, LightmapBakeType.Baked);
        area.areaSize = new Vector2(3.2f, 1.0f);
        area.transform.rotation = Quaternion.Euler(90, 0, 0);
        area = AddLight(rig.transform, "Cool side window (Baked)", LightType.Rectangle, new Vector3(3.95f, 1.65f, 4.0f), new Color(0.38f, 0.57f, 1), 2.2f, 7, LightmapBakeType.Baked);
        area.areaSize = new Vector2(1.3f, 1.5f);
        area.transform.rotation = Quaternion.LookRotation(Vector3.left);
        var fill = AddLight(rig.transform, "Cool character fill (Realtime)", LightType.Directional, Vector3.zero, new Color(0.43f, 0.6f, 1), 0.18f, 10, LightmapBakeType.Realtime);
        fill.transform.rotation = Quaternion.Euler(35, -110, 0);
        fill.shadows = LightShadows.None;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.12f, 0.16f, 0.24f);
        RenderSettings.ambientEquatorColor = new Color(0.07f, 0.085f, 0.12f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.03f, 0.025f);
        RenderSettings.reflectionIntensity = 0.2f;
        RenderSettings.fogColor = new Color(0.075f, 0.09f, 0.13f);
        RenderSettings.fogDensity = 0.012f;
        var probeObject = new GameObject("Light Probes - walkable room and counter");
        probeObject.transform.SetParent(rig.transform);
        var probes = probeObject.AddComponent<LightProbeGroup>();
        var positions = new List<Vector3>();
        foreach (float x in new[] { -2.8f, -1.4f, 0f, 1.4f, 2.8f, 3.7f })
        foreach (float z in new[] { 1.5f, 2.5f, 3.6f, 4.8f, 5.7f })
        foreach (float y in new[] { 0.3f, 1.1f, 1.8f }) positions.Add(new Vector3(x, y, z));
        probes.probePositions = positions.ToArray();
        var lighting = new LightingSettings
        {
            name = "BarRetro_BakedLighting", bakedGI = true, realtimeGI = false,
            lightmapper = LightingSettings.Lightmapper.ProgressiveCPU,
            lightmapResolution = 16, lightmapMaxSize = 1024, lightmapPadding = 4,
            directSampleCount = 32, indirectSampleCount = 128, environmentSampleCount = 64,
            maxBounces = 3, mixedBakeMode = MixedLightingMode.Shadowmask,
            directionalityMode = LightmapsMode.NonDirectional,
            ao = true, aoMaxDistance = 0.22f, aoExponentIndirect = 0.6f, aoExponentDirect = 0,
            filteringMode = LightingSettings.FilterMode.Auto
        };
        AssetDatabase.CreateAsset(lighting, Folder + "/BarRetro_BakedLighting.lighting");
        Lightmapping.lightingSettings = lighting;
        var look = UnityEngine.Object.FindFirstObjectByType<BarRetroLook>();
        look.tint = Color.white;
        look.saturation = 0.92f;
        look.contrast = 0.96f;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/BarLighting/status.txt", $"Baking: {count} static renderers, {meshes.Count} unwrapped meshes, {materials.Count} scene-only materials, {positions.Count} probes.");
        Lightmapping.bakeCompleted -= OnBaked;
        Lightmapping.bakeCompleted += OnBaked;
        if (!Lightmapping.BakeAsync()) throw new Exception("Lightmapping could not start.");
    }

    static string Safe(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
    static Light AddLight(Transform parent, string name, LightType type, Vector3 position, Color color, float intensity, float range, LightmapBakeType mode)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = type; light.color = color; light.intensity = intensity; light.range = range;
        light.lightmapBakeType = mode; light.shadows = LightShadows.Soft; light.shadowBias = 0.025f;
        light.shadowNormalBias = 0.15f; light.bounceIntensity = 1;
        return light;
    }
    static void OnBaked()
    {
        Lightmapping.bakeCompleted -= OnBaked;
        EditorApplication.delayCall += FinishBake;
    }
    static void Polish()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != LightingScene) throw new Exception("Open the lighting comparison first.");
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Point || light.lightmapBakeType != LightmapBakeType.Mixed) continue;
            var data = light.GetUniversalAdditionalLightData();
            var settings = new SerializedObject(data);
            settings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        var rig = GameObject.Find("Bar Lighting - Warm Counter and Cool Ambient");
        if (rig.transform.Find("Face readability fill (Realtime)") == null)
        {
            var fill = AddLight(rig.transform, "Face readability fill (Realtime)", LightType.Point,
                new Vector3(1.3f, 1.45f, 3.1f), new Color(1, 0.85f, 0.68f), 0.28f, 2.5f, LightmapBakeType.Realtime);
            fill.shadows = LightShadows.None;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ReloadAndVerify();
    }
    [MenuItem("Tools/ProjectCRT/Verify Bar Lighting Test")]
    static void ReloadAndVerify()
    {
        var scene = EditorSceneManager.OpenScene(LightingScene);
        var staticRenderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>())
            .Where(r => GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.ContributeGI)).ToArray();
        if (staticRenderers.Any(r => GetMesh(r) == null || GetMesh(r).uv2.Length == 0)) throw new Exception("A static mesh lost its lightmap UVs.");
        if (staticRenderers.SelectMany(r => r.sharedMaterials).Any(m => m != null && m.shader.name.Contains("ToonShader"))) throw new Exception("A baked renderer still uses the unsupported toon shader.");
        var character = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SkinnedMeshRenderer>()).First();
        if (character.lightProbeUsage != LightProbeUsage.BlendProbes) throw new Exception("Character probes were not saved.");
        FinishBake();
        File.AppendAllText("Logs/BarLighting/status.txt", "\nReload verified: lightmap UVs, GI materials, baked lightmaps, probes and captures.");
    }
    [MenuItem("Tools/ProjectCRT/Capture Bar Lighting Comparison")]
    public static void Compare()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
        Capture("before");
        ReloadAndVerify();
    }
    static void FinishBake()
    {
        try
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != LightingScene) throw new Exception("Unexpected scene after bake.");
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            int mapped = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>()).Count(r => r.lightmapIndex >= 0 && r.lightmapIndex < LightmapSettings.lightmaps.Length);
            if (LightmapSettings.lightmaps.Length == 0 || mapped == 0 || LightmapSettings.lightProbes == null || LightmapSettings.lightProbes.count == 0)
                throw new Exception("Bake did not produce valid lightmaps and probes.");
            Capture("after");
            File.WriteAllText("Logs/BarLighting/status.txt", $"COMPLETE: {LightmapSettings.lightmaps.Length} lightmap atlases, {mapped} mapped renderers, {LightmapSettings.lightProbes.count} baked probes.");
            Debug.Log("BAR_LIGHTING_COMPLETE");
        }
        catch (Exception e) { File.WriteAllText("Logs/BarLightingFailure.txt", e.ToString()); Debug.LogException(e); }
    }
    static void Capture(string prefix)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var source = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
        var go = new GameObject("Bar Retro Verification");
        var camera = go.AddComponent<Camera>();
        camera.CopyFrom(source); camera.enabled = false;
        camera.GetUniversalAdditionalCameraData().SetRenderer(1);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var target = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
        target.Create();
        var previous = RenderTexture.active;
        try
        {
            foreach (string view in new[] { "character", "room" })
            {
                // Fixed poses remain comparable even if Cinemachine updates the scene camera on reload.
                camera.fieldOfView = 55;
                if (view == "character")
                {
                    camera.transform.position = new Vector3(1.4f, 1.15f, 3.7f);
                    camera.transform.LookAt(new Vector3(0.75f, 1.0f, 2.52f));
                }
                else { camera.transform.position = new Vector3(-2.7f, 1.55f, 5.65f); camera.transform.LookAt(new Vector3(1.0f, 1.2f, 2.1f)); }
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(960, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); image.Apply();
                File.WriteAllBytes($"Logs/BarLighting/{prefix}_{view}.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
        finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go); }
    }
}
