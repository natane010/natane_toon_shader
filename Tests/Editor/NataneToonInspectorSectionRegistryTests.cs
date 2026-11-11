using System.Linq;
using NUnit.Framework;
using NataneToon.Editor;

namespace NataneToon.Tests.Editor
{
    public class NataneToonInspectorSectionRegistryTests
    {
        [Test]
        public void SectionKeys_AreUnique()
        {
            string[] duplicateKeys = NataneToonInspectorSectionRegistry.All
                .GroupBy(section => section.Key)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.That(duplicateKeys, Is.Empty);
        }

        [Test]
        public void ToggleSections_HaveToggleProperties()
        {
            string[] invalidSections = NataneToonInspectorSectionRegistry.All
                .Where(section => !string.IsNullOrEmpty(section.ToggleKeyword))
                .Where(section => string.IsNullOrEmpty(section.ToggleProperty))
                .Select(section => section.Key)
                .ToArray();

            Assert.That(invalidSections, Is.Empty);
        }

        [Test]
        public void ToggleKeywords_AreUnique()
        {
            string[] duplicateKeywords = NataneToonInspectorSectionRegistry.All
                .Where(section => !string.IsNullOrEmpty(section.ToggleKeyword))
                .GroupBy(section => section.ToggleKeyword)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.That(duplicateKeywords, Is.Empty);
        }

        [TestCase("mirror", "MirrorTexture")]
        [TestCase("顔", "FaceOrtho")]
        [TestCase("stencil", "Rendering")]
        [TestCase("sampler", "Performance")]
        [TestCase("boil", "LineBoil")]
        [TestCase("ボイル", "LineBoil")]
        [TestCase("等高線", "Topographic")]
        [TestCase("topographic", "Topographic")]
        [TestCase("modulator", "FXModulator")]
        [TestCase("catchlight", "ShapedHighlight")]
        [TestCase("pixel", "PixelArt")]
        [TestCase("ピクセル", "PixelArt")]
        [TestCase("caustics", "Caustics")]
        [TestCase("コースティクス", "Caustics")]
        [TestCase("lenticular", "Lenticular")]
        [TestCase("レンチキュラー", "Lenticular")]
        [TestCase("x-ray", "XRay")]
        [TestCase("透視", "XRay")]
        public void Search_FindsExpectedSection(string query, string expectedKey)
        {
            string[] resultKeys = NataneToonInspectorSectionRegistry.Search(query)
                .Select(section => section.Key)
                .ToArray();

            Assert.That(resultKeys, Does.Contain(expectedKey));
        }

        [Test]
        public void EveryTab_HasSections()
        {
            foreach (NataneInspectorTab tab in System.Enum.GetValues(typeof(NataneInspectorTab)))
                Assert.That(NataneToonInspectorSectionRegistry.ForTab(tab), Is.Not.Empty, tab.ToString());
        }

        [Test]
        public void EverySection_HasBeginnerDescriptions()
        {
            string[] missing = NataneToonInspectorSectionRegistry.All
                .Where(section => string.IsNullOrWhiteSpace(section.DescriptionJP)
                               || string.IsNullOrWhiteSpace(section.DescriptionEN))
                .Select(section => section.Key)
                .ToArray();

            Assert.That(missing, Is.Empty, "Sections without a JP+EN one-line description: " + string.Join(", ", missing));
        }

        // v1.6.x / v1.7.x で追加した表現機能が登録から漏れていないことを担保する。
        // これらはトグルキーワード付きの Effects タブ機能。
        [TestCase("LineBoil", "_LINE_BOIL", "_LineBoil")]
        [TestCase("ShapedHighlight", "_SHAPED_HIGHLIGHT", "_ShapedHighlight")]
        [TestCase("Topographic", "_TOPOGRAPHIC", "_Topographic")]
        [TestCase("FXModulator", "_FX_MODULATOR", "_FXModulator")]
        [TestCase("PixelArt", "_PIXEL_ART", "_PixelArt")]
        [TestCase("Caustics", "_CAUSTICS", "_Caustics")]
        [TestCase("Lenticular", "_LENTICULAR", "_Lenticular")]
        public void NewExpressionSections_AreRegistered(string key, string expectedKeyword, string expectedProperty)
        {
            NataneInspectorSectionDescriptor descriptor = NataneToonInspectorSectionRegistry.Find(key);

            Assert.That(descriptor, Is.Not.Null, key + " is not registered");
            Assert.That(descriptor.ToggleKeyword, Is.EqualTo(expectedKeyword));
            Assert.That(descriptor.ToggleProperty, Is.EqualTo(expectedProperty));
            Assert.That(descriptor.Tab, Is.EqualTo(NataneInspectorTab.Effects));
            Assert.That(descriptor.FindByKeywordRoundTrips(), Is.True);
        }

        [Test]
        public void FXModulator_IsUnderControlGroup()
        {
            NataneInspectorSectionDescriptor descriptor = NataneToonInspectorSectionRegistry.Find("FXModulator");
            Assert.That(descriptor.GroupEN, Is.EqualTo("Control"));
            Assert.That(descriptor.GroupJP, Is.EqualTo("制御"));
        }

        // X-Ray は Ghost と同じくバリアント専用（HasProperty ガード）のため、
        // トグルキーワードを持たない。誤ってキーワードを付けていないことを保証する。
        [Test]
        public void XRay_IsRegisteredAsKeywordlessVariantSection()
        {
            NataneInspectorSectionDescriptor descriptor = NataneToonInspectorSectionRegistry.Find("XRay");

            Assert.That(descriptor, Is.Not.Null, "XRay is not registered");
            Assert.That(descriptor.Tab, Is.EqualTo(NataneInspectorTab.Output));
            Assert.That(descriptor.Difficulty, Is.EqualTo(NataneInspectorDifficulty.Advanced));
            Assert.That(string.IsNullOrEmpty(descriptor.ToggleKeyword), Is.True, "XRay must not carry a shader keyword (variant-only, like Ghost)");
            Assert.That(descriptor.GroupEN, Is.EqualTo("Variant"));
        }
    }

    internal static class NataneInspectorSectionDescriptorTestExtensions
    {
        public static bool FindByKeywordRoundTrips(this NataneInspectorSectionDescriptor descriptor)
        {
            return NataneToonInspectorSectionRegistry.FindByKeyword(descriptor.ToggleKeyword) == descriptor;
        }
    }
}
