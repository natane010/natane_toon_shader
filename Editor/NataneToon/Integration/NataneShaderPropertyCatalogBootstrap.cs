using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// 現行の 12 バリアントから Properties の正準カタログを起こし、
    /// 「そのカタログから 12 本すべてを無損失で再現できるか」を検証する。
    ///
    /// カタログは手書きしない。1,100 行規模の転記はミスの温床になるので、
    /// 現行ソースから機械的に抽出する。
    ///
    /// このツールは **.shader を一切書き換えない**。生成器を入れる前に、
    /// カタログ表現で現状を完全に説明できることを先に証明するのが目的。
    /// ここが通らないうちは生成に進んではいけない。
    /// </summary>
    internal static class NataneShaderPropertyCatalogBootstrap
    {
        private static string ProjectRootPath =>
            Path.GetDirectoryName(Application.dataPath)?.Replace("\\", "/") ?? string.Empty;

        private static string ReportPath =>
            Path.Combine(ProjectRootPath, "Library", "NataneToon", "Audit", "property-catalog-check.md");

        /// <summary>
        /// ディスクから読み直してカタログを組み立てる。整合チェックと生成の双方から使う。
        /// </summary>
        internal static bool LoadFromDisk(
            out List<NataneToonVariantLocator.Entry> entries,
            out List<NataneShaderPropertySet> sets,
            out NataneShaderPropertyCatalog catalog,
            out List<string> excluded)
        {
            entries = NataneToonVariantLocator.Load(out excluded);
            sets = new List<NataneShaderPropertySet>();
            catalog = null;

            if (entries.Count == 0) return false;

            foreach (NataneToonVariantLocator.Entry e in entries)
                sets.Add(BuildSet(e));

            catalog = BuildCatalog(sets);
            return true;
        }

        private static NataneShaderPropertySet BuildSet(NataneToonVariantLocator.Entry e)
        {
            List<NatanePropertyDeclaration> decls = NatanePropertyParser.ParseSource(
                e.Source, out List<string> unparsed, out List<string> trailing);

            int sourceComments = 0;
            if (NatanePropertyParser.TryLocatePropertiesBlock(e.Source, out int bs, out int bl))
            {
                // 生成物のコメント（センチネル・除外理由）はパーサーが装飾として
                // 取り込まないので、原文側の数からも除く。
                // 除かないと生成済みファイルで常に不一致になる。
                sourceComments = e.Source.Substring(bs, bl)
                    .Split('\n')
                    .Select(l => l.TrimStart())
                    .Count(l => l.StartsWith("//", StringComparison.Ordinal)
                                && !NatanePropertyParser.IsGeneratedDecoration(l));
            }

            return new NataneShaderPropertySet
            {
                FileName = e.FileName,
                AssetPath = e.AssetPath,
                Declarations = decls,
                Unparsed = unparsed,
                TrailingLines = trailing,
                SourceCommentLines = sourceComments
            };
        }

        // ---- 生成メニュー ----

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/Properties 生成 (ドライラン)", false, 63)]
        public static void GenerateDryRun() => RunGenerate(dryRun: true, onlyFileName: null);

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/Properties 生成 (Lite のみ適用)", false, 64)]
        public static void GenerateLiteOnly() =>
            RunGenerate(dryRun: false, onlyFileName: "NataneToonShader_Lite.shader");

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/Properties 生成 (全バリアント適用)", false, 65)]
        public static void GenerateAll() => RunGenerate(dryRun: false, onlyFileName: null);

        /// <summary>
        /// ダイアログを出さずに生成を実行する（自動検証ウォッチャー用）。
        /// 整合チェックに落ちた場合は何も書かずに false を返す。
        /// </summary>
        internal static bool ApplyGenerationHeadless(out int written, out int problems, out string message)
        {
            written = 0;
            problems = 0;
            message = string.Empty;

            if (!LoadFromDisk(out List<NataneToonVariantLocator.Entry> entries,
                              out List<NataneShaderPropertySet> sets,
                              out NataneShaderPropertyCatalog catalog,
                              out _))
            {
                message = "対象シェーダーが見つかりませんでした。";
                return false;
            }

            List<string> failures = Verify(catalog, sets);
            if (failures.Count > 0)
            {
                message = $"整合チェック不合格 {failures.Count} 件のため生成を中止しました。";
                return false;
            }

            List<string> merged = NataneShaderPropertyAdditions.Merge(catalog);

            var results = entries
                .Select(e => NataneShaderPropertyWriter.Apply(e, catalog, dryRun: false, merged))
                .ToList();

            written = results.Count(r => r.Written);
            problems = results.Count(r => r.Problems.Count > 0);
            message = $"追加合流 {merged.Count} / 書き込み {written} / 問題 {problems}";

            WriteGenerateReport(results, dryRun: false);
            if (written > 0) AssetDatabase.Refresh();

            return problems == 0;
        }

        private static void RunGenerate(bool dryRun, string onlyFileName)
        {
            if (!LoadFromDisk(out List<NataneToonVariantLocator.Entry> entries,
                              out List<NataneShaderPropertySet> sets,
                              out NataneShaderPropertyCatalog catalog,
                              out _))
            {
                EditorUtility.DisplayDialog("Properties 生成", "対象シェーダーが見つかりませんでした。", "OK");
                return;
            }

            // 整合チェックが通らない状態で生成するとプロパティが失われる。ここで必ず止める。
            // 追加分を合流させる前に見るのが重要で、後に見ると
            // 「まだどのシェーダーにも無い新規プロパティ」が余分として検出されてしまう。
            List<string> failures = Verify(catalog, sets);
            if (failures.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Properties 生成",
                    $"整合チェックが不合格です（{failures.Count} 件）。\n" +
                    "生成すると宣言が失われるため中止しました。\n\n" +
                    "先に「Properties カタログ整合チェック」を実行して内容を確認してください。",
                    "OK");
                return;
            }

            // 新規プロパティの定義表を合流させる。
            // 導出だけでは「1 箇所に足して全バリアントへ展開」ができないため。
            List<string> mergedNames = NataneShaderPropertyAdditions.Merge(catalog);
            if (mergedNames.Count > 0)
                Debug.Log($"[NataneToonShader] 追加定義から {mergedNames.Count} プロパティを合流させました。");

            var targets = entries.Where(e => onlyFileName == null || e.FileName == onlyFileName).ToList();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Properties 生成", $"対象 {onlyFileName} が見つかりませんでした。", "OK");
                return;
            }

            if (!dryRun)
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Properties 生成",
                    $"{targets.Count} 本の .shader の Properties ブロックを書き換えます。\n\n" +
                    "・Properties の中身以外は変更しません\n" +
                    "・生成物を再解析し、宣言が1つでも失われる場合は書き込みません\n\n" +
                    "Git で差分を確認できる状態ですか？",
                    "実行する", "やめる");
                if (!ok) return;
            }

            var results = new List<NataneShaderPropertyWriter.ApplyResult>();
            foreach (NataneToonVariantLocator.Entry e in targets)
                results.Add(NataneShaderPropertyWriter.Apply(e, catalog, dryRun, mergedNames));

            int written = results.Count(r => r.Written);
            int changed = results.Count(r => r.Changed);
            int problematic = results.Count(r => r.Problems.Count > 0);

            if (written > 0) AssetDatabase.Refresh();

            // Console だけだと後から診断を追えないので、レポートも残す。
            string reportPath = WriteGenerateReport(results, dryRun);

            var sb = new StringBuilder();
            sb.AppendLine($"[NataneToonShader] Properties 生成{(dryRun ? "（ドライラン）" : string.Empty)}");
            foreach (NataneShaderPropertyWriter.ApplyResult r in results)
            {
                string state = r.Problems.Count > 0 ? "NG" : (r.Written ? "書込" : (r.Changed ? "差分あり" : "差分なし"));
                sb.AppendLine($"  [{state}] {r.FileName}  {r.BeforeLines} -> {r.AfterLines} 行");
                foreach (string p in r.Problems.Take(5)) sb.AppendLine($"        {p}");
            }
            sb.AppendLine($"レポート: {reportPath}");
            if (problematic > 0) Debug.LogWarning(sb.ToString()); else Debug.Log(sb.ToString());

            EditorUtility.DisplayDialog(
                "Properties 生成",
                (dryRun ? "ドライラン結果\n\n" : "実行結果\n\n") +
                $"対象: {targets.Count} 本\n" +
                $"差分あり: {changed} 本\n" +
                (dryRun ? string.Empty : $"書き込み: {written} 本\n") +
                $"問題あり: {problematic} 本\n\n" +
                $"レポート:\n{reportPath}",
                "OK");
        }

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/Properties カタログ整合チェック", false, 62)]
        public static void RunFromMenu()
        {
            // 読み込みは LoadFromDisk に一本化している。
            // 生成メニュー側と別々に組み立てると、片方だけ直したときにズレる。
            if (!LoadFromDisk(out _,
                              out List<NataneShaderPropertySet> sets,
                              out NataneShaderPropertyCatalog catalog,
                              out List<string> excluded))
            {
                EditorUtility.DisplayDialog(
                    "Properties カタログ整合チェック",
                    "対象シェーダーが見つかりませんでした。",
                    "OK");
                return;
            }

            List<string> failures = Verify(catalog, sets);

            string report = BuildReport(catalog, sets, failures, excluded);
            string path = WriteReport(report);

            if (failures.Count == 0)
            {
                Debug.Log(
                    $"[NataneToonShader] Properties カタログ整合チェック: {sets.Count} 本すべて再現可能。\n" +
                    $"和集合 {catalog.Declarations.Count} プロパティ / グループ {catalog.Groups.Count} 種 / 上書き {catalog.Overrides.Count} 件\n" +
                    $"レポート: {path}");

                EditorUtility.DisplayDialog(
                    "Properties カタログ整合チェック",
                    $"合格。{sets.Count} 本すべてをカタログから無損失で再現できます。\n\n" +
                    $"和集合: {catalog.Declarations.Count} プロパティ\n" +
                    $"採否グループ: {catalog.Groups.Count} 種\n" +
                    $"宣言の上書き: {catalog.Overrides.Count} 件\n\n" +
                    $"レポート:\n{path}",
                    "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[NataneToonShader] Properties カタログ整合チェック: 不一致 {failures.Count} 件");
            foreach (string f in failures.Take(40)) sb.AppendLine("  " + f);
            if (failures.Count > 40) sb.AppendLine($"  … 他 {failures.Count - 40} 件");
            sb.AppendLine($"レポート: {path}");
            Debug.LogWarning(sb.ToString());

            EditorUtility.DisplayDialog(
                "Properties カタログ整合チェック",
                $"不一致が {failures.Count} 件あります。\n" +
                "この状態で生成に進むとプロパティが失われます。\n\n" +
                $"レポート:\n{path}",
                "OK");
        }

        // ---- カタログ構築 ----

        internal static NataneShaderPropertyCatalog BuildCatalog(List<NataneShaderPropertySet> sets)
        {
            var catalog = new NataneShaderPropertyCatalog();
            catalog.Shaders = sets.Select(s => s.FileName).ToList();

            var byName = new Dictionary<string, Dictionary<string, NatanePropertyDeclaration>>(StringComparer.Ordinal);
            foreach (NataneShaderPropertySet s in sets)
            {
                Dictionary<string, NatanePropertyDeclaration> map = s.ByName();
                var decoration = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                foreach (var kv in map)
                {
                    if (!byName.TryGetValue(kv.Key, out var perShader))
                    {
                        perShader = new Dictionary<string, NatanePropertyDeclaration>(StringComparer.Ordinal);
                        byName[kv.Key] = perShader;
                    }
                    perShader[s.FileName] = kv.Value;

                    // 装飾行はバリアント自身のものを保持する（正準化しない）。
                    decoration[kv.Key] = kv.Value.LeadingLines;
                }

                catalog.DecorationByShader[s.FileName] = decoration;
            }

            // 正準順: 本体の宣言順を基準にし、本体に無いものを以降のバリアント順で追加する。
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (NataneShaderPropertySet s in sets)
                foreach (NatanePropertyDeclaration d in s.Declarations)
                    if (seen.Add(d.Name)) ordered.Add(d.Name);

            // 既定宣言は「本体のもの」、無ければ最も多くのバリアントで使われているもの。
            string mainName = NataneToonVariantLocator.MainFileName;
            foreach (string name in ordered)
            {
                Dictionary<string, NatanePropertyDeclaration> perShader = byName[name];

                NatanePropertyDeclaration def;
                if (!perShader.TryGetValue(mainName, out def))
                {
                    def = perShader.Values
                        .GroupBy(d => d.SignatureKey)
                        .OrderByDescending(g => g.Count())
                        .First()
                        .First();
                }
                catalog.Declarations.Add(def);

                // 既定と異なる宣言を持つバリアントを上書きとして登録する。
                foreach (var grp in perShader
                             .Where(kv => kv.Value.SignatureKey != def.SignatureKey)
                             .GroupBy(kv => kv.Value.SignatureKey))
                {
                    var ov = new NatanePropertyOverride
                    {
                        PropertyName = name,
                        Declaration = grp.First().Value
                    };
                    foreach (var kv in grp) ov.Shaders.Add(kv.Key);
                    catalog.Overrides.Add(ov);
                }
            }

            // 採否グループ: 同じ「所有バリアント集合」を持つプロパティをまとめる。
            var groupBySignature = new Dictionary<string, NatanePropertyGroup>(StringComparer.Ordinal);
            foreach (string name in ordered)
            {
                var owners = byName[name].Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
                string signature = string.Join("|", owners);

                if (!groupBySignature.TryGetValue(signature, out NatanePropertyGroup g))
                {
                    g = new NatanePropertyGroup { Id = MakeGroupId(owners, catalog.Shaders) };
                    foreach (string o in owners) g.Members.Add(o);
                    groupBySignature[signature] = g;
                    catalog.Groups.Add(g);
                }
                g.PropertyNames.Add(name);
            }

            return catalog;
        }

        /// <summary>採否パターンから人間が読めるグループ名を作る。</summary>
        private static string MakeGroupId(List<string> owners, List<string> allShaders)
        {
            if (owners.Count >= allShaders.Count) return "CORE";

            var missing = allShaders.Where(s => !owners.Contains(s)).ToList();
            if (missing.Count <= owners.Count)
                return "EXCEPT_" + string.Join("_", missing.Select(Short));
            return "ONLY_" + string.Join("_", owners.Select(Short));
        }

        private static string Short(string fileName)
        {
            string s = fileName.Replace("NataneToonShader", string.Empty).Replace(".shader", string.Empty).TrimStart('_');
            return s.Length == 0 ? "MAIN" : s;
        }

        // ---- 受け入れテスト ----

        /// <summary>
        /// カタログから再構成した宣言集合が、実ソースの宣言集合と完全一致するかを見る。
        /// 判定は SignatureKey（属性・名前・表示名・型・既定値）の集合一致。
        /// 並び順は正準順へ寄せるため比較対象にしない。
        /// </summary>
        internal static List<string> Verify(NataneShaderPropertyCatalog catalog, List<NataneShaderPropertySet> sets)
        {
            var failures = new List<string>();

            foreach (NataneShaderPropertySet s in sets)
            {
                var actual = new HashSet<string>(s.Declarations.Select(d => d.SignatureKey), StringComparer.Ordinal);
                var rebuilt = new HashSet<string>(
                    catalog.BuildFor(s.FileName).Select(d => d.SignatureKey), StringComparer.Ordinal);

                foreach (string missing in actual.Except(rebuilt, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
                    failures.Add($"{s.FileName}: カタログ側に不足 -> {missing}");

                foreach (string extra in rebuilt.Except(actual, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
                    failures.Add($"{s.FileName}: カタログ側に余分 -> {extra}");

                if (s.Unparsed.Count > 0)
                    failures.Add($"{s.FileName}: 解釈できない行が {s.Unparsed.Count} 件（{s.Unparsed[0]}）");

                // コメントの取りこぼし検出。
                // 当初 StripComments 後に解析していたため Properties 内のセクションコメントが
                // 全て失われていたが、SignatureKey 比較だけでは素通りしてしまった。
                // 生成時にコメントが消えるのは実質的な情報損失なので、ここで明示的に検査する。
                if (s.PreservedCommentLines != s.SourceCommentLines)
                {
                    failures.Add(
                        $"{s.FileName}: Properties 内のコメント行を取りこぼしている " +
                        $"（原文 {s.SourceCommentLines} 行 → 保持 {s.PreservedCommentLines} 行）");
                }
            }

            return failures;
        }

        // ---- レポート ----

        private static string BuildReport(
            NataneShaderPropertyCatalog catalog,
            List<NataneShaderPropertySet> sets,
            List<string> failures,
            List<string> excluded)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Properties 正準カタログ 整合チェック");
            sb.AppendLine();
            sb.AppendLine($"- 生成: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- 対象: {sets.Count} バリアント");
            if (excluded.Count > 0) sb.AppendLine($"- 対象外: {string.Join(", ", excluded)}");
            sb.AppendLine($"- 和集合: {catalog.Declarations.Count} プロパティ");
            sb.AppendLine($"- 採否グループ: {catalog.Groups.Count} 種");
            sb.AppendLine($"- 宣言の上書き: {catalog.Overrides.Count} 件");
            sb.AppendLine();
            sb.AppendLine(failures.Count == 0
                ? "**結果: 合格。** カタログから全バリアントを無損失で再現できます。生成器に進めます。"
                : $"**結果: 不合格（{failures.Count} 件）。** この状態で生成するとプロパティが失われます。");
            sb.AppendLine();

            if (failures.Count > 0)
            {
                sb.AppendLine("## 不一致");
                sb.AppendLine();
                foreach (string f in failures) sb.AppendLine($"- {f}");
                sb.AppendLine();
            }

            sb.AppendLine("## 採否グループ");
            sb.AppendLine();
            sb.AppendLine("| グループ | 所有本数 | プロパティ数 | 代表プロパティ |");
            sb.AppendLine("|---|---:|---:|---|");
            foreach (NatanePropertyGroup g in catalog.Groups.OrderByDescending(g => g.PropertyNames.Count))
            {
                string sample = string.Join(", ", g.PropertyNames.Take(4));
                sb.AppendLine($"| {g.Id} | {g.Members.Count}/{sets.Count} | {g.PropertyNames.Count} | {sample} |");
            }
            sb.AppendLine();

            sb.AppendLine("## 宣言の上書き");
            sb.AppendLine();
            if (catalog.Overrides.Count == 0)
            {
                sb.AppendLine("なし。");
            }
            else
            {
                sb.AppendLine("正準の宣言と異なる内容を持つバリアント。意図的な差分と、");
                sb.AppendLine("更新の取り残し（ドリフト）が混在するので、1件ずつ意図を確認すること。");
                sb.AppendLine();
                foreach (NatanePropertyOverride o in catalog.Overrides.OrderBy(o => o.PropertyName, StringComparer.Ordinal))
                {
                    sb.AppendLine($"### {o.PropertyName}");
                    sb.AppendLine($"- 対象: {string.Join(", ", o.Shaders.Select(Short).OrderBy(x => x, StringComparer.Ordinal))}");
                    sb.AppendLine($"- 上書き: `{o.Declaration.Line}`");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("## バリアント別のプロパティ数");
            sb.AppendLine();
            sb.AppendLine("| ファイル | 実ソース | カタログ再構成 | 原文コメント行 | 保持コメント行 |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            foreach (NataneShaderPropertySet s in sets)
            {
                sb.AppendLine(
                    $"| {s.FileName} | {s.Declarations.Count} | {catalog.BuildFor(s.FileName).Count} " +
                    $"| {s.SourceCommentLines} | {s.PreservedCommentLines} |");
            }
            sb.AppendLine();
            sb.AppendLine("コメント行は「宣言の直前にある装飾行」として保持している。");
            sb.AppendLine("生成時、セクションコメントは正準側の装飾を使い、");
            sb.AppendLine("`// _REFRACTION removed (...)` のような除外理由は採否グループから生成し直す。");
            sb.AppendLine();

            return sb.ToString();
        }

        private static string GenerateReportPath =>
            Path.Combine(ProjectRootPath, "Library", "NataneToon", "Audit", "property-generate.md");

        /// <summary>生成の実行結果をレポートに残す（スキップ理由の追跡用）。</summary>
        private static string WriteGenerateReport(
            List<NataneShaderPropertyWriter.ApplyResult> results, bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Properties 生成レポート");
            sb.AppendLine();
            sb.AppendLine($"- 生成: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- モード: {(dryRun ? "ドライラン（書き込みなし）" : "適用")}");
            sb.AppendLine();

            sb.AppendLine("| ファイル | 状態 | 変更前 | 変更後 | 問題 |");
            sb.AppendLine("|---|---|---:|---:|---:|");
            foreach (NataneShaderPropertyWriter.ApplyResult r in results)
            {
                string state = r.Problems.Count > 0 ? "スキップ(問題あり)"
                    : (r.Written ? "書き込み" : (r.Changed ? "差分あり(未書込)" : "差分なし"));
                sb.AppendLine($"| {r.FileName} | {state} | {r.BeforeLines} | {r.AfterLines} | {r.Problems.Count} |");
            }
            sb.AppendLine();

            var withProblems = results.Where(r => r.Problems.Count > 0).ToList();
            sb.AppendLine("## 検出された問題");
            sb.AppendLine();
            if (withProblems.Count == 0)
            {
                sb.AppendLine("なし。");
            }
            else
            {
                sb.AppendLine("問題が出たファイルは書き込みをスキップしている（他のファイルには影響しない）。");
                sb.AppendLine();
                foreach (NataneShaderPropertyWriter.ApplyResult r in withProblems)
                {
                    sb.AppendLine($"### {r.FileName}");
                    foreach (string p in r.Problems) sb.AppendLine($"- {p}");
                    sb.AppendLine();
                }
            }

            string path = GenerateReportPath;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneToonShader] 生成レポートを書き出せませんでした: {e.Message}");
                return "(書き出し失敗)";
            }
            return path.Replace("\\", "/");
        }

        private static string WriteReport(string markdown)
        {
            string path = ReportPath;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, markdown, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneToonShader] カタログレポートを書き出せませんでした: {e.Message}");
                return "(書き出し失敗)";
            }
            return path.Replace("\\", "/");
        }
    }
}
