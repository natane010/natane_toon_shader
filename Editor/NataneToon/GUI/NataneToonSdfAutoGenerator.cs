using System.IO;
using UnityEditor;
using UnityEngine;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// Generates a grayscale <b>mask</b> SDF texture from an existing texture and assigns it
    /// to the shader. Source priority: shadow receive mask > main texture.
    ///
    /// <b>This is not the map that <c>_FACE_SDF_ROTATION</c> wants.</b> What this produces is
    /// the signed distance field of a mask shape, which carries no light-angle information.
    /// The rotation-tracking path compares the sampled value against a light-angle threshold,
    /// so feeding it a distance field makes the shadow transition in an arbitrary order.
    ///
    /// For rotation tracking use the Map Generator's "Face SDF Shadow Map" bake, which sweeps
    /// the light around the mesh and records the actual transition angle per texel.
    /// See <c>Documentation~/NPR2026_P2_FACE_SDF_BAKE.md</c>.
    /// </summary>
    internal static class NataneToonSdfAutoGenerator
    {
        /// <summary>
        /// True when the material is set up for rotation tracking, where this generator's
        /// output is the wrong kind of map. Callers use it to steer the user to the bake.
        /// </summary>
        public static bool IsRotationTrackingMaterial(Material material)
        {
            if (material == null) return false;
            if (material.IsKeywordEnabled("_FACE_SDF_ROTATION")) return true;
            return material.HasProperty("_FaceSDFRotation") && material.GetFloat("_FaceSDFRotation") >= 0.5f;
        }

        private const float DiagonalDistance = 1.41421356f;
        private const float MaskThreshold = 0.5f;
        private const float AlphaUsageThreshold = 0.05f;
        private const string FallbackOutputRoot = "Assets/NataneToonGenerated";

        public static bool TryGenerateAndAssign(Material material, out string message)
        {
            if (material == null)
            {
                message = L("マテリアルが見つかりません。", "Material was not found.");
                return false;
            }

            if (!TryResolveSourceTexture(material, out Texture2D sourceTexture, out string sourceLabel))
            {
                message = L(
                    "SDF の元になるテクスチャが見つかりませんでした。\nShadow Receive Mask または Main Texture を設定してください。",
                    "No source texture was found for SDF generation.\nAssign a Shadow Receive Mask or Main Texture first.");
                return false;
            }

            Texture2D readableTexture = CreateReadableCopy(sourceTexture);
            if (readableTexture == null)
            {
                message = L("元テクスチャを読み取れませんでした。", "Failed to read the source texture.");
                return false;
            }

            try
            {
                Color[] sourcePixels = readableTexture.GetPixels();
                bool useAlphaChannel = ShouldUseAlphaChannel(sourcePixels);
                bool[] mask = BuildMask(sourcePixels, useAlphaChannel);
                if (!HasMaskVariation(mask))
                {
                    message = L(
                        "元テクスチャから SDF 生成に必要な明暗差を検出できませんでした。\nMainTex の alpha かマスクテクスチャを確認してください。",
                        "The source texture does not contain enough contrast to generate an SDF.\nCheck the MainTex alpha or use a mask texture.");
                    return false;
                }

                float[] signedDistances = BuildSignedDistanceField(mask, readableTexture.width, readableTexture.height);
                Texture2D sdfTexture = BuildSdfTexture(signedDistances, readableTexture.width, readableTexture.height);
                string outputPath = GetOutputTexturePath(material, sourceTexture);
                WriteTextureAsset(outputPath, sdfTexture);
                ConfigureGeneratedTexture(outputPath);

                Texture2D savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                if (savedTexture == null)
                {
                    message = L("生成した SDF テクスチャの読み込みに失敗しました。", "Failed to load the generated SDF texture.");
                    return false;
                }

                Undo.RecordObject(material, "Generate SDF Map");
                material.SetTexture("_SDFMap", savedTexture);
                if (material.HasProperty("_UseSDFMap"))
                {
                    material.SetFloat("_UseSDFMap", 1.0f);
                }
                material.EnableKeyword("_SDF_MAP");
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                message = L(
                    $"SDF を自動生成して適用しました。\n元テクスチャ: {sourceLabel}\n保存先: {outputPath}",
                    $"Generated and assigned an SDF map.\nSource: {sourceLabel}\nSaved to: {outputPath}");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(readableTexture);
            }
        }

        private static bool TryResolveSourceTexture(Material material, out Texture2D texture, out string sourceLabel)
        {
            texture = null;
            sourceLabel = string.Empty;

            if (TryGetTexture(material, "_ShadowReceiveMask", out texture))
            {
                sourceLabel = L("Shadow Receive Mask", "Shadow Receive Mask");
                return true;
            }

            if (TryGetTexture(material, "_MainTex", out texture))
            {
                sourceLabel = L("Main Texture", "Main Texture");
                return true;
            }

            return false;
        }

        private static bool TryGetTexture(Material material, string propertyName, out Texture2D texture)
        {
            texture = null;
            if (material == null || !material.HasProperty(propertyName))
            {
                return false;
            }

            texture = material.GetTexture(propertyName) as Texture2D;
            if (texture == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            return !string.IsNullOrEmpty(assetPath);
        }

        private static Texture2D CreateReadableCopy(Texture2D sourceTexture)
        {
            if (sourceTexture == null)
            {
                return null;
            }

            RenderTexture temporary = RenderTexture.GetTemporary(
                sourceTexture.width,
                sourceTexture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);

            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(sourceTexture, temporary);
                RenderTexture.active = temporary;

                Texture2D readable = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, temporary.width, temporary.height), 0, 0);
                readable.Apply(false, false);
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static bool ShouldUseAlphaChannel(Color[] pixels)
        {
            float minAlpha = 1.0f;
            float maxAlpha = 0.0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                float alpha = pixels[i].a;
                minAlpha = Mathf.Min(minAlpha, alpha);
                maxAlpha = Mathf.Max(maxAlpha, alpha);
            }

            return maxAlpha - minAlpha > AlphaUsageThreshold;
        }

        private static bool[] BuildMask(Color[] pixels, bool useAlphaChannel)
        {
            bool[] mask = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = useAlphaChannel ? pixels[i].a : pixels[i].grayscale;
                mask[i] = value >= MaskThreshold;
            }

            return mask;
        }

        private static bool HasMaskVariation(bool[] mask)
        {
            bool anyInside = false;
            bool anyOutside = false;
            for (int i = 0; i < mask.Length; i++)
            {
                anyInside |= mask[i];
                anyOutside |= !mask[i];
                if (anyInside && anyOutside)
                {
                    return true;
                }
            }

            return false;
        }

        private static float[] BuildSignedDistanceField(bool[] mask, int width, int height)
        {
            float[] distanceToInside = BuildDistanceField(mask, width, height);

            bool[] inverted = new bool[mask.Length];
            for (int i = 0; i < mask.Length; i++)
            {
                inverted[i] = !mask[i];
            }

            float[] distanceToOutside = BuildDistanceField(inverted, width, height);
            float[] signedDistance = new float[mask.Length];
            for (int i = 0; i < mask.Length; i++)
            {
                signedDistance[i] = distanceToOutside[i] - distanceToInside[i];
            }

            return signedDistance;
        }

        private static float[] BuildDistanceField(bool[] seeds, int width, int height)
        {
            const float largeValue = 1e6f;
            float[] distance = new float[width * height];

            for (int i = 0; i < seeds.Length; i++)
            {
                distance[i] = seeds[i] ? 0.0f : largeValue;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    float best = distance[index];
                    Relax(distance, width, height, x, y, x - 1, y, 1.0f, ref best);
                    Relax(distance, width, height, x, y, x, y - 1, 1.0f, ref best);
                    Relax(distance, width, height, x, y, x - 1, y - 1, DiagonalDistance, ref best);
                    Relax(distance, width, height, x, y, x + 1, y - 1, DiagonalDistance, ref best);
                    distance[index] = best;
                }
            }

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    int index = y * width + x;
                    float best = distance[index];
                    Relax(distance, width, height, x, y, x + 1, y, 1.0f, ref best);
                    Relax(distance, width, height, x, y, x, y + 1, 1.0f, ref best);
                    Relax(distance, width, height, x, y, x + 1, y + 1, DiagonalDistance, ref best);
                    Relax(distance, width, height, x, y, x - 1, y + 1, DiagonalDistance, ref best);
                    distance[index] = best;
                }
            }

            return distance;
        }

        private static void Relax(float[] distance, int width, int height, int x, int y, int neighborX, int neighborY, float stepCost, ref float best)
        {
            if (neighborX < 0 || neighborY < 0 || neighborX >= width || neighborY >= height)
            {
                return;
            }

            int neighborIndex = neighborY * width + neighborX;
            best = Mathf.Min(best, distance[neighborIndex] + stepCost);
        }

        private static Texture2D BuildSdfTexture(float[] signedDistances, int width, int height)
        {
            Color[] outputPixels = new Color[signedDistances.Length];
            float normalizationDistance = Mathf.Max(8.0f, Mathf.Max(width, height) * 0.25f);

            for (int i = 0; i < signedDistances.Length; i++)
            {
                float normalized = Mathf.Clamp01(0.5f + (signedDistances[i] / normalizationDistance) * 0.5f);
                outputPixels[i] = new Color(normalized, normalized, normalized, 1.0f);
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels(outputPixels);
            texture.Apply(false, false);
            return texture;
        }

        private static string GetOutputTexturePath(Material material, Texture2D sourceTexture)
        {
            string materialPath = AssetDatabase.GetAssetPath(material);
            string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);

            string baseFolder = TryResolveBaseFolder(materialPath)
                ?? TryResolveBaseFolder(sourcePath)
                ?? EnsureFolderHierarchy(FallbackOutputRoot);

            string generatedFolder = GetGeneratedFolder(baseFolder);
            string baseName = SanitizeFileName(material != null ? material.name : sourceTexture.name);
            return AssetDatabase.GenerateUniqueAssetPath($"{generatedFolder}/{baseName}_AutoSDF.png");
        }

        private static string GetGeneratedFolder(string baseFolder)
        {
            string normalized = (baseFolder ?? FallbackOutputRoot).Replace("\\", "/").TrimEnd('/');
            if (normalized.EndsWith("/NataneToon/SDF"))
            {
                return EnsureFolderHierarchy(normalized);
            }

            if (normalized.EndsWith("/NataneToon"))
            {
                return EnsureFolderHierarchy($"{normalized}/SDF");
            }

            return EnsureFolderHierarchy($"{normalized}/NataneToon/SDF");
        }

        private static string TryResolveBaseFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
            {
                return null;
            }

            return Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        }

        private static string EnsureFolderHierarchy(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return assetPath;
            }

            string[] segments = assetPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }

            return current;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "NataneSDF" : value;
        }

        private static void WriteTextureAsset(string assetPath, Texture2D texture)
        {
            byte[] bytes = texture.EncodeToPNG();
            string absolutePath = GetAbsolutePath(assetPath);
            if (string.IsNullOrEmpty(absolutePath))
            {
                throw new IOException($"Failed to resolve an absolute path for asset output: {assetPath}");
            }

            string directoryPath = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllBytes(absolutePath, bytes);
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string GetAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
            {
                return null;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, relativePath);
        }

        private static void ConfigureGeneratedTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
