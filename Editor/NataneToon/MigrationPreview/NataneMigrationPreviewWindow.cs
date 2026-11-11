using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Migration プレビュー用ウィンドウ。収集→プレビュー（前後値並記・個別除外・危険強調）→
    /// 確認ダイアログ付き適用→ロールバック→再検査サマリ表示を担う。
    /// 書換えは Material/Clip のみ。全て明示操作で、Shader 更新検出による自動保存はしない。
    /// </summary>
    public sealed class NataneMigrationPreviewWindow : EditorWindow
    {
        private NataneMigrationPlan plan;
        private NataneMigrationApplyResult lastResult;
        private Vector2 scroll;

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/Migration プレビュー Migration Preview", false, 61)]
        public static void Open()
        {
            var window = GetWindow<NataneMigrationPreviewWindow>();
            window.titleContent = new GUIContent("Migration プレビュー");
            window.minSize = new Vector2(560, 360);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Migration プレビュー", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Registry の移行定義に基づき Material / AnimationClip の書換え候補を提示します。" +
                "Prefab/Scene は影響表示のみ（書換えません）。適用は確認ダイアログ後にのみ実行されます。",
                MessageType.Info);

            DrawStatus();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("対象を収集してプレビュー生成", GUILayout.Height(24)))
                {
                    RebuildPlan();
                }

                using (new EditorGUI.DisabledScope(!HasRollback()))
                {
                    if (GUILayout.Button("直前バッチをロールバック", GUILayout.Height(24)))
                    {
                        RollbackLast();
                    }
                }
            }

            if (plan == null)
            {
                return;
            }

            DrawPlan();
        }

        private void DrawStatus()
        {
            NataneUpdateStatus status = NataneMigrationService.GetUpdateStatus();
            EditorGUILayout.HelpBox(
                $"Package: {status.currentPackageVersion}  /  移行定義: {status.migrationDefinitionCount} 件  /  " +
                $"監査: {(status.hasAudit ? "あり" : "なし")}  /  再監査要否: {(status.auditStale ? "要" : "不要")}\n" +
                $"未知Keyword: {status.unknownKeywordCount}  削除済Property候補: {status.missingPropertyCount}  " +
                $"Registry互換: {(status.registrySchemaMatches ? "一致" : "不一致")}",
                status.auditStale || !status.registrySchemaMatches ? MessageType.Warning : MessageType.None);
        }

        private void DrawPlan()
        {
            if (plan.warnings.Count > 0)
            {
                EditorGUILayout.HelpBox("警告:\n" + string.Join("\n", plan.warnings), MessageType.Warning);
            }

            EditorGUILayout.LabelField(
                $"変更候補: {plan.changes.Count} 件（自動適用可 {plan.AutoApplicableCount} / 要確認 {plan.ManualReviewCount}）",
                EditorStyles.boldLabel);

            if (plan.affectedPrefabPaths.Count > 0)
            {
                EditorGUILayout.LabelField($"影響 Prefab（表示のみ）: {plan.affectedPrefabPaths.Count} 件", EditorStyles.miniLabel);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (NataneMigrationChange change in plan.changes)
            {
                DrawChangeRow(change);
            }
            EditorGUILayout.EndScrollView();

            int included = plan.changes.Count(c => c.included);
            using (new EditorGUI.DisabledScope(included == 0))
            {
                if (GUILayout.Button($"適用（{included} 件）", GUILayout.Height(28)))
                {
                    ConfirmAndApply();
                }
            }

            DrawResult();
        }

        private void DrawChangeRow(NataneMigrationChange change)
        {
            Color prev = GUI.backgroundColor;
            if (change.manualReview)
            {
                GUI.backgroundColor = new Color(1f, 0.85f, 0.6f); // 危険項目の強調
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    change.included = EditorGUILayout.ToggleLeft(
                        $"[{KindLabel(change.kind)}] {change.assetName}", change.included, GUILayout.Width(320));
                    if (change.manualReview)
                    {
                        EditorGUILayout.LabelField("要確認（危険）", EditorStyles.boldLabel, GUILayout.Width(110));
                    }
                }

                EditorGUILayout.LabelField($"現在: {change.CurrentValueText}  →  適用後: {change.AppliedValueText}", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(change.note))
                {
                    EditorGUILayout.LabelField(change.note, EditorStyles.miniLabel);
                }
            }

            GUI.backgroundColor = prev;
        }

        private void DrawResult()
        {
            if (lastResult == null)
            {
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"適用 {lastResult.appliedCount} 件 / スキップ {lastResult.skippedCount} 件（Material {lastResult.materialCount} / Clip {lastResult.clipCount}）");
            sb.AppendLine($"再検査: Missing 参照 {lastResult.missingReferenceCount} / 旧 Keyword 残留 {lastResult.residualOldKeywordCount}");
            if (lastResult.errors.Count > 0) sb.AppendLine("エラー: " + string.Join(" | ", lastResult.errors));
            if (lastResult.warnings.Count > 0) sb.AppendLine("警告: " + string.Join(" | ", lastResult.warnings));

            EditorGUILayout.HelpBox(sb.ToString(),
                lastResult.errors.Count > 0 ? MessageType.Error : MessageType.Info);
        }

        private void RebuildPlan()
        {
            IReadOnlyList<NataneMigrationDefinition> defs = NataneShaderFeatureRegistry.MigrationDefinitions;
            List<Material> materials = NataneMigrationService.CollectTargetMaterials(defs);
            List<AnimationClip> clips = NataneMigrationService.CollectTargetClips(defs);
            plan = NataneMigrationService.BuildMigrationPlan(defs, materials, clips);
            lastResult = null;
        }

        private void ConfirmAndApply()
        {
            List<NataneMigrationChange> included = plan.changes.Where(c => c.included).ToList();
            int materials = included.Where(c => c.kind != NataneMigrationChangeKind.ClipBinding)
                .Select(c => c.assetGuid).Distinct().Count();
            int clips = included.Where(c => c.kind == NataneMigrationChangeKind.ClipBinding)
                .Select(c => c.assetGuid).Distinct().Count();
            int review = included.Count(c => c.manualReview);

            string message =
                $"{included.Count} 件を適用します。\n" +
                $"Material: {materials} 件 / AnimationClip: {clips} 件\n" +
                (review > 0 ? $"うち要確認（危険）: {review} 件\n" : string.Empty) +
                "適用内容は履歴に記録され、ロールバック可能です。続行しますか？";

            if (!EditorUtility.DisplayDialog("Migration 適用の確認", message, "適用する", "キャンセル"))
            {
                return;
            }

            lastResult = NataneMigrationService.Apply(plan);
            RebuildPlan();
        }

        private void RollbackLast()
        {
            if (!EditorUtility.DisplayDialog("ロールバックの確認",
                "直前の Migration バッチを逆適用します。続行しますか？", "ロールバック", "キャンセル"))
            {
                return;
            }

            lastResult = NataneMigrationService.RollbackLastBatch();
            RebuildPlan();
        }

        private static bool HasRollback()
        {
            return NataneMigrationService.LoadHistory().batches.Any(b => !b.rolledBack && b.records.Count > 0);
        }

        private static string KindLabel(NataneMigrationChangeKind kind)
        {
            switch (kind)
            {
                case NataneMigrationChangeKind.MaterialProperty: return "Property";
                case NataneMigrationChangeKind.MaterialKeyword: return "Keyword";
                case NataneMigrationChangeKind.MaterialShader: return "Shader";
                case NataneMigrationChangeKind.ClipBinding: return "Clip";
                default: return "?";
            }
        }
    }
}
