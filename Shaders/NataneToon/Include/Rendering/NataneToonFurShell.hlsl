// NataneToonFurShell.hlsl - Shell-based fur vertex/fragment shader
// Called from NataneToonShader_Fur.shader with FUR_SHELL_INDEX defined per pass
//
// The strand field and the shading live in NataneToonFurCommon.hlsl, shared with the
// fin method. This file only turns "which shell am I" into a slice of that field.

#ifndef NATANE_TOON_FUR_SHELL_INCLUDED
#define NATANE_TOON_FUR_SHELL_INCLUDED

#include "NataneToonFurCommon.hlsl"

// Shell parameters
#define FUR_SHELL_COUNT 16
#define FUR_LAYER ((float)FUR_SHELL_INDEX / (float)(FUR_SHELL_COUNT - 1))

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

v2f_fur furVert(appdata_fur v)
{
    v2f_fur o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float layer = FUR_LAYER;
    o.furLayer = layer;
    o.uv = v.uv;
    o.uv1 = v.uv1;

    float3 offset = NataneFurStrandOffset(v.normal, v.vertex.xyz, layer);

    // Apply mask - scale shell offset by mask value
    float mask = tex2Dlod(_FurMask, float4(v.uv, 0, 0)).r;
    offset *= mask;

    float4 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz + offset, 1.0));
    o.worldPos = worldPos.xyz;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.pos = UnityWorldToClipPos(worldPos);

    // Fin-only mode. The Built-in pipeline cannot switch a Pass off from a material
    // property, so the 16 shell passes still run: collapse the triangle to a point and
    // let the rasteriser drop it. The draw calls remain, the pixel cost does not.
    if (!NataneFurShellsEnabled()) o.pos = float4(0, 0, 0, 1);

    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

fixed4 furFrag(v2f_fur i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float layer = i.furLayer;

    // Distance-based LOD: fade out upper shells at distance
    float camDist = NataneViewerDistance(i.worldPos);   // same shell LOD in both eyes
    float lodFade = saturate(1.0 - max(camDist - _FurLODDistance, 0.0) / max(_FurLODDistance * 0.5, 0.001));
    // Upper layers get clipped first at distance
    float lodThreshold = lerp(_FurLODMinLayers / (float)FUR_SHELL_COUNT, 1.0, lodFade);
    clip(lodThreshold - layer - 0.001);

    float field;
    float alpha = NataneFurStrandAlpha(i.uv, layer, field);
    clip(alpha - 0.004);

    float3 finalColor = NataneFurShade(i.uv, i.uv1, layer, field, i.worldPos, i.worldNormal);

    fixed4 col = fixed4(finalColor, alpha);
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}

#endif // NATANE_TOON_FUR_SHELL_INCLUDED
