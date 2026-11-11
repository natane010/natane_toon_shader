using NUnit.Framework;
using UnityEngine;

namespace NataneToon.Editor.Tests
{
    /// <summary>
    /// マスクペイントのピクセル演算 (NataneMaskPaintUtil) の EditMode テスト。
    /// </summary>
    [TestFixture]
    public class NataneMaskPaintUtilTests
    {
        private static Color32[] MakeBlack(int w, int h)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(0, 0, 0, 255);
            }
            return px;
        }

        private static int Index(int x, int y, int w) => (y * w) + x;

        [Test]
        public void SplatBuffer_PaintsCenter_LeavesCornerUntouched()
        {
            const int size = 64;
            Color32[] px = MakeBlack(size, size);

            // Paint red channel at the centre.
            NataneMaskPaintUtil.SplatBuffer(px, size, size,
                new Vector2(0.5f, 0.5f), radiusPx: 8f, hardness: 0.5f, opacity: 1f, channel: 0, erase: false);

            Color32 center = px[Index(32, 32, size)];
            Color32 corner = px[Index(0, 0, size)];

            Assert.Greater(center.r, 0, "Center R channel should be painted.");
            Assert.AreEqual(0, corner.r, "Corner R channel should remain untouched.");
        }

        [Test]
        public void SplatBuffer_PreservesOtherChannels()
        {
            const int size = 64;
            Color32[] px = MakeBlack(size, size);

            NataneMaskPaintUtil.SplatBuffer(px, size, size,
                new Vector2(0.5f, 0.5f), 8f, 0.5f, 1f, channel: 0, erase: false);

            Color32 center = px[Index(32, 32, size)];
            Assert.AreEqual(0, center.g, "Green channel must be preserved.");
            Assert.AreEqual(0, center.b, "Blue channel must be preserved.");
            Assert.AreEqual(255, center.a, "Alpha channel must be preserved.");
        }

        [Test]
        public void SplatBuffer_MultiChannel_SlotGGreenIndependentOfR()
        {
            // Emulates _FXModMaskTex where R=slot0, G=slot1.
            const int size = 64;
            Color32[] px = MakeBlack(size, size);

            // Paint slot0 (R) then slot1 (G) at the same spot.
            NataneMaskPaintUtil.SplatBuffer(px, size, size, new Vector2(0.5f, 0.5f), 6f, 1f, 1f, 0, false);
            byte rAfterFirst = px[Index(32, 32, size)].r;

            NataneMaskPaintUtil.SplatBuffer(px, size, size, new Vector2(0.5f, 0.5f), 6f, 1f, 1f, 1, false);
            Color32 center = px[Index(32, 32, size)];

            Assert.Greater(center.g, 0, "Green (slot1) should be painted.");
            Assert.AreEqual(rAfterFirst, center.r, "Red (slot0) must be unchanged by painting green.");
        }

        [Test]
        public void SplatBuffer_Erase_ReducesChannel()
        {
            const int size = 32;
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(255, 0, 0, 255);
            }

            NataneMaskPaintUtil.SplatBuffer(px, size, size, new Vector2(0.5f, 0.5f), 6f, 1f, 1f, 0, erase: true);

            Assert.Less(px[Index(16, 16, size)].r, 255, "Erase should reduce the R channel at the centre.");
        }

        [Test]
        public void BrushWeight_IsOneAtCenter_ZeroBeyondRadius()
        {
            Assert.AreEqual(1f, NataneMaskPaintUtil.BrushWeight(0f, 10f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, NataneMaskPaintUtil.BrushWeight(20f, 10f, 0.5f), 1e-4f);
        }

        [Test]
        public void DilateChannel_GrowsPaintedArea()
        {
            const int size = 16;
            Color32[] px = MakeBlack(size, size);
            px[Index(8, 8, size)] = new Color32(255, 0, 0, 255); // single painted texel

            NataneMaskPaintUtil.DilateChannel(px, size, size, channel: 0, iterations: 1);

            Assert.Greater(px[Index(9, 8, size)].r, 0, "Neighbor to the right should be filled after dilation.");
            Assert.Greater(px[Index(8, 9, size)].r, 0, "Neighbor above should be filled after dilation.");
        }

        [Test]
        public void EnumerateMaskProperties_FindsMaskTexturesOnNataneShader()
        {
            Shader shader = Shader.Find("Natane/Toon Shader");
            if (shader == null)
            {
                Assert.Ignore("Natane Toon Shader not present in this project; skipping enumeration test.");
                return;
            }

            var results = new System.Collections.Generic.List<NataneMaskPainterCore.MaskTargetInfo>();
            NataneMaskPainterCore.EnumerateMaskProperties(shader, results);

            Assert.Greater(results.Count, 0, "Expected at least one mask texture property on the Natane shader.");
            foreach (var r in results)
            {
                StringAssert.Contains("Mask", r.propertyName);
            }
        }
    }
}
