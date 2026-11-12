#ifndef NATANE_SCREEN_FX_CORE_INCLUDED
#define NATANE_SCREEN_FX_CORE_INCLUDED

// Screen FX の効果チェーン。Built-in RP (GrabPass) と URP (全画面パス) で共有する。見た目の変更はここで行う。
// include する側が先に用意するもの:
//   NATANE_SCREENFX_SAMPLE_SCREEN(uv)     画面の色 (half3)
//   NATANE_SCREENFX_SAMPLE_GRADATION(uv)  グラデーションランプ (half3)
//   _GradationTexture の宣言

half _Intensity;
half4 _TintColor;
half _Contrast;
half _Saturation;

half _PosterizeStrength;
half _PosterizeSteps;
half _EdgeStrength;
half _EdgeThreshold;

half _ChromaticAberration;
half _AberrationScale;
half _AberrationEdgeOnly;
half _AberrationEdgeStart;

half _RadialBlurStrength;
float4 _RadialBlurCenter;
half _RadialBlurSamples;
half _RadialBlurEdgeOnly;

half4 _GradationColorA;
half4 _GradationColorB;
half _GradationBlend;
half _GradationMode;
half _GradationAngle;

half _MonochromeStrength;
half _MonochromeEdgeOnly;

half _Vignette;
half _VignetteSoftness;
half _ScanlineStrength;
half _ScanlineDensity;
half _ScanlineSpeed;
half _GrainStrength;
half _GrainScale;
half _GrainSpeed;

inline half NataneLuminance(half3 color)
{
    return dot(color, half3(0.299h, 0.587h, 0.114h));
}

inline half Hash12(float2 p)
{
    half h = dot(p, float2(127.1, 311.7));
    return frac(sin(h) * 43758.5453);
}

// texel: 1 ピクセルぶんの UV。baseColor: screenUV の元の色。戻り値は _Intensity で混ぜる前の色。
half3 NataneScreenFXApply(float2 screenUV, float2 texel, half3 baseColor)
{
    half3 fxColor = baseColor;

    // 半径と周辺の重み。色収差 / 放射ブラー / ビネット / モノクロで「周辺」の範囲を揃える。
    float2 centeredUV = screenUV * 2.0 - 1.0;
    half radius = saturate(length(centeredUV));
    half edgeWeight = smoothstep(_AberrationEdgeStart, 1.0h, radius);

    // 放射ブラー。後段がブラー後の色に作用するよう、最初にかける。
    if (_RadialBlurStrength > 0.001h)
    {
        half rbWeight = lerp(1.0h, edgeWeight, _RadialBlurEdgeOnly);
        float2 dir = screenUV - _RadialBlurCenter.xy;
        half3 acc = fxColor;
        int count = (int)min(_RadialBlurSamples, 8.0h);

        [loop] for (int s = 1; s <= count; ++s)
        {
            half t = (half)s / (half)count;
            acc += NATANE_SCREENFX_SAMPLE_SCREEN(screenUV - dir * t * _RadialBlurStrength * 0.15);
        }

        acc /= (half)(count + 1);
        fxColor = lerp(fxColor, acc, rbWeight);
    }

    // 色収差。_AberrationEdgeOnly = 1 で周辺だけにかかる (視線を中央へ誘導する)。
    half caAmount = _ChromaticAberration * lerp(1.0h, edgeWeight, _AberrationEdgeOnly);
    float2 aberrationOffset = texel * (caAmount * _AberrationScale * 2.0);
    half3 chromaColor;
    chromaColor.r = NATANE_SCREENFX_SAMPLE_SCREEN(screenUV + aberrationOffset).r;
    // baseColor.g ではなく fxColor.g (緑だけ放射ブラーが元に戻るのを防ぐ)。
    chromaColor.g = fxColor.g;
    chromaColor.b = NATANE_SCREENFX_SAMPLE_SCREEN(screenUV - aberrationOffset).b;
    fxColor = lerp(fxColor, chromaColor, saturate(caAmount));

    // 輝度差のあるエッジを暗くする。
    half centerLum = NataneLuminance(baseColor);
    half rightLum = NataneLuminance(NATANE_SCREENFX_SAMPLE_SCREEN(screenUV + float2(texel.x, 0.0)));
    half leftLum = NataneLuminance(NATANE_SCREENFX_SAMPLE_SCREEN(screenUV - float2(texel.x, 0.0)));
    half upLum = NataneLuminance(NATANE_SCREENFX_SAMPLE_SCREEN(screenUV + float2(0.0, texel.y)));
    half downLum = NataneLuminance(NATANE_SCREENFX_SAMPLE_SCREEN(screenUV - float2(0.0, texel.y)));
    half edge = abs(centerLum - rightLum) + abs(centerLum - leftLum) + abs(centerLum - upLum) + abs(centerLum - downLum);
    edge = saturate((edge - _EdgeThreshold) * 6.0);
    fxColor *= (1.0h - edge * saturate(_EdgeStrength));

    // ポスタライズ
    half steps = max(_PosterizeSteps, 2.0h);
    half scale = steps - 1.0h;
    half3 posterized = floor(fxColor * scale + 0.5h) / scale;
    fxColor = lerp(fxColor, posterized, saturate(_PosterizeStrength));

    // スキャンライン
    half scanWave = sin((screenUV.y * _ScanlineDensity + _Time.y * _ScanlineSpeed) * 6.2831853h) * 0.5h + 0.5h;
    fxColor *= (1.0h - _ScanlineStrength * (1.0h - scanWave));

    // フィルムグレイン
    float2 grainUV = floor(screenUV * _GrainScale) + _Time.y * _GrainSpeed;
    half grain = Hash12(grainUV) - 0.5h;
    fxColor += grain * _GrainStrength;

    // ビネット
    half vignetteStart = saturate(1.0h - _VignetteSoftness);
    half vignette = smoothstep(vignetteStart, 1.0h, radius);
    fxColor *= (1.0h - vignette * _Vignette);

    // グラデーション (画面に色のランプを重ねる)。
    if (_GradationBlend > 0.001h)
    {
        float a = radians(_GradationAngle);
        float s, c;
        sincos(a, s, c);
        half g = saturate(dot(centeredUV, float2(s, c)) * 0.5 + 0.5);

        half3 grad = lerp(_GradationColorB.rgb, _GradationColorA.rgb, g);
        // テクスチャ未指定なら "white" なので、2色補間がそのまま残る。
        grad *= NATANE_SCREENFX_SAMPLE_GRADATION(float2(g, 0.5));

        int mode = (int)(_GradationMode + 0.5h);
        half3 blended =
            (mode == 0) ? fxColor * grad :
            (mode == 1) ? 1.0h - (1.0h - fxColor) * (1.0h - grad) :
            (mode == 2) ? lerp(2.0h * fxColor * grad,
                               1.0h - 2.0h * (1.0h - fxColor) * (1.0h - grad),
                               step(0.5h, NataneLuminance(fxColor)))
                        : fxColor + grad;

        fxColor = lerp(fxColor, blended, _GradationBlend);
    }

    // カラーグレーディング
    fxColor = (fxColor - 0.5h) * _Contrast + 0.5h;
    half gray = NataneLuminance(fxColor);
    fxColor = lerp(gray.xxx, fxColor, _Saturation);
    fxColor *= _TintColor.rgb;

    // 部分モノクロ。_Saturation ではできない「周辺だけ色を抜く」ために、半径で重み付けする。
    if (_MonochromeStrength > 0.001h)
    {
        half mWeight = _MonochromeStrength * lerp(1.0h, edgeWeight, _MonochromeEdgeOnly);
        fxColor = lerp(fxColor, NataneLuminance(fxColor).xxx, mWeight);
    }

    fxColor = saturate(fxColor);

    return fxColor;
}

#endif // NATANE_SCREEN_FX_CORE_INCLUDED
