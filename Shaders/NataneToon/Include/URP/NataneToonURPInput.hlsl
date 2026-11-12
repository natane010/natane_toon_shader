#ifndef NATANE_TOON_URP_INPUT_INCLUDED
#define NATANE_TOON_URP_INPUT_INCLUDED

// URP 用の共有マテリアル入力。全パスがこのファイルを include する。

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

// SRP Batcher は全パスで同じ UnityPerMaterial レイアウトを要求する。ifdef で囲まないこと。
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
float _Cutoff;
CBUFFER_END

TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);
TEXTURE2D(_RampTex);
TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_MatCapTex);
TEXTURE2D(_SmoothNormalTex);
TEXTURE2D(_OutlineMask);
TEXTURE2D(_OutlineWidthMap);

// ramp / matcap 用。
SAMPLER(sampler_linear_clamp);
// 補助テクスチャ (輪郭線マスク / 幅マップ / ベイク法線) 用。sampler_MainTex を流用すると、
// _MainTex を読まないバリアントでサンプラーが最適化で消えてエラーになる (Unity 6)。
SAMPLER(sampler_linear_repeat);

// DOTS Instancing (GPU Resident Drawer)。
// バリアントごとにオフセットが変わると CPU 側が壊れるので、ここも ifdef で囲まないこと。
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _Color)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _OutlineColor)
    UNITY_DOTS_INSTANCED_PROP(float , _OutlineWidth)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// 参照のたびにロードコードが生成されないよう static にキャッシュする (URP Lit と同じ)。
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

// URP 同梱のパス (ObjectMotionVectors.hlsl など) は Lit の命名 (_BaseMap / _BaseColor) 前提。
// プロパティ名は BiRP と共通のままにしたいので、名前だけ読み替える。
#define _BaseMap        _MainTex
#define sampler_BaseMap sampler_MainTex
#define _BaseMap_ST     _MainTex_ST
#define _BaseColor      _Color

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
    return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
}

// Cutout はパス側で _ALPHATEST_ON を #define する (キーワードではないのでバリアントは増えない)。
half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    half alpha = albedoAlpha * color.a;
#if defined(_ALPHATEST_ON)
    clip(alpha - cutoff);
#endif
    return alpha;
}

// メインテクスチャのアルファで Cutout をクリップする。
half NataneURPAlphaClip(float2 uv)
{
    return Alpha(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a, _Color, _Cutoff);
}

// BiRP 版 ToonShading のコア移植。aaFloor は段の境界にかける最小の AA 幅。
// fwidth はライトループ内で呼べない (勾配が未定義になる) ので、ループの外で求めて渡す。
float NataneToonStepURP(float ndotl, float steps, float sharpness, float aaFloor)
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

    float smoothRange = saturate(sharpness + _StepBorderSmooth) * 0.5;
    smoothRange = max(smoothRange, aaFloor);
    float smoothedStep = smoothstep(0.5 - smoothRange, 0.5 + smoothRange, stepPosition);

    return saturate((currentStep + smoothedStep) * stepValue);
}

float NataneToonStepAAFloorURP(float ndotl, float steps)
{
    return min(fwidth(saturate(ndotl + clamp(_ShadowOffset, -1.0, 1.0)) * steps) * 0.5, 0.25);
}

float NataneToonStepURP(float ndotl, float steps, float sharpness)
{
    return NataneToonStepURP(ndotl, steps, sharpness, NataneToonStepAAFloorURP(ndotl, steps));
}

#endif // NATANE_TOON_URP_INPUT_INCLUDED
