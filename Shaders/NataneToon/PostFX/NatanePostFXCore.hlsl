// NatanePostFXCore.hlsl
//
// Natane Post FX の全パスが使う共通部。Built-in RP と URP の両方から同じコードを使うため、
// 入力テクスチャはすべて C# 側が MaterialPropertyBlock で渡し、頂点は SV_VertexID から作る
// (Graphics.Blit の _MainTex や URP の _BlitTexture に依存しない)。
// VR (シングルパス インスタンシング) は対象外。
#ifndef NATANE_POSTFX_CORE_INCLUDED
#define NATANE_POSTFX_CORE_INCLUDED

#include "UnityCG.cginc"

sampler2D _NPFX_Source;
sampler2D _NPFX_Aux;
sampler2D _NPFX_BloomTex;
sampler2D _NPFX_BlurTex;
sampler2D _NPFX_DofTex;
sampler2D _NPFX_StreakTex;
sampler2D _NPFX_ShaftTex;
sampler2D _NPFX_MaskTex;
sampler2D _NPFX_HistoryTex;
sampler2D _NPFX_LutTex;
sampler2D _NPFX_OverlayTex;
UNITY_DECLARE_DEPTH_TEXTURE(_NPFX_DepthTex);
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float4 _NPFX_SourceSize;   // xy = 1 / サイズ, zw = サイズ (このパスの入力)
float4 _NPFX_ScreenSize;   // xy = 1 / サイズ, zw = サイズ (カメラの解像度)
float4 _NPFX_StepParams;   // パスごとの値 (ぼかしの向きなど)
float _NPFX_Time;
float _NPFX_DepthMode;     // 0 = 深度なし, 1 = _CameraDepthTexture, 2 = _NPFX_DepthTex

struct NPFXVaryings
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

// 画面を覆う三角形 1 枚。中間テクスチャ同士のコピーで上下が変わらない向きにしてある。
NPFXVaryings NPFXVert(uint vertexID : SV_VertexID)
{
    NPFXVaryings o;
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    o.pos = float4(uv * 2.0 - 1.0, 0.0, 1.0);
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
#endif
    o.uv = uv;
    return o;
}

inline float3 NPFXSample(float2 uv) { return tex2Dlod(_NPFX_Source, float4(uv, 0, 0)).rgb; }
inline float4 NPFXSample4(float2 uv) { return tex2Dlod(_NPFX_Source, float4(uv, 0, 0)); }
inline float NPFXLuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }
// 見た目の明るさ (網点や 2 値化のしきい値は、リニアの輝度のままだと暗い側に寄りすぎる)。
inline float NPFXPerceptual(float l)
{
#ifdef UNITY_COLORSPACE_GAMMA
    return saturate(l);
#else
    return sqrt(saturate(l));
#endif
}

float NPFXEyeDepth(float2 uv)
{
    if (_NPFX_DepthMode < 0.5) return 1000.0;
    float raw = _NPFX_DepthMode > 1.5
        ? SAMPLE_DEPTH_TEXTURE_LOD(_NPFX_DepthTex, float4(uv, 0, 0))
        : SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(uv, 0, 0));
    return LinearEyeDepth(raw);
}

float NPFXHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float NPFXNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = NPFXHash(i);
    float b = NPFXHash(i + float2(1, 0));
    float c = NPFXHash(i + float2(0, 1));
    float d = NPFXHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float2 NPFXRotate(float2 p, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float3 NPFXRgbToHsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 NPFXHsvToRgb(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

// ============================================================================
// 0: コピー
// ============================================================================
float4 NPFXFragCopy(NPFXVaryings i) : SV_Target
{
    return NPFXSample4(i.uv);
}

// ============================================================================
// 1: レンズ (UV を動かす効果と、色ずれ・方向ブラー)
// ============================================================================
float4 _L_Distort;     // x 歪曲, y 色収差, z RGB ずらし量, w RGB ずらし角度
float4 _L_Xform;       // x ズーム, y 回転, zw オフセット
float4 _L_RadialBlur;  // x 放射ブラー, yz 中心, w 方向ブラー
float4 _L_Mirror;      // x ミラー (0 なし 1 左右 2 上下 3 四分割), y 万華鏡の分割数, z 万華鏡の回転, w タイル X
float4 _L_Wave;        // x 波の振幅, y 波の周波数, z 波の位相, w 陽炎
float4 _L_Ripple;      // xy 中心, z 半径, w 強さ
float4 _L_Glitch;      // x ブロックずれ, y ブロック数, z 走査線ずれ, w 縦ジャンプ
float4 _L_Glitch2;     // x 色ずれノイズ, y シード, z VHS のうねり, w ブラウン管の湾曲
float4 _L_Misc;        // x 方向ブラー角度, y タイル Y, z 波紋の幅, w モザイクのピクセル数

float2 NPFXLensUV(float2 uv, out float outside)
{
    outside = 0.0;
    float aspect = _NPFX_ScreenSize.z * _NPFX_ScreenSize.y;

    // ブラウン管の湾曲。はみ出した所は黒。
    if (_L_Glitch2.w > 0.0001)
    {
        float2 c = uv * 2.0 - 1.0;
        c *= 1.0 + dot(c, c) * _L_Glitch2.w * 0.25;
        c /= 1.0 + _L_Glitch2.w * 0.25;
        uv = c * 0.5 + 0.5;
        outside = step(1.0, max(abs(c.x), abs(c.y)));
    }

    // ズーム・回転・平行移動 (画面中心が基準)
    float2 p = uv - 0.5;
    p.x *= aspect;
    p = NPFXRotate(p, _L_Xform.y) / max(_L_Xform.x, 0.01);
    p.x /= aspect;
    uv = p + 0.5 + _L_Xform.zw;

    // タイル
    if (_L_Mirror.w > 1.0 || _L_Misc.y > 1.0)
        uv = frac(uv * float2(max(_L_Mirror.w, 1.0), max(_L_Misc.y, 1.0)));

    // ミラー
    if (_L_Mirror.x > 0.5)
    {
        if (_L_Mirror.x < 1.5 || _L_Mirror.x > 2.5) uv.x = 0.5 - abs(uv.x - 0.5);
        if (_L_Mirror.x > 1.5) uv.y = 0.5 - abs(uv.y - 0.5);
    }

    // 万華鏡
    if (_L_Mirror.y > 1.5)
    {
        float2 k = uv - 0.5;
        k.x *= aspect;
        float seg = UNITY_TWO_PI / _L_Mirror.y;
        float ang = atan2(k.y, k.x) + _L_Mirror.z;
        ang = abs(fmod(abs(ang), seg) - seg * 0.5);
        k = length(k) * float2(cos(ang), sin(ang));
        k.x /= aspect;
        // はみ出した分は鏡のように折り返す。
        uv = 1.0 - abs(1.0 - 2.0 * frac((k + 0.5) * 0.5));
    }

    // レンズ歪曲 (正 = 樽型, 負 = 糸巻き型)
    if (abs(_L_Distort.x) > 0.0001)
    {
        float2 d = uv * 2.0 - 1.0;
        d.x *= aspect;
        float r2 = dot(d, d);
        d *= 1.0 + r2 * _L_Distort.x;
        d /= 1.0 + max(_L_Distort.x, 0.0);
        d.x /= aspect;
        uv = d * 0.5 + 0.5;
    }

    // 波
    if (_L_Wave.x > 0.00001)
    {
        uv.x += sin(uv.y * _L_Wave.y + _L_Wave.z) * _L_Wave.x;
        uv.y += cos(uv.x * _L_Wave.y * 0.7 + _L_Wave.z * 1.3) * _L_Wave.x * 0.5;
    }

    // 陽炎
    if (_L_Wave.w > 0.00001)
    {
        float2 n = float2(NPFXNoise(uv * 12.0 + float2(0.0, _NPFX_Time * 0.8)), NPFXNoise(uv * 12.0 + float2(7.3, -_NPFX_Time * 0.6)));
        uv += (n - 0.5) * _L_Wave.w * 0.05;
    }

    // 波紋・衝撃波
    if (abs(_L_Ripple.w) > 0.00001)
    {
        float2 r = uv - _L_Ripple.xy;
        r.x *= aspect;
        float dist = length(r);
        float band = 1.0 - saturate(abs(dist - _L_Ripple.z) / max(_L_Misc.z, 0.001));
        band = band * band * (3.0 - 2.0 * band);
        float2 dir = dist > 1e-5 ? r / dist : float2(0, 0);
        dir.x /= aspect;
        uv -= dir * band * _L_Ripple.w * 0.1;
    }

    // グリッチ
    float seed = _L_Glitch2.y;
    if (_L_Glitch.x > 0.0001)
    {
        float2 blocks = float2(max(_L_Glitch.y * 0.35, 1.0), max(_L_Glitch.y, 1.0));
        float2 cell = floor(uv * blocks);
        float pick = NPFXHash(cell + seed * 17.0);
        float gate = step(1.0 - _L_Glitch.x * 0.6, pick);
        uv.x += (NPFXHash(cell + seed * 3.1) - 0.5) * gate * _L_Glitch.x * 0.4;
    }
    if (_L_Glitch.z > 0.0001)
    {
        float row = floor(uv.y * _NPFX_ScreenSize.w * 0.5);
        float jitter = NPFXHash(float2(row, seed * 9.7)) - 0.5;
        float gate = step(0.5, NPFXHash(float2(floor(row * 0.05), seed * 5.3)));
        uv.x += jitter * gate * _L_Glitch.z * 0.05;
    }
    if (_L_Glitch.w > 0.0001)
    {
        float jump = step(1.0 - _L_Glitch.w * 0.5, NPFXHash(float2(seed, 1.7)));
        uv.y = frac(uv.y + jump * NPFXHash(float2(seed, 4.2)));
    }
    if (_L_Glitch2.z > 0.0001)
    {
        float wob = sin(uv.y * 40.0 + _NPFX_Time * 6.0) * 0.002 + (NPFXNoise(float2(uv.y * 8.0, _NPFX_Time * 3.0)) - 0.5) * 0.01;
        float band = smoothstep(0.0, 0.06, abs(frac(uv.y - _NPFX_Time * 0.15) - 0.5) - 0.44);
        uv.x += (wob + band * 0.03) * _L_Glitch2.z;
    }

    // モザイク
    if (_L_Misc.w > 1.0)
    {
        float2 cells = _NPFX_ScreenSize.zw / _L_Misc.w;
        uv = (floor(uv * cells) + 0.5) / cells;
    }
    return uv;
}

float3 NPFXLensTap(float2 uv, float2 center)
{
    float2 fromCenter = uv - center;
    float2 ca = fromCenter * dot(fromCenter, fromCenter) * _L_Distort.y * 0.5;
    float2 shift = float2(cos(_L_Distort.w), sin(_L_Distort.w)) * _L_Distort.z * 0.02;
    if (_L_Glitch2.x > 0.0001)
    {
        float n = NPFXHash(float2(floor(uv.y * 24.0), _L_Glitch2.y * 2.3)) - 0.5;
        shift.x += n * _L_Glitch2.x * 0.03;
    }
    float2 off = ca + shift;
    float3 c = NPFXSample(uv);
    if (dot(off, off) >= 1e-10)
    {
        c.r = NPFXSample(uv + off).r;
        c.b = NPFXSample(uv - off).b;
    }
    return c;
}

float4 NPFXFragLens(NPFXVaryings i) : SV_Target
{
    float outside;
    float2 uv = NPFXLensUV(i.uv, outside);
    float2 center = float2(0.5, 0.5);
    float3 col;

    bool radial = _L_RadialBlur.x > 0.0001;
    bool directional = _L_RadialBlur.w > 0.0001;
    if (radial || directional)
    {
        const int kTaps = 10;
        float2 stride = float2(0, 0);
        if (radial) stride += (_L_RadialBlur.yz - uv) * _L_RadialBlur.x * 0.5 / kTaps;
        if (directional) stride += float2(cos(_L_Misc.x), sin(_L_Misc.x)) * _L_RadialBlur.w * 0.1 / kTaps;
        float jitter = NPFXHash(i.uv * _NPFX_ScreenSize.zw + _NPFX_Time) - 0.5;
        float3 sum = 0;
        for (int t = 0; t < kTaps; t++)
        {
            float f = (float)t + jitter - (directional && !radial ? kTaps * 0.5 : 0.0);
            sum += NPFXLensTap(uv + stride * f, center);
        }
        col = sum / kTaps;
    }
    else
    {
        col = NPFXLensTap(uv, center);
    }
    return float4(col * (1.0 - outside), 1.0);
}

// ============================================================================
// 2: 画風 (近傍を読むフィルター)
// ============================================================================
float4 _S_Kuwahara;   // x 桑原フィルターの半径, y ポスタライズ段数, z シャープ, w レトロ減色のビット数
float4 _S_Edge;       // x 輪郭線の強さ, y 太さ, z 深度の感度, w 色の感度
float4 _S_EdgeColor;
float4 _S_Paper;      // rgb 紙の色, a 線画化の度合い
float4 _S_Halftone;   // x 網点の強さ, y 網点の大きさ, z 角度, w クロスハッチ
float4 _S_Extra;      // x ディザの粗さ, y 網点をカラーにする, z エンボス, w 未使用

float3 NPFXKuwahara(float2 uv, float radius)
{
    float2 texel = _NPFX_SourceSize.xy;
    float3 mean[4];
    float3 sq[4];
    int q;
    for (q = 0; q < 4; q++) { mean[q] = 0; sq[q] = 0; }
    int r = (int)clamp(radius, 1.0, 5.0);
    float n = (float)((r + 1) * (r + 1));
    for (int y = 0; y <= 5; y++)
    {
        if (y > r) break;
        for (int x = 0; x <= 5; x++)
        {
            if (x > r) break;
            float2 o = float2(x, y) * texel;
            float3 c0 = NPFXSample(uv + float2(-o.x, -o.y));
            float3 c1 = NPFXSample(uv + float2(o.x, -o.y));
            float3 c2 = NPFXSample(uv + float2(-o.x, o.y));
            float3 c3 = NPFXSample(uv + float2(o.x, o.y));
            mean[0] += c0; sq[0] += c0 * c0;
            mean[1] += c1; sq[1] += c1 * c1;
            mean[2] += c2; sq[2] += c2 * c2;
            mean[3] += c3; sq[3] += c3 * c3;
        }
    }
    float best = 1e10;
    float3 result = 0;
    for (q = 0; q < 4; q++)
    {
        float3 m = mean[q] / n;
        float3 v = abs(sq[q] / n - m * m);
        float variance = v.r + v.g + v.b;
        if (variance < best) { best = variance; result = m; }
    }
    return result;
}

float NPFXBayer4(float2 pixel)
{
    int x = (int)fmod(pixel.x, 4.0);
    int y = (int)fmod(pixel.y, 4.0);
    static const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (m[y * 4 + x] + 0.5) / 16.0;
}

// 網点 1 色ぶん。ink (0-1) が濃いほど点が大きくなる。
float NPFXHalftoneDot(float2 pixel, float angle, float size, float ink)
{
    float2 p = NPFXRotate(pixel, angle) / size;
    float dist = length(frac(p) - 0.5) * 1.414;
    return smoothstep(0.0, 0.15, sqrt(saturate(ink)) - dist);
}

float4 NPFXFragStyle(NPFXVaryings i) : SV_Target
{
    float2 uv = i.uv;
    float2 texel = _NPFX_SourceSize.xy;
    float3 col = _S_Kuwahara.x >= 1.0 ? NPFXKuwahara(uv, _S_Kuwahara.x) : NPFXSample(uv);

    // シャープ (アンシャープマスク)
    if (_S_Kuwahara.z > 0.0001)
    {
        float3 blur = (NPFXSample(uv + float2(texel.x, 0)) + NPFXSample(uv - float2(texel.x, 0))
                     + NPFXSample(uv + float2(0, texel.y)) + NPFXSample(uv - float2(0, texel.y))) * 0.25;
        col = max(col + (col - blur) * _S_Kuwahara.z * 2.0, 0.0);
    }

    // エンボス
    if (_S_Extra.z > 0.0001)
    {
        float e = NPFXLuma(NPFXSample(uv + texel)) - NPFXLuma(NPFXSample(uv - texel));
        col = lerp(col, saturate(0.5 + e * 4.0).xxx, _S_Extra.z);
    }

    // ポスタライズ
    if (_S_Kuwahara.y > 1.0)
    {
        float levels = _S_Kuwahara.y;
        col = floor(saturate(col) * levels + 0.5) / levels;
    }

    // 網点
    if (_S_Halftone.x > 0.0001)
    {
        float2 pixel = uv * _NPFX_ScreenSize.zw;
        float size = max(_S_Halftone.y, 2.0);
        float3 dots;
        if (_S_Extra.y > 0.5)
        {
            // チャンネルごとに角度を変えた網 (印刷の分版風)
            float3 ink = 1.0 - float3(NPFXPerceptual(col.r), NPFXPerceptual(col.g), NPFXPerceptual(col.b));
            float3 d = float3(
                NPFXHalftoneDot(pixel, _S_Halftone.z + 0.26, size, ink.r),
                NPFXHalftoneDot(pixel, _S_Halftone.z + 1.31, size, ink.g),
                NPFXHalftoneDot(pixel, _S_Halftone.z, size, ink.b));
            dots = 1.0 - d;
        }
        else
        {
            float ink = 1.0 - NPFXPerceptual(NPFXLuma(col));
            dots = lerp(_S_Paper.rgb, _S_EdgeColor.rgb, NPFXHalftoneDot(pixel, _S_Halftone.z, size, ink));
        }
        col = lerp(col, dots, _S_Halftone.x);
    }

    // クロスハッチ
    if (_S_Halftone.w > 0.0001)
    {
        float2 pixel = uv * _NPFX_ScreenSize.zw;
        float lum = NPFXPerceptual(NPFXLuma(col));
        float spacing = max(_S_Halftone.y, 3.0);
        float hatch = 1.0;
        float l1 = abs(frac((pixel.x + pixel.y) / spacing) - 0.5) * 2.0;
        float l2 = abs(frac((pixel.x - pixel.y) / spacing) - 0.5) * 2.0;
        float l3 = abs(frac((pixel.x + pixel.y * 0.5) / (spacing * 0.7)) - 0.5) * 2.0;
        if (lum < 0.75) hatch = min(hatch, smoothstep(0.1, 0.3, l1));
        if (lum < 0.5) hatch = min(hatch, smoothstep(0.1, 0.3, l2));
        if (lum < 0.25) hatch = min(hatch, smoothstep(0.1, 0.3, l3));
        col = lerp(col, lerp(_S_EdgeColor.rgb, _S_Paper.rgb, hatch), _S_Halftone.w);
    }

    // 輪郭線 (深度と色の Sobel)
    if (_S_Edge.x > 0.0001)
    {
        float2 o = texel * max(_S_Edge.y, 0.5);
        float edge = 0.0;
        if (_S_Edge.w > 0.0001)
        {
            float tl = NPFXLuma(NPFXSample(uv + float2(-o.x, o.y)));
            float tr = NPFXLuma(NPFXSample(uv + float2(o.x, o.y)));
            float bl = NPFXLuma(NPFXSample(uv + float2(-o.x, -o.y)));
            float br = NPFXLuma(NPFXSample(uv + float2(o.x, -o.y)));
            float l = NPFXLuma(NPFXSample(uv + float2(-o.x, 0)));
            float r = NPFXLuma(NPFXSample(uv + float2(o.x, 0)));
            float t = NPFXLuma(NPFXSample(uv + float2(0, o.y)));
            float b = NPFXLuma(NPFXSample(uv + float2(0, -o.y)));
            float gx = (tr + 2.0 * r + br) - (tl + 2.0 * l + bl);
            float gy = (tl + 2.0 * t + tr) - (bl + 2.0 * b + br);
            edge = max(edge, saturate(sqrt(gx * gx + gy * gy) * _S_Edge.w * 2.0 - 0.2));
        }
        if (_S_Edge.z > 0.0001 && _NPFX_DepthMode > 0.5)
        {
            float d = NPFXEyeDepth(uv);
            float d1 = NPFXEyeDepth(uv + float2(o.x, o.y));
            float d2 = NPFXEyeDepth(uv - float2(o.x, o.y));
            float d3 = NPFXEyeDepth(uv + float2(-o.x, o.y));
            float d4 = NPFXEyeDepth(uv + float2(o.x, -o.y));
            float diff = (abs(d1 - d2) + abs(d3 - d4)) / max(d, 0.01);
            edge = max(edge, saturate(diff * _S_Edge.z * 10.0 - 0.3));
        }
        float3 base = lerp(col, _S_Paper.rgb, _S_Paper.a);
        col = lerp(base, _S_EdgeColor.rgb, saturate(edge * _S_Edge.x));
    }
    else if (_S_Paper.a > 0.0001 && _S_Halftone.x < 0.0001 && _S_Halftone.w < 0.0001)
    {
        col = lerp(col, _S_Paper.rgb, _S_Paper.a);
    }

    // レトロ減色 (ベイヤーディザ)
    if (_S_Kuwahara.w > 0.5)
    {
        float scale = max(_S_Extra.x, 1.0);
        float levels = pow(2.0, _S_Kuwahara.w) - 1.0;
        float threshold = NPFXBayer4(floor(uv * _NPFX_ScreenSize.zw / scale)) - 0.5;
        col = floor(saturate(col) * levels + threshold + 0.5) / levels;
    }
    return float4(col, 1.0);
}

// ============================================================================
// 3-5: ブルームのピラミッド
// ============================================================================
float4 _B_Threshold;  // x しきい値, y ひざ, z 上限, w 未使用

float4 NPFXFragPrefilter(NPFXVaryings i) : SV_Target
{
    float2 t = _NPFX_SourceSize.xy;
    float3 c = (NPFXSample(i.uv + float2(-t.x, -t.y)) + NPFXSample(i.uv + float2(t.x, -t.y))
              + NPFXSample(i.uv + float2(-t.x, t.y)) + NPFXSample(i.uv + float2(t.x, t.y))) * 0.25;
    c = min(c, _B_Threshold.z);
    float br = max(c.r, max(c.g, c.b));
    float knee = max(_B_Threshold.x * _B_Threshold.y, 1e-4);
    float soft = clamp(br - _B_Threshold.x + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee);
    float w = max(soft, br - _B_Threshold.x) / max(br, 1e-4);
    return float4(c * w, 1.0);
}

float4 NPFXFragDown(NPFXVaryings i) : SV_Target
{
    float2 t = _NPFX_SourceSize.xy;
    float3 c = NPFXSample(i.uv) * 4.0;
    c += NPFXSample(i.uv + float2(-t.x, -t.y));
    c += NPFXSample(i.uv + float2(t.x, -t.y));
    c += NPFXSample(i.uv + float2(-t.x, t.y));
    c += NPFXSample(i.uv + float2(t.x, t.y));
    return float4(c * 0.125, 1.0);
}

// _NPFX_Source = ひとつ粗い段, _NPFX_Aux = 同じ段のダウンサンプル。x = 広がり。
float4 NPFXFragUp(NPFXVaryings i) : SV_Target
{
    float2 t = _NPFX_SourceSize.xy * max(_NPFX_StepParams.x, 0.25);
    float3 c = NPFXSample(i.uv) * 4.0;
    c += (NPFXSample(i.uv + float2(t.x, 0)) + NPFXSample(i.uv - float2(t.x, 0))
        + NPFXSample(i.uv + float2(0, t.y)) + NPFXSample(i.uv - float2(0, t.y))) * 2.0;
    c += NPFXSample(i.uv + float2(t.x, t.y)) + NPFXSample(i.uv + float2(-t.x, t.y))
       + NPFXSample(i.uv + float2(t.x, -t.y)) + NPFXSample(i.uv + float2(-t.x, -t.y));
    c *= 1.0 / 16.0;
    return float4(c + tex2Dlod(_NPFX_Aux, float4(i.uv, 0, 0)).rgb, 1.0);
}

// ============================================================================
// 6: 分離ガウスぼかし (xy = 1 タップ分のずれ, z = 減衰。光条では z で尾を伸ばす)
// ============================================================================
float4 NPFXFragBlur(NPFXVaryings i) : SV_Target
{
    float2 d = _NPFX_StepParams.xy * _NPFX_SourceSize.xy;
    static const float w[5] = { 0.2270270, 0.1945946, 0.1216216, 0.0540541, 0.0162162 };
    float4 c = NPFXSample4(i.uv) * w[0];
    for (int k = 1; k < 5; k++)
    {
        c += NPFXSample4(i.uv + d * k) * w[k];
        c += NPFXSample4(i.uv - d * k) * w[k];
    }
    return c * max(_NPFX_StepParams.z, 1.0);
}

// ============================================================================
// 7: 放射ぼかし (光芒)。xy = 中心, z = 長さ, w = 減衰
// ============================================================================
float4 NPFXFragRadial(NPFXVaryings i) : SV_Target
{
    const int kTaps = 16;
    float2 delta = (_NPFX_StepParams.xy - i.uv) * _NPFX_StepParams.z / kTaps;
    float2 uv = i.uv;
    float3 sum = 0;
    float weight = 1.0;
    float total = 0.0;
    for (int k = 0; k < kTaps; k++)
    {
        sum += NPFXSample(uv) * weight;
        total += weight;
        weight *= _NPFX_StepParams.w;
        uv += delta;
    }
    return float4(sum / max(total, 1e-4), 1.0);
}

// ============================================================================
// 8: 被写界深度 (円形ボケ)。a に錯乱円の大きさを入れる。
// ============================================================================
float4 _F_Dof;   // x 有効, y ピント距離, z ピントの合う幅, w ボケの最大半径 (ピクセル)
float4 _F_Tilt;  // x チルトシフトの強さ, y 中心の高さ, z 帯の幅, w 画面全体のぼかし

float NPFXCoc(float2 uv)
{
    float coc = 0.0;
    if (_F_Dof.x > 0.5)
    {
        float depth = NPFXEyeDepth(uv);
        coc = saturate((abs(depth - _F_Dof.y) - _F_Dof.z * 0.5) / max(_F_Dof.z, 0.01));
    }
    if (_F_Tilt.x > 0.0001)
    {
        float band = saturate((abs(uv.y - _F_Tilt.y) - _F_Tilt.z * 0.5) / max(_F_Tilt.z, 0.01));
        coc = max(coc, band * _F_Tilt.x);
    }
    return coc;
}

float4 NPFXFragDof(NPFXVaryings i) : SV_Target
{
    float coc = NPFXCoc(i.uv);
    float radius = coc * _F_Dof.w;
    float3 sum = NPFXSample(i.uv);
    float total = 1.0;
    if (radius > 0.5)
    {
        const int kTaps = 28;
        const float golden = 2.39996323;
        float jitter = NPFXHash(i.uv * _NPFX_ScreenSize.zw) * UNITY_TWO_PI;
        for (int k = 0; k < kTaps; k++)
        {
            float r = sqrt((k + 0.5) / kTaps);
            float a = k * golden + jitter;
            float2 uv = i.uv + float2(cos(a), sin(a)) * r * radius * _NPFX_ScreenSize.xy;
            // 手前のピントが合った物体がボケに滲まないよう、ピントの合ったタップは重みを落とす。
            float w = lerp(0.1, 1.0, NPFXCoc(uv));
            float3 c = NPFXSample(uv);
            // 明るい点を強めて玉ボケを出す。
            w *= 1.0 + NPFXLuma(c) * 2.0;
            sum += c * w;
            total += w;
        }
    }
    return float4(sum / total, coc);
}

// ============================================================================
// 9: 仕上げ (合成・色・画面の飾り)
// ============================================================================
float4 _F_Bloom;        // x ブルーム, y 光条, z 光芒, w ディフュージョン
float4 _F_BloomTint;
float4 _F_StreakTint;
float4 _F_ShaftTint;
float4 _F_Fog;          // x 濃さ, y 開始距離, z 空にもかける, w 未使用
float4 _F_FogColor;
float4 _F_Mask;         // x 背景の彩度落とし, y 背景を暗く, z 背景ぼかし, w 縁取りの太さ
float4 _F_Mask2;        // x 縁取りの強さ, y シルエット塗り, z マスク有効, w 縁取りのぼけ
float4 _F_MaskOutlineColor;
float4 _F_MaskFillColor;
float4 _F_Grade;        // x 露出, y コントラスト, z 彩度, w 色相
float4 _F_Grade2;       // x 色温度, y 色かぶり, z 自然な彩度, w トーンマップ (0 なし 1 ACES 2 Neutral 3 Reinhard)
float4 _F_Lift;
float4 _F_Gamma;
float4 _F_Gain;
float4 _F_SplitShadow;  // rgb 色, a 強さ
float4 _F_SplitHigh;
float4 _F_Lut;          // x 効き, y LUT の一辺 (32 など)
float4 _F_Filter;       // x モノクロ, y セピア, z ネガ反転, w ソラリゼーション
float4 _F_Filter2;      // x 2 値化, y 2 値化のしきい値, z 暗視, w サーモグラフィ
float4 _F_Duo;          // x グラデーションマップ
float4 _F_DuoA;
float4 _F_DuoB;
float4 _F_Overlay;      // rgb 色, a 強さ
float4 _F_Overlay2;     // x 合成 (0 通常 1 乗算 2 スクリーン 3 オーバーレイ 4 加算), y 上下グラデーション, z 角度, w 光漏れ
float4 _F_GradTop;
float4 _F_GradBottom;
float4 _F_LeakColor;
float4 _F_OverlayTexParams; // x テクスチャの強さ, y 合成, zw スクロール
float4 _F_Flash;        // rgb 色, a 強さ
float4 _F_Fade;         // rgb 色, a 強さ
float4 _F_Vignette;     // x 強さ, y なめらかさ, z 丸さ
float4 _F_VignetteColor;
float4 _F_Lines;        // x 集中線の強さ, y 本数, z 内側の半径, w シード
float4 _F_LinesColor;
float4 _F_Center;       // xy 集中線・光芒の中心
float4 _F_Crt;          // x 走査線, y 走査線の本数, z 走査線の位相, w RGB マスク
float4 _F_Film;         // x 粒子, y 粒子の大きさ, z 縦傷, w ちらつき
float4 _F_Film2;        // x ほこり, y レターボックスの縦横比, z 枠の丸み (未使用), w ノイズ (砂嵐)

float3 NPFXBlend(float3 base, float3 over, float amount, float mode)
{
    float3 r;
    if (mode < 0.5) r = over;
    else if (mode < 1.5) r = base * over;
    else if (mode < 2.5) r = 1.0 - (1.0 - saturate(base)) * (1.0 - saturate(over));
    else if (mode < 3.5)
    {
        float3 b = saturate(base);
        r = lerp(2.0 * b * over, 1.0 - 2.0 * (1.0 - b) * (1.0 - over), step(0.5, b));
    }
    else r = base + over;
    return lerp(base, r, amount);
}

float3 NPFXTonemap(float3 c, float mode)
{
    if (mode < 0.5) return c;
    c = max(c, 0.0);
    if (mode < 1.5)
    {
        // ACES の近似 (Narkowicz 2015)
        return saturate((c * (2.51 * c + 0.03)) / (c * (2.43 * c + 0.59) + 0.14));
    }
    if (mode < 2.5)
    {
        // 色を寝かせない控えめなカーブ (Hable の係数)
        const float A = 0.15, B = 0.50, C = 0.10, D = 0.20, E = 0.02, F = 0.30, W = 11.2;
        float3 x = c * 2.0;
        float3 mapped = ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
        float white = ((W * (A * W + C * B) + D * E) / (W * (A * W + B) + D * F)) - E / F;
        return saturate(mapped / white);
    }
    return c / (1.0 + c);
}

float3 NPFXApplyLut(float3 c, float size)
{
    // 横長のストリップ LUT (size*size x size)
    c = saturate(c);
    float slice = c.b * (size - 1.0);
    float s0 = floor(slice);
    float s1 = min(s0 + 1.0, size - 1.0);
    float2 uv0 = float2((s0 + (c.r * (size - 1.0) + 0.5) / size) / size, (c.g * (size - 1.0) + 0.5) / size);
    float2 uv1 = float2((s1 + (c.r * (size - 1.0) + 0.5) / size) / size, uv0.y);
    float3 a = tex2Dlod(_NPFX_LutTex, float4(uv0, 0, 0)).rgb;
    float3 b = tex2Dlod(_NPFX_LutTex, float4(uv1, 0, 0)).rgb;
    return lerp(a, b, slice - s0);
}

float3 NPFXThermal(float t)
{
    t = saturate(t);
    float3 c = lerp(float3(0.0, 0.0, 0.3), float3(0.5, 0.0, 0.6), saturate(t * 4.0));
    c = lerp(c, float3(1.0, 0.1, 0.1), saturate(t * 4.0 - 1.0));
    c = lerp(c, float3(1.0, 0.9, 0.1), saturate(t * 4.0 - 2.0));
    c = lerp(c, float3(1.0, 1.0, 1.0), saturate(t * 4.0 - 3.0));
    return c;
}

float4 NPFXFragFinal(NPFXVaryings i) : SV_Target
{
    float2 uv = i.uv;
    float aspect = _NPFX_ScreenSize.z * _NPFX_ScreenSize.y;
    float3 col = NPFXSample(uv);
    float3 blurred = tex2Dlod(_NPFX_BlurTex, float4(uv, 0, 0)).rgb;

    // 被写界深度・チルトシフト
    if (_F_Dof.x > 0.5 || _F_Tilt.x > 0.0001)
    {
        float4 dof = tex2Dlod(_NPFX_DofTex, float4(uv, 0, 0));
        col = lerp(col, dof.rgb, smoothstep(0.0, 0.35, dof.a));
    }
    // 画面全体のぼかし
    col = lerp(col, blurred, saturate(_F_Tilt.w));

    // キャラクターマスク (指定したオブジェクトだけ / それ以外だけに効かせる)
    if (_F_Mask2.z > 0.5)
    {
        float sceneDepth = NPFXEyeDepth(uv);
        float maskDepth = tex2Dlod(_NPFX_MaskTex, float4(uv, 0, 0)).r;
        float inside = step(maskDepth, sceneDepth * 1.02 + 0.05) * step(maskDepth, 9000.0);
        float bg = 1.0 - inside;

        col = lerp(col, blurred, bg * saturate(_F_Mask.z));
        col = lerp(col, NPFXLuma(col).xxx, bg * saturate(_F_Mask.x));
        col *= 1.0 - bg * saturate(_F_Mask.y);
        col = lerp(col, _F_MaskFillColor.rgb, inside * _F_Mask2.y * _F_MaskFillColor.a);

        if (_F_Mask2.x > 0.0001 && _F_Mask.w > 0.01)
        {
            // マスクを周囲 8 + 8 方向に広げ、マスクの外側だけに縁を描く。
            float2 px = _NPFX_ScreenSize.xy * _F_Mask.w;
            float spread = 0.0;
            for (int k = 0; k < 16; k++)
            {
                float a = k * (UNITY_TWO_PI / 16.0);
                float r = (k & 1) ? 1.0 : 0.55;
                float2 o = float2(cos(a), sin(a)) * px * r;
                float md = tex2Dlod(_NPFX_MaskTex, float4(uv + o, 0, 0)).r;
                float vis = step(md, NPFXEyeDepth(uv + o) * 1.02 + 0.05) * step(md, 9000.0);
                spread = max(spread, vis * lerp(1.0, 1.0 - r * 0.6, _F_Mask2.w));
            }
            float rim = spread * bg;
            col = lerp(col, _F_MaskOutlineColor.rgb, saturate(rim * _F_Mask2.x) * _F_MaskOutlineColor.a);
        }
    }

    // 距離フォグ
    if (_F_Fog.x > 0.00001 && _NPFX_DepthMode > 0.5)
    {
        float depth = NPFXEyeDepth(uv);
        float sky = step(_ProjectionParams.z * 0.95, depth);
        float f = 1.0 - exp(-max(depth - _F_Fog.y, 0.0) * _F_Fog.x);
        f *= lerp(1.0 - sky, 1.0, _F_Fog.z);
        col = lerp(col, _F_FogColor.rgb, saturate(f) * _F_FogColor.a);
    }

    // 光の足し込み
    col += tex2Dlod(_NPFX_BloomTex, float4(uv, 0, 0)).rgb * _F_Bloom.x * _F_BloomTint.rgb;
    col += tex2Dlod(_NPFX_StreakTex, float4(uv, 0, 0)).rgb * _F_Bloom.y * _F_StreakTint.rgb;
    col += tex2Dlod(_NPFX_ShaftTex, float4(uv, 0, 0)).rgb * _F_Bloom.z * _F_ShaftTint.rgb;

    // ディフュージョン (ぼかした絵をスクリーンで重ねる、アニメ撮影の定番)
    if (_F_Bloom.w > 0.0001)
    {
        float3 soft = saturate(blurred);
        float3 screen = 1.0 - (1.0 - saturate(col)) * (1.0 - soft * soft);
        col = lerp(col, max(col, screen), _F_Bloom.w);
    }

    // ---- カラーグレーディング ----
    col *= _F_Grade.x;
    col *= float3(1.0 + _F_Grade2.x * 0.2, 1.0 - _F_Grade2.y * 0.1, 1.0 - _F_Grade2.x * 0.2)
         * float3(1.0 + _F_Grade2.y * 0.1, 1.0, 1.0 + _F_Grade2.y * 0.1);
    col = NPFXTonemap(col, _F_Grade2.w);

    col = max(col * _F_Gain.rgb + _F_Lift.rgb * (1.0 - col), 0.0);
    col = pow(col, 1.0 / max(_F_Gamma.rgb, 0.01));
    col = (col - 0.5) * _F_Grade.y + 0.5;
    col = max(col, 0.0);

    float luma = NPFXLuma(col);
    if (_F_SplitShadow.a + _F_SplitHigh.a > 0.0001)
    {
        float t = saturate(luma);
        col = lerp(col, col * _F_SplitShadow.rgb * 2.0, (1.0 - t) * _F_SplitShadow.a);
        col = lerp(col, col + (_F_SplitHigh.rgb - 0.5) * 0.5, t * _F_SplitHigh.a);
        col = max(col, 0.0);
        luma = NPFXLuma(col);
    }
    if (abs(_F_Grade2.z) > 0.0001)
    {
        float sat = max(col.r, max(col.g, col.b)) - min(col.r, min(col.g, col.b));
        col = lerp(luma.xxx, col, 1.0 + _F_Grade2.z * (1.0 - saturate(sat)));
    }
    col = lerp(luma.xxx, col, _F_Grade.z);
    if (abs(_F_Grade.w) > 0.0001)
    {
        float3 hsv = NPFXRgbToHsv(max(col, 0.0));
        hsv.x = frac(hsv.x + _F_Grade.w);
        col = NPFXHsvToRgb(hsv);
    }
    col = max(col, 0.0);

    if (_F_Lut.x > 0.0001)
        col = lerp(col, NPFXApplyLut(col, max(_F_Lut.y, 2.0)), _F_Lut.x);

    // ---- 色のフィルター ----
    luma = NPFXLuma(col);
    col = lerp(col, luma.xxx, _F_Filter.x);
    col = lerp(col, luma * float3(1.07, 0.86, 0.63), _F_Filter.y);
    if (_F_Duo.x > 0.0001) col = lerp(col, lerp(_F_DuoA.rgb, _F_DuoB.rgb, saturate(luma)), _F_Duo.x);
    if (_F_Filter.w > 0.0001)
    {
        float3 s = saturate(col);
        col = lerp(col, lerp(s, 1.0 - s, step(0.5, s)) * 2.0, _F_Filter.w);
    }
    col = lerp(col, 1.0 - saturate(col), _F_Filter.z);
    if (_F_Filter2.x > 0.0001)
    {
        float bw = smoothstep(_F_Filter2.y - 0.01, _F_Filter2.y + 0.01, NPFXPerceptual(NPFXLuma(col)));
        col = lerp(col, bw.xxx, _F_Filter2.x);
    }
    if (_F_Filter2.z > 0.0001)
    {
        float nv = saturate(NPFXLuma(col) * 2.5);
        float n = NPFXHash(uv * _NPFX_ScreenSize.zw + _NPFX_Time * 60.0);
        col = lerp(col, float3(0.1, 1.0, 0.2) * (nv + (n - 0.5) * 0.15), _F_Filter2.z);
    }
    if (_F_Filter2.w > 0.0001) col = lerp(col, NPFXThermal(NPFXLuma(col)), _F_Filter2.w);

    // ---- 重ねもの ----
    if (_F_Overlay.a > 0.0001) col = NPFXBlend(col, _F_Overlay.rgb, _F_Overlay.a, _F_Overlay2.x);
    if (_F_Overlay2.y > 0.0001)
    {
        float2 dir = float2(sin(_F_Overlay2.z), cos(_F_Overlay2.z));
        float t = saturate(dot(uv - 0.5, dir) + 0.5);
        col = NPFXBlend(col, lerp(_F_GradBottom.rgb, _F_GradTop.rgb, t), _F_Overlay2.y, _F_Overlay2.x);
    }
    if (_F_OverlayTexParams.x > 0.0001)
    {
        float4 tex = tex2Dlod(_NPFX_OverlayTex, float4(uv + _F_OverlayTexParams.zw * _NPFX_Time, 0, 0));
        col = NPFXBlend(col, tex.rgb, _F_OverlayTexParams.x * tex.a, _F_OverlayTexParams.y);
    }
    if (_F_Overlay2.w > 0.0001)
    {
        // 光漏れ: ゆっくり動く暖色のむら
        float t = _NPFX_Time * 0.25;
        float a = NPFXNoise(float2(uv.x * 1.5 + t, uv.y * 0.6 - t * 0.7));
        float b = NPFXNoise(float2(uv.x * 0.8 - t * 0.5, uv.y * 1.2 + t * 0.3) + 13.7);
        float leak = pow(saturate(a * b * 2.2), 2.0) * saturate(1.3 - uv.x * 0.6);
        float3 leakCol = lerp(_F_LeakColor.rgb, _F_LeakColor.gbr * float3(1.0, 0.5, 0.3) + float3(0.5, 0.2, 0.0), b * 0.5);
        col = 1.0 - (1.0 - saturate(col)) * (1.0 - saturate(leakCol * leak * _F_Overlay2.w * 1.5));
    }

    // 集中線
    if (_F_Lines.x > 0.0001)
    {
        float2 p = uv - _F_Center.xy;
        p.x *= aspect;
        float ang = atan2(p.y, p.x) / UNITY_TWO_PI + 0.5;
        float density = max(_F_Lines.y, 4.0);
        float cell = floor(ang * density);
        float rnd = NPFXHash(float2(cell, _F_Lines.w));
        float width = lerp(0.15, 0.5, NPFXHash(float2(cell, _F_Lines.w + 3.0)));
        float streak = 1.0 - smoothstep(width * 0.6, width, abs(frac(ang * density) - 0.5) * 2.0);
        float start = _F_Lines.z * lerp(0.7, 1.3, rnd);
        float radial = smoothstep(start, start + 0.35, length(p));
        col = lerp(col, _F_LinesColor.rgb, saturate(streak * radial * step(0.35, rnd) * _F_Lines.x) * _F_LinesColor.a);
    }

    // ビネット
    if (_F_Vignette.x > 0.0001)
    {
        float2 d = abs(uv - 0.5) * 2.0;
        d.x *= lerp(1.0, aspect, _F_Vignette.z);
        float v = pow(saturate(1.0 - dot(d, d) * _F_Vignette.x * 0.5), 1.0 / max(_F_Vignette.y, 0.01) * 0.5 + 0.5);
        col = lerp(_F_VignetteColor.rgb, col, lerp(1.0, v, _F_VignetteColor.a));
    }

    // 走査線・RGB マスク
    float2 pixel = uv * _NPFX_ScreenSize.zw;
    if (_F_Crt.x > 0.0001)
    {
        float s = sin((uv.y * _F_Crt.y + _F_Crt.z) * UNITY_PI) * 0.5 + 0.5;
        col *= 1.0 - _F_Crt.x * (1.0 - s * s);
    }
    if (_F_Crt.w > 0.0001)
    {
        int m = (int)fmod(pixel.x, 3.0);
        float3 mask = float3(m == 0 ? 1.0 : 0.55, m == 1 ? 1.0 : 0.55, m == 2 ? 1.0 : 0.55);
        col *= lerp(float3(1, 1, 1), mask * 1.35, _F_Crt.w);
    }

    // フィルム
    if (_F_Film.w > 0.0001)
        col *= 1.0 + (NPFXHash(float2(floor(_NPFX_Time * 24.0), 0.37)) - 0.5) * _F_Film.w * 0.4;
    if (_F_Film.z > 0.0001)
    {
        float frame = floor(_NPFX_Time * 16.0);
        float x = NPFXHash(float2(frame, 1.3));
        float scratch = 1.0 - smoothstep(0.0, 0.0015, abs(uv.x - x + sin(uv.y * 30.0 + frame) * 0.001));
        float on = step(1.0 - _F_Film.z * 0.6, NPFXHash(float2(frame, 7.7)));
        col = lerp(col, float3(0.9, 0.9, 0.85), scratch * on * 0.5);
    }
    if (_F_Film2.x > 0.0001)
    {
        float frame = floor(_NPFX_Time * 12.0);
        float2 cell = floor(uv * float2(aspect, 1.0) * 14.0);
        float2 local = frac(uv * float2(aspect, 1.0) * 14.0) - 0.5;
        float rnd = NPFXHash(cell + frame * 1.7);
        float2 off = float2(NPFXHash(cell + frame + 2.0), NPFXHash(cell + frame + 5.0)) - 0.5;
        float speck = (1.0 - smoothstep(0.02, 0.06, length(local - off * 0.6))) * step(1.0 - _F_Film2.x * 0.15, rnd);
        col *= 1.0 - speck * 0.8;
    }
    if (_F_Film.x > 0.0001)
    {
        float2 g = floor(pixel / max(_F_Film.y, 1.0));
        float n = NPFXHash(g + frac(_NPFX_Time * 7.13) * 100.0) - 0.5;
        float response = lerp(1.0, 0.3, saturate(NPFXLuma(col)));
        col += n * _F_Film.x * 0.35 * response;
    }
    if (_F_Film2.w > 0.0001)
    {
        float n = NPFXHash(floor(pixel * 0.5) + frac(_NPFX_Time * 3.7) * 200.0);
        col = lerp(col, n.xxx, _F_Film2.w);
    }

    // フラッシュ・フェード
    col = lerp(col, col + _F_Flash.rgb, _F_Flash.a);
    col = lerp(col, _F_Fade.rgb, _F_Fade.a);

    // レターボックス
    if (_F_Film2.y > 0.01)
    {
        if (_F_Film2.y > aspect) col *= step(abs(uv.y - 0.5) * 2.0, aspect / _F_Film2.y);
        else col *= step(abs(uv.x - 0.5) * 2.0, _F_Film2.y / aspect);
    }
    return float4(max(col, 0.0), 1.0);
}

// ============================================================================
// 10: 時間方向 (残像・コマ落ち)。x = 前のフレームを残す割合
// ============================================================================
float4 _T_Params;

float4 NPFXFragTemporal(NPFXVaryings i) : SV_Target
{
    float3 cur = NPFXSample(i.uv);
    float3 hist = tex2Dlod(_NPFX_HistoryTex, float4(i.uv, 0, 0)).rgb;
    float3 mixed = lerp(cur, hist, saturate(_T_Params.x));
    // y > 0 なら明るい方を残す (光の尾を引く残像)
    mixed = lerp(mixed, max(cur, hist * saturate(_T_Params.x)), saturate(_T_Params.y));
    return float4(mixed, 1.0);
}

#endif // NATANE_POSTFX_CORE_INCLUDED
