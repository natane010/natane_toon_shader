using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Natane Post FX の設定を、VRChat ワールドで動くマテリアル (GrabPass のシェーダー) として書き出す。
    /// ワールドでは C# のコンポーネントが動かないので、画面を覆うメッシュ + マテリアルの形にする。
    /// </summary>
    public static class NatanePostFXWorldBuilder
    {
        public const string ShaderFull = "Natane/PostFX World";
        public const string ShaderLensColor = "Natane/PostFX World (Lens + Color)";
        public const string ShaderColor = "Natane/PostFX World (Color)";
        const string ObjectName = "Natane Post FX (World)";

        static string L(string ja, string en) { return NataneToonLocalization.L(ja, en); }

        /// <summary>設定に必要な段だけを持つ、一番軽いシェーダーを選ぶ。</summary>
        public static string PickShaderName(NatanePostFX fx)
        {
            if (fx.WorldNeedsStyle) return ShaderFull;
            return fx.WorldNeedsLens ? ShaderLensColor : ShaderColor;
        }

        /// <summary>ワールド用では再現されない効果のうち、今オンになっているもの。</summary>
        public static List<string> UnsupportedInUse(NatanePostFX fx)
        {
            var list = new List<string>();
            if (fx.glow.bloom > 1e-4f || fx.beat.AffectsBloom) list.Add(L("ブルーム", "Bloom"));
            if (fx.glow.streak > 1e-4f) list.Add(L("光条", "Anamorphic streak"));
            if (fx.glow.shafts > 1e-4f) list.Add(L("光芒", "Light shafts"));
            if (fx.glow.diffusion > 1e-4f) list.Add(L("ディフュージョン", "Diffusion"));
            if (fx.focus.depthOfField) list.Add(L("被写界深度", "Depth of field"));
            if (fx.focus.tiltShift > 1e-4f) list.Add(L("チルトシフト", "Tilt shift"));
            if (fx.focus.blur > 1e-4f) list.Add(L("画面全体のぼかし", "Full screen blur"));
            if (fx.mask.HasEffect) list.Add(L("キャラクターマスク", "Character mask"));
            if (fx.time.Active || fx.time.lightTrail > 1e-4f) list.Add(L("残像・コマ落ち", "Afterimage / frame hold"));
            return list;
        }

        [MenuItem(NataneToolMenuPaths.PostFXWorld, false, 2102)]
        static void CreateFromMenu()
        {
            NatanePostFX fx = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<NatanePostFX>() : null;
            if (fx == null)
            {
                NatanePostFX[] all = NataneEditorCompat.FindObjectsOfTypeCompat<NatanePostFX>();
                if (all.Length > 0) fx = all[0];
            }
            if (fx == null)
            {
                EditorUtility.DisplayDialog("Natane Post FX",
                    L("Natane Post FX の付いたカメラが見つかりません。先にカメラへ追加して、見た目を作ってください。",
                      "No camera with Natane Post FX was found. Add it to a camera and set up the look first."), "OK");
                return;
            }
            CreateInteractive(fx);
        }

        public static void CreateInteractive(NatanePostFX fx)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                L("ワールド用マテリアルの保存先", "Save the world material"), "NatanePostFXWorld", "mat",
                L("同じ名前のマテリアルがあれば、値を上書きします。", "An existing material with the same name is updated."));
            if (string.IsNullOrEmpty(path)) return;

            Material material = WriteMaterial(fx, path);
            if (material == null)
            {
                EditorUtility.DisplayDialog("Natane Post FX", L("マテリアルを作れませんでした。Console を確認してください。", "Could not create the material. See the Console."), "OK");
                return;
            }
            GameObject go = EnsureSceneObject(material, Path.GetDirectoryName(path).Replace('\\', '/'));
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(material);

            var sb = new StringBuilder();
            sb.AppendLine(L("ワールド用のマテリアルとオブジェクトを作りました。", "Created the world material and object."));
            sb.AppendLine(L("シェーダー: ", "Shader: ") + material.shader.name);
            List<string> unsupported = UnsupportedInUse(fx);
            if (unsupported.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(L("次の効果はワールド用では再現されません:", "These effects are not reproduced in the world version:"));
                sb.AppendLine("  " + string.Join(", ", unsupported.ToArray()));
            }
            if (fx.WorldNeedsDepth)
            {
                sb.AppendLine();
                sb.AppendLine(L("フォグ / 深度の輪郭線は深度テクスチャを使います。ワールドに影付きの Directional Light が無いと効きません。",
                                "Fog / depth edges use the depth texture. The world needs a Directional Light with shadows for it to exist."));
            }
            EditorUtility.DisplayDialog("Natane Post FX", sb.ToString(), "OK");
        }

        /// <summary>マテリアルを作る (あれば値だけ更新する)。</summary>
        public static Material WriteMaterial(NatanePostFX fx, string assetPath)
        {
            if (fx == null) return null;
            Shader shader = Shader.Find(PickShaderName(fx));
            if (shader == null)
            {
                Debug.LogError("[Natane Toon] " + L("ワールド用シェーダーが見つかりません: ", "World shader not found: ") + PickShaderName(fx));
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool created = material == null;
            if (created)
            {
                material = new Material(shader);
            }
            else
            {
                Undo.RecordObject(material, "Bake Natane Post FX (World)");
                if (material.shader != shader) material.shader = shader;
            }

            if (!fx.BakeWorldMaterial(material))
            {
                if (created) Object.DestroyImmediate(material);
                return null;
            }
            NatanePostFXKeywords.ApplyWorld(fx, material);

            if (created)
            {
                AssetDatabase.CreateAsset(material, assetPath);
                Undo.RegisterCreatedObjectUndo(material, "Bake Natane Post FX (World)");
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>画面を覆うメッシュ。UV をそのまま画面に広げるので、形は関係なくバウンディングボックスだけ大きくしてある。</summary>
        public static Mesh EnsureMesh(string folder)
        {
            string path = folder + "/NatanePostFXWorldQuad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            mesh = new Mesh { name = "NatanePostFXWorldQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            // どこから見てもカリングされないようにする。
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static GameObject EnsureSceneObject(Material material, string folder)
        {
            Mesh mesh = EnsureMesh(folder);
            foreach (MeshRenderer existing in NataneEditorCompat.FindObjectsOfTypeCompat<MeshRenderer>())
            {
                if (existing.sharedMaterial == material) return existing.gameObject;
            }

            var go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create Natane Post FX (World)");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }
    }
}
