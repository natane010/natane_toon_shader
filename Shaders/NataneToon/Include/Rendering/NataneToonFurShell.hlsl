// NataneToonFurShell.hlsl - Shell-based fur vertex/fragment shader
// Called from NataneToonShader_Fur.shader with FUR_SHELL_INDEX defined per pass

#ifndef NATANE_TOON_FUR_SHELL_INCLUDED
#define NATANE_TOON_FUR_SHELL_INCLUDED

#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "Lighting.cginc"
#define NATANE_UTILS_STANDALONE
#include "../Utils/NataneToonUtils.hlsl"

// FurShell passes are standalone CG programs with only 3 samplers (_MainTex,
// _FurNoiseTex, _FurMask) — well within the DX11 16-sampler limit.
// They use standard sampler2D declarations from the .shader Pass block,
// so NOSAMPLER conversion is intentionally skipped here.

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

// Shell parameters
#define FUR_SHELL_COUNT 16
// Shell 0 sits one step above the base surface (which FORWARD_BASE already draws)
// and shell 15 reaches the full fur length: 1/16 .. 16/16.
#define FUR_LAYER ((float)(FUR_SHELL_INDEX + 1) / (float)FUR_SHELL_COUNT)

struct appdata_fur {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f_fur {
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float3 worldPos : TEXCOORD2;
    float furLayer : TEXCOORD3;
    UNITY_FOG_COORDS(4)
    float2 uv1 : TEXCOORD5;
    UNITY_VERTEX_OUTPUT_STEREO
};

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

// Procedural noise for fur strand pattern (used as fallback when no noise texture is set)
float _furHash(float2 p)
{
    p = frac(p * float2(443.8975, 397.2973));
    p += dot(p, p.yx + 19.19);
    return frac(p.x * p.y);
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

v2f_fur furVert(appdata_fur v)
{
    v2f_fur o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float layer = FUR_LAYER;
    o.furLayer = layer;
    o.uv = v.uv;
    o.uv1 = v.uv1;

    // Shell offset along normal (object space)
    float3 offset = v.normal * layer * _FurLength;

    // Gravity (quadratic falloff for natural droop).
    // World-space "down" converted to object space so fur droops toward the
    // ground even when the object is rotated or tilted.
    float3 gravityDirOS = mul((float3x3)unity_WorldToObject, float3(0.0, -1.0, 0.0));
    gravityDirOS = normalize(gravityDirOS + float3(0.0, 0.0, 0.0001));
    offset += gravityDirOS * (_FurGravity * layer * layer);

    // Wind animation (_FurWindDirection is a world-space direction)
    float windPhase = _Time.y * _FurWindSpeed;
    float3 windDirOS = mul((float3x3)unity_WorldToObject, _FurWindDirection.xyz);
    float3 windOffset = windDirOS * sin(windPhase + v.vertex.x * 2.0) * _FurWindStrength * layer;
    offset += windOffset;

    // Apply mask - scale shell offset by mask value
    float mask = tex2Dlod(_FurMask, float4(v.uv, 0, 0)).r;
    offset *= mask;

    float4 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz + offset, 1.0));
    o.worldPos = worldPos.xyz;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.pos = UnityWorldToClipPos(worldPos);

    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

fixed4 furFrag(v2f_fur i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float layer = i.furLayer;

    // Distance-based LOD: fade out upper shells at distance
    float camDist = distance(i.worldPos, _WorldSpaceCameraPos);
    float lodFade = saturate(1.0 - max(camDist - _FurLODDistance, 0.0) / max(_FurLODDistance * 0.5, 0.001));
    // Upper layers get clipped first at distance
    float lodThreshold = lerp(_FurLODMinLayers / (float)FUR_SHELL_COUNT, 1.0, lodFade);
    clip(lodThreshold + 0.001 - layer);

    // Noise-based alpha cutoff for fur strand pattern
    // Procedural noise: always generates strand pattern
    float2 cellUV = i.uv * _FurDensity;
    float strandNoise = _furValueNoise(cellUV);
    // Add fine detail at 2x frequency for more natural look
    strandNoise = strandNoise * 0.7 + _furValueNoise(cellUV * 2.13 + 7.77) * 0.3;

    // Texture mask: modulates procedural noise (white texture = no effect)
    float texMask = tex2D(_FurNoiseTex, TRANSFORM_TEX(i.uv, _FurNoiseTex)).r;
    float furNoise = strandNoise * texMask;

    // Height-based alpha: upper layers have less fur
    // Taper toward the tip, but keep one shell's worth of alpha on the top shell so
    // the outermost pass still contributes instead of being clipped entirely.
    float alpha = furNoise * saturate(1.0 + 1.0 / (float)FUR_SHELL_COUNT - layer);
    clip(alpha - _FurAlphaCutoff);

    // Base color from main texture
    float4 mainColor = tex2D(_MainTex, TRANSFORM_TEX(i.uv, _MainTex));

    // Root-to-tip color gradient
    float3 furRootColor = _FurRootColor.rgb;
    float3 furTipColor = _FurTipColor.rgb;
    float3 furColor = lerp(furRootColor, furTipColor, layer);

    // Blend fur color with main texture
    float3 finalColor = lerp(mainColor.rgb, furColor, _FurColorBlend);

    // Self-occlusion (AO): root is darker, tip is brighter
    float ao = lerp(1.0 - _FurAO, 1.0, layer);
    finalColor *= ao;

    // Simple toon lighting
    float3 worldNormal = normalize(i.worldNormal);
    float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
    float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
    float NdotL = dot(worldNormal, lightDir);
    float toonShading = smoothstep(-0.1, 0.3, NdotL);

    // Self-shadow: inner layers receive less light
    float selfShadow = lerp(1.0 - _FurShadowStrength, 1.0, layer * 0.7 + 0.3);
    toonShading *= selfShadow;

    // Apply lighting
    float3 directLight = _LightColor0.rgb * toonShading;
    float3 ambient = ShadeSH9(float4(worldNormal, 1.0));

    // Runtime branch: FurShell always compiles LV/LTCGI helpers (via NATANE_FORCE_*),
    // unlike the main shader which uses shader_feature keywords for compile-time stripping.
    if (_UseLightVolume > 0.5)
    {
        float3 L0, L1r, L1g, L1b;
        LightVolumeSH(i.worldPos, L0, L1r, L1g, L1b);

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
        NataneLTCGIContribution(i.worldPos, worldNormal, viewDir, 1.0, i.uv1, ltcgiDiffuse, ltcgiSpecular);

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

    fixed4 col = fixed4(finalColor, alpha);
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}

#endif // NATANE_TOON_FUR_SHELL_INCLUDED
