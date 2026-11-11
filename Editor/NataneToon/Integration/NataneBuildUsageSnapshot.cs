using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NataneToon.Editor
{
    // ==== 不変 Snapshot データ（JsonUtility 対応・全フィールド public） ====

    [Serializable]
    public sealed class ShaderUsageSnapshotEntry
    {
        public string shaderName;
        public string shaderGuid;
        public List<string> usedKeywords = new List<string>();   // 当該シェーダーを使う全 Material のキーワード和集合（ソート済）
        public List<string> materialGuids = new List<string>();  // ソート済
    }

    [Serializable]
    public sealed class MaterialConfigSnapshotEntry
    {
        public string materialGuid;
        public string shaderName;
        public string keywordSetKey;                             // 管理対象キーワードの完全構成（ソート済セットキー）
    }

    [Serializable]
    public sealed class AnimationDrivenKeywordEntry
    {
        public string clipGuid;
        public string clipName;
        public List<string> keywords = new List<string>();       // クリップが >=0.5 で有効化し得る管理キーワード（ソート済）
    }

    [Serializable]
    public sealed class AnimatorClipDependencyEntry
    {
        public string controllerGuid;
        public List<string> clipGuids = new List<string>();      // ソート済
    }

    [Serializable]
    public sealed class SceneDependencySnapshotEntry
    {
        public string kind;                                      // "Prefab" または "Scene"
        public string guid;
        public string path;
        public string name;
        public List<string> materialGuids = new List<string>(); // Natane マテリアルのみ（ソート済）
    }

    [Serializable]
    public sealed class NataneOptimizationDigest
    {
        public string mode;
        public string strictFailurePolicy;
        public string unknownKeywordPolicy;
        public string hlslFeatureGuardMode;
        public bool buildPrewarmEnabled;
        public bool variantStrippingEnabled;
        public bool legacyExactSetStrippingEnabled;

        // fingerprint 用の安定ダイジェスト文字列。
        public string ToStableString()
        {
            return $"mode={mode};strict={strictFailurePolicy};unknown={unknownKeywordPolicy};" +
                   $"guard={hlslFeatureGuardMode};prewarm={(buildPrewarmEnabled ? 1 : 0)};" +
                   $"strip={(variantStrippingEnabled ? 1 : 0)};legacy={(legacyExactSetStrippingEnabled ? 1 : 0)}";
        }
    }

    [Serializable]
    public sealed class NataneSnapshotAuditSummary
    {
        public int unknownKeywordCount;
        public int orphanDefinitionCount;
        public int unparseableItemCount;
        public bool auditSkipped;                                // Disabled モードで監査省略した場合 true
        public List<string> unknownKeywords = new List<string>();
    }

    /// <summary>
    /// ビルド前 1 回の走査で構築する不変 Build Usage Snapshot。
    /// 消費（ストリップ / レポート）は後続ステージが担当し、本ステージでは生成・保存・鮮度判定のみ。
    /// </summary>
    [Serializable]
    public sealed class NataneBuildUsageSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;

        // 使用状況
        public List<ShaderUsageSnapshotEntry> shaderUsages = new List<ShaderUsageSnapshotEntry>();
        public List<MaterialConfigSnapshotEntry> materialConfigs = new List<MaterialConfigSnapshotEntry>();
        public List<AnimationDrivenKeywordEntry> animationDrivenKeywords = new List<AnimationDrivenKeywordEntry>();
        public List<AnimatorClipDependencyEntry> animatorClipDependencies = new List<AnimatorClipDependencyEntry>();
        public List<SceneDependencySnapshotEntry> prefabSceneDependencies = new List<SceneDependencySnapshotEntry>();

        // 保持指定（NataneBuildPolicySettings 由来）
        public List<string> runtimeDynamicKeywords = new List<string>();
        public List<string> alwaysKeepKeywords = new List<string>();
        public List<string> alwaysKeepMaterialGuids = new List<string>();
        public List<string> alwaysKeepShaderGuids = new List<string>();
        public List<string> shaderVariantCollectionGuids = new List<string>();

        // 監査要約
        public NataneSnapshotAuditSummary auditSummary = new NataneSnapshotAuditSummary();

        // Registry / バージョン情報
        public int registrySchemaVersion;
        public int registryMigrationVersion;
        public string registryFingerprint;
        public string shaderCompatibilityVersion;
        public string packageVersion;
        public string shaderDependencyFingerprint;
        public string unityVersion;
        public string buildTarget;
        public List<string> graphicsApis = new List<string>();

        // 最適化設定ダイジェスト
        public NataneOptimizationDigest optimizationDigest = new NataneOptimizationDigest();

        // AssetIndex の鮮度比較用（stale 判定に使用）
        public long assetIndexGeneratedAtUtcTicks;

        // メタ
        public string snapshotFingerprint;
        public long generatedAtUtcTicks;
        public List<string> warnings = new List<string>();
        public List<string> unparseableItems = new List<string>();

        /// <summary>
        /// generatedAtUtcTicks / snapshotFingerprint を除く全内容から安定ハッシュを計算する。
        /// 同一プロジェクト状態なら 2 回生成しても一致する。
        /// </summary>
        public string ComputeFingerprint()
        {
            var sb = new StringBuilder();
            sb.Append("schema=").Append(schemaVersion).Append('\n');
            sb.Append("registrySchema=").Append(registrySchemaVersion).Append('\n');
            sb.Append("registryMigration=").Append(registryMigrationVersion).Append('\n');
            sb.Append("registryFp=").Append(registryFingerprint).Append('\n');
            sb.Append("shaderCompat=").Append(shaderCompatibilityVersion).Append('\n');
            sb.Append("package=").Append(packageVersion).Append('\n');
            sb.Append("shaderDep=").Append(shaderDependencyFingerprint).Append('\n');
            sb.Append("unity=").Append(unityVersion).Append('\n');
            sb.Append("target=").Append(buildTarget).Append('\n');
            sb.Append("gapi=").Append(string.Join(",", graphicsApis)).Append('\n');
            sb.Append("opt=").Append(optimizationDigest.ToStableString()).Append('\n');

            foreach (var s in shaderUsages)
            {
                sb.Append("SU|").Append(s.shaderName).Append('|').Append(s.shaderGuid).Append('|')
                  .Append(string.Join(",", s.usedKeywords)).Append('|')
                  .Append(string.Join(",", s.materialGuids)).Append('\n');
            }
            foreach (var m in materialConfigs)
            {
                sb.Append("MC|").Append(m.materialGuid).Append('|').Append(m.shaderName).Append('|')
                  .Append(m.keywordSetKey).Append('\n');
            }
            foreach (var a in animationDrivenKeywords)
            {
                sb.Append("AK|").Append(a.clipGuid).Append('|').Append(string.Join(",", a.keywords)).Append('\n');
            }
            foreach (var d in animatorClipDependencies)
            {
                sb.Append("AC|").Append(d.controllerGuid).Append('|').Append(string.Join(",", d.clipGuids)).Append('\n');
            }
            foreach (var p in prefabSceneDependencies)
            {
                sb.Append("PS|").Append(p.kind).Append('|').Append(p.guid).Append('|')
                  .Append(string.Join(",", p.materialGuids)).Append('\n');
            }

            sb.Append("keep|").Append(string.Join(",", runtimeDynamicKeywords)).Append('|')
              .Append(string.Join(",", alwaysKeepKeywords)).Append('|')
              .Append(string.Join(",", alwaysKeepMaterialGuids)).Append('|')
              .Append(string.Join(",", alwaysKeepShaderGuids)).Append('|')
              .Append(string.Join(",", shaderVariantCollectionGuids)).Append('\n');

            sb.Append("audit|").Append(auditSummary.unknownKeywordCount).Append('|')
              .Append(auditSummary.orphanDefinitionCount).Append('|')
              .Append(auditSummary.unparseableItemCount).Append('|')
              .Append(auditSummary.auditSkipped ? 1 : 0).Append('|')
              .Append(string.Join(",", auditSummary.unknownKeywords)).Append('\n');

            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }
    }

    /// <summary>
    /// Snapshot 生成結果。例外は握りつぶさず FailureReason に格納し、Succeeded=false で返す。
    /// 呼出側（Stage D）が KeepAll フォールバック / Strict 失敗を選べる形。
    /// </summary>
    public sealed class SnapshotBuildResult
    {
        public NataneBuildUsageSnapshot Snapshot;
        public bool Succeeded;
        public string FailureReason;
    }

    /// <summary>
    /// Build Usage Snapshot ビルダー。処理順は SPEC に従う:
    /// ①AssetIndex 最新化 ②Update Audit ③Material Keyword 同期 ④1 回走査で構築 ⑤検証。
    /// 材料は AssetIndex と Registry から取得し、マテリアル全走査（FindAssets t:Material）は
    /// インデックス空時のフォールバックのみ（警告記録）。
    /// </summary>
    public static class NataneBuildUsageSnapshotBuilder
    {
        // テスト用の例外注入フック。非 null なら走査冒頭で当該例外を投げ、失敗経路を検証できる。
        internal static Func<Exception> TestFailureHook;

        public static SnapshotBuildResult Build(BuildTarget buildTarget, string[] scenePathsOrNull, bool saveSyncToDisk = false)
        {
            var snapshot = new NataneBuildUsageSnapshot
            {
                schemaVersion = NataneBuildUsageSnapshot.CurrentSchemaVersion,
                unityVersion = Application.unityVersion,
                buildTarget = buildTarget.ToString(),
                registrySchemaVersion = NataneShaderFeatureRegistry.RegistrySchemaVersion,
                registryMigrationVersion = NataneShaderFeatureRegistry.MigrationVersion,
                registryFingerprint = NataneShaderFeatureRegistry.ComputeRegistryFingerprint(),
                shaderCompatibilityVersion = NataneShaderFeatureRegistry.ShaderCompatibilityVersion,
                packageVersion = NataneShaderFeatureRegistry.ShaderCompatibilityVersion
            };

            var buildStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (TestFailureHook != null)
                {
                    Exception injected = TestFailureHook();
                    if (injected != null) throw injected;
                }

                bool disabled = NataneBuildPolicySettings.instance.OptimizationMode == NataneOptimizationMode.Disabled;

                PopulateGraphicsApis(snapshot, buildTarget);
                PopulateOptimizationDigest(snapshot);
                PopulateKeepLists(snapshot);

                // ① AssetIndex 最新化（差分キャッチアップ）。
                var indexStopwatch = System.Diagnostics.Stopwatch.StartNew();
                NataneAssetIndexService.RunSynchronousCatchUp();
                indexStopwatch.Stop();
                NataneBuildStageTimings.SetIndexCatchUpMs(indexStopwatch.Elapsed.TotalMilliseconds);
                if (NataneAssetIndexStore.TryLoadIndex(out var indexData) && indexData != null)
                {
                    snapshot.assetIndexGeneratedAtUtcTicks = indexData.generatedAtUtcTicks;
                }

                // 常に依存ハッシュは記録（鮮度判定に必須）。
                snapshot.shaderDependencyFingerprint = NataneShaderUpdateAudit.ComputeCurrentDependencyFingerprint();

                if (disabled)
                {
                    // Disabled: 監査・使用キーワード走査をスキップし、メタデータのみ。
                    snapshot.auditSummary.auditSkipped = true;
                    snapshot.warnings.Add("最適化モードが Disabled のため、監査と使用状況走査を省略しました（メタデータのみ）。");
                }
                else
                {
                    // ② Update Audit。
                    var auditStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    NataneShaderAuditData audit = NataneShaderUpdateAudit.Run();
                    auditStopwatch.Stop();
                    NataneBuildStageTimings.SetAuditMs(auditStopwatch.Elapsed.TotalMilliseconds);
                    PopulateAuditSummary(snapshot, audit);
                    // 監査由来の依存ハッシュを採用（同一計算だが監査結果と整合させる）。
                    if (!string.IsNullOrEmpty(audit.shaderDependencyFingerprint))
                    {
                        snapshot.shaderDependencyFingerprint = audit.shaderDependencyFingerprint;
                    }
                    foreach (string item in audit.unparseableItems)
                    {
                        snapshot.unparseableItems.Add(item);
                    }

                    // ③ Material Keyword 同期。
                    SynchronizeKeywords(snapshot, saveSyncToDisk);

                    // ④ 1 回走査で使用状況を構築（材料は AssetIndex / Registry から）。
                    PopulateUsageFromIndex(snapshot);
                    PopulateAnimationDependencies(snapshot);
                    PopulateSceneDependencies(snapshot, scenePathsOrNull);

                    // ⑤ 検証。
                    ValidateSnapshot(snapshot, audit);
                }

                SortForStableFingerprint(snapshot);
                snapshot.generatedAtUtcTicks = DateTime.UtcNow.Ticks;
                snapshot.snapshotFingerprint = snapshot.ComputeFingerprint();

                buildStopwatch.Stop();
                NataneBuildStageTimings.SetSnapshotBuildMs(buildStopwatch.Elapsed.TotalMilliseconds);
                return new SnapshotBuildResult { Snapshot = snapshot, Succeeded = true, FailureReason = null };
            }
            catch (Exception ex)
            {
                // 例外は握りつぶさず理由として返す。生成途中の snapshot も参考用に添える。
                buildStopwatch.Stop();
                NataneBuildStageTimings.SetSnapshotBuildMs(buildStopwatch.Elapsed.TotalMilliseconds);
                snapshot.warnings.Add("Snapshot 生成に失敗しました: " + ex.Message);
                snapshot.generatedAtUtcTicks = DateTime.UtcNow.Ticks;
                return new SnapshotBuildResult
                {
                    Snapshot = snapshot,
                    Succeeded = false,
                    FailureReason = ex.Message
                };
            }
        }

        private static void PopulateGraphicsApis(NataneBuildUsageSnapshot snapshot, BuildTarget buildTarget)
        {
            try
            {
                var apis = PlayerSettings.GetGraphicsAPIs(buildTarget);
                if (apis != null)
                {
                    snapshot.graphicsApis = apis.Select(a => a.ToString())
                        .OrderBy(a => a, StringComparer.Ordinal).ToList();
                }
            }
            catch (Exception ex)
            {
                snapshot.warnings.Add("Graphics API の取得に失敗しました: " + ex.Message);
            }
        }

        private static void PopulateOptimizationDigest(NataneBuildUsageSnapshot snapshot)
        {
            var s = NataneBuildPolicySettings.instance;
            snapshot.optimizationDigest = new NataneOptimizationDigest
            {
                mode = s.OptimizationMode.ToString(),
                strictFailurePolicy = s.StrictFailurePolicy.ToString(),
                unknownKeywordPolicy = s.UnknownKeywordPolicy.ToString(),
                hlslFeatureGuardMode = s.HlslFeatureGuardMode.ToString(),
                buildPrewarmEnabled = s.BuildPrewarmEnabled,
                variantStrippingEnabled = s.VariantStrippingEnabled,
                legacyExactSetStrippingEnabled = s.LegacyExactSetStrippingEnabled
            };
        }

        private static void PopulateKeepLists(NataneBuildUsageSnapshot snapshot)
        {
            var s = NataneBuildPolicySettings.instance;
            snapshot.runtimeDynamicKeywords = Sorted(s.RuntimeDynamicKeywords);
            snapshot.alwaysKeepKeywords = Sorted(s.AlwaysKeepKeywords);
            snapshot.alwaysKeepMaterialGuids = Sorted(s.AlwaysKeepMaterialGuids);
            snapshot.alwaysKeepShaderGuids = Sorted(s.AlwaysKeepShaderGuids);
            snapshot.shaderVariantCollectionGuids = Sorted(s.ShaderVariantCollectionGuids);
        }

        private static void PopulateAuditSummary(NataneBuildUsageSnapshot snapshot, NataneShaderAuditData audit)
        {
            snapshot.auditSummary = new NataneSnapshotAuditSummary
            {
                unknownKeywordCount = audit.unknownKeywords.Count,
                orphanDefinitionCount = audit.orphanDefinitions.Count,
                unparseableItemCount = audit.unparseableItems.Count,
                auditSkipped = false,
                unknownKeywords = audit.unknownKeywords
                    .OrderBy(k => k, StringComparer.Ordinal).ToList()
            };
        }

        private static void SynchronizeKeywords(NataneBuildUsageSnapshot snapshot, bool saveSyncToDisk)
        {
            if (saveSyncToDisk)
            {
                // ビルド文脈: ディスクの .mat を SDK が直読するため保存を伴う同期。
                NataneShaderKeywordSynchronizer.SynchronizeAllNataneMaterials();
                return;
            }

            // 手動生成（エディタ文脈）: in-memory 同期のみ（ディスク保存なし）。
            try
            {
                NataneAssetIndexService.EnsureLoaded();
                foreach (var entry in NataneAssetIndexService.EnumerateMaterialEntries(e => e.isNataneShader).ToList())
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(entry.path);
                    if (material == null || material.shader == null) continue;
                    NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(material); // 保存しない
                }
            }
            catch (Exception ex)
            {
                snapshot.warnings.Add("in-memory キーワード同期をスキップしました: " + ex.Message);
            }
        }

        private static void PopulateUsageFromIndex(NataneBuildUsageSnapshot snapshot)
        {
            NataneAssetIndexService.EnsureLoaded();
            var materialEntries = NataneAssetIndexService
                .EnumerateMaterialEntries(e => e.isNataneShader)
                .ToList();

            if (materialEntries.Count == 0)
            {
                // インデックス空: フォールバックで t:Material 全走査（警告記録）。
                snapshot.warnings.Add("AssetIndex に Natane マテリアルが無いため、FindAssets(t:Material) フォールバックで走査しました。");
                materialEntries = FallbackScanMaterials();
            }

            // shaderName -> (usedKeywords, materialGuids)
            var perShaderKeywords = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var perShaderMaterials = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var shaderGuidByName = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var e in materialEntries)
            {
                if (string.IsNullOrEmpty(e.shaderName)) continue;

                if (!perShaderKeywords.TryGetValue(e.shaderName, out var kws))
                {
                    kws = new HashSet<string>(StringComparer.Ordinal);
                    perShaderKeywords[e.shaderName] = kws;
                    perShaderMaterials[e.shaderName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    shaderGuidByName[e.shaderName] = ResolveShaderGuid(e.shaderName);
                }

                if (!string.IsNullOrEmpty(e.keywordSetKey))
                {
                    foreach (string kw in e.keywordSetKey.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        kws.Add(kw);
                    }
                }

                if (!string.IsNullOrEmpty(e.guid))
                {
                    perShaderMaterials[e.shaderName].Add(e.guid);
                }

                snapshot.materialConfigs.Add(new MaterialConfigSnapshotEntry
                {
                    materialGuid = e.guid,
                    shaderName = e.shaderName,
                    keywordSetKey = e.keywordSetKey ?? string.Empty
                });
            }

            foreach (var kv in perShaderKeywords)
            {
                snapshot.shaderUsages.Add(new ShaderUsageSnapshotEntry
                {
                    shaderName = kv.Key,
                    shaderGuid = shaderGuidByName.TryGetValue(kv.Key, out var g) ? g : string.Empty,
                    usedKeywords = kv.Value.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                    materialGuids = perShaderMaterials[kv.Key].OrderBy(k => k, StringComparer.Ordinal).ToList()
                });
            }
        }

        private static List<MaterialIndexEntry> FallbackScanMaterials()
        {
            var result = new List<MaterialIndexEntry>();
            string[] guids = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) continue;
                if (!NataneShaderCatalog.IsNataneShader(material.shader.name)) continue;

                string setKey = material.shaderKeywords == null || material.shaderKeywords.Length == 0
                    ? string.Empty
                    : string.Join(";", material.shaderKeywords
                        .Where(k => !string.IsNullOrEmpty(k))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(k => k, StringComparer.Ordinal));

                result.Add(new MaterialIndexEntry
                {
                    guid = guid,
                    path = path,
                    name = material.name,
                    shaderName = material.shader.name,
                    keywordSetKey = setKey,
                    isNataneShader = true
                });
            }

            return result;
        }

        private static string ResolveShaderGuid(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) return string.Empty;
            string path = AssetDatabase.GetAssetPath(shader);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        /// <summary>Registry の PropertyName -> Keyword（アニメ可能な定義のみ）マップを構築する。</summary>
        internal static Dictionary<string, string> BuildAnimatablePropertyToKeywordMap()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var def in NataneShaderFeatureRegistry.AllDefinitions)
            {
                if (!string.IsNullOrEmpty(def.PropertyName) && def.IsAnimatable && !map.ContainsKey(def.PropertyName))
                {
                    map[def.PropertyName] = def.Keyword;
                }
            }
            return map;
        }

        /// <summary>
        /// クリップが管理 Toggle Property を >=0.5 で有効化し得る管理キーワード集合を返す（ソート済）。
        /// propertyName 照合は Registry の PropertyName を用いる。テスト用に内部公開。
        /// </summary>
        internal static List<string> ExtractAnimationDrivenKeywords(AnimationClip clip, IReadOnlyDictionary<string, string> propertyToKeyword)
        {
            var keywords = new HashSet<string>(StringComparer.Ordinal);
            if (clip == null || propertyToKeyword == null)
            {
                return keywords.ToList();
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName == null ||
                    !binding.propertyName.StartsWith("material.", StringComparison.Ordinal))
                    continue;

                string prop = binding.propertyName.Substring("material.".Length);
                if (!propertyToKeyword.TryGetValue(prop, out string kw)) continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;
                if (curve.keys.Any(k => k.value >= 0.5f))
                {
                    keywords.Add(kw);
                }
            }

            return keywords.OrderBy(k => k, StringComparer.Ordinal).ToList();
        }

        private static void PopulateAnimationDependencies(NataneBuildUsageSnapshot snapshot)
        {
            var propertyToKeyword = BuildAnimatablePropertyToKeywordMap();

            // AnimationClip は AssetIndex 非対象のため FindAssets で収集（マテリアル走査制約の対象外）。
            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip");
            foreach (string clipGuid in clipGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(clipGuid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                List<string> keywords = ExtractAnimationDrivenKeywords(clip, propertyToKeyword);
                if (keywords.Count > 0)
                {
                    snapshot.animationDrivenKeywords.Add(new AnimationDrivenKeywordEntry
                    {
                        clipGuid = clipGuid,
                        clipName = clip.name,
                        keywords = keywords
                    });
                }
            }

            // AnimatorController -> Clip 依存。
            string[] controllerGuids = AssetDatabase.FindAssets("t:AnimatorController");
            foreach (string controllerGuid in controllerGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(controllerGuid);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null || controller.animationClips == null) continue;

                var clipGuidSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var clip in controller.animationClips)
                {
                    if (clip == null) continue;
                    string clipPath = AssetDatabase.GetAssetPath(clip);
                    string g = AssetDatabase.AssetPathToGUID(clipPath);
                    if (!string.IsNullOrEmpty(g)) clipGuidSet.Add(g);
                }

                if (clipGuidSet.Count > 0)
                {
                    snapshot.animatorClipDependencies.Add(new AnimatorClipDependencyEntry
                    {
                        controllerGuid = controllerGuid,
                        clipGuids = clipGuidSet.OrderBy(g => g, StringComparer.Ordinal).ToList()
                    });
                }
            }
        }

        private static void PopulateSceneDependencies(NataneBuildUsageSnapshot snapshot, string[] scenePathsOrNull)
        {
            // 既存 AssetIndex の Prefab 依存を流用。
            NataneAssetIndexService.EnsureLoaded();
            if (NataneAssetIndexStore.TryLoadIndex(out var indexData) && indexData != null)
            {
                foreach (var prefab in indexData.prefabs)
                {
                    if (prefab.materialGuids == null || prefab.materialGuids.Count == 0) continue;
                    var nataneMats = prefab.materialGuids
                        .Where(IsNataneMaterialGuid)
                        .OrderBy(g => g, StringComparer.Ordinal).ToList();
                    if (nataneMats.Count == 0) continue;

                    snapshot.prefabSceneDependencies.Add(new SceneDependencySnapshotEntry
                    {
                        kind = "Prefab",
                        guid = prefab.guid,
                        path = prefab.path,
                        name = prefab.name,
                        materialGuids = nataneMats
                    });
                }
            }

            // ビルド対象 Scene の依存アセット（Natane マテリアルのみ記録）。
            string[] scenePaths = scenePathsOrNull;
            if (scenePaths == null)
            {
                scenePaths = EditorBuildSettings.scenes
                    .Where(s => s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                    .Select(s => s.path)
                    .ToArray();
            }

            foreach (string scenePath in scenePaths)
            {
                if (string.IsNullOrEmpty(scenePath)) continue;
                string[] deps = AssetDatabase.GetDependencies(scenePath, true);
                var nataneMats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string dep in deps)
                {
                    if (!dep.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
                    string g = AssetDatabase.AssetPathToGUID(dep);
                    if (IsNataneMaterialGuid(g)) nataneMats.Add(g);
                }

                if (nataneMats.Count == 0) continue;
                string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                snapshot.prefabSceneDependencies.Add(new SceneDependencySnapshotEntry
                {
                    kind = "Scene",
                    guid = sceneGuid,
                    path = scenePath,
                    name = System.IO.Path.GetFileNameWithoutExtension(scenePath),
                    materialGuids = nataneMats.OrderBy(g => g, StringComparer.Ordinal).ToList()
                });
            }
        }

        private static bool IsNataneMaterialGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            var entry = NataneAssetIndexService.TryGetMaterialEntry(guid);
            if (entry != null) return entry.isNataneShader;

            // インデックス外は実ロードで確認（Scene 依存で拾った未索引マテリアル向け）。
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            return material != null && material.shader != null &&
                   NataneShaderCatalog.IsNataneShader(material.shader.name);
        }

        private static void ValidateSnapshot(NataneBuildUsageSnapshot snapshot, NataneShaderAuditData audit)
        {
            if (audit.registrySchemaVersion != NataneShaderFeatureRegistry.RegistrySchemaVersion)
            {
                snapshot.warnings.Add("Registry schema バージョンが監査時と不一致です（cache/snapshot 再利用に注意）。");
            }
            if (audit.unknownKeywords.Count > 0)
            {
                snapshot.warnings.Add($"未知キーワードが {audit.unknownKeywords.Count} 件あります（自動削除しません）。");
            }
            if (audit.unparseableItems.Count > 0)
            {
                snapshot.warnings.Add($"解析不能項目が {audit.unparseableItems.Count} 件あります（安全側で保持対象）。");
            }
            if (NataneShaderSourceChangeDetector.ShaderSourcesChangedSinceLastAudit)
            {
                snapshot.warnings.Add("最終監査以降にシェーダーソースが変更されています（再監査推奨）。");
            }
        }

        private static void SortForStableFingerprint(NataneBuildUsageSnapshot snapshot)
        {
            snapshot.shaderUsages = snapshot.shaderUsages
                .OrderBy(s => s.shaderName, StringComparer.Ordinal).ToList();
            snapshot.materialConfigs = snapshot.materialConfigs
                .OrderBy(m => m.materialGuid, StringComparer.Ordinal)
                .ThenBy(m => m.shaderName, StringComparer.Ordinal).ToList();
            snapshot.animationDrivenKeywords = snapshot.animationDrivenKeywords
                .OrderBy(a => a.clipGuid, StringComparer.Ordinal).ToList();
            snapshot.animatorClipDependencies = snapshot.animatorClipDependencies
                .OrderBy(a => a.controllerGuid, StringComparer.Ordinal).ToList();
            snapshot.prefabSceneDependencies = snapshot.prefabSceneDependencies
                .OrderBy(p => p.kind, StringComparer.Ordinal)
                .ThenBy(p => p.guid, StringComparer.Ordinal).ToList();
            snapshot.warnings.Sort(StringComparer.Ordinal);
            snapshot.unparseableItems.Sort(StringComparer.Ordinal);
        }

        private static List<string> Sorted(IEnumerable<string> source)
        {
            if (source == null) return new List<string>();
            return source.Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>
    /// Snapshot の保存 / 読込 / 鮮度判定。
    /// Library/NataneToon/Build/build-usage-snapshot-v1.json（Git 管理外）。
    /// </summary>
    public static class NataneBuildUsageSnapshotStore
    {
        private const string SnapshotFileName = "build-usage-snapshot-v1.json";
        private static string ProjectRootPath =>
            System.IO.Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
        public static string SnapshotPath =>
            System.IO.Path.Combine(ProjectRootPath, "Library", "NataneToon", "Build", SnapshotFileName);

        public static bool SnapshotExists => System.IO.File.Exists(SnapshotPath);

        public static void Save(NataneBuildUsageSnapshot snapshot)
        {
            if (snapshot == null) return;
            string dir = System.IO.Path.GetDirectoryName(SnapshotPath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snapshot, true));
        }

        public static bool TryLoad(out NataneBuildUsageSnapshot snapshot)
        {
            snapshot = null;
            if (!SnapshotExists) return false;
            try
            {
                var loaded = JsonUtility.FromJson<NataneBuildUsageSnapshot>(
                    System.IO.File.ReadAllText(SnapshotPath));
                if (loaded == null || loaded.schemaVersion != NataneBuildUsageSnapshot.CurrentSchemaVersion)
                {
                    return false; // schema 不一致は破棄（再構築）
                }
                snapshot = loaded;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Snapshot] Snapshot の読込に失敗しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 依存ハッシュ / Registry / package / Unity / buildTarget / 最適化ダイジェスト の
        /// いずれか不一致、Shader ソース変更フラグ、または AssetIndex が Snapshot より新しい場合に stale。
        /// </summary>
        public static bool IsSnapshotStale(NataneBuildUsageSnapshot snapshot)
        {
            if (snapshot == null) return true;

            if (snapshot.schemaVersion != NataneBuildUsageSnapshot.CurrentSchemaVersion) return true;
            if (snapshot.shaderDependencyFingerprint != NataneShaderUpdateAudit.ComputeCurrentDependencyFingerprint()) return true;
            if (snapshot.registryFingerprint != NataneShaderFeatureRegistry.ComputeRegistryFingerprint()) return true;
            if (snapshot.packageVersion != NataneShaderFeatureRegistry.ShaderCompatibilityVersion) return true;
            if (snapshot.unityVersion != Application.unityVersion) return true;

            var digest = new NataneOptimizationDigest
            {
                mode = NataneBuildPolicySettings.instance.OptimizationMode.ToString(),
                strictFailurePolicy = NataneBuildPolicySettings.instance.StrictFailurePolicy.ToString(),
                unknownKeywordPolicy = NataneBuildPolicySettings.instance.UnknownKeywordPolicy.ToString(),
                hlslFeatureGuardMode = NataneBuildPolicySettings.instance.HlslFeatureGuardMode.ToString(),
                buildPrewarmEnabled = NataneBuildPolicySettings.instance.BuildPrewarmEnabled,
                variantStrippingEnabled = NataneBuildPolicySettings.instance.VariantStrippingEnabled,
                legacyExactSetStrippingEnabled = NataneBuildPolicySettings.instance.LegacyExactSetStrippingEnabled
            };
            if (snapshot.optimizationDigest == null ||
                snapshot.optimizationDigest.ToStableString() != digest.ToStableString())
            {
                return true;
            }

            if (NataneShaderSourceChangeDetector.ShaderSourcesChangedSinceLastAudit) return true;

            if (NataneAssetIndexStore.TryLoadIndex(out var indexData) && indexData != null &&
                indexData.generatedAtUtcTicks > snapshot.assetIndexGeneratedAtUtcTicks)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// ビルドセッション単位の Snapshot 状態管理。
    /// VRChat ビルドで前回の古い static cache を誤用しないよう、セッション開始時に初期化する。
    /// SessionState ではなく static 保持。ドメインリロードで消えるが、
    /// ビルドは同一ドメイン内で完結するため許容（セッション開始で必ず再構築される想定）。
    /// </summary>
    public static class NataneBuildSession
    {
        private static NataneBuildUsageSnapshot current;
        private static bool active;

        public static bool IsActive => active;
        public static NataneBuildUsageSnapshot CurrentSnapshot => current;

        public static void BeginBuildSession()
        {
            current = null;
            active = true;
        }

        public static void SetSnapshot(NataneBuildUsageSnapshot snapshot)
        {
            current = snapshot;
        }

        public static void EndBuildSession()
        {
            current = null;
            active = false;
        }
    }
}
