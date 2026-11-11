// NataneToonShadowShapeRig.hlsl
// Art-directable shadow shaping (Shading Rig style). Pure math, no CBUFFER reads.
//
// The package could already shape the *highlight* (_SHAPED_HIGHLIGHT) but had no way to
// direct the *shadow*: _SHADOW_EDGE_NOISE is random, _USE_MULTI_SHADOW only adds steps,
// and _SDF_MAP fixes the transition order but the shape itself lives in a baked texture
// that cannot be edited in the inspector. This adds the missing side — elliptical rigs
// that locally push the shading boundary out or in.
//
// Everything here is a pure function of its arguments so the file can be included from the
// standalone OUTLINE pass without dragging in Input/Utils.
#ifndef NATANE_TOON_SHADOW_SHAPE_RIG_INCLUDED
#define NATANE_TOON_SHADOW_SHAPE_RIG_INCLUDED

// Elliptical rig weight. Returns 1 inside the ellipse, falling to 0 at its edge.
//   params  = (centerX, centerY, radiusX, radiusY)
//   rotRad  = rotation in radians
//   falloff = 0 (hard edge) .. 1 (very soft)
//
// A slot with either radius at zero is treated as unused and costs one compare.
half NataneRigWeight(float2 uv, float4 params, float rotRad, half falloff)
{
    float2 radius = params.zw;
    if (radius.x <= 1e-5 || radius.y <= 1e-5)
    {
        return 0.0h;
    }

    float2 d = uv - params.xy;

    float s, c;
    sincos(rotRad, s, c);
    // Rotate into the ellipse's local frame, then normalize by the radii so the
    // boundary is always at length == 1 regardless of aspect.
    float2 p = float2(d.x * c + d.y * s, -d.x * s + d.y * c);
    p /= radius;

    half r = (half)length(p);
    // falloff 1.0 would make inner == 0 and smoothstep degenerate, so clamp just under.
    half inner = 1.0h - clamp(falloff, 0.0h, 0.999h);
    return 1.0h - smoothstep(inner, 1.0h, r);
}

#endif // NATANE_TOON_SHADOW_SHAPE_RIG_INCLUDED
