#ifndef NATANE_TOON_URP_FORWARD_PASS_INCLUDED
#define NATANE_TOON_URP_FORWARD_PASS_INCLUDED

// ===== NataneToon URP Forward Pass =====
// トゥーンシェーディングのコア機能を URP (Forward / Forward+) に移植したパス。
// - メインライト (リアルタイムシャドウ対応)
// - 追加ライト (Forward+ の LIGHT_LOOP 対応)
// - トゥーン段階シェーディング / ランプテクスチャ
// - ノーマルマップ / リムライト / MatCap / エミッション
// - アンビエント (SH)

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

    return output;
}

half4 NataneToonPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 uv = input.uv;
    half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;

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
    Light mainLight = GetMainLight(shadowCoord);
    half atten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

    half ndotl = dot(normalWS, mainLight.direction);

    // Lit Area Softness (BiRP: _LitSoftness smoothstep)
    half lightTerm = lerp(ndotl, smoothstep(0.0, 1.0, ndotl), _LitSoftness);

    // ===== Toon / Ramp Shading =====
    half3 lighting;
#if defined(_USE_RAMP)
    half rampInput = lightTerm * atten;
    half2 rampUV = half2(saturate(rampInput + clamp(_ShadowOffset, -1.0, 1.0)), 0.5);
    lighting = SAMPLE_TEXTURE2D(_RampTex, sampler_linear_clamp, rampUV).rgb;
#else
    half shadingValue = NataneToonStepURP(lightTerm, _ShadowSteps, _ShadowSharpness);
    shadingValue *= atten;
    lighting = lerp(_ShadowColor.rgb, half3(1.0, 1.0, 1.0), shadingValue);
#endif

    half3 lightAccum = lighting * mainLight.color;

    // ===== Additional Lights (Forward+ 対応) =====
#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    {
        // LIGHT_LOOP_BEGIN (Forward+) は inputData.normalizedScreenSpaceUV /
        // inputData.positionWS を参照するためローカル InputData を構築する
        InputData inputData = (InputData)0;
        inputData.positionWS = positionWS;
        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

        uint pixelLightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light light = GetAdditionalLight(lightIndex, positionWS, half4(1.0, 1.0, 1.0, 1.0));
            half addNdotl = saturate(dot(normalWS, light.direction));
            half addToon = NataneToonStepURP(addNdotl, _ShadowSteps, _ShadowSharpness);
            lightAccum += light.color * (addToon * light.distanceAttenuation * light.shadowAttenuation);
        LIGHT_LOOP_END
    }
#endif

    // ===== Ambient (SH) =====
    lightAccum += SampleSH(normalWS);

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
    color = MixFog(color, input.fogFactor);

    return half4(color, albedo.a);
}

#endif // NATANE_TOON_URP_FORWARD_PASS_INCLUDED
