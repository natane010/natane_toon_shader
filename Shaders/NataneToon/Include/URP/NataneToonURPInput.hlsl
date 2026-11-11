#ifndef NATANE_TOON_URP_INPUT_INCLUDED
#define NATANE_TOON_URP_INPUT_INCLUDED

// ===== NataneToon URP Shared Input =====
// URP (Universal Render Pipeline) 用の共有マテリアル入力定義。
// SRP Batcher 互換のため、全パスでこのファイルを include して
// UnityPerMaterial CBUFFER のレイアウトを完全に一致させること。
//
// GPU Resident Drawer (Unity 6) / BatchRendererGroup 対応:
// DOTS_INSTANCING_ON 時は主要プロパティを DOTS instanced プロパティとして
// 読み出す (URP Lit.shader / LitInput.hlsl と同じパターン)。

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

// NOTE: SRP Batcher は全パスで同一の UnityPerMaterial レイアウトを要求する。
// ここに宣言するプロパティは ifdef で囲まないこと。
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _OutlineMask_ST;
half4 _Color;
half4 _ShadowColor;
half4 _RimColor;
half4 _EmissionColor;
half4 _OutlineColor;
half4 _OutlineColor2;
float _ShadowSteps;
float _ShadowSharpness;
float _StepBorderSmooth;
float _ShadowOffset;
float _ShadowBlend;
float _LitSoftness;
half _BumpScale;
half _RimPower;
half _RimIntensity;
half _MatCapIntensity;
half _MatCapBlendMode;
half _MatCapBlend;
float _OutlineWidth;
float _OutlineDistCompMax;
float _OutlineMode;
float _OutlineColorMix;
float _OutlineCornerSmooth;
float _OutlineEdgeCompensation;
float _OutlineTexColorBlend;
float _OutlineTexColorDarken;
float _OutlineTexColorHueShift;
float _OutlineTexColorSaturation;
float _SmoothNormalMode;
CBUFFER_END

TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);
TEXTURE2D(_RampTex);
TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_MatCapTex);
TEXTURE2D(_SmoothNormalTex);
TEXTURE2D(_OutlineMask);
TEXTURE2D(_OutlineWidthMap);

// Inline sampler (ramp / matcap 用クランプサンプラー)
SAMPLER(sampler_linear_clamp);

// ===== DOTS Instancing (GPU Resident Drawer / BatchRendererGroup) =====
// NOTE: DOTS instanced プロパティも ifdef で囲まないこと
// (バリアントごとに constant-buffer オフセットが変わると CPU 側が壊れる)。
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _Color)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _OutlineColor)
    UNITY_DOTS_INSTANCED_PROP(float , _OutlineWidth)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// URP Lit.shader と同じ static キャッシュパターン
// (プロパティ参照ごとのロードコード再生成を防ぐ)
static float4 unity_DOTS_Sampled_Color;
static float4 unity_DOTS_Sampled_EmissionColor;
static float4 unity_DOTS_Sampled_OutlineColor;
static float  unity_DOTS_Sampled_OutlineWidth;

void SetupDOTSNataneToonMaterialPropertyCaches()
{
    unity_DOTS_Sampled_Color         = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _Color);
    unity_DOTS_Sampled_EmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_OutlineColor  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OutlineColor);
    unity_DOTS_Sampled_OutlineWidth  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OutlineWidth);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSNataneToonMaterialPropertyCaches()

#define _Color          unity_DOTS_Sampled_Color
#define _EmissionColor  unity_DOTS_Sampled_EmissionColor
#define _OutlineColor   unity_DOTS_Sampled_OutlineColor
#define _OutlineWidth   unity_DOTS_Sampled_OutlineWidth

#endif // UNITY_DOTS_INSTANCING_ENABLED

// ===== Shared toon shading helper =====
// BiRP 版 ToonShading (Include/Lighting/NataneToonLighting.hlsl) のコア移植。
// fwidth ベースの AA フロアを追加してピクセル化を防止。
float NataneToonStepURP(float ndotl, float steps, float sharpness)
{
    ndotl = saturate(ndotl + clamp(_ShadowOffset, -1.0, 1.0));

    if (_ShadowBlend > 0.001)
    {
        sharpness = lerp(sharpness, sharpness * (1.0 + _ShadowBlend * 2.0), _ShadowBlend);
    }

    float stepValue = 1.0 / max(steps, 1.0);
    float scaled = ndotl * steps;
    float currentStep = floor(scaled);
    float stepPosition = frac(scaled);

    // BiRP と同じ smoothstep 幅 + fwidth ベースの最小 AA 幅
    float smoothRange = saturate(sharpness + _StepBorderSmooth) * 0.5;
    smoothRange = max(smoothRange, min(fwidth(scaled) * 0.5, 0.25));
    float smoothedStep = smoothstep(0.5 - smoothRange, 0.5 + smoothRange, stepPosition);

    return saturate((currentStep + smoothedStep) * stepValue);
}

#endif // NATANE_TOON_URP_INPUT_INCLUDED
