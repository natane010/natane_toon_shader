using System.Collections.Generic;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// How eye triangles are identified on the source mesh.
    /// 目のポリゴンを特定する方法
    /// </summary>
    internal enum EyeSelectionMode
    {
        MaterialSlot = 0,
        UVRect = 1,
        TextureMask = 2
    }

    /// <summary>
    /// How detected UV islands are laid out inside the target rect.
    /// 検出した UV アイランドをターゲット矩形内へ配置する方法
    /// </summary>
    internal enum EyeIslandLayout
    {
        SingleBlock = 0,      // Treat the whole submesh as one block / 全体を1ブロックとして扱う
        OverlapLeftRight = 1, // Overlap left/right eyes on the same area / 左右の目を同じ領域に重ねる
        SideBySide = 2        // Place left/right eyes side by side / 左右の目を並べて配置する
    }

    /// <summary>
    /// Result details of a UV remap computation (localization-free, formatted by the window).
    /// UV 再配置計算の詳細情報（ローカライズはウィンドウ側で行う）
    /// </summary>
    internal class EyeUVRemapReport
    {
        public int islandCount;
        public int groupAVertexCount;
        public int groupBVertexCount;
        public int sharedVertexCount;   // vertices also used by other submeshes
        public bool fallbackToSingle;   // two-group layout requested but only one island found
        public bool seededFromUV0;      // target channel had no data, seeded from uv0
        public int remappedVertexCount;
    }

    /// <summary>
    /// Mesh operations used by the Eye Setup Tool: triangle selection,
    /// submesh extraction, UV island detection and UV relayout.
    /// Eye Setup Tool 用のメッシュ処理（三角形選択・サブメッシュ分離・UVアイランド検出・UV再配置）
    /// </summary>
    internal static class EyeSetupMeshUtility
    {
        private const float Epsilon = 1e-6f;

        // =====================================================================
        // Triangle enumeration
        // =====================================================================

        /// <summary>Total triangle count across all submeshes (triangle topology only).</summary>
        public static int TotalTriangleCount(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            int total = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                    continue;
                total += mesh.GetTriangles(s).Length / 3;
            }
            return total;
        }

        /// <summary>Per-submesh triangle index arrays. Non-triangle submeshes yield empty arrays.</summary>
        public static List<int[]> GetSubmeshTriangles(Mesh mesh)
        {
            var result = new List<int[]>();
            if (mesh == null)
                return result;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                {
                    result.Add(new int[0]);
                    continue;
                }
                result.Add(mesh.GetTriangles(s));
            }
            return result;
        }

        public static int CountSelected(bool[] selection)
        {
            if (selection == null)
                return 0;

            int count = 0;
            for (int i = 0; i < selection.Length; i++)
            {
                if (selection[i])
                    count++;
            }
            return count;
        }

        // =====================================================================
        // Selection
        // =====================================================================

        /// <summary>
        /// Select every triangle belonging to the given material slot (submesh).
        /// Returns one flag per triangle, ordered submesh by submesh.
        /// </summary>
        public static bool[] SelectByMaterialSlot(Mesh mesh, int slot)
        {
            if (mesh == null || slot < 0 || slot >= mesh.subMeshCount)
                return null;

            var subTris = GetSubmeshTriangles(mesh);
            var selection = new bool[TotalTriangleCount(mesh)];
            int cursor = 0;
            for (int s = 0; s < subTris.Count; s++)
            {
                int triCount = subTris[s].Length / 3;
                for (int t = 0; t < triCount; t++, cursor++)
                    selection[cursor] = (s == slot);
            }
            return selection;
        }

        /// <summary>
        /// Select triangles whose UV0 centroid falls inside the given UV rect.
        /// </summary>
        public static bool[] SelectByUVRect(Mesh mesh, Rect uvRect)
        {
            if (mesh == null)
                return null;

            Vector2[] uv = mesh.uv;
            if (uv == null || uv.Length == 0)
                return null;

            return SelectByCentroid(mesh, uv, centroid => uvRect.Contains(centroid));
        }

        /// <summary>
        /// Select triangles whose UV0 centroid samples above the threshold on the mask
        /// (grayscale, white = eye). The mask must already be CPU-readable
        /// (see <see cref="CreateReadableCopy"/>).
        /// </summary>
        public static bool[] SelectByTextureMask(Mesh mesh, Texture2D readableMask, float threshold)
        {
            if (mesh == null || readableMask == null)
                return null;

            Vector2[] uv = mesh.uv;
            if (uv == null || uv.Length == 0)
                return null;

            return SelectByCentroid(mesh, uv, centroid =>
            {
                float u = centroid.x - Mathf.Floor(centroid.x);
                float v = centroid.y - Mathf.Floor(centroid.y);
                Color c = readableMask.GetPixelBilinear(u, v);
                float value = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                return value >= threshold;
            });
        }

        private static bool[] SelectByCentroid(Mesh mesh, Vector2[] uv, System.Func<Vector2, bool> predicate)
        {
            var subTris = GetSubmeshTriangles(mesh);
            var selection = new bool[TotalTriangleCount(mesh)];
            int cursor = 0;

            for (int s = 0; s < subTris.Count; s++)
            {
                int[] tris = subTris[s];
                for (int t = 0; t < tris.Length; t += 3, cursor++)
                {
                    int i0 = tris[t];
                    int i1 = tris[t + 1];
                    int i2 = tris[t + 2];
                    if (i0 >= uv.Length || i1 >= uv.Length || i2 >= uv.Length)
                        continue;

                    Vector2 centroid = (uv[i0] + uv[i1] + uv[i2]) / 3f;
                    selection[cursor] = predicate(centroid);
                }
            }
            return selection;
        }

        // =====================================================================
        // Submesh extraction
        // =====================================================================

        /// <summary>
        /// Build a new mesh where the selected triangles are removed from their
        /// original submeshes and appended as a new last submesh.
        /// The source mesh is never modified (vertices, blend shapes, bind poses
        /// and bone weights are copied via Instantiate).
        /// 選択した三角形を元のサブメッシュから取り除き、新しい最終サブメッシュとして
        /// 追加した新規メッシュを生成します（元メッシュは変更しません）。
        /// </summary>
        public static Mesh ExtractSelectedTriangles(Mesh source, bool[] selection)
        {
            if (source == null || selection == null)
                return null;
            if (selection.Length != TotalTriangleCount(source))
                return null;

            Mesh result = Object.Instantiate(source);
            result.name = source.name + "_EyeSplit";

            var subTris = GetSubmeshTriangles(source);
            var keptPerSubmesh = new List<List<int>>();
            var extracted = new List<int>();
            int cursor = 0;

            for (int s = 0; s < subTris.Count; s++)
            {
                int[] tris = subTris[s];
                var kept = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3, cursor++)
                {
                    if (selection[cursor])
                    {
                        extracted.Add(tris[t]);
                        extracted.Add(tris[t + 1]);
                        extracted.Add(tris[t + 2]);
                    }
                    else
                    {
                        kept.Add(tris[t]);
                        kept.Add(tris[t + 1]);
                        kept.Add(tris[t + 2]);
                    }
                }
                keptPerSubmesh.Add(kept);
            }

            if (extracted.Count == 0)
            {
                Object.DestroyImmediate(result);
                return null;
            }

            int originalSubCount = subTris.Count;
            result.subMeshCount = originalSubCount + 1;
            for (int s = 0; s < originalSubCount; s++)
                result.SetTriangles(keptPerSubmesh[s], s, false);
            result.SetTriangles(extracted, originalSubCount, false);
            result.bounds = source.bounds;

            return result;
        }

        // =====================================================================
        // UV island detection
        // =====================================================================

        /// <summary>
        /// Find connected islands of the given submesh. Vertices at identical
        /// positions are welded so seam-split vertices stay in one island.
        /// Returns lists of vertex indices.
        /// </summary>
        public static List<List<int>> FindConnectedIslands(Mesh mesh, int submesh)
        {
            var islands = new List<List<int>>();
            if (mesh == null || submesh < 0 || submesh >= mesh.subMeshCount)
                return islands;
            if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                return islands;

            int[] tris = mesh.GetTriangles(submesh);
            if (tris.Length == 0)
                return islands;

            Vector3[] positions = mesh.vertices;

            // Weld vertices that share the same position (seam splits).
            var positionToRep = new Dictionary<Vector3, int>();
            var vertexToRep = new Dictionary<int, int>();
            foreach (int index in tris)
            {
                if (vertexToRep.ContainsKey(index))
                    continue;

                Vector3 pos = positions[index];
                int rep;
                if (!positionToRep.TryGetValue(pos, out rep))
                {
                    rep = index;
                    positionToRep.Add(pos, rep);
                }
                vertexToRep.Add(index, rep);
            }

            // Union-find over representatives via triangle connectivity.
            var parent = new Dictionary<int, int>();
            foreach (var kvp in vertexToRep)
            {
                if (!parent.ContainsKey(kvp.Value))
                    parent.Add(kvp.Value, kvp.Value);
            }

            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = vertexToRep[tris[t]];
                int b = vertexToRep[tris[t + 1]];
                int c = vertexToRep[tris[t + 2]];
                Union(parent, a, b);
                Union(parent, a, c);
            }

            // Collect original vertex indices per island root.
            var rootToIsland = new Dictionary<int, List<int>>();
            foreach (var kvp in vertexToRep)
            {
                int root = Find(parent, kvp.Value);
                List<int> island;
                if (!rootToIsland.TryGetValue(root, out island))
                {
                    island = new List<int>();
                    rootToIsland.Add(root, island);
                }
                island.Add(kvp.Key);
            }

            foreach (var kvp in rootToIsland)
                islands.Add(kvp.Value);
            return islands;
        }

        private static int Find(Dictionary<int, int> parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        private static void Union(Dictionary<int, int> parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);
            if (rootA != rootB)
                parent[rootB] = rootA;
        }

        // =====================================================================
        // UV relayout
        // =====================================================================

        /// <summary>
        /// Compute remapped UVs for the given submesh. Returns a full-length UV
        /// array (copy of the channel data with only the submesh vertices changed),
        /// or null when the input is invalid. Nothing is written to the mesh here.
        /// 指定サブメッシュの UV を再配置した結果を計算します（メッシュへは書き込みません）。
        /// </summary>
        public static Vector2[] ComputeRemappedUVs(
            Mesh mesh, int submesh, int uvChannel,
            Rect targetRect, float padding,
            EyeIslandLayout layout, bool mirrorSecondGroup, bool preserveAspect,
            out EyeUVRemapReport report)
        {
            report = new EyeUVRemapReport();

            if (mesh == null || submesh < 0 || submesh >= mesh.subMeshCount)
                return null;
            if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                return null;

            int[] tris = mesh.GetTriangles(submesh);
            if (tris.Length == 0)
                return null;

            // Read source channel; fall back to uv0 when the channel is empty.
            var uvList = new List<Vector2>();
            mesh.GetUVs(uvChannel, uvList);
            if (uvList.Count != mesh.vertexCount)
            {
                if (uvChannel == 0)
                    return null;

                mesh.GetUVs(0, uvList);
                if (uvList.Count != mesh.vertexCount)
                    return null;
                report.seededFromUV0 = true;
            }

            Vector2[] uv = uvList.ToArray();

            // Island detection and left/right grouping (by mesh-space X).
            List<List<int>> islands = FindConnectedIslands(mesh, submesh);
            report.islandCount = islands.Count;

            var groupA = new List<int>();
            var groupB = new List<int>();
            bool wantTwoGroups = (layout != EyeIslandLayout.SingleBlock);

            if (wantTwoGroups && islands.Count >= 2)
            {
                SplitIslandsLeftRight(islands, mesh.vertices, groupA, groupB);
                if (groupA.Count == 0 || groupB.Count == 0)
                {
                    report.fallbackToSingle = true;
                    wantTwoGroups = false;
                }
            }
            else if (wantTwoGroups)
            {
                report.fallbackToSingle = true;
                wantTwoGroups = false;
            }

            if (!wantTwoGroups)
            {
                groupA.Clear();
                groupB.Clear();
                foreach (List<int> island in islands)
                    groupA.AddRange(island);
            }

            report.groupAVertexCount = groupA.Count;
            report.groupBVertexCount = groupB.Count;
            report.sharedVertexCount = CountSharedVertices(mesh, submesh, tris);

            // Inner rect with padding.
            float pad = Mathf.Max(0f, padding);
            Rect inner = new Rect(
                targetRect.x + pad,
                targetRect.y + pad,
                Mathf.Max(Epsilon, targetRect.width - pad * 2f),
                Mathf.Max(Epsilon, targetRect.height - pad * 2f));

            Rect rectA, rectB;
            if (!wantTwoGroups || layout == EyeIslandLayout.OverlapLeftRight)
            {
                rectA = inner;
                rectB = inner;
            }
            else // SideBySide
            {
                float gap = pad;
                float halfWidth = Mathf.Max(Epsilon, (inner.width - gap) * 0.5f);
                rectA = new Rect(inner.x, inner.y, halfWidth, inner.height);
                rectB = new Rect(inner.x + halfWidth + gap, inner.y, halfWidth, inner.height);
            }

            RemapGroup(uv, groupA, rectA, preserveAspect, false);
            if (wantTwoGroups)
                RemapGroup(uv, groupB, rectB, preserveAspect, mirrorSecondGroup);

            report.remappedVertexCount = groupA.Count + groupB.Count;
            return uv;
        }

        /// <summary>
        /// Split islands into two groups by their mesh-space centroid X
        /// (left/right eyes on a character).
        /// </summary>
        private static void SplitIslandsLeftRight(
            List<List<int>> islands, Vector3[] positions,
            List<int> groupA, List<int> groupB)
        {
            int count = islands.Count;
            var centroidX = new float[count];
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                float sum = 0f;
                foreach (int v in islands[i])
                    sum += positions[v].x;
                centroidX[i] = islands[i].Count > 0 ? sum / islands[i].Count : 0f;
                min = Mathf.Min(min, centroidX[i]);
                max = Mathf.Max(max, centroidX[i]);
            }

            float mid = (min + max) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                if (centroidX[i] <= mid)
                    groupA.AddRange(islands[i]);
                else
                    groupB.AddRange(islands[i]);
            }
        }

        private static void RemapGroup(Vector2[] uv, List<int> vertices, Rect target, bool preserveAspect, bool mirrorU)
        {
            if (vertices.Count == 0)
                return;

            // Current UV bounds of the group.
            Vector2 boundsMin = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 boundsMax = new Vector2(float.MinValue, float.MinValue);
            foreach (int v in vertices)
            {
                boundsMin = Vector2.Min(boundsMin, uv[v]);
                boundsMax = Vector2.Max(boundsMax, uv[v]);
            }

            float srcWidth = Mathf.Max(Epsilon, boundsMax.x - boundsMin.x);
            float srcHeight = Mathf.Max(Epsilon, boundsMax.y - boundsMin.y);

            float scaleX = target.width / srcWidth;
            float scaleY = target.height / srcHeight;
            if (preserveAspect)
            {
                float s = Mathf.Min(scaleX, scaleY);
                scaleX = s;
                scaleY = s;
            }

            Vector2 srcCenter = (boundsMin + boundsMax) * 0.5f;
            Vector2 dstCenter = target.center;

            foreach (int v in vertices)
            {
                float u = dstCenter.x + (uv[v].x - srcCenter.x) * scaleX;
                float w = dstCenter.y + (uv[v].y - srcCenter.y) * scaleY;
                if (mirrorU)
                    u = dstCenter.x * 2f - u;
                uv[v] = new Vector2(u, w);
            }
        }

        /// <summary>
        /// Count vertices of the given submesh that are also referenced by other
        /// submeshes (relayout would move their UVs on both submeshes).
        /// </summary>
        private static int CountSharedVertices(Mesh mesh, int submesh, int[] submeshTris)
        {
            var own = new HashSet<int>(submeshTris);
            var shared = new HashSet<int>();

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (s == submesh || mesh.GetTopology(s) != MeshTopology.Triangles)
                    continue;
                foreach (int index in mesh.GetTriangles(s))
                {
                    if (own.Contains(index))
                        shared.Add(index);
                }
            }
            return shared.Count;
        }

        /// <summary>Write the given UVs to the mesh channel.</summary>
        public static void ApplyUVs(Mesh mesh, int uvChannel, Vector2[] uv)
        {
            if (mesh == null || uv == null || uv.Length != mesh.vertexCount)
                return;
            mesh.SetUVs(uvChannel, new List<Vector2>(uv));
        }

        // =====================================================================
        // Texture helpers
        // =====================================================================

        /// <summary>
        /// Create a CPU-readable copy of any texture (works with non-readable
        /// or compressed imports). Caller must DestroyImmediate the result.
        /// </summary>
        public static Texture2D CreateReadableCopy(Texture source)
        {
            if (source == null)
                return null;

            RenderTexture rt = RenderTexture.GetTemporary(
                source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }
    }
}
