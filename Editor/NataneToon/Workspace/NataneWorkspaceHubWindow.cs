using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// 統合ワークスペース（Asset Workspace &amp; Build Optimizer）。
    /// 既存機能の薄いホスト: ロジックは再実装せず各サービス/ウィンドウを呼ぶだけ。
    /// FindAssets や重い集計は OnEnable / 更新ボタン押下時のみ実行しキャッシュする（OnGUI 毎の走査禁止）。
    /// UI 状態（選択タブ/展開）は EditorPrefs、ビルド結果に影響する設定は ProjectSettings 側に置く。
    /// </summary>
    public sealed class NataneWorkspaceHubWindow : EditorWindow
    {
        private const string SettingsProviderPath = "Project/Natane Toon/Scalability";
        private const string SelectedTabPrefsKey = "NataneToon_WorkspaceHub_Tab";
        private const string ExpandReportPrefsKey = "NataneToon_WorkspaceHub_ExpandReport";

        private enum Tab { Assets, Scenes, BuildOptimization, ShaderUpdates, Reports }

        private static readonly string[] TabLabelsJa =
            { "アセット", "シーン", "ビルド最適化", "Shader 更新", "レポート" };
        private static readonly string[] TabLabelsEn =
            { "Assets", "Scenes", "Build Optimization", "Shader Updates", "Reports" };

        private Tab _tab;
        private Vector2 _scroll;

        // ---- キャッシュ（明示更新時のみ再取得） ----
        private WorkspaceSnapshotData _cache;
        private List<NataneSceneProfile> _sceneProfiles;
        private NataneSceneProfile _selectedProfile;
        private NataneUpdateStatus _updateStatus;
        private List<string> _reportFiles;
        private ReportSummaryDto _selectedReport;
        private ReportSummaryDto _selectedReportPrev;
        private string _selectedReportFile;

        [MenuItem("Tools/Natane/ワークスペース Workspace/Asset Workspace & Build Optimizer", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<NataneWorkspaceHubWindow>();
            window.titleContent = new GUIContent(L("ワークスペース", "Workspace"));
            window.minSize = new Vector2(620, 560);
            window.Show();
        }

        private void OnEnable()
        {
            _tab = (Tab)EditorPrefs.GetInt(SelectedTabPrefsKey, 0);
            RefreshAll();
        }

        // ============================================================
        //  データ収集（明示更新時のみ）
        // ============================================================

        private void RefreshAll()
        {
            _cache = WorkspaceSnapshotData.Collect();
            RefreshSceneProfiles();
            _updateStatus = NataneMigrationService.GetUpdateStatus();
            RefreshReportList();
        }

        private void RefreshSceneProfiles()
        {
            _sceneProfiles = new List<NataneSceneProfile>();
            foreach (string guid in AssetDatabase.FindAssets("t:NataneSceneProfile"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<NataneSceneProfile>(path);
                if (profile != null) _sceneProfiles.Add(profile);
            }
        }

        private void RefreshReportList()
        {
            _reportFiles = new List<string>();
            try
            {
                string dir = NataneAssetIndexStore.ReportDirectoryPath;
                if (Directory.Exists(dir))
                {
                    _reportFiles = Directory.GetFiles(dir, "*.json")
                        .OrderByDescending(f => f, StringComparer.Ordinal) // タイムスタンプ名＝新しい順
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Workspace] レポート一覧の取得に失敗: {ex.Message}");
            }
        }

        // ============================================================
        //  描画
        // ============================================================

        private void OnGUI()
        {
            DrawStatusBanner();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int current = (int)_tab;
                int next = GUILayout.Toolbar(current, IsJapanese ? TabLabelsJa : TabLabelsEn, EditorStyles.toolbarButton);
                if (next != current)
                {
                    _tab = (Tab)next;
                    EditorPrefs.SetInt(SelectedTabPrefsKey, next);
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L("全体更新", "Refresh"), EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    RefreshAll();
                }
                if (GUILayout.Button(L("設定を開く", "Settings"), EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    SettingsService.OpenProjectSettings(SettingsProviderPath);
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Assets: DrawAssetsTab(); break;
                case Tab.Scenes: DrawScenesTab(); break;
                case Tab.BuildOptimization: DrawBuildOptimizationTab(); break;
                case Tab.ShaderUpdates: DrawShaderUpdatesTab(); break;
                case Tab.Reports: DrawReportsTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusBanner()
        {
            if (_cache == null) return;

            NataneWorkspaceStatus status = NataneWorkspaceStatusEvaluator.ComputeWorkspaceStatus(_cache.StatusInput);
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = ColorFor(status.level);
            var box = new GUIStyle(EditorStyles.helpBox) { fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true };
            EditorGUILayout.LabelField(
                BadgeText(status.level) + "  " + (IsJapanese ? status.messageJa : status.messageEn), box);
            GUI.backgroundColor = prev;
        }

        private static Color ColorFor(NataneWorkspaceStatusLevel level)
        {
            switch (level)
            {
                case NataneWorkspaceStatusLevel.Safe: return new Color(0.55f, 0.85f, 0.55f);
                case NataneWorkspaceStatusLevel.Attention: return new Color(0.95f, 0.85f, 0.45f);
                case NataneWorkspaceStatusLevel.Danger: return new Color(0.95f, 0.5f, 0.5f);
                case NataneWorkspaceStatusLevel.OptimizationStopped: return new Color(0.7f, 0.7f, 0.7f);
                case NataneWorkspaceStatusLevel.KeepAllFallback: return new Color(0.97f, 0.7f, 0.4f);
                default: return Color.white;
            }
        }

        private static string BadgeText(NataneWorkspaceStatusLevel level)
        {
            switch (level)
            {
                case NataneWorkspaceStatusLevel.Safe: return L("[安全]", "[SAFE]");
                case NataneWorkspaceStatusLevel.Attention: return L("[要確認]", "[ATTENTION]");
                case NataneWorkspaceStatusLevel.Danger: return L("[危険]", "[DANGER]");
                case NataneWorkspaceStatusLevel.OptimizationStopped: return L("[停止中]", "[STOPPED]");
                case NataneWorkspaceStatusLevel.KeepAllFallback: return L("[全保持]", "[KEEP-ALL]");
                default: return "";
            }
        }

        // ============================================================
        //  Assets タブ
        // ============================================================

        private void DrawAssetsTab()
        {
            EditorGUILayout.LabelField(L("アセット整理 (Asset Organizer)", "Asset Organizer"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                L("GUID を維持したままアセットを整理します。プレビュー/適用/ロールバックは専用ウィンドウで行います。",
                  "Organizes assets while preserving GUIDs. Preview/apply/rollback happen in the dedicated window."),
                MessageType.Info);

            if (GUILayout.Button(L("Asset Organizer を開く", "Open Asset Organizer"), GUILayout.Height(26)))
            {
                NataneAssetOrganizerWindow.ShowWindow();
            }

            EditorGUILayout.Space(4);
            NataneMoveHistory history = NataneAssetOrganizerService.LoadHistory();
            int batches = history?.batches?.Count ?? 0;
            int active = history?.batches?.Count(b => b != null && !b.rolledBack && b.records.Count > 0) ?? 0;
            EditorGUILayout.LabelField(L("移動履歴", "Move History"), $"{batches} " + L("バッチ", "batches") +
                $" / " + L("有効", "active") + $" {active}");
        }

        // ============================================================
        //  Scenes タブ
        // ============================================================

        private void DrawScenesTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L("Scene プロファイル", "Scene Profiles"), EditorStyles.boldLabel);
                if (GUILayout.Button(L("一覧更新", "Refresh"), GUILayout.Width(90)))
                {
                    RefreshSceneProfiles();
                }
            }

            if (_sceneProfiles == null || _sceneProfiles.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("Scene プロファイルがありません。Scene Workspace で現構成から作成できます。",
                      "No Scene Profiles. Create one from the current setup in Scene Workspace."),
                    MessageType.Info);
                if (GUILayout.Button(L("Scene Workspace を開く", "Open Scene Workspace")))
                {
                    NataneSceneWorkspaceWindow.ShowWindow();
                }
                return;
            }

            // お気に入り優先 → 名前順。
            IEnumerable<NataneSceneProfile> ordered = _sceneProfiles
                .Where(p => p != null)
                .OrderByDescending(p => p.IsFavorite)
                .ThenBy(p => p.name, StringComparer.OrdinalIgnoreCase);

            foreach (NataneSceneProfile profile in ordered)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    bool isSel = profile == _selectedProfile;
                    if (GUILayout.Toggle(isSel, GUIContent.none, GUILayout.Width(16)) && !isSel)
                        _selectedProfile = profile;
                    EditorGUILayout.LabelField((profile.IsFavorite ? "★ " : "") + profile.name,
                        isSel ? EditorStyles.boldLabel : EditorStyles.label);
                    if (GUILayout.Button(L("表示", "Ping"), GUILayout.Width(60)))
                        EditorGUIUtility.PingObject(profile);
                }
            }

            if (_selectedProfile == null)
            {
                EditorGUILayout.HelpBox(L("プロファイルを選択してください。", "Select a profile."), MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            NataneSceneResolveResult resolved = NataneSceneWorkspaceService.ResolveProfile(_selectedProfile);
            EditorGUILayout.LabelField(L($"Scene {resolved.scenes.Count} 件 / 警告 {resolved.warnings.Count} 件",
                $"{resolved.scenes.Count} scenes / {resolved.warnings.Count} warnings"));

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!resolved.HasAnyScene))
                {
                    if (GUILayout.Button(L("適用", "Apply"), GUILayout.Height(24)))
                        NataneSceneWorkspaceService.ApplyProfile(_selectedProfile);
                }
                using (new EditorGUI.DisabledScope(!NataneSceneWorkspaceService.HasPreviousSnapshot()))
                {
                    if (GUILayout.Button(L("直前へ戻る", "Restore"), GUILayout.Height(24)))
                        NataneSceneWorkspaceService.RestorePrevious();
                }
            }

            if (GUILayout.Button(L("現構成をプロファイルとして保存", "Save Current as Profile")))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    L("プロファイルの保存", "Save Profile"), "NataneSceneProfile", "asset",
                    L("保存先を選択", "Choose destination"), "Assets");
                if (!string.IsNullOrEmpty(path))
                {
                    _selectedProfile = NataneSceneWorkspaceService.CaptureCurrentAsProfile(path);
                    RefreshSceneProfiles();
                }
            }
        }

        // ============================================================
        //  Build Optimization タブ
        // ============================================================

        private void DrawBuildOptimizationTab()
        {
            if (_cache == null) { EditorGUILayout.HelpBox(L("データ未取得", "No data"), MessageType.Info); return; }
            var c = _cache;

            EditorGUILayout.LabelField(L("現在のモード", "Current Mode"), EditorStyles.boldLabel);
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = ModeColor(c.mode);
            EditorGUILayout.LabelField(c.mode.ToString() + (c.variantStrippingEnabled ? "" : L("（ストリップ OFF）", " (stripping off)")),
                EditorStyles.helpBox);
            GUI.backgroundColor = prev;

            if (c.mode == NataneOptimizationMode.Aggressive)
            {
                if (c.AggressiveGateReasons.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        L("Aggressive は実行されません（続行ガード不通過）:\n", "Aggressive will NOT run (guard failed):\n") +
                        " - " + string.Join("\n - ", c.AggressiveGateReasons),
                        MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        L("Aggressive が有効です。実在構成に無いバリアントは削除されます。",
                          "Aggressive enabled. Variants absent from real configs will be stripped."),
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(L("鮮度", "Freshness"), EditorStyles.boldLabel);
            Row(L("Index 生成時刻", "Index generated"), c.indexGeneratedText);
            Row(L("Snapshot", "Snapshot"), c.snapshotExists
                ? (c.snapshotStale ? L("あり（stale）", "present (stale)") : L("あり（新鮮）", "present (fresh)"))
                : L("なし", "none"));
            Row(L("Registry", "Registry"), $"schema {c.registrySchemaVersion} / fp {c.registryFingerprintShort}");
            Row(L("監査", "Audit"), c.auditStale ? L("stale（要再監査）", "stale (re-audit)") : L("新鮮", "fresh"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(L("使用状況（Snapshot 由来）", "Usage (from Snapshot)"), EditorStyles.boldLabel);
            if (c.snapshotExists)
            {
                Row(L("使用機能キーワード数", "Used feature keywords"), c.usedKeywordTotal.ToString());
                Row(L("Shader 別 Keyword 数", "Per-shader keyword counts"), c.perShaderKeywordText);
                Row(L("Animation 由来", "Animation-driven"), c.animationClipCount + L(" クリップ", " clips"));
                Row(L("Runtime 動的", "Runtime dynamic"), c.runtimeDynamicCount.ToString());
                Row(L("常時保持 K/M/S", "Always-keep K/M/S"), $"{c.alwaysKeepKeywordCount}/{c.alwaysKeepMaterialCount}/{c.alwaysKeepShaderCount}");
                Row(L("未知 / 解析不能", "Unknown / Unparseable"), $"{c.unknownKeywordCount} / {c.unparseableCount}");
            }
            else
            {
                EditorGUILayout.HelpBox(L("Snapshot がありません。下部の再構築で生成できます。",
                    "No snapshot. Generate it via the rebuild button below."), MessageType.Info);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(L("最新ストリップレポート", "Latest Stripping Report"), EditorStyles.boldLabel);
            if (c.latestReport != null)
            {
                Row(L("モード / 保持 / 削除", "Mode / Kept / Stripped"),
                    $"{c.latestReport.mode} / {c.latestReport.totalKept} / {c.latestReport.totalStripped}");
                Row(L("削除候補 / Aggressive 候補", "Candidates / Aggressive"),
                    $"{c.latestReport.totalCandidates} / {c.latestReport.aggressiveCandidateCount}");
                if (c.latestReport.fallbackReasons != null && c.latestReport.fallbackReasons.Count > 0)
                    EditorGUILayout.HelpBox(L("フォールバック理由:\n", "Fallback reasons:\n") +
                        " - " + string.Join("\n - ", c.latestReport.fallbackReasons), MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(L("（レポートなし）", "(no report)"));
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(L("キャッシュ", "Cache"), EditorStyles.boldLabel);
            Row(L("HLSL ガード state", "HLSL guard state"), c.hlslGuardStateExists ? L("あり", "present") : L("なし", "none"));

            EditorGUILayout.Space(8);
            if (GUILayout.Button(L("強制再構築（Snapshot 再生成 + HLSL ガードリセット + 再監査）",
                "Force Rebuild (regenerate Snapshot + reset HLSL guard + re-audit)"), GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog(
                    L("強制再構築", "Force Rebuild"),
                    L("Snapshot を再生成し、HLSL ガード状態をリセットし、Shader 監査を再実行します。続行しますか？",
                      "Regenerate Snapshot, reset HLSL guard state, and re-run the shader audit. Continue?"),
                    L("実行", "Run"), L("キャンセル", "Cancel")))
                {
                    ForceRebuild();
                }
            }
        }

        private void ForceRebuild()
        {
            try
            {
                NataneBuildUsageSnapshotMenu.Generate(forceRebuild: true);
                NataneBuildFeatureOptimizer.ResetHlslGuardStateFromMenu();
                NataneShaderAuditData audit = NataneShaderUpdateAudit.Run();
                NataneShaderUpdateAudit.Save(audit);
                NataneShaderSourceChangeDetector.ClearChangedFlag();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Workspace] 強制再構築中に例外: {ex.Message}");
            }
            finally
            {
                RefreshAll();
            }
        }

        private static Color ModeColor(NataneOptimizationMode mode)
        {
            switch (mode)
            {
                case NataneOptimizationMode.Disabled: return new Color(0.7f, 0.7f, 0.7f);
                case NataneOptimizationMode.ReportOnly: return new Color(0.6f, 0.75f, 0.95f);
                case NataneOptimizationMode.Safe: return new Color(0.55f, 0.85f, 0.55f);
                case NataneOptimizationMode.Aggressive: return new Color(0.95f, 0.5f, 0.5f);
                default: return Color.white;
            }
        }

        // ============================================================
        //  Shader Updates タブ
        // ============================================================

        private void DrawShaderUpdatesTab()
        {
            if (_updateStatus == null) _updateStatus = NataneMigrationService.GetUpdateStatus();
            NataneUpdateStatus s = _updateStatus;

            EditorGUILayout.LabelField(L("Shader アップデート状態", "Shader Update Status"), EditorStyles.boldLabel);
            Row(L("最終監査 version", "Last audit version"), s.hasAudit ? s.lastAuditPackageVersion : L("（未監査）", "(none)"));
            Row(L("現行 version", "Current version"), s.currentPackageVersion);
            Row(L("Unity", "Unity"), s.currentUnityVersion);
            Row(L("未知 Keyword", "Unknown keywords"), s.unknownKeywordCount.ToString());
            Row(L("孤児定義", "Orphan definitions"), s.orphanDefinitionCount.ToString());
            Row(L("対象 Shader 不在", "Missing target shaders"), s.missingTargetShaderCount.ToString());
            Row(L("Property 欠落", "Missing properties"), s.missingPropertyCount.ToString());
            Row(L("解析不能", "Unparseable"), s.unparseableItemCount.ToString());
            Row(L("Migration 候補", "Migration candidates"), s.migrationCandidateCount.ToString());
            Row(L("Registry 互換", "Registry compatible"), s.registrySchemaMatches ? "OK" : L("不一致", "mismatch"));
            Row(L("監査鮮度", "Audit freshness"), s.auditStale ? L("stale", "stale") : L("新鮮", "fresh"));

            if (s.notes != null && s.notes.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", s.notes), MessageType.Info);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("再監査", "Re-audit"), GUILayout.Height(26)))
                {
                    NataneShaderAuditData audit = NataneShaderUpdateAudit.Run();
                    NataneShaderUpdateAudit.Save(audit);
                    NataneShaderSourceChangeDetector.ClearChangedFlag();
                    _updateStatus = NataneMigrationService.GetUpdateStatus();
                }
                if (GUILayout.Button(L("Migration プレビュー", "Migration Preview"), GUILayout.Height(26)))
                {
                    NataneMigrationPreviewWindow.Open();
                }
            }
        }

        // ============================================================
        //  Reports タブ
        // ============================================================

        private void DrawReportsTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L("レポート（新しい順）", "Reports (newest first)"), EditorStyles.boldLabel);
                if (GUILayout.Button(L("一覧更新", "Refresh"), GUILayout.Width(90)))
                    RefreshReportList();
            }

            if (_reportFiles == null || _reportFiles.Count == 0)
            {
                EditorGUILayout.HelpBox(L("レポートがありません（ビルド後に生成されます）。",
                    "No reports (generated after builds)."), MessageType.Info);
                return;
            }

            foreach (string file in _reportFiles.Take(40))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    bool isSel = file == _selectedReportFile;
                    if (GUILayout.Toggle(isSel, GUIContent.none, GUILayout.Width(16)) && !isSel)
                        SelectReport(file);
                    EditorGUILayout.LabelField(Path.GetFileName(file),
                        isSel ? EditorStyles.boldLabel : EditorStyles.miniLabel);
                }
            }

            if (_selectedReport != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(L("要約", "Summary"), EditorStyles.boldLabel);
                Row(L("Context / モード", "Context / Mode"), $"{_selectedReport.context} / {_selectedReport.mode}");
                Row(L("生成", "Generated"), _selectedReport.generatedAt);
                Row(L("対象 / 保持 / 削除", "Seen / Kept / Stripped"),
                    $"{_selectedReport.totalSeen} / {_selectedReport.totalKept} / {_selectedReport.totalStripped}");
                Row(L("削除候補", "Candidates"), _selectedReport.totalCandidates.ToString());
                if (_selectedReport.fallbackReasons != null && _selectedReport.fallbackReasons.Count > 0)
                    EditorGUILayout.HelpBox(L("フォールバック理由:\n", "Fallback reasons:\n") +
                        " - " + string.Join("\n - ", _selectedReport.fallbackReasons), MessageType.Warning);

                var cmp = _selectedReport.previousComparison;
                if (cmp != null && !string.IsNullOrEmpty(cmp.previousReportFile))
                {
                    Row(L("前回との差分（保持/削除/候補）", "Delta vs previous (kept/stripped/candidates)"),
                        $"{Signed(cmp.deltaKept)} / {Signed(cmp.deltaStripped)} / {Signed(cmp.deltaCandidates)}");
                    EditorGUILayout.LabelField(L("　前回", "  prev"), cmp.previousReportFile, EditorStyles.miniLabel);
                }
            }
        }

        private void SelectReport(string file)
        {
            _selectedReportFile = file;
            _selectedReport = null;
            try
            {
                _selectedReport = JsonUtility.FromJson<ReportSummaryDto>(File.ReadAllText(file));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Workspace] レポート読込に失敗: {ex.Message}");
            }
        }

        private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

        private static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(240));
                EditorGUILayout.SelectableLabel(value ?? "-", GUILayout.Height(16));
            }
        }
    }

    // ============================================================
    //  収集済みデータ（OnGUI では読むだけ）
    // ============================================================

    internal sealed class WorkspaceSnapshotData
    {
        public NataneOptimizationMode mode;
        public bool variantStrippingEnabled;
        public bool auditStale;
        public int unknownKeywordCount;
        public int unparseableCount;

        public bool snapshotExists;
        public bool snapshotStale;
        public string indexGeneratedText;
        public int registrySchemaVersion;
        public string registryFingerprintShort;

        public int usedKeywordTotal;
        public string perShaderKeywordText;
        public int animationClipCount;
        public int runtimeDynamicCount;
        public int alwaysKeepKeywordCount;
        public int alwaysKeepMaterialCount;
        public int alwaysKeepShaderCount;

        public bool hlslGuardStateExists;
        public ReportSummaryDto latestReport;
        public readonly List<string> AggressiveGateReasons = new List<string>();

        public NataneWorkspaceStatusInput StatusInput
        {
            get
            {
                bool fallback = latestReport != null &&
                                latestReport.fallbackReasons != null &&
                                latestReport.fallbackReasons.Count > 0;
                return new NataneWorkspaceStatusInput
                {
                    mode = mode,
                    variantStrippingEnabled = variantStrippingEnabled,
                    auditStale = auditStale,
                    unknownKeywordCount = unknownKeywordCount,
                    hasFallbackReason = fallback
                };
            }
        }

        public static WorkspaceSnapshotData Collect()
        {
            var d = new WorkspaceSnapshotData();
            var settings = NataneBuildPolicySettings.instance;
            d.mode = settings.OptimizationMode;
            d.variantStrippingEnabled = settings.VariantStrippingEnabled;
            d.registrySchemaVersion = NataneShaderFeatureRegistry.RegistrySchemaVersion;
            string fp = NataneShaderFeatureRegistry.ComputeRegistryFingerprint();
            d.registryFingerprintShort = string.IsNullOrEmpty(fp) ? "-" : fp.Substring(0, Math.Min(8, fp.Length));

            try { d.auditStale = NataneShaderUpdateAudit.IsStale(); } catch { d.auditStale = true; }

            if (NataneAssetIndexStore.TryLoadIndex(out var index) && index != null)
            {
                d.indexGeneratedText = new DateTime(index.generatedAtUtcTicks, DateTimeKind.Utc)
                    .ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                d.indexGeneratedText = L("（未生成）", "(none)");
            }

            d.alwaysKeepKeywordCount = settings.AlwaysKeepKeywords.Count;
            d.alwaysKeepMaterialCount = settings.AlwaysKeepMaterialGuids.Count;
            d.alwaysKeepShaderCount = settings.AlwaysKeepShaderGuids.Count;
            d.runtimeDynamicCount = settings.RuntimeDynamicKeywords.Count;

            if (NataneBuildUsageSnapshotStore.TryLoad(out var snap) && snap != null)
            {
                d.snapshotExists = true;
                try { d.snapshotStale = NataneBuildUsageSnapshotStore.IsSnapshotStale(snap); } catch { d.snapshotStale = true; }
                d.animationClipCount = snap.animationDrivenKeywords?.Count ?? 0;
                if (snap.auditSummary != null)
                {
                    d.unknownKeywordCount = snap.auditSummary.unknownKeywordCount;
                    d.unparseableCount = snap.auditSummary.unparseableItemCount;
                }
                var perShader = new List<string>();
                int total = 0;
                if (snap.shaderUsages != null)
                {
                    foreach (var su in snap.shaderUsages.OrderBy(x => x.shaderName, StringComparer.Ordinal))
                    {
                        int n = su.usedKeywords?.Count ?? 0;
                        total += n;
                        perShader.Add($"{ShortShader(su.shaderName)}={n}");
                    }
                }
                d.usedKeywordTotal = total;
                d.perShaderKeywordText = perShader.Count > 0 ? string.Join(", ", perShader) : "-";
            }
            else
            {
                d.perShaderKeywordText = "-";
            }

            d.hlslGuardStateExists = NataneBuildFeatureOptimizer.TryLoadGuardState(out _);
            d.latestReport = LoadLatestReport();

            // Aggressive の続行ガード理由（表示用）。Snapshot / 監査状態から推定。
            if (d.mode == NataneOptimizationMode.Aggressive)
            {
                bool schemaMismatch = false;
                bool hasUnknown = d.unknownKeywordCount > 0;
                bool hasUnparseable = d.unparseableCount > 0;
                if (d.snapshotExists && NataneBuildUsageSnapshotStore.TryLoad(out var s2) && s2 != null)
                {
                    schemaMismatch = s2.registrySchemaVersion != NataneShaderFeatureRegistry.RegistrySchemaVersion ||
                                     s2.registryFingerprint != fp;
                }
                var gate = NataneAggressiveGate.Evaluate(hasUnknown, schemaMismatch, d.auditStale, hasUnparseable);
                if (!gate.Proceed) d.AggressiveGateReasons.AddRange(gate.Reasons);
            }

            return d;
        }

        private static string ShortShader(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            int idx = name.LastIndexOf('/');
            return idx >= 0 && idx + 1 < name.Length ? name.Substring(idx + 1) : name;
        }

        private static ReportSummaryDto LoadLatestReport()
        {
            try
            {
                string dir = NataneAssetIndexStore.ReportDirectoryPath;
                if (!Directory.Exists(dir)) return null;
                string[] files = Directory.GetFiles(dir, "variant-stripping-*.json");
                if (files == null || files.Length == 0) return null;
                Array.Sort(files, StringComparer.Ordinal);
                return JsonUtility.FromJson<ReportSummaryDto>(File.ReadAllText(files[files.Length - 1]));
            }
            catch
            {
                return null;
            }
        }
    }

    // ストリップレポート JSON の読み取り用 DTO（書き出し側の一部フィールドのみ）。
    [Serializable]
    internal sealed class ReportSummaryDto
    {
        public string context;
        public string mode;
        public string generatedAt;
        public int totalSeen;
        public int totalKept;
        public int totalStripped;
        public int totalCandidates;
        public int aggressiveCandidateCount;
        public List<string> fallbackReasons = new List<string>();
        public ReportDeltaDto previousComparison;
    }

    [Serializable]
    internal sealed class ReportDeltaDto
    {
        public string previousReportFile;
        public int previousKept;
        public int previousStripped;
        public int previousCandidates;
        public int deltaKept;
        public int deltaStripped;
        public int deltaCandidates;
    }
}
