#if VRC_SDK_VRCSDK3
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using L = NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// VRChat SDK が導入されている場合のみコンパイルされるアバターアップロード用フック。
    ///
    /// この型を含むアセンブリ (NataneToon.Editor.VRChat) は defineConstraints
    /// ["VRC_SDK_VRCSDK3"] を持つため、SDK 非導入プロジェクトでは
    /// アセンブリごとコンパイル対象外となり、SDK への参照も一切評価されない。
    /// これにより本体 (NataneToon.Editor) は SDK 無しでも問題なくコンパイルできる。
    ///
    /// アバターのビルド前に:
    ///   1. 全 Natane マテリアルのキーワードをプロパティ値と同期する。
    ///   2. Lite (GrabPass 無し) 版へ変換できるアバターマテリアルをログ出力する（提案のみ）。
    /// </summary>
    public sealed class NataneVRChatBuildHook : IVRCSDKPreprocessAvatarCallback
    {
        // VRChat SDK 本体の処理より前に走らせる（SDK は 0 付近を使う）
        public int callbackOrder => -50;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            try
            {
                // 0. ビルドセッションを開始し、古い static cache / 前回 Snapshot の誤用を防ぐ。
                NataneBuildSession.BeginBuildSession();

                // 統合ストリッパーの集計を初期化する。VRChat では IPostprocessBuild が不発のため、
                // 前回アバタービルドで溜まったストリップ集計をここで掃き出して永続化する
                // （IPreprocessShaders は本コールバックより後に発火するため保存は 1 ビルド遅延する）。
                NataneUnifiedVariantStripper.BeginSession();

                // 1. キーワード同期（プロパティ値 ↔ シェーダーキーワードのズレを解消）
                NataneShaderKeywordSynchronizer.SynchronizeAllNataneMaterials();

                // 2. Build Usage Snapshot をプロジェクト全体で 1 回生成しセッションへ格納（消費は後続ステージ）。
                //    生成失敗はエラーログのみで継続（VRChat では IPostprocessBuild が不発のため後始末しない）。
                try
                {
                    SnapshotBuildResult result = NataneBuildUsageSnapshotBuilder.Build(
                        EditorUserBuildSettings.activeBuildTarget,
                        scenePathsOrNull: null,
                        saveSyncToDisk: false);
                    if (result.Succeeded)
                    {
                        NataneBuildUsageSnapshotStore.Save(result.Snapshot);
                        NataneBuildSession.SetSnapshot(result.Snapshot);
                    }
                    else
                    {
                        Debug.LogError($"[Natane Snapshot] VRChat ビルド前 Snapshot 生成に失敗しました: {result.FailureReason}");
                    }
                }
                catch (System.Exception snapshotEx)
                {
                    Debug.LogError($"[Natane Snapshot] VRChat ビルド前 Snapshot 生成で例外: {snapshotEx.Message}");
                }

                // 3. Lite 変換候補のログ出力（このアバターに含まれるマテリアルのみ）
                LogLiteConvertibleMaterials(avatarGameObject);
            }
            catch (System.Exception ex)
            {
                // フックの失敗でアップロードを止めない
                Debug.LogWarning($"[NataneToonShader] VRChat ビルドフックでエラー: {ex.Message}");
            }

            // true を返してビルドを継続
            return true;
        }

        private static void LogLiteConvertibleMaterials(GameObject avatarGameObject)
        {
            if (avatarGameObject == null)
                return;

            var reported = new HashSet<int>();
            var candidates = new List<NataneBuildOptimizationReport.LiteCandidate>();

            var renderers = avatarGameObject.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;
                    if (!reported.Add(material.GetInstanceID()))
                        continue;

                    if (NataneBuildOptimizationReport.TryGetLiteConversion(
                            material, out NataneBuildOptimizationReport.LiteCandidate candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                Debug.Log(L.L(
                    "[NataneToonShader] VRChat: Lite 版へ変換可能なマテリアルはありませんでした。",
                    "[NataneToonShader] VRChat: no materials are convertible to a Lite shader."));
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(L.L(
                $"[NataneToonShader] VRChat: {candidates.Count} 個のマテリアルが Lite 版へ変換できます（GrabPass を削減しパフォーマンス向上）:",
                $"[NataneToonShader] VRChat: {candidates.Count} material(s) can switch to a Lite shader (removes GrabPass, improves performance):"));
            foreach (var c in candidates)
                sb.AppendLine($"    - {c.currentShader} → {c.liteShader}  ({c.materialPath})");

            Debug.Log(sb.ToString());
        }
    }
}
#endif
