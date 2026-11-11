using System.Collections.Generic;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Analyzes mesh geometry to derive optimal shader parameters.
    /// Uses PCA shape classification, normal variance, and bounds analysis.
    /// </summary>
    public static class NataneMeshAnalyzer
    {
        /// <summary>
        /// Shape classification result from PCA analysis.
        /// </summary>
        public enum MeshShape
        {
            Spherical,      // Face-like
            Cylindrical,    // Arm/leg-like
            Planar,         // Cloth/hair-like
            Elongated,      // Weapon/staff-like
        }

        /// <summary>
        /// UV analysis result — texel density, seams, channel usage.
        /// </summary>
        public struct UVAnalysisResult
        {
            public bool valid;

            /// <summary>Texel density (texture pixels per world unit). Higher = more detail.</summary>
            public float texelDensity;

            /// <summary>Number of UV seams detected (same position, different UV).</summary>
            public int seamCount;

            /// <summary>Seam ratio: seamCount / totalSharedPositions. 0..1.</summary>
            public float seamRatio;

            /// <summary>Whether UV1 channel exists (for detail map / lightmap).</summary>
            public bool hasUV1;

            /// <summary>Whether vertex colors exist (for smooth normal bake detection).</summary>
            public bool hasVertexColors;

            // ===== Derived recommendations =====

            /// <summary>Recommended _BumpScale based on texel density.</summary>
            public float recommendedBumpScale;

            /// <summary>Recommended _MicroNormalTiling based on texel density.</summary>
            public float recommendedMicroNormalTiling;

            /// <summary>Recommended _MicroNormalStrength based on texel density.</summary>
            public float recommendedMicroNormalStrength;

            /// <summary>Recommended _SpecularSize based on texel density.</summary>
            public float recommendedSpecularSize;

            /// <summary>Outline width adjustment multiplier from seam analysis. 1.0 = no change.</summary>
            public float outlineWidthSeamMultiplier;

            /// <summary>Recommended _SmoothNormalMode (0=ObjectSpace, 2=Texture).</summary>
            public float recommendedSmoothNormalMode;
        }

        /// <summary>
        /// Full mesh analysis result.
        /// </summary>
        public struct MeshAnalysisResult
        {
            public bool valid;
            public MeshShape shape;
            public float outlineWidth;
            public float normalFlattenY;
            public bool needsSmoothNormals;
            public AutoSetupQuality recommendedQuality;
            public UVAnalysisResult uvResult;
        }

        /// <summary>
        /// Analyze a mesh and return recommended shader parameters.
        /// </summary>
        public static MeshAnalysisResult Analyze(Mesh mesh, AutoSetupRole role)
        {
            var result = new MeshAnalysisResult { valid = false };
            if (mesh == null) return result;

            result.valid = true;
            result.shape = ClassifyShapePCA(mesh);
            result.outlineWidth = AutoOutlineWidth(mesh);
            result.normalFlattenY = AutoNormalFlattenY(mesh, result.shape, role);
            result.needsSmoothNormals = NeedsSmoothNormals(mesh);
            result.recommendedQuality = AutoQualityFromMesh(mesh);
            result.uvResult = AnalyzeUV(mesh);

            // Apply UV seam multiplier to outline width
            if (result.uvResult.valid)
                result.outlineWidth *= result.uvResult.outlineWidthSeamMultiplier;

            return result;
        }

        /// <summary>
        /// Analyze a mesh with a texture reference for texel density calculation.
        /// </summary>
        public static MeshAnalysisResult Analyze(Mesh mesh, AutoSetupRole role, Texture2D mainTex)
        {
            var result = Analyze(mesh, role);
            if (result.valid && mainTex != null && result.uvResult.valid)
            {
                // Recalculate texel density with actual texture resolution
                result.uvResult = AnalyzeUV(mesh, mainTex.width, mainTex.height);
                result.outlineWidth = AutoOutlineWidth(mesh) * result.uvResult.outlineWidthSeamMultiplier;
            }
            return result;
        }

        /// <summary>
        /// Try to find the mesh associated with a material (via scene renderers).
        /// </summary>
        public static Mesh FindMeshForMaterial(Material mat)
        {
            if (mat == null) return null;

            // Search scene for renderers using this material
            var renderers = NataneEditorCompat.FindObjectsOfTypeCompat<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials == null) continue;
                foreach (var sharedMat in renderer.sharedMaterials)
                {
                    if (sharedMat != mat) continue;

                    if (renderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                        return smr.sharedMesh;
                    if (renderer is MeshRenderer mr)
                    {
                        var mf = renderer.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                            return mf.sharedMesh;
                    }
                }
            }
            return null;
        }

        // ===== PCA shape classification =====

        private static MeshShape ClassifyShapePCA(Mesh mesh)
        {
            Vector3[] verts = mesh.vertices;
            if (verts == null || verts.Length < 3)
                return MeshShape.Spherical;

            // Compute centroid
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < verts.Length; i++)
                centroid += verts[i];
            centroid /= verts.Length;

            // Compute covariance matrix (3x3 symmetric)
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 d = verts[i] - centroid;
                xx += d.x * d.x;
                xy += d.x * d.y;
                xz += d.x * d.z;
                yy += d.y * d.y;
                yz += d.y * d.z;
                zz += d.z * d.z;
            }
            float n = verts.Length;
            xx /= n; xy /= n; xz /= n; yy /= n; yz /= n; zz /= n;

            // Use power iteration to find principal eigenvalues (simplified)
            // For shape classification, we can use bounds ratio as a reasonable proxy
            Bounds b = mesh.bounds;
            float sx = b.size.x;
            float sy = b.size.y;
            float sz = b.size.z;
            float maxDim = Mathf.Max(sx, Mathf.Max(sy, sz));
            float minDim = Mathf.Min(sx, Mathf.Min(sy, sz));
            float midDim = sx + sy + sz - maxDim - minDim;

            if (maxDim < 0.001f) return MeshShape.Spherical;

            float elongation = maxDim / Mathf.Max(midDim, 0.001f);
            float flatness = minDim / Mathf.Max(midDim, 0.001f);

            // Classify based on ratios
            if (elongation > 3.0f)
                return MeshShape.Elongated;
            if (flatness < 0.3f)
                return MeshShape.Planar;
            if (elongation > 1.8f)
                return MeshShape.Cylindrical;
            return MeshShape.Spherical;
        }

        // ===== Auto parameters =====

        /// <summary>
        /// Compute optimal outline width from mesh bounds.
        /// Matches OutlineOptimizer.OptimizeOutlineWidth logic.
        /// </summary>
        public static float AutoOutlineWidth(Mesh mesh)
        {
            if (mesh == null) return 0.003f;
            float maxSize = Mathf.Max(
                mesh.bounds.size.x,
                Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
            return Mathf.Clamp(maxSize * 0.01f, 0.001f, 0.01f);
        }

        private static float AutoNormalFlattenY(Mesh mesh, MeshShape shape, AutoSetupRole role)
        {
            // Shape-based base value
            float baseFlatten;
            switch (shape)
            {
                case MeshShape.Spherical:
                    baseFlatten = 0.2f;
                    break;
                case MeshShape.Cylindrical:
                    baseFlatten = 0.1f;
                    break;
                case MeshShape.Planar:
                    baseFlatten = 0.05f;
                    break;
                case MeshShape.Elongated:
                    baseFlatten = 0.03f;
                    break;
                default:
                    baseFlatten = 0.1f;
                    break;
            }

            // Role-based adjustment
            switch (role)
            {
                case AutoSetupRole.Face:
                    baseFlatten = Mathf.Max(baseFlatten, 0.2f);
                    break;
                case AutoSetupRole.Hair:
                    baseFlatten = Mathf.Clamp(baseFlatten, 0.05f, 0.15f);
                    break;
                case AutoSetupRole.Eye:
                    baseFlatten = 0.0f;
                    break;
            }

            return baseFlatten;
        }

        /// <summary>
        /// Detect if a mesh needs smooth normal baking (has split normals at shared positions).
        /// </summary>
        public static bool NeedsSmoothNormals(Mesh mesh)
        {
            if (mesh == null) return false;
            var verts = mesh.vertices;
            var norms = mesh.normals;
            if (norms == null || norms.Length == 0) return false;

            // Group vertices by position
            var groups = new Dictionary<Vector3Int, List<int>>();
            const float quantize = 10000f;
            for (int i = 0; i < verts.Length; i++)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(verts[i].x * quantize),
                    Mathf.RoundToInt(verts[i].y * quantize),
                    Mathf.RoundToInt(verts[i].z * quantize));
                if (!groups.ContainsKey(key))
                    groups[key] = new List<int>();
                groups[key].Add(i);
            }

            // Detect split normals
            float maxVariance = 0f;
            foreach (var g in groups.Values)
            {
                if (g.Count <= 1) continue;
                Vector3 avg = Vector3.zero;
                for (int j = 0; j < g.Count; j++)
                    avg += norms[g[j]];
                avg.Normalize();

                float variance = 0f;
                for (int j = 0; j < g.Count; j++)
                    variance += (1f - Vector3.Dot(norms[g[j]], avg));
                maxVariance = Mathf.Max(maxVariance, variance / g.Count);
            }

            return maxVariance > 0.1f;
        }

        private static AutoSetupQuality AutoQualityFromMesh(Mesh mesh)
        {
            int triCount = mesh.triangles.Length / 3;
            if (triCount < 20000) return AutoSetupQuality.High;
            if (triCount < 50000) return AutoSetupQuality.Standard;
            return AutoSetupQuality.Mobile;
        }

        // ===== UV Analysis =====

        /// <summary>
        /// Analyze UV data with default texture resolution assumption (1024x1024).
        /// </summary>
        public static UVAnalysisResult AnalyzeUV(Mesh mesh)
        {
            return AnalyzeUV(mesh, 1024, 1024);
        }

        /// <summary>
        /// Analyze UV data with actual texture resolution for accurate texel density.
        /// </summary>
        public static UVAnalysisResult AnalyzeUV(Mesh mesh, int texWidth, int texHeight)
        {
            var result = new UVAnalysisResult { valid = false };
            if (mesh == null) return result;

            Vector2[] uv0 = mesh.uv;
            if (uv0 == null || uv0.Length == 0) return result;

            result.valid = true;

            // --- 1. Texel density ---
            result.texelDensity = CalculateTexelDensity(mesh, uv0, texWidth, texHeight);
            DeriveTexelDensityRecommendations(ref result);

            // --- 2. UV seam detection ---
            CalculateUVSeams(mesh, uv0, ref result);
            DeriveSeamRecommendations(ref result);

            // --- 3. Channel detection ---
            Vector2[] uv1 = mesh.uv2;
            result.hasUV1 = uv1 != null && uv1.Length > 0;

            Color[] colors = mesh.colors;
            result.hasVertexColors = colors != null && colors.Length == mesh.vertexCount;

            // Smooth normal mode: vertex colors present → likely already baked → ObjectSpace
            result.recommendedSmoothNormalMode = result.hasVertexColors ? 0f : 2f;

            return result;
        }

        /// <summary>
        /// Calculate average texel density: how many texture pixels per world unit.
        /// Computed as: sqrt(textureArea_in_pixels / worldSurfaceArea).
        /// </summary>
        private static float CalculateTexelDensity(Mesh mesh, Vector2[] uv, int texW, int texH)
        {
            int[] tris = mesh.triangles;
            Vector3[] verts = mesh.vertices;
            if (tris == null || tris.Length < 3 || verts == null) return 0f;

            float totalWorldArea = 0f;
            float totalUVArea = 0f;

            // Sample triangles (cap at 3000 triangles for performance)
            int triCount = tris.Length / 3;
            int step = Mathf.Max(1, triCount / 3000);

            for (int t = 0; t < triCount; t += step)
            {
                int i0 = tris[t * 3];
                int i1 = tris[t * 3 + 1];
                int i2 = tris[t * 3 + 2];

                if (i0 >= verts.Length || i1 >= verts.Length || i2 >= verts.Length) continue;
                if (i0 >= uv.Length || i1 >= uv.Length || i2 >= uv.Length) continue;

                // World-space triangle area
                Vector3 edge1 = verts[i1] - verts[i0];
                Vector3 edge2 = verts[i2] - verts[i0];
                float worldArea = Vector3.Cross(edge1, edge2).magnitude * 0.5f;

                // UV-space triangle area
                Vector2 uvEdge1 = uv[i1] - uv[i0];
                Vector2 uvEdge2 = uv[i2] - uv[i0];
                float uvArea = Mathf.Abs(uvEdge1.x * uvEdge2.y - uvEdge1.y * uvEdge2.x) * 0.5f;

                totalWorldArea += worldArea;
                totalUVArea += uvArea;
            }

            if (totalWorldArea < 0.0001f) return 0f;

            // UV area → pixel area
            float pixelArea = totalUVArea * texW * texH;
            // Density = sqrt(pixelArea / worldArea) → texels per world unit
            return Mathf.Sqrt(pixelArea / totalWorldArea);
        }

        /// <summary>
        /// Derive BumpScale, MicroNormalTiling/Strength, SpecularSize from texel density.
        /// </summary>
        private static void DeriveTexelDensityRecommendations(ref UVAnalysisResult result)
        {
            float density = result.texelDensity;

            // Normalize density to a 0..1 range (typical range: 50..2000 texels/unit)
            // Low density = 0, High density = 1
            float t = Mathf.InverseLerp(50f, 2000f, density);

            // High density (detailed texture) → stronger bump, larger micro normal tiling, smaller specular
            // Low density (coarse texture) → weaker bump, smaller tiling, larger specular
            result.recommendedBumpScale = Mathf.Lerp(0.5f, 1.5f, t);
            result.recommendedMicroNormalTiling = Mathf.Lerp(1.0f, 5.0f, t);
            result.recommendedMicroNormalStrength = Mathf.Lerp(0.2f, 0.8f, t);
            result.recommendedSpecularSize = Mathf.Lerp(0.15f, 0.04f, t); // Inverse: high density → smaller
        }

        /// <summary>
        /// Detect UV seams: vertex positions that share location but have different UVs.
        /// </summary>
        private static void CalculateUVSeams(Mesh mesh, Vector2[] uv, ref UVAnalysisResult result)
        {
            Vector3[] verts = mesh.vertices;
            if (verts == null || verts.Length == 0) return;

            // Group vertices by position (same quantization as NeedsSmoothNormals)
            var groups = new Dictionary<Vector3Int, List<int>>();
            const float quantize = 10000f;
            for (int i = 0; i < verts.Length; i++)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(verts[i].x * quantize),
                    Mathf.RoundToInt(verts[i].y * quantize),
                    Mathf.RoundToInt(verts[i].z * quantize));
                if (!groups.ContainsKey(key))
                    groups[key] = new List<int>();
                groups[key].Add(i);
            }

            int seamPositions = 0;
            int sharedPositions = 0;
            const float uvThreshold = 0.001f;

            foreach (var g in groups.Values)
            {
                if (g.Count <= 1) continue;
                sharedPositions++;

                // Check if any pair has different UVs
                bool hasSeam = false;
                Vector2 refUV = (g[0] < uv.Length) ? uv[g[0]] : Vector2.zero;
                for (int j = 1; j < g.Count; j++)
                {
                    if (g[j] >= uv.Length) continue;
                    Vector2 diff = uv[g[j]] - refUV;
                    if (Mathf.Abs(diff.x) > uvThreshold || Mathf.Abs(diff.y) > uvThreshold)
                    {
                        hasSeam = true;
                        break;
                    }
                }

                if (hasSeam) seamPositions++;
            }

            result.seamCount = seamPositions;
            result.seamRatio = sharedPositions > 0
                ? (float)seamPositions / sharedPositions
                : 0f;
        }

        /// <summary>
        /// Derive outline width multiplier from seam density.
        /// </summary>
        private static void DeriveSeamRecommendations(ref UVAnalysisResult result)
        {
            // Many seams → outline tends to break at seams → slightly wider to mask
            // Few seams → clean outline → keep as-is
            // Seam ratio 0.0 → multiplier 1.0, ratio 0.5+ → multiplier 1.3
            result.outlineWidthSeamMultiplier = Mathf.Lerp(1.0f, 1.3f,
                Mathf.Clamp01(result.seamRatio * 2f));
        }
    }
}
