using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NataneToon.Editor
{
    /// <summary>
    /// パリティ検査の1件。ファイル・キーワード・駆動プロパティを保持する。
    /// </summary>
    internal sealed class NataneParityFinding
    {
        public string FileName;
        public string PropertyName;
        public string Keyword;
        public NataneParityKind Kind;

        /// <summary>宣言テーブルで受理済みか（受理済みは Info として扱い、失敗させない）。</summary>
        public bool Declared;

        /// <summary>受理理由（Declared のときのみ）。</summary>
        public string DeclaredReason;
    }

    /// <summary>
    /// 検査対象1ファイル分の入力。AssetDatabase に依存させないための最小の器で、
    /// <see cref="NataneToonVariantLocator.Entry"/> からも生ソース文字列からも作れる。
    /// </summary>
    internal sealed class NataneParityInput
    {
        public string FileName;
        public string Source;

        public static NataneParityInput From(NataneToonVariantLocator.Entry entry)
        {
            return new NataneParityInput { FileName = entry.FileName, Source = entry.Source };
        }
    }

    internal enum NataneParityKind
    {
        /// <summary>
        /// どの変種のどのパスもコンパイルしていないキーワード。
        /// マテリアルには死んだキーワードが書き込まれ、インスペクタのトグルは何も変えない。
        /// </summary>
        UncompiledEverywhere,

        /// <summary>
        /// 他の変種ではコンパイルされているのに、この変種だけコンパイルしていないキーワード。
        /// 「その変種に載せない」という設計判断か、pragma の書き忘れかを区別する必要がある。
        /// </summary>
        UncompiledInVariant
    }

    /// <summary>
    /// 変種間パリティ検査。<see cref="NataneShaderConsistencyAudit"/> が見ていなかった
    /// 「逆方向」——<c>Properties</c> の <c>[Toggle(KEYWORD)]</c> が立てるキーワードを
    /// どのパスもコンパイルしていない状態——を検出する。
    ///
    /// 既存の監査は「<c>#pragma</c> にあるが Registry 未登録」の方向しか見ていないため、
    /// この状態は素通りしていた。実際 <c>_DISSOLVE_MASK</c> / <c>_AUDIOLINK_DISSOLVE</c> は
    /// 12 変種すべてで宣言されているのに、どのパスにも <c>#pragma</c> が無い。
    ///
    /// パースは文字列処理だけで完結し、AssetDatabase に触れない。
    /// そのため Unity 非依存の CI スクリプト（<c>Tests/CI/check_shader_parity.py</c>）と
    /// 同じ判定をこのクラスと EditMode テストの双方から実行できる。
    /// 本検査はファイルを一切書き換えない。検出と記録のみ。
    /// </summary>
    internal static class NataneShaderParityChecker
    {
        // [Toggle(_KEYWORD)] _PropertyName ("Label", Float) = 0
        // [NoScaleOffset][Toggle(_KEYWORD)] のように属性が連なる形も拾えるよう、
        // Toggle 属性とプロパティ名を分けて解析する。
        private static readonly Regex ToggleAttributeRegex = new Regex(
            @"\[\s*Toggle\s*\(\s*(?<keyword>[_A-Za-z][A-Za-z0-9_]*)\s*\)\s*\]",
            RegexOptions.Compiled);

        private static readonly Regex PropertyNameRegex = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*(?<name>[_A-Za-z][A-Za-z0-9_]*)\s*\(",
            RegexOptions.Compiled);

        // ---- 宣言テーブル ----

        /// <summary>
        /// 「どの変種もコンパイルしないキーワード」として受理済みのもの。
        ///
        /// これらは <c>[Toggle(KEYWORD)]</c> でキーワードを宣言しているが、HLSL 側は
        /// 対応するマスクテクスチャを常時サンプルする実装になっており（既定値が
        /// <c>"white"</c> / <c>"gray"</c> なので絵は正しい）、キーワードは誰も読まない。
        ///
        /// これは v1.6.5 時点で既に存在する負債であり、本検査で新規に発見したもの。
        /// トグルを実際に効かせる（Uniform 分岐を入れる）修正は、既にマスクを設定して
        /// いるマテリアルの見た目を変えるため、機能ごとに個別判断が要る。
        /// ここに列挙してあるものは「既知・受理済み」として扱い、
        /// <b>新しく増えたものだけを失敗させる</b>。
        /// </summary>
        private static readonly Dictionary<string, string> KnownUncompiledEverywhere =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // --- 追加テクスチャのマスク（HLSL は常時サンプル）---
                { "_2ND_TEX_MASK", "HLSL は _2ndTexMask を常時サンプルする。既定 white で無害。" },
                { "_3RD_TEX_MASK", "HLSL は _3rdTexMask を常時サンプルする。既定 white で無害。" },
                { "_4TH_TEX_MASK", "HLSL は _4thTexMask を常時サンプルする。既定 white で無害。" },
                { "_5TH_TEX_MASK", "HLSL は _5thTexMask を常時サンプルする。既定 white で無害。" },

                // --- AudioLink（実装は各 *Intensity プロパティを直接見ている）---
                { "_AUDIOLINK_CHRONOTENSITY", "実装は _AudioLinkChronotensity* を直接参照する。" },
                { "_AUDIOLINK_EMISSION", "実装は _AudioLinkEmissionIntensity を直接参照する。" },
                { "_AUDIOLINK_HUE_SHIFT", "実装は _AudioLinkHueShiftIntensity を直接参照する。" },
                { "_AUDIOLINK_OUTLINE", "実装は _AudioLinkOutlineIntensity を直接参照する。" },
                { "_AUDIOLINK_RIM", "実装は _AudioLinkRimIntensity を直接参照する。" },

                // --- 各種マスク（HLSL は常時サンプル）---
                { "_DRIP_MASK", "HLSL は _WaterDripMask を常時サンプルする。" },
                { "_EMISSION_MASK", "HLSL は _EmissionMask を常時サンプルする。" },
                { "_ENV_RIM_MASK", "HLSL は _EnvRimMask を常時サンプルする。" },
                { "_GLITTER_MASK", "HLSL は _GlitterMask を常時サンプルする。" },
                { "_HOLOGRAM_MASK", "HLSL は _HologramMask を常時サンプルする。" },
                { "_IRIDESCENCE_MASK", "HLSL は _IridescenceMask を常時サンプルする。" },
                { "_MATCAP_MASK", "HLSL は _MatCapMask を常時サンプルする。" },
                { "_MATCAP_MASK_2", "HLSL は _MatCapMask2 を常時サンプルする。" },
                { "_MATCAP_MASK_3", "HLSL は _MatCapMask3 を常時サンプルする。" },
                { "_REFLECTION_MASK", "HLSL は _ReflectionMask を常時サンプルする。" },
                { "_REFRACTION_MASK", "HLSL は _RefractionMask を常時サンプルする。" },
                { "_RIM_MASK", "HLSL は _RimMask を常時サンプルする。" },
                { "_RIM_MASK_2", "HLSL は _RimMask2 を常時サンプルする。" },
                { "_SHADOW_COLOR_TEX", "HLSL は _ShadowColorTex を常時サンプルする。" },
                { "_SPECULAR_MASK", "HLSL は _SpecularMask を常時サンプルする。" },
                { "_SSS_MASK", "HLSL は _SSSMask を常時サンプルする。" },
                { "_THICKNESS_MAP", "HLSL は _ThicknessMap を常時サンプルする。" },
                { "_VERTEX_ANIM_MASK", "HLSL は _VertexAnimMask を常時サンプルする。" },

                // --- エミッションのアニメーション（実装は速度/強度プロパティを直接見ている）---
                { "_EMISSION_PULSE", "実装は _EmissionPulseSpeed を直接参照する。" },
                { "_EMISSION_SCROLL", "実装は _EmissionScrollSpeed を直接参照する。" },

                // --- リムライトの方向制御 ---
                { "_RIM_DIRECTION_CONTROL", "実装は _RimDirection* を直接参照する。" }
            };

        /// <summary>
        /// 「この変種だけコンパイルしない」ことが設計上正しいもの。<c>ファイル名 :: キーワード</c>。
        /// 変種がそのパス自体を持たない場合、pragma を書いても無意味なので宣言しないのが正しい。
        /// </summary>
        private static readonly Dictionary<string, string> KnownUncompiledInVariant =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Ghost は OUTLINE パスを持たない（半透明の霊体表現で輪郭線が破綻するため）。
                // Properties 側は他変種との互換のために残してある。
                { "NataneToonShader_Ghost.shader :: _OUTLINE", "Ghost は OUTLINE パスを持たない。" },
                { "NataneToonShader_Ghost.shader :: _OUTLINE_HAND_DRAWN", "Ghost は OUTLINE パスを持たない。" },
                { "NataneToonShader_Ghost.shader :: _OUTLINE_MASK", "Ghost は OUTLINE パスを持たない。" },
                { "NataneToonShader_Ghost.shader :: _OUTLINE_MULTI_COLOR", "Ghost は OUTLINE パスを持たない。" },
                { "NataneToonShader_Ghost.shader :: _OUTLINE_TEXTURE_COLOR", "Ghost は OUTLINE パスを持たない。" },
                { "NataneToonShader_Ghost.shader :: _OUTLINE_WIDTH_MAP", "Ghost は OUTLINE パスを持たない。" },

                // Lite 系と Ghost はテッセレーションを載せない（Quest / 軽量化方針）。
                { "NataneToonShader_Ghost.shader :: _TESSELLATION", "Ghost はテッセレーション非対応。" },
                { "NataneToonShader_Ghost.shader :: _TESS_DISPLACEMENT", "Ghost はテッセレーション非対応。" },
                { "NataneToonShader_Lite.shader :: _TESSELLATION", "Lite はテッセレーション非対応。" },
                { "NataneToonShader_Lite.shader :: _TESS_DISPLACEMENT", "Lite はテッセレーション非対応。" },
                { "NataneToonShader_Cutout_Lite.shader :: _TESSELLATION", "Lite はテッセレーション非対応。" },
                { "NataneToonShader_Cutout_Lite.shader :: _TESS_DISPLACEMENT", "Lite はテッセレーション非対応。" },
                { "NataneToonShader_Fur_Lite.shader :: _TESSELLATION", "Lite はテッセレーション非対応。" },
                { "NataneToonShader_Fur_Lite.shader :: _TESS_DISPLACEMENT", "Lite はテッセレーション非対応。" },
                { "NataneToonShader_Transparent_Lite.shader :: _TESSELLATION", "Lite はテッセレーション非対応。" },
                { "NataneToonShader_Transparent_Lite.shader :: _TESS_DISPLACEMENT", "Lite はテッセレーション非対応。" }
            };

        // ---- 解析 ----

        /// <summary>
        /// <c>Properties</c> ブロックの <c>[Toggle(KEYWORD)]</c> を「キーワード → プロパティ名」で返す。
        /// コメント除去済みソースを前提とせず、内部で除去する。
        /// </summary>
        public static Dictionary<string, string> ParseToggleKeywords(string source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(source))
            {
                return result;
            }

            string cleaned = NataneShaderSourceParser.StripComments(source);

            foreach (string rawLine in cleaned.Split('\n'))
            {
                Match toggle = ToggleAttributeRegex.Match(rawLine);
                if (!toggle.Success)
                {
                    continue;
                }

                Match prop = PropertyNameRegex.Match(rawLine);
                string propertyName = prop.Success ? prop.Groups["name"].Value : null;

                // 同一キーワードを複数プロパティが立てることは無い前提。
                // 万一衝突しても最初の宣言を保持する（報告の安定性を優先）。
                string keyword = toggle.Groups["keyword"].Value;
                if (!result.ContainsKey(keyword))
                {
                    result[keyword] = propertyName;
                }
            }

            return result;
        }

        /// <summary>
        /// そのファイル内で <c>#pragma shader_feature*</c> によりコンパイルされるキーワード集合。
        /// パスをまたいだ和集合。1 つでもパスが宣言していれば「コンパイルされている」とみなす。
        /// </summary>
        public static HashSet<string> ParseCompiledKeywords(string source)
        {
            NataneParsedShaderSource parsed = NataneShaderSourceParser.Parse(source);
            return new HashSet<string>(parsed.ShaderFeatureKeywords, StringComparer.Ordinal);
        }

        // ---- 検査本体 ----

        /// <summary>
        /// 変種一式を突き合わせ、未コンパイルの Toggle キーワードを列挙する。
        /// 宣言テーブルで受理済みのものも <see cref="NataneParityFinding.Declared"/> を立てて返す
        /// （呼び出し側が Info として出せるようにするため。握り潰さない）。
        /// </summary>
        public static List<NataneParityFinding> FindUncompiledToggleKeywords(
            IReadOnlyList<NataneParityInput> entries)
        {
            var findings = new List<NataneParityFinding>();
            if (entries == null || entries.Count == 0)
            {
                return findings;
            }

            // ファイル名 -> ( toggle キーワード -> プロパティ名 )
            var togglesByFile = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            // ファイル名 -> コンパイルされるキーワード
            var compiledByFile = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            // どこか 1 つでもコンパイルされているキーワードの和集合
            var compiledAnywhere = new HashSet<string>(StringComparer.Ordinal);

            foreach (NataneParityInput e in entries)
            {
                togglesByFile[e.FileName] = ParseToggleKeywords(e.Source);
                HashSet<string> compiled = ParseCompiledKeywords(e.Source);
                compiledByFile[e.FileName] = compiled;
                compiledAnywhere.UnionWith(compiled);
            }

            foreach (NataneParityInput e in entries)
            {
                Dictionary<string, string> toggles = togglesByFile[e.FileName];
                HashSet<string> compiled = compiledByFile[e.FileName];

                foreach (var kv in toggles.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    string keyword = kv.Key;
                    if (compiled.Contains(keyword))
                    {
                        continue;
                    }

                    bool anywhere = compiledAnywhere.Contains(keyword);
                    var finding = new NataneParityFinding
                    {
                        FileName = e.FileName,
                        PropertyName = kv.Value,
                        Keyword = keyword,
                        Kind = anywhere
                            ? NataneParityKind.UncompiledInVariant
                            : NataneParityKind.UncompiledEverywhere
                    };

                    if (anywhere)
                    {
                        string key = e.FileName + " :: " + keyword;
                        if (KnownUncompiledInVariant.TryGetValue(key, out string reason))
                        {
                            finding.Declared = true;
                            finding.DeclaredReason = reason;
                        }
                    }
                    else if (KnownUncompiledEverywhere.TryGetValue(keyword, out string globalReason))
                    {
                        finding.Declared = true;
                        finding.DeclaredReason = globalReason;
                    }

                    findings.Add(finding);
                }
            }

            return findings;
        }

        /// <summary>
        /// 未宣言（＝新規に混入した）検出のみ。テストと CI はこれが空であることを要求する。
        /// </summary>
        public static List<NataneParityFinding> FindUndeclared(
            IReadOnlyList<NataneParityInput> entries)
        {
            return FindUncompiledToggleKeywords(entries).Where(f => !f.Declared).ToList();
        }

        /// <summary>
        /// 宣言テーブルにあるのに実際には検出されなくなったもの（＝修正済みなので消してよい行）。
        /// 宣言テーブルが腐って肥大するのを防ぐ。
        /// </summary>
        public static List<string> FindStaleDeclarations(
            IReadOnlyList<NataneParityInput> entries)
        {
            var actual = FindUncompiledToggleKeywords(entries);

            var actualGlobal = new HashSet<string>(
                actual.Where(f => f.Kind == NataneParityKind.UncompiledEverywhere).Select(f => f.Keyword),
                StringComparer.Ordinal);
            var actualVariant = new HashSet<string>(
                actual.Where(f => f.Kind == NataneParityKind.UncompiledInVariant)
                      .Select(f => f.FileName + " :: " + f.Keyword),
                StringComparer.Ordinal);

            var stale = new List<string>();
            foreach (string kw in KnownUncompiledEverywhere.Keys)
            {
                if (!actualGlobal.Contains(kw))
                {
                    stale.Add(kw);
                }
            }
            foreach (string key in KnownUncompiledInVariant.Keys)
            {
                if (!actualVariant.Contains(key))
                {
                    stale.Add(key);
                }
            }

            stale.Sort(StringComparer.Ordinal);
            return stale;
        }

        public static string DescribeFinding(NataneParityFinding f)
        {
            string property = string.IsNullOrEmpty(f.PropertyName) ? "(プロパティ名不明)" : f.PropertyName;
            string kind = f.Kind == NataneParityKind.UncompiledEverywhere
                ? "どの変種のどのパスもコンパイルしていない"
                : "他変種ではコンパイルされているがこの変種には無い";
            return $"{f.FileName} :: [Toggle({f.Keyword})] {property} — {kind}";
        }
    }
}
