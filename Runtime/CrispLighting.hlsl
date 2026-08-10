#ifndef CRISP_LIGHTING_INCLUDED
#define CRISP_LIGHTING_INCLUDED

// A structural copy of UniversalFragmentPBR; only the BRDF and GI calls are redirected to the
// Crisp core. Lights, shadows, decals, SSAO and the cluster loop all come from URP's own
// API, so pipeline features and third-party integrations keep working. No clear coat, by design.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "CrispBRDF.hlsl"

half4 CrispFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    BRDFData brdfData;

    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half3 energyCompensation = CrispEnergyCompensation(brdfData.specular, brdfData.perceptualRoughness, NoV);

    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);

    lightingData.giColor = CrispGlobalIllumination(brdfData, inputData.bakedGI, aoFactor.indirectAmbientOcclusion,
                                                  inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS,
                                                  inputData.normalizedScreenSpaceUV, energyCompensation);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = CrispLightingPhysicallyBased(brdfData, mainLight,
                                                                  inputData.normalWS, inputData.viewDirectionWS,
                                                                  energyCompensation, surfaceData.occlusion, specularHighlightsOff);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += CrispLightingPhysicallyBased(brdfData, light,
                                                                              inputData.normalWS, inputData.viewDirectionWS,
                                                                              energyCompensation, surfaceData.occlusion, specularHighlightsOff);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += CrispLightingPhysicallyBased(brdfData, light,
                                                                              inputData.normalWS, inputData.viewDirectionWS,
                                                                              energyCompensation, surfaceData.occlusion, specularHighlightsOff);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

#if REAL_IS_HALF
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

#endif
