using System.IO;
using System.Linq;
using NUnit.Framework;
using NataneToon.Editor;

namespace NataneToon.Tests.Editor
{
    public class NataneShaderUpdateAuditTests
    {
        // ---- ソース解析ユニット（文字列入力・Unity アセット非依存）----

        [Test]
        public void Parser_IgnoresCommentedPragmas()
        {
            string src =
                "// #pragma shader_feature _LINE_COMMENTED\n" +
                "/* #pragma shader_feature _BLOCK_COMMENTED */\n" +
                "#pragma shader_feature_local _REAL_ONE\n";

            var parsed = NataneShaderSourceParser.Parse(src);

            Assert.That(parsed.ShaderFeatureKeywords, Does.Contain("_REAL_ONE"));
            Assert.That(parsed.ShaderFeatureKeywords, Does.Not.Contain("_LINE_COMMENTED"));
            Assert.That(parsed.ShaderFeatureKeywords, Does.Not.Contain("_BLOCK_COMMENTED"));
        }

        [Test]
        public void Parser_HandlesMultiLinePragma()
        {
            string src = "#pragma shader_feature_local _A \\\n    _B \\\n    _C\n";

            var parsed = NataneShaderSourceParser.Parse(src);

            Assert.That(parsed.ShaderFeatureKeywords, Does.Contain("_A"));
            Assert.That(parsed.ShaderFeatureKeywords, Does.Contain("_B"));
            Assert.That(parsed.ShaderFeatureKeywords, Does.Contain("_C"));
        }

        [Test]
        public void Parser_RecordsBareShaderFeatureAsUnparseable()
        {
            // shader_feature でキーワード無し＝解析不能。
            // multi_compile の組込みショートカット形（fog/instancing 等）は正当なので解析不能にしない。
            string src = "#pragma shader_feature\n#pragma multi_compile_fog\n#pragma multi_compile_instancing\n";

            var parsed = NataneShaderSourceParser.Parse(src);

            Assert.That(parsed.UnparseableLines.Count, Is.EqualTo(1),
                "bare な shader_feature のみ解析不能として記録されるべき");
            Assert.That(parsed.MultiCompileKeywords, Does.Contain("multi_compile_fog"));
            Assert.That(parsed.MultiCompileKeywords, Does.Contain("multi_compile_instancing"));
        }

        [Test]
        public void Parser_ExtractsShaderNamePropertiesIncludeAndMultiCompile()
        {
            string src =
                "Shader \"Natane/Test\" {\n" +
                "  Properties {\n" +
                "    [Toggle(_FOO)] _Foo (\"Foo\", Float) = 0\n" +
                "    _Bar (\"Bar\", Color) = (1,1,1,1)\n" +
                "  }\n" +
                "  SubShader {\n" +
                "    Pass {\n" +
                "      Name \"MAINPASS\"\n" +
                "      CGPROGRAM\n" +
                "      #include \"Foo/Bar.hlsl\"\n" +
                "      #pragma shader_feature_local _FOO\n" +
                "      #pragma multi_compile_fwdbase _MC_ONE\n" +
                "      ENDCG\n" +
                "    }\n" +
                "  }\n" +
                "  Fallback \"Diffuse\"\n" +
                "}\n";

            var parsed = NataneShaderSourceParser.Parse(src);

            Assert.That(parsed.ShaderNames, Does.Contain("Natane/Test"));
            Assert.That(parsed.PropertyNames, Does.Contain("_Foo"));
            Assert.That(parsed.PropertyNames, Does.Contain("_Bar"));
            Assert.That(parsed.Includes, Does.Contain("Foo/Bar.hlsl"));
            Assert.That(parsed.PassNames, Does.Contain("MAINPASS"));
            Assert.That(parsed.ShaderFeatureKeywords, Does.Contain("_FOO"));
            Assert.That(parsed.MultiCompileKeywords, Does.Contain("_MC_ONE"));
            Assert.That(parsed.Fallbacks, Does.Contain("Diffuse"));
        }

        [Test]
        public void DependencyFingerprint_ChangesWithContent()
        {
            string dir = Path.Combine(Path.GetTempPath(), "NataneAuditTest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string shaderPath = Path.Combine(dir, "Temp.shader");

            try
            {
                File.WriteAllText(shaderPath, "Shader \"Temp\" { }\n");
                string a = NataneShaderUpdateAudit.ComputeDependencyFingerprint(new[] { shaderPath });

                File.WriteAllText(shaderPath, "Shader \"Temp\" { /* changed */ }\n");
                string b = NataneShaderUpdateAudit.ComputeDependencyFingerprint(new[] { shaderPath });

                Assert.That(a, Is.Not.Empty);
                Assert.That(b, Is.Not.EqualTo(a), "内容変更で依存ハッシュが変化していません");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* テンポラリ削除失敗は無視 */ }
            }
        }

        // ---- 監査（Unity アセット依存。パッケージ導入済みプロジェクトでのみ有効）----

        [Test]
        public void Audit_HasNoUnknownKeywordsAfterRegistration()
        {
            var data = NataneShaderUpdateAudit.Run();

            // 解決できた Natane シェーダーが 1 つも無い環境ではスキップ（-runTests 不能環境保護）
            if (data.managedKeywordCount == 0)
            {
                Assert.Ignore("Natane シェーダーが解決できないため監査をスキップ");
            }

            Assert.That(data.unknownKeywords, Is.Empty,
                "登録補完後は未知キーワード 0 件であるべき: " + string.Join(", ", data.unknownKeywords));
        }

        [Test]
        public void Audit_HasNoMissingPropertiesForToggleDefinitions()
        {
            var data = NataneShaderUpdateAudit.Run();
            if (data.managedKeywordCount == 0)
            {
                Assert.Ignore("Natane シェーダーが解決できないため監査をスキップ");
            }

            Assert.That(data.missingProperties, Is.Empty,
                "Toggle/Enum 定義の Property が対象 Shader に存在しません: " + string.Join(", ", data.missingProperties));
        }

        [Test]
        public void Audit_SaveLoadRoundTripsWithSchema()
        {
            var data = NataneShaderUpdateAudit.Run();
            NataneShaderUpdateAudit.Save(data);

            Assert.That(NataneShaderUpdateAudit.TryLoad(out var loaded), Is.True);
            Assert.That(loaded.schemaVersion, Is.EqualTo(NataneShaderUpdateAudit.AuditSchemaVersion));
            Assert.That(loaded.registryFingerprint, Is.EqualTo(data.registryFingerprint));
        }
    }
}
