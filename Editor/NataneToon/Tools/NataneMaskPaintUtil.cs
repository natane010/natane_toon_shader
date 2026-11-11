using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// マスクペイントのピクセル演算ユーティリティ (GUI 非依存・単体テスト可能)
    /// Pure pixel math for mask painting. No editor/GUI dependencies so it can be
    /// unit-tested and reused by both the standalone window and the studio tab.
    ///
    /// チャンネル規約 / Channel convention: 0=R, 1=G, 2=B, 3=A
    /// 指定チャンネルのみ書き込み、他チャンネルは保持する。
    /// Only the chosen channel is written; the others are preserved.
    /// </summary>
    public static class NataneMaskPaintUtil
    {
        /// <summary>
        /// ソフト円ブラシの重み (0..1)。
        /// Soft round brush weight in [0,1] for a texel at <paramref name="dist"/>
        /// texels from the brush centre.
        /// hardness 0 = fully feathered, 1 = hard edge.
        /// </summary>
        public static float BrushWeight(float dist, float radiusPx, float hardness)
        {
            if (radiusPx <= 0f)
            {
                return dist <= 0.5f ? 1f : 0f;
            }

            float t = Mathf.Clamp01(dist / radiusPx);
            float inner = Mathf.Clamp01(hardness);

            if (t <= inner)
            {
                return 1f;
            }
            if (t >= 1f)
            {
                return 0f;
            }

            // Smoothstep falloff from the hard core to the outer edge.
            float f = (t - inner) / Mathf.Max(1e-4f, 1f - inner);
            return 1f - (f * f * (3f - 2f * f));
        }

        /// <summary>
        /// UV 座標を中心にソフト円ブラシを1チャンネルへスプラットする (バッファ版)。
        /// Splat a soft round brush centred at <paramref name="uv"/> into a single
        /// channel of an RGBA32 pixel buffer. Other channels are untouched.
        /// </summary>
        public static void SplatBuffer(Color32[] pixels, int width, int height,
            Vector2 uv, float radiusPx, float hardness, float opacity, int channel, bool erase)
        {
            if (pixels == null || width <= 0 || height <= 0)
            {
                return;
            }

            channel = Mathf.Clamp(channel, 0, 3);
            opacity = Mathf.Clamp01(opacity);
            if (opacity <= 0f)
            {
                return;
            }

            float cx = uv.x * width;
            float cy = uv.y * height;
            int reach = Mathf.CeilToInt(radiusPx) + 1;

            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - reach));
            int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(cx + reach));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - reach));
            int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(cy + reach));

            float target = erase ? 0f : 1f;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x + 0.5f) - cx;
                    float dy = (y + 0.5f) - cy;
                    float dist = Mathf.Sqrt((dx * dx) + (dy * dy));

                    float w = BrushWeight(dist, radiusPx, hardness) * opacity;
                    if (w <= 0f)
                    {
                        continue;
                    }

                    int idx = (y * width) + x;
                    Color32 c = pixels[idx];
                    float cur = GetChannel(c, channel) / 255f;
                    float nv = Mathf.Lerp(cur, target, w);
                    pixels[idx] = SetChannel(c, channel, (byte)Mathf.RoundToInt(Mathf.Clamp01(nv) * 255f));
                }
            }
        }

        /// <summary>
        /// Texture2D へ直接スプラットする便利メソッド。
        /// Convenience wrapper that reads, splats, and applies to a readable Texture2D.
        /// </summary>
        public static void SplatTexture(Texture2D texture, Vector2 uv,
            float radiusPx, float hardness, float opacity, int channel, bool erase)
        {
            if (texture == null)
            {
                return;
            }

            Color32[] pixels = texture.GetPixels32();
            SplatBuffer(pixels, texture.width, texture.height, uv, radiusPx, hardness, opacity, channel, erase);
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        /// <summary>
        /// 塗られた領域を数ピクセル外側へ拡張し、UV シームを緩和する。
        /// Dilate the painted (non-zero) area of the given channel outward by
        /// <paramref name="iterations"/> texels using an 8-neighbour max filter.
        /// This bleeds paint across UV island edges to avoid visible seams.
        /// </summary>
        public static void DilateChannel(Color32[] pixels, int width, int height, int channel, int iterations)
        {
            if (pixels == null || width <= 0 || height <= 0 || iterations <= 0)
            {
                return;
            }

            channel = Mathf.Clamp(channel, 0, 3);

            for (int it = 0; it < iterations; it++)
            {
                Color32[] src = (Color32[])pixels.Clone();

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int idx = (y * width) + x;
                        if (GetChannel(src[idx], channel) > 0)
                        {
                            continue; // already painted
                        }

                        byte best = 0;
                        for (int oy = -1; oy <= 1; oy++)
                        {
                            int ny = y + oy;
                            if (ny < 0 || ny >= height)
                            {
                                continue;
                            }

                            for (int ox = -1; ox <= 1; ox++)
                            {
                                if (ox == 0 && oy == 0)
                                {
                                    continue;
                                }

                                int nx = x + ox;
                                if (nx < 0 || nx >= width)
                                {
                                    continue;
                                }

                                byte nv = GetChannel(src[(ny * width) + nx], channel);
                                if (nv > best)
                                {
                                    best = nv;
                                }
                            }
                        }

                        if (best > 0)
                        {
                            pixels[idx] = SetChannel(pixels[idx], channel, best);
                        }
                    }
                }
            }
        }

        public static byte GetChannel(Color32 c, int channel)
        {
            switch (channel)
            {
                case 0: return c.r;
                case 1: return c.g;
                case 2: return c.b;
                default: return c.a;
            }
        }

        public static Color32 SetChannel(Color32 c, int channel, byte value)
        {
            switch (channel)
            {
                case 0: c.r = value; break;
                case 1: c.g = value; break;
                case 2: c.b = value; break;
                default: c.a = value; break;
            }
            return c;
        }
    }
}
