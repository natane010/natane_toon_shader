using System;
using UnityEngine;

namespace NataneToon
{
    /// <summary>
    /// シェーダーのキーワードで切り替える効果のまとまり。
    /// オフのまとまりはコンパイル時に取り除かれ、命令もテクスチャ読みも残らない。
    /// </summary>
    [Flags]
    public enum NPFXFeature
    {
        None = 0,
        /// <summary>歪曲・ミラー・万華鏡・タイル・波・陽炎・波紋・モザイク・ブラウン管の湾曲。</summary>
        LensWarp = 1 << 0,
        LensGlitch = 1 << 1,
        /// <summary>色収差・RGB ずらし・色ずれノイズ (画面を 3 回読む)。</summary>
        LensFringe = 1 << 2,
        /// <summary>放射ブラー・方向ブラー (10 タップ)。</summary>
        LensBlur = 1 << 3,
        StyleKuwahara = 1 << 4,
        StyleEdge = 1 << 5,
        /// <summary>網点・クロスハッチ・レトロ減色。</summary>
        StylePrint = 1 << 6,
        /// <summary>ブルーム・光条・光芒・ディフュージョン・被写界深度・ぼかしの合成。</summary>
        Composite = 1 << 7,
        Mask = 1 << 8,
        Fog = 1 << 9,
        /// <summary>「色」の項目すべて。</summary>
        Grade = 1 << 10,
        /// <summary>色かけ・グラデーション・光漏れ・テクスチャ・集中線・ビネット・レターボックス。</summary>
        Overlay = 1 << 11,
        Retro = 1 << 12,
        All = (1 << 13) - 1,
        /// <summary>ワールド用シェーダーに無いもの (中間テクスチャが要る)。</summary>
        NotInWorld = Composite | Mask,
    }

    public static class NatanePostFXKeywords
    {
        // NPFXFeature のビットの並びと一致させること。シェーダー側は NatanePostFXCore.hlsl の #if。
        static readonly string[] s_Names =
        {
            "NPFX_L_WARP", "NPFX_L_GLITCH", "NPFX_L_FRINGE", "NPFX_L_BLUR",
            "NPFX_S_KUWAHARA", "NPFX_S_EDGE", "NPFX_S_PRINT",
            "NPFX_F_COMPOSITE", "NPFX_F_MASK", "NPFX_F_FOG", "NPFX_F_GRADE", "NPFX_F_OVERLAY", "NPFX_F_RETRO",
        };

        /// <summary>
        /// true にすると、設定に関係なく全部のまとまりを有効にする (負荷の比較・不具合の切り分け用)。
        /// 見た目は変わらない。
        /// </summary>
        public static bool forceAll;

        const float k_Eps = 1e-4f;

        /// <summary>今の設定で必要なまとまり。composite / mask は実行部の手順 (NatanePostFXPlan) から渡す。</summary>
        public static NPFXFeature Needed(NatanePostFX fx, bool composite, bool mask)
        {
            NPFXFeature f = NPFXFeature.None;

            NPFXLens l = fx.lens;
            if (Mathf.Abs(l.distortion) > k_Eps || l.mirror != NPFXMirror.Off || l.kaleidoscope >= 2f || l.tileX > 1.001f || l.tileY > 1.001f ||
                l.wave > k_Eps || l.heatHaze > k_Eps || Mathf.Abs(l.ripple) > k_Eps || l.pixelate > 1f || l.crtCurvature > k_Eps)
                f |= NPFXFeature.LensWarp;
            if (fx.glitch.Active) f |= NPFXFeature.LensGlitch;
            if (l.chromaticAberration > k_Eps || l.rgbShift > k_Eps || fx.glitch.colorDrift > k_Eps || (fx.beat.On && fx.beat.chromatic > k_Eps))
                f |= NPFXFeature.LensFringe;
            if (l.radialBlur > k_Eps || l.directionalBlur > k_Eps) f |= NPFXFeature.LensBlur;

            NPFXStyle s = fx.style;
            if (s.kuwahara >= 1f) f |= NPFXFeature.StyleKuwahara;
            if (s.edge > k_Eps) f |= NPFXFeature.StyleEdge;
            if (s.halftone > k_Eps || s.crosshatch > k_Eps || s.retroBits >= 0.5f) f |= NPFXFeature.StylePrint;

            if (composite) f |= NPFXFeature.Composite;
            if (mask) f |= NPFXFeature.Mask;
            if (fx.fog.density > 1e-5f) f |= NPFXFeature.Fog;

            NPFXColor c = fx.color;
            if (Mathf.Abs(c.exposure) > k_Eps || Mathf.Abs(c.contrast - 1f) > k_Eps || Mathf.Abs(c.saturation - 1f) > k_Eps ||
                Mathf.Abs(c.vibrance) > k_Eps || Mathf.Abs(c.hueShift) > k_Eps || Mathf.Abs(c.temperature) > k_Eps || Mathf.Abs(c.tint) > k_Eps ||
                c.tonemap != NPFXTonemap.None || !IsRgb(c.lift, 0f) || !IsRgb(c.gamma, 1f) || !IsRgb(c.gain, 1f) ||
                c.shadowTintAmount > k_Eps || c.highlightTintAmount > k_Eps || (c.lut != null && c.lutContribution > k_Eps) ||
                c.monochrome > k_Eps || c.sepia > k_Eps || c.invert > k_Eps || c.solarize > k_Eps || c.threshold > k_Eps ||
                c.gradientMap > k_Eps || c.nightVision > k_Eps || c.thermal > k_Eps)
                f |= NPFXFeature.Grade;

            NPFXOverlay o = fx.overlay;
            if (o.colorAmount > k_Eps || o.gradient > k_Eps || o.lightLeak > k_Eps || (o.texture != null && o.textureAmount > k_Eps) ||
                o.vignette > k_Eps || o.speedLines > k_Eps || o.letterbox > 0.01f)
                f |= NPFXFeature.Overlay;

            NPFXRetro r = fx.retro;
            if (r.scanlines > k_Eps || r.rgbMask > k_Eps || r.grain > k_Eps || r.scratches > k_Eps || r.flicker > k_Eps ||
                r.dust > k_Eps || r.staticNoise > k_Eps)
                f |= NPFXFeature.Retro;

            return f;
        }

        static bool IsRgb(Color c, float v)
        {
            return Mathf.Abs(c.r - v) < 1e-5f && Mathf.Abs(c.g - v) < 1e-5f && Mathf.Abs(c.b - v) < 1e-5f;
        }

        /// <summary>マテリアルのキーワードを features に合わせる。</summary>
        public static void Apply(Material m, NPFXFeature features)
        {
            if (m == null) return;
            for (int i = 0; i < s_Names.Length; i++)
            {
                if ((features & (NPFXFeature)(1 << i)) != 0) m.EnableKeyword(s_Names[i]);
                else m.DisableKeyword(s_Names[i]);
            }
        }

        /// <summary>
        /// ワールド用マテリアルのキーワードを設定に合わせる。
        /// keepAll = true なら全部残す (あとから Animator / Udon で 0 から動かす効果があるとき用。そのぶん重い)。
        /// </summary>
        public static NPFXFeature ApplyWorld(NatanePostFX fx, Material m, bool keepAll = false)
        {
            NPFXFeature f = keepAll ? NPFXFeature.All : Needed(fx, false, false);
            f &= ~NPFXFeature.NotInWorld;
            Apply(m, f);
            return f;
        }

        public static string Describe(NPFXFeature features)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s_Names.Length; i++)
            {
                if ((features & (NPFXFeature)(1 << i)) == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(s_Names[i]);
            }
            return sb.ToString();
        }
    }
}
