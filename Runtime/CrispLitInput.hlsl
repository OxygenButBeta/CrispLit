#ifndef CRISP_LIT_INPUT_INCLUDED
#define CRISP_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

// SRP batcher: layout tum pass'lerde ayni kalmali, property'leri ifdef'leme.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
half _SpecAAVariance;
half _SpecAAThreshold;
half _Surface;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// MADS mask map (HDRP paketlemesiyle ayni): R=Metallic, G=AO, B=Detail mask (rezerve), A=Smoothness
TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

// Isim URP'nin LitMetaPass/DepthNormals gibi paylasilan pass dosyalarinin bekledigi kontrat;
// bu sayede o pass'ler fork'lanmadan aynen kullaniliyor.
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;

    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

#if defined(_MASKMAP)
    half4 mads = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
    outSurfaceData.metallic = mads.r * _Metallic;
    outSurfaceData.occlusion = LerpWhiteTo(mads.g, _OcclusionStrength);
    outSurfaceData.smoothness = mads.a * _Smoothness;
#else
    outSurfaceData.metallic = _Metallic;
    outSurfaceData.occlusion = half(1.0);
    outSurfaceData.smoothness = _Smoothness;
#endif

    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    outSurfaceData.clearCoatMask = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
}

#endif
