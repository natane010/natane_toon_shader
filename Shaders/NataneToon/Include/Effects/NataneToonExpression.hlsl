// NataneToonExpression.hlsl
// Small helpers for the uniform-only "expression extras" (face / hair / skin / cloth /
// lighting response). Every caller is gated by a strength uniform whose default is 0,
// so none of this runs for existing materials.
#ifndef NATANE_TOON_EXPRESSION_INCLUDED
#define NATANE_TOON_EXPRESSION_INCLUDED

// ----- OKLab (Ottosson 2020), linear sRGB in / out -----
float3 NataneLinearToOKLab(float3 c)
{
    float l = 0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b;
    float m = 0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b;
    float s = 0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b;
    float3 lms = pow(max(float3(l, m, s), 0.0), 1.0 / 3.0);
    return float3(
        0.2104542553 * lms.x + 0.7936177850 * lms.y - 0.0040720468 * lms.z,
        1.9779984951 * lms.x - 2.4285922050 * lms.y + 0.4505937099 * lms.z,
        0.0259040371 * lms.x + 0.7827717662 * lms.y - 0.8086757660 * lms.z);
}

float3 NataneOKLabToLinear(float3 lab)
{
    float l = lab.x + 0.3963377774 * lab.y + 0.2158037573 * lab.z;
    float m = lab.x - 0.1055613458 * lab.y - 0.0638541728 * lab.z;
    float s = lab.x - 0.0894841775 * lab.y - 1.2914855480 * lab.z;
    l = l * l * l; m = m * m * m; s = s * s * s;
    return float3(
         4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
        -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
        -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
}

// Shadow colour derived from the albedo: darker, more chromatic, hue rotated. Returned
// as a multiplier on the albedo because that is what the shadow colour is in this shader.
half3 NataneOKLabShadowMultiplier(half3 albedo, half hueShift, half chroma, half lightness)
{
    float3 lin = saturate(albedo);
    #ifdef UNITY_COLORSPACE_GAMMA
        lin = GammaToLinearSpace(lin);
    #endif
    float3 lab = NataneLinearToOKLab(lin);
    float ang = hueShift * 6.28318530718;
    float sn = sin(ang);
    float cs = cos(ang);
    float2 ab = float2(lab.y * cs - lab.z * sn, lab.y * sn + lab.z * cs) * chroma;
    float3 shade = max(NataneOKLabToLinear(float3(lab.x * lightness, ab)), 0.0);
    #ifdef UNITY_COLORSPACE_GAMMA
        shade = LinearToGammaSpace(shade);
        lin = saturate(albedo);
    #endif
    return (half3)saturate(shade / max(lin, 0.02));
}

// Skin transmittance profile: the sum of Gaussians of Jimenez et al., "Real-Time
// Realistic Skin Translucency" (2010). s = optical thickness; 0 gives white.
half3 NataneSkinTransmittance(half s)
{
    float dd = -(float)s * (float)s;
    float3 t = float3(0.233, 0.455, 0.649) * exp(dd / 0.0064)
             + float3(0.100, 0.336, 0.344) * exp(dd / 0.0484)
             + float3(0.118, 0.198, 0.000) * exp(dd / 0.187)
             + float3(0.113, 0.007, 0.007) * exp(dd / 0.567)
             + float3(0.358, 0.004, 0.000) * exp(dd / 1.99)
             + float3(0.078, 0.000, 0.000) * exp(dd / 7.41);
    return (half3)t;
}

// Soft shadow of an analytic sphere (Quilez). 1 = lit, 0 = fully occluded.
// Points inside the sphere are left lit so the occluder's own mesh is not blacked out.
half NataneSphereSoftShadow(float3 ro, float3 rd, float3 center, float radius, float k)
{
    float3 oc = ro - center;
    float b = dot(oc, rd);
    float c = dot(oc, oc) - radius * radius;
    float h = b * b - c;
    if (c < 0.0 || b > 0.0) return 1.0;
    return (half)smoothstep(0.0, 1.0, h * k / b);
}

// Dominant direction of a rough specular lobe (Lagarde & de Rousiers, "Moving Frostbite
// to PBR", 2014): rough reflections lean back towards the normal.
half3 NataneSpecularDominantDir(half3 N, half3 R, half roughness)
{
    half smoothness = saturate(1.0 - roughness);
    half lerpFactor = smoothness * (sqrt(smoothness) + roughness);
    return normalize(lerp(N, R, lerpFactor));
}

#endif // NATANE_TOON_EXPRESSION_INCLUDED
