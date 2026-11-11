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
    /// 検出項目1件分。重大度つきで、レポート出力と Console 要約の双方に使う。
    /// </summary>
    internal sealed class NataneConsistencyFinding
    {
        public NataneConsistencySeverity Severity;
        public string Category;
        public string Message;
        public string Detail;
    }

    internal enum NataneConsistencySeverity
    {
        /// <summary>実害が確定しているもの（ビルド時にストリップされない等）。</summary>
        Error = 0,
        /// <summary>ドリフトの疑いが強く、人間の確認が要るもの。</summary>
        Warning = 1,
        /// <summary>情報。意図的な設計判断である可能性が高いもの。</summary>
        Info = 2
    }

    /// <summary>
    /// Toon シェーダーバリアント間の「宣言のズレ」を検出する監査。
    ///
    /// 既存の <see cref="NataneShaderUpdateAudit"/> が Registry との縦の突合を担うのに対し、
    /// こちらは 13 本のバリアント同士を横に相互比較し、加えてキーワード管理表
    /// (<see cref="NataneShaderKeywordSynchronizer.KeywordMappings"/>) の穴を検出する。
    ///
    /// パースは <see cref="NataneShaderSourceParser"/> をそのまま再利用する
    /// (コメント除去・行連結・UnparseableLines 安全網が実装済みのため)。
    /// Pass 単位の比較だけは粒度が足りないので、コメント除去済みソースを
    /// このクラスで再走査している。
    ///
    /// 本監査はファイルを一切書き換えない。検出と記録のみ。
    ///
    /// 対象外: MaterialValidator のパフォーマンス評価キーワードとの突合。
    /// MaterialValidator は下流アセンブリ NataneToon.Editor.Tools にあり、
    /// ここ (NataneToon.Editor) から参照すると循環参照になる。
    /// 評価キーワードを KeywordMappings 由来へ移設する時点で本監査に取り込む。
    /// </summary>
    internal static class NataneShaderConsistencyAudit
    {
        // 対象バリアントの探索条件（Particle 除外など）は NataneToonVariantLocator を参照。

        // 「ほぼ全バリアントにあるのに一部だけ欠けている」を判定する許容欠落数。
        // Lite 系のように意図的に機能を落とす系統が最大 4 本あるため、
        // 欠落が 2 本以内のものだけをドリフト候補として扱う。
        private const int MissingCountThreshold = 2;

        // 「同じ欠落パターンを共有する項目数」の上限。これを超える群は設計上の除外とみなす。
        //
        // 根拠: バリアント間の採否パターンを実測すると、和集合 991 プロパティが
        // 11 種類の署名に収まる。設計上の機能縮小（例: Background が持たない 148 個）は
        // 大きな群を作り、更新の取り残しによる本物のドリフトは 1〜数個の小さな群になる。
        // この閾値を入れる前は Warning 183 件中 178 件が Background 単独欠落のノイズだった。
        private const int CoherentGroupThreshold = 5;

        // 例: "// _REFRACTION removed (Lite variant: no GrabPass)"
        //     "// _SOFT_FILTER, _KUWAHARA_FILTER removed (Lite variant: no GrabPass)"
        private static readonly Regex RemovalCommentRegex = new Regex(
            @"//\s*(?<keywords>_[A-Z0-9_]+(?:\s*,\s*_[A-Z0-9_]+)*)\s+removed\s*\((?<reason>[^)]*)\)",
            RegexOptions.Compiled);

        private static readonly Regex PassNameRegex = new Regex(
            "\\bName\\s+\"([^\"]+)\"", RegexOptions.Compiled);

        private static string ProjectRootPath =>
            Path.GetDirectoryName(Application.dataPath)?.Replace("\\", "/") ?? string.Empty;

        private static string ReportPath =>
            Path.Combine(ProjectRootPath, "Library", "NataneToon", "Audit", "shader-consistency-audit.md");

        // ---- 1シェーダー分の解析結果 ----

        private sealed class VariantInfo
        {
            public string FileName;
            public string AssetPath;
            /// <summary>コメント除去前の生ソース。パリティ検査へそのまま渡す。</summary>
            public string Source;
            public HashSet<string> Properties;
            public HashSet<string> FeatureKeywords;
            /// <summary>Pass 名 → その Pass で宣言されている shader_feature キーワード。</summary>
            public Dictionary<string, HashSet<string>> KeywordsByPass;
            /// <summary>意図的な除外コメントで名指しされたキーワード。</summary>
            public HashSet<string> IntentionallyRemoved;
            public List<string> RemovalNotes;
            public List<string> UnparseableLines;
        }

        // ---- エントリポイント ----

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/シェーダー整合性監査 Shader Consistency Audit", false, 61)]
        public static void RunFromMenu()
        {
            List<NataneConsistencyFinding> findings = Run(out int variantCount, out string reportMarkdown);

            if (variantCount == 0)
            {
                EditorUtility.DisplayDialog(
                    "シェーダー整合性監査",
                    "監査対象のシェーダーが見つかりませんでした。\n" +
                    "Shaders/NataneToon/ 配下に NataneToonShader*.shader が存在するか確認してください。",
                    "OK");
                return;
            }

            string path = WriteReport(reportMarkdown);

            int errors = findings.Count(f => f.Severity == NataneConsistencySeverity.Error);
            int warnings = findings.Count(f => f.Severity == NataneConsistencySeverity.Warning);

            LogSummary(findings, variantCount, path);

            EditorUtility.DisplayDialog(
                "シェーダー整合性監査",
                $"対象 {variantCount} 本を監査しました。\n\n" +
                $"要修正 (Error): {errors} 件\n" +
                $"要確認 (Warning): {warnings} 件\n\n" +
                $"詳細レポート:\n{path}\n\n" +
                "要約は Console にも出力しました。",
                "OK");
        }

        /// <summary>
        /// 監査本体。ファイルは書き換えない。
        /// Phase 2 (Properties 生成器) の受け入れテストからも呼べるよう戻り値で結果を返す。
        /// </summary>
        internal static List<NataneConsistencyFinding> Run(out int variantCount, out string reportMarkdown)
        {
            var findings = new List<NataneConsistencyFinding>();
            List<VariantInfo> variants = LoadVariants(out List<string> excludedFiles);
            variantCount = variants.Count;

            if (variantCount == 0)
            {
                reportMarkdown = "# シェーダー整合性監査\n\n監査対象が見つかりませんでした。\n";
                return findings;
            }

            AuditPropertyDrift(variants, findings);
            AuditPassKeywordDrift(variants, findings);
            AuditToggleKeywordParity(variants, findings);
            AuditGuardCoverage(variants, findings);
            AuditPerformanceCoverage(variants, findings);
            AuditUnparseable(variants, findings);

            reportMarkdown = BuildReport(findings, variants, excludedFiles);
            return findings;
        }

        // ---- 読み込み ----

        private static List<VariantInfo> LoadVariants(out List<string> excludedFiles)
        {
            var variants = new List<VariantInfo>();

            // 探索は NataneToonVariantLocator に集約している（カタログ生成側と共通）。
            foreach (NataneToonVariantLocator.Entry e in NataneToonVariantLocator.Load(out excludedFiles))
                variants.Add(BuildVariantInfo(e.FileName, e.AssetPath, e.Source));

            return variants;
        }

        private static VariantInfo BuildVariantInfo(string fileName, string assetPath, string source)
        {
            NataneParsedShaderSource parsed = NataneShaderSourceParser.Parse(source);

            var info = new VariantInfo
            {
                FileName = fileName,
                AssetPath = assetPath,
                Source = source,
                Properties = new HashSet<string>(parsed.PropertyNames, StringComparer.Ordinal),
                FeatureKeywords = new HashSet<string>(parsed.ShaderFeatureKeywords, StringComparer.Ordinal),
                KeywordsByPass = ParseKeywordsByPass(source),
                IntentionallyRemoved = new HashSet<string>(StringComparer.Ordinal),
                RemovalNotes = new List<string>(),
                UnparseableLines = new List<string>(parsed.UnparseableLines)
            };

            // 除外コメントは「コメント」なので、除去前の生ソースから拾う。
            foreach (Match m in RemovalCommentRegex.Matches(source))
            {
                string reason = m.Groups["reason"].Value.Trim();
                string[] keywords = m.Groups["keywords"].Value
                    .Split(',')
                    .Select(k => k.Trim())
                    .Where(k => k.Length > 0)
                    .ToArray();

                foreach (string kw in keywords) info.IntentionallyRemoved.Add(kw);
                info.RemovalNotes.Add($"{string.Join(", ", keywords)} — {reason}");
            }

            return info;
        }

        /// <summary>
        /// Pass 単位の shader_feature 宣言を拾う。
        /// NataneShaderSourceParser はファイル全体の集合しか返さないため、
        /// コメント除去済みソースを Name "..." 区切りで再走査する。
        /// </summary>
        private static Dictionary<string, HashSet<string>> ParseKeywordsByPass(string source)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            string cleaned = NataneShaderSourceParser.StripComments(source);

            string currentPass = null;

            foreach (string rawLine in cleaned.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;

                Match nameMatch = PassNameRegex.Match(line);
                if (nameMatch.Success)
                {
                    currentPass = nameMatch.Groups[1].Value;
                    if (!result.ContainsKey(currentPass))
                        result[currentPass] = new HashSet<string>(StringComparer.Ordinal);
                    continue;
                }

                if (currentPass == null) continue;
                if (!line.StartsWith("#", StringComparison.Ordinal)) continue;

                string body = line.Substring(1).TrimStart();
                if (!body.StartsWith("pragma", StringComparison.Ordinal)) continue;

                string[] tokens = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3) continue;
                if (!tokens[1].StartsWith("shader_feature", StringComparison.Ordinal)) continue;

                for (int i = 2; i < tokens.Length; i++)
                {
                    string token = tokens[i];
                    if (token == "__" || token == "_") continue;
                    result[currentPass].Add(token);
                }
            }

            return result;
        }

        // ---- 検出1: プロパティのドリフト ----

        private static void AuditPropertyDrift(List<VariantInfo> variants, List<NataneConsistencyFinding> findings)
        {
            // プロパティ名 → それを宣言しているバリアント
            var declaring = new Dictionary<string, List<VariantInfo>>(StringComparer.Ordinal);
            foreach (VariantInfo v in variants)
                foreach (string prop in v.Properties)
                {
                    if (!declaring.TryGetValue(prop, out var list))
                    {
                        list = new List<VariantInfo>();
                        declaring[prop] = list;
                    }
                    list.Add(v);
                }

            // プロパティ → キーワード（意図的除外の判定に使う）
            var propertyToKeyword = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
                if (!string.IsNullOrEmpty(mapping.propertyName) && !string.IsNullOrEmpty(mapping.keyword))
                    propertyToKeyword[mapping.propertyName] = mapping.keyword;

            // 欠落パターン（どのバリアントに無いか）で項目をクラスタリングする。
            var byMissingSignature = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var missingOf = new Dictionary<string, List<VariantInfo>>(StringComparer.Ordinal);

            foreach (var kv in declaring)
            {
                var missingIn = variants.Where(v => !kv.Value.Contains(v)).ToList();
                if (missingIn.Count == 0) continue;

                string signature = string.Join("|", missingIn.Select(v => v.FileName).OrderBy(x => x, StringComparer.Ordinal));
                if (!byMissingSignature.TryGetValue(signature, out var names))
                {
                    names = new List<string>();
                    byMissingSignature[signature] = names;
                }
                names.Add(kv.Key);
                missingOf[kv.Key] = missingIn;
            }

            foreach (var group in byMissingSignature.OrderByDescending(g => g.Value.Count))
            {
                List<VariantInfo> missingIn = missingOf[group.Value[0]];

                // 大きな群 = 設計上の機能縮小。1行にまとめて情報として出す。
                if (group.Value.Count > CoherentGroupThreshold)
                {
                    findings.Add(new NataneConsistencyFinding
                    {
                        Severity = NataneConsistencySeverity.Info,
                        Category = "設計上の機能差",
                        Message = $"{group.Value.Count} プロパティが {string.Join(", ", missingIn.Select(v => v.FileName))} にのみ存在しない",
                        Detail =
                            "同じ欠落パターンを共有する項目が多いため、更新漏れではなく機能縮小と判断した。\n" +
                            "例: " + string.Join(", ", group.Value.Take(8))
                    });
                    continue;
                }

                // 小さな群のうち、欠落本数も少ないものだけがドリフト候補。
                if (missingIn.Count > MissingCountThreshold) continue;

                foreach (string propName in group.Value.OrderBy(x => x, StringComparer.Ordinal))
                {
                    propertyToKeyword.TryGetValue(propName, out string keyword);
                    var unexplained = missingIn
                        .Where(v => keyword == null || !v.IntentionallyRemoved.Contains(keyword))
                        .ToList();

                    if (unexplained.Count == 0)
                    {
                        findings.Add(new NataneConsistencyFinding
                        {
                            Severity = NataneConsistencySeverity.Info,
                            Category = "プロパティ欠落(意図的)",
                            Message = $"{propName} — {missingIn.Count} 本で未宣言（除外コメントで説明済み）",
                            Detail = string.Join(", ", missingIn.Select(v => v.FileName))
                        });
                        continue;
                    }

                    findings.Add(new NataneConsistencyFinding
                    {
                        Severity = NataneConsistencySeverity.Warning,
                        Category = "プロパティ欠落",
                        Message = $"{propName} — {variants.Count - missingIn.Count}/{variants.Count} 本で宣言。除外理由が見当たらない",
                        Detail = "未宣言: " + string.Join(", ", unexplained.Select(v => v.FileName))
                    });
                }
            }
        }

        // ---- 検出2: Pass 間の shader_feature ドリフト ----

        private static void AuditPassKeywordDrift(List<VariantInfo> variants, List<NataneConsistencyFinding> findings)
        {
            // 全バリアントに登場する Pass 名を対象にする。
            var allPasses = new HashSet<string>(StringComparer.Ordinal);
            foreach (VariantInfo v in variants)
                foreach (string pass in v.KeywordsByPass.Keys)
                    allPasses.Add(pass);

            foreach (string pass in allPasses.OrderBy(p => p, StringComparer.Ordinal))
            {
                // この Pass を持つバリアントだけを母集団にする
                // （Ghost/XRay など Pass 構成が違う系統を誤検出しないため）。
                var withPass = variants.Where(v => v.KeywordsByPass.ContainsKey(pass)).ToList();
                if (withPass.Count < 3) continue;

                var declaring = new Dictionary<string, List<VariantInfo>>(StringComparer.Ordinal);
                foreach (VariantInfo v in withPass)
                    foreach (string kw in v.KeywordsByPass[pass])
                    {
                        if (!declaring.TryGetValue(kw, out var list))
                        {
                            list = new List<VariantInfo>();
                            declaring[kw] = list;
                        }
                        list.Add(v);
                    }

                // プロパティ側と同じく、欠落パターンでクラスタリングしてノイズを潰す。
                var byMissingSignature = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var missingOf = new Dictionary<string, List<VariantInfo>>(StringComparer.Ordinal);

                foreach (var kv in declaring)
                {
                    var missingIn = withPass.Where(v => !kv.Value.Contains(v)).ToList();
                    if (missingIn.Count == 0) continue;

                    string signature = string.Join("|", missingIn.Select(v => v.FileName).OrderBy(x => x, StringComparer.Ordinal));
                    if (!byMissingSignature.TryGetValue(signature, out var kws))
                    {
                        kws = new List<string>();
                        byMissingSignature[signature] = kws;
                    }
                    kws.Add(kv.Key);
                    missingOf[kv.Key] = missingIn;
                }

                foreach (var group in byMissingSignature.OrderByDescending(g => g.Value.Count))
                {
                    List<VariantInfo> missingIn = missingOf[group.Value[0]];

                    if (group.Value.Count > CoherentGroupThreshold)
                    {
                        findings.Add(new NataneConsistencyFinding
                        {
                            Severity = NataneConsistencySeverity.Info,
                            Category = "設計上の機能差",
                            Message = $"{pass} パス: {group.Value.Count} キーワードが {string.Join(", ", missingIn.Select(v => v.FileName))} にのみ無い",
                            Detail =
                                "同じ欠落パターンを共有する項目が多いため機能縮小と判断した。\n" +
                                "例: " + string.Join(", ", group.Value.Take(8))
                        });
                        continue;
                    }

                    if (missingIn.Count > MissingCountThreshold) continue;

                    foreach (string kw in group.Value.OrderBy(x => x, StringComparer.Ordinal))
                    {
                        var unexplained = missingIn
                            .Where(v => !v.IntentionallyRemoved.Contains(kw))
                            // そのキーワードがシェーダー内のどのパスにも無いなら、
                            // 「このパスだけ書き忘れた」ではなく機能ごと非搭載。
                            // 例: Background は _SMEAR をプロパティごと持たない。
                            .Where(v => v.FeatureKeywords.Contains(kw))
                            .ToList();
                        if (unexplained.Count == 0) continue;

                        findings.Add(new NataneConsistencyFinding
                        {
                            Severity = NataneConsistencySeverity.Warning,
                            Category = $"pragma 欠落({pass})",
                            Message = $"{kw} — {pass} パスで {withPass.Count - missingIn.Count}/{withPass.Count} 本のみ宣言",
                            Detail = "未宣言: " + string.Join(", ", unexplained.Select(v => v.FileName))
                        });
                    }
                }
            }

            // 同一バリアント内での FORWARD_BASE / FORWARD_ADD の非対称も参考情報として出す。
            foreach (VariantInfo v in variants)
            {
                if (!v.KeywordsByPass.TryGetValue("FORWARD_BASE", out var baseKw)) continue;
                if (!v.KeywordsByPass.TryGetValue("FORWARD_ADD", out var addKw)) continue;

                var baseOnly = baseKw.Except(addKw, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
                if (baseOnly.Count == 0) continue;

                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Info,
                    Category = "BASE/ADD 非対称",
                    Message = $"{v.FileName} — FORWARD_BASE のみで宣言: {baseOnly.Count} 件",
                    Detail =
                        "これ自体は正常なことが多い（該当コードが UNITY_PASS_FORWARDBASE ガード内にある場合、\n" +
                        "ADD パスで pragma を宣言しても効果が無いどころか変種を無駄に増やす）。\n" +
                        "実際 _RIM_LIGHT / _RIM_LIGHT_2 / _OFFSET_RIM_LIGHT / _ENV_RIM / _SSS は\n" +
                        "フラグメント側が完全に BASE 限定であり、宣言していない本体シェーダーの方が正しい。\n" +
                        string.Join(", ", baseOnly)
                });
            }
        }

        // ---- 検出2b: Properties の Toggle キーワードが未コンパイル（逆方向）----

        /// <summary>
        /// <c>Properties</c> の <c>[Toggle(KEYWORD)]</c> が立てるキーワードを、
        /// どのパスもコンパイルしていない状態を検出する。
        ///
        /// 既存の検出はすべて「<c>#pragma</c> にあるものが他所にあるか」を見ており、
        /// この逆方向は素通りしていた。トグルをONにしても何も変わらず、
        /// マテリアルには誰も読まないキーワードだけが書き込まれる。
        ///
        /// 判定と宣言テーブルは <see cref="NataneShaderParityChecker"/> に集約している
        /// （Unity 非依存の CI スクリプトと同じ判定にするため）。
        /// </summary>
        private static void AuditToggleKeywordParity(List<VariantInfo> variants, List<NataneConsistencyFinding> findings)
        {
            var inputs = variants
                .Select(v => new NataneParityInput { FileName = v.FileName, Source = v.Source })
                .ToList();

            List<NataneParityFinding> parity = NataneShaderParityChecker.FindUncompiledToggleKeywords(inputs);
            if (parity.Count == 0)
            {
                return;
            }

            // 未宣言＝新規に混入したもの。ここが本検査の主目的なので個別に出す。
            foreach (NataneParityFinding f in parity.Where(f => !f.Declared))
            {
                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Error,
                    Category = "未コンパイルのToggleキーワード",
                    Message = NataneShaderParityChecker.DescribeFinding(f),
                    Detail =
                        "インスペクタのトグルが何も変えず、誰も読まないキーワードだけがマテリアルへ書き込まれる。\n" +
                        "対応は次のいずれか:\n" +
                        "  (a) 該当パスへ #pragma shader_feature_local を足して実際に効かせる\n" +
                        "  (b) Uniform 分岐で実装し [Toggle(KEYWORD)] を [Toggle] へ変更する\n" +
                        "  (c) 意図的なら NataneShaderParityChecker の宣言テーブルへ理由付きで登録する"
                });
            }

            // 宣言済みは既知の負債。件数だけまとめて残し、レポートから消えないようにする。
            var declaredGlobal = parity
                .Where(f => f.Declared && f.Kind == NataneParityKind.UncompiledEverywhere)
                .Select(f => f.Keyword)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            if (declaredGlobal.Count > 0)
            {
                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Info,
                    Category = "未コンパイルのToggleキーワード(受理済み)",
                    Message = $"{declaredGlobal.Count} キーワードが全変種で未コンパイル（宣言テーブルで受理済み）",
                    Detail =
                        "HLSL 側がマスクを常時サンプルする実装のため絵は正しいが、トグル自体は効いていない。\n" +
                        "Uniform 分岐へ移すと、既にマスクを設定しているマテリアルの見た目が変わるため個別判断が要る。\n" +
                        string.Join(", ", declaredGlobal)
                });
            }

            // 宣言テーブルの腐り（修正済みなのに残っている行）も報告する。
            List<string> stale = NataneShaderParityChecker.FindStaleDeclarations(inputs);
            if (stale.Count > 0)
            {
                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Warning,
                    Category = "パリティ宣言テーブルの腐り",
                    Message = $"{stale.Count} 件が宣言テーブルにあるが実際には検出されない",
                    Detail =
                        "既に解消済みなので NataneShaderParityChecker から削除してよい。\n" +
                        string.Join(", ", stale)
                });
            }
        }

        // ---- 検出3: AWBO の #undef ガードが届かないキーワード（実害あり） ----

        private static void AuditGuardCoverage(List<VariantInfo> variants, List<NataneConsistencyFinding> findings)
        {
            var guarded = new HashSet<string>(
                NataneBuildFeatureOptimizer.GetGuardKeywords(), StringComparer.Ordinal);

            var used = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (VariantInfo v in variants)
                foreach (string kw in v.FeatureKeywords)
                {
                    if (!used.TryGetValue(kw, out var list))
                    {
                        list = new List<string>();
                        used[kw] = list;
                    }
                    list.Add(v.FileName);
                }

            foreach (var kv in used.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (guarded.Contains(kv.Key)) continue;

                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Error,
                    Category = "AWBOガード穴",
                    Message = $"{kv.Key} — shader_feature で使用されているが KeywordMappings / ExtraKeywords に未登録",
                    Detail =
                        "NataneToonBuildSettings.hlsl に #undef ガードが自動生成されないため、" +
                        "ビルド機能最適化でこの機能を無効化してもストリップされない。" +
                        $"使用: {string.Join(", ", kv.Value)}"
                });
            }

            // 逆方向: 管理表にあるのに、どのシェーダーでも使われていないキーワード。
            foreach (string kw in guarded.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (used.ContainsKey(kw)) continue;

                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Info,
                    Category = "孤児キーワード",
                    Message = $"{kw} — 管理表にあるが Toon バリアントの shader_feature に出現しない",
                    Detail = "Eye / Wirelight など対象外シェーダー専用か、既に削除された機能の可能性がある。"
                });
            }
        }

        // ---- 検出4: パフォーマンス評価の計上漏れ ----

        /// <summary>
        /// インスペクターにセクションとして登録されている（＝ユーザーが意識して
        /// 有効化する）機能のうち、パフォーマンス評価に計上されていないものを報告する。
        ///
        /// 計上対象は手で維持している集合なので、全キーワードとの差集合を出すと
        /// 軽微なものまで並んでノイズになる。セクション登録の有無で「ユーザーが
        /// 機能として認識するもの」に絞ってから突き合わせる。
        /// </summary>
        private static void AuditPerformanceCoverage(List<VariantInfo> variants, List<NataneConsistencyFinding> findings)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (VariantInfo v in variants)
                foreach (string kw in v.FeatureKeywords)
                    used.Add(kw);

            var sectionKeywords = new HashSet<string>(
                NataneToonInspectorSectionRegistry.All
                    .Select(s => s.ToggleKeyword)
                    .Where(k => !string.IsNullOrEmpty(k)),
                StringComparer.Ordinal);

            var uncounted = used
                .Where(sectionKeywords.Contains)
                .Where(kw => !NatanePerformanceFeatureKeywords.IsCounted(kw))
                // 「負荷を増やさない」と判断して意図的に外したものは再提示しない。
                .Where(kw => !NatanePerformanceFeatureKeywords.IsIntentionallyNotCounted(kw))
                .OrderBy(kw => kw, StringComparer.Ordinal)
                .ToList();

            if (uncounted.Count > 0)
            {
                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Warning,
                    Category = "パフォーマンス評価の計上漏れ",
                    Message = $"インスペクターに登録済みの {uncounted.Count} 機能が評価に計上されていない",
                    Detail =
                        "これらを有効にしても A/B/C/D の判定に反映されないため、実際より軽い評価が出る。\n" +
                        "計上すべきかは機能ごとの負荷次第なので、追加は個別に判断すること。\n" +
                        string.Join(", ", uncounted)
                });
            }

            var stale = NatanePerformanceFeatureKeywords.Counted
                .Where(kw => !used.Contains(kw))
                .OrderBy(kw => kw, StringComparer.Ordinal)
                .ToList();

            if (stale.Count > 0)
            {
                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Info,
                    Category = "パフォーマンス評価の死んだ項目",
                    Message = $"計上対象の {stale.Count} 件が Toon バリアントに存在しない",
                    Detail = string.Join(", ", stale)
                });
            }
        }

        // ---- 検出5: パース不能行（安全網） ----

        private static void AuditUnparseable(List<VariantInfo> variants, List<NataneConsistencyFinding> findings)
        {
            foreach (VariantInfo v in variants)
            {
                if (v.UnparseableLines.Count == 0) continue;

                findings.Add(new NataneConsistencyFinding
                {
                    Severity = NataneConsistencySeverity.Warning,
                    Category = "パース不能",
                    Message = $"{v.FileName} — 解釈できない行が {v.UnparseableLines.Count} 件",
                    Detail =
                        "本監査の結果がこの行の分だけ不完全である可能性がある。\n" +
                        string.Join("\n", v.UnparseableLines.Take(20))
                });
            }
        }

        // ---- 出力 ----

        private static string BuildReport(
            List<NataneConsistencyFinding> findings,
            List<VariantInfo> variants,
            List<string> excludedFiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# シェーダー整合性監査レポート");
            sb.AppendLine();
            sb.AppendLine($"- 生成: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- Unity: {Application.unityVersion}");
            sb.AppendLine($"- 対象バリアント: {variants.Count} 本");
            if (excludedFiles.Count > 0)
                sb.AppendLine($"- 対象外: {string.Join(", ", excludedFiles)}（構造が別系統のため相互比較から除外）");
            sb.AppendLine();
            sb.AppendLine("このレポートはファイルを書き換えません。検出と記録のみです。");
            sb.AppendLine();

            foreach (NataneConsistencySeverity severity in new[]
            {
                NataneConsistencySeverity.Error,
                NataneConsistencySeverity.Warning,
                NataneConsistencySeverity.Info
            })
            {
                var group = findings.Where(f => f.Severity == severity).ToList();
                sb.AppendLine($"## {SeverityLabel(severity)} — {group.Count} 件");
                sb.AppendLine();

                if (group.Count == 0)
                {
                    sb.AppendLine("なし。");
                    sb.AppendLine();
                    continue;
                }

                foreach (var byCategory in group.GroupBy(f => f.Category).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    sb.AppendLine($"### {byCategory.Key}");
                    sb.AppendLine();
                    foreach (NataneConsistencyFinding f in byCategory)
                    {
                        sb.AppendLine($"- **{f.Message}**");
                        if (!string.IsNullOrEmpty(f.Detail))
                            foreach (string line in f.Detail.Split('\n'))
                                sb.AppendLine($"  - {line}");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("## 意図的な除外コメント一覧");
            sb.AppendLine();
            bool anyRemoval = false;
            foreach (VariantInfo v in variants)
            {
                if (v.RemovalNotes.Count == 0) continue;
                anyRemoval = true;
                sb.AppendLine($"### {v.FileName}");
                foreach (string note in v.RemovalNotes) sb.AppendLine($"- {note}");
                sb.AppendLine();
            }
            if (!anyRemoval)
            {
                sb.AppendLine("検出されませんでした。");
                sb.AppendLine();
            }

            sb.AppendLine("## 本監査の対象外");
            sb.AppendLine();
            sb.AppendLine("- **MaterialValidator のパフォーマンス評価キーワードとの突合**: ");
            sb.AppendLine("  MaterialValidator は下流アセンブリ `NataneToon.Editor.Tools` にあり、");
            sb.AppendLine("  本監査 (`NataneToon.Editor`) から参照すると循環参照になるため未実装。");
            sb.AppendLine("  評価キーワードを `KeywordMappings` 由来の導出に移設した時点で取り込む。");
            sb.AppendLine("- **Eye / Wirelight / ScreenFXOverlay**: Toon バリアントと構造が別系統のため対象外。");
            sb.AppendLine();

            sb.AppendLine("## 対象バリアント");
            sb.AppendLine();
            sb.AppendLine("| ファイル | プロパティ数 | shader_feature 数 |");
            sb.AppendLine("|---|---:|---:|");
            foreach (VariantInfo v in variants)
                sb.AppendLine($"| {v.FileName} | {v.Properties.Count} | {v.FeatureKeywords.Count} |");
            sb.AppendLine();

            return sb.ToString();
        }

        private static string WriteReport(string markdown)
        {
            string path = ReportPath;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, markdown, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneToonShader] 整合性監査レポートを書き出せませんでした: {e.Message}");
                return "(書き出し失敗)";
            }
            return path.Replace("\\", "/");
        }

        private static void LogSummary(List<NataneConsistencyFinding> findings, int variantCount, string reportPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[NataneToonShader] シェーダー整合性監査: 対象 {variantCount} 本");
            sb.AppendLine($"レポート: {reportPath}");
            sb.AppendLine();

            foreach (NataneConsistencySeverity severity in new[]
            {
                NataneConsistencySeverity.Error,
                NataneConsistencySeverity.Warning
            })
            {
                var group = findings.Where(f => f.Severity == severity).ToList();
                if (group.Count == 0) continue;

                sb.AppendLine($"■ {SeverityLabel(severity)} ({group.Count} 件)");
                foreach (NataneConsistencyFinding f in group.Take(30))
                    sb.AppendLine($"  [{f.Category}] {f.Message}");
                if (group.Count > 30)
                    sb.AppendLine($"  … 他 {group.Count - 30} 件（全件はレポート参照）");
                sb.AppendLine();
            }

            int errors = findings.Count(f => f.Severity == NataneConsistencySeverity.Error);
            if (errors > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        private static string SeverityLabel(NataneConsistencySeverity severity)
        {
            switch (severity)
            {
                case NataneConsistencySeverity.Error: return "要修正 (Error)";
                case NataneConsistencySeverity.Warning: return "要確認 (Warning)";
                default: return "情報 (Info)";
            }
        }
    }
}
