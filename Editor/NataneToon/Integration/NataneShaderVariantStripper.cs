using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// ビルド時に、プロジェクト内のどの Natane マテリアルからも使用されていない
    /// shader_feature_local キーワードを含むコンパイル済みバリアントを破棄する。
    ///
    /// lilToon 方式の #undef ガード (NataneBuildFeatureOptimizer) がソースレベルで
    /// 未使用機能を無効化するのに対し、こちらは Unity のシェーダーバリアント
    /// コンパイルパイプライン (IPreprocessShaders) 側で、実際にコンパイルされる
    /// バリアント数そのものを削減する。両者は補完関係にある。
    ///
    /// - 収集対象: プロジェクト内の全 Natane マテリアルで有効な既知キーワードの和集合。
    /// - 判定対象: 既知の Natane キーワードのみ。multi_compile 由来のビルトイン
    ///   キーワード (DIRECTIONAL / SHADOWS_SCREEN / INSTANCING_ON 等) には一切触れない。
    /// - 有効判定: NataneBuildPolicySettings.VariantStrippingEnabled（既定 true）。
    ///   旧 EditorPrefs["NataneToon_DisableVariantStripping"] は初回移行で反転取込み済み。
    /// </summary>
    public sealed class NataneShaderVariantStripper : IPreprocessShaders, IPreprocessBuildWithReport
    {
        /// <summary>
        /// EditorPrefs のオプトアウトキー。true にするとバリアントストリップを無効化する。
        /// </summary>
        public const string OptOutPrefKey = "NataneToon_DisableVariantStripping";

        /// <summary>
        /// KeywordMappings に含まれない追加キーワード（NataneBuildFeatureOptimizer と揃える）。
        /// </summary>
        private static readonly string[] ExtraKeywords = { "_EYE_PARALLAX" };

        // シェーダーストリッパーは最後の方で実行して、他のストリッパーの結果を尊重する
        public int callbackOrder => 100;

        // ビルドセッション内でキャッシュする使用キーワード集合（null = 未計算 / 再計算が必要）
        private static HashSet<string> _usedKeywords;
        private static HashSet<string> _knownKeywords;

        // レポート用の集計値（NataneBuildOptimizationReport から参照される）
        internal static int TotalVariantsSeen;
        internal static int VariantsStripped;
        internal static bool RanThisBuild;

        /// <summary>
        /// 通常のプレイヤー/アセットバンドルビルドでは各ビルド開始時に状態をリセットする。
        /// （VRChat のアバタービルドではこのコールバックは呼ばれないため、
        ///  その場合は下の遅延初期化で最新のマテリアル状態から計算される。）
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            ResetState();
        }

        internal static void ResetState()
        {
            _usedKeywords = null;
            _knownKeywords = null;
            TotalVariantsSeen = 0;
            VariantsStripped = 0;
            RanThisBuild = false;
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            // 統合ストリッパー(NataneUnifiedVariantStripper, callbackOrder=110)へ移行済み。
            // 本差分式ストリッパーの IPreprocessShaders 経路は無効化する（即 return）。
            // クラスは後方互換とレビュー容易性のため残置し、静的ヘルパ
            // (CollectUsedKeywords/BuildKnownKeywordSet/EnumerateNataneMaterials) は
            // NataneBuildOptimizationReport 等が引き続き利用する。
            return;
#pragma warning disable CS0162 // 到達不能コード（移行済みの旧実装を保全）
            // 有効判定は設定資産(variantStrippingEnabled)を正とする。
            // 旧 EditorPrefs は初回移行で設定資産へ取り込み済み（以降は資産が唯一の情報源）。
            if (!NataneBuildPolicySettings.instance.VariantStrippingEnabled)
                return;

            // Natane シェーダー以外は一切触れない
            if (shader == null || !NataneShaderCatalog.IsNataneShader(shader.name))
                return;

            if (data == null || data.Count == 0)
                return;

            try
            {
                EnsureKeywordSets();
            }
            catch (Exception ex)
            {
                // 収集に失敗した場合はストリップせず全バリアントを残す（ビルドを止めない）
                Debug.LogWarning($"[NataneToonShader] バリアントストリップ用キーワード収集に失敗。全バリアントを維持します: {ex.Message}");
                return;
            }

            RanThisBuild = true;

            int before = data.Count;
            TotalVariantsSeen += before;

            for (int i = data.Count - 1; i >= 0; i--)
            {
                if (ShouldStripVariant(data[i]))
                {
                    data.RemoveAt(i);
                    VariantsStripped++;
                }
            }

            int after = data.Count;
            if (before != after)
            {
                Debug.Log(
                    $"[NataneToonShader] バリアントストリップ: {shader.name} " +
                    $"[{snippet.passType} / {snippet.shaderType}] {before} → {after} " +
                    $"({before - after} 破棄)");
            }
#pragma warning restore CS0162
        }

        /// <summary>
        /// このバリアントが、いずれの Natane マテリアルからも使われていない
        /// 既知キーワードを有効化している場合 true（=破棄すべき）を返す。
        /// </summary>
        private static bool ShouldStripVariant(ShaderCompilerData variant)
        {
            UnityEngine.Rendering.ShaderKeyword[] keywords = variant.shaderKeywordSet.GetShaderKeywords();
            foreach (UnityEngine.Rendering.ShaderKeyword keyword in keywords)
            {
                string name = keyword.name;
                if (string.IsNullOrEmpty(name))
                    continue;

                // 既知の Natane キーワードのみ判定対象。ビルトインキーワードは無視。
                if (!_knownKeywords.Contains(name))
                    continue;

                // 有効化されているが、どのマテリアルでも使われていない → 破棄
                if (!_usedKeywords.Contains(name))
                    return true;
            }

            return false;
        }

        private static void EnsureKeywordSets()
        {
            if (_knownKeywords == null)
                _knownKeywords = BuildKnownKeywordSet();

            if (_usedKeywords == null)
                _usedKeywords = CollectUsedKeywords();
        }

        internal static HashSet<string> BuildKnownKeywordSet()
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
                known.Add(mapping.keyword);
            foreach (string extra in ExtraKeywords)
                known.Add(extra);
            return known;
        }

        /// <summary>
        /// プロジェクト内の全 Natane マテリアルで有効な既知キーワードの和集合を収集する。
        /// キーワードのズレに備え、Toggle プロパティ値 (>= 0.5) とキーワード状態の
        /// どちらかが有効ならば「使用中」とみなす。
        /// </summary>
        internal static HashSet<string> CollectUsedKeywords()
        {
            var used = new HashSet<string>(StringComparer.Ordinal);

            foreach (Material material in EnumerateNataneMaterials())
            {
                if (material == null || material.shader == null)
                    continue;

                foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
                {
                    bool propertyOn = material.HasProperty(mapping.propertyName) &&
                                      material.GetFloat(mapping.propertyName) >= 0.5f;
                    bool keywordOn = material.IsKeywordEnabled(mapping.keyword);
                    if (propertyOn || keywordOn)
                        used.Add(mapping.keyword);
                }

                if ((material.HasProperty("_EyeParallax") && material.GetFloat("_EyeParallax") >= 0.5f) ||
                    material.IsKeywordEnabled("_EYE_PARALLAX"))
                {
                    used.Add("_EYE_PARALLAX");
                }
            }

            return used;
        }

        /// <summary>
        /// Natane マテリアルを列挙する。アセットインデックスを優先し、
        /// 空の場合は AssetDatabase の全走査にフォールバックする。
        /// </summary>
        internal static IEnumerable<Material> EnumerateNataneMaterials()
        {
            List<MaterialIndexEntry> entries = null;
            try
            {
                NataneAssetIndexService.EnsureLoaded();
                entries = NataneAssetIndexService
                    .EnumerateMaterialEntries(e => e.isNataneShader)
                    .ToList();
            }
            catch
            {
                entries = null;
            }

            if (entries != null && entries.Count > 0)
            {
                foreach (var entry in entries)
                    yield return AssetDatabase.LoadAssetAtPath<Material>(entry.path);
                yield break;
            }

            // フォールバック: プロジェクト全走査
            string[] guids = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && material.shader != null &&
                    NataneShaderCatalog.IsNataneShader(material.shader.name))
                {
                    yield return material;
                }
            }
        }
    }
}
