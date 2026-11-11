using NUnit.Framework;
using UnityEngine;

namespace NataneToon.Editor.Tests
{
    [TestFixture]
    public class NataneToonSamplerBudgetEstimatorTests
    {
        private Material material;

        [SetUp]
        public void SetUp()
        {
            // Unity 2021.2以降、シェーダーに宣言されていないキーワードは
            // EnableKeyword しても IsKeywordEnabled が false を返すため、
            // Natane 本体シェーダー(全 shader_feature 宣言済み)を使う。
            Shader shader = Shader.Find("Natane/Toon Shader");
            Assert.That(shader, Is.Not.Null, "Natane/Toon Shader could not be found.");
            material = new Material(shader);
        }

        [TearDown]
        public void TearDown()
        {
            if (material != null)
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Estimate_WithDangerousLightingCombo_AddsBothReserves()
        {
            EnableKeywords(
                "_2ND_TEXTURE",
                "_3RD_TEXTURE",
                "_COLOR_QUANTIZE",
                "_HAIR_SPECULAR",
                "_HATCHING",
                "_IRIDESCENCE",
                "_LTCGI",
                "_NORMALMAP",
                "_RIM_LIGHT",
                "_SCREEN_TONE",
                "_USE_LIGHT_VOLUME");

            var estimate = NataneToonSamplerBudgetEstimator.Estimate(material);

            // Current cost model: NOSAMPLER sharing makes the mask-style features
            // free; only LV(1) + LTCGI(1) + combo reserve(3) + critical reserve(1)
            // count on top of the base. Base(3) + 6 = 9.
            Assert.That(estimate.HasLightVolumeLtcgiCombo, Is.True);
            Assert.That(estimate.HasCriticalLightingCombo, Is.True);
            Assert.That(estimate.EstimatedSamplers, Is.EqualTo(NataneToonSamplerBudgetEstimator.BaseSamplerCount + 6));
            Assert.That(estimate.IsOverLimit, Is.False);
        }

        [Test]
        public void EvaluateEnable_WithNearLimitMaterial_BlocksExtraHeavyFeature()
        {
            // 5 dedicated-sampler features (1 each) + shared GrabPass (_WATERCOLOR, +1)
            // + shared depth (_INTERSECTION_FADE, +1) + LV(1)+LTCGI(1)+reserve(3)
            // = Base(3) + 12 = 15.
            EnableKeywords(
                "_OUTLINE",
                "_REFLECTION",
                "_ENV_RIM",
                "_VIDEO_TEXTURE",
                "_AUDIOLINK",
                "_WATERCOLOR",
                "_INTERSECTION_FADE",
                "_LTCGI",
                "_USE_LIGHT_VOLUME");

            var current = NataneToonSamplerBudgetEstimator.Estimate(material);
            var evaluation = NataneToonSamplerBudgetEstimator.EvaluateEnable(material, "_SCREEN_EDGE");

            Assert.That(current.IsNearLimit, Is.True);
            // Screen Edge adds depth-normals (+1) and, combined with LV+LTCGI,
            // the screen-space lighting reserve (+2) → 18 > 16, so blocked.
            Assert.That(evaluation.CanEnable, Is.False);
            Assert.That(evaluation.AddedSamplers, Is.EqualTo(3));
            Assert.That(evaluation.AfterEnable.IsOverLimit, Is.True);
        }

        [Test]
        public void Estimate_WithOnlyLightVolumeAndLtcgi_SetsComboFlagWithoutOverflow()
        {
            EnableKeywords("_USE_LIGHT_VOLUME", "_LTCGI");

            var estimate = NataneToonSamplerBudgetEstimator.Estimate(material);

            Assert.That(estimate.HasLightVolumeLtcgiCombo, Is.True);
            Assert.That(estimate.HasCriticalLightingCombo, Is.False);
            Assert.That(estimate.HasScreenSpaceLightingCombo, Is.False);
            Assert.That(estimate.IsOverLimit, Is.False);
            Assert.That(estimate.EstimatedSamplers, Is.EqualTo(NataneToonSamplerBudgetEstimator.BaseSamplerCount + 5));
        }

        [Test]
        public void EvaluateEnable_WithSharedMaskFeature_DoesNotAddSamplerCost()
        {
            EnableKeywords("_USE_LIGHT_VOLUME", "_LTCGI");

            var evaluation = NataneToonSamplerBudgetEstimator.EvaluateEnable(material, "_RIM_LIGHT");

            Assert.That(evaluation.CanEnable, Is.True);
            Assert.That(evaluation.AddedSamplers, Is.EqualTo(0));
            Assert.That(evaluation.AfterEnable.EstimatedSamplers, Is.EqualTo(evaluation.Current.EstimatedSamplers));
        }

        [Test]
        public void EvaluateEnable_WithSharedBlurMaskFeature_DoesNotAddSamplerCost()
        {
            EnableKeywords("_USE_LIGHT_VOLUME", "_LTCGI");

            var screenTone = NataneToonSamplerBudgetEstimator.EvaluateEnable(material, "_SCREEN_TONE");
            var ao = NataneToonSamplerBudgetEstimator.EvaluateEnable(material, "_USE_AO");

            Assert.That(screenTone.CanEnable, Is.True);
            Assert.That(screenTone.AddedSamplers, Is.EqualTo(0));
            Assert.That(screenTone.AfterEnable.EstimatedSamplers, Is.EqualTo(screenTone.Current.EstimatedSamplers));

            Assert.That(ao.CanEnable, Is.True);
            Assert.That(ao.AddedSamplers, Is.EqualTo(0));
            Assert.That(ao.AfterEnable.EstimatedSamplers, Is.EqualTo(ao.Current.EstimatedSamplers));
        }

        [Test]
        public void Estimate_WithScreenEdgeLightingCombo_HitsLimit()
        {
            EnableKeywords(
                "_2ND_TEXTURE",
                "_3RD_TEXTURE",
                "_4TH_TEXTURE",
                "_LTCGI",
                "_PARALLAX",
                "_PROCEDURAL_MATCAP",
                "_RIM_LIGHT",
                "_SCREEN_EDGE",
                "_SSS",
                "_USE_AO",
                "_USE_LIGHT_VOLUME");

            var estimate = NataneToonSamplerBudgetEstimator.Estimate(material);

            Assert.That(estimate.HasLightVolumeLtcgiCombo, Is.True);
            Assert.That(estimate.HasCriticalLightingCombo, Is.False);
            Assert.That(estimate.HasScreenSpaceLightingCombo, Is.True);
            // LV(1)+LTCGI(1) + shared depth(+1) + depth-normals(+1)
            // + combo reserve(3) + screen-space reserve(2) = Base(3) + 9 = 12.
            Assert.That(estimate.EstimatedSamplers, Is.EqualTo(NataneToonSamplerBudgetEstimator.BaseSamplerCount + 9));
            Assert.That(estimate.IsOverLimit, Is.False);
        }

        private void EnableKeywords(params string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                material.EnableKeyword(keywords[i]);
                Assert.That(material.IsKeywordEnabled(keywords[i]), Is.True,
                    $"Keyword {keywords[i]} did not take effect — is it declared on the test shader?");
            }
        }
    }
}
