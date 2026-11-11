using System.Linq;
using NUnit.Framework;
using NataneToon.Editor;

namespace NataneToon.Tests.Editor
{
    public class NataneShaderFeatureRegistryTests
    {
        [Test]
        public void Definitions_HaveUniqueKeywords()
        {
            string[] duplicates = NataneShaderFeatureRegistry.AllDefinitions
                .GroupBy(d => d.Keyword)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            Assert.That(duplicates, Is.Empty, "Registry にキーワード重複があります");
        }

        [Test]
        public void KeywordMappings_AreAllRegistered()
        {
            // 既存 KeywordMappings が漏れなく Registry に取り込まれていること（後方互換）。
            string[] missing = NataneShaderKeywordSynchronizer.KeywordMappings
                .Select(m => m.keyword)
                .Distinct()
                .Where(kw => !NataneShaderFeatureRegistry.IsKnownKeyword(kw))
                .ToArray();

            Assert.That(missing, Is.Empty, "KeywordMappings の一部が Registry 未登録です");
        }

        [Test]
        public void GetScopeForShader_MapsSpecialShaders()
        {
            Assert.That(NataneShaderFeatureRegistry.GetScopeForShader("Natane/Toon Shader (Particle)"),
                Is.EqualTo(NataneFeatureScope.Particle));
            Assert.That(NataneShaderFeatureRegistry.GetScopeForShader("Natane/Toon Shader Wirelight"),
                Is.EqualTo(NataneFeatureScope.Wirelight));
            Assert.That(NataneShaderFeatureRegistry.GetScopeForShader("Natane/Eye"),
                Is.EqualTo(NataneFeatureScope.Eye));
            Assert.That(NataneShaderFeatureRegistry.GetScopeForShader("Natane/Toon Shader (Background)"),
                Is.EqualTo(NataneFeatureScope.Background));
            Assert.That(NataneShaderFeatureRegistry.GetScopeForShader("Natane/Toon Shader"),
                Is.EqualTo(NataneFeatureScope.CoreFamily));
        }

        [Test]
        public void NamedDefinitions_TargetShadersExistInCatalog()
        {
            foreach (var def in NataneShaderFeatureRegistry.AllDefinitions
                         .Where(d => d.Scope == NataneFeatureScope.Named && d.TargetShaders != null))
            {
                foreach (string target in def.TargetShaders)
                {
                    Assert.That(NataneShaderCatalog.IsNataneShader(target), Is.True,
                        $"Named 定義 {def.Keyword} の対象 Shader {target} が Catalog に存在しません");
                }
            }
        }

        [Test]
        public void ParticleKeywords_AreScopedToParticle()
        {
            string[] particleKeywords =
            {
                "_PARTICLE_TOON_LIGHTING", "_SOFT_PARTICLES", "_FLIPBOOK_BLENDING", "_CAMERA_FADE"
            };

            foreach (string kw in particleKeywords)
            {
                Assert.That(NataneShaderFeatureRegistry.TryGetByKeyword(kw, out var def), Is.True, $"{kw} 未登録");
                Assert.That(def.Scope, Is.EqualTo(NataneFeatureScope.Particle), $"{kw} の Scope が Particle ではありません");
            }
        }

        [Test]
        public void WirelightKeywords_AreScopedToWirelight()
        {
            string[] wirelightKeywords =
            {
                "_WIRELIGHT", "_USE_VERTEX_COLOR_POS", "_CYBER_SCANLINE", "_CYBER_CHROMA",
                "_CYBER_GLITCH", "_CYBER_DATASTREAM", "_WL_DISTANCE_FADE"
            };

            foreach (string kw in wirelightKeywords)
            {
                Assert.That(NataneShaderFeatureRegistry.TryGetByKeyword(kw, out var def), Is.True, $"{kw} 未登録");
                Assert.That(def.Scope, Is.EqualTo(NataneFeatureScope.Wirelight), $"{kw} の Scope が Wirelight ではありません");
            }
        }

        [Test]
        public void PbrLike_IsDerivedAndNotSafeStrippable()
        {
            Assert.That(NataneShaderFeatureRegistry.TryGetByKeyword("_PBR_LIKE", out var def), Is.True);
            Assert.That(def.EvaluationKind, Is.EqualTo(NataneFeatureEvaluationKind.Derived));
            Assert.That(def.SafeStrippable, Is.False, "_PBR_LIKE は自動判定未確立のため SafeStrippable=false であるべき");
            Assert.That(def.AlwaysKeep, Is.True, "_PBR_LIKE は AlwaysKeep=true であるべき");
        }

        [Test]
        public void Fingerprint_IsDeterministicAndNonEmpty()
        {
            string a = NataneShaderFeatureRegistry.ComputeRegistryFingerprint();
            string b = NataneShaderFeatureRegistry.ComputeRegistryFingerprint();

            Assert.That(a, Is.Not.Null.And.Not.Empty);
            Assert.That(a, Is.EqualTo(b), "同一定義で fingerprint が安定していません");
        }
    }
}
