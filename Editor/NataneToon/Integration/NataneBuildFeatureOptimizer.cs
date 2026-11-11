using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// HLSL フィーチャーガード（旧 lilToon 方式）のビルド時最適化。
    ///
    /// 既定は Off（NataneBuildPolicySettings.HlslFeatureGuardMode）。Off のときは何もしない:
    /// 統合ストリッパー(Safe)が上位互換で未使用機能を削減するため。Stage D の比較レポート
    /// (NataneStrippingComparisonReport) で「HLSL 方式のみが削る機能=0件・Safe⊇HLSL」を確認済み。
    ///
    /// Advanced のときのみ従来動作を実行する:
    ///   ビルド前 - 使用機能だけを #define した NataneToonBuildSettings.hlsl を生成し、
    ///              未使用キーワードを #undef ガードで強制無効化。全 Natane シェーダーを再インポート。
    ///   ビルド後 - デフォルト（全機能有効）へ復元。
    /// Advanced では書換え/再インポートの正当性を証明できる場合に限りキャッシュでスキップする
    /// （期待 HLSL 内容ハッシュ・依存/Registry フィンガープリント等の全一致時のみ）。
    /// </summary>
    internal sealed class NataneBuildFeatureOptimizer : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // キーワード同期 (-100) の後、他のビルド処理の前に実行
        public int callbackOrder => -90;

        /// <summary>
        /// KeywordMappings に含まれない追加キーワード（派生・特殊用途）。
        ///
        /// ここに載せないと NataneToonBuildSettings.hlsl の #undef ガードが
        /// 自動生成されず、ビルド機能最適化でその機能を無効化してもストリップされない。
        /// 追加漏れは NataneShaderConsistencyAudit の「AWBOガード穴」で検出できる。
        /// </summary>
        private static readonly string[] ExtraKeywords =
        {
            "_EYE_PARALLAX",

            // PBR 系。トグルプロパティ _EnablePBR は Background にしか無く、
            // _PBR_LIKE に至っては単一トグルで駆動されない派生キーワードのため
            // KeywordMappings(プロパティ名→キーワード)では表現できない。
            // 実体は 12 バリアントすべての shader_feature_local に存在し、
            // NataneToonFragment.hlsl / NataneToonInput.hlsl のコードを実際に切り替える。
            "_PBR",
            "_PBR_LIKE",
        };

        /// <summary>
        /// #undef ガードが対象とするキーワード全集合（KeywordMappings + ExtraKeywords）。
        /// 比較レポートが「HLSL 方式が #undef し得るキーワード」の母集合として参照する。
        /// </summary>
        public static IReadOnlyList<string> GetGuardKeywords()
        {
            var list = new List<string>();
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
                if (!string.IsNullOrEmpty(mapping.keyword) && !list.Contains(mapping.keyword))
                    list.Add(mapping.keyword);
            foreach (string extra in ExtraKeywords)
                if (!list.Contains(extra))
                    list.Add(extra);
            return list;
        }

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

        // ---- ガード状態キャッシュ（Library 配下・Git 管理外） ----

        private const int GuardStateSchemaVersion = 1;
        private const string GuardStateFileName = "hlsl-guard-state-v1.json";
        private static string ProjectRootPath => Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
        private static string GuardStatePath =>
            Path.Combine(ProjectRootPath, "Library", "NataneToon", "Build", GuardStateFileName);

        public void OnPreprocessBuild(BuildReport report)
        {
            // Off（既定）: 何もしない。統合ストリッパー(Safe)が上位互換で削減を担うため、
            // ビルド前の HLSL 書換えも全 Natane 再インポートも行わない（Stage D 比較レポートで同等性確認済み）。
            if (!ShouldRunHlslGuard(NataneBuildPolicySettings.instance.HlslFeatureGuardMode))
                return;

            try
            {
                // パスキャッシュをクリアして最新状態で検索
                _buildSettingsPath = null;
                RunHlslGuardPreprocess();
            }
            catch (Exception ex)
            {
                // 最適化失敗時はデフォルト（全機能有効）に戻してビルドを止めない
                Debug.LogWarning($"[NataneToonShader] HLSL ガードに失敗しました。全機能有効で続行します: {ex.Message}");
                RestoreDefaultBuildSettings(force: true);
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            NataneHlslFeatureGuardMode mode = NataneBuildPolicySettings.instance.HlslFeatureGuardMode;
            if (!ShouldRunHlslGuard(mode))
            {
                // Off でも過去の Advanced / 旧版が書換えたまま残した非デフォルト状態は復元する（残骸対策）。
                RestoreDefaultIfNonDefault();
                return;
            }

            // Advanced: 内容が既にデフォルトなら書込み/再インポートを行わない。
            RestoreDefaultBuildSettings(force: false);
        }

        /// <summary>
        /// HLSL ガードを実行すべきか（Advanced のときのみ true）。副作用なしの純関数（テスト用）。
        /// </summary>
        public static bool ShouldRunHlslGuard(NataneHlslFeatureGuardMode mode)
        {
            return mode == NataneHlslFeatureGuardMode.Advanced;
        }

        /// <summary>
        /// Advanced 時のビルド前処理本体。期待 HLSL 内容とキャッシュ状態を突合し、
        /// 一致すれば書込み・再インポートをスキップする。
        /// </summary>
        private static void RunHlslGuardPreprocess()
        {
            HashSet<string> usedFeatures = CollectUsedFeatures();
            string expectedContent = BuildFeatureHlslContent(usedFeatures);
            string expectedHash = Sha1Hex(expectedContent);

            // 現在のファイル内容と一致すれば書込みは不要。
            bool contentMatches = CurrentBuildSettingsEquals(expectedContent);
            if (!contentMatches)
                WriteBuildSettingsHlsl(usedFeatures);

            // キャッシュ判定: 内容一致かつ状態ファイルが全項目一致した場合のみ再インポート済みとみなす。
            // 1 項目でも不一致 / 読取失敗ならキャッシュ不使用で従来どおり全再インポートする。
            NataneHlslGuardState expectedState = BuildCurrentGuardState(expectedHash);
            bool cacheHit = contentMatches
                            && TryLoadGuardState(out NataneHlslGuardState saved)
                            && IsGuardStateFresh(saved, expectedState);

            if (cacheHit)
            {
                Debug.Log("[NataneToonShader] HLSL ガード: キャッシュ一致のため再インポートをスキップしました。");
            }
            else
            {
                ReimportAllNataneShaders();
                SaveGuardState(expectedState);
            }

            Debug.Log($"[NataneToonShader] HLSL ガード(Advanced): {usedFeatures.Count}/{NataneShaderKeywordSynchronizer.KeywordMappings.Length} 機能を有効化");
        }

        /// <summary>
        /// アセットインデックスから Natane マテリアルのみを取得し、
        /// 使用されている機能キーワードを収集する。
        /// AnimationClip はプレハブ経由で参照される可能性があるため、
        /// インデックス内のプレハブが参照するクリップのみをスキャンする。
        ///
        /// 比較レポート(NataneStrippingComparisonReport)から再利用できるよう public な純関数として公開する。
        /// 副作用は AssetIndex の差分同期のみで、返り値はプロジェクト状態に対し決定的（挙動不変）。
        /// </summary>
        public static HashSet<string> CollectUsedFeatures()
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

                // _EYE_PARALLAX
                if (material.HasProperty("_EyeParallax") && material.GetFloat("_EyeParallax") >= 0.5f)
                    usedKeywords.Add("_EYE_PARALLAX");

                // _PBR / _PBR_LIKE は KeywordMappings に載らない（ExtraKeywords 参照）。
                // ここで拾わないと「未使用」と判定され、Advanced ガードで常に #undef されていた。
                if (material.IsKeywordEnabled("_PBR"))
                    usedKeywords.Add("_PBR");
                if (material.IsKeywordEnabled("_PBR_LIKE"))
                    usedKeywords.Add("_PBR_LIKE");
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
        /// 使用機能のみを #define した NataneToonBuildSettings.hlsl の内容を生成する（純関数・決定的）。
        /// キーワードはソートして出力するため、同一入力なら常に同一文字列を返す。
        /// NATANE_BUILD_ALL_FEATURES を定義しないため、#undef ガードが有効になり未使用キーワードが強制無効化される。
        /// </summary>
        public static string BuildFeatureHlslContent(IEnumerable<string> usedKeywords)
        {
            var sortedKeywords = (usedKeywords ?? Enumerable.Empty<string>())
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal);

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
            return sb.ToString();
        }

        /// <summary>
        /// 使用機能のみを #define した NataneToonBuildSettings.hlsl を書き出す。
        /// </summary>
        private static void WriteBuildSettingsHlsl(HashSet<string> usedKeywords)
        {
            string path = BuildSettingsPath;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[NataneToonShader] NataneToonBuildSettings.hlsl が見つかりません。スキップします。");
                return;
            }

            string content = BuildFeatureHlslContent(usedKeywords);
            string fullPath = Path.GetFullPath(path);
            File.WriteAllText(fullPath, content, new UTF8Encoding(false));

            Debug.Log($"[NataneToonShader] BuildSettings.hlsl を書き出しました: {path}");
        }

        /// <summary>
        /// 現在の NataneToonBuildSettings.hlsl が指定内容と一致するか。読取不能/未検出は false（不一致扱い）。
        /// </summary>
        private static bool CurrentBuildSettingsEquals(string content)
        {
            string path = BuildSettingsPath;
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                    return false;
                return string.Equals(File.ReadAllText(fullPath), content, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// NataneToonBuildSettings.hlsl をデフォルト（全機能有効）に復元する。
        /// force=false のときは既にデフォルト内容なら書込み/再インポートをスキップする。
        /// </summary>
        private static void RestoreDefaultBuildSettings(bool force)
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

                if (!force && File.Exists(fullPath) &&
                    string.Equals(File.ReadAllText(fullPath), content, StringComparison.Ordinal))
                {
                    return; // 既にデフォルト → スキップ
                }

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
        /// ビルド状態のマーカー(NATANE_BUILD_ALL_FEATURES 欠落)を検出した場合のみデフォルト復元する。
        /// Off モード / エディタ起動時の残骸対策に用いる。
        /// </summary>
        private static void RestoreDefaultIfNonDefault()
        {
            _buildSettingsPath = null;
            string path = BuildSettingsPath;
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                    return;

                string content = File.ReadAllText(fullPath);
                if (!content.Contains("NATANE_BUILD_ALL_FEATURES"))
                {
                    Debug.Log("[NataneToonShader] ビルド設定がビルド状態のまま残っていました。復元します。");
                    RestoreDefaultBuildSettings(force: true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NataneToonShader] ビルド設定の残骸チェックに失敗: {ex.Message}");
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

        // ---- ガード状態キャッシュ ----

        /// <summary>
        /// 現在のプロジェクト状態から期待ガード状態を組み立てる。
        /// 全項目が保存済み状態と一致する場合のみ「再インポート済み」とみなす根拠となる。
        /// </summary>
        private static NataneHlslGuardState BuildCurrentGuardState(string expectedHlslHash)
        {
            var state = new NataneHlslGuardState
            {
                schemaVersion = GuardStateSchemaVersion,
                expectedHlslHash = expectedHlslHash,
                shaderDependencyFingerprint = SafeCompute(NataneShaderUpdateAudit.ComputeCurrentDependencyFingerprint),
                registryFingerprint = SafeCompute(NataneShaderFeatureRegistry.ComputeRegistryFingerprint),
                packageVersion = SafeCompute(() => NataneShaderFeatureRegistry.ShaderCompatibilityVersion),
                unityVersion = Application.unityVersion,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
            };

            try
            {
                var apis = PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget);
                if (apis != null)
                    state.graphicsApis = apis.Select(a => a.ToString()).OrderBy(a => a, StringComparer.Ordinal).ToList();
            }
            catch
            {
                // Graphics API 取得不能でも致命ではない（不一致になればキャッシュ不使用へ倒れる）。
            }

            // Snapshot fingerprint は「あれば」利用（無くても null 同士で一致する）。
            try
            {
                if (NataneBuildUsageSnapshotStore.TryLoad(out var snapshot) && snapshot != null)
                    state.snapshotFingerprint = snapshot.snapshotFingerprint;
            }
            catch
            {
                // 読取失敗時は null のまま（保存側も null なら一致、値ありなら不一致でキャッシュ不使用）。
            }

            return state;
        }

        private static string SafeCompute(Func<string> compute)
        {
            try { return compute(); }
            catch { return null; }
        }

        /// <summary>
        /// 保存済みガード状態と期待状態を突合する純関数。1 項目でも不一致 / いずれか null なら false。
        /// テスト用に公開。
        /// </summary>
        public static bool IsGuardStateFresh(NataneHlslGuardState saved, NataneHlslGuardState expected)
        {
            if (saved == null || expected == null)
                return false;
            if (saved.schemaVersion != expected.schemaVersion)
                return false;
            if (!StringEquals(saved.expectedHlslHash, expected.expectedHlslHash))
                return false;
            if (!StringEquals(saved.shaderDependencyFingerprint, expected.shaderDependencyFingerprint))
                return false;
            if (!StringEquals(saved.registryFingerprint, expected.registryFingerprint))
                return false;
            if (!StringEquals(saved.packageVersion, expected.packageVersion))
                return false;
            if (!StringEquals(saved.unityVersion, expected.unityVersion))
                return false;
            if (!StringEquals(saved.buildTarget, expected.buildTarget))
                return false;
            if (!StringEquals(saved.snapshotFingerprint, expected.snapshotFingerprint))
                return false;
            return SequenceEquals(saved.graphicsApis, expected.graphicsApis);
        }

        private static bool StringEquals(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool SequenceEquals(List<string> a, List<string> b)
        {
            int ca = a?.Count ?? 0;
            int cb = b?.Count ?? 0;
            if (ca != cb)
                return false;
            for (int i = 0; i < ca; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        /// <summary>ガード状態を保存する（テスト用に公開）。</summary>
        public static void SaveGuardState(NataneHlslGuardState state)
        {
            if (state == null)
                return;

            try
            {
                string dir = Path.GetDirectoryName(GuardStatePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(GuardStatePath, JsonUtility.ToJson(state, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NataneToonShader] HLSL ガード状態の保存に失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// ガード状態を読み込む（テスト用に公開）。ファイル欠損 / schema 不一致 / 読取失敗はいずれも false
        /// （正当性を証明できないキャッシュは使わない）。
        /// </summary>
        public static bool TryLoadGuardState(out NataneHlslGuardState state)
        {
            state = null;
            try
            {
                if (!File.Exists(GuardStatePath))
                    return false;

                var loaded = JsonUtility.FromJson<NataneHlslGuardState>(File.ReadAllText(GuardStatePath));
                if (loaded == null || loaded.schemaVersion != GuardStateSchemaVersion)
                    return false;

                state = loaded;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void DeleteGuardState()
        {
            try
            {
                if (File.Exists(GuardStatePath))
                    File.Delete(GuardStatePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NataneToonShader] HLSL ガード状態の削除に失敗: {ex.Message}");
            }
        }

        private static string Sha1Hex(string text)
        {
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        // ---- メニュー ----

        /// <summary>
        /// 「キャッシュ無視して再構築」: 状態ファイル削除 + BuildSettings.hlsl をデフォルト復元 + 全 Natane 再インポート。
        /// 明示操作のため書込み/再インポートを常に行う。
        /// </summary>
        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/HLSLガード状態をリセット Reset HLSL Guard State", false, 64)]
        public static void ResetHlslGuardStateFromMenu()
        {
            _buildSettingsPath = null;
            DeleteGuardState();
            RestoreDefaultBuildSettings(force: true);
            ReimportAllNataneShaders();
            Debug.Log("[NataneToonShader] HLSL ガード状態をリセットしました（状態ファイル削除＋デフォルト復元＋全 Natane 再インポート）。");
        }

        /// <summary>
        /// ビルドがキャンセルされた場合のフォールバック復元。
        /// エディタ起動時に BuildSettings が非デフォルトのままなら（モードに関わらず）復元する。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureDefaultOnEditorLoad()
        {
            EditorApplication.delayCall += () =>
            {
                RestoreDefaultIfNonDefault();
            };
        }
    }

    /// <summary>
    /// HLSL ガードのキャッシュ状態（Library/NataneToon/Build/hlsl-guard-state-v1.json）。
    /// 全項目一致時のみ「この状態で再インポート済み」とみなして再インポートをスキップする。
    /// </summary>
    [Serializable]
    public sealed class NataneHlslGuardState
    {
        public int schemaVersion;
        public string expectedHlslHash;
        public string shaderDependencyFingerprint;
        public string registryFingerprint;
        public string packageVersion;
        public string unityVersion;
        public string buildTarget;
        public List<string> graphicsApis = new List<string>();
        public string snapshotFingerprint;
    }
}
