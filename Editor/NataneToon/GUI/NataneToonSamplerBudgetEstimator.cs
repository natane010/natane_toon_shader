using System.Collections.Generic;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Estimates D3D11 sampler pressure so the inspector can warn and block
    /// risky feature combinations before shader compilation fails.
    /// </summary>
    public static class NataneToonSamplerBudgetEstimator
    {
        public const string ScreenEdgeSplitShaderName = "Natane/Toon Shader (ScreenEdge Split)";
        public const int BaseSamplerCount = 3;
        public const int SamplerLimit = 16;
        public const int WarningThreshold = 13;
        public const int NearLimitThreshold = 15;

        // Light Probe Proxy Volume (LPPV) binds one additional global sampler
        // (unity_ProbeVolumeSH, a filtered 3D texture) but ONLY in the
        // `UNITY_LIGHT_PROBE_PROXY_VOLUME` ForwardBase variant, and only while a
        // renderer is actually driven by an LPPV component. It is a global,
        // tier-driven keyword rather than a per-material feature toggle, so it
        // is not part of the FeatureCosts table below and is not added to the
        // per-material estimate. The BaseSamplerCount headroom (3 of 16) covers
        // this worst-case +1. See Documentation~/LPPV_SUPPORT.md.
        public const int LppvProbeVolumeSamplerCost = 1;

        private const int LightVolumeLtcgiReserve = 3;
        private const int CriticalLightingReserve = 1;
        private const int ScreenSpaceLightingReserve = 2;
        private const int SharedGrabPassSamplerCost = 1;
        private const int SharedDepthTextureSamplerCost = 1;
        private const int ScreenEdgeDepthNormalsSamplerCost = 1;
        private const int PcssShadowMapSamplerCost = 1;

        public readonly struct FeatureCost
        {
            public readonly string Keyword;
            public readonly string LabelJa;
            public readonly string LabelEn;
            public readonly int SamplerCost;

            public FeatureCost(string keyword, string labelJa, string labelEn, int samplerCost)
            {
                Keyword = keyword;
                LabelJa = labelJa;
                LabelEn = labelEn;
                SamplerCost = samplerCost;
            }
        }

        public readonly struct SamplerBudgetEstimate
        {
            public readonly int BaseSamplers;
            public readonly int EstimatedSamplers;
            public readonly int Limit;
            public readonly FeatureCost[] Contributors;
            public readonly bool HasLightVolumeLtcgiCombo;
            public readonly bool HasCriticalLightingCombo;
            public readonly bool HasScreenSpaceLightingCombo;
            public readonly bool UsesScreenEdgeSplitVariant;
            public readonly int ExtraPassCount;

            public SamplerBudgetEstimate(
                int baseSamplers,
                int estimatedSamplers,
                int limit,
                FeatureCost[] contributors,
                bool hasLightVolumeLtcgiCombo,
                bool hasCriticalLightingCombo,
                bool hasScreenSpaceLightingCombo,
                bool usesScreenEdgeSplitVariant,
                int extraPassCount)
            {
                BaseSamplers = baseSamplers;
                EstimatedSamplers = estimatedSamplers;
                Limit = limit;
                Contributors = contributors ?? new FeatureCost[0];
                HasLightVolumeLtcgiCombo = hasLightVolumeLtcgiCombo;
                HasCriticalLightingCombo = hasCriticalLightingCombo;
                HasScreenSpaceLightingCombo = hasScreenSpaceLightingCombo;
                UsesScreenEdgeSplitVariant = usesScreenEdgeSplitVariant;
                ExtraPassCount = extraPassCount;
            }

            public int OptionalSamplers => Mathf.Max(0, EstimatedSamplers - BaseSamplers);
            public bool IsWarning => EstimatedSamplers >= WarningThreshold;
            public bool IsNearLimit => EstimatedSamplers >= NearLimitThreshold;
            public bool IsAtLimit => EstimatedSamplers == Limit;
            public bool IsOverLimit => EstimatedSamplers > Limit;
        }

        public readonly struct ToggleEvaluation
        {
            public readonly bool CanEnable;
            public readonly int AddedSamplers;
            public readonly SamplerBudgetEstimate Current;
            public readonly SamplerBudgetEstimate AfterEnable;

            public ToggleEvaluation(bool canEnable, int addedSamplers, SamplerBudgetEstimate current, SamplerBudgetEstimate afterEnable)
            {
                CanEnable = canEnable;
                AddedSamplers = addedSamplers;
                Current = current;
                AfterEnable = afterEnable;
            }
        }

        private static readonly FeatureCost[] FeatureCosts =
        {
            new FeatureCost("_2ND_TEXTURE", "2nd Texture", "2nd Texture", 0),
            new FeatureCost("_3RD_TEXTURE", "3rd Texture", "3rd Texture", 0),
            new FeatureCost("_4TH_TEXTURE", "4th Texture", "4th Texture", 0),
            new FeatureCost("_5TH_TEXTURE", "5th Texture", "5th Texture", 0),
            new FeatureCost("_SCREEN_TONE", "Screen Tone", "Screen Tone", 0),
            new FeatureCost("_USE_AO", "AO", "Ambient Occlusion", 0),
            new FeatureCost("_SPECULAR", "Specular", "Specular", 0),
            new FeatureCost("_HAIR_SPECULAR", "Hair Specular", "Hair Specular", 0),
            new FeatureCost("_RIM_LIGHT", "Rim Light", "Rim Light", 0),
            new FeatureCost("_RIM_LIGHT_2", "Rim Light 2", "Rim Light 2", 0),
            new FeatureCost("_OFFSET_RIM_LIGHT", "Offset Rim Light", "Offset Rim Light", 0),
            new FeatureCost("_SHEEN", "Sheen", "Sheen", 0),
            new FeatureCost("_MATCAP", "MatCap", "MatCap", 0),
            new FeatureCost("_MATCAP_2", "MatCap 2", "MatCap 2", 0),
            new FeatureCost("_MATCAP_3", "MatCap 3", "MatCap 3", 0),
            new FeatureCost("_GLITTER", "Glitter", "Glitter", 0),
            new FeatureCost("_OUTLINE", "Outline", "Outline", 1),
            new FeatureCost("_EMISSION", "Emission", "Emission", 0),
            new FeatureCost("_NORMALMAP", "Normal Map", "Normal Map", 0),
            new FeatureCost("_SSS", "SSS", "SSS", 0),
            new FeatureCost("_SSS_LUT", "SSS LUT", "SSS LUT", 0),
            new FeatureCost("_DISSOLVE", "Dissolve", "Dissolve", 0),
            // Shadow Shape Rig: マスク 1 枚。NOSAMPLER で _MainTex のサンプラーを
            // 共有するため、増えるのはテクスチャスロットだけでサンプラーは増えない。
            new FeatureCost("_SHADOW_SHAPE_RIG", "影シェイプリグ", "Shadow Shape Rig", 0),
            new FeatureCost("_ALPHA_MASK", "Alpha Mask", "Alpha Mask", 0),
            new FeatureCost("_REFLECTION", "Reflection", "Reflection", 1),
            new FeatureCost("_IRIDESCENCE", "Iridescence", "Iridescence", 0),
            new FeatureCost("_ENV_RIM", "Environmental Rim", "Environmental Rim", 1),
            new FeatureCost("_PARALLAX", "Parallax", "Parallax", 0),
            new FeatureCost("_REFRACTION", "Refraction", "Refraction", 1),
            new FeatureCost("_SOFT_FILTER", "Soft Filter", "Soft Filter", 1),
            new FeatureCost("_KUWAHARA_FILTER", "Kuwahara", "Kuwahara", 1),
            new FeatureCost("_COLOR_BLEEDING", "Color Bleeding", "Color Bleeding", 1),
            new FeatureCost("_CHROMATIC_ABERRATION", "Chromatic Aberration", "Chromatic Aberration", 1),
            new FeatureCost("_DECAL", "Decal", "Decal", 0),
            new FeatureCost("_BACKFACE_TEXTURE", "Backface Texture", "Backface Texture", 0),
            new FeatureCost("_VIDEO_TEXTURE", "Video Texture", "Video Texture", 1),
            new FeatureCost("_VERTEX_ANIMATION", "Vertex Animation", "Vertex Animation", 0),
            new FeatureCost("_WATER_DRIP", "Water Drip", "Water Drip", 0),
            new FeatureCost("_SMEAR", "Smear", "Smear", 0),
            new FeatureCost("_FUR", "Fur", "Fur", 1),
            new FeatureCost("_PBR", "PBR", "PBR", 0),
            new FeatureCost("_SMOOTH_NORMAL", "Smooth Normal", "Smooth Normal", 0),
            new FeatureCost("_HOLOGRAM", "Hologram", "Hologram", 0),
            new FeatureCost("_HOLOGRAM_NOISE", "Hologram Noise", "Hologram Noise", 0),
            new FeatureCost("_GLITCH", "Glitch", "Glitch", 0),
            new FeatureCost("_GLITCH_STRETCH", "Stretch Glitch", "Stretch Glitch", 0),
            new FeatureCost("_COLOR_QUANTIZE", "Color Quantize", "Color Quantize", 0),
            new FeatureCost("_LUT_3D", "LUT", "LUT", 0),
            new FeatureCost("_HATCHING", "Hatching", "Hatching", 0),
            new FeatureCost("_WATERCOLOR", "Watercolor", "Watercolor", 1),
            new FeatureCost("_SCREEN_EDGE", "Screen Edge", "Screen Edge", 2),
            new FeatureCost("_INTERSECTION_FADE", "Intersection Fade", "Intersection Fade", 1),
            new FeatureCost("_PCSS", "PCSS", "PCSS", 2),
            new FeatureCost("_DETAIL_MAP", "Detail Map", "Detail Map", 0),
            new FeatureCost("_SURFACE_COVER", "Surface Cover", "Surface Cover", 0),
            new FeatureCost("_AUDIOLINK", "AudioLink", "AudioLink", 1),
            new FeatureCost("_VAT", "VAT", "VAT", 0),
            new FeatureCost("_TESS_DISPLACEMENT", "Displacement", "Displacement", 0),
            new FeatureCost("_USE_LIGHT_VOLUME", "Light Volume", "Light Volume", 1),
            new FeatureCost("_LTCGI", "LTCGI", "LTCGI", 1)
        };

        private static readonly FeatureCost LightVolumeLtcgiReserveCost =
            new FeatureCost("__LIGHT_VOLUME_LTCGI_RESERVE", "Light Volume + LTCGI Reserve", "Light Volume + LTCGI Reserve", LightVolumeLtcgiReserve);

        private static readonly FeatureCost CriticalLightingReserveCost =
            new FeatureCost("__CRITICAL_LIGHTING_RESERVE", "Critical Lighting Reserve", "Critical Lighting Reserve", CriticalLightingReserve);

        private static readonly FeatureCost ScreenSpaceLightingReserveCost =
            new FeatureCost("__SCREEN_SPACE_LIGHTING_RESERVE", "Screen Edge Reserve", "Screen Edge Reserve", ScreenSpaceLightingReserve);

        private static readonly string[] SharedGrabPassKeywords =
        {
            "_REFRACTION",
            "_SOFT_FILTER",
            "_KUWAHARA_FILTER",
            "_COLOR_BLEEDING",
            "_CHROMATIC_ABERRATION",
            "_WATERCOLOR"
        };

        private static readonly string[] SharedDepthTextureKeywords =
        {
            "_INTERSECTION_FADE",
            "_SCREEN_EDGE",
            "_PCSS"
        };

        private static readonly FeatureCost SharedGrabPassCost =
            new FeatureCost("__SHARED_GRABPASS", "GrabPass", "GrabPass", SharedGrabPassSamplerCost);

        private static readonly FeatureCost SharedDepthTextureCost =
            new FeatureCost("__SHARED_DEPTH_TEXTURE", "Depth Texture", "Depth Texture", SharedDepthTextureSamplerCost);

        private static readonly FeatureCost ScreenEdgeDepthNormalsCost =
            new FeatureCost("__SCREEN_EDGE_DEPTH_NORMALS", "Depth Normals", "Depth Normals", ScreenEdgeDepthNormalsSamplerCost);

        private static readonly FeatureCost PcssShadowMapCost =
            new FeatureCost("__PCSS_SHADOWMAP", "Shadow Map", "Shadow Map", PcssShadowMapSamplerCost);

        // ===== Shared estimate cache =====
        // Estimate() はインスペクターの複数のドロワーから毎リペイント呼ばれる。
        // キーワード状態のビットマスク(fingerprint)が変わらない限り、
        // List/配列の確保・ソートを含む再計算を省いて前回結果を使い回す。

        private struct CachedEstimate
        {
            public ulong Fingerprint;
            public int ShaderInstanceId;
            public SamplerBudgetEstimate Estimate;
        }

        private static readonly Dictionary<int, CachedEstimate> _estimateCache = new Dictionary<int, CachedEstimate>();
        private const int EstimateCacheCapacity = 64;

        /// <summary>
        /// FeatureCosts のキーワード有効状態を1ビットずつ詰めたフィンガープリント。
        /// アロケーションなしで「前回から機能構成が変わったか」を判定できる。
        /// FeatureCosts が64件を超えたらビット割当を見直すこと（現在61件）。
        /// </summary>
        private static ulong ComputeFingerprint(Material material)
        {
            ulong mask = 0UL;
            for (int i = 0; i < FeatureCosts.Length; i++)
            {
                if (material.IsKeywordEnabled(FeatureCosts[i].Keyword))
                {
                    mask |= 1UL << (i & 63);
                }
            }
            return mask;
        }

        public static SamplerBudgetEstimate Estimate(Material material)
        {
            if (material == null)
            {
                return Estimate(material, null, false);
            }

            int materialId = material.GetInstanceID();
            int shaderId = material.shader != null ? material.shader.GetInstanceID() : 0;
            ulong fingerprint = ComputeFingerprint(material);

            CachedEstimate cached;
            if (_estimateCache.TryGetValue(materialId, out cached) &&
                cached.Fingerprint == fingerprint &&
                cached.ShaderInstanceId == shaderId)
            {
                return cached.Estimate;
            }

            SamplerBudgetEstimate estimate = Estimate(material, null, false);

            if (_estimateCache.Count >= EstimateCacheCapacity)
            {
                _estimateCache.Clear();
            }
            _estimateCache[materialId] = new CachedEstimate
            {
                Fingerprint = fingerprint,
                ShaderInstanceId = shaderId,
                Estimate = estimate
            };
            return estimate;
        }

        public static SamplerBudgetEstimate Estimate(Material material, string overrideKeyword, bool overrideEnabled)
        {
            if (material == null)
            {
                return new SamplerBudgetEstimate(0, 0, SamplerLimit, new FeatureCost[0], false, false, false, false, 0);
            }

            int total = BaseSamplerCount;
            var contributors = new List<FeatureCost>();
            bool usesScreenEdgeSplitVariant = material.shader != null &&
                material.shader.name == ScreenEdgeSplitShaderName;

            for (int i = 0; i < FeatureCosts.Length; i++)
            {
                FeatureCost feature = FeatureCosts[i];
                bool isEnabled = material.IsKeywordEnabled(feature.Keyword);
                if (!string.IsNullOrEmpty(overrideKeyword) && feature.Keyword == overrideKeyword)
                {
                    isEnabled = overrideEnabled;
                }

                if (usesScreenEdgeSplitVariant && feature.Keyword == "_SCREEN_EDGE")
                {
                    isEnabled = false;
                }

                if (UsesSharedGrabPassSampler(feature.Keyword) || UsesSharedDepthTextureSampler(feature.Keyword))
                {
                    continue;
                }

                if (!isEnabled || feature.SamplerCost <= 0)
                {
                    continue;
                }

                total += feature.SamplerCost;
                contributors.Add(feature);
            }

            bool hasLightVolume = IsKeywordEnabled(material, "_USE_LIGHT_VOLUME", overrideKeyword, overrideEnabled);
            bool hasLtcgi = IsKeywordEnabled(material, "_LTCGI", overrideKeyword, overrideEnabled);
            bool hasHatching = IsKeywordEnabled(material, "_HATCHING", overrideKeyword, overrideEnabled);
            bool hasGrabPassConsumer = AnyKeywordEnabled(material, SharedGrabPassKeywords, overrideKeyword, overrideEnabled);
            bool hasScreenEdge = !usesScreenEdgeSplitVariant && IsKeywordEnabled(material, "_SCREEN_EDGE", overrideKeyword, overrideEnabled);
            bool hasPcss = IsKeywordEnabled(material, "_PCSS", overrideKeyword, overrideEnabled);
            bool hasDepthTextureConsumer =
                IsKeywordEnabled(material, "_INTERSECTION_FADE", overrideKeyword, overrideEnabled) ||
                hasScreenEdge ||
                hasPcss;
            int extraPassCount = usesScreenEdgeSplitVariant ? 1 : 0;

            if (hasGrabPassConsumer)
            {
                total += SharedGrabPassSamplerCost;
                contributors.Add(SharedGrabPassCost);
            }

            if (hasDepthTextureConsumer)
            {
                total += SharedDepthTextureSamplerCost;
                contributors.Add(SharedDepthTextureCost);
            }

            if (hasScreenEdge)
            {
                total += ScreenEdgeDepthNormalsSamplerCost;
                contributors.Add(ScreenEdgeDepthNormalsCost);
            }

            if (hasPcss)
            {
                total += PcssShadowMapSamplerCost;
                contributors.Add(PcssShadowMapCost);
            }

            bool hasLightVolumeLtcgiCombo = hasLightVolume && hasLtcgi;
            bool hasCriticalLightingCombo = hasLightVolumeLtcgiCombo && hasHatching;
            bool hasScreenSpaceLightingCombo = hasLightVolumeLtcgiCombo && hasScreenEdge && !usesScreenEdgeSplitVariant;

            if (hasLightVolumeLtcgiCombo)
            {
                total += LightVolumeLtcgiReserve;
                contributors.Add(LightVolumeLtcgiReserveCost);
            }

            if (hasCriticalLightingCombo)
            {
                total += CriticalLightingReserve;
                contributors.Add(CriticalLightingReserveCost);
            }

            if (hasScreenSpaceLightingCombo)
            {
                total += ScreenSpaceLightingReserve;
                contributors.Add(ScreenSpaceLightingReserveCost);
            }

            contributors.Sort((left, right) =>
            {
                int byCost = right.SamplerCost.CompareTo(left.SamplerCost);
                return byCost != 0 ? byCost : string.CompareOrdinal(left.Keyword, right.Keyword);
            });

            return new SamplerBudgetEstimate(
                BaseSamplerCount,
                total,
                SamplerLimit,
                contributors.ToArray(),
                hasLightVolumeLtcgiCombo,
                hasCriticalLightingCombo,
                hasScreenSpaceLightingCombo,
                usesScreenEdgeSplitVariant,
                extraPassCount);
        }

        public static ToggleEvaluation EvaluateEnable(Material material, string keyword)
        {
            SamplerBudgetEstimate current = Estimate(material);

            if (!TryGetFeatureCost(keyword, out FeatureCost feature) || feature.SamplerCost <= 0)
            {
                return new ToggleEvaluation(true, 0, current, current);
            }

            if (material != null && material.IsKeywordEnabled(keyword))
            {
                return new ToggleEvaluation(true, 0, current, current);
            }

            SamplerBudgetEstimate afterEnable = Estimate(material, keyword, true);
            bool canEnable = afterEnable.EstimatedSamplers <= SamplerLimit;
            int addedSamplers = Mathf.Max(0, afterEnable.EstimatedSamplers - current.EstimatedSamplers);
            if (current.UsesScreenEdgeSplitVariant && keyword == "_SCREEN_EDGE")
            {
                addedSamplers = 0;
            }

            return new ToggleEvaluation(canEnable, addedSamplers, current, afterEnable);
        }

        public static bool TryGetFeatureCost(string keyword, out FeatureCost feature)
        {
            for (int i = 0; i < FeatureCosts.Length; i++)
            {
                if (FeatureCosts[i].Keyword == keyword)
                {
                    feature = FeatureCosts[i];
                    return true;
                }
            }

            feature = default;
            return false;
        }

        public static string GetDisplayName(FeatureCost feature)
        {
            return L(feature.LabelJa, feature.LabelEn);
        }

        public static string FormatContributor(FeatureCost feature)
        {
            return $"{GetDisplayName(feature)} +{feature.SamplerCost}";
        }

        private static bool IsKeywordEnabled(Material material, string keyword, string overrideKeyword, bool overrideEnabled)
        {
            if (material == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(overrideKeyword) && keyword == overrideKeyword)
            {
                return overrideEnabled;
            }

            return material.IsKeywordEnabled(keyword);
        }

        private static bool AnyKeywordEnabled(Material material, string[] keywords, string overrideKeyword, bool overrideEnabled)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (IsKeywordEnabled(material, keywords[i], overrideKeyword, overrideEnabled))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool UsesSharedGrabPassSampler(string keyword)
        {
            return ArrayContains(SharedGrabPassKeywords, keyword);
        }

        private static bool UsesSharedDepthTextureSampler(string keyword)
        {
            return ArrayContains(SharedDepthTextureKeywords, keyword);
        }

        private static bool ArrayContains(string[] values, string keyword)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == keyword)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
