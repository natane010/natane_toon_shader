#ifndef NATANE_TOON_TESSELLATION_INCLUDED
#define NATANE_TOON_TESSELLATION_INCLUDED

// ===================================================================
// Natane Toon Shader - Tessellation Module
// Phong Tessellation with displacement map support
// ===================================================================

// Only emit hull/domain code on SM4.6+ passes. Lite variants compile their
// forward passes at target 3.5 (Quest/GLES3) and must not see patch types.
#if defined(UNITY_CAN_COMPILE_TESSELLATION) && SHADER_TARGET >= 46

#ifdef _TESSELLATION

// ===== Tessellation Control Point =====
struct TessellationControlPoint
{
    float4 vertex : INTERNALTESSPOS;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 uv : TEXCOORD0;
    #if defined(_BACKGROUND_MODE) || defined(_DETAIL_MAP) || defined(_LTCGI)
        float2 uv1 : TEXCOORD1;
    #endif
    #if defined(_SMOOTH_NORMAL) || defined(_VERTEX_COLOR_SHADOW)
        float4 color : COLOR;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// ===== Tessellation Factors =====
struct TessellationFactors
{
    float edge[3] : SV_TessFactor;
    float inside  : SV_InsideTessFactor;
};

// ===== Vertex Shader (Pass-through for tessellation) =====
TessellationControlPoint tessVert(appdata v)
{
    TessellationControlPoint o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    o.vertex = v.vertex;
    o.normal = v.normal;
    o.tangent = v.tangent;
    o.uv = v.uv;
    #if defined(_BACKGROUND_MODE) || defined(_DETAIL_MAP) || defined(_LTCGI)
        o.uv1 = v.uv1;
    #endif
    #if defined(_SMOOTH_NORMAL) || defined(_VERTEX_COLOR_SHADOW)
        o.color = v.color;
    #endif
    return o;
}

// ===== Phong Tessellation Helpers =====
float3 ProjectOntoPlane(float3 pt, float3 planeOrigin, float3 planeNml)
{
    return pt - dot(pt - planeOrigin, planeNml) * planeNml;
}

float3 PhongSmoothing(float3 posOS, float3 p0, float3 p1, float3 p2,
                       float3 n0, float3 n1, float3 n2, float3 bary, float strength)
{
    float3 c0 = ProjectOntoPlane(posOS, p0, n0);
    float3 c1 = ProjectOntoPlane(posOS, p1, n1);
    float3 c2 = ProjectOntoPlane(posOS, p2, n2);
    float3 phongPos = bary.x * c0 + bary.y * c1 + bary.z * c2;
    return lerp(posOS, phongPos, strength);
}

// ===== Patch Constant Function =====
TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 3> patch)
{
    TessellationFactors f;

    float3 p0 = mul(unity_ObjectToWorld, patch[0].vertex).xyz;
    float3 p1 = mul(unity_ObjectToWorld, patch[1].vertex).xyz;
    float3 p2 = mul(unity_ObjectToWorld, patch[2].vertex).xyz;
    float3 center = (p0 + p1 + p2) / 3.0;

    // Distance-based LOD
    float dist = NataneViewerDistance(center);   // same tessellation in both eyes
    float distFactor = saturate(1.0 - (dist - _TessDistanceMin) / max(_TessDistanceMax - _TessDistanceMin, 0.01));
    float factor = max(1.0, distFactor * _TessFactor);

    f.edge[0] = factor;
    f.edge[1] = factor;
    f.edge[2] = factor;
    f.inside  = factor;
    return f;
}

// ===== Hull Shader =====
[UNITY_domain("tri")]
[UNITY_outputcontrolpoints(3)]
[UNITY_outputtopology("triangle_cw")]
[UNITY_partitioning("fractional_odd")]
[UNITY_patchconstantfunc("PatchConstantFunction")]
TessellationControlPoint hull(
    InputPatch<TessellationControlPoint, 3> patch,
    uint id : SV_OutputControlPointID)
{
    return patch[id];
}

// ===== Domain Shader =====
[UNITY_domain("tri")]
v2f domain(
    TessellationFactors factors,
    OutputPatch<TessellationControlPoint, 3> patch,
    float3 bary : SV_DomainLocation)
{
    // Re-establish the instance / stereo eye index in the domain stage.
    // Without this, Single Pass Instanced VR picks the wrong eye matrices
    // for tessellated geometry (vert() below reads unity_StereoEyeIndex).
    appdata v;
    UNITY_TRANSFER_INSTANCE_ID(patch[0], v);
    UNITY_SETUP_INSTANCE_ID(v);

    // Barycentric interpolation of attributes
    v.vertex = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
    float3 flatNormal = patch[0].normal * bary.x + patch[1].normal * bary.y + patch[2].normal * bary.z;
    v.normal = normalize(flatNormal);
    v.tangent = patch[0].tangent * bary.x + patch[1].tangent * bary.y + patch[2].tangent * bary.z;
    v.uv = patch[0].uv * bary.x + patch[1].uv * bary.y + patch[2].uv * bary.z;
    #if defined(_BACKGROUND_MODE) || defined(_DETAIL_MAP) || defined(_LTCGI)
        v.uv1 = patch[0].uv1 * bary.x + patch[1].uv1 * bary.y + patch[2].uv1 * bary.z;
    #endif
    #if defined(_SMOOTH_NORMAL) || defined(_VERTEX_COLOR_SHADOW)
        v.color = patch[0].color * bary.x + patch[1].color * bary.y + patch[2].color * bary.z;
    #endif

    // Phong Tessellation: smooth surface by projecting onto tangent planes
    float3 linearPos = v.vertex.xyz; // Save pre-Phong position
    v.vertex.xyz = PhongSmoothing(
        v.vertex.xyz,
        patch[0].vertex.xyz, patch[1].vertex.xyz, patch[2].vertex.xyz,
        patch[0].normal, patch[1].normal, patch[2].normal,
        bary, _TessPhongStrength
    );

    // Normal smoothing: adjust normal based on Phong displacement curvature.
    // The displacement vector (Phong pos - linear pos) encodes the surface curvature.
    // Adding it to the interpolated normal with user-controlled strength gives
    // smoother, more "spherical" shading where the mesh curves most.
    {
        float3 phongDisp = v.vertex.xyz - linearPos;
        float3 adjustedNormal = v.normal + phongDisp * _TessNormalSmooth * 4.0;
        v.normal = normalize(adjustedNormal + float3(0, 0, 0.0001));
    }

    // Displacement map: push vertices along normal based on height map
    #ifdef _TESS_DISPLACEMENT
    {
        float2 dispUV = v.uv;
        float height = NATANE_SAMPLE_REPEAT_LOD(_TessDispMap, dispUV, 0).r;
        height = (height + _TessDispOffset) * _TessDispStrength;
        v.vertex.xyz += v.normal * height;
    }
    #endif

    // Use existing vert() for clip space transform, FOG, SHADOW etc.
    return vert(v);
}

#else // !_TESSELLATION

// ===== Pass-through when tessellation is disabled =====
// tessVert simply calls vert, hull/domain pass data through with factor=1
// This results in zero tessellation overhead

v2f tessVert(appdata v) { return vert(v); }

struct TessellationFactors
{
    float edge[3] : SV_TessFactor;
    float inside  : SV_InsideTessFactor;
};

TessellationFactors PatchConstantFunction(InputPatch<v2f, 3> patch)
{
    TessellationFactors f;
    f.edge[0] = f.edge[1] = f.edge[2] = f.inside = 1.0;
    return f;
}

[UNITY_domain("tri")]
[UNITY_outputcontrolpoints(3)]
[UNITY_outputtopology("triangle_cw")]
[UNITY_partitioning("fractional_odd")]
[UNITY_patchconstantfunc("PatchConstantFunction")]
v2f hull(InputPatch<v2f, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

[UNITY_domain("tri")]
v2f domain(TessellationFactors factors, OutputPatch<v2f, 3> patch, float3 bary : SV_DomainLocation)
{
    // With factor=1, bary is always (1,0,0), (0,1,0), or (0,0,1).
    // Select the nearest vertex to correctly pass through ALL v2f fields
    // (including FOG, SHADOW_COORDS, screenPos, VR stereo, etc.)
    v2f o;
    if (bary.x >= bary.y && bary.x >= bary.z)
        o = patch[0];
    else if (bary.y >= bary.z)
        o = patch[1];
    else
        o = patch[2];
    return o;
}

#endif // _TESSELLATION

#endif // UNITY_CAN_COMPILE_TESSELLATION && SHADER_TARGET >= 46

#endif // NATANE_TOON_TESSELLATION_INCLUDED
