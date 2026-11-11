#ifndef NATANE_TOON_LIGHTING_INCLUDED
#define NATANE_TOON_LIGHTING_INCLUDED

#include "NataneToonThirdPartyLighting.hlsl"

// Lighting Calculation Functions

// ===== 影の自然さ（単一入口） =====
//
// 「影のフェード感」を決める操作子はこれまで 9 個に散らばっており
// (_WrapAmount / _LitSoftness / _ShadowSmoothing / _ShadowBlend /
//  _StepBorderSmooth / _ShadingGradientWidth / _SoftLightingIntensity /
//  _ShadingMode / _ShadowSharpness)、どれを触れば「PBR のような自然な影」に
// なるのか分からない状態だった。_ShadowNaturalness はそれらの代表値を
// まとめて押し上げる入口。
//
// いずれも max() で「下限を引き上げる」だけにしてあるのが要点。上書きにすると
// 個別調整した値が無視されて、詳細設定が効かなくなる。
//
// 係数は「1.0 で PBR 寄りの自然な減衰、既定 0 で従来と完全に同一」になるよう定めた。

float NataneShadowNaturalWrap()
{
    // 0.35 はハーフランバート(0.5)よりやや控えめ。トゥーンの陰影は残る。
    return max(_WrapAmount, _ShadowNaturalness * 0.35);
}

float NataneShadowNaturalSmoothing()
{
    // 段階を連続へ寄せる量。1.0 まで上げるとトゥーンの段が完全に消えるので 0.6 止まり。
    return max(_ShadowSmoothing, _ShadowNaturalness * 0.6);
}

float NataneShadowNaturalStepBorder()
{
    return max(_StepBorderSmooth, _ShadowNaturalness * 0.35);
}

// ===== Bump terminator softening =====
// Estevez, Lecocq, Stein, "A Microfacet-Based Shadowing Function to Solve the Bump
// Terminator Problem" (Ray Tracing Gems, 2019). A normal map can light pixels the real
// geometry turns away from the light; with a hard toon step that shows as a jagged,
// leaking terminator. The mapped normal's deviation from the geometric normal is treated
// as a microfacet roughness and the matching GGX shadowing term fades the leak smoothly.
//   Ng = interpolated vertex normal, Ns = normal-mapped normal, L = light direction.
// Returns 1 when the two normals agree (no bump), so unmapped areas are unchanged.
half NataneBumpTerminatorShadow(half3 Ng, half3 Ns, half3 L)
{
    half cosNgL = dot(Ng, L);
    half cosNgNs = dot(Ng, Ns);
    half cosNsL = dot(Ns, L);

    half cosI = min(abs(cosNgL), 0.9999);
    half cosD = min(abs(cosNgNs), 0.9999);
    half alpha2 = saturate(0.125 * (1.0 / max(cosD * cosD, 1e-4) - 1.0));
    half tan2I = 1.0 / max(cosI * cosI, 1e-6) - 1.0;
    half g1 = 2.0 / (1.0 + sqrt(1.0 + alpha2 * tan2I));

    // Light on the geometric back side while the mapped normal still faces it: pure leak.
    return (cosNgL * cosNgNs * cosNsL < 0.0) ? 0.0 : g1;
}

// ===== Non-linear irradiance from L1 spherical harmonics =====
// Light Volumes store L0 + L1 only. The linear evaluation (L0 + dot(L1, n)) under-shoots
// the lit side and goes flat or negative on the far side. Both reconstructions below add
// back the contrast a higher-order fit would have, from the same four coefficients.
// Inputs follow the Unity / VRC Light Volumes convention (linear eval = L0 + dot(L1, n)).

// "ZH3: Quadratic Zonal Harmonics" (Roughton et al., I3D 2024), hallucinated variant:
// adds the missing L2 zonal lobe around one shared luminance axis, so there is no
// per-channel hue shift and no pow().
float3 NataneSHL1ZH3(float3 n, float3 L0, float3 L1r, float3 L1g, float3 L1b)
{
    const float3 lumWeights = float3(0.2126, 0.7152, 0.0722);
    float3 axis = L1r * lumWeights.r + L1g * lumWeights.g + L1b * lumWeights.b;
    axis *= rsqrt(max(dot(axis, axis), 1e-8));

    float3 l1Axis = float3(dot(L1r, axis), dot(L1g, axis), dot(L1b, axis));
    float3 ratio = 0.8660254 * abs(l1Axis) / max(L0, 1e-5);
    float3 zonal = L0 * (0.08 * ratio + 0.6 * ratio * ratio);

    float fZ = dot(axis, n);
    float3 linearEval = L0 + float3(dot(L1r, n), dot(L1g, n), dot(L1b, n));
    return max(0.0, linearEval + 0.2795085 * zonal * (3.0 * fZ * fZ - 1.0));
}

// Geomerics non-linear fit (Hazel, "Converting SH Radiance to Irradiance"), per channel.
// Never negative; restores the full dynamic range of a strongly directional probe.
float NataneSHL1Geomerics(float L0, float3 L1, float3 n)
{
    float R0 = max(L0, 1e-5);
    float3 R1 = 0.5 * L1;
    float lenR1 = max(length(R1), 1e-5);
    float q = 0.5 * (1.0 + dot(R1 / lenR1, n));
    float r = min(lenR1 / R0, 1.0);
    float p = 1.0 + 2.0 * r;
    float a = (1.0 - r) / (1.0 + r);
    return R0 * (a + (1.0 - a) * (p + 1.0) * pow(max(q, 1e-5), p));
}

#if defined(_USE_LIGHT_VOLUME) || defined(NATANE_FORCE_LIGHTVOLUME_HELPERS)
// mode: 0 = linear (identical to LightVolumeEvaluate), 1 = ZH3, 2 = Geomerics.
float3 NataneLightVolumeEvaluateMode(float3 n, float3 L0, float3 L1r, float3 L1g, float3 L1b, float mode)
{
    if (mode < 0.5)
        return LightVolumeEvaluate(n, L0, L1r, L1g, L1b);
    if (mode < 1.5)
        return NataneSHL1ZH3(n, L0, L1r, L1g, L1b);
    return float3(
        NataneSHL1Geomerics(L0.r, L1r, n),
        NataneSHL1Geomerics(L0.g, L1g, n),
        NataneSHL1Geomerics(L0.b, L1b, n));
}
#endif

// ===== Light Volume key light (toon decomposition of L0 + L1) =====
// Baked / volume lighting arrives as irradiance SH, which shades like a soft gradient and
// gives a toon material nothing to put its terminator on. This splits the four
// coefficients into the pieces a toon pipeline expects:
//   keyDir    dominant direction d of the L1 band (shared luminance axis, no hue split)
//   keyColor  irradiance facing d:        E(d)  = L0 + L1.d
//   fillColor irradiance facing away:     E(-d) = L0 - L1.d   (what really reaches the shade)
// Feeding keyDir / keyColor to the normal toon path and using fillColor as the ambient
// floor reproduces E(d) on the lit side exactly, keeps a painted shadow colour from
// dropping below the light that is physically there, and puts a clean toon boundary
// where the baked light actually comes from (per pixel, so it follows the avatar
// through the volume and includes point light volumes).
// Inputs follow the VRC Light Volumes convention (linear eval = L0 + dot(L1, n)).
void NataneSHL1KeyLight(float3 L0, float3 L1r, float3 L1g, float3 L1b,
                        out half3 keyDir, out half3 keyColor, out half3 fillColor)
{
    const float3 lumWeights = float3(0.2126, 0.7152, 0.0722);
    float3 axis = L1r * lumWeights.r + L1g * lumWeights.g + L1b * lumWeights.b;
    float axisLenSq = dot(axis, axis);
    // Perfectly uniform ambient has no direction: light from above, like the SH fallback.
    float3 d = (axisLenSq > 1e-8) ? axis * rsqrt(axisLenSq) : float3(0.0, 1.0, 0.0);
    float3 l1d = float3(dot(L1r, d), dot(L1g, d), dot(L1b, d));
    keyDir = d;
    keyColor = max(0.0, L0 + l1d);
    fillColor = max(0.0, L0 - l1d);
}

// ===== Matte diffuse: energy-preserving Oren-Nayar (EON) =====
// Portsmouth, Kutz, Hill, "EON: A Practical Energy-Preserving Rough Diffuse BRDF"
// (JCGT 2025). Rough, powdery surfaces (unglazed clay, cotton, dry skin, felt) do not
// follow Lambert: they stay brighter toward the terminator when lit from the viewer's
// side and fall off faster when lit from behind, which is the cue that reads as "matte".
// The multiple-scattering term returns the energy classic Oren-Nayar loses and, because
// it is tinted by albedo^2, deepens the saturation of coloured matte surfaces.
// Returns the BRDF relative to Lambert (multiply N.L by it): 1 at roughness 0.
//   NdotL, NdotV must be > 0;  LdotV = dot(L, V);  r = roughness 0..1;  rho = albedo.
half3 NataneEONDiffuseRatio(half NdotL, half NdotV, half LdotV, half r, half3 rho)
{
    const half c1 = 0.5 - 2.0 / (3.0 * UNITY_PI);
    const half c2 = 2.0 / 3.0 - 28.0 / (15.0 * UNITY_PI);

    half AF = 1.0 / (1.0 + c1 * r);

    // Single scattering (Fujii's improved Oren-Nayar, the "FON" term)
    half s = LdotV - NdotL * NdotV;
    half sOverT = (s > 0.0) ? s / max(max(NdotL, NdotV), 1e-3) : s;
    half single = AF * (1.0 + r * sOverT);

    // Multiple scattering: analytic fit of the directional albedo E(mu)
    half mcL = 1.0 - NdotL;
    half mcV = 1.0 - NdotV;
    half gL = mcL * (0.0571085289 + mcL * (0.491881867 + mcL * (-0.332181442 + mcL * 0.0714429953)));
    half gV = mcV * (0.0571085289 + mcV * (0.491881867 + mcV * (-0.332181442 + mcV * 0.0714429953)));
    half EL = AF * (1.0 + r * gL);
    half EV = AF * (1.0 + r * gV);
    half avgE = AF * (1.0 + c2 * r);
    half3 rhoMsOverRho = rho * avgE / max(1.0 - rho * (1.0 - avgE), 1e-3);
    half3 multi = rhoMsOverRho * (max(1e-4, 1.0 - EV) * max(1e-4, 1.0 - EL) / max(1e-4, 1.0 - avgE));

    return max(0.0, single) + multi;
}

// Toon Shading with adjustable steps and sharpness
// Creates cel-shaded stepped lighting effect
float ToonShading(float ndotl, float steps, float sharpness)
{
    // Apply shadow offset to adjust shadow threshold (clamped to prevent extreme values)
    ndotl = saturate(ndotl + clamp(_ShadowOffset, -1.0, 1.0));

    // Apply shadow blend (softness) if enabled
    #ifdef _SOFT_LIGHTING_MODE
        sharpness = lerp(sharpness, sharpness * 2.0, _SoftLightingIntensity);
    #endif

    // Apply shadow blend parameter for individual softness control
    if (_ShadowBlend > 0.001)
    {
        sharpness = lerp(sharpness, sharpness * (1.0 + _ShadowBlend * 2.0), _ShadowBlend);
    }

    // For anime-style clean shadows, use a sharper threshold approach
    // This creates more distinct light/shadow boundaries
    float stepValue = 1.0 / max(steps, 1.0);

    // Calculate which step we're on
    float currentStep = floor(ndotl * steps);

    // Calculate position within the current step
    float stepPosition = frac(ndotl * steps);

    // Apply anti-aliasing to prevent harsh pixelation while maintaining sharpness
    // Use smaller smoothstep range for cleaner anime look
    // _StepBorderSmooth を追加して段階境界のなじませ幅を拡張
    float smoothRange = saturate(sharpness + NataneShadowNaturalStepBorder()) * 0.5;
    // Floor the band by the screen-space derivative so the cel boundary stays
    // ~1px anti-aliased at any distance/angle (prevents shimmering, esp. in VR).
    smoothRange = min(max(smoothRange, fwidth(ndotl * steps) * 0.5), 0.5);
    float smoothedStep = smoothstep(0.5 - smoothRange, 0.5 + smoothRange, stepPosition);

    // Combine for final toon value with better precision
    float toon = (currentStep + smoothedStep) * stepValue;

    return saturate(toon);
}

// Gradient Shading
// Creates smooth gradient lighting effect for softer appearance
float GradientShading(float ndotl, float gradientWidth)
{
    // Apply shadow offset to adjust shadow threshold (clamped to prevent extreme values)
    ndotl = saturate(ndotl + clamp(_ShadowOffset, -1.0, 1.0));

    // Apply soft lighting mode to make gradient even softer
    #ifdef _SOFT_LIGHTING_MODE
        gradientWidth = lerp(gradientWidth, gradientWidth * 1.5, _SoftLightingIntensity);
    #endif

    // Apply shadow blend for additional softness control
    if (_ShadowBlend > 0.001)
    {
        gradientWidth = lerp(gradientWidth, gradientWidth * (1.0 + _ShadowBlend), _ShadowBlend);
    }

    // Calculate shadow boundary position (0.5 is the default lit/shadow boundary)
    float shadowBoundary = 0.5;

    // Create smooth gradient using smoothstep
    // gradientWidth controls the softness of the transition
    float halfWidth = gradientWidth * 0.5;
    // Keep the boundary at least ~1px wide in screen space to avoid aliasing.
    halfWidth = max(halfWidth, fwidth(ndotl) * 0.5);
    float gradient = smoothstep(shadowBoundary - halfWidth, shadowBoundary + halfWidth, ndotl);

    return saturate(gradient);
}

// Toon-style vertex light calculation at pixel precision
// Provides higher quality per-pixel toon shading for vertex lights (ForwardBase 4 point lights)
// while keeping a clear directional falloff so point lights do not behave like a flat color wash.
half3 CalculateVertexLightsPixelPrecision(
    float3 worldPos,
    half3 worldNormal,
    float shadowSteps,
    float shadowSharpness,
    float shadingMode,
    float gradientWidth,
    int excludedLightIndex)
{
    half3 totalLight = 0;
    UNITY_UNROLL
    for (int i = 0; i < 4; i++)
    {
        if (i == excludedLightIndex)
        {
            continue;
        }

        float3 lightPos = float3(unity_4LightPosX0[i], unity_4LightPosY0[i], unity_4LightPosZ0[i]);
        float3 toLight = lightPos - worldPos;
        float distSq = max(dot(toLight, toLight), 0.000001);
        half3 lightDir = toLight * rsqrt(distSq);
        half ndotl = max(0, dot(worldNormal, lightDir));
        half toonLight = ToonShading(ndotl, shadowSteps, shadowSharpness);
        half gradientLight = GradientShading(ndotl, gradientWidth);
        half stylizedLight = toonLight;
        half useGradient = step(HALF_VALUE, shadingMode) * (1.0 - step(1.5, shadingMode));
        half usePbrLike = step(2.5, shadingMode);
        half pbrLikeLight = saturate(ndotl + clamp(_ShadowOffset, -1.0, 1.0));
        stylizedLight = lerp(stylizedLight, gradientLight, useGradient);
        half shapedLight = lerp(stylizedLight * ndotl, pbrLikeLight, usePbrLike);
        float atten = 1.0 / (1.0 + distSq * unity_4LightAtten0[i]);
        totalLight += unity_LightColor[i].rgb * shapedLight * atten;
    }
    return totalLight;
}

// Apply Light Blend (Softness)
// Softens lighting transitions for smoother appearance
float ApplyLightBlend(float lightValue)
{
    #ifdef _SOFT_LIGHTING_MODE
        // Global soft lighting mode
        float softness = _SoftLightingIntensity;
        lightValue = smoothstep(0.0, 1.0, lightValue * (1.0 + softness));
    #endif

    // Individual light blend control
    if (_LightBlend > 0.001)
    {
        lightValue = smoothstep(0.0, 1.0, lightValue * (1.0 + _LightBlend));
    }

    // Highlight softness
    if (_HighlightSoftness > 0.001 && lightValue > 0.7)
    {
        // Soften highlights (bright areas)
        float highlightFactor = (lightValue - 0.7) / 0.3; // Normalize to 0-1 for highlight range
        highlightFactor = smoothstep(0.0, 1.0, highlightFactor * (1.0 - _HighlightSoftness));
        lightValue = lerp(0.7, 1.0, highlightFactor);
    }

    return saturate(lightValue);
}

// Dithering Pattern (Bayer Matrix)
// Creates a dithering effect for softer shadow transitions
float DitheringPattern(float2 screenPos, float scale)
{
    return NataneGetFadeDitherThreshold(StabilizeDitherCoord(screenPos), scale);
}

// Specular occlusion approximation based on visibility, view angle, and roughness.
// This keeps micro-cavity areas from receiving unrealistically strong highlights.
half NataneSpecularOcclusion(half visibility, half ndotv, half roughness)
{
    visibility = saturate(visibility);
    ndotv = saturate(ndotv);
    roughness = saturate(roughness);

    half exponent = exp2(-16.0 * roughness - 1.0);
    return saturate(pow(ndotv + visibility, exponent) - 1.0 + visibility);
}

// Ramp Texture Shading
// Uses a gradient texture to control shadow colors
#if defined(_USE_RAMP)
float3 RampShading(float ndotl)
{
    float2 rampUV = float2(saturate(ndotl + _ShadowOffset), 0.5);
    return NATANE_SAMPLE_CLAMP(_RampTex, rampUV).rgb;
}
#endif

// Multi-tone Shadow Colors
// Applies multiple shadow color tones based on lighting intensity
float3 MultiToneShadowColor(float shadowFactor, float3 baseColor)
{
    #ifdef _USE_MULTI_SHADOW
        // Calculate which shadow level to use based on shadow factor
        // shadowFactor: 0 = darkest, 1 = brightest

        // Validate and sort shadow border values (ensure 3rd < 2nd) - branchless
        float border2nd = max(_Shadow2ndBorder, _Shadow3rdBorder);
        float border3rd = min(_Shadow2ndBorder, _Shadow3rdBorder);

        // Calculate blend width from _ShadowBlend parameter
        // _ShadowBlend=0: 0.02 (sharp, backward compatible), _ShadowBlend=1: 0.5 (wide smooth fade)
        float blendWidth = max(0.01, lerp(0.02, 0.5, _ShadowBlend) + _StepBorderSmooth * 0.3);

        // Symmetric multi-tone shadow blending using smoothstep centered on border
        // This creates a natural, even fade around each shadow boundary
        float blend2nd = smoothstep(border2nd - blendWidth * 0.5, border2nd + blendWidth * 0.5, shadowFactor);
        float blend3rd = smoothstep(border3rd - blendWidth * 0.5, border3rd + blendWidth * 0.5, shadowFactor);
        float3 shadowColor = _ShadowColor.rgb;
        shadowColor = lerp(_Shadow2ndColor.rgb, shadowColor, blend2nd);
        shadowColor = lerp(_Shadow3rdColor.rgb, shadowColor, blend3rd);

        return shadowColor * baseColor;
    #else
        // Single shadow color mode
        return _ShadowColor.rgb * baseColor;
    #endif
}

// Specular Highlight (Anime Style)
// Creates sharp, stylized specular reflections
#if defined(_SPECULAR)
half SpecularHighlight(half3 normal, half3 viewDir, half3 lightDir, half size, half softness)
{
    half3 halfVector = normalize(lightDir + viewDir);
    half ndoth = max(0.0, dot(normal, halfVector));

    half edgeMin = 1.0 - size - softness;
    half edgeMax = 1.0 - size + softness;

    #if defined(_SPECULAR_AA)
        // Expand threshold by derivatives to reduce high-frequency shimmer.
        half aaWidth = max(fwidth(ndoth) * _SpecularAAStrength, 0.0005);
        edgeMin -= aaWidth;
        edgeMax += aaWidth;
    #endif

    half spec = smoothstep(edgeMin, edgeMax, ndoth);
    return spec;
}
#endif // _SPECULAR

// Hair Specular (Kajiya-Kay)
#if defined(_HAIR_SPECULAR)
half NataneHairSurfaceWeight()
{
    return step(1.5, _SurfaceModel) * (1.0 - step(2.5, _SurfaceModel));
}

half3 ResolveHairStrandDirection(half3 worldNormal, half3 worldTangent, half3 worldBinormal, float2 uv)
{
    half3 tangent = normalize(worldBinormal);
    half directionStrength = saturate(_HairStrandDirectionStrength) * NataneHairSurfaceWeight();

    if (directionStrength > 0.001)
    {
        half2 strandDirTS = NATANE_SAMPLE_REPEAT(_HairStrandDirectionMap, uv).rg * 2.0 - 1.0;
        half strandLenSq = dot(strandDirTS, strandDirTS);

        if (strandLenSq > 0.0001)
        {
            strandDirTS *= rsqrt(strandLenSq);
            half3 mappedStrandDir = normalize(worldTangent * strandDirTS.x + worldBinormal * strandDirTS.y);
            tangent = normalize(lerp(tangent, mappedStrandDir, directionStrength));
        }
    }

    return normalize(tangent + worldNormal * 0.0001);
}

half KajiyaKaySpecular(half3 shiftedTangent, half3 halfVector, half exponent)
{
    half TdotH = dot(shiftedTangent, halfVector);
    half sinTH = sqrt(max(0.001, 1.0 - TdotH * TdotH));

    #if defined(_SPECULAR_AA)
        half aaFactor = 1.0 + fwidth(TdotH) * _SpecularAAStrength * 128.0;
        exponent = max(1.0, exponent / aaFactor);
    #endif

    return saturate(pow(sinTH, exponent));
}

half3 HairSpecularHighlight(half3 worldNormal, half3 worldTangent, half3 worldBinormal,
                             half3 viewDir, half3 lightDir, float2 uv)
{
    half3 halfVec = normalize(lightDir + viewDir);
    half3 tangent = ResolveHairStrandDirection(worldNormal, worldTangent, worldBinormal, uv);

    // Sample shift texture if enabled
    half shiftTexValue = 0.0;
    #ifdef _HAIR_SPEC_SHIFT_TEX
        shiftTexValue = NATANE_SAMPLE_SHARED_R(_HairSpecShiftTex, _MainTex, uv) - 0.5;
    #endif

    // Shift tangent along normal for each lobe
    half3 shiftedTangent1 = normalize(tangent + worldNormal * (_HairSpecShift1 + shiftTexValue) + float3(0, 0, 0.0001));
    half3 shiftedTangent2 = normalize(tangent + worldNormal * (_HairSpecShift2 + shiftTexValue) + float3(0, 0, 0.0001));

    // Calculate two specular lobes
    half spec1 = KajiyaKaySpecular(shiftedTangent1, halfVec, _HairSpecWidth1);
    half spec2 = KajiyaKaySpecular(shiftedTangent2, halfVec, _HairSpecWidth2);

    // Combine lobes with their colors
    half3 specular = spec1 * _HairSpecColor1.rgb + spec2 * _HairSpecColor2.rgb;

    // Apply mask if enabled
    #ifdef _HAIR_SPEC_MASK
        half mask = NATANE_SAMPLE_SHARED_R(_HairSpecMask, _MainTex, uv);
        specular *= mask;
    #endif

    return specular * _HairSpecIntensity;
}

half3 HairTransmissionHighlight(half3 worldNormal, half3 worldTangent, half3 worldBinormal,
                                half3 viewDir, half3 lightDir, float2 uv)
{
    half transmissionStrength = max(0.0, _HairTransmissionStrength) * NataneHairSurfaceWeight();
    if (transmissionStrength <= 0.001)
    {
        return half3(0, 0, 0);
    }

    half3 strandDir = ResolveHairStrandDirection(worldNormal, worldTangent, worldBinormal, uv);
    half3 transmissionVec = normalize(viewDir - lightDir + strandDir * 0.001);
    half strandScatter = KajiyaKaySpecular(strandDir, transmissionVec, max(1.0, _HairTransmissionPower));
    half backLit = saturate(dot(-worldNormal, lightDir));
    half viewLightAlignment = saturate(dot(viewDir, -lightDir));
    half transmissionMask = NATANE_SAMPLE_SHARED_R(_HairTransmissionMask, _MainTex, uv);
    half transmission = strandScatter * backLit * viewLightAlignment * transmissionMask * transmissionStrength;

    return _HairTransmissionColor.rgb * transmission;
}
#endif // _HAIR_SPECULAR

// Rim Light Calculation
// Creates highlights at grazing angles (edges of objects)
// Uses view-space Fresnel with mirror compensation for consistent VRChat mirror rendering
#if defined(_RIM_LIGHT)
half3 RimLighting(half3 normal, half3 viewDir, half power, half intensity)
{
    // Convert normal to view space and compensate mirror X-axis flip
    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normal);
    viewNormal.x *= NataneMirrorSign();
    // Fresnel in view space: dot against forward direction (0,0,1)
    half rim = 1.0 - saturate(dot(normalize(viewNormal), float3(0, 0, 1)));
    rim = pow(rim, power) * intensity;
    return rim * _RimColor.rgb;
}
#endif // _RIM_LIGHT

// Offset Rim Light Calculation
// Creates rim highlights with manual XY offset and optional light direction linking
#if defined(_OFFSET_RIM_LIGHT)
half3 OffsetRimLighting(half3 normal, half3 viewDir, half3 lightDir, half power, half intensity)
{
    // ビュー空間に法線を変換してXYオフセットを適用
    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normal);
    viewNormal.x *= NataneMirrorSign(); // Compensate VRChat mirror X-axis flip

    // マニュアルオフセット
    float2 manualOffset = float2(_OffsetRimOffsetX, _OffsetRimOffsetY);

    // ライト方向連動オフセット
    float3 viewLightDir = mul((float3x3)UNITY_MATRIX_V, lightDir);
    viewLightDir.x *= NataneMirrorSign(); // Compensate VRChat mirror X-axis flip
    float2 lightOffset = viewLightDir.xy * _OffsetRimLightDirStrength;

    // オフセット合成（マニュアル + ライト方向 * UseLightDir）
    float2 totalOffset = manualOffset + lightOffset * _OffsetRimUseLightDir;

    // オフセット適用
    float3 offsetViewNormal = normalize(float3(viewNormal.xy + totalOffset, viewNormal.z));

    // Fresnel計算（オフセット後）
    half rim = 1.0 - saturate(dot(offsetViewNormal, float3(0, 0, 1)));
    rim = pow(rim, power);

    // トゥーンシャープネス
    rim = smoothstep(_OffsetRimSharpness - 0.01, _OffsetRimSharpness + max(0.02, (1.0 - _OffsetRimSharpness) * 0.5), rim);

    rim *= intensity;
    return rim * _OffsetRimColor.rgb;
}
#endif // _OFFSET_RIM_LIGHT

// Sheen (Fabric Luster)
// Simulates the sheen effect of fabric materials at grazing angles
// Uses view-space Fresnel with mirror compensation
#if defined(_SHEEN)
half3 SheenHighlight(half3 normal, half3 viewDir, half3 lightDir)
{
    // Mirror-safe NdotV via view-space Fresnel
    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normal);
    viewNormal.x *= NataneMirrorSign();
    half NdotV = max(0.0, dot(normalize(viewNormal), float3(0, 0, 1)));
    half NdotL = max(0.0, dot(normal, lightDir));
    if (_SheenModel >= 0.5)
    {
        // Charlie sheen: inverted-Gaussian NDF of Estevez & Kulla, "Production Friendly
        // Microfacet Sheen BRDF" (2017) with Neubelt's visibility term (as in Filament).
        // Unlike the Fresnel-style term below it has a real microfibre lobe: the luster
        // follows the half vector and widens with roughness, so velvet, wool and fleece
        // read as different cloths instead of the same rim glow.
        half3 H = normalize(viewDir + lightDir);
        half NdotH = saturate(dot(normal, H));
        half alpha = max(_SheenRoughness * _SheenRoughness, 0.01);
        half invAlpha = 1.0 / alpha;
        half sin2h = max(1.0 - NdotH * NdotH, 0.0078125);   // keeps pow() finite on half
        half D = (2.0 + invAlpha) * pow(sin2h, invAlpha * 0.5) * (0.5 / UNITY_PI);
        half V = 1.0 / (4.0 * (NdotL + NdotV - NdotL * NdotV) + 1e-3);
        half charlie = D * V * NdotL * UNITY_PI;
        return min(charlie, 4.0) * _SheenColor.rgb * _SheenIntensity;
    }
    // Classic: grazing angle fabric luster
    half sheen = pow(1.0 - NdotV, _SheenPower) * NdotL;
    return saturate(sheen) * _SheenColor.rgb * _SheenIntensity;
}
#endif // _SHEEN

// Subsurface Scattering (Translucency)
// Simulates light passing through thin or translucent materials
#if defined(_SSS)
// lightColor: pass the light the direction came from. This used to read _LightColor0
// directly, which is black in baked-only worlds where the direction falls back to a
// vertex light / light probe, so transmission vanished there.
half3 SubsurfaceScattering(half3 normal, half3 lightDir, half3 viewDir, half thickness, half atten, float powerOverride, half3 lightColor)
{
    // Distort the normal for more realistic scattering effect
    half3 distortedNormal = normal + normalize(viewDir) * _SSSDistortion;

    // Calculate back-lit effect (light passing through the object).
    // rsqrt with an epsilon guards the case where the distortion cancels
    // the normal and the vector collapses to ~zero (normalize would NaN).
    half3 backDir = -distortedNormal * rsqrt(max(dot(distortedNormal, distortedNormal), 1e-4));
    half backLight = max(0.0, dot(backDir, lightDir));

    // Apply power function for falloff and multiply by inverse thickness
    // Thicker areas scatter less light
    backLight = pow(backLight, powerOverride) * (1.0 - thickness);

    // Apply intensity, attenuation, and light color
    backLight *= _SSSIntensity * atten;

    return backLight * _SSSColor.rgb * lightColor;
}
#endif // _SSS

// Pre-integrated Subsurface Scattering using LUT
// Uses a 2D lookup table indexed by NdotL and curvature for physically-based SSS
#if defined(_SSS) && defined(_SSS_LUT)
half3 SubsurfaceScatteringLUT(half ndotl, half3 normal, half3 worldPos, half thickness)
{
    // Curvature calculation (using fwidth)
    half curvature = saturate(length(fwidth(normal)) / max(length(fwidth(worldPos)), 0.0001) * 0.5);
    // LUT UV: X=NdotL(0~1), Y=curvature(0~1)
    float2 lutUV = float2(ndotl * 0.5 + 0.5, curvature * (1.0 - thickness));
    half3 sssLUT = NATANE_SAMPLE_CLAMP(_SSSLUTTex, lutUV).rgb;
    return sssLUT * _SSSColor.rgb * _SSSLUTScale;
}
#endif

// Cubemap Reflection (Environment Mapping)
// Samples a cubemap based on reflection vector for realistic environment reflections
// Fresnel uses view-space computation for mirror-safe grazing angle detection
#if defined(_REFLECTION)
half3 CubemapReflection(half3 worldNormal, half3 viewDir, half smoothness, half metallic)
{
    // Calculate reflection vector
    // No mirror compensation: in a mirror the view direction already comes from the
    // reflected camera position, so the world-space reflection vector is correct as is.
    // (Flipping it in view space sampled the cubemap from the wrong side in mirrors and
    // disagreed with the PBR path, which never flipped.)
    half3 reflectDir = reflect(-viewDir, worldNormal);

    // Calculate mip level based on smoothness (roughness = 1 - smoothness)
    half roughness = 1.0 - smoothness;
    half mipLevel = roughness * 7.0; // Assume 8 mip levels (0-7)

    // Sample cubemap with calculated mip level for roughness effect
    half4 reflectionSample = texCUBElod(_ReflectionCube, float4(reflectDir, mipLevel));

    // Apply reflection color tint
    half3 reflection = reflectionSample.rgb * _ReflectionColor.rgb;

    // Fresnel effect - mirror-safe via view-space dot product
    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    viewNormal.x *= NataneMirrorSign();
    half viewAngle = saturate(dot(normalize(viewNormal), float3(0, 0, 1)));

    // Apply softness to Fresnel transition - Optimized: removed branching
    // Softness creates a more gradual transition between reflected and non-reflected areas
    half softRange = _FresnelSoftness * 0.5;
    viewAngle = smoothstep(softRange, 1.0 - softRange, viewAngle);

    half fresnel = pow(1.0 - viewAngle, _FresnelPower);

    // Metallic surfaces reflect more, non-metallic reflect at grazing angles
    half reflectionStrength = lerp(fresnel, 1.0, metallic);

    // Apply blend mode (0 = Additive, 1 = Overlay)
    // Additive: Simply adds reflection to base color
    // Overlay: Blends reflection more naturally with base color
    half blendFactor = lerp(1.0, reflectionStrength, _ReflectionBlendMode);

    return reflection * reflectionStrength * _ReflectionIntensity * blendFactor;
}
#endif // _REFLECTION

// Environmental Rim (Low-angle environment reflections)
// Simulates reflections at grazing angles from environment cubemap
// Uses view-space computations for mirror-safe rendering
#if defined(_ENV_RIM)
half3 EnvironmentalRim(half3 worldNormal, half3 viewDir, float powerOverride)
{
    // World-space reflection vector; valid in mirrors without compensation (see CubemapReflection).
    half3 reflectDir = reflect(-viewDir, worldNormal);

    // Sample environment cubemap
    half3 envColor = texCUBE(_EnvRimCube, reflectDir).rgb;

    // Calculate rim factor (stronger at edges) — mirror-safe via view-space Fresnel
    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
    viewNormal.x *= NataneMirrorSign();
    half rim = 1.0 - saturate(dot(normalize(viewNormal), float3(0, 0, 1)));
    rim = pow(rim, powerOverride);

    // Apply color tint and intensity
    return envColor * _EnvRimColor.rgb * rim * _EnvRimIntensity;
}
#endif // _ENV_RIM

// Refraction Calculation
// Calculates refracted view direction for transparent materials
// Mirror-safe: compensates view-space X-axis flip for refract/reflect directions
#if defined(_REFRACTION)
float3 CalculateRefraction(float3 worldNormal, float3 viewDir, float refractionIndex)
{
    // Calculate refraction using Snell's law
    // IOR ratio: from air (1.0) to material (refractionIndex)
    float iorRatio = 1.0 / max(refractionIndex, 0.001);

    // Refract the view direction through the surface
    float3 refractDir = refract(-viewDir, worldNormal, iorRatio);

    // If total internal reflection occurs, use reflection instead
    if (dot(refractDir, refractDir) < 0.0001)
    {
        refractDir = reflect(-viewDir, worldNormal);
    }

    // Mirror compensation: flip X in view space, then back to world
    float3 viewRefract = mul((float3x3)UNITY_MATRIX_V, refractDir);
    viewRefract.x *= NataneMirrorSign();
    refractDir = mul(viewRefract, (float3x3)UNITY_MATRIX_V);

    return refractDir;
}
#endif // _REFRACTION

// Face-relative light basis, shared by ApplySDFShadow and ApplyShadowShapeRig.
//
// Both need "where is the light horizontally, relative to the face". Duplicating the
// derivation would let the two drift apart, and a rig that follows a slightly different
// notion of "front" than the SDF map does is very hard to debug from the visual alone.
//
// FdotL: +1 light in front of the face, -1 behind.
// RdotL: +1 light to the face's right, -1 to its left.
void NataneFaceLightBasis(float3 lightDir, out float FdotL, out float RdotL)
{
    // Transform face directions from object to world space.
    // The tiny epsilon keeps normalize() from producing NaN when the author leaves an
    // axis at exactly zero.
    float3 faceForward = normalize(mul((float3x3)unity_ObjectToWorld, _FaceForwardDirection.xyz) + float3(0, 0, 0.0001));
    float3 faceRight = normalize(mul((float3x3)unity_ObjectToWorld, _FaceRightDirection.xyz) + float3(0.0001, 0, 0));
    float3 faceUp = cross(faceForward, faceRight);

    // Project light direction onto the face plane (remove the vertical component).
    // NaN guard: if lightDir is zero or parallel to faceUp, fall back to faceForward.
    float3 rawLightDirFlat = lightDir - dot(lightDir, faceUp) * faceUp;
    float flatLen = length(rawLightDirFlat);
    float3 lightDirFlat = (flatLen > 0.001) ? (rawLightDirFlat / flatLen) : faceForward;

    FdotL = dot(faceForward, lightDirFlat);
    RdotL = dot(faceRight, lightDirFlat);
}

// SDF Shadow Map
// Uses signed distance field to add directional-independent shadows (like face shadows)
float ApplySDFShadow(float2 uv, float ndotl, float3 lightDir, float3 worldPos)
{
    #ifdef _SDF_MAP
        #ifdef _FACE_SDF_ROTATION
            float FdotL, RdotL;
            NataneFaceLightBasis(lightDir, FdotL, RdotL);

            // Mirror UV.x when light comes from the left
            float2 sdfUV = uv;
            sdfUV.x = (RdotL < 0) ? (1.0 - sdfUV.x) : sdfUV.x;

            // Sample SDF map
            float sdfValue = NATANE_SAMPLE_REPEAT(_SDFMap, sdfUV).r;

            // Threshold based on forward dot light.
            //
            // lightDir points from the surface toward the light, so FdotL = +1 means the
            // light is in front of the face and FdotL = -1 means it is behind.
            //
            // The baked map stores s(theta_t) where s(theta) = (1 - cos theta) / 2 is the
            // monotonic [0,1] encoding of the horizontal light angle at which the texel
            // flips into shadow. A texel is lit while s(theta) < s(theta_t), so the
            // threshold must be s(theta) itself:
            //
            //     lit  <=>  theta < theta_t
            //          <=>  cos theta > cos theta_t
            //          <=>  (1 - cos theta) / 2 < (1 - cos theta_t) / 2
            //
            // The previous form (FdotL * 0.5 + 0.5) had the sign inverted, which made a
            // front-facing light shadow almost the whole face. Fixed in v1.7 — see
            // Documentation~/NPR2026_P2_FACE_SDF_BAKE.md for the derivation.
            float threshold = (1.0 - FdotL) * 0.5 + _SDFOffset;

            // Apply softness
            float shadowEdge = _SDFSoftness * 0.5;
            float sdfShadow = smoothstep(threshold - shadowEdge, threshold + shadowEdge, sdfValue);

            // SDF fully controls face shadow when rotation is enabled
            return lerp(ndotl, sdfShadow, _SDFIntensity);
        #else
            // Original UV-fixed behavior
            float sdfValue = NATANE_SAMPLE_REPEAT(_SDFMap, uv).r;

            // Apply offset to adjust shadow threshold
            sdfValue = saturate(sdfValue + _SDFOffset);

            // Apply softness to blend shadow edges
            float shadowEdge = _SDFSoftness * 0.5;
            float sdfShadow = smoothstep(0.5 - shadowEdge, 0.5 + shadowEdge, sdfValue);

            // Combine SDF shadow with lighting shadow using intensity control
            return lerp(ndotl, ndotl * sdfShadow, _SDFIntensity);
        #endif
    #else
        return ndotl;
    #endif
}

// Shadow Shape Rig
// Locally biases the shading boundary with up to four elliptical rigs, so the shape of the
// shadow can be art-directed instead of falling out of the geometry alone.
//
//   ndotl' = saturate(ndotl + sum_i( w_i(uv) * strength_i ) * mask)
//
// strength > 0 pushes the boundary toward lit (the shadow shrinks),
// strength < 0 pushes it toward shadow (the shadow grows).
//
// A slot whose radius is zero costs a single compare, so the four slots are effectively
// free when unused — that is why they are unrolled rather than looped.
float ApplyShadowShapeRig(float2 uv, float ndotl, float3 lightDir)
{
#ifdef _SHADOW_SHAPE_RIG
    // Follow the light the way Shading Rig does: the rig drifts with the light direction
    // instead of being pinned to the mesh. Projecting onto the face basis keeps the drift
    // horizontal, which is what reads as "the shadow moved" rather than "the face turned".
    float FdotL, RdotL;
    NataneFaceLightBasis(lightDir, FdotL, RdotL);
    float2 follow = float2(RdotL, FdotL) * _ShadowRigFollowScale;

    // NOSAMPLER なので他のマスクと同じく _MainTex のサンプラーを共有する。
    // 専用サンプラーを増やすと Quest のサンプラー上限に効いてくる。
    half mask = lerp(1.0h, (half)NATANE_SAMPLE_SHARED_R(_ShadowRigMask, _MainTex, uv), (half)_ShadowRigMaskStrength);

    half bias = 0.0h;

    #define NATANE_RIG_SLOT(P, S) \
        { \
            float4 prm = P; \
            prm.xy += follow * (S).w; \
            bias += NataneRigWeight(uv, prm, radians((S).x), (half)(S).z) * (half)(S).y; \
        }

    NATANE_RIG_SLOT(_ShadowRigParams0, _ShadowRigShape0)
    NATANE_RIG_SLOT(_ShadowRigParams1, _ShadowRigShape1)
    NATANE_RIG_SLOT(_ShadowRigParams2, _ShadowRigShape2)
    NATANE_RIG_SLOT(_ShadowRigParams3, _ShadowRigShape3)

    #undef NATANE_RIG_SLOT

    // Not saturated: callers apply wrap / naturalness to the signed N.L, and clamping here
    // changed the shading as soon as the keyword was on, even with no rig placed.
    return ndotl + bias * mask;
#else
    return ndotl;
#endif
}

// Shading Grade Map
// Adjusts shadow intensity per-pixel for fine control
float ApplyShadingGradeMap(float2 uv, float shadowFactor)
{
    #ifdef _SHADING_GRADE_MAP
        // Sample shading grade map (0.5 = neutral, <0.5 = darker, >0.5 = lighter)
        float gradeValue = NATANE_SAMPLE_REPEAT(_ShadingGradeMap, uv).r;

        // Remap from 0-1 to -1 to +1 range, then scale by user parameter
        float gradeAdjust = (gradeValue - 0.5) * 2.0 * _ShadingGradeScale;

        // Apply grade adjustment to shadow factor
        shadowFactor = saturate(shadowFactor + gradeAdjust);

        return shadowFactor;
    #else
        return shadowFactor;
    #endif
}

// Glitter Effect
// Creates sparkly/shimmery effect on surfaces
half3 GlitterEffect(float2 uv, float3 worldPos, half3 viewDir, half3 normal, half3 lightDir, float blur)
{
    #ifdef _GLITTER
        // Create random glitter pattern using world position
        float3 glitterPos = worldPos * _GlitterSize * 50.0;

        // Generate pseudo-random values using sine functions
        half glitterRandom = frac(sin(dot(glitterPos, float3(12.9898, 78.233, 45.164))) * 43758.5453);

        // Apply density threshold (blur softens the hard step into smoothstep)
        half densityThreshold = 1.0 - _GlitterDensity;
        half glitterMask = lerp(step(densityThreshold, glitterRandom),
            smoothstep(densityThreshold - 0.3, densityThreshold, glitterRandom), blur);

        half glitter = 0.0;
        #if defined(_GLINTS_ADVANCED)
            // Approximate physically-plausible glints using micro-normal perturbation and NdotH lobe.
            float3 cell = floor(glitterPos * 4.0);
            float cellHash = frac(sin(dot(cell, float3(95.435, 74.231, 11.973))) * 43758.5453);
            float3 randDir = normalize(float3(
                frac(cellHash * 13.37) * 2.0 - 1.0,
                frac(cellHash * 7.91) * 2.0 - 1.0,
                frac(cellHash * 5.23) * 2.0 - 1.0));
            half3 microNormal = normalize(normal + randDir * _GlintsNormalJitter);
            half3 halfVec = normalize(viewDir + lightDir);
            half ndoth = saturate(dot(microNormal, halfVec));
            half lobe = pow(ndoth, _GlintsSharpness);

            half temporal = frac(cellHash * 21.7 + _Time.y * _GlitterSpeed * _GlintsTemporal);
            temporal = smoothstep(0.2, 0.8, temporal);
            glitter = glitterMask * lobe * temporal;
        #else
            // Animate glitter using time
            half glitterTime = _Time.y * _GlitterSpeed;
            half glitterFlicker = frac(glitterRandom * 10.0 + glitterTime);
            glitterFlicker = smoothstep(0.3, 0.7, glitterFlicker); // Pulse animation

            // Calculate view-dependent glitter intensity (mirror-safe)
            float3 glitterViewNormal = mul((float3x3)UNITY_MATRIX_V, normal);
            glitterViewNormal.x *= NataneMirrorSign();
            half viewDot = max(0.0, dot(normalize(glitterViewNormal), float3(0, 0, 1)));
            half viewFactor = viewDot * viewDot;
            glitter = glitterMask * glitterFlicker * viewFactor;
        #endif

        // Apply user mask
        half maskValue = NATANE_SAMPLE_SHARED_R(_GlitterMask, _MainTex, uv);
        glitter *= maskValue;

        return glitter * _GlitterColor.rgb * _GlitterIntensity;
    #else
        return 0;
    #endif
}

// Iridescence Effect
// Creates rainbow-like color shifts based on viewing angle
half3 IridescenceEffect(half3 normal, half3 viewDir, float2 uv, float sizeOverride)
{
    #ifdef _IRIDESCENCE
        // Calculate view-dependent angle (mirror-safe)
        float3 iriViewNormal = mul((float3x3)UNITY_MATRIX_V, normal);
        iriViewNormal.x *= NataneMirrorSign();
        half viewAngle = saturate(dot(normalize(iriViewNormal), float3(0, 0, 1)));

        // Create color shift based on view angle and size parameter
        half hueShift = (1.0 - viewAngle) * sizeOverride;
        hueShift = frac(hueShift + _IridescenceHueShift);

        // Convert hue to RGB (simplified HSV to RGB conversion)
        half3 iridColor;
        half h = hueShift * 6.0;
        half c = 1.0;
        half x = c * (1.0 - abs(fmod(h, 2.0) - 1.0));

        if (h < 1.0) iridColor = half3(c, x, 0);
        else if (h < 2.0) iridColor = half3(x, c, 0);
        else if (h < 3.0) iridColor = half3(0, c, x);
        else if (h < 4.0) iridColor = half3(0, x, c);
        else if (h < 5.0) iridColor = half3(x, 0, c);
        else iridColor = half3(c, 0, x);

        // Apply color tint
        iridColor *= _IridescenceColor.rgb;

        // Apply mask
        half maskValue = NATANE_SAMPLE_SHARED_R(_IridescenceMask, _MainTex, uv);
        iridColor *= maskValue;

        // Apply intensity and view-dependent falloff
        half oneMinusView = 1.0 - viewAngle;
        half falloff = oneMinusView * oneMinusView;
        return iridColor * _IridescenceIntensity * falloff;
    #else
        return 0;
    #endif
}

#endif // NATANE_TOON_LIGHTING_INCLUDED
