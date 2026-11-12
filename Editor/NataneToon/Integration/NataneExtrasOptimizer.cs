using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// 「未使用の追加表現を省く」(_EXTRAS_OFF) の判定と一括設定。
    ///
    /// 本体シェーダーには、強さ 0 のあいだは何もしない追加表現（肌・顔・布・影の仕上げなど）が
    /// 約 25 個あり、キーワードではなく値で切り替わる。値が 0 でもシェーダーには命令が残るため、
    /// 使っていないマテリアルでは _EXTRAS_OFF でまとめて取り除ける
    /// （4K 全画面で前方パス 0.342 → 0.193 ms、RTX 5090 / D3D11 実測）。
    ///
    /// 既定はオフ。アニメーションで 0 から動かしているマテリアルを壊さないよう、自動では立てない。
    /// </summary>
    public static class NataneExtrasOptimizer
    {
        public const string Keyword = "_EXTRAS_OFF";
        public const string Property = "_ExtrasOff";

        /// <summary>_EXTRAS_OFF で取り除かれる機能のプロパティと、機能が無効になる値。</summary>
        public static readonly (string name, float offValue, string ja, string en)[] ExtrasProperties =
        {
            ("_MicroNormalStrength", 0f, "マイクロノーマル", "Micro Normal"),
            ("_SphereOccluderStrength", 0f, "球オクルーダー", "Sphere Occluder"),
            ("_DepthHairShadow", 0f, "深度ヘアシャドウ", "Depth Hair Shadow"),
            ("_NoseLineStrength", 0f, "鼻筋ライン", "Nose Line"),
            ("_Stocking", 0f, "ストッキング", "Stocking"),
            ("_Wetness", 0f, "濡れ", "Wetness"),
            ("_BentNormalStrength", 0f, "ベントノーマル", "Bent Normal"),
            ("_FaceProxyNormal", 0f, "顔プロキシ法線", "Face Proxy Normal"),
            ("_FaceFlatten", 0f, "顔の陰影フラット化", "Face Shading Flatten"),
            ("_FaceLightVerticalLimit", 0f, "顔ライトの上下制限", "Face Light Vertical Limit"),
            ("_LightSourceSize", 0f, "光源サイズ", "Light Source Size"),
            ("_ShadowOffsetMapStrength", 0f, "方向別シャドウオフセット", "Directional Shadow Offset"),
            ("_MatteDiffuse", 0f, "マット拡散 (EON)", "Matte Diffuse (EON)"),
            ("_CavityStrength", 0f, "キャビティ", "Cavity"),
            ("_ShadowAutoColor", 0f, "影色の自動生成", "Shadow Auto Color"),
            ("_ShadowHueShift", 0f, "影の色相シフト", "Shadow Hue Shift"),
            ("_ShadowSaturation", 1f, "影の彩度", "Shadow Saturation"),
            ("_ReflectionExposureOcclusion", 0f, "反射の露出オクルージョン", "Reflection Exposure Occlusion"),
            ("_ShadowBorderStrength", 0f, "影境界の色づけ", "Shadow Border"),
            ("_LitSphereStrength", 0f, "リットスフィア", "Lit Sphere"),
            ("_SkinTerminatorScatter", 0f, "肌の明暗境界の散乱", "Skin Terminator Scatter"),
            ("_SubsurfaceFlatten", 0f, "サブサーフェスのフラット化", "Subsurface Flatten"),
            ("_ClothSubsurface", 0f, "布のサブサーフェス", "Cloth Subsurface"),
            ("_LightMaxSoftKnee", 0f, "ライト上限のソフトニー", "Light Max Soft Knee"),
            ("_AmbientRimIntensity", 0f, "環境リム", "Ambient Rim"),
            ("_ClearCoatIntensity", 0f, "クリアコート", "Clear Coat"),
            ("_HighlightCompress", 0f, "ハイライト圧縮", "Highlight Compression"),
            ("_SpecularAAGeometric", 0f, "幾何スペキュラ AA", "Geometric Specular AA"),
        };

        private const float Epsilon = 0.001f;

        public static bool Supports(Material material)
        {
            return material != null && material.shader != null && material.HasProperty(Property);
        }

        public static bool IsStripped(Material material)
        {
            return Supports(material) && material.GetFloat(Property) >= 0.5f;
        }

        /// <summary>マテリアルの値として使われている追加表現の表示名。</summary>
        public static List<string> GetExtrasInUse(Material material)
        {
            var used = new List<string>();
            if (material == null) return used;
            foreach (var entry in ExtrasProperties)
            {
                if (!material.HasProperty(entry.name)) continue;
                if (Mathf.Abs(material.GetFloat(entry.name) - entry.offValue) > Epsilon)
                    used.Add(L(entry.ja, entry.en));
            }
            return used;
        }

        /// <summary>Undo 付きで _ExtrasOff とキーワードを揃える。</summary>
        public static void SetStripped(Material material, bool stripped)
        {
            if (!Supports(material)) return;
            Undo.RecordObject(material, L("未使用の追加表現を省く設定を変更", "Change Strip Unused Extras"));
            material.SetFloat(Property, stripped ? 1f : 0f);
            if (stripped) material.EnableKeyword(Keyword);
            else material.DisableKeyword(Keyword);
            EditorUtility.SetDirty(material);
        }

        // ------------------------------------------------------------------
        // 一括設定
        // ------------------------------------------------------------------

        [MenuItem(NataneToolMenuPaths.StripUnusedExtras, false, 2203)]
        private static void StripUnusedExtrasMenu()
        {
            List<Material> materials = CollectTargetMaterials(out bool fromSelection);
            if (materials.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    L("未使用の追加表現を省く", "Strip Unused Extras"),
                    L("対象になる Natane Toon のマテリアルが見つかりませんでした。", "No Natane Toon materials were found."),
                    "OK");
                return;
            }

            HashSet<string> animated = CollectAnimatedExtrasProperties();

            var targets = new List<Material>();
            int alreadyOn = 0, inUse = 0;
            foreach (Material material in materials)
            {
                if (IsStripped(material)) { alreadyOn++; continue; }
                if (GetExtrasInUse(material).Count > 0) { inUse++; continue; }
                targets.Add(material);
            }

            string scope = fromSelection
                ? L("選択中のマテリアル", "the selected materials")
                : L("プロジェクト内のすべてのマテリアル", "all materials in the project");
            string summary = L(
                $"対象: {scope}（{materials.Count} 個）\n\n" +
                $"・省く設定にする: {targets.Count} 個\n" +
                $"・すでに設定済み: {alreadyOn} 個\n" +
                $"・追加表現を使っているので変更しない: {inUse} 個",
                $"Scope: {scope} ({materials.Count})\n\n" +
                $"- Will be stripped: {targets.Count}\n" +
                $"- Already stripped: {alreadyOn}\n" +
                $"- Left alone because they use extras: {inUse}");
            if (animated.Count > 0)
            {
                string names = string.Join(", ", animated.OrderBy(n => n).Take(8));
                summary += L(
                    $"\n\n⚠ アニメーションクリップが次のプロパティを動かしています:\n{names}\n" +
                    "これらを動かす対象のマテリアルでは、省く設定にすると効果が出なくなります。該当するマテリアルはあとでインスペクターからオフに戻してください。",
                    $"\n\n⚠ Animation clips drive these properties:\n{names}\n" +
                    "On the materials they animate, stripping removes the effect. Turn the option back off in the inspector for those materials.");
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(L("未使用の追加表現を省く", "Strip Unused Extras"), summary, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    L("未使用の追加表現を省く", "Strip Unused Extras"),
                    summary,
                    L("適用する", "Apply"), L("キャンセル", "Cancel")))
                return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(L("未使用の追加表現を省く", "Strip Unused Extras"));
            foreach (Material material in targets) SetStripped(material, true);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            AssetDatabase.SaveAssets();
            Debug.Log($"[NataneToon] {L("未使用の追加表現を省く設定にしました", "Stripped unused extras")}: {targets.Count}");
        }

        private static List<Material> CollectTargetMaterials(out bool fromSelection)
        {
            var result = new List<Material>();
            var seen = new HashSet<Material>();
            void Add(Material material)
            {
                if (Supports(material) && seen.Add(material)) result.Add(material);
            }

            foreach (Object selected in Selection.objects)
            {
                if (selected is Material material) Add(material);
                else if (selected is GameObject go)
                    foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
                        foreach (Material shared in renderer.sharedMaterials) Add(shared);
            }
            fromSelection = result.Count > 0;
            if (fromSelection) return result;

            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // パッケージ内の読み取り専用マテリアルは書き換えない。
                if (!path.StartsWith("Assets/")) continue;
                Add(AssetDatabase.LoadAssetAtPath<Material>(path));
            }
            return result;
        }

        /// <summary>プロジェクト内のアニメーションクリップが動かしている追加表現のプロパティ名。</summary>
        public static HashSet<string> CollectAnimatedExtrasProperties()
        {
            var animated = new HashSet<string>();
            var names = new HashSet<string>(ExtrasProperties.Select(e => e.name));
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    if ((i & 63) == 0 && EditorUtility.DisplayCancelableProgressBar(
                            L("未使用の追加表現を省く", "Strip Unused Extras"),
                            L("アニメーションクリップを確認しています", "Checking animation clips"),
                            (float)i / guids.Length))
                        break;
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (clip == null) continue;
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        // マテリアルのプロパティは "material._Name" の形で入る。
                        const string prefix = "material.";
                        if (!binding.propertyName.StartsWith(prefix)) continue;
                        string property = binding.propertyName.Substring(prefix.Length);
                        int dot = property.IndexOf('.');
                        if (dot >= 0) property = property.Substring(0, dot);
                        if (names.Contains(property)) animated.Add(property);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return animated;
        }
    }
}
