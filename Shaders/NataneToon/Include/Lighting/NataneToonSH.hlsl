#ifndef NATANE_TOON_SH_INCLUDED
#define NATANE_TOON_SH_INCLUDED

// =============================================================================
// Natane Toon - Shared Spherical Harmonics (Ambient) Evaluator
// =============================================================================
// Unified per-pixel SH evaluation used by every ambient/indirect SH lookup in
// the shader (Fragment, Fur Shell, Particle, Background). It mirrors Unity
// 2022.3 Built-in `ShadeSHPerPixel()` (UnityStandardUtils.cginc) so that
// Light Probe Proxy Volume (LPPV) is honoured transparently:
//
//   * When the `UNITY_LIGHT_PROBE_PROXY_VOLUME` keyword is compiled in AND the
//     renderer is currently driven by an LPPV component
//     (`unity_ProbeVolumeParams.x == 1`), the L0/L1 term is sampled from the
//     3D probe-volume texture (`unity_ProbeVolumeSH`) per pixel.
//   * Otherwise it falls back to the ordinary per-object SH (identical output
//     to the classic `ShadeSH9()` call it replaces).
//
// This keeps normal (non-LPPV) worlds bit-for-bit identical to the previous
// `ShadeSH9()` behaviour while adding volumetric probe support where available.
//
// Requirements:
//   * Include AFTER "UnityCG.cginc" (provides SHEvalLinearL0L1 / L2 and the
//     LPPV sampler helper `SHEvalLinearL0L1_SampleProbeVolume`).
//   * `UNITY_LIGHT_PROBE_PROXY_VOLUME` resolves to 0/1 inside
//     UnityShaderVariables.cginc, so the `#if` below is always well-defined.
//   * LPPV path is only reached in ForwardBase passes that declare
//     `#pragma multi_compile _ UNITY_LIGHT_PROBE_PROXY_VOLUME`. In every other
//     pass the macro is 0 and this collapses to the classic SH path.
// =============================================================================

// Evaluates the RGB ambient/indirect SH contribution for a world-space normal.
// `worldPos` is only consumed by the LPPV branch; on the classic path it is
// dead code and stripped by the compiler.
half3 NataneShadeSH(half3 worldNormal, float3 worldPos)
{
    // Linear + constant polynomial terms (L0 / L1).
#if UNITY_LIGHT_PROBE_PROXY_VOLUME
    half3 res;
    UNITY_BRANCH
    if (unity_ProbeVolumeParams.x == 1.0)
    {
        res = SHEvalLinearL0L1_SampleProbeVolume(half4(worldNormal, 1.0), worldPos);
    }
    else
    {
        res = SHEvalLinearL0L1(half4(worldNormal, 1.0));
    }
#else
    half3 res = SHEvalLinearL0L1(half4(worldNormal, 1.0));
#endif

    // Quadratic polynomials (L2) always come from the per-object SH, matching
    // Unity's ShadeSHPerPixel (L2 is not stored in the probe volume texture).
    res += SHEvalLinearL2(half4(worldNormal, 1.0));

#ifdef UNITY_COLORSPACE_GAMMA
    res = LinearToGammaSpace(res);
#endif

    return res;
}

#endif // NATANE_TOON_SH_INCLUDED
