using System.Text;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Build Usage Snapshot の手動生成メニュー。生成結果を保存し、Console に日本語で要約表示する。
    /// forceRebuild=true でキャッシュ（既存の鮮度良好な Snapshot）を無視して再構築する。
    /// </summary>
    public static class NataneBuildUsageSnapshotMenu
    {
        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/Snapshotを生成 Generate Build Usage Snapshot", false, 61)]
        public static void GenerateFromMenu()
        {
            Generate(forceRebuild: false);
        }

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/Snapshotを再構築（キャッシュ無視）Rebuild Snapshot", false, 62)]
        public static void RebuildFromMenu()
        {
            Generate(forceRebuild: true);
        }

        public static NataneBuildUsageSnapshot Generate(bool forceRebuild)
        {
            // 鮮度良好な既存 Snapshot があり forceRebuild でなければ再利用する。
            if (!forceRebuild &&
                NataneBuildUsageSnapshotStore.TryLoad(out var existing) &&
                !NataneBuildUsageSnapshotStore.IsSnapshotStale(existing))
            {
                Debug.Log("[Natane Snapshot] 既存 Snapshot が最新のため再利用しました（再構築するには「キャッシュ無視」を使用）。");
                LogSummary(existing, reused: true);
                return existing;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            SnapshotBuildResult result = NataneBuildUsageSnapshotBuilder.Build(target, scenePathsOrNull: null, saveSyncToDisk: false);

            if (result.Succeeded)
            {
                NataneBuildUsageSnapshotStore.Save(result.Snapshot);
            }

            LogSummary(result.Snapshot, reused: false, succeeded: result.Succeeded, failureReason: result.FailureReason);
            return result.Snapshot;
        }

        // -executeMethod からの実行用（Exit は -quit に委ねる）。
        public static void GenerateFromBatch()
        {
            NataneBuildUsageSnapshot snapshot = Generate(forceRebuild: true);
            Debug.Log($"SNAPSHOT_FINGERPRINT={snapshot?.snapshotFingerprint}");
            Debug.Log($"SNAPSHOT_JSON_PATH={NataneBuildUsageSnapshotStore.SnapshotPath}");
            Debug.Log($"SNAPSHOT_MATERIAL_COUNT={snapshot?.materialConfigs.Count}");
            Debug.Log($"SNAPSHOT_SHADER_COUNT={snapshot?.shaderUsages.Count}");
        }

        private static void LogSummary(NataneBuildUsageSnapshot s, bool reused, bool succeeded = true, string failureReason = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Natane Build Usage Snapshot]");
            if (!succeeded)
            {
                sb.AppendLine($"  生成失敗: {failureReason}");
            }
            sb.AppendLine($"  再利用: {(reused ? "はい" : "いいえ")} / 最適化モード: {s?.optimizationDigest?.mode}");
            sb.AppendLine($"  BuildTarget: {s?.buildTarget} / Unity: {s?.unityVersion} / Package: {s?.packageVersion}");
            sb.AppendLine($"  Fingerprint: {s?.snapshotFingerprint}");
            sb.AppendLine($"  Shader 使用: {s?.shaderUsages.Count} 件 / Material 構成: {s?.materialConfigs.Count} 件");
            sb.AppendLine($"  Animation 由来キーワード: {s?.animationDrivenKeywords.Count} クリップ");
            sb.AppendLine($"  Animator-Clip 依存: {s?.animatorClipDependencies.Count} 件 / Prefab・Scene 依存: {s?.prefabSceneDependencies.Count} 件");
            if (s?.auditSummary != null)
            {
                sb.AppendLine($"  監査要約: 未知 {s.auditSummary.unknownKeywordCount} / 孤児 {s.auditSummary.orphanDefinitionCount} / 解析不能 {s.auditSummary.unparseableItemCount}{(s.auditSummary.auditSkipped ? "（監査省略）" : "")}");
            }
            sb.AppendLine($"  常時保持 K/M/S: {s?.alwaysKeepKeywords.Count}/{s?.alwaysKeepMaterialGuids.Count}/{s?.alwaysKeepShaderGuids.Count} / SVC: {s?.shaderVariantCollectionGuids.Count} / Runtime動的: {s?.runtimeDynamicKeywords.Count}");
            sb.AppendLine($"  警告: {s?.warnings.Count} 件");
            if (s?.warnings != null)
            {
                foreach (string w in s.warnings) sb.AppendLine("    - " + w);
            }
            Debug.Log(sb.ToString());
        }
    }
}
