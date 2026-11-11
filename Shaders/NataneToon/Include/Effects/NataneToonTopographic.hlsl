// NataneToonTopographic.hlsl
// Topographic / Fault-Slice overlay (等高線/断層スライス).
// Slices the surface with evenly spaced contour bands derived from a spatial
// coordinate. Pure helpers (values passed as arguments); the coordinate build,
// noise distortion and emission-colored composition happen in the fragment.
#ifndef NATANE_TOON_TOPOGRAPHIC_INCLUDED
#define NATANE_TOON_TOPOGRAPHIC_INCLUDED

// Smooth value noise (matches the frac(sin(dot(...))) convention used across
// the shader for distortion) — used to warp the band coordinate.
float NataneTopo_Noise(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    fp = fp * fp * (3.0 - 2.0 * fp);
    // 実体は NataneToonUtils.hlsl の共通ハッシュ。定数は従来値のまま渡しており、
    // 出力はビット単位で変更前と同一。
    const float2 k = float2(12.9898, 78.233);
    float a = NataneHash21(ip + float2(0.0, 0.0), k);
    float b = NataneHash21(ip + float2(1.0, 0.0), k);
    float c = NataneHash21(ip + float2(0.0, 1.0), k);
    float d = NataneHash21(ip + float2(1.0, 1.0), k);
    return lerp(lerp(a, b, fp.x), lerp(c, d, fp.x), fp.y);
}

// Shape a normalized band coordinate into a contour intensity.
// phase:     already-wrapped position (frac() applied internally)
// mode:      0 Lines, 1 Bands, 2 GradientBands, 3 DoubleLines, 4 PulseRings, 5 NoiseDistorted
// lineWidth: contour thickness in band-space [0,1]
// time:      animated time (for PulseRings)
// Returns: x = contour/band intensity [0,1], y = primary/secondary mix factor [0,1]
float2 NataneTopoBand(float phase, float mode, float lineWidth, float time)
{
    float band = frac(phase);
    float lw = clamp(lineWidth, 0.0001, 0.5);
    float intensity;
    float mixFactor = band;
    int m = (int)(mode + 0.5);

    if (m == 1) // Bands: solid alternating stripes
    {
        intensity = step(0.5, band);
        mixFactor = intensity;
    }
    else if (m == 2) // GradientBands: smooth ramp across each band
    {
        intensity = 1.0;
        mixFactor = band;
    }
    else if (m == 3) // DoubleLines: primary contour + finer secondary contour
    {
        float d1 = min(band, 1.0 - band);
        float b2 = frac(phase * 2.0);
        float d2 = min(b2, 1.0 - b2);
        float contour1 = 1.0 - smoothstep(0.0, lw, d1);
        float contour2 = (1.0 - smoothstep(0.0, lw * 0.5, d2)) * 0.5;
        intensity = max(contour1, contour2);
    }
    else if (m == 4) // PulseRings: contour brightness travels over time
    {
        float d = min(band, 1.0 - band);
        float contour = 1.0 - smoothstep(0.0, lw, d);
        float pulse = frac(phase - time);
        float pd = min(pulse, 1.0 - pulse);
        intensity = contour * (0.35 + 0.65 * (1.0 - smoothstep(0.0, lw * 4.0, pd)));
    }
    else // 0 Lines and 5 NoiseDistorted (caller pre-warps phase): thin contour
    {
        float d = min(band, 1.0 - band);
        intensity = 1.0 - smoothstep(0.0, lw, d);
    }

    return float2(saturate(intensity), saturate(mixFactor));
}

#endif // NATANE_TOON_TOPOGRAPHIC_INCLUDED
