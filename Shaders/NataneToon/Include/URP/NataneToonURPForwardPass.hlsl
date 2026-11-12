#ifndef NATANE_TOON_URP_FORWARD_PASS_INCLUDED
#define NATANE_TOON_URP_FORWARD_PASS_INCLUDED

// ===== NataneToon URP Forward Pass =====
// トゥーンシェーディングのコア機能を URP に移植したパス。SubShader が 2 つこのファイルを使う。
//
//   NATANE_URP_UNITY6 定義あり : Unity 6.1 (URP 17.1) 以降用の SubShader。
//       Forward / Forward+ / Deferred / Deferred+ (LightMode = UniversalForwardOnly)、
//       Adaptive Probe Volumes、ライトレイヤー、ライトクッキー、シャドウマスク、
//       Rendering Layers の書き出し、LOD クロスフェード、ピクセル単位のフォグ。
//   NATANE_URP_UNITY6 定義なし : URP 14 〜 17.0 用の互換 SubShader (従来の挙動のまま)。
//
// パス側で定義するスイッチ:
//   _ALPHATEST_ON 1          Cutout バリアント (アルファでクリップ)
//   NATANE_URP_TRANSPARENT   Transparent バリアント (アルファを出力)

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// Forward+ のクラスターライトループは Unity 6.1 で _FORWARD_PLUS から _CLUSTER_LIGHT_LOOP に改名された。
// どちらの URP でも同じコードで回せるように名前を吸収する。
#if defined(USE_CLUSTER_LIGHT_LOOP)
    #define NATANE_URP_CLUSTER_LOOP USE_CLUSTER_LIGHT_LOOP
    #define NATANE_URP_SUBTRACTIVE_LIGHT_CHECK CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
#elif defined(USE_FORWARD_PLUS)
    #define NATANE_URP_CLUSTER_LOOP USE_FORWARD_PLUS
    #define NATANE_URP_SUBTRACTIVE_LIGHT_CHECK FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
#else
    #define NATANE_URP_CLUSTER_LOOP 0
    #define NATANE_URP_SUBTRACTIVE_LIGHT_CHECK
#endif

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3  normalWS   : TEXCOORD2;
#if defined(_NORMALMAP)
    half4  tangentWS  : TEXCOORD3;    // xyz: tangent, w: sign
#endif
    half   fogFactor  : TEXCOORD4;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD5;
#endif
#if defined(NATANE_URP_UNITY6)
    half3  vertexSH   : TEXCOORD6;
    #ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD7;
    #endif
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings NataneToonPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = vertexInput.positionCS;
    output.positionWS = vertexInput.positionWS;
    output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
    output.normalWS = normalInput.normalWS;

#if defined(_NORMALMAP)
    real sgn = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sgn);
#endif

    output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

#if defined(NATANE_URP_UNITY6)
    // SH の評価位置 (頂点 / ピクセル / 混合) は URP の設定 (EVALUATE_SH_*) に従う。
    // APV 使用時はここでプローブオクルージョンも受け取る。
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
#endif

    return output;
}

// 追加ライト 1 灯ぶんのトゥーン寄与。
half3 NataneToonAdditionalLightURP(Light light, half3 normalWS, float aaFloor)
{
    half addNdotl = saturate(dot(normalWS, light.direction));
    half addToon = NataneToonStepURP(addNdotl, _ShadowSteps, _ShadowSharpness, aaFloor);
    return light.color * (addToon * light.distanceAttenuation * light.shadowAttenuation);
}

#if defined(NATANE_URP_UNITY6)
void NataneToonPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
#else
half4 NataneToonPassFragment(Varyings input) : SV_Target
#endif
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 uv = input.uv;
    half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;

#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    // ===== Normal =====
    half3 normalWS = normalize(input.normalWS);
#if defined(_NORMALMAP)
    {
        half4 packedNormal = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv);
        half3 normalTS = UnpackNormalScale(packedNormal, _BumpScale);
        half3 tangentWS = normalize(input.tangentWS.xyz);
        half3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
        normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(tangentWS, bitangentWS, normalWS)));
    }
#endif

    float3 positionWS = input.positionWS;

    // ===== Main Light + Shadows =====
    float4 shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    shadowCoord = TransformWorldToShadowCoord(positionWS);
#endif

    // LIGHT_LOOP_BEGIN (Forward+) は inputData.normalizedScreenSpaceUV / positionWS を参照する。
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

#if defined(NATANE_URP_UNITY6)
    inputData.positionCS = input.positionCS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.shadowCoord = shadowCoord;
    inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), input.fogFactor);
    inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

    // ===== Ambient: Adaptive Probe Volumes または SH =====
    #if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
        inputData.bakedGI = SAMPLE_GI(input.vertexSH,
            GetAbsolutePositionWS(positionWS),
            normalWS,
            inputData.viewDirectionWS,
            input.positionCS.xy,
            input.probeOcclusion,
            inputData.shadowMask);
    #else
        inputData.bakedGI = SampleSHPixel(input.vertexSH, normalWS);
        inputData.shadowMask = SAMPLE_SHADOWMASK(float2(0.0, 0.0));
    #endif

    // SSAO はあえて受けない (顔や肌のトゥーン面が汚れるため)。係数 1 で素通しにする。
    AmbientOcclusionFactor aoFactor;
    aoFactor.indirectAmbientOcclusion = 1.0;
    aoFactor.directAmbientOcclusion = 1.0;

    half4 shadowMask = CalculateShadowMask(inputData);
    // クッキーとシャドウマスクはこのオーバーロードの中で適用される。
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);

    #ifdef _LIGHT_LAYERS
        uint meshRenderingLayers = GetMeshRenderingLayer();
        if (!IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
        {
            mainLight.color = half3(0.0, 0.0, 0.0);
        }
    #endif
#else
    Light mainLight = GetMainLight(shadowCoord);
#endif

    half atten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
    half ndotl = dot(normalWS, mainLight.direction);

    // Lit Area Softness (BiRP: _LitSoftness smoothstep)
    half lightTerm = lerp(ndotl, smoothstep(0.0, 1.0, ndotl), _LitSoftness);

    // 段の境界の AA 幅。追加ライトのループ内では fwidth を呼べないので、ここで求めた値を使い回す。
    float aaFloor = NataneToonStepAAFloorURP(lightTerm, _ShadowSteps);

    // ===== Toon / Ramp Shading =====
    half3 lighting;
#if defined(_USE_RAMP)
    half rampInput = lightTerm * atten;
    half2 rampUV = half2(saturate(rampInput + clamp(_ShadowOffset, -1.0, 1.0)), 0.5);
    lighting = SAMPLE_TEXTURE2D(_RampTex, sampler_linear_clamp, rampUV).rgb;
#else
    half shadingValue = NataneToonStepURP(lightTerm, _ShadowSteps, _ShadowSharpness, aaFloor);
    shadingValue *= atten;
    lighting = lerp(_ShadowColor.rgb, half3(1.0, 1.0, 1.0), shadingValue);
#endif

    half3 lightAccum = lighting * mainLight.color;

    // ===== Additional Lights =====
#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    {
        uint pixelLightCount = GetAdditionalLightsCount();

        // Forward+ / Deferred+ では、追加のディレクショナルライトはクラスターに入らず別枠で回す。
        #if NATANE_URP_CLUSTER_LOOP
        [loop] for (uint dirLightIndex = 0; dirLightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirLightIndex++)
        {
            uint lightIndex = dirLightIndex;
            NATANE_URP_SUBTRACTIVE_LIGHT_CHECK

            #if defined(NATANE_URP_UNITY6)
                Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                #endif
            #else
                Light light = GetAdditionalLight(lightIndex, positionWS, half4(1.0, 1.0, 1.0, 1.0));
            #endif
            {
                lightAccum += NataneToonAdditionalLightURP(light, normalWS, aaFloor);
            }
        }
        #endif

        LIGHT_LOOP_BEGIN(pixelLightCount)
            #if defined(NATANE_URP_UNITY6)
                Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                #endif
            #else
                Light light = GetAdditionalLight(lightIndex, positionWS, half4(1.0, 1.0, 1.0, 1.0));
            #endif
            {
                lightAccum += NataneToonAdditionalLightURP(light, normalWS, aaFloor);
            }
        LIGHT_LOOP_END
    }
#endif

    // ===== Ambient =====
#if defined(NATANE_URP_UNITY6)
    lightAccum += inputData.bakedGI;
#else
    lightAccum += SampleSH(normalWS);
#endif

    half3 color = albedo.rgb * lightAccum;

    // ===== MatCap =====
#if defined(_MATCAP)
    {
        half3 normalVS = normalize(TransformWorldToViewDir(normalWS));
        float2 matcapUV = normalVS.xy * 0.5 + 0.5;
        half3 matcap = SAMPLE_TEXTURE2D(_MatCapTex, sampler_linear_clamp, matcapUV).rgb * _MatCapIntensity;
        half3 matcapResult;
        if (_MatCapBlendMode < 0.5)
        {
            matcapResult = color + matcap;          // Add
        }
        else if (_MatCapBlendMode < 1.5)
        {
            matcapResult = color * matcap;          // Multiply
        }
        else
        {
            matcapResult = matcap;                  // Replace
        }
        color = lerp(color, matcapResult, _MatCapBlend);
    }
#endif

    // ===== Rim Light =====
#if defined(_RIM_LIGHT)
    {
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
        half rimDot = saturate(1.0 - saturate(dot(normalWS, viewDirWS)));
        half rim = pow(rimDot, _RimPower) * _RimIntensity;
        color += _RimColor.rgb * rim;
    }
#endif

    // ===== Emission =====
#if defined(_EMISSION)
    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_MainTex, uv).rgb * _EmissionColor.rgb;
#endif

    // ===== Fog =====
#if defined(NATANE_URP_UNITY6)
    color = MixFog(color, inputData.fogCoord);
#else
    color = MixFog(color, input.fogFactor);
#endif

#if defined(NATANE_URP_TRANSPARENT)
    half outAlpha = albedo.a;
#else
    // 不透明 / カットアウトはアルファ 1 を書く (カメラのアルファ出力や合成で透けないように)。
    half outAlpha = 1.0;
#endif

#if defined(NATANE_URP_UNITY6)
    outColor = half4(color, outAlpha);
    #ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
#else
    return half4(color, outAlpha);
#endif
}

#endif // NATANE_TOON_URP_FORWARD_PASS_INCLUDED
