using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// 現行 HLSL FeatureOptimizer 方式（#undef ガード）と、統合ストリッパー Safe 方式の
    /// 「削除するキーワード集合」を静的に突合する比較レポート（ビルド不要）。
    ///
    /// この比較は Stage E（通常ビルドでの HLSL 書換え無効化可否）の判断材料となる:
    ///   - 「完全同等」なら HLSL 書換え/全再インポートを安全に外せる可能性が高い。
    ///   - 「差分あり」なら差分キーワードの原因（走査経路の違い等）を解消してから判断する。
    ///
    /// HLSL 方式: NataneBuildFeatureOptimizer.CollectUsedFeatures() を再利用し、
    ///            ガード対象キーワード母集合 - 使用機能 = #undef され得るキーワード。
    /// Safe 方式: Snapshot の使用/保持集合から、SafeStrippable かつ未使用の既知キーワード = 削除対象。
    /// </summary>
    public static class NataneStrippingComparisonReport
    {
        [Serializable]
        private sealed class ComparisonRoot
        {
            public string note;
            public string generatedAt;
            public string equivalence;      // "完全同等" or "差分あり"
            public string snapshotFingerprint;
            public List<string> hlslUndefSet = new List<string>();
            public List<string> safeStripSet = new List<string>();
            public List<string> bothStrip = new List<string>();
            public List<string> hlslOnly = new List<string>();
            public List<string> safeOnly = new List<string>();
            public List<string> neitherStrip = new List<string>();
        }

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/HLSL方式との比較レポート Compare vs HLSL Method", false, 63)]
        public static void RunFromMenu()
        {
            string path = Run(out string summary);
            Debug.Log(summary);
            if (!string.IsNullOrEmpty(path))
                Debug.Log($"[Natane 比較レポート] JSON を書き出しました: {path}");
        }

        // -executeMethod 用（Exit は -quit に委ねる）。
        public static void RunFromBatch()
        {
            string path = Run(out string summary);
            Debug.Log(summary);
            Debug.Log($"COMPARISON_JSON_PATH={path}");
        }

        /// <summary>比較を実行し JSON を保存する。要約文字列を out で返し、JSON パスを戻り値で返す。</summary>
        public static string Run(out string summary)
        {
            // 鮮度良好な Snapshot を再利用、無ければ生成（メニューと同じ経路）。
            NataneBuildUsageSnapshot snapshot = NataneBuildUsageSnapshotMenu.Generate(forceRebuild: false);

            // (a) HLSL 方式が #undef し得るキーワード = ガード母集合 - 使用機能。
            var guard = new HashSet<string>(NataneBuildFeatureOptimizer.GetGuardKeywords(), StringComparer.Ordinal);
            var hlslUsed = NataneBuildFeatureOptimizer.CollectUsedFeatures();
            var hlslUndef = new HashSet<string>(guard.Where(k => !hlslUsed.Contains(k)), StringComparer.Ordinal);

            // (b) Safe が削除するキーワード（キーワード粒度の等価集合）。
            //     SafeStrippable かつ !AlwaysKeep の既知キーワードのうち、全シェーダーの保持集合和に含まれないもの。
            var safeStrip = ComputeSafeStrippedKeywords(snapshot, out HashSet<string> globalKeep);

            var both = hlslUndef.Intersect(safeStrip).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var hlslOnly = hlslUndef.Except(safeStrip).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var safeOnly = safeStrip.Except(hlslUndef).OrderBy(k => k, StringComparer.Ordinal).ToList();

            // どちらも削らない既知キーワード（参考）。
            var allKnown = NataneShaderFeatureRegistry.AllDefinitions.Select(d => d.Keyword);
            var neither = allKnown
                .Where(k => !hlslUndef.Contains(k) && !safeStrip.Contains(k))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal).ToList();

            bool equivalent = hlslOnly.Count == 0 && safeOnly.Count == 0;

            var root = new ComparisonRoot
            {
                note = "HLSL FeatureOptimizer(#undef) と 統合ストリッパー Safe の削除キーワード比較。" +
                       "Stage E（通常ビルドでの HLSL 書換え無効化可否）の判断材料。",
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                equivalence = equivalent ? "完全同等" : "差分あり",
                snapshotFingerprint = snapshot?.snapshotFingerprint,
                hlslUndefSet = hlslUndef.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                safeStripSet = safeStrip.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                bothStrip = both,
                hlslOnly = hlslOnly,
                safeOnly = safeOnly,
                neitherStrip = neither
            };

            string jsonPath = SaveJson(root);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Natane 比較レポート] HLSL 方式 vs 統合ストリッパー Safe");
            sb.AppendLine("  ※ Stage E（HLSL 書換え無効化可否）の判断材料です。");
            sb.AppendLine($"  同等性: {root.equivalence}");
            sb.AppendLine($"  HLSL が #undef: {hlslUndef.Count} 件 / Safe が削除: {safeStrip.Count} 件");
            sb.AppendLine($"  両方が削除: {both.Count} / HLSL のみ: {hlslOnly.Count} / Safe のみ: {safeOnly.Count}");
            if (hlslOnly.Count > 0)
                sb.AppendLine("  HLSL のみが削るキーワード: " + string.Join(", ", hlslOnly));
            if (safeOnly.Count > 0)
                sb.AppendLine("  Safe のみが削るキーワード: " + string.Join(", ", safeOnly));
            if (equivalent)
                sb.AppendLine("  → 削除集合は完全同等。HLSL 書換えの無効化検討に前進可能。");
            else
                sb.AppendLine("  → 差分あり。差分キーワードの原因（走査経路差/派生判定）を確認のこと。");
            summary = sb.ToString();
            return jsonPath;
        }

        /// <summary>
        /// Safe がキーワード粒度で削除する集合を Snapshot から計算する。
        /// globalKeep = 全シェーダーの使用キーワード和 ∪ Animation 由来 ∪ Runtime 動的 ∪ 常時保持。
        /// 削除対象 = SafeStrippable かつ !AlwaysKeep の既知キーワードで globalKeep に含まれないもの。
        /// </summary>
        public static HashSet<string> ComputeSafeStrippedKeywords(NataneBuildUsageSnapshot snapshot, out HashSet<string> globalKeep)
        {
            globalKeep = new HashSet<string>(StringComparer.Ordinal);
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot == null) return result;

            foreach (var su in snapshot.shaderUsages)
                if (su?.usedKeywords != null)
                    foreach (var k in su.usedKeywords) globalKeep.Add(k);
            foreach (var a in snapshot.animationDrivenKeywords)
                if (a?.keywords != null)
                    foreach (var k in a.keywords) globalKeep.Add(k);
            if (snapshot.runtimeDynamicKeywords != null)
                foreach (var k in snapshot.runtimeDynamicKeywords) globalKeep.Add(k);
            if (snapshot.alwaysKeepKeywords != null)
                foreach (var k in snapshot.alwaysKeepKeywords) globalKeep.Add(k);

            foreach (var def in NataneShaderFeatureRegistry.AllDefinitions)
            {
                if (def == null || string.IsNullOrEmpty(def.Keyword)) continue;
                if (!def.SafeStrippable || def.AlwaysKeep) continue;
                if (!globalKeep.Contains(def.Keyword))
                    result.Add(def.Keyword);
            }
            return result;
        }

        private static string SaveJson(ComparisonRoot root)
        {
            try
            {
                string dir = NataneStrippingReportPaths.ReportsDir;
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, $"hlsl-comparison-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                System.IO.File.WriteAllText(path, JsonUtility.ToJson(root, true));
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane 比較レポート] JSON 保存に失敗: {ex.Message}");
                return null;
            }
        }
    }
}
