// NataneToonFurFin.hlsl - Fin-based fur (geometry shader)
// Called from NataneToonShader_Fur.shader / _Fur_Lite.shader, FUR_FIN pass.
//
// A fin is a card standing on a triangle edge, along the surface normal. Where shells
// stack slices parallel to the surface and therefore thin out exactly where you look
// along them (the silhouette), fins do the opposite: they are widest at the silhouette
// and disappear where you look straight down at the surface. The two are complementary,
// which is why "Shell and Fin" exists as a mode.
//
// The strand pattern comes from NataneToonFurCommon.hlsl, the same field the shells
// sample, so a fin is literally a vertical slice through the shell volume — the strands
// line up instead of reading as two unrelated coats.

#ifndef NATANE_TOON_FUR_FIN_INCLUDED
#define NATANE_TOON_FUR_FIN_INCLUDED

#include "NataneToonFurCommon.hlsl"

// 3 edges x (joints + 1) rings x 2 vertices, with _FurFinJoints capped at 4.
#define NATANE_FUR_FIN_MAX_JOINTS 4
#define NATANE_FUR_FIN_MAX_VERTS 30

struct appdata_fin {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Object space all the way to the geometry stage: the strand offset (gravity, wind) is
// defined in object space, so building the fin there keeps shells and fins identical.
struct v2g_fin {
    float4 vertex : SV_POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct g2f_fin {
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float3 worldPos : TEXCOORD2;
    float3 finNormal : TEXCOORD3;
    float2 heightAlpha : TEXCOORD4;   // x = height along the strand (0..1), y = fin fade
    UNITY_FOG_COORDS(5)
    float2 uv1 : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

v2g_fin finVert(appdata_fin v)
{
    v2g_fin o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);

    o.vertex = v.vertex;
    o.normal = v.normal;
    o.uv = v.uv;
    o.uv1 = v.uv1;
    return o;
}

[maxvertexcount(NATANE_FUR_FIN_MAX_VERTS)]
void finGeom(triangle v2g_fin input[3], inout TriangleStream<g2f_fin> stream)
{
    UNITY_SETUP_INSTANCE_ID(input[0]);

    // Shell-only mode. The pass cannot be switched off from a material property, so it
    // runs and emits nothing — one draw call, no geometry.
    if (!NataneFurFinsEnabled()) return;

    float3 posOS[3], normalOS[3], posWS[3];
    [unroll] for (int v = 0; v < 3; v++)
    {
        posOS[v] = input[v].vertex.xyz;
        normalOS[v] = input[v].normal;
        posWS[v] = mul(unity_ObjectToWorld, float4(posOS[v], 1.0)).xyz;
    }

    float3 faceCenter = (posWS[0] + posWS[1] + posWS[2]) / 3.0;
    float3 viewDir = normalize(_WorldSpaceCameraPos - faceCenter);
    float3 faceNormal = normalize(cross(posWS[1] - posWS[0], posWS[2] - posWS[0]));

    // Looking straight down at a face, a fin is a visible sheet of cardboard and the
    // shells already cover that area properly. Skip those triangles outright, and fade
    // the ones just inside the threshold so fins do not pop in as the camera turns.
    // The fade window is a fixed slice below the threshold, not a fraction of it:
    // scaling it with the threshold would, at a threshold of 1, fade out most of the
    // surface instead of just the triangles about to be culled.
    float faceView = abs(dot(faceNormal, viewDir));
    if (faceView > _FurFinViewThreshold) return;
    float viewFade = saturate((_FurFinViewThreshold - faceView) / 0.1);

    int joints = (int)clamp(_FurFinJoints, 1, NATANE_FUR_FIN_MAX_JOINTS);

    g2f_fin o;
    UNITY_INITIALIZE_OUTPUT(g2f_fin, o);
    UNITY_TRANSFER_INSTANCE_ID(input[0], o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    [unroll] for (int e = 0; e < 3; e++)
    {
        int a = e;
        int b = (e + 1) % 3;

        float3 edgeDirOS = posOS[b] - posOS[a];
        if (dot(edgeDirOS, edgeDirOS) < 1e-12) continue;
        edgeDirOS = normalize(edgeDirOS);

        // Direction the fin grows in. Averaging the two vertex normals keeps neighbouring
        // fins continuous across a shared edge.
        float3 growOS = normalize(normalOS[a] + normalOS[b]);

        // Per-fin direction jitter, so the coat does not look combed into a grid.
        if (_FurFinRandomDir > 0.001)
        {
            float2 seed = NataneFurCellUV((input[a].uv + input[b].uv) * 0.5);
            float2 r = _furHash2(floor(seed)) * 2.0 - 1.0;
            float3 sideOS = normalize(cross(growOS, edgeDirOS));
            growOS = normalize(growOS + (edgeDirOS * r.x + sideOS * r.y) * _FurFinRandomDir);
        }

        // A fin is invisible edge-on, which is exactly where the shells take over.
        float3 growWS = UnityObjectToWorldNormal(growOS);
        float3 edgeDirWS = normalize(posWS[b] - posWS[a]);
        float3 finNormalWS = cross(edgeDirWS, growWS);
        if (dot(finNormalWS, finNormalWS) < 1e-12) continue;
        finNormalWS = normalize(finNormalWS);

        float finAlpha = abs(dot(finNormalWS, viewDir)) * viewFade;
        if (finAlpha < 0.02) continue;

        float maskA = tex2Dlod(_FurMask, float4(input[a].uv, 0, 0)).r;
        float maskB = tex2Dlod(_FurMask, float4(input[b].uv, 0, 0)).r;

        o.finNormal = finNormalWS;
        o.heightAlpha.y = finAlpha;

        // Ring by ring from the root to the tip. Sampling the shared strand offset at
        // several heights is what bends the fin: gravity is quadratic in height and wind
        // is linear, so a single quad would be dead straight.
        [loop] for (int j = 0; j <= joints; j++)
        {
            float t = (float)j / (float)joints;
            o.heightAlpha.x = t;

            float3 offsetA = NataneFurStrandOffset(growOS, posOS[a], t) * maskA;
            float3 offsetB = NataneFurStrandOffset(growOS, posOS[b], t) * maskB;

            float4 worldA = mul(unity_ObjectToWorld, float4(posOS[a] + offsetA, 1.0));
            o.worldPos = worldA.xyz;
            o.worldNormal = UnityObjectToWorldNormal(normalOS[a]);
            o.uv = input[a].uv;
            o.uv1 = input[a].uv1;
            o.pos = UnityWorldToClipPos(worldA);
            UNITY_TRANSFER_FOG(o, o.pos);
            stream.Append(o);

            float4 worldB = mul(unity_ObjectToWorld, float4(posOS[b] + offsetB, 1.0));
            o.worldPos = worldB.xyz;
            o.worldNormal = UnityObjectToWorldNormal(normalOS[b]);
            o.uv = input[b].uv;
            o.uv1 = input[b].uv1;
            o.pos = UnityWorldToClipPos(worldB);
            UNITY_TRANSFER_FOG(o, o.pos);
            stream.Append(o);
        }

        stream.RestartStrip();
    }
}

fixed4 finFrag(g2f_fin i, float face : VFACE) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float height = i.heightAlpha.x;

    float field;
    float alpha = NataneFurStrandAlpha(i.uv, height, field) * i.heightAlpha.y;
    clip(alpha - 0.004);

    // Fins are drawn with Cull Off, so the card's own normal has to follow the side we
    // are actually looking at; the mesh normal underneath it does not flip.
    float3 finNormal = i.finNormal * ((face >= 0.0) ? 1.0 : -1.0);
    float3 normal = normalize(lerp(i.worldNormal, finNormal, saturate(_FurFinNormalBlend)));

    float3 finalColor = NataneFurShade(i.uv, i.uv1, height, field, i.worldPos, normal);

    fixed4 col = fixed4(finalColor, alpha);
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}

#endif // NATANE_TOON_FUR_FIN_INCLUDED
