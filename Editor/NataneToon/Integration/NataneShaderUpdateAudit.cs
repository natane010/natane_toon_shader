using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    // ---- 解析結果（1シェーダーソース分）----
    // 単一正規表現に頼らず、コメント除去＋行連結の前処理後に各要素を抽出する。
    public sealed class NataneParsedShaderSource
    {
        public readonly List<string> ShaderNames = new List<string>();
        public readonly List<string> Includes = new List<string>();
        public readonly HashSet<string> ShaderFeatureKeywords = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> MultiCompileKeywords = new HashSet<string>(StringComparer.Ordinal);
        public readonly List<string> PropertyNames = new List<string>();
        public readonly List<string> PassNames = new List<string>();
        public readonly List<string> UsePasses = new List<string>();
        public readonly List<string> Fallbacks = new List<string>();
        public readonly List<string> UnparseableLines = new List<string>();
    }

    /// <summary>
    /// .shader / .hlsl / .cginc ソースの構文解析。前処理として
    /// (1) ブロック/行コメント除去（文字列リテラルは保護）
    /// (2) 行末バックスラッシュによる行連結（複数行 pragma 対応）
    /// を行った後に #include / #pragma shader_feature 系・multi_compile 系 /
    /// Shader 名 / Properties / Pass 名 / UsePass / Fallback を抽出する。
    /// 期待要素を取り出せなかった行は「存在しない」ではなく UnparseableLines へ記録する。
    /// </summary>
    public static class NataneShaderSourceParser
    {
        private static readonly Regex ShaderNameRegex = new Regex("\\bShader\\s+\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex NameRegex = new Regex("\\bName\\s+\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex UsePassRegex = new Regex("\\bUsePass\\s+\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex FallbackRegex = new Regex("\\bFallback\\s+(\"([^\"]+)\"|Off|[A-Za-z0-9_/]+)", RegexOptions.Compiled);
        // #include と #include_with_pragmas の双方を許容
        private static readonly Regex IncludeRegex = new Regex("#\\s*include(?:_with_pragmas)?\\s+(\"([^\"]+)\"|<([^>]+)>)", RegexOptions.Compiled);
        private static readonly Regex PropertyRegex = new Regex("^\\s*(?:\\[[^\\]]*\\]\\s*)*([_A-Za-z][A-Za-z0-9_]*)\\s*\\(", RegexOptions.Compiled);

        public static NataneParsedShaderSource Parse(string source)
        {
            var result = new NataneParsedShaderSource();
            if (string.IsNullOrEmpty(source))
            {
                return result;
            }

            string cleaned = StripComments(source);
            cleaned = JoinLineContinuations(cleaned);

            // Shader 名 / UsePass / Fallback は全文から拾う（ブロック外にも現れうる）
            foreach (Match m in ShaderNameRegex.Matches(cleaned))
            {
                result.ShaderNames.Add(m.Groups[1].Value);
            }

            string propertiesBlock = ExtractBracedBlock(cleaned, "Properties");
            if (propertiesBlock != null)
            {
                foreach (string rawLine in propertiesBlock.Split('\n'))
                {
                    Match pm = PropertyRegex.Match(rawLine);
                    if (pm.Success)
                    {
                        result.PropertyNames.Add(pm.Groups[1].Value);
                    }
                }
            }

            foreach (string rawLine in cleaned.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    ParsePreprocessorLine(line, result);
                    continue;
                }

                Match usePass = UsePassRegex.Match(line);
                if (usePass.Success)
                {
                    result.UsePasses.Add(usePass.Groups[1].Value);
                }

                if (line.StartsWith("Fallback", StringComparison.Ordinal))
                {
                    Match fb = FallbackRegex.Match(line);
                    if (fb.Success)
                    {
                        result.Fallbacks.Add(fb.Groups[1].Value.Trim('"'));
                    }
                    else
                    {
                        result.UnparseableLines.Add(line);
                    }
                }

                Match name = NameRegex.Match(line);
                if (name.Success)
                {
                    result.PassNames.Add(name.Groups[1].Value);
                }
            }

            return result;
        }

        private static void ParsePreprocessorLine(string line, NataneParsedShaderSource result)
        {
            // 行頭の "#" と directive の間に空白が入る記法も許容
            string body = line.Substring(1).TrimStart();

            if (body.StartsWith("include", StringComparison.Ordinal))
            {
                Match inc = IncludeRegex.Match(line);
                if (inc.Success)
                {
                    string path = inc.Groups[2].Success ? inc.Groups[2].Value : inc.Groups[3].Value;
                    if (!string.IsNullOrEmpty(path))
                    {
                        result.Includes.Add(path);
                        return;
                    }
                }

                result.UnparseableLines.Add(line);
                return;
            }

            if (!body.StartsWith("pragma", StringComparison.Ordinal))
            {
                return; // #if/#define などは対象外（keyword を持たない）
            }

            string[] tokens = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                return;
            }

            string directive = tokens[1];
            bool isShaderFeature = directive.StartsWith("shader_feature", StringComparison.Ordinal);
            bool isMultiCompile = directive.StartsWith("multi_compile", StringComparison.Ordinal);
            if (!isShaderFeature && !isMultiCompile)
            {
                return; // #pragma vertex / target 等は keyword 抽出対象外
            }

            int added = 0;
            for (int i = 2; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (token == "__" || token == "_")
                {
                    continue; // 「キーワード無し」オプションのプレースホルダ
                }

                if (!IsKeywordToken(token))
                {
                    continue;
                }

                if (isShaderFeature)
                {
                    result.ShaderFeatureKeywords.Add(token);
                }
                else
                {
                    result.MultiCompileKeywords.Add(token);
                }

                added++;
            }

            if (added == 0)
            {
                if (isMultiCompile)
                {
                    // multi_compile_fog / multi_compile_instancing 等の組込みショートカット形は
                    // 明示キーワードを持たない正当な記法。管理外リストへ directive 名を記録する。
                    result.MultiCompileKeywords.Add(directive);
                }
                else
                {
                    // shader_feature 系でキーワードを1つも取り出せない＝解析不能として記録（安全側）
                    result.UnparseableLines.Add(line);
                }
            }
        }

        private static bool IsKeywordToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            char first = token[0];
            if (first != '_' && !char.IsLetter(first))
            {
                return false;
            }

            for (int i = 1; i < token.Length; i++)
            {
                char c = token[i];
                if (c != '_' && !char.IsLetterOrDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        // 文字列リテラルを保護しつつ // 行コメントと /* */ ブロックコメントを空白へ置換（改行は保持）。
        // NataneShaderConsistencyAudit が Pass 単位の再解析に流用するため internal。
        internal static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            int i = 0;
            int n = src.Length;
            while (i < n)
            {
                char c = src[i];

                if (c == '"')
                {
                    sb.Append(c);
                    i++;
                    while (i < n)
                    {
                        char s = src[i];
                        sb.Append(s);
                        i++;
                        if (s == '\\' && i < n)
                        {
                            sb.Append(src[i]);
                            i++;
                            continue;
                        }
                        if (s == '"')
                        {
                            break;
                        }
                    }
                    continue;
                }

                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n')
                    {
                        i++;
                    }
                    continue;
                }

                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    i += 2;
                    while (i < n && !(src[i] == '*' && i + 1 < n && src[i + 1] == '/'))
                    {
                        if (src[i] == '\n')
                        {
                            sb.Append('\n'); // 行番号/連結判定のため改行は保持
                        }
                        i++;
                    }
                    i += 2; // "*/" を読み飛ばす
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        private static string JoinLineContinuations(string src)
        {
            return Regex.Replace(src, "\\\\[^\\S\\r\\n]*\\r?\\n", " ");
        }

        private static string ExtractBracedBlock(string src, string keyword)
        {
            int idx = 0;
            while (true)
            {
                idx = src.IndexOf(keyword, idx, StringComparison.Ordinal);
                if (idx < 0)
                {
                    return null;
                }

                int brace = src.IndexOf('{', idx);
                if (brace < 0)
                {
                    return null;
                }

                // keyword と '{' の間に別トークンが挟まる場合はスキップ（誤検出防止）
                string between = src.Substring(idx + keyword.Length, brace - (idx + keyword.Length)).Trim();
                if (between.Length != 0)
                {
                    idx += keyword.Length;
                    continue;
                }

                int depth = 0;
                for (int i = brace; i < src.Length; i++)
                {
                    if (src[i] == '{') depth++;
                    else if (src[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return src.Substring(brace + 1, i - brace - 1);
                        }
                    }
                }

                return src.Substring(brace + 1); // 閉じ括弧欠落時は残り全体（安全側）
            }
        }
    }

    // ---- 保存用データ（JsonUtility 対応）----
    [Serializable]
    public sealed class NataneShaderVariantEstimate
    {
        public string shader;
        public int featureKeywordCount;
        public double estimatedVariants;
    }

    [Serializable]
    public sealed class NataneShaderAuditData
    {
        public int schemaVersion;
        public string shaderDependencyFingerprint;
        public string registryFingerprint;
        public int registrySchemaVersion;
        public string packageVersion;
        public string unityVersion;
        public long generatedAtUtcTicks;

        public List<string> unknownKeywords = new List<string>();       // "shader :: keyword"
        public List<string> orphanDefinitions = new List<string>();     // keyword（どの Shader にも無い定義）
        public List<string> missingTargetShaders = new List<string>();  // "keyword :: shader"（対象 Shader 不在）
        public List<string> missingProperties = new List<string>();     // "keyword :: property"
        public List<string> multiCompileKeywords = new List<string>();  // 管理外（別リスト）
        public List<string> unparseableItems = new List<string>();      // "path :: line"
        public List<string> unresolvedShaders = new List<string>();     // Shader.Find 失敗
        public List<NataneShaderVariantEstimate> variantEstimates = new List<NataneShaderVariantEstimate>();
        public double totalVariantEstimate;
        public int managedKeywordCount;
    }

    /// <summary>
    /// Shader アップデート監査基盤。管理対象 Natane シェーダーの実キーワード集合と Registry を突合し、
    /// 未知キーワード・孤児定義・対象不在・プロパティ欠落を検出する。Material/Clip の自動保存や
    /// 積極ストリップの続行は行わない（検出と記録のみ）。
    /// </summary>
    public static class NataneShaderUpdateAudit
    {
        public const int AuditSchemaVersion = 1;

        private const string AuditFileName = "shader-update-audit-v1.json";
        private static string ProjectRootPath => Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
        private static string AuditPath => Path.Combine(ProjectRootPath, "Library", "NataneToon", "Audit", AuditFileName);

        // ---- 監査本体 ----

        public static NataneShaderAuditData Run()
        {
            var data = new NataneShaderAuditData
            {
                schemaVersion = AuditSchemaVersion,
                registrySchemaVersion = NataneShaderFeatureRegistry.RegistrySchemaVersion,
                registryFingerprint = NataneShaderFeatureRegistry.ComputeRegistryFingerprint(),
                packageVersion = NataneShaderFeatureRegistry.ShaderCompatibilityVersion,
                unityVersion = Application.unityVersion,
                generatedAtUtcTicks = DateTime.UtcNow.Ticks
            };

            var rootFullPaths = new List<string>();
            // keyword -> それを宣言しているシェーダー名の集合
            var keywordDeclaringShaders = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            // シェーダー名 -> Properties 名集合
            var shaderProperties = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var allShaderFeatureKeywords = new HashSet<string>(StringComparer.Ordinal);
            var allMultiCompile = new HashSet<string>(StringComparer.Ordinal);
            var resolvedShaderNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (string shaderName in NataneShaderCatalog.ShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    data.unresolvedShaders.Add(shaderName);
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(shader);
                if (string.IsNullOrEmpty(assetPath))
                {
                    data.unresolvedShaders.Add(shaderName);
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(assetPath);
                }
                catch
                {
                    data.unresolvedShaders.Add(shaderName);
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    data.unresolvedShaders.Add(shaderName);
                    continue;
                }

                resolvedShaderNames.Add(shaderName);
                rootFullPaths.Add(fullPath);

                string source = File.ReadAllText(fullPath);
                NataneParsedShaderSource parsed = NataneShaderSourceParser.Parse(source);

                shaderProperties[shaderName] = new HashSet<string>(parsed.PropertyNames, StringComparer.Ordinal);

                foreach (string kw in parsed.ShaderFeatureKeywords)
                {
                    allShaderFeatureKeywords.Add(kw);
                    if (!keywordDeclaringShaders.TryGetValue(kw, out var set))
                    {
                        set = new HashSet<string>(StringComparer.Ordinal);
                        keywordDeclaringShaders[kw] = set;
                    }
                    set.Add(shaderName);

                    // (a) 未知キーワード（Shader にあり Registry に無い）
                    if (!NataneShaderFeatureRegistry.IsKnownKeyword(kw))
                    {
                        data.unknownKeywords.Add($"{shaderName} :: {kw}");
                    }
                }

                foreach (string mc in parsed.MultiCompileKeywords)
                {
                    allMultiCompile.Add(mc);
                }

                foreach (string bad in parsed.UnparseableLines)
                {
                    data.unparseableItems.Add($"{assetPath} :: {bad}");
                }

                int featureCount = parsed.ShaderFeatureKeywords.Count;
                data.variantEstimates.Add(new NataneShaderVariantEstimate
                {
                    shader = shaderName,
                    featureKeywordCount = featureCount,
                    estimatedVariants = EstimateVariants(featureCount)
                });
            }

            data.multiCompileKeywords = allMultiCompile.OrderBy(k => k, StringComparer.Ordinal).ToList();
            data.managedKeywordCount = allShaderFeatureKeywords.Count;
            data.totalVariantEstimate = data.variantEstimates.Sum(v => v.estimatedVariants);

            foreach (var def in NataneShaderFeatureRegistry.AllDefinitions)
            {
                bool declaredSomewhere = keywordDeclaringShaders.TryGetValue(def.Keyword, out var declaring);

                // (b) 孤児定義（Registry にありどの Shader にも無い）
                if (!declaredSomewhere)
                {
                    data.orphanDefinitions.Add(def.Keyword);
                }

                // (c) 対象 Shader 不在（Named 定義の対象が解決済み集合に無い）
                if (def.Scope == NataneFeatureScope.Named && def.TargetShaders != null)
                {
                    foreach (string target in def.TargetShaders)
                    {
                        if (!resolvedShaderNames.Contains(target))
                        {
                            data.missingTargetShaders.Add($"{def.Keyword} :: {target}");
                        }
                    }
                }

                // (d) Property が Shader Properties に無い定義
                //     単一プロパティで駆動される Toggle/Enum のみ検証（Derived/Composite/Custom は駆動が単一でないため除外）
                if (declaredSomewhere &&
                    !string.IsNullOrEmpty(def.PropertyName) &&
                    (def.EvaluationKind == NataneFeatureEvaluationKind.Toggle ||
                     def.EvaluationKind == NataneFeatureEvaluationKind.Enum))
                {
                    bool foundProperty = declaring.Any(s =>
                        shaderProperties.TryGetValue(s, out var props) && props.Contains(def.PropertyName));
                    if (!foundProperty)
                    {
                        data.missingProperties.Add($"{def.Keyword} :: {def.PropertyName}");
                    }
                }
            }

            data.shaderDependencyFingerprint = ComputeDependencyFingerprint(rootFullPaths);

            return data;
        }

        private static double EstimateVariants(int featureKeywordCount)
        {
            // shader_feature は概ね ON/OFF の 2 状態 → 2^n をバリアント数の粗い上限とする
            if (featureKeywordCount <= 0) return 1d;
            if (featureKeywordCount >= 1023) return double.PositiveInfinity;
            return Math.Pow(2d, featureKeywordCount);
        }

        // ---- 依存ハッシュ ----

        /// <summary>
        /// ルート .shader 群 ＋ 参照 include（.hlsl/.cginc、再帰・循環ガード）の内容 SHA1 合成。
        /// テスト用にルートを外部から渡せるよう公開。
        /// </summary>
        public static string ComputeDependencyFingerprint(IEnumerable<string> rootShaderFullPaths)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var contents = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (string root in rootShaderFullPaths ?? Enumerable.Empty<string>())
            {
                CollectFileRecursive(root, visited, contents);
            }

            var sb = new StringBuilder();
            foreach (var kv in contents)
            {
                // パスは環境依存のため内容ハッシュのみ結合（ファイル境界を区切り記号で保持）
                sb.Append(Sha1Hex(kv.Value)).Append('\n');
            }

            return Sha1Hex(sb.ToString());
        }

        private static void CollectFileRecursive(string fullPath, HashSet<string> visited, SortedDictionary<string, string> contents)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return;
            }

            string normalized;
            try
            {
                normalized = Path.GetFullPath(fullPath);
            }
            catch
            {
                return;
            }

            if (!visited.Add(normalized) || !File.Exists(normalized))
            {
                return; // 循環ガード / 未解決 include は安全側でスキップ
            }

            string source = File.ReadAllText(normalized);
            contents[normalized] = source;

            NataneParsedShaderSource parsed = NataneShaderSourceParser.Parse(source);
            string dir = Path.GetDirectoryName(normalized);
            foreach (string include in parsed.Includes)
            {
                if (Path.IsPathRooted(include))
                {
                    CollectFileRecursive(include, visited, contents);
                    continue;
                }

                string candidate = Path.Combine(dir ?? string.Empty, include);
                CollectFileRecursive(candidate, visited, contents);
            }
        }

        private static string Sha1Hex(string text)
        {
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    hex.Append(b.ToString("x2"));
                }
                return hex.ToString();
            }
        }

        // ---- 保存 / 読込 ----

        public static void Save(NataneShaderAuditData data)
        {
            if (data == null)
            {
                return;
            }

            string dir = Path.GetDirectoryName(AuditPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(AuditPath, JsonUtility.ToJson(data, true));
        }

        public static bool TryLoad(out NataneShaderAuditData data)
        {
            data = null;
            if (!File.Exists(AuditPath))
            {
                return false;
            }

            try
            {
                var loaded = JsonUtility.FromJson<NataneShaderAuditData>(File.ReadAllText(AuditPath));
                if (loaded == null || loaded.schemaVersion != AuditSchemaVersion)
                {
                    return false; // schema 不一致は破棄
                }

                data = loaded;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Natane Shader Audit] 監査結果の読込に失敗しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存済み監査と現状の差分で鮮度を判定。依存ハッシュ / Registry ハッシュ /
        /// packageVersion / Unity バージョンのいずれか不一致で true（要再監査）。
        /// </summary>
        public static bool IsStale()
        {
            if (!TryLoad(out var saved))
            {
                return true;
            }

            string currentRegistry = NataneShaderFeatureRegistry.ComputeRegistryFingerprint();
            string currentPackage = NataneShaderFeatureRegistry.ShaderCompatibilityVersion;
            string currentUnity = Application.unityVersion;
            string currentDependency = ComputeCurrentDependencyFingerprint();

            return saved.registryFingerprint != currentRegistry ||
                   saved.packageVersion != currentPackage ||
                   saved.unityVersion != currentUnity ||
                   saved.shaderDependencyFingerprint != currentDependency;
        }

        public static string ComputeCurrentDependencyFingerprint()
        {
            var roots = new List<string>();
            foreach (string shaderName in NataneShaderCatalog.ShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null) continue;
                string assetPath = AssetDatabase.GetAssetPath(shader);
                if (string.IsNullOrEmpty(assetPath)) continue;
                try
                {
                    string full = Path.GetFullPath(assetPath);
                    if (File.Exists(full)) roots.Add(full);
                }
                catch
                {
                    // 解決不能な Shader はハッシュ対象外（安全側）
                }
            }

            return ComputeDependencyFingerprint(roots);
        }

        // ---- メニュー / バッチ ----

        [MenuItem("Tools/Natane/ビルド最適化 Build Optimization/Shaderアップデート監査 Shader Update Audit", false, 60)]
        public static void RunFromMenu()
        {
            NataneShaderAuditData data = Run();
            Save(data);
            NataneShaderSourceChangeDetector.ClearChangedFlag();
            LogSummary(data);
        }

        // -executeMethod からの実行用（Exit は -quit に委ねる）。
        public static void RunFromBatch()
        {
            NataneShaderAuditData data = Run();
            Save(data);
            LogSummary(data);
            Debug.Log($"AUDIT_UNKNOWN_COUNT={data.unknownKeywords.Count}");
            Debug.Log($"AUDIT_JSON_PATH={AuditPath}");
        }

        private static void LogSummary(NataneShaderAuditData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Natane Shader アップデート監査]");
            sb.AppendLine($"  Package: {data.packageVersion} / Unity: {data.unityVersion} / Registry schema: {data.registrySchemaVersion}");
            sb.AppendLine($"  管理対象キーワード数: {data.managedKeywordCount} / バリアント概算合計: {data.totalVariantEstimate}");
            sb.AppendLine($"  未知キーワード: {data.unknownKeywords.Count} 件");
            sb.AppendLine($"  孤児定義: {data.orphanDefinitions.Count} 件");
            sb.AppendLine($"  対象Shader不在: {data.missingTargetShaders.Count} 件");
            sb.AppendLine($"  Property欠落: {data.missingProperties.Count} 件");
            sb.AppendLine($"  multi_compile(管理外): {data.multiCompileKeywords.Count} 件");
            sb.AppendLine($"  解析不能項目: {data.unparseableItems.Count} 件");
            sb.AppendLine($"  未解決Shader: {data.unresolvedShaders.Count} 件");

            if (data.unknownKeywords.Count > 0)
            {
                sb.AppendLine("  [未知キーワード一覧]");
                foreach (string u in data.unknownKeywords.Take(50)) sb.AppendLine("    " + u);
            }
            if (data.orphanDefinitions.Count > 0)
            {
                sb.AppendLine("  [孤児定義一覧]");
                foreach (string o in data.orphanDefinitions.Take(50)) sb.AppendLine("    " + o);
            }
            if (data.missingProperties.Count > 0)
            {
                sb.AppendLine("  [Property欠落一覧]");
                foreach (string p in data.missingProperties.Take(50)) sb.AppendLine("    " + p);
            }
            if (data.unparseableItems.Count > 0)
            {
                sb.AppendLine("  [解析不能項目一覧]");
                foreach (string p in data.unparseableItems.Take(50)) sb.AppendLine("    " + p);
            }

            Debug.Log(sb.ToString());
        }
    }

    /// <summary>
    /// .shader/.hlsl/.cginc 変更を検出して SessionState フラグを立てるのみ（重い処理はしない）。
    /// 監査本体はユーザー操作/ビルド前フローが明示的に起動する。
    /// </summary>
    public sealed class NataneShaderSourceChangeDetector : AssetPostprocessor
    {
        private const string SessionKey = "NataneToon_ShaderSourcesChangedSinceLastAudit";

        public static bool ShaderSourcesChangedSinceLastAudit => SessionState.GetBool(SessionKey, false);

        public static void ClearChangedFlag()
        {
            SessionState.SetBool(SessionKey, false);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsNataneShaderSource(importedAssets) ||
                ContainsNataneShaderSource(deletedAssets) ||
                ContainsNataneShaderSource(movedAssets))
            {
                SessionState.SetBool(SessionKey, true);
            }
        }

        private static bool ContainsNataneShaderSource(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path) || path.IndexOf("NataneToon", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".cginc", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
