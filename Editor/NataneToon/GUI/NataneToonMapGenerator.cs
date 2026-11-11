using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// Generates Curvature / Thickness / Flow / AO maps from mesh geometry
    /// and assigns them to material properties via the NataneToon inspector.
    /// </summary>
    internal static class NataneToonMapGenerator
    {
        private const int DefaultResolution = 512;
        private const string FallbackOutputRoot = "Assets/NataneToonGenerated";

        // ===== Public API =====

        /// <summary>
        /// Generate a Curvature Map from mesh vertex normals.
        /// Saved to disk; not auto-assigned (no dedicated property).
        /// </summary>
        public static bool TryGenerateCurvature(Material material, out string message)
        {
            if (!TryGetMesh(material, out Mesh mesh, out message))
                return false;

            try
            {
                EditorUtility.DisplayProgressBar(L("Curvature Map", "Curvature Map"), L("曲率を計算中...", "Calculating curvature..."), 0.2f);

                float[] curvature = ComputeVertexCurvature(mesh);
                float[] concavity = MapGenUtils.ComputeVertexConcavity(mesh);

                // Blend concavity into curvature
                for (int i = 0; i < curvature.Length; i++)
                {
                    curvature[i] = Mathf.Clamp01(curvature[i] - concavity[i] * 0.3f);
                }

                EditorUtility.DisplayProgressBar(L("Curvature Map", "Curvature Map"), L("UV ベイク中...", "UV baking..."), 0.5f);

                Texture2D baked = BakeScalarToUV(mesh, curvature, DefaultResolution);
                if (baked == null)
                {
                    message = L("UV ベイクに失敗しました。メッシュに UV が設定されているか確認してください。",
                                "UV bake failed. Make sure the mesh has UVs assigned.");
                    return false;
                }

                EditorUtility.DisplayProgressBar(L("Curvature Map", "Curvature Map"), L("ブラー適用中...", "Applying blur..."), 0.7f);
                Texture2D blurred = ApplyGaussianBlur(baked, DefaultResolution, 2);

                string path = GetOutputPath(material, "Curvature");
                SaveTexture(blurred, path);

                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(blurred);

                message = L($"Curvature Map を生成しました。\n保存先: {path}\n\n手動で DetailMask 等にドラッグして使用してください。",
                            $"Generated Curvature Map.\nSaved to: {path}\n\nDrag it manually to DetailMask or other slots.");
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Generate a Thickness Map via inverse-normal raycasting.
        /// Assigns to _ThicknessMap.
        /// </summary>
        public static bool TryGenerateThickness(Material material, out string message)
        {
            if (!TryGetMeshAndRenderer(material, out Mesh mesh, out Renderer renderer, out message))
                return false;

            GameObject tempObj = null;
            MeshCollider tempCollider = null;
            try
            {
                EditorUtility.DisplayProgressBar(L("Thickness Map", "Thickness Map"), L("レイキャスト準備中...", "Preparing raycast..."), 0.1f);

                tempObj = CreateTempCollider(mesh, renderer.transform, out tempCollider);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Transform xform = renderer.transform;

                float maxDist = mesh.bounds.size.magnitude;
                float[] thickness = new float[vertices.Length];

                for (int i = 0; i < vertices.Length; i++)
                {
                    if (i % 500 == 0)
                    {
                        float progress = 0.1f + 0.6f * ((float)i / vertices.Length);
                        if (EditorUtility.DisplayCancelableProgressBar(
                            L("Thickness Map", "Thickness Map"),
                            L($"レイキャスト中... ({i}/{vertices.Length})", $"Raycasting... ({i}/{vertices.Length})"),
                            progress))
                        {
                            message = L("ユーザーによりキャンセルされました。", "Cancelled by user.");
                            return false;
                        }
                    }

                    Vector3 worldPos = xform.TransformPoint(vertices[i]);
                    Vector3 worldNormal = xform.TransformDirection(normals[i]).normalized;

                    // Ray from vertex inward (inverse normal)
                    Ray ray = new Ray(worldPos - worldNormal * 0.001f, -worldNormal);
                    if (tempCollider.Raycast(ray, out RaycastHit hit, maxDist))
                    {
                        thickness[i] = 1.0f - Mathf.Clamp01(hit.distance / maxDist);
                    }
                    else
                    {
                        thickness[i] = 0.0f; // No hit = thick
                    }
                }

                EditorUtility.DisplayProgressBar(L("Thickness Map", "Thickness Map"), L("UV ベイク中...", "UV baking..."), 0.75f);
                Texture2D baked = BakeScalarToUV(mesh, thickness, DefaultResolution);
                if (baked == null)
                {
                    message = L("UV ベイクに失敗しました。", "UV bake failed.");
                    return false;
                }

                EditorUtility.DisplayProgressBar(L("Thickness Map", "Thickness Map"), L("ブラー適用中...", "Applying blur..."), 0.85f);
                Texture2D blurred = ApplyGaussianBlur(baked, DefaultResolution, 2);

                string path = GetOutputPath(material, "Thickness");
                SaveTexture(blurred, path);

                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(blurred);

                Texture2D saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (saved != null)
                {
                    Undo.RecordObject(material, "Generate Thickness Map");
                    material.SetTexture("_ThicknessMap", saved);
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssets();
                }

                message = L($"Thickness Map を生成して _ThicknessMap に適用しました。\n保存先: {path}",
                            $"Generated and assigned Thickness Map to _ThicknessMap.\nSaved to: {path}");
                return true;
            }
            finally
            {
                if (tempObj != null) Object.DestroyImmediate(tempObj);
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Generate a Flow Map from mesh tangent vectors.
        /// Assigns to _HairSpecShiftTex.
        /// </summary>
        public static bool TryGenerateFlowMap(Material material, out string message)
        {
            if (!TryGetMesh(material, out Mesh mesh, out message))
                return false;

            Vector4[] tangents = mesh.tangents;
            if (tangents == null || tangents.Length == 0)
            {
                message = L("メッシュにタンジェントデータがありません。", "Mesh has no tangent data.");
                return false;
            }

            try
            {
                EditorUtility.DisplayProgressBar(L("Flow Map", "Flow Map"), L("タンジェントをエンコード中...", "Encoding tangents..."), 0.3f);

                // Encode tangent xyz to [0,1] range
                Vector3[] flowData = new Vector3[tangents.Length];
                for (int i = 0; i < tangents.Length; i++)
                {
                    flowData[i] = new Vector3(
                        tangents[i].x * 0.5f + 0.5f,
                        tangents[i].y * 0.5f + 0.5f,
                        tangents[i].z * 0.5f + 0.5f);
                }

                EditorUtility.DisplayProgressBar(L("Flow Map", "Flow Map"), L("UV ベイク中...", "UV baking..."), 0.6f);
                Texture2D baked = BakeFloat3ToUV(mesh, flowData, DefaultResolution);
                if (baked == null)
                {
                    message = L("UV ベイクに失敗しました。", "UV bake failed.");
                    return false;
                }

                string path = GetOutputPath(material, "Flow");
                SaveTexture(baked, path);
                Object.DestroyImmediate(baked);

                Texture2D saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (saved != null)
                {
                    Undo.RecordObject(material, "Generate Flow Map");
                    material.SetTexture("_HairSpecShiftTex", saved);
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssets();
                }

                message = L($"Flow Map を生成して _HairSpecShiftTex に適用しました。\n保存先: {path}",
                            $"Generated and assigned Flow Map to _HairSpecShiftTex.\nSaved to: {path}");
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Generate an AO Map via hemisphere ray sampling.
        /// Assigns to _AOMap and enables _USE_AO keyword.
        /// </summary>
        public static bool TryGenerateAO(Material material, out string message)
        {
            if (!TryGetMeshAndRenderer(material, out Mesh mesh, out Renderer renderer, out message))
                return false;

            const int rayCount = 64;
            GameObject tempObj = null;
            MeshCollider tempCollider = null;
            try
            {
                EditorUtility.DisplayProgressBar(L("AO Map", "AO Map"), L("レイキャスト準備中...", "Preparing raycast..."), 0.1f);

                tempObj = CreateTempCollider(mesh, renderer.transform, out tempCollider);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Transform xform = renderer.transform;

                float rayLength = mesh.bounds.size.magnitude * 0.5f;
                float[] ao = new float[vertices.Length];

                for (int i = 0; i < vertices.Length; i++)
                {
                    if (i % 200 == 0)
                    {
                        float progress = 0.1f + 0.7f * ((float)i / vertices.Length);
                        if (EditorUtility.DisplayCancelableProgressBar(
                            L("AO Map", "AO Map"),
                            L($"レイキャスト中... ({i}/{vertices.Length})", $"Raycasting... ({i}/{vertices.Length})"),
                            progress))
                        {
                            message = L("ユーザーによりキャンセルされました。", "Cancelled by user.");
                            return false;
                        }
                    }

                    Vector3 worldPos = xform.TransformPoint(vertices[i]);
                    Vector3 worldNormal = xform.TransformDirection(normals[i]).normalized;

                    // Outward hemisphere raycasting
                    int hits = 0;
                    for (int r = 0; r < rayCount; r++)
                    {
                        Vector3 dir = GetHemisphereDirection(worldNormal, r, rayCount);
                        Ray ray = new Ray(worldPos + worldNormal * 0.001f, dir);
                        if (tempCollider.Raycast(ray, out _, rayLength))
                        {
                            hits++;
                        }
                    }

                    // Inward raycasting for concave detection (eye sockets, cavities)
                    int inwardRayCount = Mathf.Max(1, rayCount / 4);
                    float inwardRayLength = rayLength * 0.3f;
                    int inwardHits = 0;
                    for (int r = 0; r < inwardRayCount; r++)
                    {
                        Vector3 inwardDir = -GetHemisphereDirection(worldNormal, r, inwardRayCount);
                        Ray inwardRay = new Ray(worldPos - worldNormal * 0.002f, inwardDir);
                        if (tempCollider.Raycast(inwardRay, out _, inwardRayLength))
                        {
                            inwardHits++;
                        }
                    }
                    float inwardOcclusion = (float)inwardHits / inwardRayCount;

                    float outwardOcclusion = 1.0f - (float)hits / rayCount;
                    ao[i] = outwardOcclusion * (1f - inwardOcclusion * 0.5f);
                }

                EditorUtility.DisplayProgressBar(L("AO Map", "AO Map"), L("UV ベイク中...", "UV baking..."), 0.85f);
                Texture2D baked = BakeScalarToUV(mesh, ao, DefaultResolution);
                if (baked == null)
                {
                    message = L("UV ベイクに失敗しました。", "UV bake failed.");
                    return false;
                }

                EditorUtility.DisplayProgressBar(L("AO Map", "AO Map"), L("ブラー適用中...", "Applying blur..."), 0.9f);
                Texture2D blurred = ApplyGaussianBlur(baked, DefaultResolution, 3);

                string path = GetOutputPath(material, "AO");
                SaveTexture(blurred, path);

                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(blurred);

                Texture2D saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (saved != null)
                {
                    Undo.RecordObject(material, "Generate AO Map");
                    material.SetTexture("_AOMap", saved);
                    if (material.HasProperty("_UseAO"))
                    {
                        material.SetFloat("_UseAO", 1.0f);
                    }
                    material.EnableKeyword("_USE_AO");
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssets();
                }

                message = L($"AO Map を生成して _AOMap に適用しました。\n保存先: {path}",
                            $"Generated and assigned AO Map to _AOMap.\nSaved to: {path}");
                return true;
            }
            finally
            {
                if (tempObj != null) Object.DestroyImmediate(tempObj);
                EditorUtility.ClearProgressBar();
            }
        }

        // ===== Mesh Retrieval =====

        private static bool TryGetMesh(Material material, out Mesh mesh, out string message)
        {
            mesh = null;
            if (material == null)
            {
                message = L("マテリアルが見つかりません。", "Material was not found.");
                return false;
            }

            mesh = NataneMeshAnalyzer.FindMeshForMaterial(material);
            if (mesh == null)
            {
                message = L("シーンにこのマテリアルを使用しているオブジェクトを配置してください。",
                            "Place an object using this material in the scene.");
                return false;
            }

            if (mesh.uv == null || mesh.uv.Length == 0)
            {
                message = L("メッシュに UV が設定されていません。", "Mesh has no UV data.");
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool TryGetMeshAndRenderer(Material material, out Mesh mesh, out Renderer renderer, out string message)
        {
            mesh = null;
            renderer = null;
            if (material == null)
            {
                message = L("マテリアルが見つかりません。", "Material was not found.");
                return false;
            }

            renderer = FindRendererForMaterial(material);
            if (renderer == null)
            {
                message = L("シーンにこのマテリアルを使用しているオブジェクトを配置してください。",
                            "Place an object using this material in the scene.");
                return false;
            }

            if (renderer is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
            }
            else if (renderer is MeshRenderer)
            {
                var mf = renderer.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }

            if (mesh == null)
            {
                message = L("メッシュが見つかりません。", "Mesh was not found.");
                return false;
            }

            if (mesh.uv == null || mesh.uv.Length == 0)
            {
                message = L("メッシュに UV が設定されていません。", "Mesh has no UV data.");
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static Renderer FindRendererForMaterial(Material mat)
        {
            if (mat == null) return null;
            var renderers = NataneEditorCompat.FindObjectsOfTypeCompat<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials == null) continue;
                foreach (var sharedMat in renderer.sharedMaterials)
                {
                    if (sharedMat == mat) return renderer;
                }
            }
            return null;
        }

        // ===== Curvature Calculation =====

        private static float[] ComputeVertexCurvature(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;

            // Build adjacency: for each vertex, collect neighbors
            var adjacency = new Dictionary<int, HashSet<int>>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                AddEdge(adjacency, a, b);
                AddEdge(adjacency, a, c);
                AddEdge(adjacency, b, c);
            }

            float[] curvature = new float[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!adjacency.TryGetValue(v, out HashSet<int> neighbors) || neighbors.Count == 0)
                {
                    curvature[v] = 0.5f;
                    continue;
                }

                float sum = 0f;
                foreach (int n in neighbors)
                {
                    sum += 1.0f - Vector3.Dot(normals[v], normals[n]);
                }
                // Mean curvature, remapped to 0.5 = flat
                float mean = sum / neighbors.Count;
                curvature[v] = Mathf.Clamp01(0.5f + mean * 0.5f);
            }

            return curvature;
        }

        private static void AddEdge(Dictionary<int, HashSet<int>> adjacency, int a, int b)
        {
            if (!adjacency.TryGetValue(a, out HashSet<int> setA))
            {
                setA = new HashSet<int>();
                adjacency[a] = setA;
            }
            setA.Add(b);

            if (!adjacency.TryGetValue(b, out HashSet<int> setB))
            {
                setB = new HashSet<int>();
                adjacency[b] = setB;
            }
            setB.Add(a);
        }

        // ===== Hemisphere Ray Sampling =====

        private static Vector3 GetHemisphereDirection(Vector3 normal, int index, int total)
        {
            // Fibonacci hemisphere sampling for uniform distribution
            float goldenRatio = (1.0f + Mathf.Sqrt(5.0f)) * 0.5f;
            float theta = 2.0f * Mathf.PI * index / goldenRatio;
            float cosPhiSq = 1.0f - (float)(index + 0.5f) / total;
            float cosPhi = Mathf.Sqrt(cosPhiSq);
            float sinPhi = Mathf.Sqrt(1.0f - cosPhiSq);

            Vector3 localDir = new Vector3(
                sinPhi * Mathf.Cos(theta),
                cosPhi,
                sinPhi * Mathf.Sin(theta));

            // Rotate from Y-up to normal direction
            return RotateToNormal(localDir, normal);
        }

        private static Vector3 RotateToNormal(Vector3 localDir, Vector3 normal)
        {
            Vector3 up = Mathf.Abs(normal.y) < 0.999f ? Vector3.up : Vector3.right;
            Vector3 tangent = Vector3.Cross(up, normal).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            return tangent * localDir.x + normal * localDir.y + bitangent * localDir.z;
        }

        // ===== Temporary Collider =====

        private static GameObject CreateTempCollider(Mesh mesh, Transform sourceTransform, out MeshCollider collider)
        {
            var go = new GameObject("_NataneTempCollider") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = sourceTransform.position;
            go.transform.rotation = sourceTransform.rotation;
            go.transform.localScale = sourceTransform.lossyScale;
            collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            return go;
        }

        // ===== UV Bake (Scalar via Compute) =====

        private static Texture2D BakeScalarToUV(Mesh mesh, float[] vertexData, int resolution)
        {
            ComputeShader uvBake = FindComputeShader("AutoMat_UVBake");
            if (uvBake == null) return BakeScalarToUVCPU(mesh, vertexData, resolution);

            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            int triCount = triangles.Length / 3;

            ComputeBuffer uvBuffer = new ComputeBuffer(uvs.Length, sizeof(float) * 2);
            ComputeBuffer triBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
            ComputeBuffer dataBuffer = new ComputeBuffer(vertexData.Length, sizeof(float));

            try
            {
                uvBuffer.SetData(uvs);
                triBuffer.SetData(triangles);
                dataBuffer.SetData(vertexData);

                RenderTexture rt = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
                rt.enableRandomWrite = true;
                rt.Create();

                try
                {
                    int kernel = uvBake.FindKernel("CSMain");
                    uvBake.SetInts("_TexSize", resolution, resolution);
                    uvBake.SetInt("_TriangleCount", triCount);
                    uvBake.SetBuffer(kernel, "_UVs", uvBuffer);
                    uvBake.SetBuffer(kernel, "_Triangles", triBuffer);
                    uvBake.SetBuffer(kernel, "_VertexData", dataBuffer);
                    uvBake.SetTexture(kernel, "_Result", rt);
                    uvBake.Dispatch(kernel, Mathf.CeilToInt(triCount / 64f), 1, 1);

                    return RTToTexture2D(rt, resolution);
                }
                finally
                {
                    RenderTexture.ReleaseTemporary(rt);
                }
            }
            finally
            {
                uvBuffer.Release();
                triBuffer.Release();
                dataBuffer.Release();
            }
        }

        // ===== UV Bake (Float3 via Compute) =====

        private static Texture2D BakeFloat3ToUV(Mesh mesh, Vector3[] vertexData, int resolution)
        {
            ComputeShader uvBake = FindComputeShader("NataneToon_UVBakeFloat3");
            if (uvBake == null) return BakeFloat3ToUVCPU(mesh, vertexData, resolution);

            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            int triCount = triangles.Length / 3;

            ComputeBuffer uvBuffer = new ComputeBuffer(uvs.Length, sizeof(float) * 2);
            ComputeBuffer triBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
            ComputeBuffer dataBuffer = new ComputeBuffer(vertexData.Length, sizeof(float) * 3);

            try
            {
                uvBuffer.SetData(uvs);
                triBuffer.SetData(triangles);
                dataBuffer.SetData(vertexData);

                RenderTexture rt = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
                rt.enableRandomWrite = true;
                rt.Create();

                try
                {
                    int kernel = uvBake.FindKernel("CSMain");
                    uvBake.SetInts("_TexSize", resolution, resolution);
                    uvBake.SetInt("_TriangleCount", triCount);
                    uvBake.SetBuffer(kernel, "_UVs", uvBuffer);
                    uvBake.SetBuffer(kernel, "_Triangles", triBuffer);
                    uvBake.SetBuffer(kernel, "_VertexData3", dataBuffer);
                    uvBake.SetTexture(kernel, "_Result", rt);
                    uvBake.Dispatch(kernel, Mathf.CeilToInt(triCount / 64f), 1, 1);

                    return RTToTexture2D(rt, resolution);
                }
                finally
                {
                    RenderTexture.ReleaseTemporary(rt);
                }
            }
            finally
            {
                uvBuffer.Release();
                triBuffer.Release();
                dataBuffer.Release();
            }
        }

        // ===== CPU Fallbacks =====

        private static Texture2D BakeScalarToUVCPU(Mesh mesh, float[] vertexData, int resolution)
        {
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            Color[] pixels = new Color[resolution * resolution];

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int i0 = triangles[t], i1 = triangles[t + 1], i2 = triangles[t + 2];
                RasterizeTriangleScalar(pixels, resolution,
                    uvs[i0], uvs[i1], uvs[i2],
                    vertexData[i0], vertexData[i1], vertexData[i2]);
            }

            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false, true);
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D BakeFloat3ToUVCPU(Mesh mesh, Vector3[] vertexData, int resolution)
        {
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            Color[] pixels = new Color[resolution * resolution];

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int i0 = triangles[t], i1 = triangles[t + 1], i2 = triangles[t + 2];
                RasterizeTriangleFloat3(pixels, resolution,
                    uvs[i0], uvs[i1], uvs[i2],
                    vertexData[i0], vertexData[i1], vertexData[i2]);
            }

            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false, true);
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static void RasterizeTriangleScalar(Color[] pixels, int res,
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            float d0, float d1, float d2)
        {
            Vector2 p0 = uv0 * res, p1 = uv1 * res, p2 = uv2 * res;
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))), 0, res - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))), 0, res - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))), 0, res - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))), 0, res - 1);

            float area = Cross2D(p1 - p0, p2 - p0);
            if (Mathf.Abs(area) < 0.001f) return;
            float invArea = 1.0f / area;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross2D(p1 - p0, p - p0) * invArea;
                    float w1 = Cross2D(p2 - p1, p - p1) * invArea;
                    float w2 = 1.0f - w0 - w1;

                    if (w0 >= -0.001f && w1 >= -0.001f && w2 >= -0.001f)
                    {
                        float val = d0 * w2 + d1 * w0 + d2 * w1;
                        pixels[y * res + x] = new Color(val, val, val, 1.0f);
                    }
                }
            }
        }

        private static void RasterizeTriangleFloat3(Color[] pixels, int res,
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            Vector3 d0, Vector3 d1, Vector3 d2)
        {
            Vector2 p0 = uv0 * res, p1 = uv1 * res, p2 = uv2 * res;
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))), 0, res - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))), 0, res - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))), 0, res - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))), 0, res - 1);

            float area = Cross2D(p1 - p0, p2 - p0);
            if (Mathf.Abs(area) < 0.001f) return;
            float invArea = 1.0f / area;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross2D(p1 - p0, p - p0) * invArea;
                    float w1 = Cross2D(p2 - p1, p - p1) * invArea;
                    float w2 = 1.0f - w0 - w1;

                    if (w0 >= -0.001f && w1 >= -0.001f && w2 >= -0.001f)
                    {
                        Vector3 val = d0 * w2 + d1 * w0 + d2 * w1;
                        pixels[y * res + x] = new Color(val.x, val.y, val.z, 1.0f);
                    }
                }
            }
        }

        private static float Cross2D(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // ===== Gaussian Blur =====

        private static Texture2D ApplyGaussianBlur(Texture2D input, int resolution, int blurRadius)
        {
            ComputeShader blurCS = FindComputeShader("AutoMat_GaussianBlur");
            if (blurCS == null) return input; // Skip blur if compute not available

            RenderTexture inputRT = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
            inputRT.enableRandomWrite = true;
            inputRT.Create();
            Graphics.Blit(input, inputRT);

            RenderTexture outputRT = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
            outputRT.enableRandomWrite = true;
            outputRT.Create();

            RenderTexture tempRT = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
            tempRT.enableRandomWrite = true;
            tempRT.Create();

            try
            {
                float sigma = Mathf.Max(blurRadius / 3f, 0.5f);

                int kH = blurCS.FindKernel("BlurH");
                blurCS.SetInts("_TexSize", resolution, resolution);
                blurCS.SetInt("_BlurRadius", blurRadius);
                blurCS.SetFloat("_Sigma", sigma);
                blurCS.SetTexture(kH, "_Input", inputRT);
                blurCS.SetTexture(kH, "_Result", tempRT);
                blurCS.Dispatch(kH, Mathf.CeilToInt(resolution / 8f), Mathf.CeilToInt(resolution / 8f), 1);

                int kV = blurCS.FindKernel("BlurV");
                blurCS.SetTexture(kV, "_Input", tempRT);
                blurCS.SetTexture(kV, "_Result", outputRT);
                blurCS.Dispatch(kV, Mathf.CeilToInt(resolution / 8f), Mathf.CeilToInt(resolution / 8f), 1);

                return RTToTexture2D(outputRT, resolution);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(inputRT);
                RenderTexture.ReleaseTemporary(outputRT);
                RenderTexture.ReleaseTemporary(tempRT);
            }
        }

        // ===== File I/O =====

        private static string GetOutputPath(Material material, string mapType)
        {
            string materialPath = AssetDatabase.GetAssetPath(material);
            string baseFolder = !string.IsNullOrEmpty(materialPath) && materialPath.StartsWith("Assets/")
                ? Path.GetDirectoryName(materialPath)?.Replace("\\", "/")
                : null;
            baseFolder = baseFolder ?? FallbackOutputRoot;

            string generatedFolder = EnsureFolderHierarchy($"{baseFolder}/NataneToon/Generated");
            string baseName = SanitizeFileName(material.name);
            return AssetDatabase.GenerateUniqueAssetPath($"{generatedFolder}/{baseName}_{mapType}.png");
        }

        private static void SaveTexture(Texture2D texture, string assetPath)
        {
            byte[] bytes = texture.EncodeToPNG();
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            string absolutePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllBytes(absolutePath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureGeneratedTexture(assetPath);
        }

        private static void ConfigureGeneratedTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

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

        private static string EnsureFolderHierarchy(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return assetPath;

            string[] segments = assetPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
            return current;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "NataneGenerated" : value;
        }

        // ===== Utility =====

        private static Texture2D RTToTexture2D(RenderTexture rt, int resolution)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            return tex;
        }

        private static ComputeShader FindComputeShader(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"t:ComputeShader {name}");
            if (guids.Length == 0) return null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }
    }
}
