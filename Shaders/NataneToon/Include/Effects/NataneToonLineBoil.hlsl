// NataneToonLineBoil.hlsl
// Line Boil (ラインボイル): time-quantized hand-drawn "boil" jitter.
//
// Pure helpers only (NataneLineBoilOffset takes every value as an argument and
// touches no CBUFFER member) so this file can be used both from the main
// fragment path and from the standalone OUTLINE pass. The single helper that
// reads _LineBoil* uniforms (NataneLineBoilPhase) is guarded by _LINE_BOIL and
// assumes those uniforms are declared beforehand — from the CBUFFER in
// NataneToonInput.hlsl for the forward passes, or inline in the OUTLINE pass.
#ifndef NATANE_TOON_LINE_BOIL_INCLUDED
#define NATANE_TOON_LINE_BOIL_INCLUDED

// Hash-based per-frame offset. Uses the same frac(sin(dot(...))) hash
// convention as GetHandDrawnJitter / Smear elsewhere in the codebase.
// seedPos: spatial seed (object- or world-space position)
// phase:   quantized animation phase (stable within a held frame)
// amount:  magnitude of the returned [-amount, amount] offset
float3 NataneLineBoilOffset(float3 seedPos, float phase, float amount)
{
    float3 seed = seedPos + phase * 1.7;
    float h1 = frac(sin(dot(seed.xy + phase, float2(12.9898, 78.233))) * 43758.5453);
    float h2 = frac(sin(dot(seed.yz + phase, float2(45.164, 93.177))) * 27183.8241);
    float h3 = frac(sin(dot(seed.xz + phase, float2(63.419, 17.652))) * 69143.2758);
    return (float3(h1, h2, h3) * 2.0 - 1.0) * amount;
}

#if defined(_LINE_BOIL)
// Quantized animation phase: floor(time * fps / holdFrames) + seed.
// Holding for N frames keeps the jitter frozen for N "boil" frames, matching
// hand-drawn animation on 2s / 3s.
float NataneLineBoilPhase()
{
    float fps  = max(_LineBoilFPS, 0.0001);
    float hold = max(_LineBoilHoldFrames, 1.0);
    return floor(_Time.y * fps / hold) + _LineBoilRandomSeed;
}
#endif // _LINE_BOIL

#endif // NATANE_TOON_LINE_BOIL_INCLUDED
