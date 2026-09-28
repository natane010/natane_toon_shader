using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// lilToon 方式のビルド時機能最適化。
    /// ビルド前: プロジェクト内のマテリアル・AnimationClip をスキャンし、
    ///           実際に使用されている機能だけを #define した NataneToonBuildSettings.hlsl を生成。
    ///           未使用キーワードは #undef で強制無効化される。
    /// ビルド後: デフォルト（全機能有効）に復元。
    /// </summary>
    internal sealed class NataneBuildFeatureOptimizer : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // キーワード同期 (-100) の後、他のビルド処理の前に実行
        public int callbackOrder => -90;

        /// <summary>
        /// KeywordMappings に含まれない追加キーワード（派生・特殊用途）。
        /// </summary>
        private static readonly string[] ExtraKeywords =
        {
            // _EYE_PARALLAX moved into NataneShaderKeywordSynchronizer.KeywordMappings.
        };

        // 遅延初期化: static readonly だと AssetDatabase 未準備時に null になる
        private static string _buildSettingsPath;
        private static string BuildSettingsPath
        {
            get
            {
                if (_buildSettingsPath == null)
                    _buildSettingsPath = FindBuildSettingsHlslPath();
                return _buildSettingsPath;
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                // パスキャッシュをクリアして最新状態で検索
                _buildSettingsPath = null;

                var usedFeatures = CollectUsedFeatures();
                WriteBuildSettingsHlsl(usedFeatures);
                ReimportAllNataneShaders();

                Debug.Log($"[NataneToonShader] ビルド時機能最適化: {usedFeatures.Count}/{NataneShaderKeywordSynchronizer.KeywordMappings.Length} 機能を有効化");
            }
            catch (Exception ex)
            {
                // 最適化失敗時はデフォルト（全機能有効）に戻してビルドを止めない
                Debug.LogWarning($"[NataneToonShader] ビルド時機能最適化に失敗しました。全機能有効で続行します: {ex.Message}");
                RestoreDefaultBuildSettings();
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RestoreDefaultBuildSettings();
        }

        /// <summary>
        /// アセットインデックスから Natane マテリアルのみを取得し、
        /// 使用されている機能キーワードを収集する。
        /// AnimationClip はプレハブ経由で参照される可能性があるため、
        /// インデックス内のプレハブが参照するクリップのみをスキャンする。
        /// </summary>
        private static HashSet<string> CollectUsedFeatures()
        {
            var usedKeywords = new HashSet<string>(StringComparer.Ordinal);

            // インデックスを最新に同期
            NataneAssetIndexService.EnsureLoaded();
            NataneAssetIndexService.RunSynchronousCatchUp();

            // 1. マテリアルスキャン（インデックスから Natane マテリアルのみ）
            var nataneEntries = NataneAssetIndexService
                .EnumerateMaterialEntries(e => e.isNataneShader)
                .ToList();

            foreach (var entry in nataneEntries)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(entry.path);
                if (material == null || material.shader == null)
                    continue;

                foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
                {
                    if (!material.HasProperty(mapping.propertyName))
                        continue;

                    if (material.GetFloat(mapping.propertyName) >= 0.5f)
                    {
                        usedKeywords.Add(mapping.keyword);
                    }
                }

            }

            // 2. AnimationClip スキャン
            // Natane マテリアルを参照するプレハブから AnimationClip を収集し、
            // アニメーションで機能の ON/OFF を切り替えるケースに対応。
            var nataneGuids = new HashSet<string>(
                nataneEntries.Select(e => e.guid),
                StringComparer.OrdinalIgnoreCase);

            var prefabs = NataneAssetIndexService.GetPrefabsUsingMaterialGuids(nataneGuids);
            var scannedClipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prefabEntry in prefabs)
            {
                GameObject prefab = NataneAssetIndexService.LoadPrefab(prefabEntry);
                if (prefab == null)
                    continue;

                // プレハブ配下の Animator から AnimationClip を収集
                foreach (var animator in prefab.GetComponentsInChildren<Animator>(true))
                {
                    if (animator.runtimeAnimatorController == null)
                        continue;

                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    {
                        if (clip == null)
                            continue;

                        string clipPath = AssetDatabase.GetAssetPath(clip);
                        if (string.IsNullOrEmpty(clipPath) || !scannedClipPaths.Add(clipPath))
                            continue;

                        ScanAnimationClipForFeatures(clip, usedKeywords);
                    }
                }
            }

            return usedKeywords;
        }

        /// <summary>
        /// AnimationClip のバインディングをスキャンし、Natane の Toggle プロパティが
        /// アニメーションで有効化されるケースを検出する。
        /// </summary>
        private static void ScanAnimationClipForFeatures(AnimationClip clip, HashSet<string> usedKeywords)
        {
            if (clip == null)
                return;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                // マテリアルプロパティのアニメーションは "material._PropertyName" 形式
                if (!binding.propertyName.StartsWith("material.", StringComparison.Ordinal))
                    continue;

                string materialPropertyName = binding.propertyName.Substring("material.".Length);

                // KeywordMappings に存在するプロパティか確認
                foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
                {
                    if (!string.Equals(materialPropertyName, mapping.propertyName, StringComparison.Ordinal))
                        continue;

                    // いずれかのキーフレームで >= 0.5 なら使用と判定
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                    {
                        foreach (var key in curve.keys)
                        {
                            if (key.value >= 0.5f)
                            {
                                usedKeywords.Add(mapping.keyword);
                                break;
                            }
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// 使用機能のみを #define した NataneToonBuildSettings.hlsl を書き出す。
        /// NATANE_BUILD_ALL_FEATURES を定義しないため、#undef ガードが有効になり、
        /// 未使用キーワードが強制無効化される。
        /// </summary>
        private static void WriteBuildSettingsHlsl(HashSet<string> usedKeywords)
        {
            string path = BuildSettingsPath;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[NataneToonShader] NataneToonBuildSettings.hlsl が見つかりません。スキップします。");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("// NataneToonBuildSettings.hlsl");
            sb.AppendLine("// Auto-generated by NataneBuildFeatureOptimizer before build.");
            sb.AppendLine("// DO NOT EDIT - this file will be restored after build completes.");
            sb.AppendLine();
            sb.AppendLine("#ifndef NATANE_BUILD_SETTINGS_INCLUDED");
            sb.AppendLine("#define NATANE_BUILD_SETTINGS_INCLUDED");
            sb.AppendLine();
            sb.AppendLine("// Build-time feature defines (only used features are enabled).");
            sb.AppendLine("// NATANE_BUILD_ALL_FEATURES is intentionally NOT defined,");
            sb.AppendLine("// so the #undef guard block below will strip unused keywords.");

            // キーワードをソートして出力（再現性のため）
            var sortedKeywords = usedKeywords.OrderBy(k => k, StringComparer.Ordinal);
            foreach (string keyword in sortedKeywords)
            {
                string featureDefine = "NATANE_FEATURE" + keyword; // keyword already starts with _
                sb.AppendLine($"#define {featureDefine}");
            }

            sb.AppendLine();

            // #undef ガードブロックを生成
            AppendUndefGuardBlock(sb);

            sb.AppendLine();
            sb.AppendLine("#endif // NATANE_BUILD_SETTINGS_INCLUDED");

            string fullPath = Path.GetFullPath(path);
            File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[NataneToonShader] BuildSettings.hlsl を書き出しました: {path}");
        }

        /// <summary>
        /// NataneToonBuildSettings.hlsl をデフォルト（全機能有効）に復元する。
        /// NATANE_BUILD_ALL_FEATURES が定義されるため、#undef ガードはスキップされる。
        /// </summary>
        private static void RestoreDefaultBuildSettings()
        {
            // パスキャッシュをクリアして再検索
            _buildSettingsPath = null;
            string path = BuildSettingsPath;
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                string fullPath = Path.GetFullPath(path);
                string content = GenerateDefaultContent();
                File.WriteAllText(fullPath, content, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log("[NataneToonShader] ビルド設定を復元しました（全機能有効）");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NataneToonShader] ビルド設定の復元に失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// デフォルトの NataneToonBuildSettings.hlsl 内容を生成する。
        /// KeywordMappings から #undef ガードブロックを自動生成するため、
        /// 新しいキーワードを追加しても自動的に対応される。
        /// </summary>
        private static string GenerateDefaultContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("// NataneToonBuildSettings.hlsl");
            sb.AppendLine("// Auto-managed by NataneBuildFeatureOptimizer.");
            sb.AppendLine("// During builds, this file is rewritten to contain only the features actually used.");
            sb.AppendLine("// In the editor (non-build), all features are enabled by default.");
            sb.AppendLine("// DO NOT EDIT MANUALLY - changes will be overwritten during builds.");
            sb.AppendLine();
            sb.AppendLine("#ifndef NATANE_BUILD_SETTINGS_INCLUDED");
            sb.AppendLine("#define NATANE_BUILD_SETTINGS_INCLUDED");
            sb.AppendLine();
            sb.AppendLine("// Default: all features enabled (editor mode).");
            sb.AppendLine("// During VRChat/platform builds, NataneBuildFeatureOptimizer rewrites this file");
            sb.AppendLine("// to #define only the features actually referenced by project materials,");
            sb.AppendLine("// ensuring unused features are stripped even if shader keywords are desynchronized.");
            sb.AppendLine("#define NATANE_BUILD_ALL_FEATURES");
            sb.AppendLine();

            AppendUndefGuardBlock(sb);

            sb.AppendLine();
            sb.AppendLine("#endif // NATANE_BUILD_SETTINGS_INCLUDED");
            return sb.ToString();
        }

        /// <summary>
        /// #undef ガードブロックを StringBuilder に追加する。
        /// NATANE_BUILD_ALL_FEATURES が未定義の場合のみ有効化され、
        /// 対応する NATANE_FEATURE_* がないキーワードを強制的に #undef する。
        /// </summary>
        private static void AppendUndefGuardBlock(StringBuilder sb)
        {
            sb.AppendLine("// -----------------------------------------------------------------");
            sb.AppendLine("// Build-time keyword guard: when NATANE_BUILD_ALL_FEATURES is NOT");
            sb.AppendLine("// defined (i.e. during a build), force-#undef any shader keyword");
            sb.AppendLine("// whose corresponding NATANE_FEATURE_* is absent. This prevents");
            sb.AppendLine("// desynchronized keywords from enabling features the user disabled.");
            sb.AppendLine("// -----------------------------------------------------------------");
            sb.AppendLine("#ifndef NATANE_BUILD_ALL_FEATURES");
            sb.AppendLine();

            // KeywordMappings から自動生成
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
            {
                string keyword = mapping.keyword;
                string featureDefine = "NATANE_FEATURE" + keyword;
                sb.AppendLine($"#if defined({keyword}) && !defined({featureDefine})");
                sb.AppendLine($"    #undef {keyword}");
                sb.AppendLine("#endif");
            }

            // 追加キーワード（KeywordMappings 外）
            foreach (string keyword in ExtraKeywords)
            {
                string featureDefine = "NATANE_FEATURE" + keyword;
                sb.AppendLine($"#if defined({keyword}) && !defined({featureDefine})");
                sb.AppendLine($"    #undef {keyword}");
                sb.AppendLine("#endif");
            }

            sb.AppendLine();
            sb.AppendLine("#endif // !NATANE_BUILD_ALL_FEATURES");
        }

        /// <summary>
        /// 全 Natane シェーダー (.shader) を再インポートして再コンパイルを強制する。
        /// .hlsl ファイルだけのリインポートでは依存シェーダーの再コンパイルが
        /// 発動しないケースがあるため、.shader 本体もリインポートする。
        /// </summary>
        private static void ReimportAllNataneShaders()
        {
            // まず .hlsl をリインポート
            string hlslPath = BuildSettingsPath;
            if (!string.IsNullOrEmpty(hlslPath))
            {
                AssetDatabase.ImportAsset(hlslPath, ImportAssetOptions.ForceUpdate);
            }

            // 全 Natane .shader ファイルをリインポート
            string[] shaderGuids = AssetDatabase.FindAssets("t:Shader");
            int reimportCount = 0;
            foreach (string guid in shaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Natane シェーダーのパスに含まれるか確認
                if (!path.Contains("NataneToon"))
                    continue;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                reimportCount++;
            }

            Debug.Log($"[NataneToonShader] シェーダー再コンパイル: {reimportCount} ファイル");
        }

        /// <summary>
        /// NataneToonBuildSettings.hlsl のアセットパスを検索する。
        /// Unity は .hlsl を ShaderInclude として扱うため t:TextAsset では見つからない。
        /// </summary>
        private static string FindBuildSettingsHlslPath()
        {
            // 方法1: ShaderInclude 型で検索
            string[] guids = AssetDatabase.FindAssets("NataneToonBuildSettings t:ShaderInclude");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("NataneToonBuildSettings.hlsl", StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            // 方法2: 型フィルタなしで検索
            guids = AssetDatabase.FindAssets("NataneToonBuildSettings");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("NataneToonBuildSettings.hlsl", StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            // 方法3: NatanePackagePathResolver 経由のフォールバック
            if (NatanePackagePathResolver.TryResolvePackageAssetPath(
                    "Shaders/NataneToon/Include/Core/NataneToonBuildSettings.hlsl",
                    out string resolvedPath))
            {
                if (File.Exists(Path.GetFullPath(resolvedPath)))
                {
                    Debug.Log($"[NataneToonShader] BuildSettings.hlsl をフォールバックパスで検出: {resolvedPath}");
                    return resolvedPath;
                }
            }

            Debug.LogWarning("[NataneToonShader] NataneToonBuildSettings.hlsl が見つかりません");
            return null;
        }

        /// <summary>
        /// ビルドがキャンセルされた場合のフォールバック復元。
        /// エディタ起動時に BuildSettings が非デフォルトのままなら復元する。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureDefaultOnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                // パスキャッシュをクリアして最新で検索
                _buildSettingsPath = null;
                string path = BuildSettingsPath;
                if (string.IsNullOrEmpty(path))
                    return;

                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                    return;

                string content = File.ReadAllText(fullPath);
                if (!content.Contains("NATANE_BUILD_ALL_FEATURES"))
                {
                    Debug.Log("[NataneToonShader] ビルド設定がビルド状態のまま残っていました。復元します。");
                    RestoreDefaultBuildSettings();
                }
            };
        }
    }
}
