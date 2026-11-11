using System;
using System.Collections.Generic;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// ショーケースで機能を「実際に見える状態」にするための個別セットアップ表。
    ///
    /// キーワードを立てるだけでは何も映らない機能がかなりある。テクスチャを要求するもの
    /// （MatCap / Ramp / Decal / Parallax など）は既定値が <c>"white"</c> や <c>"black"</c> で、
    /// トグルを ON にしても Baseline と区別がつかない。それを「動作確認できた」と
    /// 見なしてしまうのが一番まずいので、素材と強度をここで用意する。
    ///
    /// ここに登録が無いキーワードは「トグルを立てるだけ」で並び、
    /// ショーケース生成時のレポートに<b>素の状態として明示</b>される。
    /// 見えないまま黙って並ぶことはない。
    /// </summary>
    internal static class NataneShowcaseFeatureSetups
    {
        /// <summary>キーワード → 追加セットアップ。</summary>
        private static readonly Dictionary<string, Action<Material>> Setups =
            new Dictionary<string, Action<Material>>(StringComparer.Ordinal)
            {
                // ---- テクスチャを要求するもの ----

                { "_MATCAP", m =>
                    {
                        SetTex(m, "_MatCapTex", NataneShowcaseAssets.MatCap());
                        Set(m, "_MatCapIntensity", 1f);
                        Set(m, "_MatCapBlend", 1f);
                    }
                },
                { "_MATCAP_2", m =>
                    {
                        SetTex(m, "_MatCapTex2", NataneShowcaseAssets.MatCap());
                        Set(m, "_MatCapIntensity2", 1f);
                    }
                },
                { "_MATCAP_3", m =>
                    {
                        SetTex(m, "_MatCapTex3", NataneShowcaseAssets.MatCap());
                        Set(m, "_MatCapIntensity3", 1f);
                    }
                },
                { "_USE_RAMP", m => SetTex(m, "_RampTex", NataneShowcaseAssets.ShadingRamp()) },
                { "_NORMALMAP", m =>
                    {
                        SetTex(m, "_BumpMap", NataneShowcaseAssets.NormalMap());
                        Set(m, "_BumpScale", 1f);
                    }
                },
                { "_DETAIL_MAP", m =>
                    {
                        SetTex(m, "_DetailNormalMap", NataneShowcaseAssets.NormalMap());
                        SetTex(m, "_DetailTex", NataneShowcaseAssets.Noise());
                        Set(m, "_DetailIntensity", 1f);
                    }
                },
                { "_PARALLAX", m =>
                    {
                        SetTex(m, "_ParallaxMap", NataneShowcaseAssets.Height());
                        Set(m, "_ParallaxScale", 0.05f);
                    }
                },
                { "_DECAL", m =>
                    {
                        SetTex(m, "_DecalTex", NataneShowcaseAssets.Decal());
                        Set(m, "_DecalScale", 0.5f);
                        SetColor(m, "_DecalColor", new Color(1f, 0.3f, 0.3f, 1f));
                    }
                },
                { "_LUT_3D", m =>
                    {
                        SetTex(m, "_LUT3DTex", NataneShowcaseAssets.Lut());
                        Set(m, "_LUT3DIntensity", 1f);
                        Set(m, "_LUT3DSize", 16f);
                    }
                },
                { "_WATERCOLOR", m =>
                    {
                        SetTex(m, "_WCGranulationTex", NataneShowcaseAssets.Granulation());
                        SetTex(m, "_WCPaperTex", NataneShowcaseAssets.Paper());
                        Set(m, "_WCBlend", 1f);
                        Set(m, "_WCGranulation", 0.6f);
                        Set(m, "_WCPaperIntensity", 0.3f);
                        Set(m, "_WCEdgeDarkening", 0.8f);
                    }
                },
                { "_SURFACE_COVER", m =>
                    {
                        Set(m, "_SurfaceCoverAmount", 0.6f);
                        SetColor(m, "_SurfaceCoverColor", new Color(0.95f, 0.96f, 1f, 1f));
                    }
                },
                { "_BACKFACE_TEXTURE", m =>
                    {
                        SetTex(m, "_BackfaceTex", NataneShowcaseAssets.Decal());
                        SetColor(m, "_BackfaceColor", new Color(1f, 0.5f, 0.5f, 1f));
                    }
                },

                // ---- 強度が既定 0 で、上げないと見えないもの ----

                { "_EMISSION", m =>
                    {
                        Set(m, "_EmissionIntensity", 2f);
                        SetColor(m, "_EmissionColor", new Color(1.4f, 0.9f, 0.4f, 1f));
                    }
                },
                { "_RIM_LIGHT", m =>
                    {
                        Set(m, "_RimIntensity", 1.2f);
                        Set(m, "_RimWidth", 0.5f);
                        SetColor(m, "_RimColor", new Color(0.6f, 0.9f, 1f, 1f));
                    }
                },
                { "_SPECULAR", m =>
                    {
                        Set(m, "_SpecularIntensity", 1.5f);
                        Set(m, "_SpecularSoftness", 0.15f);
                    }
                },
                { "_SSS", m =>
                    {
                        Set(m, "_SSSIntensity", 1.5f);
                        SetColor(m, "_SSSColor", new Color(1f, 0.35f, 0.3f, 1f));
                    }
                },
                { "_GLITTER", m => Set(m, "_GlitterIntensity", 2f) },
                { "_IRIDESCENCE", m => Set(m, "_IridescenceIntensity", 1.5f) },
                { "_ENV_RIM", m => Set(m, "_EnvRimIntensity", 1.5f) },
                { "_HAIR_SPECULAR", m =>
                    {
                        Set(m, "_HairSpecIntensity", 1.5f);
                        Set(m, "_HairSpecShift", 0.2f);
                    }
                },
                { "_PROCEDURAL_MATCAP", m => Set(m, "_ProceduralMatCapIntensity", 1.5f) },
                { "_FAKE_REFLECTION", m => Set(m, "_FakeReflectionIntensity", 1.2f) },
                { "_REFLECTION", m => Set(m, "_ReflectionIntensity", 1f) },
                { "_USE_AO", m =>
                    {
                        SetTex(m, "_AOMap", NataneShowcaseAssets.Noise());
                        Set(m, "_AOIntensity", 1f);
                    }
                },
                { "_PROCEDURAL_AO", m => Set(m, "_ProceduralAOIntensity", 1f) },

                // ---- 座標・強度を与えないと何も起きないもの ----

                { "_DISSOLVE", m =>
                    {
                        SetTex(m, "_DissolveTex", NataneShowcaseAssets.Noise());
                        Set(m, "_DissolveAmount", 0.45f);
                        Set(m, "_DissolveEdgeWidth", 0.08f);
                        Set(m, "_DissolveEdgeIntensity", 4f);
                        SetColor(m, "_DissolveEdgeColor", new Color(2.5f, 1.2f, 0.4f, 1f));
                    }
                },
                { "_HUE_SHIFT", m => Set(m, "_HueShift", 0.35f) },
                { "_COLOR_QUANTIZE", m =>
                    {
                        Set(m, "_QuantizeLevels", 4f);
                        Set(m, "_QuantizeBlend", 1f);
                    }
                },
                { "_PIXEL_ART", m => Set(m, "_PixelArtSize", 48f) },
                { "_TOPOGRAPHIC", m =>
                    {
                        Set(m, "_TopoSpacing", 0.12f);
                        Set(m, "_TopoEmission", 2f);
                        SetColor(m, "_TopoColor", new Color(0.2f, 1f, 0.8f, 1f));
                    }
                },
                { "_CAUSTICS", m =>
                    {
                        Set(m, "_CausticsIntensity", 2f);
                        Set(m, "_CausticsScale", 4f);
                        SetColor(m, "_CausticsColor", new Color(0.6f, 0.9f, 1f, 1f));
                    }
                },
                { "_LENTICULAR", m => Set(m, "_LenticularBlend", 1f) },
                { "_SMEAR", m => Set(m, "_SmearStrength", 0.5f) },
                { "_WATER_DRIP", m => Set(m, "_WaterDripAmount", 0.6f) },
                { "_HOLOGRAM", m =>
                    {
                        Set(m, "_HologramBlend", 1f);
                        Set(m, "_HologramScanlineIntensity", 0.6f);
                        SetColor(m, "_HologramColor", new Color(0.4f, 1.4f, 1.8f, 1f));
                    }
                },
                { "_GLITCH", m =>
                    {
                        Set(m, "_GlitchIntensity", 0.6f);
                        Set(m, "_GlitchFrequency", 1f);
                    }
                },
                { "_VERTEX_ANIMATION", m =>
                    {
                        Set(m, "_VertexAnimAmplitude", 0.08f);
                        Set(m, "_VertexAnimSpeed", 1.5f);
                    }
                },
                { "_TESSELLATION", m =>
                    {
                        Set(m, "_TessellationFactor", 4f);
                        Set(m, "_TessDisplacement", 0.05f);
                    }
                },
                { "_HEIGHT_FOG", m =>
                    {
                        Set(m, "_HeightFogDensity", 0.8f);
                        SetColor(m, "_HeightFogColor", new Color(0.6f, 0.7f, 0.9f, 1f));
                    }
                },
                { "_SHADOW_EDGE_NOISE", m =>
                    {
                        Set(m, "_ShadowEdgeNoiseScale", 24f);
                        Set(m, "_ShadowEdgeNoiseStrength", 0.15f);
                    }
                },
                { "_CAST_SHADOW_COLOR", m =>
                    {
                        SetColor(m, "_CastShadowColor", new Color(0.4f, 0.3f, 0.6f, 1f));
                        Set(m, "_CastShadowColorStrength", 1f);
                    }
                },
                { "_LINE_BOIL", m =>
                    {
                        Set(m, "_LineBoilUVJitter", 0.01f);
                        Set(m, "_LineBoilFps", 8f);
                    }
                },
                { "_SHAPED_HIGHLIGHT", m =>
                    {
                        Set(m, "_ShapedHLShape", 3f);   // Star
                        Set(m, "_ShapedHLIntensity", 3f);
                        Set(m, "_ShapedHLSize", 0.5f);
                    }
                },
                { "_OUTLINE", m =>
                    {
                        Set(m, "_OutlineWidth", 0.15f);
                        SetColor(m, "_OutlineColor", new Color(0.05f, 0.05f, 0.08f, 1f));
                    }
                },
                { "_HEIGHT_FADE", m =>
                    {
                        Set(m, "_HeightFadeStart", 0.5f);
                        Set(m, "_HeightFadeEnd", 1.8f);
                    }
                },
                // シェル毛皮は既定値のままだと毛に見えない。
                // 長さ 0.02（球体の半径の 4%）では毛の存在が分からず、
                // LOD 距離 10 のままだとショーケース全体を写す距離で
                // シェルが 4 枚まで間引かれて丸い塊に戻ってしまう。
                { "_FUR", m =>
                    {
                        Set(m, "_FurLength", 0.07f);         // 球体の半径の 14%。
                        // 密度＝毛の本数。1 本を太らせると毛同士がくっついて
                        // 塊になるので、覆いたければこちらを上げる（内部で 4 倍のセル数）。
                        Set(m, "_FurDensity", 65f);
                        Set(m, "_FurAlphaCutoff", 0.15f);    // 毛の細さ。大きいほど1本が細い
                        Set(m, "_FurFluff", 0.6f);           // 毛を散らして交差させる
                        Set(m, "_FurRootOffset", -0.35f);    // 根元を詰めて地肌を隠す
                        Set(m, "_FurGravity", 0.35f);        // 毛先だけが垂れる
                        Set(m, "_FurColorBlend", 0.9f);      // 根元→毛先の色差を見せる
                        SetColor(m, "_FurRootColor", new Color(0.26f, 0.17f, 0.12f, 1f));
                        SetColor(m, "_FurTipColor", new Color(0.96f, 0.84f, 0.68f, 1f));
                        Set(m, "_FurAO", 0.6f);
                        Set(m, "_FurSpecular", 0.35f);
                        Set(m, "_FurRimLight", 0.5f);
                        Set(m, "_FurWindStrength", 0.25f);
                        // ショーケースは全体を俯瞰する距離から見るので、LOD で間引かれないようにする。
                        Set(m, "_FurLODDistance", 50f);
                        Set(m, "_FurLODMinLayers", 8f);
                    }
                },

                { "_GRADIENT_BASE_COLOR", m =>
                    {
                        SetColor(m, "_GradientTopColor", new Color(1f, 0.85f, 0.6f, 1f));
                        SetColor(m, "_GradientBottomColor", new Color(0.4f, 0.5f, 0.9f, 1f));
                    }
                },
            };

        /// <summary>
        /// 追加セットアップがあれば適用する。戻り値は「素のトグルではない」かどうか。
        /// </summary>
        public static bool TryApply(string keyword, Material material)
        {
            if (material == null || string.IsNullOrEmpty(keyword)) return false;
            if (!Setups.TryGetValue(keyword, out Action<Material> setup)) return false;

            setup(material);
            return true;
        }

        public static bool Has(string keyword)
        {
            return !string.IsNullOrEmpty(keyword) && Setups.ContainsKey(keyword);
        }

        // ---- 小物 ----

        private static void Set(Material m, string prop, float value)
        {
            if (m != null && m.HasProperty(prop)) m.SetFloat(prop, value);
        }

        private static void SetColor(Material m, string prop, Color value)
        {
            if (m != null && m.HasProperty(prop)) m.SetColor(prop, value);
        }

        private static void SetTex(Material m, string prop, Texture texture)
        {
            if (m != null && texture != null && m.HasProperty(prop)) m.SetTexture(prop, texture);
        }
    }
}
