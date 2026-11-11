using System;
using System.Collections.Generic;
using System.Linq;

namespace NataneToon.Editor
{
    /// <summary>
    /// パフォーマンス評価 (A/B/C/D) で「有効な機能」として数えるキーワード。
    ///
    /// 元は MaterialValidator の private 配列だったが、下流アセンブリ
    /// NataneToon.Editor.Tools にあるため監査側から突合できず、
    /// v1.6/1.7 で追加された機能の登録漏れを検出できなかった。
    /// 上流のここへ移すことで NataneShaderConsistencyAudit が差集合を報告できる。
    ///
    /// KeywordMappings 全体から機械的に導出はしない。あちらは 114 件あり、
    /// _MAIN_TEX_ANIMATION のような軽微なものまで含むため、そのまま数えると
    /// A(0-3)/B(4-6)/C(7-9)/D(10+) という評価尺度の較正が壊れる。
    /// ここは「ユーザーが意識して有効化する、負荷のある表現機能」の集合として
    /// 手で維持し、漏れは監査に検出させる方針を取る。
    /// </summary>
    // MaterialValidator は下流アセンブリ NataneToon.Editor.Tools にあるため public。
    public static class NatanePerformanceFeatureKeywords
    {
        /// <summary>評価で 1 機能として数えるキーワード。</summary>
        public static readonly string[] Counted =
        {
            "_SPECULAR", "_RIM_LIGHT", "_RIM_LIGHT_2", "_OFFSET_RIM_LIGHT", "_SSS", "_MATCAP", "_OUTLINE", "_EMISSION",
            "_DISSOLVE", "_HUE_SHIFT", "_NORMALMAP", "_REFLECTION", "_ENV_RIM", "_PARALLAX", "_REFRACTION",
            "_IRIDESCENCE", "_GLITTER", "_MATCAP_2", "_MATCAP_3", "_AUDIOLINK", "_HOLOGRAM", "_GLITCH",
            "_HOLOGRAM_NOISE", "_DECAL", "_VAT", "_VERTEX_ANIMATION", "_PIXEL_VERTEX_LIGHTS", "_DETAIL_MAP",
            "_TRIPLANAR", "_HEIGHT_FOG", "_SURFACE_COVER", "_MIRROR_CONTROL", "_QUEST_LITE", "_WATER_DRIP",
            "_VIDEO_TEXTURE", "_INTERSECTION_FADE", "_SCREEN_TONE", "_SCREEN_EDGE", "_HATCHING", "_USE_LIGHT_VOLUME",
            "_LTCGI", "_HAIR_SPECULAR", "_WATERCOLOR", "_SMEAR", "_BACKFACE_TEXTURE", "_FUR",

            // --- v1.6/1.7 で追加され、登録を忘れていた表現機能 ---
            // 監査(パフォーマンス評価の計上漏れ)で検出したもののうち、
            // 「有効化するとフラグメント/頂点の処理が明確に増える表現機能」を採用した。
            "_CAUSTICS", "_LENTICULAR", "_TOPOGRAPHIC", "_PIXEL_ART", "_LINE_BOIL",
            "_SHAPED_HIGHLIGHT", "_FX_MODULATOR", "_HALFTONE_SHADOW", "_PROCEDURAL_MATCAP",
            "_FAKE_REFLECTION", "_MIRROR_TEXTURE", "_GRADIENT_BASE_COLOR", "_COLOR_QUANTIZE",
            "_CAST_SHADOW_COLOR", "_SHADOW_EDGE_NOISE",

            // Shadow Shape Rig (NPR2026 P1)。マスク 1 枚と楕円 4 スロット分の
            // 距離計算がフラグメントに乗る。未使用スロットは早期 return で軽いが、
            // 有効化した時点で確実にコストは増える。
            "_SHADOW_SHAPE_RIG",

            // 深度テクスチャを要求するため実コストが大きい。
            "_DEPTH_COLOR_FADE",

            // テッセレーションは頂点数そのものが増える。Quest 向け案内の観点で
            // 未計上だったのは特に影響が大きい。
            "_TESSELLATION",

            // PBR ライティング一式。
            "_PBR",

            // 影の玉ボケ。3x3 のセル走査を行うため相応の負荷がある。
            "_SHADOW_BOKEH",
        };

        /// <summary>
        /// 意図的に計上しないキーワード。監査が「計上漏れ」として再提示しないよう明示する。
        ///
        /// 除外の基準は「負荷を増やす表現ではないもの」:
        ///   - 距離/高さフェードは描画を間引く側、つまり負荷を下げる方向の機能
        ///   - ライトスナップ・ディザ・顔向き補正・平面化は演算がごく軽い
        ///   - AO は基本シェーディングの一部で、独立した表現機能ではない
        /// </summary>
        public static readonly string[] IntentionallyNotCounted =
        {
            "_DISTANCE_FADE",
            "_HEIGHT_FADE",
            "_LIGHT_SNAP",
            "_USE_DITHERING",
            "_FACE_ORTHO",
            "_PERSPECTIVE_FLAT",
            "_USE_AO",
        };

        private static HashSet<string> _excluded;

        public static bool IsIntentionallyNotCounted(string keyword)
        {
            if (_excluded == null)
                _excluded = new HashSet<string>(IntentionallyNotCounted, StringComparer.Ordinal);
            return !string.IsNullOrEmpty(keyword) && _excluded.Contains(keyword);
        }

        private static HashSet<string> _set;

        public static HashSet<string> AsSet()
        {
            return _set ?? (_set = new HashSet<string>(Counted, StringComparer.Ordinal));
        }

        public static bool IsCounted(string keyword)
        {
            return !string.IsNullOrEmpty(keyword) && AsSet().Contains(keyword);
        }
    }
}
