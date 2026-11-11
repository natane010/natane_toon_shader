using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// アセット整理のコアサービス（UI 非依存・検証可能）。
    /// 提案→確認→適用方式。移動は AssetDatabase.MoveAsset のみ（File.Move / .meta 再生成禁止）。
    /// </summary>
    public static class NataneAssetOrganizerService
    {
        // 代表として保持する依存元パスの上限（プレビュー表示用）。
        private const int DependentSampleLimit = 5;

        private const string HistoryFileName = "move-history-v1.json";

        private static string ProjectRootPath => Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");

        internal static string HistoryPath =>
            Path.Combine(ProjectRootPath, "Library", "NataneToon", "AssetOrganizer", HistoryFileName);

        // 画像系拡張子。Texture 種別の判定に使用。
        private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr", ".bmp", ".gif", ".hdr"
        };

        // ============================================================
        //  純関数群（テスト対象）
        // ============================================================

        /// <summary>
        /// ワイルドカード照合（* と ?）。大文字小文字を無視。空/"*" は全一致。
        /// </summary>
        public static bool MatchesWildcard(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*")
            {
                return true;
            }

            if (input == null)
            {
                return false;
            }

            string regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 拡張子からアセット種別を分類する（決定的・アセットロード不要）。
        /// </summary>
        public static NataneAssetKind ClassifyAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return NataneAssetKind.Other;
            }

            string ext = Path.GetExtension(assetPath);
            if (string.Equals(ext, ".mat", StringComparison.OrdinalIgnoreCase)) return NataneAssetKind.Material;
            if (string.Equals(ext, ".prefab", StringComparison.OrdinalIgnoreCase)) return NataneAssetKind.Prefab;
            if (string.Equals(ext, ".anim", StringComparison.OrdinalIgnoreCase)) return NataneAssetKind.AnimationClip;
            if (TextureExtensions.Contains(ext)) return NataneAssetKind.Texture;
            return NataneAssetKind.Other;
        }

        /// <summary>
        /// 整理対象になり得るパスか。Assets 配下のファイルのみ。
        /// Packages/・Assets 外・フォルダ自体・生成物は対象外（安全側で除外）。
        /// </summary>
        public static bool IsPathOrganizable(string assetPath)
        {
            assetPath = Normalize(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            // Assets/ 配下限定。Packages/ や外部パスは除外。
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            // フォルダそのものは移動対象にしない。
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return false;
            }

            // .meta 単体は対象外。
            if (assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        // ============================================================
        //  提案生成
        // ============================================================

        /// <summary>
        /// ルールセットから移動候補を生成する（明示操作。毎フレーム呼ばないこと）。
        /// </summary>
        public static List<NataneOrganizeCandidate> BuildProposal(NataneAssetOrganizerRuleSet ruleSet)
        {
            var candidates = new List<NataneOrganizeCandidate>();
            if (ruleSet == null || ruleSet.Rules == null || ruleSet.Rules.Count == 0)
            {
                return candidates;
            }

            var enabledRules = ruleSet.Rules.Where(r => r != null && r.enabled).ToList();
            if (enabledRules.Count == 0)
            {
                return candidates;
            }

            // Assets 配下の全アセット（Packages は含まれない）。
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
            var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
            var pending = new List<NataneOrganizeCandidate>();

            foreach (string guid in guids)
            {
                string path = Normalize(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(path) || !uniquePaths.Add(path))
                {
                    continue;
                }

                if (!IsPathOrganizable(path))
                {
                    continue;
                }

                // pin されたアセットは提案から除外。
                if (ruleSet.IsPinned(guid))
                {
                    continue;
                }

                NataneAssetKind kind = ClassifyAsset(path);
                string fileName = Path.GetFileName(path);

                // 先頭一致のルールを採用。
                NataneAssetOrganizerRule matched = enabledRules.FirstOrDefault(r =>
                    r.targetKind == kind && MatchesWildcard(fileName, r.namePattern));
                if (matched == null)
                {
                    continue;
                }

                string destFolder = Normalize(matched.destinationFolder);
                if (string.IsNullOrEmpty(destFolder))
                {
                    continue;
                }

                string toPath = destFolder + "/" + fileName;
                // 既に目的地にあるものは提案不要。
                if (string.Equals(toPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                pending.Add(new NataneOrganizeCandidate
                {
                    guid = guid,
                    fromPath = path,
                    toPath = toPath,
                    kind = kind
                });
            }

            // 依存元（逆引き）をまとめて解決してから衝突判定を行う。
            var targetPaths = new HashSet<string>(pending.Select(c => c.fromPath), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> reverse = BuildReverseDependencies(targetPaths);

            foreach (NataneOrganizeCandidate candidate in pending)
            {
                if (reverse.TryGetValue(candidate.fromPath, out List<string> referencers))
                {
                    candidate.dependentCount = referencers.Count;
                    candidate.dependentPaths = referencers.Take(DependentSampleLimit).ToList();
                }

                // 衝突: 移動先に別 GUID の同名アセットが既に存在。
                string existingGuid = AssetDatabase.AssetPathToGUID(candidate.toPath);
                if (!string.IsNullOrEmpty(existingGuid) &&
                    !string.Equals(existingGuid, candidate.guid, StringComparison.OrdinalIgnoreCase))
                {
                    candidate.status = NataneOrganizeCandidateStatus.Conflict;
                    candidate.conflictReason = "移動先に同名アセットが存在します";
                }

                candidates.Add(candidate);
            }

            return candidates
                .OrderBy(c => c.fromPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 対象パス群に対する依存元（逆引き）マップを 1 回の走査で構築する。
        /// 参照元になり得る型のみ走査してコストを抑える。
        /// </summary>
        private static Dictionary<string, List<string>> BuildReverseDependencies(HashSet<string> targetPaths)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (targetPaths == null || targetPaths.Count == 0)
            {
                return map;
            }

            // 参照を持ち得る代表的な型。Texture/AnimationClip 等の末端は参照元にならない。
            string[] referencerGuids = AssetDatabase.FindAssets(
                "t:Material t:Prefab t:Scene t:AnimatorController t:AnimationClip",
                new[] { "Assets" });

            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in referencerGuids)
            {
                string referencerPath = Normalize(AssetDatabase.GUIDToAssetPath(guid));
                if (string.IsNullOrEmpty(referencerPath) || !visited.Add(referencerPath))
                {
                    continue;
                }

                string[] deps = AssetDatabase.GetDependencies(referencerPath, false);
                for (int i = 0; i < deps.Length; i++)
                {
                    string dep = Normalize(deps[i]);
                    if (string.Equals(dep, referencerPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (targetPaths.Contains(dep))
                    {
                        if (!map.TryGetValue(dep, out List<string> list))
                        {
                            list = new List<string>();
                            map[dep] = list;
                        }

                        list.Add(referencerPath);
                    }
                }
            }

            return map;
        }

        // ============================================================
        //  適用
        // ============================================================

        /// <summary>
        /// 候補を適用する。GUID 記録→StartAssetEditing(try/finally)→MoveAsset→GUID 一致検証→
        /// 依存元の Missing 参照が増えていないか検証→履歴保存→AssetIndex dirty 投入。
        /// </summary>
        public static NataneOrganizeApplyResult Apply(IList<NataneOrganizeCandidate> candidates)
        {
            var result = new NataneOrganizeApplyResult();
            if (candidates == null || candidates.Count == 0)
            {
                return result;
            }

            List<NataneOrganizeCandidate> applicable = candidates.Where(c => c != null && c.IsApplicable).ToList();
            result.skippedCount = candidates.Count - applicable.Count;
            if (applicable.Count == 0)
            {
                return result;
            }

            // 移動前の状態を記録（GUID と、依存元の依存 GUID 集合ベースライン）。
            var movedFromPaths = applicable.Select(c => c.fromPath).ToList();
            HashSet<string> dependentGuids = CollectDependentGuids(applicable);
            Dictionary<string, HashSet<string>> baseline = CaptureDependencyGuids(dependentGuids);

            var batch = new NataneMoveBatch
            {
                batchId = Guid.NewGuid().ToString("N"),
                utcTicks = DateTime.UtcNow.Ticks
            };

            // 移動先フォルダを事前生成（MoveAsset は親フォルダ存在が前提）。編集ブロック外で実施。
            foreach (NataneOrganizeCandidate candidate in applicable)
            {
                EnsureFolderExists(GetParentFolder(candidate.toPath));
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (NataneOrganizeCandidate candidate in applicable)
                {
                    string error = AssetDatabase.MoveAsset(candidate.fromPath, candidate.toPath);
                    if (!string.IsNullOrEmpty(error))
                    {
                        result.moveErrors.Add($"{candidate.fromPath} -> {candidate.toPath}: {error}");
                        continue;
                    }

                    batch.records.Add(new NataneMoveRecord
                    {
                        guid = candidate.guid,
                        fromPath = candidate.fromPath,
                        toPath = candidate.toPath
                    });
                    result.movedCount++;
                }
            }
            finally
            {
                // StartAssetEditing は必ず閉じる（例外時も含む）。
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            // 移動後検証: GUID 一致。
            foreach (NataneMoveRecord record in batch.records)
            {
                string guidAtNewPath = AssetDatabase.AssetPathToGUID(record.toPath);
                if (!string.Equals(guidAtNewPath, record.guid, StringComparison.OrdinalIgnoreCase))
                {
                    result.guidMismatches.Add($"{record.toPath}: expected {record.guid}, got {guidAtNewPath}");
                }
            }

            // 依存元の Missing 増分検証（適用前後の依存 GUID 集合を比較）。
            Dictionary<string, HashSet<string>> after = CaptureDependencyGuids(dependentGuids);
            result.missingReferenceIncrease = CountMissingIncrease(baseline, after);
            if (result.missingReferenceIncrease > 0)
            {
                result.warnings.Add("依存元で解決できなくなった参照が検出されました");
            }

            if (batch.records.Count > 0)
            {
                result.batchId = batch.batchId;
                AppendBatch(batch);

                // AssetIndex の dirty キューへ移動元/移動先を投入（Snapshot/Audit の stale は既存機構に委譲）。
                NataneAssetIndexService.MarkAssetsDirty(
                    batch.records.Select(r => r.toPath).ToArray(),
                    Array.Empty<string>(),
                    batch.records.Select(r => r.toPath).ToArray(),
                    movedFromPaths.ToArray());
            }

            return result;
        }

        // ============================================================
        //  ロールバック
        // ============================================================

        public static NataneOrganizeApplyResult RollbackLastBatch()
        {
            NataneMoveHistory history = LoadHistory();
            NataneMoveBatch last = history.batches.LastOrDefault(b => !b.rolledBack && b.records.Count > 0);
            if (last == null)
            {
                var empty = new NataneOrganizeApplyResult();
                empty.warnings.Add("ロールバック可能なバッチがありません");
                return empty;
            }

            return RollbackBatch(last.batchId);
        }

        /// <summary>
        /// 指定バッチを逆方向 MoveAsset で戻す（こちらも GUID 検証付き）。
        /// </summary>
        public static NataneOrganizeApplyResult RollbackBatch(string batchId)
        {
            var result = new NataneOrganizeApplyResult { batchId = batchId };
            NataneMoveHistory history = LoadHistory();
            NataneMoveBatch batch = history.batches.FirstOrDefault(b => b.batchId == batchId);
            if (batch == null || batch.rolledBack)
            {
                result.warnings.Add("対象バッチが見つからないか、既にロールバック済みです");
                return result;
            }

            foreach (NataneMoveRecord record in batch.records)
            {
                EnsureFolderExists(GetParentFolder(record.fromPath));
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (NataneMoveRecord record in batch.records)
                {
                    // 逆方向: toPath -> fromPath。
                    string error = AssetDatabase.MoveAsset(record.toPath, record.fromPath);
                    if (!string.IsNullOrEmpty(error))
                    {
                        result.moveErrors.Add($"{record.toPath} -> {record.fromPath}: {error}");
                        continue;
                    }

                    result.movedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            foreach (NataneMoveRecord record in batch.records)
            {
                string guidAtOriginal = AssetDatabase.AssetPathToGUID(record.fromPath);
                if (!string.IsNullOrEmpty(guidAtOriginal) &&
                    !string.Equals(guidAtOriginal, record.guid, StringComparison.OrdinalIgnoreCase))
                {
                    result.guidMismatches.Add($"{record.fromPath}: expected {record.guid}, got {guidAtOriginal}");
                }
            }

            if (result.moveErrors.Count == 0)
            {
                batch.rolledBack = true;
                SaveHistory(history);

                NataneAssetIndexService.MarkAssetsDirty(
                    batch.records.Select(r => r.fromPath).ToArray(),
                    Array.Empty<string>(),
                    batch.records.Select(r => r.fromPath).ToArray(),
                    batch.records.Select(r => r.toPath).ToArray());
            }

            return result;
        }

        // ============================================================
        //  依存 GUID 集合の捕捉と比較（Missing 検証）
        // ============================================================

        private static HashSet<string> CollectDependentGuids(IEnumerable<NataneOrganizeCandidate> candidates)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (NataneOrganizeCandidate candidate in candidates)
            {
                if (candidate.dependentPaths == null)
                {
                    continue;
                }

                foreach (string dependentPath in candidate.dependentPaths)
                {
                    string guid = AssetDatabase.AssetPathToGUID(dependentPath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        set.Add(guid);
                    }
                }
            }

            return set;
        }

        // 依存元 GUID ごとに、その解決可能な依存 GUID 集合を捕捉する。
        private static Dictionary<string, HashSet<string>> CaptureDependencyGuids(HashSet<string> dependentGuids)
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string dependentGuid in dependentGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(dependentGuid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var depGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                {
                    string g = AssetDatabase.AssetPathToGUID(dep);
                    if (!string.IsNullOrEmpty(g))
                    {
                        depGuids.Add(g);
                    }
                }

                map[dependentGuid] = depGuids;
            }

            return map;
        }

        // 適用前に解決できていた依存 GUID が適用後に解決できなくなった数を数える。
        private static int CountMissingIncrease(
            Dictionary<string, HashSet<string>> before,
            Dictionary<string, HashSet<string>> after)
        {
            int missing = 0;
            foreach (KeyValuePair<string, HashSet<string>> pair in before)
            {
                if (!after.TryGetValue(pair.Key, out HashSet<string> afterSet))
                {
                    continue;
                }

                foreach (string guid in pair.Value)
                {
                    if (!afterSet.Contains(guid))
                    {
                        missing++;
                    }
                }
            }

            return missing;
        }

        // ============================================================
        //  履歴 I/O
        // ============================================================

        public static NataneMoveHistory LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryPath))
                {
                    var loaded = JsonUtility.FromJson<NataneMoveHistory>(File.ReadAllText(HistoryPath));
                    // schema 違い/破損は安全側で新規扱い（削除せず上書き前まで保持）。
                    if (loaded != null && loaded.schemaVersion == 1 && loaded.batches != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Asset Organizer] 履歴の読み込みに失敗: {ex.Message}");
            }

            return new NataneMoveHistory();
        }

        private static void AppendBatch(NataneMoveBatch batch)
        {
            NataneMoveHistory history = LoadHistory();
            history.batches.Add(batch);
            SaveHistory(history);
        }

        private static void SaveHistory(NataneMoveHistory history)
        {
            try
            {
                string dir = Path.GetDirectoryName(HistoryPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(HistoryPath, JsonUtility.ToJson(history, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Asset Organizer] 履歴の保存に失敗: {ex.Message}");
            }
        }

        // ============================================================
        //  ヘルパ
        // ============================================================

        private static string GetParentFolder(string assetPath)
        {
            assetPath = Normalize(assetPath);
            int slash = assetPath.LastIndexOf('/');
            return slash <= 0 ? "Assets" : assetPath.Substring(0, slash);
        }

        // "Assets/A/B/C" を順に AssetDatabase.CreateFolder で用意する。
        private static void EnsureFolderExists(string folder)
        {
            folder = Normalize(folder);
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                return;
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace("\\", "/");
        }
    }
}
