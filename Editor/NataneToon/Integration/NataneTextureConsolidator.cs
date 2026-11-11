using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace NataneToon.Editor
{
    /// <summary>
    /// ビルド / アップロード時のテクスチャ統合・最適化のコア。
    ///
    /// Natane マテリアルが参照するテクスチャを解析し、以下を検出する:
    ///   a. 完全一致 (byte-identical) の重複テクスチャ … 別アセットだが画素内容が同一
    ///   b. ほぼ一致 (near-identical)               … 32x32 に縮小した差分がしきい値未満（提案のみ）
    ///   c. 過大なマスク (*Mask / *MaskTex)         … 512px 超のマスク系テクスチャ（提案のみ）
    ///
    /// 「完全一致」のみ自動統合の対象。マテリアルのテクスチャ参照を 1 つの正規アセットへ
    /// 付け替える（Undo 対応・破壊的でない）。ほぼ一致は明示フラグ (applyNearIdentical) が
    /// 無い限り絶対に自動適用しない。
    ///
    /// このクラスは UI を持たない（Tools ワークストリームのウィンドウから呼び出せる公開 API）。
    /// </summary>
    public static class NataneTextureConsolidator
    {
        /// <summary>近似一致の平均絶対差のしきい値（0..255）。控えめな 2/255。</summary>
        public const int DefaultNearIdenticalThreshold = 2;

        /// <summary>過大マスク判定のサイズしきい値（px）。</summary>
        public const int OversizedMaskThreshold = 512;

        /// <summary>近似一致比較の縮小解像度。</summary>
        private const int NearIdenticalSampleSize = 32;

        // -----------------------------------------------------------------
        // シリアライズ可能な結果モデル（ウィンドウ側で保持・表示できる）
        // -----------------------------------------------------------------

        public enum ConsolidationKind
        {
            ExactDuplicate,   // 完全一致（自動統合対象）
            NearIdentical,    // ほぼ一致（提案のみ・明示確認で統合）
            OversizedMask,    // 過大なマスク（提案のみ）
        }

        [Serializable]
        public sealed class TextureRef
        {
            public string path;
            public string guid;
            public int width;
            public int height;
            public long estimatedBytes;
            public bool isCanonical;
        }

        [Serializable]
        public sealed class ConsolidationGroup
        {
            public ConsolidationKind kind;
            public string canonicalPath;       // 完全一致/近似一致で残す正規アセット
            public List<TextureRef> textures = new List<TextureRef>();
            public long estimatedSavingsBytes; // 統合で解放される推定 VRAM
            public string note;                // 提案系の説明文
        }

        [Serializable]
        public sealed class ConsolidationResult
        {
            public int materialsScanned;
            public int texturesScanned;
            public List<ConsolidationGroup> exactDuplicateGroups = new List<ConsolidationGroup>();
            public List<ConsolidationGroup> nearIdenticalGroups = new List<ConsolidationGroup>();
            public List<ConsolidationGroup> oversizedMaskGroups = new List<ConsolidationGroup>();

            public long ExactDuplicateSavingsBytes =>
                exactDuplicateGroups?.Sum(g => g.estimatedSavingsBytes) ?? 0;

            public int ExactDuplicateGroupCount => exactDuplicateGroups?.Count ?? 0;

            public int ExactDuplicateRedundantCount =>
                exactDuplicateGroups?.Sum(g => Math.Max(0, (g.textures?.Count ?? 0) - 1)) ?? 0;

            /// <summary>
            /// Analyze 実行時に捕捉したマテリアル集合（同一セッション内での Apply 用）。
            /// シリアライズはされないため、別セッションで復元した結果には materials を渡すこと。
            /// </summary>
            [NonSerialized] internal List<Material> capturedMaterials;
        }

        // -----------------------------------------------------------------
        // 公開 API
        // -----------------------------------------------------------------

        /// <summary>
        /// Natane マテリアル集合を解析し、テクスチャ統合の候補を返す（純粋・UI 無し）。
        /// </summary>
        /// <param name="materials">対象マテリアル（Natane シェーダーのみ処理）。</param>
        /// <param name="includeNearIdentical">
        /// 近似一致の検出を行うか。O(n^2) の縮小比較を伴い GPU blit が必要なため既定は false。
        /// </param>
        /// <param name="nearIdenticalThreshold">近似一致の平均絶対差しきい値（0..255）。</param>
        public static ConsolidationResult Analyze(
            IEnumerable<Material> materials,
            bool includeNearIdentical = false,
            int nearIdenticalThreshold = DefaultNearIdenticalThreshold)
        {
            var result = new ConsolidationResult();
            if (materials == null)
                return result;

            // 対象マテリアルを一意化（Natane のみ）
            var matList = new List<Material>();
            var seenMat = new HashSet<int>();
            foreach (Material m in materials)
            {
                if (m == null || m.shader == null)
                    continue;
                if (!NataneShaderCatalog.IsNataneShader(m.shader.name))
                    continue;
                if (!seenMat.Add(m.GetInstanceID()))
                    continue;
                matList.Add(m);
            }
            result.materialsScanned = matList.Count;
            result.capturedMaterials = matList;

            // テクスチャ参照を収集: texture -> 参照した (material, prop)
            var refs = CollectTextureReferences(matList,
                out Dictionary<Texture2D, HashSet<string>> maskBindings);

            result.texturesScanned = refs.Count;

            var textures = refs.Keys.ToList();

            // a. 完全一致の検出
            DetectExactDuplicates(textures, result);

            // c. 過大マスクの検出
            DetectOversizedMasks(textures, maskBindings, result);

            // b. 近似一致の検出（任意）
            if (includeNearIdentical)
                DetectNearIdentical(textures, result, nearIdenticalThreshold);

            return result;
        }

        /// <summary>
        /// 完全一致グループについて、全マテリアルの参照を正規アセットへ付け替える。
        /// Analyze 時に捕捉したマテリアルに対して適用する（同一セッション用）。
        /// </summary>
        /// <returns>付け替えたプロパティスロット数。</returns>
        public static int ApplyExactDuplicates(ConsolidationResult result, bool undo = true)
        {
            if (result == null)
                return 0;
            return ApplyExactDuplicates(result, result.capturedMaterials, undo);
        }

        /// <summary>
        /// 完全一致グループについて、指定マテリアル集合の参照を正規アセットへ付け替える。
        /// </summary>
        public static int ApplyExactDuplicates(
            ConsolidationResult result, IEnumerable<Material> materials, bool undo)
        {
            if (result == null || materials == null)
                return 0;

            Dictionary<string, Texture> remap = BuildExactDuplicateRemap(result);
            if (remap.Count == 0)
                return 0;

            int changed = 0;
            var dirtied = new List<Material>();

            foreach (Material material in materials)
            {
                if (material == null || material.shader == null)
                    continue;
                if (!NataneShaderCatalog.IsNataneShader(material.shader.name))
                    continue;

                int matChanges = RetargetMaterialTextures(material, remap, undo);
                if (matChanges > 0)
                {
                    changed += matChanges;
                    dirtied.Add(material);
                }
            }

            if (dirtied.Count > 0)
            {
                foreach (Material m in dirtied)
                    EditorUtility.SetDirty(m);
                AssetDatabase.SaveAssets();
            }

            return changed;
        }

        /// <summary>
        /// 完全一致の結果から「非正規テクスチャのパス → 正規 Texture」の対応表を作る。
        /// Apply / VRChat フックの双方から利用する。
        /// </summary>
        public static Dictionary<string, Texture> BuildExactDuplicateRemap(ConsolidationResult result)
        {
            var remap = new Dictionary<string, Texture>(StringComparer.Ordinal);
            if (result?.exactDuplicateGroups == null)
                return remap;

            foreach (ConsolidationGroup group in result.exactDuplicateGroups)
            {
                if (group?.textures == null || string.IsNullOrEmpty(group.canonicalPath))
                    continue;

                var canonical = AssetDatabase.LoadAssetAtPath<Texture>(group.canonicalPath);
                if (canonical == null)
                    continue;

                foreach (TextureRef tr in group.textures)
                {
                    if (tr == null || tr.isCanonical || string.IsNullOrEmpty(tr.path))
                        continue;
                    if (string.Equals(tr.path, group.canonicalPath, StringComparison.Ordinal))
                        continue;
                    remap[tr.path] = canonical;
                }
            }

            return remap;
        }

        /// <summary>
        /// 1 マテリアルのテクスチャスロットを対応表に従って付け替える。
        /// remap のキーは「置き換え元テクスチャのアセットパス」。
        /// </summary>
        /// <returns>付け替えたスロット数。</returns>
        public static int RetargetMaterialTextures(
            Material material, IReadOnlyDictionary<string, Texture> remap, bool undo)
        {
            if (material == null || material.shader == null || remap == null || remap.Count == 0)
                return 0;

            Shader shader = material.shader;
            int count = ShaderUtil.GetPropertyCount(shader);
            int changed = 0;

            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(shader, i);
                Texture current = material.GetTexture(propName);
                if (current == null)
                    continue;

                string curPath = AssetDatabase.GetAssetPath(current);
                if (string.IsNullOrEmpty(curPath))
                    continue;

                if (!remap.TryGetValue(curPath, out Texture canonical) || canonical == null)
                    continue;
                if (canonical == current)
                    continue;

                if (undo)
                    Undo.RecordObject(material, "Natane Texture Consolidation");

                material.SetTexture(propName, canonical);
                changed++;
            }

            return changed;
        }

        // -----------------------------------------------------------------
        // 参照収集
        // -----------------------------------------------------------------

        private static Dictionary<Texture2D, HashSet<string>> CollectTextureReferences(
            List<Material> materials,
            out Dictionary<Texture2D, HashSet<string>> maskBindings)
        {
            var refs = new Dictionary<Texture2D, HashSet<string>>();
            maskBindings = new Dictionary<Texture2D, HashSet<string>>();

            foreach (Material material in materials)
            {
                Shader shader = material.shader;
                int count = ShaderUtil.GetPropertyCount(shader);
                for (int i = 0; i < count; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                        continue;

                    string propName = ShaderUtil.GetPropertyName(shader, i);
                    var tex = material.GetTexture(propName) as Texture2D;
                    if (tex == null)
                        continue;

                    if (!refs.TryGetValue(tex, out HashSet<string> props))
                    {
                        props = new HashSet<string>(StringComparer.Ordinal);
                        refs[tex] = props;
                    }
                    props.Add(propName);

                    if (IsMaskProperty(propName))
                    {
                        if (!maskBindings.TryGetValue(tex, out HashSet<string> mprops))
                        {
                            mprops = new HashSet<string>(StringComparer.Ordinal);
                            maskBindings[tex] = mprops;
                        }
                        mprops.Add(propName);
                    }
                }
            }

            return refs;
        }

        private static bool IsMaskProperty(string propName)
        {
            if (string.IsNullOrEmpty(propName))
                return false;
            // *Mask / *MaskTex（例: _RimMask, _FaceOrthoMaskTex）を対象にする。
            return propName.EndsWith("Mask", StringComparison.OrdinalIgnoreCase) ||
                   propName.EndsWith("MaskTex", StringComparison.OrdinalIgnoreCase) ||
                   propName.EndsWith("MaskTexture", StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------
        // a. 完全一致
        // -----------------------------------------------------------------

        private static void DetectExactDuplicates(List<Texture2D> textures, ConsolidationResult result)
        {
            // まず (width,height) でバケット化。異なる寸法は byte-identical になり得ない。
            var buckets = new Dictionary<long, List<Texture2D>>();
            foreach (Texture2D tex in textures)
            {
                if (tex == null)
                    continue;
                long key = ((long)tex.width << 32) | (uint)tex.height;
                if (!buckets.TryGetValue(key, out List<Texture2D> list))
                {
                    list = new List<Texture2D>();
                    buckets[key] = list;
                }
                list.Add(tex);
            }

            foreach (List<Texture2D> bucket in buckets.Values)
            {
                if (bucket.Count < 2)
                    continue;

                // 内容ハッシュでグループ化
                var byHash = new Dictionary<string, List<Texture2D>>(StringComparer.Ordinal);
                foreach (Texture2D tex in bucket)
                {
                    string hash = ComputeContentHash(tex);
                    if (hash == null)
                        continue;
                    if (!byHash.TryGetValue(hash, out List<Texture2D> list))
                    {
                        list = new List<Texture2D>();
                        byHash[hash] = list;
                    }
                    list.Add(tex);
                }

                foreach (List<Texture2D> group in byHash.Values)
                {
                    if (group.Count < 2)
                        continue;

                    // 別アセット（パスが異なる）のみを対象にする
                    var distinctPaths = group
                        .Select(t => AssetDatabase.GetAssetPath(t))
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToList();

                    if (distinctPaths.Count < 2)
                        continue;

                    string canonicalPath = distinctPaths[0];
                    var cg = new ConsolidationGroup
                    {
                        kind = ConsolidationKind.ExactDuplicate,
                        canonicalPath = canonicalPath,
                    };

                    long savings = 0;
                    foreach (string path in distinctPaths)
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        long bytes = EstimateTextureBytes(tex);
                        bool isCanonical = string.Equals(path, canonicalPath, StringComparison.Ordinal);
                        cg.textures.Add(new TextureRef
                        {
                            path = path,
                            guid = AssetDatabase.AssetPathToGUID(path),
                            width = tex != null ? tex.width : 0,
                            height = tex != null ? tex.height : 0,
                            estimatedBytes = bytes,
                            isCanonical = isCanonical,
                        });
                        if (!isCanonical)
                            savings += bytes;
                    }

                    cg.estimatedSavingsBytes = savings;
                    result.exactDuplicateGroups.Add(cg);
                }
            }
        }

        // -----------------------------------------------------------------
        // c. 過大マスク
        // -----------------------------------------------------------------

        private static void DetectOversizedMasks(
            List<Texture2D> textures,
            Dictionary<Texture2D, HashSet<string>> maskBindings,
            ConsolidationResult result)
        {
            foreach (KeyValuePair<Texture2D, HashSet<string>> kv in maskBindings)
            {
                Texture2D tex = kv.Key;
                if (tex == null)
                    continue;
                if (tex.width <= OversizedMaskThreshold && tex.height <= OversizedMaskThreshold)
                    continue;

                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path))
                    continue;

                var cg = new ConsolidationGroup
                {
                    kind = ConsolidationKind.OversizedMask,
                    canonicalPath = path,
                    note = NataneToonLocalization.L(
                        $"マスク ({string.Join(", ", kv.Value)}) が {tex.width}x{tex.height} → {OversizedMaskThreshold}px 以下へ縮小を検討",
                        $"mask ({string.Join(", ", kv.Value)}) is {tex.width}x{tex.height} → consider downscaling to <= {OversizedMaskThreshold}px"),
                };
                cg.textures.Add(new TextureRef
                {
                    path = path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    width = tex.width,
                    height = tex.height,
                    estimatedBytes = EstimateTextureBytes(tex),
                    isCanonical = true,
                });
                result.oversizedMaskGroups.Add(cg);
            }
        }

        // -----------------------------------------------------------------
        // b. 近似一致
        // -----------------------------------------------------------------

        private static void DetectNearIdentical(
            List<Texture2D> textures, ConsolidationResult result, int threshold)
        {
            // 完全一致で既にグループ化済みのパスは除外する。
            var exactPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (ConsolidationGroup g in result.exactDuplicateGroups)
                foreach (TextureRef tr in g.textures)
                    exactPaths.Add(tr.path);

            // 縮小サンプルを事前計算
            var samples = new Dictionary<Texture2D, Color32[]>();
            var candidates = new List<Texture2D>();
            foreach (Texture2D tex in textures)
            {
                if (tex == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path) || exactPaths.Contains(path))
                    continue;

                Color32[] sample = SampleDownscaled(tex, NearIdenticalSampleSize);
                if (sample == null)
                    continue;
                samples[tex] = sample;
                candidates.Add(tex);
            }

            // union-find でペアをグループ化
            var parent = new Dictionary<Texture2D, Texture2D>();
            foreach (Texture2D t in candidates)
                parent[t] = t;

            Func<Texture2D, Texture2D> find = null;
            find = t =>
            {
                while (!ReferenceEquals(parent[t], t))
                {
                    parent[t] = parent[parent[t]];
                    t = parent[t];
                }
                return t;
            };

            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    Texture2D a = candidates[i];
                    Texture2D b = candidates[j];

                    if (!SameAspectRatio(a, b))
                        continue;

                    float diff = MeanAbsDiff(samples[a], samples[b]);
                    if (diff <= threshold)
                    {
                        Texture2D ra = find(a);
                        Texture2D rb = find(b);
                        if (!ReferenceEquals(ra, rb))
                            parent[ra] = rb;
                    }
                }
            }

            // グループ集約
            var grouped = new Dictionary<Texture2D, List<Texture2D>>();
            foreach (Texture2D t in candidates)
            {
                Texture2D root = find(t);
                if (!grouped.TryGetValue(root, out List<Texture2D> list))
                {
                    list = new List<Texture2D>();
                    grouped[root] = list;
                }
                list.Add(t);
            }

            foreach (List<Texture2D> group in grouped.Values)
            {
                if (group.Count < 2)
                    continue;

                var distinctPaths = group
                    .Select(t => AssetDatabase.GetAssetPath(t))
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToList();

                if (distinctPaths.Count < 2)
                    continue;

                string canonicalPath = distinctPaths[0];
                var cg = new ConsolidationGroup
                {
                    kind = ConsolidationKind.NearIdentical,
                    canonicalPath = canonicalPath,
                    note = NataneToonLocalization.L(
                        "ほぼ一致（提案のみ・明示確認が必要）",
                        "near-identical (advisory; requires explicit confirmation)"),
                };

                long savings = 0;
                foreach (string path in distinctPaths)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    long bytes = EstimateTextureBytes(tex);
                    bool isCanonical = string.Equals(path, canonicalPath, StringComparison.Ordinal);
                    cg.textures.Add(new TextureRef
                    {
                        path = path,
                        guid = AssetDatabase.AssetPathToGUID(path),
                        width = tex != null ? tex.width : 0,
                        height = tex != null ? tex.height : 0,
                        estimatedBytes = bytes,
                        isCanonical = isCanonical,
                    });
                    if (!isCanonical)
                        savings += bytes;
                }

                cg.estimatedSavingsBytes = savings;
                result.nearIdenticalGroups.Add(cg);
            }
        }

        private static bool SameAspectRatio(Texture2D a, Texture2D b)
        {
            if (a == null || b == null || a.height == 0 || b.height == 0)
                return false;
            float ra = (float)a.width / a.height;
            float rb = (float)b.width / b.height;
            return Mathf.Abs(ra - rb) < 0.01f;
        }

        private static float MeanAbsDiff(Color32[] a, Color32[] b)
        {
            if (a == null || b == null || a.Length == 0 || a.Length != b.Length)
                return float.MaxValue;

            long total = 0;
            for (int i = 0; i < a.Length; i++)
            {
                total += Math.Abs(a[i].r - b[i].r);
                total += Math.Abs(a[i].g - b[i].g);
                total += Math.Abs(a[i].b - b[i].b);
                total += Math.Abs(a[i].a - b[i].a);
            }
            return total / (float)(a.Length * 4);
        }

        // -----------------------------------------------------------------
        // テクスチャユーティリティ
        // -----------------------------------------------------------------

        /// <summary>
        /// テクスチャの画素内容ハッシュ。imageContentsHash が取得できればそれを、
        /// 取得できなければ CPU 読み取り可能コピーの生データ SHA1 を用いる。
        /// </summary>
        private static string ComputeContentHash(Texture2D tex)
        {
            if (tex == null)
                return null;

            string path = AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(path) &&
                TryGetImageContentsHash(path, out string importerHash))
            {
                return "IC:" + importerHash;
            }

            byte[] raw = GetContentBytes(tex);
            if (raw == null)
                return null;

            using (var sha1 = SHA1.Create())
            {
                byte[] digest = sha1.ComputeHash(raw);
                var sb = new StringBuilder(digest.Length * 2 + 8);
                sb.Append("SHA1:").Append(tex.width).Append('x').Append(tex.height).Append(':');
                foreach (byte b in digest)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// TextureImporter が imageContentsHash を公開していればリフレクションで取得する。
        /// 未対応の Unity バージョンでは false を返し、SHA1 フォールバックに委ねる。
        /// </summary>
        private static bool TryGetImageContentsHash(string path, out string hash)
        {
            hash = null;
            try
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    return false;

                var prop = typeof(TextureImporter).GetProperty(
                    "imageContentsHash",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop == null)
                    return false;

                object value = prop.GetValue(importer);
                if (value == null)
                    return false;

                string s = value.ToString();
                if (string.IsNullOrEmpty(s) || s == "00000000000000000000000000000000")
                    return false;

                hash = s;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 生の画素バイト列を取得する。読み取り可能ならそのまま、そうでなければ
        /// RenderTexture blit で CPU 読み取り可能コピーを作って取得する
        /// （EyeSetupMeshUtility.CreateReadableCopy と同じ手法）。
        /// </summary>
        private static byte[] GetContentBytes(Texture2D tex)
        {
            if (tex == null)
                return null;

            if (tex.isReadable)
            {
                try
                {
                    byte[] raw = tex.GetRawTextureData();
                    if (raw != null && raw.Length > 0)
                        return raw;
                }
                catch
                {
                    // フォールバックへ
                }
            }

            Texture2D copy = CreateReadableCopy(tex);
            if (copy == null)
                return null;

            try
            {
                return copy.GetRawTextureData();
            }
            catch
            {
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        /// <summary>
        /// 任意のテクスチャ（非読み取り/圧縮でも可）の CPU 読み取り可能コピーを作る。
        /// 呼び出し側で DestroyImmediate すること。EyeSetupMeshUtility と同じ手法。
        /// </summary>
        private static Texture2D CreateReadableCopy(Texture source)
        {
            if (source == null)
                return null;

            RenderTexture rt = RenderTexture.GetTemporary(
                source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                return copy;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// テクスチャを size x size に縮小した Color32 サンプルを返す（近似一致比較用）。
        /// </summary>
        private static Color32[] SampleDownscaled(Texture source, int size)
        {
            if (source == null || size <= 0)
                return null;

            RenderTexture rt = RenderTexture.GetTemporary(
                size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                copy.Apply();
                return copy.GetPixels32();
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                if (copy != null)
                    UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        /// <summary>
        /// テクスチャの推定 VRAM 使用量（バイト）。Profiler の実測値を優先し、
        /// 取得できない場合は width*height*bpp で概算する。
        /// </summary>
        public static long EstimateTextureBytes(Texture tex)
        {
            if (tex == null)
                return 0;

            try
            {
                long runtime = Profiler.GetRuntimeMemorySizeLong(tex);
                if (runtime > 0)
                    return runtime;
            }
            catch
            {
                // 概算へフォールバック
            }

            // 概算: RGBA32 相当 4bpp（圧縮は考慮しない上限寄りの見積り）。
            long pixels = (long)Mathf.Max(1, tex.width) * Mathf.Max(1, tex.height);
            return pixels * 4;
        }

        /// <summary>
        /// バイト数を人間可読な文字列にする（レポート用）。
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
                return "0 B";
            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024.0 && unit < units.Length - 1)
            {
                value /= 1024.0;
                unit++;
            }
            return $"{value:0.##} {units[unit]}";
        }
    }
}
