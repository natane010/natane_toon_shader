using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NataneToon.Editor;

namespace NataneToon.Tests.Editor
{
    /// <summary>
    /// 変種間パリティ検査（W3）。
    ///
    /// 既存の <c>NataneShaderUpdateAudit</c> は「<c>#pragma</c> にあるが Registry 未登録」
    /// の方向しか見ていないため、<c>Properties</c> の <c>[Toggle(KEYWORD)]</c> が立てる
    /// キーワードを<b>どのパスもコンパイルしていない</b>状態が素通りしていた。
    /// このテストがその逆方向をゲートにする。
    ///
    /// パース部分は Unity アセットに依存しないため、文字列入力のユニットテストも併せて置く。
    /// </summary>
    public class NataneShaderVariantParityTests
    {
        private const string CompiledShaderSource =
            "Shader \"Natane/ParityFixture\" {\n" +
            "  Properties {\n" +
            "    [Toggle(_LIVE_ONE)] _UseLiveOne (\"Live\", Float) = 0\n" +
            "    [Toggle(_DEAD_ONE)] _UseDeadOne (\"Dead\", Float) = 0\n" +
            "  }\n" +
            "  SubShader {\n" +
            "    Pass {\n" +
            "      Name \"FORWARD_BASE\"\n" +
            "      CGPROGRAM\n" +
            "      #pragma shader_feature_local _LIVE_ONE\n" +
            "      ENDCG\n" +
            "    }\n" +
            "  }\n" +
            "}\n";

        // ValueTuple に依存しないよう、素朴に 1〜2 件を組み立てるヘルパにしている
        // （Unity 2019.4 系での参照アセンブリ差異を避けるため）。
        private static List<NataneParityInput> Fixture(string nameA, string sourceA)
        {
            return new List<NataneParityInput>
            {
                new NataneParityInput { FileName = nameA, Source = sourceA }
            };
        }

        private static List<NataneParityInput> Fixture(string nameA, string sourceA, string nameB, string sourceB)
        {
            return new List<NataneParityInput>
            {
                new NataneParityInput { FileName = nameA, Source = sourceA },
                new NataneParityInput { FileName = nameB, Source = sourceB }
            };
        }

        // ---- パース単体（文字列入力・Unity 非依存）----

        [Test]
        public void ParseToggleKeywords_MapsKeywordToDrivingProperty()
        {
            var toggles = NataneShaderParityChecker.ParseToggleKeywords(CompiledShaderSource);

            Assert.That(toggles.ContainsKey("_LIVE_ONE"), Is.True);
            Assert.That(toggles["_LIVE_ONE"], Is.EqualTo("_UseLiveOne"));
            Assert.That(toggles["_DEAD_ONE"], Is.EqualTo("_UseDeadOne"));
        }

        [Test]
        public void ParseToggleKeywords_IgnoresCommentedDeclarations()
        {
            string src =
                "Properties {\n" +
                "  // [Toggle(_COMMENTED)] _UseCommented (\"x\", Float) = 0\n" +
                "  [Toggle(_REAL)] _UseReal (\"x\", Float) = 0\n" +
                "}\n";

            var toggles = NataneShaderParityChecker.ParseToggleKeywords(src);

            Assert.That(toggles.ContainsKey("_REAL"), Is.True);
            Assert.That(toggles.ContainsKey("_COMMENTED"), Is.False,
                "コメントアウトされた宣言を拾ってはいけない");
        }

        [Test]
        public void FindUncompiledToggleKeywords_DetectsKeywordNoPassCompiles()
        {
            var findings = NataneShaderParityChecker.FindUncompiledToggleKeywords(
                Fixture("Fixture.shader", CompiledShaderSource));

            Assert.That(findings.Count, Is.EqualTo(1));
            Assert.That(findings[0].Keyword, Is.EqualTo("_DEAD_ONE"));
            Assert.That(findings[0].PropertyName, Is.EqualTo("_UseDeadOne"));
            Assert.That(findings[0].Kind, Is.EqualTo(NataneParityKind.UncompiledEverywhere));
            Assert.That(findings[0].Declared, Is.False,
                "架空のキーワードが宣言テーブルに載っているのはおかしい");
        }

        [Test]
        public void FindUncompiledToggleKeywords_SeparatesVariantLocalGaps()
        {
            // 同じキーワードを、片方はコンパイルし、片方はしない構成。
            string without =
                "Shader \"Natane/ParityFixtureB\" {\n" +
                "  Properties {\n" +
                "    [Toggle(_LIVE_ONE)] _UseLiveOne (\"Live\", Float) = 0\n" +
                "  }\n" +
                "  SubShader { Pass { Name \"FORWARD_BASE\" CGPROGRAM ENDCG } }\n" +
                "}\n";

            var findings = NataneShaderParityChecker.FindUncompiledToggleKeywords(
                Fixture("A.shader", CompiledShaderSource, "B.shader", without));

            var variantGap = findings.FirstOrDefault(f => f.FileName == "B.shader" && f.Keyword == "_LIVE_ONE");
            Assert.That(variantGap, Is.Not.Null, "他変種がコンパイルしているキーワードの欠落を検出できていない");
            Assert.That(variantGap.Kind, Is.EqualTo(NataneParityKind.UncompiledInVariant),
                "どこかでコンパイルされているなら『変種内の欠落』に分類されるべき");
        }

        // ---- 実シェーダーに対するゲート（Unity アセット依存）----

        [Test]
        public void Variants_HaveNoUndeclaredUncompiledToggleKeywords()
        {
            List<NataneToonVariantLocator.Entry> entries = NataneToonVariantLocator.Load(out _);
            if (entries.Count == 0)
            {
                Assert.Ignore("Toon バリアントが解決できないためスキップ");
            }

            var inputs = entries.Select(NataneParityInput.From).ToList();
            List<NataneParityFinding> undeclared = NataneShaderParityChecker.FindUndeclared(inputs);

            Assert.That(undeclared, Is.Empty,
                "Properties の [Toggle(KEYWORD)] が立てるキーワードをどのパスもコンパイルしていません。\n" +
                "トグルは何も変えず、誰も読まないキーワードだけがマテリアルへ書き込まれます。\n" +
                "意図的なら NataneShaderParityChecker の宣言テーブルへ理由付きで登録してください。\n" +
                string.Join("\n", undeclared.Select(NataneShaderParityChecker.DescribeFinding)));
        }

        [Test]
        public void ParityDeclarations_AreNotStale()
        {
            List<NataneToonVariantLocator.Entry> entries = NataneToonVariantLocator.Load(out _);
            if (entries.Count == 0)
            {
                Assert.Ignore("Toon バリアントが解決できないためスキップ");
            }

            var inputs = entries.Select(NataneParityInput.From).ToList();
            List<string> stale = NataneShaderParityChecker.FindStaleDeclarations(inputs);

            Assert.That(stale, Is.Empty,
                "宣言テーブルに、既に解消済みの項目が残っています。削除してください:\n" +
                string.Join("\n", stale));
        }
    }
}
