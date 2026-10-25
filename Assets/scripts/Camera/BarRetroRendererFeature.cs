using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;

public sealed class BarRetroRendererFeature : ScriptableRendererFeature
{
    public Material material;
    RetroPass pass;
    public override void Create() => pass = new RetroPass();
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var look = BarRetroLook.ForCamera(renderingData.cameraData.camera);
        if (look == null || material == null || renderingData.cameraData.renderType != CameraRenderType.Base) return;
        material.SetFloat("_ColorBits", look.colorBits);
        material.SetFloat("_Dither", look.dithering);
        material.SetFloat("_Blur", look.horizontalBlur);
        material.SetFloat("_Saturation", look.saturation);
        material.SetFloat("_Contrast", look.contrast);
        material.SetColor("_Tint", look.tint);
        material.SetFloat("_Vignette", look.vignette);
        pass.Setup(material, look.verticalResolution);
        renderer.EnqueuePass(pass);
    }
    protected override void Dispose(bool disposing) => pass?.Release();

    sealed class RetroPass : ScriptableRenderPass
    {
        Material material;
        int height;
        RTHandle low, processed;
        public RetroPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }
        public void Setup(Material value, int lines) { material = value; height = lines; }
        public void Release() { low?.Release(); processed?.Release(); }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var source = resources.activeColorTexture;
            var desc = graph.GetTextureDesc(source);
            desc.width = Mathf.Max(1, Mathf.RoundToInt((float)desc.width / desc.height * height));
            desc.height = height;
            desc.msaaSamples = MSAASamples.None;
            desc.depthBufferBits = DepthBits.None;
            desc.filterMode = FilterMode.Point;
            desc.clearBuffer = false;
            desc.name = "Bar Retro Low Resolution";
            var small = graph.CreateTexture(desc);
            desc.name = "Bar Retro Processed";
            var output = graph.CreateTexture(desc);
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, small, material, 0), "Bar Retro Downsample");
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(small, output, material, 1), "Bar Retro Blur and Dither");
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(output, source, material, 2), "Bar Retro Point Upscale");
        }

        // Also supports projects using URP's compatibility rendering mode.
        #pragma warning disable CS0672, CS0618
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var desc = data.cameraData.cameraTargetDescriptor;
            desc.width = Mathf.Max(1, Mathf.RoundToInt((float)desc.width / desc.height * height));
            desc.height = height;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref low, desc, FilterMode.Point, name: "BarRetroLow");
            RenderingUtils.ReAllocateHandleIfNeeded(ref processed, desc, FilterMode.Point, name: "BarRetroProcessed");
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get("Bar Retro Look");
            var source = data.cameraData.renderer.cameraColorTargetHandle;
            Blitter.BlitCameraTexture(cmd, source, low, material, 0);
            Blitter.BlitCameraTexture(cmd, low, processed, material, 1);
            Blitter.BlitCameraTexture(cmd, processed, source, material, 2);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        #pragma warning restore CS0672, CS0618
    }
}
