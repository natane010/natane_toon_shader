using System;
using UnityEngine;

namespace NataneToon
{
    /// <summary>インスペクタ用のラベル (日本語 / 英語) とスライダーの範囲。描画は Editor 側の NPFXDrawer。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NPFXAttribute : PropertyAttribute
    {
        public readonly string ja;
        public readonly string en;
        public readonly float min;
        public readonly float max;
        public readonly bool hasRange;

        public NPFXAttribute(string ja, string en)
        {
            this.ja = ja;
            this.en = en;
        }

        public NPFXAttribute(string ja, string en, float min, float max) : this(ja, en)
        {
            this.min = min;
            this.max = max;
            hasRange = true;
        }
    }

    public enum NPFXMirror { Off = 0, Horizontal = 1, Vertical = 2, Quad = 3 }
    public enum NPFXTonemap { None = 0, ACES = 1, Neutral = 2, Reinhard = 3 }
    public enum NPFXBlend { Normal = 0, Multiply = 1, Screen = 2, Overlay = 3, Add = 4 }

    [Serializable]
    public sealed class NPFXLens
    {
        [NPFX("レンズ歪曲 (＋樽 / －糸巻き)", "Lens Distortion", -1f, 1f)] public float distortion;
        [NPFX("色収差", "Chromatic Aberration", 0f, 1f)] public float chromaticAberration;
        [NPFX("RGB ずらし", "RGB Shift", 0f, 1f)] public float rgbShift;
        [NPFX("RGB ずらしの角度", "RGB Shift Angle", 0f, 360f)] public float rgbShiftAngle;
        [NPFX("放射ブラー (ズームブラー)", "Radial Blur", 0f, 1f)] public float radialBlur;
        [NPFX("放射ブラーの中心", "Radial Blur Center")] public Vector2 radialCenter = new Vector2(0.5f, 0.5f);
        [NPFX("方向ブラー", "Directional Blur", 0f, 1f)] public float directionalBlur;
        [NPFX("方向ブラーの角度", "Directional Blur Angle", 0f, 360f)] public float directionalAngle;
        [NPFX("ズーム", "Zoom", 0.25f, 4f)] public float zoom = 1f;
        [NPFX("回転", "Rotation", -180f, 180f)] public float rotation;
        [NPFX("位置ずらし", "Offset")] public Vector2 offset;
        [NPFX("画面揺れ", "Shake", 0f, 1f)] public float shake;
        [NPFX("画面揺れの速さ", "Shake Speed", 1f, 60f)] public float shakeSpeed = 20f;
        [NPFX("ミラー", "Mirror")] public NPFXMirror mirror;
        [NPFX("万華鏡の分割数 (0 でオフ)", "Kaleidoscope Segments", 0f, 24f)] public float kaleidoscope;
        [NPFX("万華鏡の回転", "Kaleidoscope Rotation", 0f, 360f)] public float kaleidoscopeRotation;
        [NPFX("タイル 横", "Tile X", 1f, 8f)] public float tileX = 1f;
        [NPFX("タイル 縦", "Tile Y", 1f, 8f)] public float tileY = 1f;
        [NPFX("波ゆがみ", "Wave", 0f, 1f)] public float wave;
        [NPFX("波の細かさ", "Wave Frequency", 1f, 100f)] public float waveFrequency = 20f;
        [NPFX("波の速さ", "Wave Speed", -10f, 10f)] public float waveSpeed = 2f;
        [NPFX("陽炎", "Heat Haze", 0f, 1f)] public float heatHaze;
        [NPFX("波紋・衝撃波の強さ", "Ripple", -1f, 1f)] public float ripple;
        [NPFX("波紋の中心", "Ripple Center")] public Vector2 rippleCenter = new Vector2(0.5f, 0.5f);
        [NPFX("波紋の半径 (アニメーションさせる)", "Ripple Radius", 0f, 1.5f)] public float rippleRadius = 0.3f;
        [NPFX("波紋の幅", "Ripple Width", 0.01f, 0.5f)] public float rippleWidth = 0.1f;
        [NPFX("モザイク (ピクセル)", "Pixelate", 0f, 64f)] public float pixelate;
        [NPFX("ブラウン管の湾曲", "CRT Curvature", 0f, 1f)] public float crtCurvature;

        public bool Active =>
            Mathf.Abs(distortion) > 1e-4f || chromaticAberration > 1e-4f || rgbShift > 1e-4f || radialBlur > 1e-4f ||
            directionalBlur > 1e-4f || Mathf.Abs(zoom - 1f) > 1e-4f || Mathf.Abs(rotation) > 1e-4f || offset.sqrMagnitude > 1e-8f ||
            shake > 1e-4f || mirror != NPFXMirror.Off || kaleidoscope >= 2f || tileX > 1.001f || tileY > 1.001f ||
            wave > 1e-4f || heatHaze > 1e-4f || Mathf.Abs(ripple) > 1e-4f || pixelate > 1f || crtCurvature > 1e-4f;
    }

    [Serializable]
    public sealed class NPFXGlitch
    {
        [NPFX("ブロックずれ", "Block Displacement", 0f, 1f)] public float block;
        [NPFX("ブロックの細かさ", "Block Count", 2f, 64f)] public float blockCount = 16f;
        [NPFX("走査線ずれ", "Line Jitter", 0f, 1f)] public float lineJitter;
        [NPFX("縦ジャンプ", "Vertical Jump", 0f, 1f)] public float verticalJump;
        [NPFX("色ずれノイズ", "Color Drift", 0f, 1f)] public float colorDrift;
        [NPFX("VHS のうねり", "VHS Tracking", 0f, 1f)] public float vhs;
        [NPFX("変化の速さ (回 / 秒)", "Steps Per Second", 1f, 60f)] public float speed = 12f;

        public bool Active => block > 1e-4f || lineJitter > 1e-4f || verticalJump > 1e-4f || colorDrift > 1e-4f || vhs > 1e-4f;
    }

    [Serializable]
    public sealed class NPFXStyle
    {
        [NPFX("油彩 (桑原フィルター)", "Kuwahara (Oil Paint)", 0f, 5f)] public float kuwahara;
        [NPFX("ポスタライズ段数 (0 でオフ)", "Posterize Levels", 0f, 16f)] public float posterize;
        [NPFX("シャープ", "Sharpen", 0f, 1f)] public float sharpen;
        [NPFX("エンボス", "Emboss", 0f, 1f)] public float emboss;
        [NPFX("輪郭線", "Edge Lines", 0f, 1f)] public float edge;
        [NPFX("輪郭線の太さ", "Edge Thickness", 0.5f, 4f)] public float edgeThickness = 1f;
        [NPFX("輪郭線: 深度の感度", "Edge Depth Sensitivity", 0f, 1f)] public float edgeDepth = 0.5f;
        [NPFX("輪郭線: 色の感度", "Edge Color Sensitivity", 0f, 1f)] public float edgeColorSensitivity = 0.5f;
        [NPFX("線・インクの色", "Ink Color")] public Color inkColor = Color.black;
        [NPFX("紙の色", "Paper Color")] public Color paperColor = Color.white;
        [NPFX("線画化 (絵を紙の色に置き換える)", "Sketch", 0f, 1f)] public float sketch;
        [NPFX("網点 (ハーフトーン)", "Halftone", 0f, 1f)] public float halftone;
        [NPFX("網点・ハッチの大きさ", "Dot Size", 2f, 32f)] public float halftoneSize = 6f;
        [NPFX("網点の角度", "Dot Angle", 0f, 90f)] public float halftoneAngle = 45f;
        [NPFX("網点をカラーにする", "Color Halftone")] public bool halftoneColor;
        [NPFX("クロスハッチ", "Crosshatch", 0f, 1f)] public float crosshatch;
        [NPFX("レトロ減色 (1 色あたりのビット数、0 でオフ)", "Retro Bits", 0f, 8f)] public float retroBits;
        [NPFX("レトロ減色のドットの粗さ", "Retro Pixel Size", 1f, 8f)] public float retroPixelSize = 1f;

        public bool Active =>
            kuwahara >= 1f || posterize > 1f || sharpen > 1e-4f || emboss > 1e-4f || edge > 1e-4f || sketch > 1e-4f ||
            halftone > 1e-4f || crosshatch > 1e-4f || retroBits >= 1f;
    }

    [Serializable]
    public sealed class NPFXLight
    {
        [NPFX("ブルーム", "Bloom", 0f, 5f)] public float bloom;
        [NPFX("しきい値", "Threshold", 0f, 5f)] public float threshold = 0.9f;
        [NPFX("しきい値のなだらかさ", "Soft Knee", 0f, 1f)] public float knee = 0.5f;
        [NPFX("広がり", "Scatter", 0f, 1f)] public float scatter = 0.7f;
        [NPFX("ブルームの色", "Bloom Tint")] public Color bloomTint = Color.white;
        [NPFX("光条 (アナモルフィック)", "Anamorphic Streak", 0f, 5f)] public float streak;
        [NPFX("光条の長さ", "Streak Length", 0f, 1f)] public float streakLength = 0.5f;
        [NPFX("光条の色", "Streak Tint")] public Color streakTint = new Color(0.6f, 0.8f, 1f, 1f);
        [NPFX("光芒 (ゴッドレイ)", "Light Shafts", 0f, 5f)] public float shafts;
        [NPFX("光芒の長さ", "Shaft Length", 0f, 1f)] public float shaftLength = 0.6f;
        [NPFX("光芒の中心 (画面座標)", "Shaft Center")] public Vector2 shaftCenter = new Vector2(0.5f, 0.8f);
        [NPFX("光芒をこのライトに合わせる", "Shaft Follows Light")] public Light shaftLight;
        [NPFX("光芒の色", "Shaft Tint")] public Color shaftTint = new Color(1f, 0.95f, 0.8f, 1f);
        [NPFX("ディフュージョン (ソフトフィルター)", "Diffusion", 0f, 1f)] public float diffusion;
        [NPFX("ディフュージョンのぼけ幅", "Diffusion Radius", 0f, 1f)] public float diffusionRadius = 0.5f;

        public bool NeedsPrefilter => bloom > 1e-4f || streak > 1e-4f || shafts > 1e-4f;
    }

    [Serializable]
    public sealed class NPFXFocus
    {
        [NPFX("被写界深度", "Depth of Field")] public bool depthOfField;
        [NPFX("ピントを合わせる対象 (あれば距離より優先)", "Focus Target")] public Transform focusTarget;
        [NPFX("ピント距離 (m)", "Focus Distance", 0.1f, 100f)] public float focusDistance = 3f;
        [NPFX("ピントの合う幅 (m)", "Focus Range", 0.05f, 50f)] public float focusRange = 1.5f;
        [NPFX("ボケの大きさ", "Bokeh Size", 0f, 32f)] public float bokehSize = 12f;
        [NPFX("チルトシフト (ミニチュア風)", "Tilt Shift", 0f, 1f)] public float tiltShift;
        [NPFX("チルトシフトの中心の高さ", "Tilt Shift Center", 0f, 1f)] public float tiltCenter = 0.5f;
        [NPFX("チルトシフトの帯の幅", "Tilt Shift Width", 0.02f, 1f)] public float tiltWidth = 0.25f;
        [NPFX("画面全体のぼかし", "Full Screen Blur", 0f, 1f)] public float blur;
        [NPFX("ぼかしの幅", "Blur Radius", 0f, 1f)] public float blurRadius = 0.5f;

        public bool NeedsDof => depthOfField || tiltShift > 1e-4f;
    }

    [Serializable]
    public sealed class NPFXMask
    {
        [NPFX("対象 (この下のレンダラーをキャラクターとして扱う)", "Targets")] public GameObject[] targets = new GameObject[0];
        [NPFX("背景の彩度を落とす", "Background Desaturate", 0f, 1f)] public float backgroundDesaturate;
        [NPFX("背景を暗くする", "Background Darken", 0f, 1f)] public float backgroundDarken;
        [NPFX("背景をぼかす", "Background Blur", 0f, 1f)] public float backgroundBlur;
        [NPFX("縁取り", "Outline", 0f, 1f)] public float outline;
        [NPFX("縁取りの太さ (ピクセル)", "Outline Width", 0f, 32f)] public float outlineWidth = 6f;
        [NPFX("縁取りをぼかす (グロー)", "Outline Softness", 0f, 1f)] public float outlineSoftness;
        [NPFX("縁取りの色", "Outline Color")] public Color outlineColor = Color.white;
        [NPFX("シルエット塗り", "Silhouette Fill", 0f, 1f)] public float fill;
        [NPFX("シルエットの色", "Silhouette Color")] public Color fillColor = Color.black;

        public bool HasEffect =>
            backgroundDesaturate > 1e-4f || backgroundDarken > 1e-4f || backgroundBlur > 1e-4f || outline > 1e-4f || fill > 1e-4f;
    }

    [Serializable]
    public sealed class NPFXColor
    {
        [NPFX("露出 (EV)", "Exposure", -5f, 5f)] public float exposure;
        [NPFX("コントラスト", "Contrast", 0f, 2f)] public float contrast = 1f;
        [NPFX("彩度", "Saturation", 0f, 2f)] public float saturation = 1f;
        [NPFX("自然な彩度", "Vibrance", -1f, 1f)] public float vibrance;
        [NPFX("色相", "Hue Shift", -180f, 180f)] public float hueShift;
        [NPFX("色温度 (＋暖色 / －寒色)", "Temperature", -1f, 1f)] public float temperature;
        [NPFX("色かぶり (＋マゼンタ / －緑)", "Tint", -1f, 1f)] public float tint;
        [NPFX("トーンマップ", "Tonemap")] public NPFXTonemap tonemap;
        [NPFX("リフト (暗部に足す色)", "Lift")] public Color lift = Color.black;
        [NPFX("ガンマ (中間調)", "Gamma")] public Color gamma = Color.white;
        [NPFX("ゲイン (明部に掛ける色)", "Gain")] public Color gain = Color.white;
        [NPFX("暗部の色づけ", "Shadow Tint")] public Color shadowTint = new Color(0.5f, 0.5f, 0.5f, 1f);
        [NPFX("暗部の色づけの強さ", "Shadow Tint Amount", 0f, 1f)] public float shadowTintAmount;
        [NPFX("明部の色づけ", "Highlight Tint")] public Color highlightTint = new Color(0.5f, 0.5f, 0.5f, 1f);
        [NPFX("明部の色づけの強さ", "Highlight Tint Amount", 0f, 1f)] public float highlightTintAmount;
        [NPFX("LUT (横長ストリップ、例 1024x32)", "LUT (strip)")] public Texture2D lut;
        [NPFX("LUT の効き", "LUT Contribution", 0f, 1f)] public float lutContribution = 1f;
        [NPFX("モノクロ", "Monochrome", 0f, 1f)] public float monochrome;
        [NPFX("セピア", "Sepia", 0f, 1f)] public float sepia;
        [NPFX("ネガ反転", "Invert", 0f, 1f)] public float invert;
        [NPFX("ソラリゼーション", "Solarize", 0f, 1f)] public float solarize;
        [NPFX("2 値化", "Threshold", 0f, 1f)] public float threshold;
        [NPFX("2 値化のしきい値", "Threshold Level", 0f, 1f)] public float thresholdLevel = 0.5f;
        [NPFX("グラデーションマップ", "Gradient Map", 0f, 1f)] public float gradientMap;
        [NPFX("グラデーションマップ: 暗い側", "Gradient Dark")] public Color gradientDark = new Color(0.1f, 0.05f, 0.3f, 1f);
        [NPFX("グラデーションマップ: 明るい側", "Gradient Light")] public Color gradientLight = new Color(1f, 0.85f, 0.6f, 1f);
        [NPFX("暗視スコープ", "Night Vision", 0f, 1f)] public float nightVision;
        [NPFX("サーモグラフィ", "Thermal", 0f, 1f)] public float thermal;
    }

    [Serializable]
    public sealed class NPFXOverlay
    {
        [NPFX("色かけ", "Color Overlay")] public Color color = Color.white;
        [NPFX("色かけの強さ", "Color Overlay Amount", 0f, 1f)] public float colorAmount;
        [NPFX("色かけ・グラデーションの合成", "Blend Mode")] public NPFXBlend blend = NPFXBlend.Multiply;
        [NPFX("グラデーションかけ", "Gradient Overlay", 0f, 1f)] public float gradient;
        [NPFX("グラデーション: 上", "Gradient Top")] public Color gradientTop = new Color(1f, 0.8f, 0.6f, 1f);
        [NPFX("グラデーション: 下", "Gradient Bottom")] public Color gradientBottom = new Color(0.3f, 0.4f, 0.8f, 1f);
        [NPFX("グラデーションの角度", "Gradient Angle", 0f, 360f)] public float gradientAngle;
        [NPFX("光漏れ (ライトリーク)", "Light Leak", 0f, 1f)] public float lightLeak;
        [NPFX("光漏れの色", "Light Leak Color")] public Color lightLeakColor = new Color(1f, 0.45f, 0.15f, 1f);
        [NPFX("重ねるテクスチャ (フレア・紙・ほこり素材など)", "Overlay Texture")] public Texture texture;
        [NPFX("テクスチャの強さ", "Texture Amount", 0f, 1f)] public float textureAmount;
        [NPFX("テクスチャの合成", "Texture Blend")] public NPFXBlend textureBlend = NPFXBlend.Screen;
        [NPFX("テクスチャのスクロール", "Texture Scroll")] public Vector2 textureScroll;
        [NPFX("フラッシュ", "Flash", 0f, 1f)] public float flash;
        [NPFX("フラッシュの色", "Flash Color")] public Color flashColor = Color.white;
        [NPFX("フェード", "Fade", 0f, 1f)] public float fade;
        [NPFX("フェードの色", "Fade Color")] public Color fadeColor = Color.black;
        [NPFX("ビネット (周辺減光)", "Vignette", 0f, 1f)] public float vignette;
        [NPFX("ビネットのなめらかさ", "Vignette Smoothness", 0.01f, 1f)] public float vignetteSmoothness = 0.5f;
        [NPFX("ビネットを円にする", "Vignette Roundness", 0f, 1f)] public float vignetteRoundness;
        [NPFX("ビネットの色", "Vignette Color")] public Color vignetteColor = Color.black;
        [NPFX("集中線", "Speed Lines", 0f, 1f)] public float speedLines;
        [NPFX("集中線の本数", "Line Density", 8f, 400f)] public float lineDensity = 120f;
        [NPFX("集中線の内側の半径", "Line Inner Radius", 0f, 1f)] public float lineInnerRadius = 0.3f;
        [NPFX("集中線の色", "Line Color")] public Color lineColor = Color.black;
        [NPFX("集中線の中心", "Line Center")] public Vector2 lineCenter = new Vector2(0.5f, 0.5f);
        [NPFX("集中線の動き (回 / 秒、0 で静止)", "Line Animation FPS", 0f, 30f)] public float lineFps = 12f;
        [NPFX("レターボックスの縦横比 (0 でオフ、例 2.39)", "Letterbox Aspect", 0f, 4f)] public float letterbox;
    }

    [Serializable]
    public sealed class NPFXRetro
    {
        [NPFX("走査線", "Scanlines", 0f, 1f)] public float scanlines;
        [NPFX("走査線の本数", "Scanline Count", 60f, 1080f)] public float scanlineCount = 360f;
        [NPFX("走査線の流れる速さ", "Scanline Speed", -10f, 10f)] public float scanlineSpeed;
        [NPFX("RGB マスク (ブラウン管の画素)", "RGB Mask", 0f, 1f)] public float rgbMask;
        [NPFX("フィルム粒子", "Film Grain", 0f, 1f)] public float grain;
        [NPFX("粒子の大きさ", "Grain Size", 1f, 8f)] public float grainSize = 1.5f;
        [NPFX("フィルムの縦傷", "Scratches", 0f, 1f)] public float scratches;
        [NPFX("ちらつき", "Flicker", 0f, 1f)] public float flicker;
        [NPFX("ほこり", "Dust", 0f, 1f)] public float dust;
        [NPFX("砂嵐", "Static Noise", 0f, 1f)] public float staticNoise;
    }

    [Serializable]
    public sealed class NPFXFog
    {
        [NPFX("距離フォグ", "Distance Fog", 0f, 1f)] public float density;
        [NPFX("フォグの開始距離 (m)", "Fog Start", 0f, 100f)] public float start;
        [NPFX("フォグの色", "Fog Color")] public Color color = new Color(0.7f, 0.8f, 0.9f, 1f);
        [NPFX("空にもかける", "Affect Sky")] public bool affectSky;
    }

    [Serializable]
    public sealed class NPFXTime
    {
        [NPFX("残像", "Afterimage", 0f, 0.98f)] public float afterimage;
        [NPFX("光の尾 (明るい所だけ残す)", "Light Trail", 0f, 1f)] public float lightTrail;
        [NPFX("コマ落ち (fps、0 でオフ)", "Frame Hold FPS", 0f, 30f)] public float frameHoldFps;

        public bool Active => afterimage > 1e-4f || frameHoldFps > 0.5f;
    }

    [Serializable]
    public sealed class NPFXBeat
    {
        [NPFX("BPM (0 でオフ)", "BPM", 0f, 300f)] public float bpm;
        [NPFX("拍の頭のずれ (秒)", "Offset (s)")] public float offset;
        [NPFX("減衰の速さ", "Decay", 1f, 20f)] public float decay = 6f;
        [NPFX("拍でズーム", "Zoom Pulse", 0f, 1f)] public float zoom;
        [NPFX("拍でフラッシュ", "Flash Pulse", 0f, 1f)] public float flash;
        [NPFX("拍で色収差", "Chromatic Pulse", 0f, 1f)] public float chromatic;
        [NPFX("拍でブルーム", "Bloom Pulse", 0f, 1f)] public float bloom;
        [NPFX("拍で画面揺れ", "Shake Pulse", 0f, 1f)] public float shake;

        public bool On => bpm > 0.01f;
        public bool AffectsLens => On && (zoom > 1e-4f || chromatic > 1e-4f || shake > 1e-4f);
        public bool AffectsBloom => On && bloom > 1e-4f;
    }
}
