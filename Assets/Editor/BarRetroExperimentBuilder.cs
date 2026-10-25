using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BarRetroExperimentBuilder
{
    const string Folder = "Assets/Settings/BarRetro";
    const string ScenePath = "Assets/Scenes/Bar_RetroTest.unity";

    [MenuItem("Tools/ProjectCRT/Open Bar Retro Test")]
    public static void Open()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }

    public static void Build()
    {
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Test scene already exists; refusing to overwrite it.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/shaders/BarRetro.shader");
        if (shader == null || !shader.isSupported) throw new Exception("Retro shader is missing or unsupported.");
        var material = new Material(shader) { name = "BarRetro" };
        AssetDatabase.CreateAsset(material, Folder + "/BarRetro.mat");
        foreach (string platform in new[] { "PC", "Mobile" })
        {
            var original = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/Settings/{platform}_Renderer.asset");
            var renderer = UnityEngine.Object.Instantiate(original);
            renderer.name = "BarRetro_" + platform;
            renderer.rendererFeatures.Clear();
            AssetDatabase.CreateAsset(renderer, $"{Folder}/{renderer.name}.asset");
            var serializedRenderer = new SerializedObject(renderer);
            serializedRenderer.FindProperty("m_RenderingMode").intValue = 0; // Dedicated Forward renderer.
            serializedRenderer.FindProperty("m_IntermediateTextureMode").intValue = 1;
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            var feature = ScriptableObject.CreateInstance<BarRetroRendererFeature>();
            feature.name = "Bar Retro Look";
            feature.material = material;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string featureGuid, out long featureId);
            var mapOwner = new SerializedObject(renderer);
            var map = mapOwner.FindProperty("m_RendererFeatureMap");
            map.arraySize = 1;
            map.GetArrayElementAtIndex(0).longValue = featureId;
            mapOwner.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);

            // Append an optional renderer; preserve the original default and its features.
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"Assets/Settings/{platform}_RPAsset.asset");
            var serializedPipeline = new SerializedObject(pipeline);
            var list = serializedPipeline.FindProperty("m_RendererDataList");
            if (list.arraySize != 1) throw new Exception("Unexpected renderer list; choose a common free index before building.");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(1).objectReferenceValue = renderer;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.CopyAsset("Assets/Scenes/Bar.unity", ScenePath)) throw new Exception("Scene copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var settings = new GameObject("Bar Retro Look - toggle component to compare").AddComponent<BarRetroLook>();
        var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
        foreach (var camera in cameras)
        {
            ConfigureCamera(camera);
            Debug.Log($"Retro camera: {camera.name}, {camera.transform.position}, active={camera.isActiveAndEnabled}");
        }
        if (cameras.Length == 0) throw new Exception("No cameras found in the copied scene.");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.15f, 0.12f, 0.11f);
        RenderSettings.fogDensity = 0.018f;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BAR_RETRO_BUILD_OK: " + ScenePath);
        Capture(settings, cameras.FirstOrDefault(c => c.CompareTag("MainCamera")) ?? cameras[0]);
    }

    static void ConfigureCamera(Camera camera)
    {
        var data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(1);
        data.antialiasing = AntialiasingMode.None;
        camera.allowMSAA = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(data);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(camera);
    }

    public static void VerifyExisting()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (string platform in new[] { "PC", "Mobile" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"{Folder}/BarRetro_{platform}.asset");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[0], out string guid, out long id);
            var owner = new SerializedObject(renderer);
            var map = owner.FindProperty("m_RendererFeatureMap");
            map.arraySize = 1;
            map.GetArrayElementAtIndex(0).longValue = id;
            owner.ApplyModifiedPropertiesWithoutUndo();
        }
        var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
        foreach (var camera in cameras) ConfigureCamera(camera);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        // Reload from disk to check the prefab camera override survives reopening.
        scene = EditorSceneManager.OpenScene(ScenePath);
        var savedCamera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First();
        var cameraSettings = new SerializedObject(savedCamera.GetUniversalAdditionalCameraData());
        if (cameraSettings.FindProperty("m_RendererIndex").intValue != 1) throw new Exception("Camera renderer override was not saved.");
        var look = UnityEngine.Object.FindFirstObjectByType<BarRetroLook>();
        Capture(look, savedCamera);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/shaders/BarRetro.shader");
        if (ShaderUtil.ShaderHasError(shader)) throw new Exception("Retro shader failed compilation.");
        Debug.Log("BAR_RETRO_VERIFY_OK: saved camera renderer, scene settings, shader and before/after renders.");
    }

    static void Capture(BarRetroLook settings, Camera camera)
    {
        Directory.CreateDirectory("Logs/BarRetro");
        // A temporary camera avoids touching the scene camera's output configuration.
        var go = new GameObject("Bar Retro Verification");
        var preview = go.AddComponent<Camera>();
        preview.CopyFrom(camera);
        preview.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        preview.enabled = false;
        preview.GetUniversalAdditionalCameraData().SetRenderer(1);
        preview.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var target = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
        target.Create();
        try
        {
            foreach (bool enabled in new[] { false, true })
            {
                settings.effectEnabled = enabled;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(preview, request);
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes($"Logs/BarRetro/{(enabled ? "retro" : "baseline")}.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.active = previous;
            }
        }
        finally
        {
            settings.effectEnabled = true;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(go);
        }
        Debug.Log("BAR_RETRO_CAPTURE_OK");
    }
}
