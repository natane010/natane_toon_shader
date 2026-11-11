// NataneToonFXModulator.hlsl
// General-purpose FX Modulator (汎用FXモジュレーター): two slots that each
// evaluate a Source signal and drive a selectable Target parameter.
//
// Source evaluation is a pure function (arguments only). AudioLink sources
// reuse the existing SampleAudioLink* helpers from NataneToonUtils.hlsl and are
// guarded by _AUDIOLINK so the file also compiles in the standalone OUTLINE
// pass (where Utils / _AudioTexture are absent → neutral 0). The per-slot state
// is computed once via NataneFXModCompute (guarded by _FX_MODULATOR, reads the
// _FXMod* uniforms) and then applied at each target site through the pure
// NataneFXModMul / NataneFXModAdd helpers.
#ifndef NATANE_TOON_FX_MODULATOR_INCLUDED
#define NATANE_TOON_FX_MODULATOR_INCLUDED

// Target ids (must match the [Enum(...)] _FXModTarget order in the .shader)
#define NATANE_FXT_NONE          0
#define NATANE_FXT_EMISSION      1
#define NATANE_FXT_HUESHIFT      2
#define NATANE_FXT_RIM           3
#define NATANE_FXT_OUTLINE_WIDTH 4
#define NATANE_FXT_LINEBOIL      5
#define NATANE_FXT_TOPO_OFFSET   6
#define NATANE_FXT_SHAPED_HL     7
#define NATANE_FXT_CAUSTICS      8
#define NATANE_FXT_LENTICULAR    9
#define NATANE_FXT_SPECULAR      10
#define NATANE_FXT_MATCAP        11
#define NATANE_FXT_ALPHA_FADE    12
// DissolveAmount: AlphaFade(12) は単純なアルファ減衰でエッジ発光を伴わないため
// ディゾルブの代替にならない。Animator を使わずに時間・音・距離で自走する
// ディゾルブを作れるようにするための専用ターゲット。
#define NATANE_FXT_DISSOLVE      13

float NataneFXMod_Hash1(float x) { return frac(sin(x * 12.9898) * 43758.5453); }

// Self-contained hash / value-noise helpers for the noise sources. These are
// defined locally (NOT reused from NataneToonUtils.hlsl) because this file is
// also included in the standalone OUTLINE pass where Utils is absent.
//
// 【集約しないこと】NataneToonUtils.hlsl に NataneHash21 / NataneHash22 /
// NataneProjectionCoord があるが、ここからは呼べない。
// NataneToonOutlinePass.hlsl は Input も Utils も include せずこのファイルだけを
// 取り込むため、Utils を参照した瞬間に OUTLINE パスがコンパイルできなくなる。
// なお NataneFXMod_Hash2 は sin ベースではなく別系統のハッシュなので、
// そもそも共通版とは出力が一致しない（統一すると見た目が変わる）。
float NataneFXMod_Hash2(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Bilinear-smoothed value noise in [0,1].
float NataneFXMod_ValueNoise(float2 p)
{
    float2 c = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = NataneFXMod_Hash2(c);
    float b = NataneFXMod_Hash2(c + float2(1.0, 0.0));
    float cc = NataneFXMod_Hash2(c + float2(0.0, 1.0));
    float d = NataneFXMod_Hash2(c + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(cc, d, f.x), f.y);
}

// Project the selected space (0 UV / 1 Object / 2 World) into a 2D noise coord.
float2 NataneFXMod_NoiseCoord(int space, float2 uv, float3 objectPos, float3 worldPos)
{
    if (space == 1) return float2(objectPos.x + objectPos.z * 0.5, objectPos.y + objectPos.z * 0.5); // Object
    if (space == 2) return float2(worldPos.x + worldPos.z * 0.5, worldPos.y + worldPos.z * 0.5);      // World
    return uv;                                                                                        // UV
}

// Raw source signal, normalized to [0,1]. source id matches the
// [Enum(...)] _FXModSource order in the .shader.
half NataneFXMod_Source(int source, float speed, float offset, float manual,
                        float3 worldPos, float3 worldNormal, float3 viewDir,
                        float distMin, float distMax,
                        float2 uv, float3 objectPos, float noiseScale, int noiseSpace)
{
    float t = _Time.y * speed + offset;
    if (source == 0) return sin(t * 6.2831853) * 0.5 + 0.5;   // Sine
    if (source == 1) return frac(t);                          // Saw
    if (source == 2) return abs(frac(t) * 2.0 - 1.0);         // Triangle
    if (source == 3) return step(0.5, frac(t));               // Pulse
    if (source == 4) return NataneFXMod_Hash1(floor(t));      // RandomStep
    #if defined(_AUDIOLINK)
    if (source == 5) return SampleAudioLink(0);               // AudioLink Bass
    if (source == 6) return SampleAudioLink(1);               // AudioLink LowMid
    if (source == 7) return SampleAudioLink(2);               // AudioLink HighMid
    if (source == 8) return SampleAudioLink(3);               // AudioLink Treble
    if (source == 9) return SampleAudioLinkChronotensity(0);  // Chronotensity (Bass, MotionIncrease)
    #else
    if (source >= 5 && source <= 9) return 0.0;               // Neutral when AudioLink absent
    #endif
    if (source == 10)                                         // CameraDistance
    {
        float d = NataneViewerDistance(worldPos);   // eye-centred, mirror-aware
        return saturate((d - distMin) / max(distMax - distMin, 0.0001));
    }
    if (source == 11) return saturate(dot(normalize(worldNormal), normalize(viewDir))); // ViewAngle
    if (source >= 13)
    {
        float2 nc = NataneFXMod_NoiseCoord(noiseSpace, uv, objectPos, worldPos) * max(noiseScale, 0.0001);
        if (source == 13)                                     // StaticNoise (spatial, no time)
            return NataneFXMod_ValueNoise(nc + offset);
        if (source == 14)                                     // DynamicNoise (2-octave, scrolled)
        {
            float2 scroll = nc + _Time.y * speed + offset;
            float n = NataneFXMod_ValueNoise(scroll) * 0.6
                    + NataneFXMod_ValueNoise(scroll * 2.03 + 17.0) * 0.4;
            return saturate(n);
        }
        // 15 DynamicNoiseSteps: time quantized to Speed-driven steps → flicker/glitch
        float tq = floor(_Time.y * max(speed, 0.0001));
        return NataneFXMod_ValueNoise(nc + tq * 1.37 + offset);
    }
    return saturate(manual);                                  // Manual (12)
}

struct NataneFXModState
{
    half v0;  // signed delta for slot 0 (already scaled by amount & mask)
    half v1;  // signed delta for slot 1
    int  t0;  // slot 0 target id
    int  t1;  // slot 1 target id
};

// Evaluate a single slot into a signed delta contribution.
half NataneFXMod_Slot(int source, float speed, float offset, float manual,
                      float invert, float curve, float mn, float mx,
                      float amount, float mask,
                      float3 wp, float3 wn, float3 vd, float distMin, float distMax,
                      float2 uv, float3 op, float noiseScale, int noiseSpace)
{
    half s = NataneFXMod_Source(source, speed, offset, manual, wp, wn, vd, distMin, distMax,
                                uv, op, noiseScale, noiseSpace);
    s = pow(saturate(s), max(curve, 0.0001));
    half outv = lerp(mn, mx, s);
    outv = (invert >= 0.5) ? (mn + mx - outv) : outv;
    return outv * amount * mask;
}

#if defined(_FX_MODULATOR)
// Compute both slots once. mask0/mask1 come from the shared mask texture (R/G).
// uv/op are needed by the spatial noise sources (13-15); op is object-space pos.
NataneFXModState NataneFXModCompute(float3 wp, float3 wn, float3 vd, float2 uv, float3 op,
                                    float mask0, float mask1)
{
    NataneFXModState st;
    st.v0 = NataneFXMod_Slot((int)_FXModSource0, _FXModSpeed0, _FXModOffset0, _FXModManual0,
                             _FXModInvert0, _FXModCurve0, _FXModMin0, _FXModMax0, _FXModAmount0, mask0,
                             wp, wn, vd, _FXModDistMin0, _FXModDistMax0,
                             uv, op, _FXModNoiseScale0, (int)_FXModNoiseSpace0);
    st.v1 = NataneFXMod_Slot((int)_FXModSource1, _FXModSpeed1, _FXModOffset1, _FXModManual1,
                             _FXModInvert1, _FXModCurve1, _FXModMin1, _FXModMax1, _FXModAmount1, mask1,
                             wp, wn, vd, _FXModDistMin1, _FXModDistMax1,
                             uv, op, _FXModNoiseScale1, (int)_FXModNoiseSpace1);
    st.t0 = (int)_FXModTarget0;
    st.t1 = (int)_FXModTarget1;
    return st;
}
#endif // _FX_MODULATOR

// Multiplicative factor for a target (neutral 1.0). Use for intensities.
half NataneFXModMul(NataneFXModState s, int target)
{
    half m = 1.0;
    m *= (s.t0 == target) ? (1.0 + s.v0) : 1.0;
    m *= (s.t1 == target) ? (1.0 + s.v1) : 1.0;
    return m;
}

// Additive term for a target (neutral 0.0). Use for hue / offset style targets.
half NataneFXModAdd(NataneFXModState s, int target)
{
    half a = 0.0;
    a += (s.t0 == target) ? s.v0 : 0.0;
    a += (s.t1 == target) ? s.v1 : 0.0;
    return a;
}

#endif // NATANE_TOON_FX_MODULATOR_INCLUDED
