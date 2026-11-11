using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// ショーケース用の合成テクスチャ。
    ///
    /// テクスチャを要求する機能は、既定値が <c>"white"</c> / <c>"black"</c> / <c>"gray"</c> のため
    /// キーワードを立てただけでは Baseline と区別がつかない。それを「確認できた」と
    /// 見なしてしまうのを避けるため、確認に足る素材をここで作る。
    ///
    /// 生成物はパスの存在でキャッシュする。アルゴリズムを変えたら
    /// <see cref="NataneFeatureShowcaseGrid.GeneratedAssetVersion"/> を上げて作り直させること。
    /// </summary>
    internal static class NataneShowcaseAssets
    {
        private const int DefaultResolution = 256;

        // ---- 公開: 機能セットアップから使う素材 ----

        /// <summary>MatCap。中心が明るく縁が落ちる、球体を写した想定の絵。</summary>
        public static Texture2D MatCap()
        {
            return GetOrCreate("Showcase_MatCap", DefaultResolution, false, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f;
                float dy = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 1f) return 0f;

                // 斜め上からの光を想定した簡易ライティング + 縁の締まり。
                float z = Mathf.Sqrt(Mathf.Max(1f - r * r, 0f));
                float lit = Mathf.Clamp01(Vector3.Dot(new Vector3(dx, dy, z).normalized,
                                                      new Vector3(-0.4f, 0.5f, 0.75f).normalized));
                float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, r));
                return Mathf.Clamp01(lit * 0.9f + rim * 0.5f);
            });
        }

        /// <summary>シェーディング用ランプ。横方向に 3 段の階調を持たせる。</summary>
        public static Texture2D ShadingRamp()
        {
            return GetOrCreate("Showcase_Ramp", DefaultResolution, false, (u, v) =>
            {
                // 3 段のトゥーン階調。境目を少しだけ滑らかにする。
                float steps = 3f;
                float q = Mathf.Floor(u * steps) / (steps - 1f);
                float soft = Mathf.SmoothStep(0f, 1f, Mathf.Repeat(u * steps, 1f));
                return Mathf.Clamp01(Mathf.Lerp(q, Mathf.Clamp01(q + soft / (steps - 1f)), 0.25f));
            });
        }

        /// <summary>ノイズ。ディゾルブ・AO・ディテールの汎用素材。</summary>
        public static Texture2D Noise()
        {
            return GetOrCreate("Showcase_Noise", DefaultResolution, false, (u, v) =>
                Mathf.Clamp01(Tiled(u, v, 8, 1) * 0.65f + Tiled(u, v, 19, 2) * 0.35f));
        }

        /// <summary>粒状感。平均 0.5 に寄せてある。</summary>
        public static Texture2D Granulation()
        {
            return GetOrCreate("Showcase_Granulation", DefaultResolution, false, (u, v) =>
                0.5f + (Tiled(u, v, 48, 3) - 0.5f) * 0.7f);
        }

        /// <summary>紙目。繊維方向へ引き伸ばしたノイズ。</summary>
        public static Texture2D Paper()
        {
            return GetOrCreate("Showcase_Paper", DefaultResolution, false, (u, v) =>
                0.5f + (Tiled(u * 0.2f, v, 24, 4) - 0.5f) * 0.5f);
        }

        /// <summary>ハイトマップ。パララックス確認用の格子状の凹凸。</summary>
        public static Texture2D Height()
        {
            return GetOrCreate("Showcase_Height", DefaultResolution, false, (u, v) =>
            {
                float bx = Mathf.Abs(Mathf.Repeat(u * 8f, 1f) - 0.5f);
                float by = Mathf.Abs(Mathf.Repeat(v * 8f, 1f) - 0.5f);
                return Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, Mathf.Min(bx, by) * 3f));
            });
        }

        /// <summary>ノーマルマップ。<see cref="Height"/> の勾配から作る。</summary>
        public static Texture2D NormalMap()
        {
            string path = Path(NataneFeatureShowcaseGrid.OutputFolder, "Showcase_Normal");
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int res = DefaultResolution;
            var pixels = new Color[res * res];

            Func<int, int, float> h = (x, y) =>
            {
                float u = (Mod(x, res) + 0.5f) / res;
                float v = (Mod(y, res) + 0.5f) / res;
                float bx = Mathf.Abs(Mathf.Repeat(u * 8f, 1f) - 0.5f);
                float by = Mathf.Abs(Mathf.Repeat(v * 8f, 1f) - 0.5f);
                return Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, Mathf.Min(bx, by) * 3f));
            };

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = h(x + 1, y) - h(x - 1, y);
                    float dy = h(x, y + 1) - h(x, y - 1);
                    Vector3 n = new Vector3(-dx * 6f, -dy * 6f, 1f).normalized;
                    // Unity の法線マップは RGB に格納（DXT5nm ではないので素直に）。
                    pixels[y * res + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            return Write(path, pixels, res, isNormalMap: true);
        }

        /// <summary>デカール。中央に円、周囲は透明。</summary>
        public static Texture2D Decal()
        {
            return GetOrCreate("Showcase_Decal", DefaultResolution, true, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                float disc = 1f - Mathf.SmoothStep(0.28f, 0.34f, d);
                float hole = Mathf.SmoothStep(0.14f, 0.2f, d);
                return Mathf.Clamp01(disc * hole);
            });
        }

        /// <summary>
        /// 3D LUT を横に並べたストリップ（16x16x16 → 256x16）。
        /// 寒色へ寄せる分かりやすいグレーディングにしてある。
        /// </summary>
        public static Texture2D Lut()
        {
            const int size = 16;
            string path = Path(NataneFeatureShowcaseGrid.OutputFolder, "Showcase_LUT");
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            int width = size * size;
            var pixels = new Color[width * size];

            for (int b = 0; b < size; b++)
            {
                for (int g = 0; g < size; g++)
                {
                    for (int r = 0; r < size; r++)
                    {
                        float rf = r / (float)(size - 1);
                        float gf = g / (float)(size - 1);
                        float bf = b / (float)(size - 1);

                        // 効果が見て分かるように、寒色寄せ + コントラストを付ける。
                        float or_ = Mathf.Clamp01(Mathf.Pow(rf, 1.25f) * 0.85f);
                        float og = Mathf.Clamp01(Mathf.Pow(gf, 1.05f) * 0.95f);
                        float ob = Mathf.Clamp01(Mathf.Pow(bf, 0.85f) * 1.05f);

                        int x = b * size + r;
                        int y = g;
                        pixels[y * width + x] = new Color(or_, og, ob, 1f);
                    }
                }
            }

            return Write(path, pixels, width, size, isNormalMap: false);
        }

        // ---- 生成の下回り ----

        private static Texture2D GetOrCreate(string name, int resolution, bool writeToAlpha,
                                             Func<float, float, float> evaluate)
        {
            string path = Path(NataneFeatureShowcaseGrid.OutputFolder, name);
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = (x + 0.5f) / resolution;
                    float v = (y + 0.5f) / resolution;
                    float value = Mathf.Clamp01(evaluate(u, v));
                    pixels[y * resolution + x] = writeToAlpha
                        ? new Color(1f, 1f, 1f, value)
                        : new Color(value, value, value, 1f);
                }
            }

            return Write(path, pixels, resolution, resolution, isNormalMap: false);
        }

        private static string Path(string folder, string name)
        {
            return $"{folder}/{name}_{NataneFeatureShowcaseGrid.GeneratedAssetVersion}.png";
        }

        private static Texture2D Write(string assetPath, Color[] pixels, int resolution, bool isNormalMap)
        {
            return Write(assetPath, pixels, resolution, resolution, isNormalMap);
        }

        private static Texture2D Write(string assetPath, Color[] pixels, int width, int height, bool isNormalMap)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pixels);
            tex.Apply(false, false);

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root)) return null;

            string absolute = System.IO.Path.Combine(
                root, assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            string dir = System.IO.Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            try
            {
                File.WriteAllBytes(absolute, png);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneShowcase] {assetPath} を書き出せませんでした: {e.Message}");
                return null;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Configure(assetPath, isNormalMap);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void Configure(string assetPath, bool isNormalMap)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            if (isNormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                // 係数として読まれるものが多いので sRGB 変換はかけない。
                importer.sRGBTexture = false;
            }

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

        private static float Tiled(float u, float v, int period, int seed)
        {
            float x = u * period;
            float y = v * period;
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Hash(Mod(xi, period), Mod(yi, period), seed);
            float b = Hash(Mod(xi + 1, period), Mod(yi, period), seed);
            float c = Hash(Mod(xi, period), Mod(yi + 1, period), seed);
            float d = Hash(Mod(xi + 1, period), Mod(yi + 1, period), seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Mod(int value, int period)
        {
            int r = value % period;
            return r < 0 ? r + period : r;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFF) / (float)0x7FFFFFF;
            }
        }
    }
}
