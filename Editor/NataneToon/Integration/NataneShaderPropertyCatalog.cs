using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NataneToon.Editor
{
    /// <summary>
    /// Properties ブロック内の1宣言。
    ///
    /// LeadingLines には直前に置かれていた [Header(...)] / [Space(N)] / 空行 /
    /// コメント行をそのまま保持する。全12バリアントがカスタム ShaderGUI
    /// (CustomEditor "NataneToonShaderGUI") を使うため Header/Space は表示に
    /// 影響しないが、落とすと生成後の .shader が読めなくなり差分も巨大になる。
    /// </summary>
    internal sealed class NatanePropertyDeclaration
    {
        /// <summary>直前の装飾行（[Header] / [Space] / 空行 / コメント）。</summary>
        public List<string> LeadingLines = new List<string>();

        /// <summary>宣言行そのもの（元のインデントを除いたもの）。</summary>
        public string Line;

        /// <summary>プロパティ名（_MainTex など）。</summary>
        public string Name;

        /// <summary>宣言頭の属性群を空白正規化した文字列（[Toggle(_CAUSTICS)] など）。</summary>
        public string Attributes;

        /// <summary>インスペクター表示名。</summary>
        public string Display;

        /// <summary>型（2D / Float / Range(0,1) / Color / Vector など）を空白正規化したもの。</summary>
        public string Type;

        /// <summary>既定値を空白正規化したもの。</summary>
        public string Default;

        /// <summary>
        /// 宣言の同一性キー。生成前後の受け入れテストはこの値の集合一致で判定する。
        /// 表示名まで含めるのは、表示名のズレも実質的なドリフトだから。
        /// </summary>
        public string SignatureKey => $"{Attributes}|{Name}|{Display}|{Type}|{Default}";
    }

    /// <summary>
    /// .shader の Properties ブロックを宣言単位に分解するパーサー。
    ///
    /// NataneShaderSourceParser はプロパティ「名」しか返さないため、生成器が必要とする
    /// 属性・型・既定値・装飾行までを取るこちらを別に用意している。
    /// コメント除去は NataneShaderSourceParser.StripComments を再利用する
    /// （文字列リテラル保護が実装済みのため）。
    /// </summary>
    internal static class NatanePropertyParser
    {
        // 属性群 + 名前 + ("表示名", 型) = 既定値
        private static readonly Regex DeclRegex = new Regex(
            "^(?<attrs>(?:\\[[^\\]]*\\]\\s*)*)(?<name>[_A-Za-z][A-Za-z0-9_]*)\\s*\\(\\s*\"(?<display>[^\"]*)\"\\s*,\\s*(?<type>.+?)\\)\\s*=\\s*(?<default>.+?)\\s*$",
            RegexOptions.Compiled);

        /// <summary>
        /// Properties ブロック本体（外側の波括弧を含まない生テキスト）を宣言列に分解する。
        ///
        /// コメント行は捨てずに次の宣言の LeadingLines へ積む。実測で本体シェーダーの
        /// Properties 内に 29 行のセクションコメント（// ===== Base Settings ===== など）が
        /// あり、これを落とすと生成後の .shader が読めなくなる。
        ///
        /// 宣言として解釈できなかった非空行は unparsed に積む（黙って捨てない）。
        /// </summary>
        public static List<NatanePropertyDeclaration> ParseBlockText(string block, out List<string> unparsed)
        {
            return ParseBlockText(block, out unparsed, out _);
        }

        /// <summary>
        /// <inheritdoc cref="ParseBlockText(string, out List{string})"/>
        ///
        /// trailing には最後の宣言より後ろに残った行を返す。生成後のブロックには
        /// 終了センチネル(// &lt;/auto-generated...&gt;)がここに来るため、
        /// これを捨てるとコメント行数の検査が誤って不合格になる。
        /// </summary>
        public static List<NatanePropertyDeclaration> ParseBlockText(
            string block, out List<string> unparsed, out List<string> trailing)
        {
            var result = new List<NatanePropertyDeclaration>();
            unparsed = new List<string>();
            trailing = new List<string>();
            if (string.IsNullOrEmpty(block)) return result;

            var pending = new List<string>();

            foreach (string rawLine in block.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r').Trim();

                if (line.Length == 0)
                {
                    pending.Add(string.Empty);
                    continue;
                }

                // 生成物のコメント（センチネル・除外理由）は装飾として取り込まない。
                // 取り込むと、次回の生成で「装飾として1回 + 生成器が1回」の二重出力になり、
                // 実行のたびに増えていく（生成が冪等でなくなる）。
                if (IsGeneratedDecoration(line)) continue;

                // コメント行はそのまま装飾として保持する。
                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    pending.Add(line);
                    continue;
                }

                // 行末コメントがあれば解析用に落とす（Line には原文をそのまま残す）。
                string forParse = StripTrailingLineComment(line);

                Match m = DeclRegex.Match(forParse);
                if (m.Success)
                {
                    result.Add(new NatanePropertyDeclaration
                    {
                        LeadingLines = NormalizeLeading(pending, result.Count == 0),
                        Line = line,
                        Name = m.Groups["name"].Value,
                        Attributes = Normalize(m.Groups["attrs"].Value),
                        Display = m.Groups["display"].Value,
                        Type = Normalize(m.Groups["type"].Value),
                        Default = Normalize(m.Groups["default"].Value)
                    });
                    pending = new List<string>();
                    continue;
                }

                // [Header(...)] / [Space(10)] などの装飾行は次の宣言に付ける。
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    pending.Add(line);
                    continue;
                }

                // ここに来るのは想定外。呼び出し側で報告できるよう記録する。
                unparsed.Add(line);
                pending.Add(line);
            }

            // 最後の宣言より後ろに残った行（終了センチネルなど）。
            trailing.AddRange(pending);
            return result;
        }

        /// <summary>
        /// ソース全文（コメント除去前）から Properties ブロックを取り出して分解する。
        /// </summary>
        public static List<NatanePropertyDeclaration> ParseSource(string source, out List<string> unparsed)
        {
            return ParseSource(source, out unparsed, out _);
        }

        /// <inheritdoc cref="ParseSource(string, out List{string})"/>
        public static List<NatanePropertyDeclaration> ParseSource(
            string source, out List<string> unparsed, out List<string> trailing)
        {
            unparsed = new List<string>();
            trailing = new List<string>();
            if (!TryLocatePropertiesBlock(source, out int start, out int length))
                return new List<NatanePropertyDeclaration>();

            return ParseBlockText(source.Substring(start, length), out unparsed, out trailing);
        }

        /// <summary>
        /// 原文における Properties ブロック本体の範囲（外側の波括弧を含まない）を返す。
        ///
        /// StripComments は行コメントを長さごと削除するためインデックスが原文とずれる。
        /// 生成器は原文の該当範囲だけを差し替える必要があるので、
        /// ここではコメント・文字列を読み飛ばしながら原文上で直接走査する。
        /// 型宣言に現れる 2D = "white" {} の波括弧も正しく数える。
        /// </summary>
        public static bool TryLocatePropertiesBlock(string source, out int bodyStart, out int bodyLength)
        {
            bodyStart = 0;
            bodyLength = 0;
            if (string.IsNullOrEmpty(source)) return false;

            int open = FindPropertiesBrace(source);
            if (open < 0) return false;

            int depth = 1;
            int i = open + 1;
            bodyStart = i;

            while (i < source.Length && depth > 0)
            {
                char c = source[i];

                if (c == '"')
                {
                    i = SkipString(source, i);
                    continue;
                }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                    i += 2;
                    continue;
                }

                if (c == '{') depth++;
                else if (c == '}') depth--;
                i++;
            }

            if (depth != 0) return false;

            bodyLength = (i - 1) - bodyStart;
            return bodyLength >= 0;
        }

        /// <summary>Properties キーワードに続く '{' の位置を、コメント/文字列を避けつつ探す。</summary>
        private static int FindPropertiesBrace(string source)
        {
            foreach (Match m in Regex.Matches(source, "\\bProperties\\b"))
            {
                if (IsInsideCommentOrString(source, m.Index)) continue;

                int i = m.Index + m.Length;
                while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
                if (i < source.Length && source[i] == '{') return i;
            }
            return -1;
        }

        private static bool IsInsideCommentOrString(string source, int index)
        {
            bool inString = false;
            int i = 0;
            while (i < index && i < source.Length)
            {
                char c = source[i];
                if (inString)
                {
                    if (c == '\\') { i += 2; continue; }
                    if (c == '"') inString = false;
                    i++;
                    continue;
                }
                if (c == '"') { inString = true; i++; continue; }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    if (i > index) return true;
                    continue;
                }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                    i += 2;
                    if (i > index) return true;
                    continue;
                }
                i++;
            }
            return inString;
        }

        private static int SkipString(string source, int i)
        {
            i++; // 開き "
            while (i < source.Length)
            {
                if (source[i] == '\\') { i += 2; continue; }
                if (source[i] == '"') return i + 1;
                i++;
            }
            return i;
        }

        /// <summary>
        /// 生成器が書き込んだセンチネル行かどうか。
        /// 目印文字列を含む行だけを対象にしているので、説明文を変えても判定は壊れない。
        /// </summary>
        public static bool IsGeneratedSentinel(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            return line.IndexOf(NataneShaderPropertyWriter.BeginMarker, StringComparison.Ordinal) >= 0
                || line.IndexOf(NataneShaderPropertyWriter.EndMarker, StringComparison.Ordinal) >= 0;
        }

        // 例: "// _SOFT_FILTER, _KUWAHARA_FILTER removed (Lite variant: no GrabPass)"
        private static readonly Regex RemovalCommentRegex = new Regex(
            @"^//\s*_[A-Z0-9_]+(?:\s*,\s*_[A-Z0-9_]+)*\s+removed\s*\(",
            RegexOptions.Compiled);

        /// <summary>
        /// 生成器が出力するコメント行かどうか（センチネルと除外理由）。
        ///
        /// 除外理由コメントは採否グループから生成し直すため、装飾としても保持すると
        /// 二重に出力される。実際 Lite 系と Ghost で 1 回あたり +5 行ずつ増えていた。
        /// </summary>
        public static bool IsGeneratedDecoration(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (IsGeneratedSentinel(line)) return true;
            return RemovalCommentRegex.IsMatch(line.TrimStart());
        }

        /// <summary>引用符の外にある "//" 以降を落とす。</summary>
        private static string StripTrailingLineComment(string line)
        {
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    return line.Substring(0, i).TrimEnd();
            }
            return line;
        }

        /// <summary>
        /// 装飾行を正規化する。
        ///
        /// 空行はセクション区切りとして意味を持つので**残す**。当初これを落としていたため、
        /// 生成すると Lite 1本だけで 59 行の空行が消え、差分が肥大化した
        /// （宣言は失われないので受け入れテストは通ってしまう類の劣化）。
        ///
        /// 連続した空行は1行に潰し、ブロック先頭（センチネル直後）の空行は落とす。
        /// </summary>
        private static List<string> NormalizeLeading(List<string> lines, bool isFirstDeclaration)
        {
            var normalized = new List<string>();
            bool previousWasBlank = false;

            foreach (string line in lines)
            {
                if (line.Length == 0)
                {
                    if (previousWasBlank) continue;
                    if (normalized.Count == 0 && isFirstDeclaration) continue;
                    normalized.Add(string.Empty);
                    previousWasBlank = true;
                    continue;
                }

                normalized.Add(line);
                previousWasBlank = false;
            }

            return normalized;
        }

        private static string Normalize(string s)
        {
            return Regex.Replace(s ?? string.Empty, "\\s+", string.Empty);
        }
    }

    /// <summary>
    /// 1バリアント分の解析結果。
    /// </summary>
    internal sealed class NataneShaderPropertySet
    {
        public string FileName;
        public string AssetPath;
        public List<NatanePropertyDeclaration> Declarations = new List<NatanePropertyDeclaration>();
        public List<string> Unparsed = new List<string>();

        /// <summary>原文の Properties ブロックに含まれるコメント行数。</summary>
        public int SourceCommentLines;

        /// <summary>最後の宣言より後ろに残った行（生成後の終了センチネルなど）。</summary>
        public List<string> TrailingLines = new List<string>();

        /// <summary>
        /// 解析結果に保持できたコメント行数。
        /// 末尾の行も数える。生成済みブロックでは終了センチネルがそこに来るため、
        /// 数え漏らすと検査が誤って不合格になる。
        /// </summary>
        public int PreservedCommentLines =>
            Declarations.Sum(d => d.LeadingLines.Count(IsComment)) + TrailingLines.Count(IsComment);

        private static bool IsComment(string line) =>
            line.TrimStart().StartsWith("//", StringComparison.Ordinal);

        public IEnumerable<string> Names => Declarations.Select(d => d.Name);

        public Dictionary<string, NatanePropertyDeclaration> ByName()
        {
            var map = new Dictionary<string, NatanePropertyDeclaration>(StringComparer.Ordinal);
            foreach (NatanePropertyDeclaration d in Declarations)
                map[d.Name] = d; // 同名重複時は後勝ち（重複自体は監査で別途報告する）
            return map;
        }
    }

    /// <summary>
    /// 正準カタログのメンバーシップ群。
    /// 「どのプロパティを、どのバリアントが持つか」を、プロパティ単位ではなく
    /// 「同じ採否パターンを共有するグループ」単位で保持する。
    ///
    /// 実測（12バリアント・和集合991プロパティ）では、採否パターンは 11 種類しか
    /// 存在しなかった。したがってカタログは
    ///   「正準順の宣言リスト + 11グループの採否 + 少数の宣言上書き」
    /// で表現でき、人間が読める規模に収まる。
    /// </summary>
    internal sealed class NatanePropertyGroup
    {
        /// <summary>グループ識別子（CORE / NOT_BACKGROUND / FUR_ONLY など）。</summary>
        public string Id;

        /// <summary>このグループのプロパティを持つバリアントのファイル名集合。</summary>
        public HashSet<string> Members = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>このグループに属するプロパティ名（正準順）。</summary>
        public List<string> PropertyNames = new List<string>();

        public bool IsUniversal(int totalShaders) => Members.Count >= totalShaders;
    }

    /// <summary>
    /// 宣言が一部バリアントだけ異なる場合の上書き。
    /// 実測では 991 件中 4 件のみ（_ZWrite / _UseLightVolume は意図的、
    /// _GlitchIntensity / _GlitchRGBSplitIntensity は取り残しによるドリフト）。
    /// </summary>
    internal sealed class NatanePropertyOverride
    {
        public string PropertyName;
        public HashSet<string> Shaders = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 上書き後の宣言。Line だけでなく解析済みフィールドも保持する
        /// （受け入れテストは SignatureKey の一致で判定するため、
        ///  ここが既定側の値のままだと上書きが検証をすり抜ける）。
        /// </summary>
        public NatanePropertyDeclaration Declaration;
    }

    /// <summary>
    /// 正準カタログ本体。生成器と検証器の双方がこれを参照する。
    /// </summary>
    internal sealed class NataneShaderPropertyCatalog
    {
        /// <summary>バリアントのファイル名（正準順）。</summary>
        public List<string> Shaders = new List<string>();

        /// <summary>正準順の宣言（既定の宣言内容）。</summary>
        public List<NatanePropertyDeclaration> Declarations = new List<NatanePropertyDeclaration>();

        /// <summary>採否グループ。</summary>
        public List<NatanePropertyGroup> Groups = new List<NatanePropertyGroup>();

        /// <summary>宣言の部分上書き。</summary>
        public List<NatanePropertyOverride> Overrides = new List<NatanePropertyOverride>();

        /// <summary>
        /// シェーダー名 → (プロパティ名 → そのシェーダー自身の装飾行)。
        ///
        /// 装飾行(セクションコメント/[Header]/空行)は正準化の対象にしない。
        /// 正準化が必要なのは「宣言の集合と順序」であって見出しの文言ではないうえ、
        /// バリアントごとに書き方が違う。実際 Background は全機能にセクション見出しを
        /// 付けており、本体の装飾で上書きすると固有の見出しが 3 行失われた。
        /// 記録が無いプロパティ(新規追加など)は正準側の装飾を使う。
        /// </summary>
        public Dictionary<string, Dictionary<string, List<string>>> DecorationByShader =
            new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

        /// <summary>指定シェーダーにおけるそのプロパティの装飾行を返す。</summary>
        public List<string> DecorationFor(string shaderFileName, string propertyName, List<string> canonical)
        {
            if (DecorationByShader.TryGetValue(shaderFileName, out var perProperty) &&
                perProperty.TryGetValue(propertyName, out List<string> own))
            {
                return own;
            }
            return canonical;
        }

        /// <summary>プロパティ名 → 所属グループ。</summary>
        public Dictionary<string, NatanePropertyGroup> GroupOf()
        {
            var map = new Dictionary<string, NatanePropertyGroup>(StringComparer.Ordinal);
            foreach (NatanePropertyGroup g in Groups)
                foreach (string name in g.PropertyNames)
                    map[name] = g;
            return map;
        }

        /// <summary>
        /// 指定バリアントが持つべき宣言列を、正準順で返す。
        /// これが .shader へ書き出す内容そのものになる。
        /// </summary>
        public List<NatanePropertyDeclaration> BuildFor(string shaderFileName)
        {
            Dictionary<string, NatanePropertyGroup> groupOf = GroupOf();

            var overrideOf = new Dictionary<string, NatanePropertyOverride>(StringComparer.Ordinal);
            foreach (NatanePropertyOverride o in Overrides)
                if (o.Shaders.Contains(shaderFileName))
                    overrideOf[o.PropertyName] = o;

            var result = new List<NatanePropertyDeclaration>();
            foreach (NatanePropertyDeclaration d in Declarations)
            {
                if (!groupOf.TryGetValue(d.Name, out NatanePropertyGroup g)) continue;
                if (!g.Members.Contains(shaderFileName)) continue;

                if (overrideOf.TryGetValue(d.Name, out NatanePropertyOverride ov) && ov.Declaration != null)
                {
                    // 装飾行は正準側（並び順を担う）、宣言内容は上書き側を採用する。
                    result.Add(new NatanePropertyDeclaration
                    {
                        LeadingLines = d.LeadingLines,
                        Line = ov.Declaration.Line,
                        Name = ov.Declaration.Name,
                        Attributes = ov.Declaration.Attributes,
                        Display = ov.Declaration.Display,
                        Type = ov.Declaration.Type,
                        Default = ov.Declaration.Default
                    });
                    continue;
                }

                result.Add(d);
            }

            return result;
        }

        /// <summary>
        /// Properties ブロックの本文テキストを生成する（外側の波括弧・センチネルは含まない）。
        /// </summary>
        public string RenderBody(string shaderFileName, string indent)
        {
            var sb = new StringBuilder();
            foreach (NatanePropertyDeclaration d in BuildFor(shaderFileName))
            {
                foreach (string lead in d.LeadingLines)
                    sb.AppendLine(lead.Length == 0 ? string.Empty : indent + lead);
                sb.AppendLine(indent + d.Line);
            }
            return sb.ToString();
        }
    }
}
