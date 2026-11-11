using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Fake Shadow setup. Builds the drop-shadow geometry that
    /// <c>Natane/Toon Shader FakeShadow</c> renders.
    ///
    /// Both methods are non-destructive: the original renderer is never modified, the
    /// shadow is added as a child GameObject, and everything goes through Undo. Materials
    /// are written under <c>Assets/</c> on the project side — never into the package, which
    /// would be wiped on the next package update.
    /// </summary>
    public class FakeShadowSetupTool : EditorWindow
    {
        private const string ShaderName = "Natane/Toon Shader FakeShadow";
        private const string DefaultOutputFolder = "Assets/NataneToonGenerated/FakeShadow";

        private enum Method
        {
            /// <summary>顔の前に Quad を 1 枚置く。軽いが形の自由度は低い。</summary>
            QuadPlane,

            /// <summary>前髪メッシュを複製して法線方向へ押し出す。形は正確だがポリゴンが増える。</summary>
            DuplicateMesh
        }

        private Method _method = Method.QuadPlane;

        // Quad 方式
        private Renderer _faceRenderer;
        private Transform _headBone;
        private Texture2D _shadowShape;
        private float _quadWidth = 0.16f;
        private float _quadHeight = 0.10f;
        private Vector3 _quadOffset = new Vector3(0f, 0.02f, 0.075f);

        // メッシュ複製方式
        private Renderer _hairRenderer;
        private float _pushDistance = 0.002f;

        // 共通
        private Color _shadowColor = new Color(0.55f, 0.5f, 0.6f, 1f);
        private float _shadowAlpha = 0.5f;
        private float _lightColorFollow = 0.3f;
        private int _stencilRef;
        private bool _useStencil;
        private string _outputFolder = DefaultOutputFolder;

        private Vector2 _scroll;

        [MenuItem(NataneToolMenuPaths.FakeShadowSetup, false, 45)]
        public static void ShowWindow()
        {
            var window = GetWindow<FakeShadowSetupTool>(L("フェイクシャドウ設定", "Fake Shadow Setup"));
            window.minSize = new Vector2(480, 560);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                L("フェイクシャドウ（前髪の落ち影）", "Fake Shadow (hair drop shadow)"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("ライティングに依存しない落ち影を非破壊で追加します。元の Renderer は変更しません。",
                  "Adds a lighting-independent drop shadow non-destructively. The original renderer is untouched."),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            if (Shader.Find(ShaderName) == null)
            {
                EditorGUILayout.HelpBox(
                    L($"シェーダー \"{ShaderName}\" が見つかりません。パッケージのインポートを確認してください。",
                      $"Shader \"{ShaderName}\" was not found. Check that the package imported correctly."),
                    MessageType.Error);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6);
            _method = (Method)EditorGUILayout.EnumPopup(L("方式", "Method"), _method);

            EditorGUILayout.HelpBox(
                _method == Method.QuadPlane
                    ? L("板ポリ方式: 顔の前に Quad を 1 枚置き、前髪のシルエットを模したテクスチャを貼ります。" +
                        "軽量ですが、形はテクスチャ次第です。",
                        "Quad method: places a single quad in front of the face with a texture shaped like the " +
                        "hair silhouette. Cheap, but the shape is only as good as the texture.")
                    : L("メッシュ複製方式: 前髪メッシュを複製し、法線方向へわずかに押し出したものを影にします。" +
                        "形は正確ですがポリゴン数が増えます。",
                        "Mesh duplicate method: duplicates the hair mesh and pushes it slightly along its normals. " +
                        "Accurate shape, but adds polygons."),
                MessageType.Info);

            EditorGUILayout.Space(6);
            if (_method == Method.QuadPlane) DrawQuadSettings();
            else DrawMeshSettings();

            EditorGUILayout.Space(8);
            DrawSharedSettings();

            EditorGUILayout.Space(10);
            bool canBuild = _method == Method.QuadPlane
                ? (_faceRenderer != null || _headBone != null)
                : _hairRenderer != null;

            using (new EditorGUI.DisabledScope(!canBuild))
            {
                if (GUILayout.Button(L("フェイクシャドウを生成", "Create Fake Shadow"), GUILayout.Height(32)))
                {
                    if (_method == Method.QuadPlane) BuildQuad();
                    else BuildDuplicatedMesh();
                }
            }

            if (!canBuild)
            {
                EditorGUILayout.HelpBox(
                    _method == Method.QuadPlane
                        ? L("顔の Renderer か、追従させる頭ボーンを指定してください。",
                            "Assign the face renderer or the head bone to parent to.")
                        : L("前髪の Renderer を指定してください。", "Assign the hair renderer."),
                    MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawQuadSettings()
        {
            EditorGUILayout.LabelField(L("板ポリ設定", "Quad Settings"), EditorStyles.boldLabel);
            _faceRenderer = (Renderer)EditorGUILayout.ObjectField(
                L("顔の Renderer", "Face Renderer"), _faceRenderer, typeof(Renderer), true);
            _headBone = (Transform)EditorGUILayout.ObjectField(
                L("頭ボーン（追従先）", "Head Bone (parent)"), _headBone, typeof(Transform), true);

            if (_headBone == null && _faceRenderer is SkinnedMeshRenderer smr && smr.rootBone != null)
            {
                EditorGUILayout.HelpBox(
                    L("頭ボーン未指定の場合、Renderer の Transform に追従させます。",
                      "With no head bone assigned, the quad follows the renderer's transform."),
                    MessageType.None);
            }

            _shadowShape = (Texture2D)EditorGUILayout.ObjectField(
                L("影の形（アルファ）", "Shadow Shape (alpha)"), _shadowShape, typeof(Texture2D), false);
            _quadWidth = EditorGUILayout.FloatField(L("幅 (m)", "Width (m)"), _quadWidth);
            _quadHeight = EditorGUILayout.FloatField(L("高さ (m)", "Height (m)"), _quadHeight);
            _quadOffset = EditorGUILayout.Vector3Field(L("位置オフセット", "Position Offset"), _quadOffset);

            if (_shadowShape == null)
            {
                EditorGUILayout.HelpBox(
                    L("影の形テクスチャ未指定だと Quad 全面が影になります。前髪の形に切り抜いたアルファ付き" +
                      "テクスチャを指定してください。",
                      "Without a shape texture the whole quad becomes shadow. Assign an alpha texture cut to the " +
                      "hair silhouette."),
                    MessageType.Warning);
            }
        }

        private void DrawMeshSettings()
        {
            EditorGUILayout.LabelField(L("メッシュ複製設定", "Mesh Duplicate Settings"), EditorStyles.boldLabel);
            _hairRenderer = (Renderer)EditorGUILayout.ObjectField(
                L("前髪の Renderer", "Hair Renderer"), _hairRenderer, typeof(Renderer), true);
            _pushDistance = EditorGUILayout.Slider(
                L("押し出し量 (m)", "Push Distance (m)"), _pushDistance, 0f, 0.02f);

            if (_hairRenderer is SkinnedMeshRenderer)
            {
                EditorGUILayout.HelpBox(
                    L("SkinnedMeshRenderer のボーン参照とブレンドシェイプ設定を引き継ぎます。",
                      "Bone references and blend shape weights are carried over from the SkinnedMeshRenderer."),
                    MessageType.None);
            }
        }

        private void DrawSharedSettings()
        {
            EditorGUILayout.LabelField(L("影の見た目", "Shadow Appearance"), EditorStyles.boldLabel);
            _shadowColor = EditorGUILayout.ColorField(L("影色", "Shadow Color"), _shadowColor);
            _shadowAlpha = EditorGUILayout.Slider(L("不透明度", "Alpha"), _shadowAlpha, 0f, 1f);
            _lightColorFollow = EditorGUILayout.Slider(
                L("ライト色追従", "Light Color Follow"), _lightColorFollow, 0f, 1f);

            EditorGUILayout.Space(4);
            _useStencil = EditorGUILayout.Toggle(L("ステンシルで顔の中に限定", "Limit to face via stencil"), _useStencil);
            if (_useStencil)
            {
                EditorGUI.indentLevel++;
                _stencilRef = EditorGUILayout.IntSlider(L("参照値", "Reference"), _stencilRef, 1, 255);
                EditorGUILayout.HelpBox(
                    L("顔のマテリアル側で同じ参照値を書き込む設定が必要です。" +
                      "ステンシルプリセットツールで両者をまとめて設定できます。",
                      "The face material must write the same reference value. The Stencil Preset tool can set " +
                      "both sides together."),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            _outputFolder = EditorGUILayout.TextField(L("マテリアル保存先", "Material Folder"), _outputFolder);
            EditorGUILayout.LabelField(
                L("パッケージ内には書き込みません（更新時に消えるため）。",
                  "Never written inside the package — it would be wiped on update."),
                EditorStyles.miniLabel);
        }

        // ---- 生成 ----

        private void BuildQuad()
        {
            Transform parent = _headBone != null ? _headBone
                             : _faceRenderer != null ? _faceRenderer.transform
                             : null;
            if (parent == null) return;

            Material material = CreateMaterial("FakeShadow_Quad");
            if (material == null) return;

            var go = new GameObject("FakeShadow (Quad)");
            Undo.RegisterCreatedObjectUndo(go, "Create Fake Shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = _quadOffset;
            go.transform.localRotation = Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildQuadMesh(_quadWidth, _quadHeight);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // 落ち影そのものは影を落とさない/受けない。二重に暗くなるのを避ける。
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            EditorUtility.DisplayDialog(
                L("フェイクシャドウ", "Fake Shadow"),
                L("板ポリの落ち影を生成しました。位置とサイズは Transform で微調整してください。",
                  "Created the quad drop shadow. Fine-tune position and size on the transform."),
                "OK");
        }

        private void BuildDuplicatedMesh()
        {
            if (_hairRenderer == null) return;

            Mesh sourceMesh = _hairRenderer is SkinnedMeshRenderer skinned
                ? skinned.sharedMesh
                : _hairRenderer.GetComponent<MeshFilter>()?.sharedMesh;

            if (sourceMesh == null)
            {
                EditorUtility.DisplayDialog(
                    L("フェイクシャドウ", "Fake Shadow"),
                    L("元メッシュを取得できませんでした。", "Could not resolve the source mesh."),
                    "OK");
                return;
            }

            Material material = CreateMaterial("FakeShadow_Mesh");
            if (material == null) return;

            Mesh pushed = CreatePushedMesh(sourceMesh, _pushDistance);
            if (pushed == null) return;

            string meshPath = SaveMeshAsset(pushed, sourceMesh.name + "_FakeShadow");
            if (meshPath != null)
            {
                pushed = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            }

            var go = new GameObject("FakeShadow (Mesh)");
            Undo.RegisterCreatedObjectUndo(go, "Create Fake Shadow");
            go.transform.SetParent(_hairRenderer.transform.parent, false);
            go.transform.localPosition = _hairRenderer.transform.localPosition;
            go.transform.localRotation = _hairRenderer.transform.localRotation;
            go.transform.localScale = _hairRenderer.transform.localScale;

            Renderer created;
            if (_hairRenderer is SkinnedMeshRenderer source)
            {
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = pushed;
                // ボーン参照を引き継がないと、複製メッシュだけがスキニングされず取り残される。
                smr.bones = source.bones;
                smr.rootBone = source.rootBone;
                smr.localBounds = source.localBounds;
                smr.quality = source.quality;
                smr.updateWhenOffscreen = source.updateWhenOffscreen;

                for (int i = 0; i < pushed.blendShapeCount && i < source.sharedMesh.blendShapeCount; i++)
                {
                    smr.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
                }

                created = smr;
            }
            else
            {
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = pushed;
                created = go.AddComponent<MeshRenderer>();
            }

            created.sharedMaterial = material;
            created.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            created.receiveShadows = false;
            created.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            EditorUtility.DisplayDialog(
                L("フェイクシャドウ", "Fake Shadow"),
                L($"メッシュ複製の落ち影を生成しました。\n三角形数: {pushed.triangles.Length / 3}",
                  $"Created the duplicated-mesh drop shadow.\nTriangles: {pushed.triangles.Length / 3}"),
                "OK");
        }

        /// <summary>
        /// 法線方向へ押し出した複製メッシュ。押し出さないと元メッシュと完全に同一面になり、
        /// Z ファイティングでちらつく。
        /// </summary>
        private static Mesh CreatePushedMesh(Mesh source, float distance)
        {
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + "_FakeShadow";

            if (distance <= 0.00001f) return mesh;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;

            if (normals == null || normals.Length != vertices.Length)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] += normals[i] * distance;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildQuadMesh(float width, float height)
        {
            float hw = Mathf.Max(width, 0.001f) * 0.5f;
            float hh = Mathf.Max(height, 0.001f) * 0.5f;

            var mesh = new Mesh { name = "FakeShadowQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-hw, -hh, 0f),
                new Vector3( hw, -hh, 0f),
                new Vector3(-hw,  hh, 0f),
                new Vector3( hw,  hh, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            // -Z を向く。顔の前に置いてカメラ側を向かせるため。
            mesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateMaterial(string baseName)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) return null;

            string folder = EnsureFolder(_outputFolder);
            if (folder == null)
            {
                EditorUtility.DisplayDialog(
                    L("フェイクシャドウ", "Fake Shadow"),
                    L("マテリアルの保存先フォルダを作成できませんでした。Assets 配下のパスを指定してください。",
                      "Could not create the material folder. Use a path under Assets."),
                    "OK");
                return null;
            }

            var material = new Material(shader) { name = baseName };
            material.SetColor("_ShadowColor", _shadowColor);
            material.SetFloat("_ShadowAlpha", _shadowAlpha);
            material.SetFloat("_LightColorFollow", _lightColorFollow);
            if (_shadowShape != null) material.SetTexture("_ShadowTex", _shadowShape);

            if (_useStencil)
            {
                material.SetFloat("_StencilRef", _stencilRef);
                // Equal(3): 顔が書いた領域にだけ描く。
                material.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Equal);
                material.SetFloat("_StencilOp", (float)UnityEngine.Rendering.StencilOp.Keep);
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{baseName}.mat");
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static string SaveMeshAsset(Mesh mesh, string name)
        {
            string folder = EnsureFolder(DefaultOutputFolder + "/Meshes");
            if (folder == null) return null;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{Sanitize(name)}.asset");
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static string EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets")) return null;
            if (AssetDatabase.IsValidFolder(assetPath)) return assetPath;

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
            return AssetDatabase.IsValidFolder(current) ? current : null;
        }

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return string.IsNullOrWhiteSpace(value) ? "FakeShadow" : value;
        }
    }
}
