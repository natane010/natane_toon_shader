using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Scene Workspace ウィンドウ。Profile の一覧/適用/現構成保存/直前へ戻る/Build Settings 反映/関連 Prefab ping。
    /// サービス層(NataneSceneWorkspaceService)を呼ぶだけ。Profile 列挙は明示操作時のみ（OnGUI 毎の FindAssets 禁止）。
    /// </summary>
    public sealed class NataneSceneWorkspaceWindow : EditorWindow
    {
        // 最近使った Profile（UI 状態なので EditorPrefs で保持）。
        private const string RecentProfileGuidPrefsKey = "NataneToon_SceneWorkspace_RecentProfile";

        // FindAssets 結果のキャッシュ（明示更新のみ再取得）。
        private List<NataneSceneProfile> cachedProfiles;
        private NataneSceneProfile selected;
        private Vector2 scroll;
        private NataneSceneApplyResult lastResult;

        [MenuItem("Tools/Natane/シーン Scene/シーンワークスペース Scene Workspace", false, 20)]
        public static void ShowWindow()
        {
            var window = GetWindow<NataneSceneWorkspaceWindow>();
            window.titleContent = new GUIContent(L("シーンワークスペース", "Scene Workspace"));
            window.minSize = new Vector2(560, 520);
            window.Show();
        }

        private void OnEnable()
        {
            // 起動時に 1 度だけ列挙（以降は明示更新ボタンのみ）。
            RefreshProfiles();
            RestoreRecentSelection();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawProfileListSection();
            EditorGUILayout.Space(6);
            DrawSelectedSection();
            EditorGUILayout.Space(6);
            DrawCaptureSection();
            EditorGUILayout.Space(6);
            DrawNotes();

            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        //  Profile 一覧
        // ============================================================

        private void DrawProfileListSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L("Scene プロファイル一覧", "Scene Profiles"), EditorStyles.boldLabel);
                if (GUILayout.Button(L("一覧を更新", "Refresh"), GUILayout.Width(110)))
                {
                    RefreshProfiles();
                }
            }

            if (cachedProfiles == null || cachedProfiles.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("プロジェクト内に Scene プロファイルがありません。下部で現構成から作成できます。",
                      "No Scene Profiles found. Create one from the current setup below."),
                    MessageType.Info);
                return;
            }

            // お気に入り優先で表示（次に名前順）。
            IEnumerable<NataneSceneProfile> ordered = cachedProfiles
                .Where(p => p != null)
                .OrderByDescending(p => p.IsFavorite)
                .ThenBy(p => p.name, System.StringComparer.OrdinalIgnoreCase);

            foreach (NataneSceneProfile profile in ordered)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    bool isSelected = profile == selected;
                    if (GUILayout.Toggle(isSelected, GUIContent.none, GUILayout.Width(16)) && !isSelected)
                    {
                        SelectProfile(profile);
                    }

                    EditorGUILayout.LabelField(
                        (profile.IsFavorite ? "★ " : "") + profile.name,
                        isSelected ? EditorStyles.boldLabel : EditorStyles.label);

                    if (GUILayout.Button(L("選択", "Ping"), GUILayout.Width(70)))
                    {
                        EditorGUIUtility.PingObject(profile);
                    }
                }
            }
        }

        // ============================================================
        //  選択中 Profile の操作
        // ============================================================

        private void DrawSelectedSection()
        {
            EditorGUILayout.LabelField(L("選択中のプロファイル", "Selected Profile"), EditorStyles.boldLabel);

            NataneSceneProfile changed = (NataneSceneProfile)EditorGUILayout.ObjectField(
                selected, typeof(NataneSceneProfile), false);
            if (changed != selected)
            {
                SelectProfile(changed);
            }

            if (selected == null)
            {
                EditorGUILayout.HelpBox(L("プロファイルを選択してください。", "Select a profile."), MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool fav = EditorGUILayout.Toggle(L("お気に入り", "Favorite"), selected.IsFavorite);
                if (fav != selected.IsFavorite)
                {
                    selected.IsFavorite = fav;
                    selected.Save();
                }
            }

            NataneSceneResolveResult resolved = NataneSceneWorkspaceService.ResolveProfile(selected);
            EditorGUILayout.LabelField(
                L($"Scene {resolved.scenes.Count} 件 / 警告 {resolved.warnings.Count} 件",
                  $"{resolved.scenes.Count} scenes / {resolved.warnings.Count} warnings"));

            foreach (NataneResolvedScene s in resolved.scenes)
            {
                EditorGUILayout.LabelField(
                    $"[{s.loadMode}{(s.isActive ? ", Active" : "")}]",
                    Path.GetFileName(s.path));
            }

            if (resolved.warnings.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", resolved.warnings), MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!resolved.HasAnyScene))
                {
                    if (GUILayout.Button(L("適用", "Apply"), GUILayout.Height(26)))
                    {
                        lastResult = NataneSceneWorkspaceService.ApplyProfile(selected);
                        TouchRecent(selected);
                    }
                }

                using (new EditorGUI.DisabledScope(!NataneSceneWorkspaceService.HasPreviousSnapshot()))
                {
                    if (GUILayout.Button(L("直前へ戻る", "Restore Previous"), GUILayout.Height(26)))
                    {
                        lastResult = NataneSceneWorkspaceService.RestorePrevious();
                    }
                }
            }

            // Build Settings 反映は明示操作 + 確認ダイアログ。ApplyProfile とは別ボタン。
            using (new EditorGUI.DisabledScope(!resolved.HasAnyScene))
            {
                if (GUILayout.Button(L("Build Settings へ反映（確認あり）", "Apply to Build Settings (confirm)")))
                {
                    if (EditorUtility.DisplayDialog(
                        L("Build Settings への反映", "Apply to Build Settings"),
                        L("現在の Build Settings の Scene 一覧を、このプロファイルの構成で上書きします。続行しますか？",
                          "This overwrites the Build Settings scene list with this profile. Continue?"),
                        L("反映", "Apply"), L("キャンセル", "Cancel")))
                    {
                        lastResult = NataneSceneWorkspaceService.ApplyToBuildSettings(selected);
                    }
                }
            }

            DrawRelatedPrefabs();
            DrawResult(lastResult);
        }

        private void DrawRelatedPrefabs()
        {
            if (selected.RelatedPrefabGuids == null || selected.RelatedPrefabGuids.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(L("関連 Prefab / キャラクター", "Related Prefabs"), EditorStyles.miniBoldLabel);
            foreach (string guid in selected.RelatedPrefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(path) ? $"(未解決) {guid}" : Path.GetFileName(path));
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
                    {
                        if (GUILayout.Button(L("表示", "Ping"), GUILayout.Width(70)))
                        {
                            Object obj = AssetDatabase.LoadMainAssetAtPath(path);
                            if (obj != null)
                            {
                                EditorGUIUtility.PingObject(obj);
                            }
                        }
                    }
                }
            }
        }

        private void DrawResult(NataneSceneApplyResult result)
        {
            if (result == null)
            {
                return;
            }

            var lines = new List<string>();
            if (result.aborted)
            {
                lines.Add(L($"中断: {result.abortReason}", $"Aborted: {result.abortReason}"));
            }
            else
            {
                lines.Add(L($"対象 Scene {result.openedCount} 件 / Active: {Path.GetFileName(result.activeScenePath ?? "-")}",
                    $"{result.openedCount} scenes / Active: {Path.GetFileName(result.activeScenePath ?? "-")}"));
            }

            if (result.warnings.Count > 0)
            {
                lines.AddRange(result.warnings);
            }

            EditorGUILayout.HelpBox(string.Join("\n", lines),
                result.Success ? MessageType.Info : MessageType.Warning);
        }

        // ============================================================
        //  現構成のキャプチャ
        // ============================================================

        private void DrawCaptureSection()
        {
            EditorGUILayout.LabelField(L("現構成から作成", "Capture Current"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("現在開いている Scene 構成を新しいプロファイルとして保存します。",
                  "Save the currently open scene setup as a new profile."));

            if (GUILayout.Button(L("現構成をプロファイルとして保存", "Save Current as Profile")))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    L("プロファイルの保存", "Save Profile"),
                    "NataneSceneProfile", "asset",
                    L("プロファイルの保存先を選択", "Choose where to save the profile"),
                    "Assets");
                if (!string.IsNullOrEmpty(path))
                {
                    NataneSceneProfile created = NataneSceneWorkspaceService.CaptureCurrentAsProfile(path);
                    RefreshProfiles();
                    SelectProfile(created);
                }
            }
        }

        private void DrawNotes()
        {
            EditorGUILayout.HelpBox(
                L("Scene 参照は GUID で保持され、Scene の移動/改名に自動追従します。適用時は未保存 Scene を保護します。",
                  "Scene references are stored by GUID and follow moves/renames. Unsaved scenes are protected on apply."),
                MessageType.Info);
            EditorGUILayout.HelpBox(
                L("このウィンドウは Build 対象の絞り込み(バリアントストリップ)には影響しません。Build Settings への反映は明示操作のみです。",
                  "This window does not affect variant stripping. Applying to Build Settings is an explicit action only."),
                MessageType.Info);
        }

        // ============================================================
        //  ヘルパ
        // ============================================================

        private void RefreshProfiles()
        {
            cachedProfiles = new List<NataneSceneProfile>();
            foreach (string guid in AssetDatabase.FindAssets("t:NataneSceneProfile"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<NataneSceneProfile>(path);
                if (profile != null)
                {
                    cachedProfiles.Add(profile);
                }
            }
        }

        private void SelectProfile(NataneSceneProfile profile)
        {
            selected = profile;
            lastResult = null;
            TouchRecent(profile);
        }

        private void TouchRecent(NataneSceneProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(profile);
            string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
            {
                EditorPrefs.SetString(RecentProfileGuidPrefsKey, guid);
            }
        }

        private void RestoreRecentSelection()
        {
            string guid = EditorPrefs.GetString(RecentProfileGuidPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                selected = AssetDatabase.LoadAssetAtPath<NataneSceneProfile>(path);
            }
        }
    }
}
