// URP 17 (Unity 6) 以降でのみコンパイルされる (NataneToon.Editor.URP.asmdef の defineConstraints)。
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NataneToon.Editor
{
    /// <summary>
    /// URP プロジェクト向けのセットアップ補助。
    /// ・Screen FX の Renderer Feature を、いま使われている Renderer に追加する
    /// ・シーン内のマテリアルが URP で描けるバリアントかどうかを確認する
    /// </summary>
    public static class NataneURPSetupTool
    {
        const string MenuRoot = "Tools/Natane/URP/";
        const string ScreenFXShaderName = "Natane/Screen FX Overlay";

        static string L(string ja, string en)
        {
            return NataneToonLocalization.L(ja, en);
        }

        static string Title
        {
            get { return L("Natane Toon - URP", "Natane Toon - URP"); }
        }

        // ------------------------------------------------------------ Screen FX

        [MenuItem(MenuRoot + "Screen FX を Renderer に追加 Add Screen FX Renderer Feature", false, 80)]
        static void AddScreenFXFeature()
        {
            ScriptableRendererData rendererData = FindActiveRendererData(out string problem);
            if (rendererData == null)
            {
                EditorUtility.DisplayDialog(Title, problem, "OK");
                return;
            }

            Material material = FindSelectedScreenFXMaterial();

            NataneScreenFXRendererFeature existing = FindFeature(rendererData);
            if (existing != null)
            {
                if (material != null && existing.material != material)
                {
                    Undo.RecordObject(existing, "Assign Natane Screen FX Material");
                    existing.material = material;
                    EditorUtility.SetDirty(existing);
                    rendererData.SetDirty();
                }
                Selection.activeObject = rendererData;
                EditorGUIUtility.PingObject(rendererData);
                EditorUtility.DisplayDialog(Title,
                    L("この Renderer には既に Natane Screen FX が追加されています。\n\n" + rendererData.name,
                      "This renderer already has a Natane Screen FX feature.\n\n" + rendererData.name),
                    "OK");
                return;
            }

            if (material == null)
            {
                material = CreateScreenFXMaterial();
                if (material == null)
                {
                    EditorUtility.DisplayDialog(Title,
                        L("シェーダー「" + ScreenFXShaderName + "」が見つかりません。パッケージが正しくインポートされているか確認してください。",
                          "The shader \"" + ScreenFXShaderName + "\" was not found. Check that the package imported correctly."),
                        "OK");
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<NataneScreenFXRendererFeature>();
            feature.name = "Natane Screen FX";
            feature.material = material;
            Undo.RegisterCreatedObjectUndo(feature, "Add Natane Screen FX");
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            // Renderer Feature の一覧は、参照のリストとローカル ID のリストの 2 本で持たれている
            // (URP の Renderer インスペクターと同じ手順で両方に足す)。
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            var serialized = new SerializedObject(rendererData);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serialized.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
            {
                Undo.DestroyObjectImmediate(feature);
                EditorUtility.DisplayDialog(Title,
                    L("この URP バージョンでは自動で追加できませんでした。Renderer のインスペクターで「Add Renderer Feature > Natane Screen FX Renderer Feature」を選んでください。",
                      "Could not add the feature automatically on this URP version. In the renderer inspector choose \"Add Renderer Feature > Natane Screen FX Renderer Feature\"."),
                    "OK");
                return;
            }

            serialized.Update();
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedProperties();

            rendererData.SetDirty();
            AssetDatabase.SaveAssets();

            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
            EditorUtility.DisplayDialog(Title,
                L("Natane Screen FX を追加しました。\n\nRenderer: " + rendererData.name + "\nマテリアル: " + material.name +
                  "\n\n効果の設定は選択中のマテリアルで行います。Effect Blend が 0 のあいだはパスごと省かれます。",
                  "Added Natane Screen FX.\n\nRenderer: " + rendererData.name + "\nMaterial: " + material.name +
                  "\n\nTune the effect on the selected material. While Effect Blend is 0 the pass is skipped entirely."),
                "OK");
        }

        static NataneScreenFXRendererFeature FindFeature(ScriptableRendererData rendererData)
        {
            List<ScriptableRendererFeature> features = rendererData.rendererFeatures;
            if (features == null) return null;
            for (int i = 0; i < features.Count; i++)
            {
                var feature = features[i] as NataneScreenFXRendererFeature;
                if (feature != null) return feature;
            }
            return null;
        }

        static Material FindSelectedScreenFXMaterial()
        {
            var material = Selection.activeObject as Material;
            if (material == null || material.shader == null) return null;
            return material.shader.name == ScreenFXShaderName ? material : null;
        }

        static Material CreateScreenFXMaterial()
        {
            Shader shader = Shader.Find(ScreenFXShaderName);
            if (shader == null) return null;
            var material = new Material(shader) { name = "Natane Screen FX (URP)" };
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Natane Screen FX (URP).mat");
            AssetDatabase.CreateAsset(material, path);
            Undo.RegisterCreatedObjectUndo(material, "Create Natane Screen FX Material");
            return material;
        }

        /// <summary>いまの Quality で使われている URP Asset の既定の Renderer を返す。</summary>
        static ScriptableRendererData FindActiveRendererData(out string problem)
        {
            problem = null;
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
            {
                problem = L("URP が有効になっていません。Project Settings > Graphics (または Quality) で URP Asset を割り当ててください。",
                            "URP is not active. Assign a URP Asset in Project Settings > Graphics (or Quality).");
                return null;
            }

            var serialized = new SerializedObject(asset);
            SerializedProperty list = serialized.FindProperty("m_RendererDataList");
            SerializedProperty defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
            ScriptableRendererData data = null;
            if (list != null && list.isArray && list.arraySize > 0)
            {
                int index = defaultIndex != null ? Mathf.Clamp(defaultIndex.intValue, 0, list.arraySize - 1) : 0;
                data = list.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
            }

            if (data == null)
            {
                problem = L("URP Asset「" + asset.name + "」に Renderer が設定されていません。",
                            "The URP Asset \"" + asset.name + "\" has no renderer assigned.");
            }
            return data;
        }

        // ------------------------------------------------------------ 対応状況の確認

        [MenuItem(MenuRoot + "シーンの URP 対応を確認 Check Scene for URP Support", false, 81)]
        static void CheckScene()
        {
            var report = new StringBuilder();
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            report.AppendLine(asset != null
                ? L("レンダーパイプライン: URP (" + asset.name + ")", "Render pipeline: URP (" + asset.name + ")")
                : L("レンダーパイプライン: URP ではありません", "Render pipeline: not URP"));

            // URP の SubShader を持たない Natane シェーダー (Built-in RP 専用バリアント) を使うマテリアルを集める。
            var unsupported = new SortedDictionary<string, List<Object>>(System.StringComparer.Ordinal);
            var supportCache = new Dictionary<Shader, bool>();
            var seen = new HashSet<Material>();
            int natane = 0;

            Renderer[] renderers = NataneEditorCompat.FindObjectsOfTypeCompat<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null || !seen.Add(material)) continue;
                    Shader shader = material.shader;
                    if (!shader.name.StartsWith("Natane/")) continue;
                    natane++;

                    if (!supportCache.TryGetValue(shader, out bool supported))
                    {
                        supported = HasURPSubShader(shader);
                        supportCache[shader] = supported;
                    }
                    if (supported) continue;

                    if (!unsupported.TryGetValue(shader.name, out List<Object> list))
                    {
                        list = new List<Object>();
                        unsupported[shader.name] = list;
                    }
                    list.Add(material);
                }
            }

            report.AppendLine(L("シーン内の Natane Toon マテリアル: " + natane, "Natane Toon materials in the scene: " + natane));
            report.AppendLine();

            if (unsupported.Count == 0)
            {
                report.AppendLine(L("すべて URP で描けるバリアントです。", "Every material uses a variant that renders in URP."));
            }
            else
            {
                report.AppendLine(L("URP に未対応のバリアント (Built-in RP 専用) を使っているマテリアルがあります。URP ではピンクになります:",
                                    "Some materials use variants without URP support (Built-in RP only). They render pink in URP:"));
                foreach (KeyValuePair<string, List<Object>> entry in unsupported)
                {
                    report.AppendLine("  " + entry.Key + " x" + entry.Value.Count);
                    foreach (Object material in entry.Value)
                    {
                        Debug.LogWarning(L("[Natane Toon] URP 未対応のバリアントです: ", "[Natane Toon] Variant without URP support: ")
                            + entry.Key + " (" + material.name + ")", material);
                    }
                }
                report.AppendLine();
                report.AppendLine(L("URP 対応: Toon Shader / Cutout / Transparent と、それぞれの Lite。該当マテリアルは Console から選択できます。",
                                    "URP-ready: Toon Shader / Cutout / Transparent and their Lite versions. The materials are listed in the Console."));
            }

            EditorUtility.DisplayDialog(Title, report.ToString(), "OK");
        }

        static bool HasURPSubShader(Shader shader)
        {
            ShaderData data = ShaderUtil.GetShaderData(shader);
            var tag = new ShaderTagId("RenderPipeline");
            for (int i = 0; i < data.SubshaderCount; i++)
            {
                if (data.GetSubshader(i).FindTagValue(tag).name == "UniversalPipeline") return true;
            }
            return false;
        }
    }
}
