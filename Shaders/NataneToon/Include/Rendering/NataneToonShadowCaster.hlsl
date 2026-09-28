#ifndef NATANE_TOON_SHADOW_CASTER_INCLUDED
#define NATANE_TOON_SHADOW_CASTER_INCLUDED

// ===================================================================
// Natane Toon Shader - Shared ShadowCaster program
// ===================================================================
// Reproduces the silhouette-changing parts of the forward passes so the
// cast shadow matches what is actually drawn:
//   - VAT vertex animation (_VAT / _VAT_NORMAL)
//   - Main texture UV animation (_MAIN_TEX_ANIMATION)
//   - Alpha mask (_ALPHA_MASK) and _Color.a
//   - Dissolve clipping (_DISSOLVE)
//   - Hashed / dithered alpha (_HASHED_ALPHA / _DITHERING_ALPHA)
//   - _Cutoff test when CUTOUT_VARIANT is defined by the pass
//
// This file deliberately does NOT include NataneToonFragment.hlsl: that file
// expands light-mode macros (UNITY_LIGHT_ATTENUATION etc.) which do not
// exist in a ShadowCaster pass.

#include "../Core/NataneToonBuildSettings.hlsl"
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"
#include "../Core/NataneToonInput.hlsl"
#include "../Utils/NataneToonUtils.hlsl"
#include "../Core/NataneToonVertex.hlsl"

#ifdef CUTOUT_VARIANT
float _Cutoff;
#endif

struct v2f_shadow
{
    V2F_SHADOW_CASTER;
    float2 uv : TEXCOORD1;
    float3 worldPos : TEXCOORD2;
    #if defined(_DITHERING_ALPHA) || defined(_HASHED_ALPHA)
        float4 screenPos : TEXCOORD3;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f_shadow vertShadow(appdata v)
{
    v2f_shadow o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f_shadow, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);

    // Same vertex deformation as the forward passes (NataneToonVertex.hlsl)
    #ifdef _VAT
        ApplyVAT(v.vertex, v.normal, v.uv);
    #endif

    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

    #if defined(_DITHERING_ALPHA) || defined(_HASHED_ALPHA)
        o.screenPos = ComputeScreenPos(o.pos);
    #endif
    return o;
}

float4 fragShadow(v2f_shadow i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float2 uv = i.uv;
    float2 mainUV = uv;
    #ifdef _MAIN_TEX_ANIMATION
        if (_MainTexAnimation >= 0.5)
            mainUV = AnimateUV(uv, _MainTexScrollSpeed.xy, _MainTexRotateSpeed);
    #endif

    // Same alpha source as the forward pass: col.a = mainTex.a * _Color.a
    half alpha = tex2D(_MainTex, mainUV).a * _Color.a;

    #ifdef _ALPHA_MASK
        if (_UseAlphaMask >= 0.5)
            alpha *= NATANE_SAMPLE_SHARED_R(_AlphaMask, _MainTex, uv);
    #endif

    // ===== Dissolve (mirrors NataneToonFragment.hlsl) =====
    #ifdef _DISSOLVE
        if (_Dissolve >= 0.5 && _DissolveAmount > 0.0)
        {
            half dissolveMaskValue = NATANE_SAMPLE_SHARED_R(_DissolveMask, _MainTex, uv);
            float2 dissolveUV = AnimateUVIfNeeded(uv, _DissolveTexScrollSpeed.xy, _DissolveTexRotateSpeed);

            float dissolveNoise;
            if (_DissolveCoordMode > 0.5)
            {
                float3 dissolveCoordPos = _DissolveCoordMode < 1.5
                    ? i.worldPos
                    : mul(unity_WorldToObject, float4(i.worldPos, 1.0)).xyz;
                float axisVal = _DissolveWorldAxis < 0.5 ? dissolveCoordPos.x
                    : (_DissolveWorldAxis < 1.5 ? dissolveCoordPos.y : dissolveCoordPos.z);
                float posNoise = saturate((axisVal - _DissolveWorldMin) / max(_DissolveWorldMax - _DissolveWorldMin, 0.01));
                float texNoise = NATANE_SAMPLE_REPEAT(_DissolveTex, dissolveUV).r;
                dissolveNoise = lerp(posNoise, posNoise * texNoise, _DissolveNoiseBlend);
            }
            else
            {
                dissolveNoise = NATANE_SAMPLE_REPEAT(_DissolveTex, dissolveUV).r;
            }

            float2 dissolveResult = CalculateDissolveFromNoise(dissolveNoise, _DissolveAmount, _DissolveEdgeWidth + _DissolveBlur * 0.15);
            clip(dissolveResult.x + (1.0 - dissolveMaskValue));
        }
    #endif

    // ===== Hashed / Dithered alpha =====
    #if defined(_HASHED_ALPHA)
        if (_HashedAlpha >= 0.5)
        {
            float2 hashedScreenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
            float2 hashedScreenPos = hashedScreenUV * _ScreenParams.xy;
            clip(ApplyHashedAlpha(alpha, StabilizeDitherCoord(hashedScreenPos), i.worldPos.xz, _HashedAlphaScale));
        }
    #elif defined(_DITHERING_ALPHA)
        if (_DitheringAlpha >= 0.5)
        {
            float2 ditherScreenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
            float2 ditherScreenPos = ditherScreenUV * _ScreenParams.xy;
            clip(ApplyDitheringAlpha(alpha, StabilizeDitherCoord(ditherScreenPos), max(_DitheringAlphaScale, 1.0)));
        }
    #endif

    #ifdef CUTOUT_VARIANT
        clip(alpha - _Cutoff);
    #endif

    SHADOW_CASTER_FRAGMENT(i)
}

#endif // NATANE_TOON_SHADOW_CASTER_INCLUDED
