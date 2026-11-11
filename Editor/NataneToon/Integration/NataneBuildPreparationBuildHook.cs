using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NataneToon.Editor
{
    internal sealed class NataneBuildPreparationBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            // キーワード同期: .mat ファイルのキーワード状態をプロパティ値と一致させ、ディスクに保存する。
            // VRChat SDK がディスクの .mat を直接読むため、ビルド前にディスク上で正しい状態にしておく必要がある。
            NataneShaderKeywordSynchronizer.SynchronizeAllNataneMaterials();

            NataneBuildPreparationService.PrepareForBuild(forceRefresh: false, logSummary: true);

            // Build Usage Snapshot を 1 回生成しビルドセッションへ格納する（消費は後続ステージ）。
            // 生成失敗はエラーログのみで既存処理を継続する（現行挙動を維持）。
            try
            {
                NataneBuildSession.BeginBuildSession();
                // キーワードは上で既にディスク同期済みのため saveSyncToDisk:false（重複 SaveAssets を回避）。
                SnapshotBuildResult result = NataneBuildUsageSnapshotBuilder.Build(
                    report != null ? report.summary.platform : EditorUserBuildSettings.activeBuildTarget,
                    scenePathsOrNull: null,
                    saveSyncToDisk: false);

                if (result.Succeeded)
                {
                    NataneBuildUsageSnapshotStore.Save(result.Snapshot);
                    NataneBuildSession.SetSnapshot(result.Snapshot);
                }
                else
                {
                    Debug.LogError($"[Natane Snapshot] ビルド前 Snapshot 生成に失敗しました: {result.FailureReason}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Natane Snapshot] ビルド前 Snapshot 生成で例外が発生しました: {ex.Message}");
            }
        }
    }
}
