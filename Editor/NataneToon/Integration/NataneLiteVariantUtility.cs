using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// 非 Lite バリアント → Lite バリアントへの変換判定と実行。
    ///
    /// 非 Lite バリアントは GrabPass（毎フレームの画面コピー）と
    /// hull / domain ステージ（テッセレーション OFF でもパススルーで実行される）を常に持つ。
    /// どちらも使っていないマテリアルは Lite に切り替えるだけで、見た目を変えずにその分のコストが消える。
    /// インスペクタのヒントと最適化ハブの一括変換が同じ判定を使うよう、ここを単一の入口にする。
    /// </summary>
    public static class NataneLiteVariantUtility
    {
        /// <summary>非 Lite シェーダー名 → Lite シェーダー名。</summary>
        public static readonly Dictionary<string, string> Counterparts = new Dictionary<string, string>
        {
            { "Natane/Toon Shader",               "Natane/Toon Shader (Lite)" },
            { "Natane/Toon Shader (Cutout)",      "Natane/Toon Shader (Cutout Lite)" },
            { "Natane/Toon Shader (Transparent)", "Natane/Toon Shader (Transparent Lite)" },
            { "Natane/Toon Shader (Fur)",         "Natane/Toon Shader (Fur Lite)" },
        };

        /// <summary>
        /// Lite に存在しない機能のトグル（float プロパティ名）。1 つでも ON なら変換しない。
        /// GrabPass 依存の 5 機能に加えてテッセレーションも含める（Lite は target 3.5 で hull / domain を持たない）。
        /// </summary>
        public static readonly string[] BlockingToggles =
        {
            "_Refraction",
            "_UseSoftFilter",
            "_UseKuwahara",
            "_UseColorBleeding",
            "_UseChromaticAberration",
            "_Tessellation",
        };

        /// <summary>
        /// Lite 版が存在し、かつ Lite に無い機能を 1 つも使っていなければ true。
        /// blockingProperty には変換を妨げている最初のトグル名が入る（無ければ null）。
        /// </summary>
        public static bool CanConvert(Material material, out string liteShaderName, out string blockingProperty)
        {
            liteShaderName = null;
            blockingProperty = null;
            if (material == null || material.shader == null) return false;
            if (!Counterparts.TryGetValue(material.shader.name, out liteShaderName)) return false;

            foreach (string prop in BlockingToggles)
            {
                if (material.HasProperty(prop) && material.GetFloat(prop) > 0.5f)
                {
                    blockingProperty = prop;
                    return false;
                }
            }
            return true;
        }

        public static bool CanConvert(Material material, out string liteShaderName)
        {
            string blocking;
            return CanConvert(material, out liteShaderName, out blocking);
        }

        /// <summary>
        /// 変換可能なものだけを Lite に切り替える（Undo 対応）。戻り値は変換した数。
        /// プロパティ値とキーワードはそのまま引き継がれ、手動設定の Render Queue も保持する。
        /// </summary>
        public static int ConvertToLite(IEnumerable<Material> materials)
        {
            if (materials == null) return 0;

            int converted = 0;
            string undoLabel = L("Lite バリアントに変換", "Convert to Lite Variant");
            foreach (Material material in materials)
            {
                string liteName;
                if (!CanConvert(material, out liteName)) continue;

                Shader liteShader = Shader.Find(liteName);
                if (liteShader == null)
                {
                    Debug.LogError("[NataneToonShader] " + L("Lite シェーダーが見つかりません: ", "Lite shader not found: ") + liteName);
                    continue;
                }

                // 手動で変更された Render Queue はシェーダー差し替え後も維持する。
                int queue = material.renderQueue;
                bool customQueue = queue != material.shader.renderQueue;

                Undo.RecordObject(material, undoLabel);
                material.shader = liteShader;
                if (customQueue) material.renderQueue = queue;
                EditorUtility.SetDirty(material);
                converted++;
            }
            return converted;
        }

        /// <summary>選択中の GameObject（子を含む）のレンダラーと、選択中のマテリアルアセットから収集する。</summary>
        public static List<Material> CollectFromSelection()
        {
            var result = new List<Material>();
            var seen = new HashSet<Material>();

            foreach (Object obj in Selection.objects)
            {
                var material = obj as Material;
                if (material != null && seen.Add(material)) result.Add(material);
            }

            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null) continue;
                foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && seen.Add(material)) result.Add(material);
                    }
                }
            }
            return result;
        }

        /// <summary>プロジェクト内（Assets 以下）の全マテリアルから、Lite 版を持つ Natane マテリアルを収集する。</summary>
        public static List<Material> CollectFromProject()
        {
            var result = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) continue;
                if (Counterparts.ContainsKey(material.shader.name)) result.Add(material);
            }
            return result;
        }
    }
}
