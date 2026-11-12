#ifndef NATANE_TOON_URP_DEPTH_PASSES_INCLUDED
#define NATANE_TOON_URP_DEPTH_PASSES_INCLUDED

// URP 用 ShadowCaster / DepthOnly / DepthNormalsOnly。
// URP 同梱のパスは Lit の入力 (_BaseMap) 前提で Cutout のアルファ抜きを通せないので、自前で持つ。
// NataneToonURPInput.hlsl を先に include すること。
// パス側のスイッチ: _ALPHATEST_ON (Cutout) / NATANE_URP_UNITY6 (Rendering Layers を書き出す)

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct NataneDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct NataneDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    half3  normalWS   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void NataneDepthAlphaAndLod(NataneDepthVaryings input)
{
#if defined(_ALPHATEST_ON)
    NataneURPAlphaClip(input.uv);
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
}

// ===== ShadowCaster =====

// URP がシャドウ描画時にセットする。
float3 _LightDirection;
float3 _LightPosition;

NataneDepthVaryings NataneShadowCasterVertex(NataneDepthAttributes input)
{
    NataneDepthVaryings output = (NataneDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

    // ニアクリップより手前の影をニア面に押し戻す (シャドウパンケーキング)。
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
    return output;
}

half4 NataneShadowCasterFragment(NataneDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    NataneDepthAlphaAndLod(input);
    return 0;
}

// ===== DepthOnly =====

NataneDepthVaryings NataneDepthOnlyVertex(NataneDepthAttributes input)
{
    NataneDepthVaryings output = (NataneDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
    return output;
}

half NataneDepthOnlyFragment(NataneDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    NataneDepthAlphaAndLod(input);
    return input.positionCS.z;
}

// ===== DepthNormalsOnly =====

NataneDepthVaryings NataneDepthNormalsVertex(NataneDepthAttributes input)
{
    NataneDepthVaryings output = (NataneDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

void NataneDepthNormalsFragment(
    NataneDepthVaryings input
    , out half4 outNormalWS : SV_Target0
#if defined(NATANE_URP_UNITY6) && defined(_WRITE_RENDERING_LAYERS)
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    NataneDepthAlphaAndLod(input);

    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);

#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
    outNormalWS = half4(packedNormalWS, 0.0);
#else
    outNormalWS = half4(normalWS, 0.0);
#endif

#if defined(NATANE_URP_UNITY6) && defined(_WRITE_RENDERING_LAYERS)
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif // NATANE_TOON_URP_DEPTH_PASSES_INCLUDED
