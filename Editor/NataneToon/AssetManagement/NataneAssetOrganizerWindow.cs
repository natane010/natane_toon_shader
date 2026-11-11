using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// アセット整理ウィンドウ。提案→確認→適用の UI。サービス層(NataneAssetOrganizerService)を呼ぶだけ。
    /// </summary>
    public sealed class NataneAssetOrganizerWindow : EditorWindow
    {
        private NataneAssetOrganizerRuleSet ruleSet;
        private List<NataneOrganizeCandidate> candidates = new List<NataneOrganizeCandidate>();
        private Vector2 scroll;
        private NataneOrganizeApplyResult lastResult;

        [MenuItem("Tools/Natane/アセット管理 Asset Management/アセット整理 Asset Organizer", false, 20)]
        public static void ShowWindow()
        {
            var window = GetWindow<NataneAssetOrganizerWindow>();
            window.titleContent = new GUIContent(L("アセット整理", "Asset Organizer"));
            window.minSize = new Vector2(560, 480);
            window.Show();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawRuleSetSection();
            EditorGUILayout.Space(6);
            DrawProposalSection();
            EditorGUILayout.Space(6);
            DrawHistorySection();
            EditorGUILayout.Space(6);
            DrawNotes();

            EditorGUILayout.EndScrollView();
        }

        private void DrawRuleSetSection()
        {
            EditorGUILayout.LabelField(L("振り分けルールセット", "Rule Set"), EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                ruleSet = (NataneAssetOrganizerRuleSet)EditorGUILayout.ObjectField(
                    ruleSet, typeof(NataneAssetOrganizerRuleSet), false);

                if (GUILayout.Button(L("既定ルールを生成", "Create Default"), GUILayout.Width(140)))
                {
                    CreateDefaultRuleSet();
                }
            }

            if (ruleSet == null)
            {
                EditorGUILayout.HelpBox(
                    L("ルールセットを割り当てるか、既定ルールを生成してください。",
                      "Assign a rule set or create the default one."),
                    MessageType.Info);
                return;
            }

            EditorGUI.indentLevel++;
            List<NataneAssetOrganizerRule> rules = ruleSet.Rules;
            int removeIndex = -1;
            for (int i = 0; i < rules.Count; i++)
            {
                NataneAssetOrganizerRule rule = rules[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    rule.enabled = EditorGUILayout.Toggle(rule.enabled, GUILayout.Width(18));
                    rule.targetKind = (NataneAssetKind)EditorGUILayout.EnumPopup(rule.targetKind, GUILayout.Width(110));
                    rule.namePattern = EditorGUILayout.TextField(rule.namePattern, GUILayout.Width(140));
                    rule.destinationFolder = EditorGUILayout.TextField(rule.destinationFolder);
                    if (GUILayout.Button("-", GUILayout.Width(22)))
                    {
                        removeIndex = i;
                    }
                }
            }

            if (removeIndex >= 0)
            {
                rules.RemoveAt(removeIndex);
                ruleSet.Save();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("ルール追加", "Add Rule"), GUILayout.Width(110)))
                {
                    rules.Add(new NataneAssetOrganizerRule());
                    ruleSet.Save();
                }

                if (GUILayout.Button(L("保存", "Save"), GUILayout.Width(80)))
                {
                    ruleSet.Save();
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawProposalSection()
        {
            EditorGUILayout.LabelField(L("提案", "Proposal"), EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(ruleSet == null))
            {
                if (GUILayout.Button(L("提案を生成", "Generate Proposal"), GUILayout.Height(24)))
                {
                    candidates = NataneAssetOrganizerService.BuildProposal(ruleSet);
                    lastResult = null;
                }
            }

            if (candidates.Count == 0)
            {
                EditorGUILayout.LabelField(L("候補はありません。", "No candidates."));
                return;
            }

            int conflicts = candidates.Count(c => c.status == NataneOrganizeCandidateStatus.Conflict);
            int ready = candidates.Count(c => c.IsApplicable);
            EditorGUILayout.LabelField(
                L($"候補 {candidates.Count} 件 / 適用可能 {ready} 件 / 衝突 {conflicts} 件",
                  $"{candidates.Count} candidates / {ready} applicable / {conflicts} conflicts"));

            foreach (NataneOrganizeCandidate candidate in candidates)
            {
                DrawCandidate(candidate);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(ready == 0))
            {
                if (GUILayout.Button(L($"選択した {ready} 件を適用", $"Apply {ready} selected"), GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog(
                        L("アセット整理の適用", "Apply Asset Organize"),
                        L("選択した候補を MoveAsset で移動します。Unity 標準 Undo は効きません（ロールバック機能で戻せます）。続行しますか？",
                          "Selected candidates will be moved via MoveAsset. Unity's standard Undo does not apply (use Rollback instead). Continue?"),
                        L("適用", "Apply"), L("キャンセル", "Cancel")))
                    {
                        lastResult = NataneAssetOrganizerService.Apply(candidates);
                        candidates = NataneAssetOrganizerService.BuildProposal(ruleSet);
                    }
                }
            }

            DrawResult(lastResult);
        }

        private void DrawCandidate(NataneOrganizeCandidate candidate)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(candidate.status == NataneOrganizeCandidateStatus.Conflict))
                    {
                        candidate.included = EditorGUILayout.Toggle(candidate.included, GUILayout.Width(18));
                    }

                    EditorGUILayout.LabelField($"[{candidate.kind}] {Path.GetFileName(candidate.fromPath)}", EditorStyles.boldLabel);

                    if (GUILayout.Button(L("固定", "Pin"), GUILayout.Width(60)))
                    {
                        ruleSet.Pin(candidate.guid);
                        candidates = NataneAssetOrganizerService.BuildProposal(ruleSet);
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.LabelField(L("移動元", "From"), candidate.fromPath);
                EditorGUILayout.LabelField(L("移動先", "To"), candidate.toPath);
                EditorGUILayout.LabelField(
                    L("依存元", "Dependents"),
                    candidate.dependentCount == 0
                        ? L("なし", "none")
                        : $"{candidate.dependentCount} : {string.Join(", ", candidate.dependentPaths.Select(Path.GetFileName))}");

                if (candidate.status == NataneOrganizeCandidateStatus.Conflict)
                {
                    EditorGUILayout.HelpBox(candidate.conflictReason, MessageType.Warning);
                }
            }
        }

        private void DrawResult(NataneOrganizeApplyResult result)
        {
            if (result == null)
            {
                return;
            }

            var lines = new List<string>
            {
                L($"移動 {result.movedCount} 件 / スキップ {result.skippedCount} 件", $"Moved {result.movedCount} / skipped {result.skippedCount}")
            };
            if (result.guidMismatches.Count > 0)
            {
                lines.Add(L($"GUID 不一致 {result.guidMismatches.Count} 件", $"GUID mismatch {result.guidMismatches.Count}"));
            }
            if (result.moveErrors.Count > 0)
            {
                lines.Add(L($"移動エラー {result.moveErrors.Count} 件", $"Move errors {result.moveErrors.Count}"));
            }
            if (result.missingReferenceIncrease > 0)
            {
                lines.Add(L($"Missing 参照増加 {result.missingReferenceIncrease}", $"Missing refs +{result.missingReferenceIncrease}"));
            }

            EditorGUILayout.HelpBox(string.Join("\n", lines),
                result.Success ? MessageType.Info : MessageType.Error);
        }

        private void DrawHistorySection()
        {
            EditorGUILayout.LabelField(L("履歴 / ロールバック", "History / Rollback"), EditorStyles.boldLabel);

            NataneMoveHistory history = NataneAssetOrganizerService.LoadHistory();
            int active = history.batches.Count(b => !b.rolledBack && b.records.Count > 0);
            EditorGUILayout.LabelField(
                L($"記録バッチ {history.batches.Count} 件 / 復元可能 {active} 件",
                  $"{history.batches.Count} batches / {active} rollbackable"));

            using (new EditorGUI.DisabledScope(active == 0))
            {
                if (GUILayout.Button(L("直前のバッチをロールバック", "Rollback Last Batch")))
                {
                    NataneOrganizeApplyResult result = NataneAssetOrganizerService.RollbackLastBatch();
                    lastResult = result;
                    if (ruleSet != null)
                    {
                        candidates = NataneAssetOrganizerService.BuildProposal(ruleSet);
                    }
                }
            }
        }

        private void DrawNotes()
        {
            EditorGUILayout.HelpBox(
                L("注意: Resources.Load 等の文字列パス参照は自動追従しません。該当アセットの移動後は手動確認が必要です。",
                  "Note: string-based path references (e.g. Resources.Load) are not auto-updated. Verify manually after moving such assets."),
                MessageType.Warning);
            EditorGUILayout.HelpBox(
                L("Packages/・Assets 外・生成物(Library 等)は対象外です。Unity 標準 Undo は MoveAsset に効かないため、取り消しはロールバック機能を使用してください。",
                  "Packages/, non-Assets, and generated files are excluded. Unity's standard Undo does not affect MoveAsset; use Rollback to revert."),
                MessageType.Info);
        }

        private void CreateDefaultRuleSet()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                L("ルールセットの保存", "Save Rule Set"),
                "NataneAssetOrganizerRuleSet", "asset",
                L("ルールセットの保存先を選択", "Choose where to save the rule set"),
                "Assets");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = CreateInstance<NataneAssetOrganizerRuleSet>();
            asset.PopulateDefaults();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            ruleSet = asset;
        }
    }
}
