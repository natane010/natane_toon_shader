#ifndef NATANE_TOON_URP_OUTLINE_PASS_INCLUDED
#define NATANE_TOON_URP_OUTLINE_PASS_INCLUDED

// ===== NataneToon URP Outline Pass =====
// BiRP 版アウトラインパス (NataneToonShader.shader OUTLINE Pass) のコア移植。
// LightMode "SRPDefaultUnlit" で Renderer Feature なしに描画される。
// 対応機能: 反転ハル / バックフェイス方式、距離補正、スムーズノーマル
// (頂点カラーOS / 頂点カラーTS / ベイク法線テクスチャ)、コーナースムーズ、
// エッジ幅補正、マスク、幅マップ、テクスチャ連動カラー、マルチカラー。

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Inline HSV functions (BiRP アウトラインパスと同一のアルゴリズム)
#if defined(_OUTLINE_TEXTURE_COLOR)
float3 NataneOutlineRGBtoHSV(float3 rgb)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(rgb.bg, K.wz), float4(rgb.gb, K.xy), step(rgb.b, rgb.g));
    float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 NataneOutlineHSVtoRGB(float3 hsv)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
    return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
}
#endif

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float4 color      : COLOR;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    half   fogFactor  : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings NataneToonOutlineVertex(OutlineAttributes input)
{
    OutlineVaryings output = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.uv = input.texcoord;

#if defined(_OUTLINE)
    // Distance compensation for consistent outline width
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float distanceToCamera = distance(positionWS, _WorldSpaceCameraPos);
    float distanceFactor = min(distanceToCamera * 0.1, _OutlineDistCompMax);

    // Get outline width from map / mask if enabled
    float widthMultiplier = 1.0;
    #if defined(_OUTLINE_WIDTH_MAP)
        widthMultiplier = SAMPLE_TEXTURE2D_LOD(_OutlineWidthMap, sampler_MainTex, input.texcoord, 0).r;
    #endif
    #if defined(_OUTLINE_MASK)
        widthMultiplier *= SAMPLE_TEXTURE2D_LOD(_OutlineMask, sampler_MainTex, TRANSFORM_TEX(input.texcoord, _OutlineMask), 0).r;
    #endif

    // Resolve outline normal (smooth normal or original)
    float3 outlineNormal = input.normalOS;
    #if defined(_SMOOTH_NORMAL)
        if (_SmoothNormalMode < 0.5)
        {
            // Mode 0: Vertex Color Object Space
            outlineNormal = input.color.rgb * 2.0 - 1.0;
        }
        else if (_SmoothNormalMode < 1.5)
        {
            // Mode 1: Vertex Color Tangent Space (lilToon compatible)
            float3 smoothTS = input.color.rgb * 2.0 - 1.0;
            float3 binormal = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
            float3x3 tbnOS = float3x3(input.tangentOS.xyz, binormal, input.normalOS);
            outlineNormal = mul(smoothTS, tbnOS);
        }
        else
        {
            // Mode 2: Baked Normal Texture
            float3 bakedNormal = SAMPLE_TEXTURE2D_LOD(_SmoothNormalTex, sampler_MainTex, input.texcoord, 0).rgb * 2.0 - 1.0;
            float3 binormal = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
            float3x3 tbnOS = float3x3(input.tangentOS.xyz, binormal, input.normalOS);
            outlineNormal = mul(bakedNormal, tbnOS);
        }
        outlineNormal = normalize(outlineNormal);
    #else
        // Fallback: blend vertex normal toward vertex position direction
        if (_OutlineCornerSmooth > 0.001)
        {
            float3 posNormal = normalize(input.positionOS.xyz);
            outlineNormal = normalize(lerp(input.normalOS, posNormal, _OutlineCornerSmooth));
        }
    #endif

    if (_OutlineMode < 0.5)
    {
        // Mode 0: Inverted Hull - Extrusion along normals in view space
        float3 normalVS = normalize(TransformWorldToViewDir(TransformObjectToWorldNormal(outlineNormal)));
        float2 offset = mul((float2x2)UNITY_MATRIX_P, normalVS.xy);

        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

        // Apply distance compensation for consistent outline width
        float outlineWidth = _OutlineWidth * 0.1 * (1.0 + distanceFactor) * widthMultiplier;

        // Edge width compensation
        if (_OutlineEdgeCompensation > 0.001)
        {
            float normalConsistency = saturate(dot(normalize(input.normalOS), outlineNormal));
            float edgeComp = lerp(1.0, lerp(0.3, 1.0, normalConsistency), _OutlineEdgeCompensation);
            outlineWidth *= edgeComp;
        }

        output.positionCS.xy += offset * output.positionCS.z * outlineWidth;
    }
    else
    {
        // Mode 1: Back Face - Scale up vertices along normals in object space
        float outlineWidth = _OutlineWidth * 0.1 * (1.0 + distanceFactor * 0.5) * widthMultiplier;

        // Edge width compensation
        if (_OutlineEdgeCompensation > 0.001)
        {
            float normalConsistency = saturate(dot(normalize(input.normalOS), outlineNormal));
            float edgeComp = lerp(1.0, lerp(0.3, 1.0, normalConsistency), _OutlineEdgeCompensation);
            outlineWidth *= edgeComp;
        }

        float3 scaledPos = input.positionOS.xyz + normalize(outlineNormal) * outlineWidth;
        output.positionCS = TransformObjectToHClip(scaledPos);
    }

    output.fogFactor = ComputeFogFactor(output.positionCS.z);
#else
    output.positionCS = float4(0.0, 0.0, 0.0, 0.0);
#endif

    return output;
}

half4 NataneToonOutlineFragment(OutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_OUTLINE)
    half4 col = _OutlineColor;

    // Apply texture-linked outline color
    #if defined(_OUTLINE_TEXTURE_COLOR)
    {
        half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
        half3 darkenedTexColor = texColor.rgb * (1.0 - _OutlineTexColorDarken);
        float3 outHSV = NataneOutlineRGBtoHSV(darkenedTexColor);
        outHSV.x = frac(outHSV.x + _OutlineTexColorHueShift);
        outHSV.y = saturate(outHSV.y * _OutlineTexColorSaturation);
        darkenedTexColor = half3(NataneOutlineHSVtoRGB(outHSV));
        col.rgb = lerp(col.rgb, darkenedTexColor, _OutlineTexColorBlend);
    }
    #endif

    // Apply multi-color outline
    #if defined(_OUTLINE_MULTI_COLOR)
    {
        float mixFactor = frac(input.uv.y * 5.0 + _Time.y * 0.5); // Animated gradient
        col.rgb = lerp(_OutlineColor.rgb, _OutlineColor2.rgb, mixFactor * _OutlineColorMix);
    }
    #endif

    // Apply outline mask
    #if defined(_OUTLINE_MASK)
    {
        half outlineMask = SAMPLE_TEXTURE2D(_OutlineMask, sampler_MainTex, TRANSFORM_TEX(input.uv, _OutlineMask)).r;
        clip(outlineMask - 0.01);
        col.a *= outlineMask;
    }
    #endif

    col.rgb = MixFog(col.rgb, input.fogFactor);
    return col;
#else
    discard;
    return half4(0.0, 0.0, 0.0, 0.0);
#endif
}

#endif // NATANE_TOON_URP_OUTLINE_PASS_INCLUDED
