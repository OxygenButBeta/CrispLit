#ifndef CRISP_BRDF_INCLUDED
#define CRISP_BRDF_INCLUDED

// Crisp quality core. Replaces URP Lit's mobile-optimised terms with:
// - Full GGX D + height-correlated Smith V + real Schlick Fresnel (URP: Kelemen approx + 1/LoH)
// - Multiscatter energy compensation (Fdez-Aguera 2019) on direct and indirect specular
// - Split-sum environment BRDF from a preintegrated LUT (URP: an empirical surfaceReduction fit)
// - Specular occlusion (Lagarde) + horizon occlusion
// - Geometric specular AA (Tokuyoshi & Kaplanyan 2019)
// This file consumes URP's BRDFData as-is, so the interop surface is unchanged.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"

// Preintegrated split-sum DFG LUT (x = NoV, y = perceptualRoughness). CrispDFGBinder.cs binds it
// globally; when it is absent (_CrispDFGBound = 0) we fall back to Karis' analytic
// approximation, so specular can never collapse to black in a build.
TEXTURE2D(_CrispDFG);
SAMPLER(sampler_CrispDFG);
float _CrispDFGBound;

half2 CrispEnvBRDFApprox(half perceptualRoughness, half NoV)
{
    // Karis, "Physically Based Shading on Mobile" - analytic DFG approximation
    const half4 c0 = half4(-1.0, -0.0275, -0.572, 0.022);
    const half4 c1 = half4(1.0, 0.0425, 1.04, -0.04);
    half4 r = perceptualRoughness * c0 + c1;
    half a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    return half2(-1.04, 1.04) * a004 + r.zw;
}

half2 CrispDFG(half perceptualRoughness, half NoV)
{
    half2 dfg;
    UNITY_BRANCH
    if (_CrispDFGBound > 0.5)
        dfg = SAMPLE_TEXTURE2D_LOD(_CrispDFG, sampler_CrispDFG, float2(NoV, perceptualRoughness), 0).rg;
    else
        dfg = CrispEnvBRDFApprox(perceptualRoughness, NoV);
    return dfg;
}

half3 CrispEnergyCompensation(half3 specular, half perceptualRoughness, half NoV)
{
    half2 dfg = CrispDFG(perceptualRoughness, NoV);
    half Ess = max(dfg.x + dfg.y, 1e-3);
    return half3(1.0, 1.0, 1.0) + specular * (1.0 / Ess - 1.0);
}

half CrispMicroShadow(half NoL, half ao)
{
    // Chan, "Material Advances in Call of Duty: WWII" - AO-derived micro-shadow cone
    half aperture = rsqrt(max(half(1e-4), half(1.0) - ao));
    half shadow = saturate(NoL * aperture);
    return shadow * shadow;
}

half3 CrispDirectSpecular(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS, half3 energyCompensation)
{
    float3 L = float3(lightDirectionWS);
    float3 V = float3(viewDirectionWS);
    float3 N = float3(normalWS);
    float3 H = SafeNormalize(L + V);

    float NoH = saturate(dot(N, H));
    float NoL = saturate(dot(N, L));
    float NoV = saturate(dot(N, V)) + 1e-5;
    float LoH = saturate(dot(L, H));

    float D = D_GGX(NoH, brdfData.roughness);
    float Vis = V_SmithJointGGX(NoL, NoV, brdfData.roughness);
    half3 F = F_Schlick(brdfData.specular, LoH);

    half3 specularTerm = half3((D * Vis) * F) * energyCompensation;

#if REAL_IS_HALF
    specularTerm = clamp(specularTerm, 0.0, 1000.0);
#endif

    return specularTerm;
}

half3 CrispLightingPhysicallyBased(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS, half3 energyCompensation, half occlusion, bool specularHighlightsOff)
{
    half NdotL = saturate(dot(normalWS, light.direction));
    half NdotV = saturate(dot(normalWS, viewDirectionWS)) + 1e-5;
    half LdotV = dot(light.direction, viewDirectionWS);

    half microShadow = CrispMicroShadow(NdotL, occlusion);
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * NdotL * microShadow);

    half3 brdf = brdfData.diffuse * DisneyDiffuseNoPI(NdotV, NdotL, LdotV, brdfData.perceptualRoughness);
    [branch] if (!specularHighlightsOff)
    {
        brdf += CrispDirectSpecular(brdfData, normalWS, light.direction, viewDirectionWS, energyCompensation);
    }
    return brdf * radiance;
}

half CrispSpecularOcclusion(half NoV, half ao, half roughness)
{
    // Lagarde & de Rousiers, "Moving Frostbite to PBR"
    return saturate(pow(abs(NoV + ao), exp2(-16.0 * roughness - 1.0)) - 1.0 + ao);
}

half CrispHorizonOcclusion(half3 reflectVector, half3 normalWS)
{
    // Darkens environment specular where the reflection vector dips below the surface
    half horizon = saturate(1.0 + dot(reflectVector, normalWS));
    return horizon * horizon;
}

half3 CrispGlobalIllumination(BRDFData brdfData, half3 bakedGI, half occlusion, float3 positionWS,
    half3 normalWS, half3 viewDirectionWS, float2 normalizedScreenSpaceUV, half3 energyCompensation)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half NoV = saturate(dot(normalWS, viewDirectionWS));

    half3 indirectDiffuse = bakedGI;
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS, brdfData.perceptualRoughness, half(1.0), normalizedScreenSpaceUV);

    half2 dfg = CrispDFG(brdfData.perceptualRoughness, NoV);
    half3 FssEss = brdfData.specular * dfg.x + dfg.y;

    half specOcclusion = CrispSpecularOcclusion(NoV, occlusion, brdfData.roughness);
    specOcclusion *= CrispHorizonOcclusion(reflectVector, normalWS);

    half3 color = indirectDiffuse * brdfData.diffuse * occlusion;
    color += indirectSpecular * FssEss * energyCompensation * specOcclusion;

    if (IsOnlyAOLightingFeatureEnabled())
    {
        color = half3(1.0, 1.0, 1.0) * occlusion;
    }

    return color;
}

half CrispApplySpecularAA(half3 geometricNormalWS, half perceptualSmoothness, half screenSpaceVariance, half varianceThreshold)
{
    // Tokuyoshi & Kaplanyan normal-variance filter: stops specular boiling on curved/distant surfaces
    float3 du = ddx(geometricNormalWS);
    float3 dv = ddy(geometricNormalWS);
    float variance = screenSpaceVariance * (dot(du, du) + dot(dv, dv));
    float kernelRoughness2 = min(2.0 * variance, varianceThreshold);

    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(perceptualSmoothness);
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    float filteredRoughness2 = saturate(roughness * roughness + kernelRoughness2);
    float filteredRoughness = sqrt(filteredRoughness2);
    return PerceptualRoughnessToPerceptualSmoothness(filteredRoughness);
}

#endif
