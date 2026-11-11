using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using L = NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// ビルド完了後に、Natane 関連の最適化サマリをコンソールとテキストファイルに出力する。
    /// - 破棄されたシェーダーバリアント数 (NataneShaderVariantStripper)
    /// - NataneBuildFeatureOptimizer によって無効化された機能
    /// - Lite (GrabPass 無し) 版へ変換可能なマテリアル
    /// - テクスチャインポート設定の改善提案 (>2048 / 未圧縮 など) ※提案のみ
    ///
    /// VRChat のアバタービルドでは IPostprocessBuildWithReport は呼ばれないため、
    /// 主にプレイヤー/アセットバンドルビルド向けのレポート。VRChat 向けの Lite 提案は
    /// NataneVRChatBuildHook 側でも出力される。
    /// </summary>
    public sealed class NataneBuildOptimizationReport : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        private const string ReportFileName = "NataneBuildOptimizationReport.txt";

        // GrabPass を必要とする機能キーワード。これらが 1 つも有効でなければ Lite 版へ変換可能。
        // （NataneToonShader.shader の GrabPass 注記と一致させること）
        public static readonly string[] GrabPassKeywords =
        {
            "_REFRACTION",
            "_SOFT_FILTER",
            "_KUWAHARA_FILTER",
            "_COLOR_BLEEDING",
            "_CHROMATIC_ABERRATION",
        };

        // フル版シェーダー名 → 対応する Lite 版シェーダー名。
        public static readonly Dictionary<string, string> FullToLiteShader =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Natane/Toon Shader", "Natane/Toon Shader (Lite)" },
            { "Natane/Toon Shader (Cutout)", "Natane/Toon Shader (Cutout Lite)" },
            { "Natane/Toon Shader (Transparent)", "Natane/Toon Shader (Transparent Lite)" },
            { "Natane/Toon Shader (Fur)", "Natane/Toon Shader (Fur Lite)" },
        };

        private const int LargeTextureThreshold = 2048;

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                string text = BuildReportText(report);
                Debug.Log(text);
                WriteReportFile(text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NataneToonShader] ビルド最適化レポートの生成に失敗: {ex.Message}");
            }
        }

        private static string BuildReportText(BuildReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine(L.L("[NataneToonShader] ビルド最適化レポート",
                              "[NataneToonShader] Build Optimization Report"));
            sb.AppendLine($"  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (report != null && report.summary.platform != BuildTarget.NoTarget)
                sb.AppendLine($"  Platform: {report.summary.platform}");
            sb.AppendLine("========================================");

            AppendVariantStrippingSection(sb);
            AppendFeatureOptimizerSection(sb);
            AppendLiteConversionSection(sb);
            AppendTextureConsolidationSection(sb);
            AppendTextureSuggestionSection(sb);

            sb.AppendLine("========================================");
            return sb.ToString();
        }

        private static void AppendVariantStrippingSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine(L.L("■ シェーダーバリアントのストリップ",
                              "■ Shader Variant Stripping"));

            // 統合ストリッパー(NataneUnifiedVariantStripper)の集計を参照する。
            // 有効判定は設定資産(VariantStrippingEnabled)を正とする（旧 EditorPrefs オプトアウトは廃止）。
            if (!NataneBuildPolicySettings.instance.VariantStrippingEnabled)
            {
                sb.AppendLine(L.L("  (無効化されています: VariantStrippingEnabled=false)",
                                  "  (disabled via VariantStrippingEnabled=false)"));
                return;
            }

            if (!NataneStrippingSessionReport.HasData)
            {
                sb.AppendLine(L.L("  このビルドではバリアント処理が実行されませんでした。",
                                  "  Variant processing did not run for this build."));
                return;
            }

            int seen = NataneStrippingSessionReport.TotalSeen;
            int stripped = NataneStrippingSessionReport.TotalStripped;
            int kept = NataneStrippingSessionReport.TotalKept;
            sb.AppendLine(L.L($"  最適化モード: {NataneBuildPolicySettings.instance.OptimizationMode}",
                              $"  Optimization mode: {NataneBuildPolicySettings.instance.OptimizationMode}"));
            sb.AppendLine(L.L($"  処理対象バリアント: {seen}", $"  Variants processed: {seen}"));
            sb.AppendLine(L.L($"  破棄したバリアント: {stripped}", $"  Variants stripped:  {stripped}"));
            sb.AppendLine(L.L($"  残したバリアント:   {kept}", $"  Variants kept:      {kept}"));
            sb.AppendLine(L.L($"  削除候補(Safe基準): {NataneStrippingSessionReport.TotalCandidates}",
                              $"  Strip candidates (Safe): {NataneStrippingSessionReport.TotalCandidates}"));
        }

        private static void AppendFeatureOptimizerSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine(L.L("■ 機能の無効化 (BuildFeatureOptimizer)",
                              "■ Features Disabled (BuildFeatureOptimizer)"));

            HashSet<string> known = NataneShaderVariantStripper.BuildKnownKeywordSet();
            HashSet<string> used = NataneShaderVariantStripper.CollectUsedKeywords();

            var disabled = known.Where(k => !used.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            sb.AppendLine(L.L($"  有効な機能: {used.Count} / {known.Count}",
                              $"  Enabled features: {used.Count} / {known.Count}"));
            sb.AppendLine(L.L($"  無効化された機能: {disabled.Count}",
                              $"  Disabled features: {disabled.Count}"));
            foreach (string keyword in disabled)
                sb.AppendLine($"    - {keyword}");
        }

        private static void AppendLiteConversionSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine(L.L("■ Lite (GrabPass 無し) へ変換可能なマテリアル",
                              "■ Materials Convertible to Lite (GrabPass-free)"));

            List<LiteCandidate> candidates = FindLiteConvertibleMaterials();
            if (candidates.Count == 0)
            {
                sb.AppendLine(L.L("  該当なし（または全て GrabPass 機能を使用中）。",
                                  "  None (or all use GrabPass features)."));
                return;
            }

            sb.AppendLine(L.L($"  {candidates.Count} 個のマテリアルが Lite 版へ変換できます（GrabPass を削減）:",
                              $"  {candidates.Count} material(s) can switch to a Lite shader (removes GrabPass):"));
            foreach (var c in candidates)
                sb.AppendLine($"    - {c.materialPath}  [{c.currentShader} → {c.liteShader}]");
        }

        private static void AppendTextureConsolidationSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine(L.L("■ テクスチャ統合（完全一致の重複を検出）",
                              "■ Texture Consolidation (byte-identical duplicates)"));

            NataneTextureConsolidator.ConsolidationResult result;
            try
            {
                // レポートは検出のみ（近似一致は O(n^2) のためスキップ）。実際の統合は
                // VRChat アップロード時 (NataneVRChatTextureConsolidation) に行われる。
                result = NataneTextureConsolidator.Analyze(
                    NataneShaderVariantStripper.EnumerateNataneMaterials(),
                    includeNearIdentical: false);
            }
            catch (Exception ex)
            {
                sb.AppendLine(L.L($"  解析に失敗しました: {ex.Message}",
                                  $"  Analysis failed: {ex.Message}"));
                return;
            }

            sb.AppendLine(L.L(
                $"  スキャンしたマテリアル: {result.materialsScanned}  テクスチャ: {result.texturesScanned}",
                $"  Materials scanned: {result.materialsScanned}  Textures: {result.texturesScanned}"));

            if (result.ExactDuplicateGroupCount == 0)
            {
                sb.AppendLine(L.L("  完全一致の重複テクスチャはありません。",
                                  "  No byte-identical duplicate textures found."));
            }
            else
            {
                sb.AppendLine(L.L(
                    $"  重複グループ: {result.ExactDuplicateGroupCount}  余剰テクスチャ: {result.ExactDuplicateRedundantCount}  推定 VRAM 削減: {NataneTextureConsolidator.FormatBytes(result.ExactDuplicateSavingsBytes)}",
                    $"  Duplicate groups: {result.ExactDuplicateGroupCount}  Redundant textures: {result.ExactDuplicateRedundantCount}  Estimated VRAM saved: {NataneTextureConsolidator.FormatBytes(result.ExactDuplicateSavingsBytes)}"));
                foreach (var group in result.exactDuplicateGroups)
                {
                    sb.AppendLine(L.L($"    - 正規: {group.canonicalPath}",
                                      $"    - canonical: {group.canonicalPath}"));
                    foreach (var tr in group.textures)
                    {
                        if (tr.isCanonical)
                            continue;
                        sb.AppendLine($"        = {tr.path}");
                    }
                }
                sb.AppendLine(L.L(
                    "  ※ 実際の統合は VRChat アップロード時にクローン上で行われます（プロジェクト資産は非変更）。",
                    "  * Actual merging happens on the avatar clone during VRChat upload (project assets untouched)."));
            }

            if (result.oversizedMaskGroups.Count > 0)
            {
                sb.AppendLine(L.L($"  過大なマスク（提案のみ）: {result.oversizedMaskGroups.Count}",
                                  $"  Oversized masks (advisory): {result.oversizedMaskGroups.Count}"));
                foreach (var group in result.oversizedMaskGroups)
                    sb.AppendLine($"    - {group.canonicalPath}: {group.note}");
            }
        }

        private static void AppendTextureSuggestionSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine(L.L("■ テクスチャインポートの改善提案（提案のみ・自動変更なし）",
                              "■ Texture Import Suggestions (advisory only, nothing changed)"));

            List<string> suggestions = CollectTextureSuggestions();
            if (suggestions.Count == 0)
            {
                sb.AppendLine(L.L("  改善提案はありません。", "  No suggestions."));
                return;
            }

            foreach (string s in suggestions)
                sb.AppendLine($"    - {s}");
        }

        // ---------------------------------------------------------------
        // 共有ロジック（NataneVRChatBuildHook からも利用）
        // ---------------------------------------------------------------

        public struct LiteCandidate
        {
            public string materialPath;
            public string currentShader;
            public string liteShader;
        }

        /// <summary>
        /// GrabPass 機能を 1 つも使っておらず、Lite 版シェーダーが存在するフル版マテリアルを列挙する。
        /// </summary>
        public static List<LiteCandidate> FindLiteConvertibleMaterials()
        {
            var result = new List<LiteCandidate>();
            foreach (Material material in NataneShaderVariantStripper.EnumerateNataneMaterials())
            {
                if (!TryGetLiteConversion(material, out LiteCandidate candidate))
                    continue;
                result.Add(candidate);
            }
            return result;
        }

        /// <summary>
        /// 指定マテリアルが Lite 版へ変換可能なら true を返し、変換情報を出力する。
        /// </summary>
        public static bool TryGetLiteConversion(Material material, out LiteCandidate candidate)
        {
            candidate = default;
            if (material == null || material.shader == null)
                return false;

            string shaderName = material.shader.name;
            if (!FullToLiteShader.TryGetValue(shaderName, out string liteShader))
                return false;

            if (UsesGrabPassFeature(material))
                return false;

            candidate = new LiteCandidate
            {
                materialPath = AssetDatabase.GetAssetPath(material),
                currentShader = shaderName,
                liteShader = liteShader,
            };
            return true;
        }

        /// <summary>
        /// GrabPass を必要とする機能が 1 つでも有効なら true。
        /// </summary>
        public static bool UsesGrabPassFeature(Material material)
        {
            if (material == null)
                return false;

            foreach (string keyword in GrabPassKeywords)
            {
                if (material.IsKeywordEnabled(keyword))
                    return true;
            }

            // キーワードのズレに備え、対応する Toggle プロパティ値でも判定する。
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
            {
                if (Array.IndexOf(GrabPassKeywords, mapping.keyword) < 0)
                    continue;
                if (material.HasProperty(mapping.propertyName) &&
                    material.GetFloat(mapping.propertyName) >= 0.5f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Natane マテリアルが参照するテクスチャをスキャンし、改善提案文字列を収集する（提案のみ）。
        /// </summary>
        public static List<string> CollectTextureSuggestions()
        {
            var suggestions = new List<string>();
            var seenTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Material material in NataneShaderVariantStripper.EnumerateNataneMaterials())
            {
                if (material == null || material.shader == null)
                    continue;

                Shader shader = material.shader;
                int count = ShaderUtil.GetPropertyCount(shader);
                for (int i = 0; i < count; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                        continue;

                    string propName = ShaderUtil.GetPropertyName(shader, i);
                    Texture tex = material.GetTexture(propName);
                    if (tex == null)
                        continue;

                    string texPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(texPath) || !seenTextures.Add(texPath))
                        continue;

                    var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if (importer == null)
                        continue;

                    string suggestion = EvaluateTexture(importer, tex, texPath);
                    if (!string.IsNullOrEmpty(suggestion))
                        suggestions.Add(suggestion);
                }
            }

            return suggestions;
        }

        private static string EvaluateTexture(TextureImporter importer, Texture tex, string texPath)
        {
            var issues = new List<string>();

            if (importer.maxTextureSize > LargeTextureThreshold)
            {
                issues.Add(L.L(
                    $"maxSize={importer.maxTextureSize} (> {LargeTextureThreshold}) → 縮小を検討",
                    $"maxSize={importer.maxTextureSize} (> {LargeTextureThreshold}) → consider downscaling"));
            }

            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                issues.Add(L.L("未圧縮 → 圧縮を検討", "uncompressed → consider compression"));
            }

            if (!importer.mipmapEnabled && importer.textureType == TextureImporterType.Default)
            {
                // ミップマップは必須ではないため軽い注記のみ
                // （ミラー/遠景での品質向上目的。必要な場合のみ）
            }

            if (issues.Count == 0)
                return null;

            return $"{texPath}: {string.Join(", ", issues)}";
        }

        private static void WriteReportFile(string text)
        {
            string dir;
            try
            {
                dir = Path.GetFullPath("Logs");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch
            {
                dir = Path.GetFullPath(".");
            }

            string path = Path.Combine(dir, ReportFileName);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Debug.Log(L.L($"[NataneToonShader] レポートを書き出しました: {path}",
                          $"[NataneToonShader] Report written to: {path}"));
        }
    }
}
