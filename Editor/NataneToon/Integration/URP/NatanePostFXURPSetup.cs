// URP 17 以降でのみコンパイルされる (NataneToon.Editor.URP.asmdef の defineConstraints)。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NataneToon.Editor
{
    /// <summary>Natane Post FX の Renderer Feature を、使用中の URP Renderer に追加する。</summary>
    public static class NatanePostFXURPSetup
    {
        const string MenuPath = "Tools/Natane/URP/Post FX を Renderer に追加 Add Post FX Renderer Feature";

        static string L(string ja, string en)
        {
            return NataneToonLocalization.L(ja, en);
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            NatanePostFXEditor.CheckUrpFeature = HasFeature;
            NatanePostFXEditor.AddUrpFeature = AddFeature;
        }

        static bool? HasFeature()
        {
            ScriptableRendererData rendererData = NataneURPSetupTool.FindActiveRendererData(out string _);
            if (rendererData == null) return null;
            return FindFeature(rendererData) != null;
        }

        static NatanePostFXRendererFeature FindFeature(ScriptableRendererData rendererData)
        {
            List<ScriptableRendererFeature> features = rendererData.rendererFeatures;
            if (features == null) return null;
            for (int i = 0; i < features.Count; i++)
            {
                var feature = features[i] as NatanePostFXRendererFeature;
                if (feature != null) return feature;
            }
            return null;
        }

        [MenuItem(MenuPath, false, 82)]
        static void AddFeature()
        {
            const string title = "Natane Post FX";
            ScriptableRendererData rendererData = NataneURPSetupTool.FindActiveRendererData(out string problem);
            if (rendererData == null)
            {
                EditorUtility.DisplayDialog(title, problem, "OK");
                return;
            }

            if (FindFeature(rendererData) != null)
            {
                EditorGUIUtility.PingObject(rendererData);
                EditorUtility.DisplayDialog(title,
                    L("この Renderer には既に Natane Post FX が追加されています。\n\n" + rendererData.name,
                      "This renderer already has the Natane Post FX feature.\n\n" + rendererData.name), "OK");
                return;
            }

            var feature = ScriptableObject.CreateInstance<NatanePostFXRendererFeature>();
            feature.name = "Natane Post FX";
            Undo.RegisterCreatedObjectUndo(feature, "Add Natane Post FX");
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            // Renderer Feature は参照のリストとローカル ID のリストの両方に足す (URP のインスペクターと同じ)。
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            var serialized = new SerializedObject(rendererData);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serialized.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
            {
                Undo.DestroyObjectImmediate(feature);
                EditorUtility.DisplayDialog(title,
                    L("この URP バージョンでは自動で追加できませんでした。Renderer のインスペクターで「Add Renderer Feature > Natane Post FX Renderer Feature」を選んでください。",
                      "Could not add the feature automatically on this URP version. In the renderer inspector choose \"Add Renderer Feature > Natane Post FX Renderer Feature\"."),
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

            EditorUtility.DisplayDialog(title,
                L("Natane Post FX を追加しました。\n\nRenderer: " + rendererData.name +
                  "\n\n効果の設定は、カメラに付けた Natane Post FX コンポーネントで行います。",
                  "Added Natane Post FX.\n\nRenderer: " + rendererData.name +
                  "\n\nTune the effects on the Natane Post FX component attached to the camera."),
                "OK");
        }
    }
}
