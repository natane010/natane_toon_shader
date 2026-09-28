// Noise functions for procedural pattern generation
//
// The hash/noise functions in this file are adapted from Morgan McGuire's
// "Noise functions" (https://www.shadertoy.com/view/4dS3Wd),
// Copyright (c) 2013 Morgan McGuire, BSD 2-Clause License.
// See THIRD_PARTY_NOTICES.md at the package root for the full notice.

#ifndef MORGANS_NOISE_FUNCTIONS_INCLUDED
#define MORGANS_NOISE_FUNCTIONS_INCLUDED

// Hash function for noise generation
float hash(float n)
{
    return frac(sin(n) * 43758.5453123);
}

// 2D hash function
float hash2(float2 p)
{
    return frac(1e4 * sin(17.0 * p.x + p.y * 0.1) * (0.1 + abs(sin(p.y * 13.0 + p.x))));
}

// 3D hash function
float hash3(float3 p)
{
    return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453123);
}

// Value noise 3D
float noise3(float3 x)
{
    float3 p = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);

    float n = p.x + p.y * 157.0 + 113.0 * p.z;
    return lerp(
        lerp(
            lerp(hash(n + 0.0), hash(n + 1.0), f.x),
            lerp(hash(n + 157.0), hash(n + 158.0), f.x),
            f.y),
        lerp(
            lerp(hash(n + 113.0), hash(n + 114.0), f.x),
            lerp(hash(n + 270.0), hash(n + 271.0), f.x),
            f.y),
        f.z);
}

// Fractal Brownian Motion (FBM) - layered noise
float fbm(float3 p)
{
    float f = 0.0;
    float amplitude = 0.5;

    // 4 octaves of noise
    for (int i = 0; i < 4; i++)
    {
        f += amplitude * noise3(p);
        p = p * 2.0;
        amplitude *= 0.5;
    }

    return f;
}

// Advanced pattern generation using multi-layer FBM
// This creates organic, flowing patterns by warping noise with itself
float pattern(float3 p, float UVscale, float TimeScale1, float TimeScale2, float TimeScale3)
{
    // Scale the input position
    p *= UVscale;

    // First layer: basic FBM noise
    float3 q = float3(
        fbm(p + float3(0.0, 0.0, 0.0) + TimeScale1 * _Time.y),
        fbm(p + float3(5.2, 1.3, 6.4)),
        fbm(p + float3(4.3, 7.3, 1.4))
    );

    // Second layer: warp the first layer
    float3 r = float3(
        fbm(p + 4.0 * q + float3(1.7, 9.2, 4.6) + TimeScale2 * _Time.y),
        fbm(p + 4.0 * q + float3(3.5, 8.2, 9.2)),
        fbm(p + 4.0 * q + float3(8.3, 2.8, 1.3) + TimeScale3 * _Time.y)
    );

    // Final layer: warp again with the second layer
    return saturate(fbm(p + 4.0 * r));
}

// Voronoi noise - creates cellular patterns
float2 voronoi(float3 x)
{
    float3 p = floor(x);
    float3 f = frac(x);

    float id = 0.0;
    float2 res = float2(100.0, 100.0);

    for (int k = -1; k <= 1; k++)
    for (int j = -1; j <= 1; j++)
    for (int i = -1; i <= 1; i++)
    {
        float3 b = float3(i, j, k);
        float3 r = float3(b) - f + hash3(p + b);
        float d = dot(r, r);

        if (d < res.x)
        {
            id = hash3(p + b);
            res.y = res.x;
            res.x = d;
        }
        else if (d < res.y)
        {
            res.y = d;
        }
    }

    return float2(sqrt(res.x), sqrt(res.y));
}

#endif // MORGANS_NOISE_FUNCTIONS_INCLUDED
