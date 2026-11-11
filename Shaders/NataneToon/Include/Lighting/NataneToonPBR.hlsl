#ifndef NATANE_TOON_PBR_INCLUDED
#define NATANE_TOON_PBR_INCLUDED

// ===== Geometric specular anti-aliasing =====
// Tokuyoshi & Kaplanyan, "Improved Geometric Specular Antialiasing" (I3D 2019).
// Where the geometric normal changes quickly across a pixel (thin or strongly curved
// geometry) the GGX lobe is narrower than the pixel footprint and the highlight
// sparkles, which is very visible in VR without temporal AA. The kernel below is the
// extra roughness that covers the footprint. It is computed once per pixel by the
// fragment shader and consumed by every GGX lobe through NatanePBRSpecular.
static half nataneGSAAKernel = 0.0;   // 0 = feature off (default)

half NataneComputeGSAAKernel(half3 geomNormalWS)
{
    half3 du = ddx(geomNormalWS);
    half3 dv = ddy(geomNormalWS);
    half variance = 0.25 * (dot(du, du) + dot(dv, dv));   // screen-space variance sigma^2 = 0.25
    return min(2.0 * variance, 0.18);                      // clamping threshold kappa = 0.18
}

// GGX Normal Distribution Function (Trowbridge-Reitz)
half NataneGGX_D(half NdotH, half roughness)
{
    half a2 = roughness * roughness;
    half d = NdotH * NdotH * (a2 - 1.0) + 1.0;
    return a2 / (UNITY_PI * d * d + 1e-7);
}

// Smith-GGX Geometry Function (Height-Correlated)
half NataneGGX_G(half NdotL, half NdotV, half roughness)
{
    half a2 = roughness * roughness;
    half gL = NdotV * sqrt(NdotL * NdotL * (1.0 - a2) + a2);
    half gV = NdotL * sqrt(NdotV * NdotV * (1.0 - a2) + a2);
    return 0.5 / (gL + gV + 1e-7);
}

// Schlick Fresnel
half3 NataneSchlickFresnel(half cosTheta, half3 F0)
{
    return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

// Full PBR Direct Specular (Cook-Torrance microfacet BRDF)
half3 NatanePBRSpecular(half3 normal, half3 viewDir, half3 lightDir,
                         half roughness, half3 F0, half3 lightColor, half atten)
{
    // Geometric specular AA: "roughness" is used as the GGX alpha here, so the kernel is
    // added to alpha^2. With the kernel at 0 this is an exact no-op.
    roughness = sqrt(saturate(roughness * roughness + nataneGSAAKernel));

    half3 H = normalize(viewDir + lightDir);
    half NdotH = max(0.001, dot(normal, H));
    half NdotL = max(0.001, dot(normal, lightDir));
    // Mirror-safe NdotV
    float3 pbrViewNormal = mul((float3x3)UNITY_MATRIX_V, normal);
    pbrViewNormal.x *= NataneMirrorSign();
    half NdotV = max(0.001, dot(normalize(pbrViewNormal), float3(0, 0, 1)));
    half VdotH = max(0.001, dot(viewDir, H));

    half D = NataneGGX_D(NdotH, roughness);
    half G = NataneGGX_G(NdotL, NdotV, roughness);
    half3 F = NataneSchlickFresnel(VdotH, F0);

    half3 spec = D * G * F;
    return spec * lightColor * atten * NdotL;
}

// Indirect Specular from Unity Reflection Probes
half3 NatanePBRIndirectSpecular(half3 worldNormal, half3 viewDir, half3 worldPos,
                                half roughness, half3 F0)
{
    half3 reflDir = reflect(-viewDir, worldNormal);
    half mipLevel = roughness * 7.0;

    // Box projection for Reflection Probe 0 (if enabled)
    #if UNITY_SPECCUBE_BOX_PROJECTION
        half3 reflDir0 = BoxProjectedCubemapDirection(reflDir, worldPos,
            unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax);
    #else
        half3 reflDir0 = reflDir;
    #endif

    half4 envSample = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflDir0, mipLevel);
    half3 envColor = DecodeHDR(envSample, unity_SpecCube0_HDR);

    // Approximate environment BRDF (Karis 2014 split-sum approximation, mirror-safe)
    float3 indViewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    indViewNormal.x *= NataneMirrorSign();
    half NdotV = max(0.001, dot(normalize(indViewNormal), float3(0, 0, 1)));
    half3 F = NataneSchlickFresnel(NdotV, F0);
    half surfaceReduction = 1.0 / (roughness * roughness + 1.0);
    half grazingTerm = saturate((1.0 - roughness) + max(F0.r, max(F0.g, F0.b)));
    half3 indirectF = lerp(F, half3(grazingTerm, grazingTerm, grazingTerm),
                           pow(1.0 - NdotV, 5.0));

    return envColor * indirectF * surfaceReduction;
}

#endif
