#if VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using L = NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// VRChat アバターのアップロード時に、完全一致の重複テクスチャを統合するフック。
    ///
    /// SDK 非導入プロジェクトでは NataneToon.Editor.VRChat アセンブリごと
    /// コンパイル対象外になる（defineConstraints ["VRC_SDK_VRCSDK3"]）。
    ///
    /// 動作:
    ///   - キーワード同期フック (NataneVRChatBuildHook, order -50) の「後」に走る (order -40)。
    ///   - このアバターに含まれる Natane マテリアルのみを解析する。
    ///   - 完全一致 (byte-identical) の重複テクスチャのみ自動統合する。
    ///     近似一致はビルド時には絶対に統合しない。
    ///   - 統合は「マテリアルのインスタンスコピー」に対して行い、プロジェクト/シーンの
    ///     共有アセットは一切書き換えない（非破壊）。renderer.sharedMaterials を
    ///     コピー済みマテリアルに差し替えることでアップロード対象のみ変更する。
    ///
    /// 有効判定: NataneBuildPolicySettings.BuildTextureConsolidation（既定 true）。
    /// false にするとビルド時のテクスチャ統合を無効化できる。
    /// </summary>
    public sealed class NataneVRChatTextureConsolidation : IVRCSDKPreprocessAvatarCallback
    {
        /// <summary>ビルド時テクスチャ統合のオプトインキー（既定 true）。</summary>
        public const string OptInPrefKey = "NataneToon_BuildTextureConsolidation";

        // キーワード同期 (-50) の後に走らせる。
        public int callbackOrder => -40;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            try
            {
                // 有効判定は設定資産(buildTextureConsolidation, 既定 true)を正とする。
                if (!NataneBuildPolicySettings.instance.BuildTextureConsolidation)
                {
                    Debug.Log(L.L(
                        "[NataneToonShader] VRChat: テクスチャ統合はオプトアウトされています（スキップ）。",
                        "[NataneToonShader] VRChat: texture consolidation opted out (skipped)."));
                    return true;
                }

                ConsolidateExactDuplicates(avatarGameObject);
            }
            catch (System.Exception ex)
            {
                // フックの失敗でアップロードを止めない
                Debug.LogWarning($"[NataneToonShader] VRChat テクスチャ統合フックでエラー: {ex.Message}");
            }

            return true;
        }

        private static void ConsolidateExactDuplicates(GameObject avatarGameObject)
        {
            if (avatarGameObject == null)
                return;

            // このアバター配下の Natane マテリアルを収集（重複排除）。
            var renderers = avatarGameObject.GetComponentsInChildren<Renderer>(true);
            var avatarMaterials = new List<Material>();
            var seen = new HashSet<int>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null)
                        continue;
                    if (!NataneShaderCatalog.IsNataneShader(material.shader.name))
                        continue;
                    if (seen.Add(material.GetInstanceID()))
                        avatarMaterials.Add(material);
                }
            }

            if (avatarMaterials.Count == 0)
                return;

            // 完全一致のみ解析（近似一致はビルド時には絶対に行わない）。
            var result = NataneTextureConsolidator.Analyze(
                avatarMaterials, includeNearIdentical: false);

            Dictionary<string, Texture> remap =
                NataneTextureConsolidator.BuildExactDuplicateRemap(result);
            if (remap.Count == 0)
            {
                Debug.Log(L.L(
                    "[NataneToonShader] VRChat: 統合可能な重複テクスチャはありませんでした。",
                    "[NataneToonShader] VRChat: no duplicate textures to consolidate."));
                return;
            }

            // マテリアルのコピーを作り、そのコピー上でのみ付け替える（共有アセット非破壊）。
            var copyCache = new Dictionary<Material, Material>();
            int retargetedSlots = 0;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Material[] shared = renderer.sharedMaterials;
                bool rendererChanged = false;

                for (int i = 0; i < shared.Length; i++)
                {
                    Material original = shared[i];
                    if (original == null || original.shader == null)
                        continue;
                    if (!NataneShaderCatalog.IsNataneShader(original.shader.name))
                        continue;

                    // このマテリアルが置き換え対象テクスチャを参照しているか事前確認。
                    if (!ReferencesRemappedTexture(original, remap))
                        continue;

                    if (!copyCache.TryGetValue(original, out Material copy))
                    {
                        copy = new Material(original)
                        {
                            name = original.name + " (Natane Consolidated)",
                            hideFlags = HideFlags.DontSave,
                        };
                        // コピーに対して付け替え（Undo 不要・アセット保存なし）。
                        NataneTextureConsolidator.RetargetMaterialTextures(copy, remap, undo: false);
                        copyCache[original] = copy;
                    }

                    shared[i] = copy;
                    rendererChanged = true;
                    retargetedSlots++;
                }

                if (rendererChanged)
                    renderer.sharedMaterials = shared;
            }

            LogSummary(result, copyCache.Count, retargetedSlots);
        }

        private static bool ReferencesRemappedTexture(
            Material material, Dictionary<string, Texture> remap)
        {
            Shader shader = material.shader;
            int count = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(shader, i);
                Texture tex = material.GetTexture(propName);
                if (tex == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(tex);
                if (!string.IsNullOrEmpty(path) && remap.ContainsKey(path))
                    return true;
            }
            return false;
        }

        private static void LogSummary(
            NataneTextureConsolidator.ConsolidationResult result,
            int materialsCopied, int retargetedSlots)
        {
            var sb = new StringBuilder();
            sb.AppendLine(L.L(
                "[NataneToonShader] VRChat: テクスチャ統合を実行しました（このアップロードのみ・共有アセットは未変更）。",
                "[NataneToonShader] VRChat: consolidated textures for this upload only (shared assets untouched)."));
            sb.AppendLine(L.L(
                $"  重複グループ: {result.ExactDuplicateGroupCount}  統合したテクスチャ: {result.ExactDuplicateRedundantCount}",
                $"  Duplicate groups: {result.ExactDuplicateGroupCount}  Textures merged: {result.ExactDuplicateRedundantCount}"));
            sb.AppendLine(L.L(
                $"  コピーしたマテリアル: {materialsCopied}  付け替えたスロット: {retargetedSlots}",
                $"  Materials copied: {materialsCopied}  Slots retargeted: {retargetedSlots}"));
            sb.AppendLine(L.L(
                $"  推定 VRAM 削減: {NataneTextureConsolidator.FormatBytes(result.ExactDuplicateSavingsBytes)}",
                $"  Estimated VRAM saved: {NataneTextureConsolidator.FormatBytes(result.ExactDuplicateSavingsBytes)}"));
            Debug.Log(sb.ToString());
        }
    }
}
#endif
