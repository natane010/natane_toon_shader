// NataneToonFurCommon.hlsl - Shared fur strand model and shading
//
// Included by both fur methods:
//   NataneToonFurShell.hlsl : concentric shells (16 passes, one per layer)
//   NataneToonFurFin.hlsl   : geometry-shader fins standing on the silhouette
//
// Both read the SAME strand field, so a material can draw shells, fins, or both and
// the strands line up: a fin is a vertical slice through the volume the shells fill.
// The .shader Pass block declares the samplers and the _Fur* floats before including.

#ifndef NATANE_TOON_FUR_COMMON_INCLUDED
#define NATANE_TOON_FUR_COMMON_INCLUDED

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"
#define NATANE_UTILS_STANDALONE
#include "../Utils/NataneToonUtils.hlsl"

// Fur passes are standalone CG programs with only 3 samplers (_MainTex, _FurNoiseTex,
// _FurMask) — well within the DX11 16-sampler limit. They use standard sampler2D
// declarations from the .shader Pass block, so NOSAMPLER conversion is skipped here.

#define NATANE_FORCE_LIGHTVOLUME_HELPERS
#define NATANE_FORCE_LTCGI_HELPERS
#include "../Lighting/NataneToonThirdPartyLighting.hlsl"
#undef NATANE_FORCE_LTCGI_HELPERS
#undef NATANE_FORCE_LIGHTVOLUME_HELPERS

// The shader keyword and the material property share the same identifier.
// Undefine the keyword macro after helper includes so the runtime float can be declared safely.
#if defined(_LTCGI)
    #undef _LTCGI
#endif

// _FurMethod
#define NATANE_FUR_METHOD_SHELL 0
#define NATANE_FUR_METHOD_FIN   1
#define NATANE_FUR_METHOD_BOTH  2

// Shared samplers and variables (from NataneToonInput.hlsl via the .shader CBUFFER)
// These are declared in the .shader Pass block that includes this file
float _MatteEffect;
float _UseLightVolume;
float _LightVolumeIntensity;
float _LTCGI;
float _LTCGIIntensity;
float _LTCGISpecular;
float _LTCGIBlend;
float _LTCGIBlendMode;

// Shell and Fin are both drawn for method 2 (Shell and Fin).
bool NataneFurShellsEnabled() { return abs(_FurMethod - NATANE_FUR_METHOD_FIN) > 0.5; }
bool NataneFurFinsEnabled()   { return _FurMethod > NATANE_FUR_METHOD_SHELL + 0.5; }

float3 ApplyFurMatteQuality(float3 effect, float3 baseColor, float matteAmount)
{
    matteAmount = saturate(matteAmount);
    float effectLum = dot(max(effect, 0.0), float3(0.299, 0.587, 0.114));
    effect = lerp(effect, effectLum.xxx, matteAmount * 0.7);

    float3 tintBase = saturate(baseColor + 0.35);
    effect = lerp(effect, effect * tintBase, matteAmount * 0.45);

    float lumAfter = dot(max(effect, 0.0), float3(0.299, 0.587, 0.114));
    float peakCompression = rcp(1.0 + lumAfter * matteAmount * 1.25);
    float mattePresence = lerp(1.0, 0.55, matteAmount);
    float matteSoften = lerp(1.0, 0.9, matteAmount);
    return effect * peakCompression * matteSoften * mattePresence;
}

// ---- Procedural strand field ----

// Procedural noise for fur strand pattern (used as fallback when no noise texture is set)
float _furHash(float2 p)
{
    p = frac(p * float2(443.8975, 397.2973));
    p += dot(p, p.yx + 19.19);
    return frac(p.x * p.y);
}

// Two decorrelated randoms per cell (Hoskins hash22). Used for the strand's position
// inside its cell: reusing the scalar hash with a constant offset lines the strands up
// along diagonals, which reads as a woven pattern rather than fur.
float2 _furHash2(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// Smooth value noise for natural-looking fur strands
float _furValueNoise(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    // Smooth interpolation
    float2 u = f * f * (3.0 - 2.0 * f);
    // 4 corner hash values
    float a = _furHash(i);
    float b = _furHash(i + float2(1.0, 0.0));
    float c = _furHash(i + float2(0.0, 1.0));
    float d = _furHash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

/// Density cells per unit of _FurDensity.
///
/// _FurDensity used to mean "noise texture tiling", where one texel could hold many
/// strands. With one strand per cell the same number gives far too few, far too fat
/// hairs: at _FurDensity 45 a cell on a 2 m sphere is ~14 cm across, so each strand is
/// a 3 cm thick, 20 cm long cone — a cactus spine, not fur. The property range (1..100)
/// cannot express fur density on its own, so cells are a multiple of it. Materials
/// authored before this keep working and simply get finer fur.
#define NATANE_FUR_CELLS_PER_DENSITY 4.0

float2 NataneFurCellUV(float2 uv) { return uv * _FurDensity * NATANE_FUR_CELLS_PER_DENSITY; }

/// The coat as a HEIGHT FIELD: the value at a point is how far the strand rooted there
/// reaches, 0 (bare) to 1 (full _FurLength).
///
/// This is the order both references build fur in — lilToon reads the field from
/// `_FurNoiseMask`, hecomi from the fur texture's alpha — and the individual hairs are
/// carved out of it by thresholding against the layer height, not drawn directly.
/// It matters: near the root the whole field is above the threshold, so the coat is
/// solid and no skin shows; higher up only the peaks survive and separate into hairs;
/// at the top only the longest hairs are left. Computing each hair's cross-section
/// geometrically instead has to reproduce all of that by hand, and the failure modes
/// (bald patches, spines, welded tubes) are all failures to reproduce it.
///
/// Fragment stage only (samples with implicit derivatives).
float NataneFurStrandField(float2 uv, float layer)
{
    // A fur noise texture, exactly lilToon's `_FurNoiseMask`: brighter texels grow
    // longer strands, and the tiling (_FurNoiseTex_ST) sets the strand size.
    // Sampled unconditionally: a texture fetch inside a branch that also returns makes
    // the compiler treat the result as possibly uninitialised, and the fetch is cheap
    // next to the neighbourhood below.
    float textureField = tex2D(_FurNoiseTex, TRANSFORM_TEX(uv, _FurNoiseTex)).r;

    // No texture assigned: stand in for one. A fur noise texture is a field of small
    // soft dots, so generate exactly that — one dot per density cell, its peak value
    // being that strand's length.
    float2 cellUV = NataneFurCellUV(uv);
    float2 baseCell = floor(cellUV);
    float2 local = frac(cellUV);

    // Flat-topped dot: a solid core with a soft shoulder. A cone-shaped dot thins from
    // the very root and reads as a spine.
    float outer = lerp(0.45, 0.18, saturate(_FurAlphaCutoff));
    float core = outer * 0.35;

    // Sideways drift, in cells. Strands that all rise straight up read as velvet or a
    // brush: from any angle you see the same dots stacked on themselves, so the volume
    // never fills in. Letting each strand drift its own way makes them cross.
    float leanScale = saturate(_FurFluff) * 0.45;

    float field = 0.0;

    // A leaning strand reaches into its neighbours' cells, so the neighbourhood has to
    // be searched. Sampling only the own cell cuts every strand off at the cell border
    // and draws a grid.
    [unroll] for (int oy = -1; oy <= 1; oy++)
    {
        [unroll] for (int ox = -1; ox <= 1; ox++)
        {
            float2 offset = float2(ox, oy);
            float2 cellId = baseCell + offset;
            float2 rnd = _furHash2(cellId);

            float2 axis = rnd * 0.6 + 0.2 + offset + (rnd * 2.0 - 1.0) * leanScale * layer;
            float height = lerp(0.55, 1.0, _furHash(cellId + 91.37));

            field = max(field, height * smoothstep(outer, core, distance(local, axis)));
        }
    }

    // Low frequency term so neighbouring strands clump instead of all reaching the same
    // height. It varies LENGTH only — thresholding a smooth noise for PRESENCE leaves
    // whole basins with no fur at all, which reads as bald patches.
    field *= lerp(0.72, 1.0, _furValueNoise(cellUV * 0.25));

    return _FurUseNoiseTex > 0.5 ? textureField : field;
}

/// Strand alpha at height <paramref name="layer"/>, on lilToon's curve.
/// <paramref name="field"/> returns the height field, which the shading needs for AO.
float NataneFurStrandAlpha(float2 uv, float layer, out float field)
{
    field = NataneFurStrandField(uv, layer);

    // lilToon's `_FurRootOffset`. A negative offset drives the threshold below zero near
    // the root, so the innermost layers come out solid and no skin shows between the
    // strands; towards 0 the coat opens up and the roots separate.
    float shift = layer - layer * _FurRootOffset + _FurRootOffset;
    float shiftAbs = abs(shift);
    float alpha = saturate(field - shift * shiftAbs * shiftAbs * shiftAbs + 0.25);

    // Mask out fur entirely where the mask is black. The vertex stage already collapses
    // the shell there, but without this the collapsed layers still paint the surface.
    return alpha * tex2D(_FurMask, uv).r;
}

// ---- Displacement ----

/// Object-space displacement of a strand at height <paramref name="t"/> (0..1).
/// Shells and fins share it so a strand bends identically in both methods.
///
/// Gravity and wind are displacements OF the strand, so they scale with its length —
/// lilToon does the same (`furVector.y -= _FurGravity * length(furVector)`). They used
/// to be absolute object-space amounts here: with the defaults (gravity 0.1, length
/// 0.02) every shell was dragged five strand-lengths straight down, which reads as a
/// smeared duplicate of the mesh rather than drooping fur.
float3 NataneFurStrandOffset(float3 normalOS, float3 positionOS, float t)
{
    float3 offset = normalOS * t * _FurLength;

    // Quadratic falloff, so the droop is concentrated at the tips.
    offset.y -= _FurGravity * _FurLength * t * t;

    // Position-based phase shift, so neighbouring strands do not sway in lockstep.
    float windPhase = _Time.y * _FurWindSpeed;
    offset += _FurWindDirection.xyz * sin(windPhase + positionOS.x * 2.0)
            * _FurWindStrength * _FurLength * t;

    return offset;
}

// ---- Shading ----

/// Lit fur colour at height <paramref name="layer"/>. Shared so that a fin standing
/// next to a shell is shaded identically — any divergence here shows up as a seam
/// exactly where the two methods meet.
float3 NataneFurShade(float2 uv, float2 uv1, float layer, float field,
                      float3 worldPos, float3 worldNormal)
{
    // Base color from main texture
    float4 mainColor = tex2D(_MainTex, TRANSFORM_TEX(uv, _MainTex));

    // Root-to-tip color gradient
    float3 furColor = lerp(_FurRootColor.rgb, _FurTipColor.rgb, layer);

    // Blend fur color with main texture
    float3 finalColor = lerp(mainColor.rgb, furColor, _FurColorBlend);

    // Self-occlusion, on lilToon's curve. Darkening purely by layer height (what this
    // did before) darkens the whole coat evenly; keying it on the height field as well
    // means a long strand is lit along its length while the short ones around it stay
    // in shadow, which is where the sense of depth in a coat comes from.
    finalColor *= saturate(1.0 - field + field * layer) * _FurAO * 1.25 + 1.0 - _FurAO;

    // Simple toon lighting
    worldNormal = normalize(worldNormal);
    float3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
    float NdotL = dot(worldNormal, lightDir);
    float toonShading = smoothstep(-0.1, 0.3, NdotL);

    // Self-shadow: inner layers receive less light
    float selfShadow = lerp(1.0 - _FurShadowStrength, 1.0, layer * 0.7 + 0.3);
    toonShading *= selfShadow;

    // Apply lighting
    float3 directLight = _LightColor0.rgb * toonShading;
    // Unified SH ambient (LPPV-aware in ForwardBase; fur runs in "Always" passes where
    // UNITY_LIGHT_PROBE_PROXY_VOLUME is 0 and this collapses to the classic ShadeSH9 result).
    float3 ambient = NataneShadeSH(worldNormal, worldPos);

    // Runtime branch: the fur passes always compile LV/LTCGI helpers (via NATANE_FORCE_*),
    // unlike the main shader which uses shader_feature keywords for compile-time stripping.
    if (_UseLightVolume > 0.5)
    {
        float3 L0, L1r, L1g, L1b;
        LightVolumeSH(worldPos, L0, L1r, L1g, L1b);

        float3 directLightLV = LightVolumeEvaluate(worldNormal, L0, L1r, L1g, L1b);
        float3 indirectLightLV = LightVolumeEvaluate(-worldNormal, L0, L1r, L1g, L1b);

        directLightLV = min(directLightLV, float3(1.1, 1.1, 1.1)) * _LightVolumeIntensity;
        indirectLightLV = min(indirectLightLV, float3(1.1, 1.1, 1.1)) * _LightVolumeIntensity;

        directLight = max(directLight, directLightLV * toonShading);
        ambient = max(ambient, indirectLightLV);
    }

    finalColor *= (directLight + ambient);

    if (_LTCGI > 0.5)
    {
        float3 ltcgiDiffuse = 0;
        float3 ltcgiSpecular = 0;
        NataneLTCGIContribution(worldPos, worldNormal, viewDir, 1.0, uv1, ltcgiDiffuse, ltcgiSpecular);

        half3 preLTCGIColor = finalColor;
        half3 ltcgiLitColor = finalColor * (1.0 + ltcgiDiffuse * _LTCGIIntensity);
        finalColor = ApplyEffectBlendPost(preLTCGIColor, ltcgiLitColor, _LTCGIBlend, _LTCGIBlendMode);

        float3 furLTCGISpec = ApplyFurMatteQuality(ltcgiSpecular * _LTCGIIntensity * _LTCGISpecular, finalColor, _MatteEffect);
        finalColor = SafeAdditiveBlend(finalColor, furLTCGISpec, saturate(length(furLTCGISpec) * 0.5));
    }

    // Specular highlight on fur tips
    if (_FurSpecular > 0.001)
    {
        float3 halfDir = normalize(lightDir + viewDir);
        float spec = pow(max(0, dot(worldNormal, halfDir)), 40.0) * _FurSpecular * layer;
        float3 furSpec = _LightColor0.rgb * spec;
        furSpec = ApplyFurMatteQuality(furSpec, finalColor, _MatteEffect);
        finalColor += furSpec;
    }

    // Rim light
    if (_FurRimLight > 0.001)
    {
        float rim = 1.0 - saturate(dot(viewDir, worldNormal));
        rim = pow(rim, 3.0) * _FurRimLight * layer;
        float3 furRim = _LightColor0.rgb * rim;
        furRim = ApplyFurMatteQuality(furRim, finalColor, _MatteEffect);
        finalColor += furRim;
    }

    return finalColor;
}

#endif // NATANE_TOON_FUR_COMMON_INCLUDED
