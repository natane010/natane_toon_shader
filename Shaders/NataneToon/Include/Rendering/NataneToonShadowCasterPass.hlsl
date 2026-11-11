// NataneToonShadowCasterPass.hlsl
// Shared SHADOW_CASTER pass implementation for all NataneToon shader variants.
// Cutout variants must "#define NATANE_SHADOWCASTER_CUTOUT" before including
// this file to enable alpha-tested shadows.
#ifndef NATANE_TOON_SHADOWCASTER_PASS_INCLUDED
#define NATANE_TOON_SHADOWCASTER_PASS_INCLUDED

#include "UnityCG.cginc"

#ifdef NATANE_SHADOWCASTER_CUTOUT
sampler2D _MainTex;
float4 _MainTex_ST;
float _Cutoff;
#endif

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
#ifdef NATANE_SHADOWCASTER_CUTOUT
    float2 uv : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    V2F_SHADOW_CASTER;
#ifdef NATANE_SHADOWCASTER_CUTOUT
    float2 uv : TEXCOORD1;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert(appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
#ifdef NATANE_SHADOWCASTER_CUTOUT
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
#endif
    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
    return o;
}

float4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#ifdef NATANE_SHADOWCASTER_CUTOUT
    fixed4 texcol = tex2D(_MainTex, i.uv);
    clip(texcol.a - _Cutoff);
#endif
    SHADOW_CASTER_FRAGMENT(i)
}

#endif // NATANE_TOON_SHADOWCASTER_PASS_INCLUDED
