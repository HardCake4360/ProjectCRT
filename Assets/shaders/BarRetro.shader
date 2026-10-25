Shader "ProjectCRT/Bar Retro"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float _ColorBits, _Dither, _Blur, _Saturation, _Contrast, _Vignette;
        float4 _Tint;
        float4 Copy(Varyings input) : SV_Target
        {
            return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
        }
        float3 ReadDisplay(float2 uv)
        {
            float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
                c = LinearToSRGB(c);
            #endif
            return c;
        }
        float4 Process(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float2 stepUV = float2(_BlitTexture_TexelSize.x, 0);
            float3 c = ReadDisplay(uv);
            c = lerp(c, c * 0.5 + (ReadDisplay(uv-stepUV) + ReadDisplay(uv+stepUV)) * 0.25, _Blur);
            c = lerp(dot(c, float3(0.299, 0.587, 0.114)).xxx, c, _Saturation);
            c = (c - 0.5) * _Contrast + 0.5;
            c *= _Tint.rgb;
            float2 edge = uv * (1-uv);
            c *= 1 - _Vignette * (1 - saturate(edge.x * edge.y * 16));
            // Same signed 4x4 pattern as the reference, evaluated in low-resolution pixels.
            static const int pattern[16] = {-4,0,-3,1, 2,-2,3,-1, -3,1,-4,0, 3,-1,2,-2};
            uint2 pixel = (uint2)floor(uv * _BlitTexture_TexelSize.zw);
            float noise = pattern[(pixel.y % 4)*4 + pixel.x % 4] * _Dither;
            float divisor = exp2(8 - _ColorBits);
            c = floor(clamp(round(saturate(c)*255) + noise, 0, 255) / divisor) / exp2(_ColorBits);
            #ifndef UNITY_COLORSPACE_GAMMA
                c = SRGBToLinear(c);
            #endif
            return float4(c, 1);
        }
        ENDHLSL
        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Copy
            ENDHLSL
        }
        Pass
        {
            Name "BlurAndDither"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Process
            ENDHLSL
        }
        Pass
        {
            Name "PointUpscale"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Copy
            ENDHLSL
        }
    }
}
