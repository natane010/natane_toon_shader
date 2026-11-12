using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NataneToon.Editor
{
    /// <summary>
    /// VRChat SDK 等のビルドパイプラインでマテリアルキーワードを同期する。
    ///
    /// VRChat SDK は BuildAssetBundle を使うため IPreprocessBuildWithReport が
    /// 呼ばれない。唯一確実に呼ばれる IProcessSceneWithReport を使って、
    /// ビルド前に全マテリアルのキーワードをプロパティ値と同期しディスクに保存する。
    /// </summary>
    internal sealed class NataneBuildSceneKeywordSync : IProcessSceneWithReport
    {
        // VRChat SDK より先に実行（VRChat SDK は通常 callbackOrder = 0 付近）
        public int callbackOrder => -50;

        // 同一ビルド内で複数シーンが処理される場合に全マテリアル同期を1回だけ実行する
        private static bool _hasSyncedThisBuild;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // エディタ再生時は処理しない（ビルド時のみ）
            if (report == null)
                return;

            // --- Phase 1: 全マテリアルのキーワード同期 + ディスク保存（1回だけ） ---
            // VRChat SDK は BuildAssetBundle を使うため IPreprocessBuildWithReport が
            // 呼ばれない。ここで全マテリアルを同期してディスクに書き込むことで、
            // アセットバンドルに正しいキーワード状態が焼き込まれる。
            if (!_hasSyncedThisBuild)
            {
                _hasSyncedThisBuild = true;

                // EditorApplication.delayCall でビルド完了後にフラグをリセット
                EditorApplication.delayCall += () => _hasSyncedThisBuild = false;

                NataneShaderKeywordSynchronizer.SynchronizeAllNataneMaterials();
                Debug.Log($"[NataneToonShader] ビルド時キーワード同期: 全マテリアルを同期してディスクに保存しました");
            }

            // --- Phase 2: シーン内マテリアルのインメモリ修正（安全ネット） ---
            // ディスク保存後にシーンがロードされた場合や、シーンローカルな
            // マテリアル参照に対する追加の安全ネット。
            var renderers = new List<Renderer>();
            var rootObjects = scene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                root.GetComponentsInChildren(true, renderers);
            }

            int fixedCount = 0;
            var processedMaterials = new HashSet<int>(); // instanceID で重複排除

            foreach (var renderer in renderers)
            {
                var sharedMaterials = renderer.sharedMaterials;
                foreach (var material in sharedMaterials)
                {
                    if (material == null || material.shader == null)
                        continue;

                    int id = NataneToon.Editor.NataneEditorCompat.GetObjectId(material);
                    if (!processedMaterials.Add(id))
                        continue;

                    if (!NataneShaderCatalog.IsNataneShader(material.shader.name))
                        continue;

                    if (NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(material))
                    {
                        fixedCount++;
                    }
                }
            }

            if (fixedCount > 0)
            {
                Debug.Log($"[NataneToonShader] ビルド時キーワード同期(シーン内): シーン '{scene.name}' の {fixedCount} マテリアルを追加修正しました");
            }
        }
    }
}
