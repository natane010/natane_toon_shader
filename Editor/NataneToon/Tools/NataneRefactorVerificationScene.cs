using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// リファクタリングの「見た目非回帰」を目視確認するための検証オブジェクト生成ツール。
    ///
    /// NataneToonUtils.hlsl へハッシュ / 座標セレクタを集約した際に、
    /// Caustics と Topographic の出力が変わっていないことを確認する用途で作った。
    /// 生成されるマテリアルは、集約で触れたコードパスを確実に通す設定にしてある:
    ///
    ///   - Caustics(Procedural)      … NataneCaustics_Hash2 → NataneHash22
    ///   - Caustics(TriplanarLite)   … NataneCausticsCoord  → NataneProjectionCoord の分岐
    ///   - Topographic(NoiseStrength>0) … NataneTopo_Noise  → NataneHash21
    ///
    /// 現在開いているシーンに追加する（新規シーンは作らない）。
    /// マテリアルは Tests/Verification 配下にアセットとして保存し、
    /// 変更前後のスクリーンショット比較に再利用できるようにしている。
    /// </summary>
    internal static class NataneRefactorVerificationScene
    {
        private const string ShaderName = "Natane/Toon Shader";
        private const string OutputFolder = "Assets/NataneToon/Tests/Verification";
        private const string RootName = "NataneRefactorVerification";

        // スフィア1体分の定義。
        private sealed class Spec
        {
            public string Label;
            public string MaterialName;
            public System.Action<Material> Configure;
        }

        [MenuItem("Tools/Natane/開発 Dev/リファクタ検証オブジェクトを現在のシーンに追加", false, 2000)]
        public static void CreateInCurrentScene()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog(
                    "リファクタ検証オブジェクト",
                    $"シェーダー \"{ShaderName}\" が見つかりませんでした。\n" +
                    "パッケージが正しくインポートされているか確認してください。",
                    "OK");
                return;
            }

            EnsureFolder();

            List<Spec> specs = BuildSpecs();
            var materials = new List<Material>(specs.Count);
            foreach (Spec spec in specs)
                materials.Add(CreateOrUpdateMaterial(shader, spec));

            // 既存の検証ルートがあれば作り直す（何度実行しても増殖しないように）。
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Natane Refactor Verification");

            CreateGround(root.transform, shader);
            EnsureDirectionalLight(root.transform);

            float spacing = 2.5f;
            float startX = -(specs.Count - 1) * spacing * 0.5f;

            for (int i = 0; i < specs.Count; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"{i + 1:00}_{specs[i].Label}";
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.localPosition = new Vector3(startX + i * spacing, 1.0f, 0f);

                var renderer = sphere.GetComponent<Renderer>();
                renderer.sharedMaterial = materials[i];

                Undo.RegisterCreatedObjectUndo(sphere, "Create Natane Refactor Verification");
            }

            AssetDatabase.SaveAssets();

            Selection.activeGameObject = root;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null) sceneView.FrameSelected();

            EditorUtility.DisplayDialog(
                "リファクタ検証オブジェクト",
                $"スフィア {specs.Count} 体を現在のシーンに追加しました。\n\n" +
                $"マテリアル出力先:\n{OutputFolder}\n\n" +
                "Caustics と Topographic はアニメーションするため、\n" +
                "Scene ビューの Always Refresh を有効にすると動きが確認できます。\n\n" +
                "取り消しは Ctrl+Z で戻せます（マテリアルアセットは残ります）。",
                "OK");
        }

        // ---- マテリアル定義 ----

        private static List<Spec> BuildSpecs()
        {
            return new List<Spec>
            {
                // 集約で触れていない基準点。他と見比べるためのプレーンなトゥーン。
                new Spec
                {
                    Label = "Baseline",
                    MaterialName = "NataneVerify_Baseline",
                    Configure = m => { }
                },

                // NataneCaustics_Hash2 → NataneHash22 を通す（Procedural + Voronoi）。
                new Spec
                {
                    Label = "Caustics_World",
                    MaterialName = "NataneVerify_Caustics_World",
                    Configure = m =>
                    {
                        EnableCaustics(m);
                        SetFloatIfExists(m, "_CausticsSpace", 2f);      // World
                        SetFloatIfExists(m, "_CausticsComposite", 0f);  // EmissionAdd
                    }
                },

                // NataneCausticsCoord → NataneProjectionCoord の Triplanar-lite 分岐を通す。
                new Spec
                {
                    Label = "Caustics_Triplanar",
                    MaterialName = "NataneVerify_Caustics_Triplanar",
                    Configure = m =>
                    {
                        EnableCaustics(m);
                        SetFloatIfExists(m, "_CausticsSpace", 3f);      // TriplanarLite
                        SetFloatIfExists(m, "_CausticsComposite", 0f);
                    }
                },

                // Phase 4（影の玉ボケ）で使う ShadowOnly 合成の現状を押さえておく。
                new Spec
                {
                    Label = "Caustics_ShadowOnly",
                    MaterialName = "NataneVerify_Caustics_ShadowOnly",
                    Configure = m =>
                    {
                        EnableCaustics(m);
                        SetFloatIfExists(m, "_CausticsSpace", 2f);
                        SetFloatIfExists(m, "_CausticsComposite", 3f);  // ShadowOnly
                        SetFloatIfExists(m, "_CausticsIntensity", 3f);
                    }
                },

                // NataneTopo_Noise → NataneHash21 を通す（NoiseStrength > 0.001 が条件）。
                new Spec
                {
                    Label = "Topographic_Noise",
                    MaterialName = "NataneVerify_Topographic_Noise",
                    Configure = m =>
                    {
                        m.SetFloat("_Topographic", 1f);
                        m.EnableKeyword("_TOPOGRAPHIC");
                        SetFloatIfExists(m, "_TopoSpace", 1f);           // World
                        SetFloatIfExists(m, "_TopoMode", 5f);            // NoiseDistorted
                        SetFloatIfExists(m, "_TopoSpacing", 0.15f);
                        SetFloatIfExists(m, "_TopoLineWidth", 0.12f);
                        SetFloatIfExists(m, "_TopoNoiseScale", 3f);
                        SetFloatIfExists(m, "_TopoNoiseStrength", 0.6f); // ここが 0 だと NataneTopo_Noise を通らない
                        SetFloatIfExists(m, "_TopoEmission", 2f);
                        SetColorIfExists(m, "_TopoColor", new Color(0.2f, 1f, 0.8f, 1f));
                    }
                },

                // 影の玉ボケ（木漏れ日）。影の中に光斑が出る本命の設定。
                new Spec
                {
                    Label = "ShadowBokeh_Komorebi",
                    MaterialName = "NataneVerify_ShadowBokeh_ShadowOnly",
                    Configure = m =>
                    {
                        EnableShadowBokeh(m);
                        SetFloatIfExists(m, "_ShadowBokehComposite", 0f);  // ShadowOnly
                    }
                },

                // 光の中に葉影を落とす側。合成方法の反転が効いているかの確認。
                new Spec
                {
                    Label = "ShadowBokeh_LeafShadow",
                    MaterialName = "NataneVerify_ShadowBokeh_LitOnly",
                    Configure = m =>
                    {
                        EnableShadowBokeh(m);
                        SetFloatIfExists(m, "_ShadowBokehComposite", 1f);  // LitOnly
                        SetColorIfExists(m, "_ShadowBokehColor", new Color(0.35f, 0.4f, 0.3f, 1f));
                    }
                },

                // 絞り羽根を立てて多角形にする。形状分岐の確認。
                new Spec
                {
                    Label = "ShadowBokeh_Hexagon",
                    MaterialName = "NataneVerify_ShadowBokeh_Hexagon",
                    Configure = m =>
                    {
                        EnableShadowBokeh(m);
                        SetFloatIfExists(m, "_ShadowBokehComposite", 0f);
                        SetFloatIfExists(m, "_ShadowBokehBlades", 6f);
                        SetFloatIfExists(m, "_ShadowBokehRimGain", 0.5f);
                    }
                },

                // 漫画網点（ドット）。影の濃さを 4 段階に量子化する。
                new Spec
                {
                    Label = "Halftone_Manga_Dot",
                    MaterialName = "NataneVerify_Halftone_Dot",
                    Configure = m =>
                    {
                        EnableHalftone(m);
                        SetFloatIfExists(m, "_HalftoneShadowPattern", 0f);  // Dot
                    }
                },

                // カケアミ。薄いうちは一方向、濃くなると直交方向が重なる。
                new Spec
                {
                    Label = "Halftone_CrossHatch",
                    MaterialName = "NataneVerify_Halftone_CrossHatch",
                    Configure = m =>
                    {
                        EnableHalftone(m);
                        SetFloatIfExists(m, "_HalftoneShadowPattern", 2f);  // CrossHatch
                        SetFloatIfExists(m, "_HalftoneShadowScale", 24f);
                    }
                },

                // 影の自然さ。0（従来）との比較用に高めの値を入れる。
                new Spec
                {
                    Label = "ShadowNaturalness_High",
                    MaterialName = "NataneVerify_ShadowNaturalness",
                    Configure = m =>
                    {
                        SetFloatIfExists(m, "_ShadowNaturalness", 0.7f);
                    }
                },

                // 両方同時に有効。相互干渉で崩れないことの確認。
                new Spec
                {
                    Label = "Caustics_and_Topo",
                    MaterialName = "NataneVerify_Caustics_And_Topo",
                    Configure = m =>
                    {
                        EnableCaustics(m);
                        SetFloatIfExists(m, "_CausticsSpace", 2f);
                        SetFloatIfExists(m, "_CausticsComposite", 0f);

                        m.SetFloat("_Topographic", 1f);
                        m.EnableKeyword("_TOPOGRAPHIC");
                        SetFloatIfExists(m, "_TopoMode", 5f);
                        SetFloatIfExists(m, "_TopoNoiseStrength", 0.6f);
                        SetFloatIfExists(m, "_TopoEmission", 2f);
                    }
                },
            };
        }

        /// <summary>
        /// 影の玉ボケの共通設定。影が十分に落ちていないと光斑が見えないので、
        /// このマテリアルを使うオブジェクトはライトに対して陰になる面を持つこと。
        /// </summary>
        private static void EnableShadowBokeh(Material m)
        {
            m.SetFloat("_ShadowBokeh", 1f);
            m.EnableKeyword("_SHADOW_BOKEH");
            SetFloatIfExists(m, "_ShadowBokehIntensity", 3f);
            SetFloatIfExists(m, "_ShadowBokehScale", 4f);
            SetFloatIfExists(m, "_ShadowBokehSize", 0.4f);
            SetFloatIfExists(m, "_ShadowBokehSoftness", 0.5f);
            SetFloatIfExists(m, "_ShadowBokehBlades", 0f);
            SetFloatIfExists(m, "_ShadowBokehRimGain", 0.25f);
            SetFloatIfExists(m, "_ShadowBokehSpeed", 0.05f);
            SetFloatIfExists(m, "_ShadowBokehShadowMin", 0.3f);
            SetFloatIfExists(m, "_ShadowBokehBlend", 1f);
            SetColorIfExists(m, "_ShadowBokehColor", new Color(1f, 0.95f, 0.8f, 1f));
            if (m.HasProperty("_ShadowBokehDirection"))
                m.SetVector("_ShadowBokehDirection", new Vector4(1f, 0.3f, 0f, 0f));
        }

        /// <summary>
        /// 漫画網点の共通設定。トーンの号数を 4 段にして、粒度が段階的に変わることを見る。
        /// </summary>
        private static void EnableHalftone(Material m)
        {
            m.SetFloat("_HalftoneShadow", 1f);
            m.EnableKeyword("_HALFTONE_SHADOW");
            SetFloatIfExists(m, "_HalftoneShadowScale", 16f);
            SetFloatIfExists(m, "_HalftoneShadowThreshold", 0.5f);
            SetFloatIfExists(m, "_HalftoneShadowSoftness", 0.25f);
            SetFloatIfExists(m, "_HalftoneShadowIntensity", 1f);
            SetFloatIfExists(m, "_HalftoneShadowBlend", 1f);
            SetFloatIfExists(m, "_HalftoneShadowAngle", 45f);
            SetFloatIfExists(m, "_HalftoneShadowLevels", 4f);
            SetFloatIfExists(m, "_HalftoneShadowSpace", 0f);   // Screen
            SetFloatIfExists(m, "_HalftoneShadowDotMin", 0.05f);
            SetFloatIfExists(m, "_HalftoneShadowDotMax", 0.9f);
            SetFloatIfExists(m, "_HalftoneShadowAA", 1f);
            SetColorIfExists(m, "_HalftoneShadowColor", new Color(0.15f, 0.15f, 0.2f, 1f));
        }

        private static void EnableCaustics(Material m)
        {
            m.SetFloat("_Caustics", 1f);
            m.EnableKeyword("_CAUSTICS");
            SetFloatIfExists(m, "_CausticsPatternMode", 0f);   // Procedural（Voronoi 経路）
            SetFloatIfExists(m, "_CausticsIntensity", 1.5f);
            SetFloatIfExists(m, "_CausticsScale", 4f);
            SetFloatIfExists(m, "_CausticsSpeed", 0.5f);
            SetFloatIfExists(m, "_CausticsDistortion", 0.2f);
            SetFloatIfExists(m, "_CausticsContrast", 2f);
            SetColorIfExists(m, "_CausticsColor", new Color(0.6f, 0.9f, 1f, 1f));
            if (m.HasProperty("_CausticsDirection"))
                m.SetVector("_CausticsDirection", new Vector4(1f, 0.5f, 0f, 0f));
        }

        // ---- アセット操作 ----

        private static Material CreateOrUpdateMaterial(Shader shader, Spec spec)
        {
            string path = $"{OutputFolder}/{spec.MaterialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = spec.MaterialName };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                Undo.RecordObject(material, "Configure Natane Verification Material");
                material.shader = shader;
            }

            // ベースカラーはエフェクトが見やすい中間グレーに揃える。
            SetColorIfExists(material, "_Color", new Color(0.5f, 0.5f, 0.55f, 1f));

            spec.Configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 地面。影の玉ボケは広い面のほうが判別しやすいので、
        /// 地面自体にも木漏れ日のマテリアルを当てておく。
        /// </summary>
        private static void CreateGround(Transform parent, Shader shader)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground (ShadowBokeh)";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            var groundSpec = new Spec
            {
                Label = "Ground",
                MaterialName = "NataneVerify_Ground_ShadowBokeh",
                Configure = m =>
                {
                    EnableShadowBokeh(m);
                    SetFloatIfExists(m, "_ShadowBokehComposite", 0f);
                    SetFloatIfExists(m, "_ShadowBokehScale", 2f);
                }
            };
            ground.GetComponent<Renderer>().sharedMaterial = CreateOrUpdateMaterial(shader, groundSpec);

            Undo.RegisterCreatedObjectUndo(ground, "Create Natane Refactor Verification");
        }

        /// <summary>
        /// シーンに Directional Light が無ければ追加する。
        /// Caustics / Topographic はライティングの影響を受けるため、無いと判別しにくい。
        /// </summary>
        private static void EnsureDirectionalLight(Transform parent)
        {
            foreach (Light light in Object.FindObjectsOfType<Light>())
                if (light.type == LightType.Directional && light.enabled)
                    return;

            var go = new GameObject("Directional Light (verification)");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);

            Light created = go.AddComponent<Light>();
            created.type = LightType.Directional;
            created.intensity = 1f;
            created.shadows = LightShadows.Soft;

            Undo.RegisterCreatedObjectUndo(go, "Create Natane Refactor Verification");
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder)) return;

            string[] parts = OutputFolder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ---- 防御的なプロパティ設定（バリアントによって存在しないものがある）----

        private static void SetFloatIfExists(Material m, string prop, float value)
        {
            if (m != null && m.HasProperty(prop)) m.SetFloat(prop, value);
        }

        private static void SetColorIfExists(Material m, string prop, Color value)
        {
            if (m != null && m.HasProperty(prop)) m.SetColor(prop, value);
        }
    }
}
