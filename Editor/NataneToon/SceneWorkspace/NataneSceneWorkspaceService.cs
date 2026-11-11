using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NataneToon.Editor
{
    /// <summary>
    /// Scene Workspace のコアサービス（UI 非依存・検証可能）。
    ///
    /// 重要な建付け:
    /// - Build Settings への反映は ApplyToBuildSettings のみが行う。ApplyProfile は絶対に触らない。
    /// - このステージでは Build Usage Snapshot / Variant Stripper には一切関与しない
    ///   （「開いている Scene だけを根拠にストリップしない」建付けの維持のため）。
    /// - SceneAsset 参照はすべて GUID 経由で解決し、解決失敗は例外にせず警告リストで返す。
    /// </summary>
    public static class NataneSceneWorkspaceService
    {
        // 直前構成スナップショットの保存先（UI 状態ではなく操作の巻き戻し用だが揮発で十分なため SessionState）。
        internal const string PrevSnapshotSessionKey = "NataneToon_SceneWorkspace_PrevSnapshot";

        // ApplyToBuildSettings が Build Settings を書き換えた回数。
        // ApplyProfile 経路からは決してインクリメントされないことをテストで検証する。
        internal static int BuildSettingsMutationCount;

        // ============================================================
        //  解決（GUID -> 実パス。失敗は警告へ）
        // ============================================================

        /// <summary>
        /// Profile 内の GUID をすべて実パスへ解決する。解決できないものは警告に積み、結果からは除外する。
        /// </summary>
        public static NataneSceneResolveResult ResolveProfile(NataneSceneProfile profile)
        {
            var result = new NataneSceneResolveResult();
            if (profile == null)
            {
                result.warnings.Add("プロファイルが null です");
                return result;
            }

            foreach (NataneSceneEntry entry in profile.SceneEntries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.sceneGuid))
                {
                    result.warnings.Add("空の Scene エントリをスキップしました");
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(entry.sceneGuid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    result.warnings.Add($"Scene が解決できません (GUID: {entry.sceneGuid})");
                    continue;
                }

                result.scenes.Add(new NataneResolvedScene
                {
                    guid = entry.sceneGuid,
                    path = path,
                    loadMode = entry.loadMode,
                    isActive = entry.isActiveScene
                });
            }

            result.playModeStartScenePath = ResolveOptional(profile.PlayModeStartSceneGuid, result.warnings, "PlayMode 開始 Scene");
            result.lightingScenePath = ResolveOptional(profile.LightingSceneGuid, result.warnings, "Lighting Scene");
            result.debugScenePath = ResolveOptional(profile.DebugSceneGuid, result.warnings, "Debug Scene");
            return result;
        }

        private static string ResolveOptional(string guid, List<string> warnings, string label)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                warnings.Add($"{label} が解決できません (GUID: {guid})");
                return null;
            }

            return path;
        }

        /// <summary>
        /// 実行計画を組み立てる（純関数寄り: AssetDatabase 参照のみ、Scene や Build Settings は変更しない）。
        /// 先頭 = Single、以降 = Additive。Single 指定が無ければ先頭エントリを Single とみなす。
        /// </summary>
        public static NataneSceneApplyPlan BuildApplyPlan(NataneSceneProfile profile)
        {
            var plan = new NataneSceneApplyPlan();
            NataneSceneResolveResult resolved = ResolveProfile(profile);
            plan.warnings.AddRange(resolved.warnings);
            plan.playModeStartScenePath = resolved.playModeStartScenePath;

            if (!resolved.HasAnyScene)
            {
                return plan;
            }

            // Single のベース Scene を決定（最初の Single 指定、無ければ先頭）。
            NataneResolvedScene single = resolved.scenes.FirstOrDefault(s => s.loadMode == NataneSceneLoadMode.Single)
                                         ?? resolved.scenes[0];

            var single2 = new NataneResolvedScene { guid = single.guid, path = single.path, loadMode = NataneSceneLoadMode.Single, isActive = single.isActive };
            plan.ordered.Add(single2);

            // 残りをリスト順に Additive で追加。
            foreach (NataneResolvedScene s in resolved.scenes)
            {
                if (ReferenceEquals(s, single))
                {
                    continue;
                }

                plan.ordered.Add(new NataneResolvedScene
                {
                    guid = s.guid,
                    path = s.path,
                    loadMode = NataneSceneLoadMode.Additive,
                    isActive = s.isActive
                });
            }

            // Active Scene: isActive 指定の最初のもの。無ければ Single。
            NataneResolvedScene active = plan.ordered.FirstOrDefault(s => s.isActive) ?? plan.ordered[0];
            plan.activeScenePath = active.path;
            return plan;
        }

        // ============================================================
        //  適用（未保存 Scene 保護 → Single → Additive → Active → PlayModeStart）
        // ============================================================

        /// <summary>
        /// Profile を適用する。適用前に未保存 Scene を保護（保存/破棄/キャンセル）し、
        /// 直前構成を SessionState へスナップショットしてから Scene を開く。
        /// Build Settings には一切触れない。
        /// </summary>
        public static NataneSceneApplyResult ApplyProfile(NataneSceneProfile profile)
        {
            return ApplyProfileInternal(profile, RealDirtyGuard, RealSceneExecutor);
        }

        /// <summary>
        /// テスト用シーム付きの適用本体。dirtyGuard は続行可否（false=キャンセルで中断）、
        /// executor は実際の Scene 開閉を担う。いずれの経路でも Build Settings は変更しない。
        /// </summary>
        internal static NataneSceneApplyResult ApplyProfileInternal(
            NataneSceneProfile profile,
            Func<bool> dirtyGuard,
            Action<NataneSceneApplyPlan> executor)
        {
            var result = new NataneSceneApplyResult();
            NataneSceneApplyPlan plan = BuildApplyPlan(profile);
            result.warnings.AddRange(plan.warnings);

            if (!plan.HasAnyScene)
            {
                // 全滅時は中断（何も開かない）。
                result.aborted = true;
                result.abortReason = "解決できる Scene がありません";
                return result;
            }

            // 未保存 Scene 保護。false（キャンセル）なら何もせず中断。
            if (dirtyGuard != null && !dirtyGuard())
            {
                result.aborted = true;
                result.abortReason = "ユーザーがキャンセルしました";
                return result;
            }

            // 直前構成を退避（1 段だけ戻せる）。
            SaveSnapshot(CaptureSnapshot());

            executor?.Invoke(plan);

            result.openedCount = plan.ordered.Count;
            result.activeScenePath = plan.activeScenePath;
            return result;
        }

        // 実際の未保存 Scene 保護。dirty があれば 3 択ダイアログ。true=続行 / false=中断。
        private static bool RealDirtyGuard()
        {
            var dirty = new List<Scene>();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty)
                {
                    dirty.Add(s);
                }
            }

            if (dirty.Count == 0)
            {
                return true;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "未保存の Scene",
                "未保存の変更があります。適用前にどうしますか？",
                "保存",      // 0
                "キャンセル", // 1
                "破棄");      // 2

            if (choice == 1)
            {
                return false;
            }

            if (choice == 0)
            {
                EditorSceneManager.SaveScenes(dirty.ToArray());
            }
            // choice == 2（破棄）は保存せずに続行。以降の Single Open で置き換えられる。
            return true;
        }

        // 実際の Scene 開閉。先頭 Single → 以降 Additive → Active → PlayModeStart。
        private static void RealSceneExecutor(NataneSceneApplyPlan plan)
        {
            for (int i = 0; i < plan.ordered.Count; i++)
            {
                NataneResolvedScene s = plan.ordered[i];
                OpenSceneMode mode = i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive;
                EditorSceneManager.OpenScene(s.path, mode);
            }

            if (!string.IsNullOrEmpty(plan.activeScenePath))
            {
                Scene active = SceneManager.GetSceneByPath(plan.activeScenePath);
                if (active.IsValid())
                {
                    EditorSceneManager.SetActiveScene(active);
                }
            }

            // PlayMode 開始 Scene（指定時のみ）。
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(plan.playModeStartScenePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(plan.playModeStartScenePath);
        }

        // ============================================================
        //  現構成のキャプチャ
        // ============================================================

        /// <summary>
        /// 現在開いている Scene 構成をエントリ列として取り出す（先頭 = Single、以降 = Additive）。
        /// 未保存の untitled Scene（パス無し）は対象外。
        /// </summary>
        public static List<NataneSceneEntry> CaptureCurrentEntries()
        {
            var entries = new List<NataneSceneEntry>();
            Scene activeScene = SceneManager.GetActiveScene();

            bool firstAssigned = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene s = EditorSceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(s.path))
                {
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(s.path);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                entries.Add(new NataneSceneEntry
                {
                    sceneGuid = guid,
                    loadMode = firstAssigned ? NataneSceneLoadMode.Additive : NataneSceneLoadMode.Single,
                    isActiveScene = s == activeScene
                });
                firstAssigned = true;
            }

            return entries;
        }

        /// <summary>
        /// 現構成を Profile 資産として保存する（明示操作）。
        /// </summary>
        public static NataneSceneProfile CaptureCurrentAsProfile(string assetPath)
        {
            var profile = ScriptableObject.CreateInstance<NataneSceneProfile>();
            profile.SceneEntries.AddRange(CaptureCurrentEntries());

            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ============================================================
        //  直前構成スナップショット（SessionState 往復）
        // ============================================================

        // 現在開いている Scene 構成をスナップショットにする。
        public static NataneSceneSnapshot CaptureSnapshot()
        {
            var snapshot = new NataneSceneSnapshot();
            Scene activeScene = SceneManager.GetActiveScene();

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene s = EditorSceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(s.path))
                {
                    continue;
                }

                snapshot.entries.Add(new NataneSceneSnapshotEntry
                {
                    path = s.path,
                    guid = AssetDatabase.AssetPathToGUID(s.path),
                    isLoaded = s.isLoaded,
                    isActive = s == activeScene
                });
            }

            return snapshot;
        }

        // 純データ往復（テスト対象）。JsonUtility で安全に文字列化。
        public static string SerializeSnapshot(NataneSceneSnapshot snapshot)
        {
            return snapshot == null ? string.Empty : JsonUtility.ToJson(snapshot);
        }

        public static NataneSceneSnapshot DeserializeSnapshot(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                NataneSceneSnapshot snapshot = JsonUtility.FromJson<NataneSceneSnapshot>(json);
                // schema 違いは復元に使わない（安全側）。
                if (snapshot != null && snapshot.schemaVersion == 1 && snapshot.entries != null)
                {
                    return snapshot;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Scene Workspace] スナップショットの復元に失敗: {ex.Message}");
            }

            return null;
        }

        internal static void SaveSnapshot(NataneSceneSnapshot snapshot)
        {
            SessionState.SetString(PrevSnapshotSessionKey, SerializeSnapshot(snapshot));
        }

        internal static NataneSceneSnapshot LoadSnapshot()
        {
            return DeserializeSnapshot(SessionState.GetString(PrevSnapshotSessionKey, string.Empty));
        }

        public static bool HasPreviousSnapshot()
        {
            NataneSceneSnapshot snapshot = LoadSnapshot();
            return snapshot != null && snapshot.entries.Count > 0;
        }

        /// <summary>
        /// ApplyProfile 直前に退避したスナップショットへ 1 段だけ戻す。
        /// GUID が動いていれば GUID から現パスを解決し直す。
        /// </summary>
        public static NataneSceneApplyResult RestorePrevious()
        {
            return RestorePreviousInternal(RealDirtyGuard, RealSnapshotExecutor);
        }

        internal static NataneSceneApplyResult RestorePreviousInternal(
            Func<bool> dirtyGuard,
            Action<NataneSceneSnapshot> executor)
        {
            var result = new NataneSceneApplyResult();
            NataneSceneSnapshot snapshot = LoadSnapshot();
            if (snapshot == null || snapshot.entries.Count == 0)
            {
                result.aborted = true;
                result.abortReason = "戻せる直前構成がありません";
                return result;
            }

            if (dirtyGuard != null && !dirtyGuard())
            {
                result.aborted = true;
                result.abortReason = "ユーザーがキャンセルしました";
                return result;
            }

            executor?.Invoke(snapshot);
            result.openedCount = snapshot.entries.Count;
            result.activeScenePath = snapshot.entries.FirstOrDefault(e => e.isActive)?.path;
            return result;
        }

        private static void RealSnapshotExecutor(NataneSceneSnapshot snapshot)
        {
            bool firstOpened = false;
            foreach (NataneSceneSnapshotEntry entry in snapshot.entries)
            {
                // GUID から現在のパスを解決し直す（移動追従）。無ければ保存時パスにフォールバック。
                string path = string.IsNullOrEmpty(entry.guid) ? entry.path : AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(path))
                {
                    path = entry.path;
                }

                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                OpenSceneMode mode = firstOpened ? OpenSceneMode.Additive : OpenSceneMode.Single;
                EditorSceneManager.OpenScene(path, mode);
                firstOpened = true;
            }

            NataneSceneSnapshotEntry activeEntry = snapshot.entries.FirstOrDefault(e => e.isActive);
            if (activeEntry != null)
            {
                Scene active = SceneManager.GetSceneByPath(activeEntry.path);
                if (active.IsValid())
                {
                    EditorSceneManager.SetActiveScene(active);
                }
            }
        }

        // ============================================================
        //  Build Settings 反映（明示操作のみ・ApplyProfile からは呼ばれない）
        // ============================================================

        /// <summary>
        /// Profile の Scene 構成を Build Settings（EditorBuildSettings.scenes）へ反映する。
        /// これは ApplyProfile とは完全に独立した明示操作。UI 側でも確認ダイアログ付きの別ボタンから呼ぶ。
        /// </summary>
        public static NataneSceneApplyResult ApplyToBuildSettings(NataneSceneProfile profile)
        {
            // Build Settings を書き換えた事実を記録（呼び出し分離のテスト用カウンタ）。
            BuildSettingsMutationCount++;

            var result = new NataneSceneApplyResult();
            NataneSceneResolveResult resolved = ResolveProfile(profile);
            result.warnings.AddRange(resolved.warnings);

            if (!resolved.HasAnyScene)
            {
                result.aborted = true;
                result.abortReason = "反映できる Scene がありません";
                return result;
            }

            var scenes = resolved.scenes
                .Select(s => new EditorBuildSettingsScene(s.path, true))
                .ToArray();
            EditorBuildSettings.scenes = scenes;

            result.openedCount = scenes.Length;
            return result;
        }
    }
}
