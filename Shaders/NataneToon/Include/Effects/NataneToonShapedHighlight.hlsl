// NataneToonShapedHighlight.hlsl
// Shaped Toon Highlight (形状付きトゥーンハイライト).
// Procedural 2D signed-distance shapes used to replace/augment the round
// specular highlight. All SDFs are pure math (no CBUFFER reads) so the file is
// self-contained. Formulas follow the well-known iq 2D SDF set and reuse the
// star/heart approach already present in Shaders/NataneToon/Eye/.
//
// Convention: p is a coordinate roughly in [-1, 1]; the shape is centered at
// the origin at unit scale; the return value is a signed distance that is
// negative inside the shape and positive outside.
#ifndef NATANE_TOON_SHAPED_HIGHLIGHT_INCLUDED
#define NATANE_TOON_SHAPED_HIGHLIGHT_INCLUDED

float NataneSHL_Dot2(float2 v) { return dot(v, v); }

float NataneSHL_Circle(float2 p) { return length(p) - 1.0; }

float NataneSHL_Ring(float2 p) { return abs(length(p) - 0.72) - 0.20; }

float NataneSHL_Cross(float2 p)
{
    // Plus/cross sign — iq sdCross with arm length 1, thickness 0.30
    float2 b = float2(1.0, 0.30);
    p = abs(p);
    p = (p.y > p.x) ? p.yx : p.xy;
    float2 q = p - b;
    float k = max(q.y, q.x);
    float2 w = (k > 0.0) ? q : float2(b.y - p.x, -k);
    return sign(k) * length(max(w, 0.0));
}

float NataneSHL_Star5(float2 p, float r, float rf)
{
    // iq sdStar5 (5-point star)
    const float2 k1 = float2(0.809016994375, -0.587785252292);
    const float2 k2 = float2(-k1.x, k1.y);
    p.x = abs(p.x);
    p -= 2.0 * max(dot(k1, p), 0.0) * k1;
    p -= 2.0 * max(dot(k2, p), 0.0) * k2;
    p.x = abs(p.x);
    p.y -= r;
    float2 ba = rf * float2(-k1.y, k1.x) - float2(0.0, 1.0);
    float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
    return length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
}

float NataneSHL_Heart(float2 p)
{
    // iq sdHeart, re-oriented so the cusp points up and it fills ~unit scale.
    p *= 0.85;
    p.y = -p.y;          // point the dimple upward
    p.y += 0.5;
    p.x = abs(p.x);
    if (p.y + p.x > 1.0)
        return sqrt(NataneSHL_Dot2(p - float2(0.25, 0.75))) - sqrt(2.0) / 4.0;
    return sqrt(min(NataneSHL_Dot2(p - float2(0.00, 1.00)),
                    NataneSHL_Dot2(p - 0.5 * max(p.x + p.y, 0.0)))) * sign(p.x - p.y);
}

float NataneSHL_Diamond(float2 p) { return abs(p.x) + abs(p.y) - 1.0; }

float NataneSHL_Crescent(float2 p)
{
    // Outer circle minus an offset circle carved from one side.
    float outer = length(p) - 1.0;
    float carve = length(p - float2(0.55, 0.0)) - 0.90;
    return max(outer, -carve);
}

float NataneSHL_Line(float2 p)
{
    // Thick horizontal streak capped along X.
    return max(abs(p.y) - 0.18, abs(p.x) - 1.0);
}

// Dispatch a procedural shape. shapeMode 0..7 (Custom = 8 is handled by the
// caller via the SDF texture). Returns a signed distance (negative = inside).
half NataneShapeSDF(float2 p, float shapeMode)
{
    int m = (int)(shapeMode + 0.5);
    if (m == 0) return NataneSHL_Circle(p);
    if (m == 1) return NataneSHL_Ring(p);
    if (m == 2) return NataneSHL_Cross(p);
    if (m == 3) return NataneSHL_Star5(p, 1.0, 0.45);
    if (m == 4) return NataneSHL_Heart(p);
    if (m == 5) return NataneSHL_Diamond(p);
    if (m == 6) return NataneSHL_Crescent(p);
    return NataneSHL_Line(p);
}

// Convert a signed distance to soft coverage in [0,1] using the AA-friendly
// smoothstep style already used by the toon step (fwidth-compatible edge).
half NataneShapeCoverage(half sdf, half softness)
{
    half aa = max(fwidth(sdf), 1e-5);
    half edge = max(softness, aa);
    return 1.0 - smoothstep(-edge, edge, sdf);
}

#endif // NATANE_TOON_SHAPED_HIGHLIGHT_INCLUDED
