using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// GPU Particle Mesh Generator
    /// GPUパーティクルメッシュ生成ツール - "Natane/Effects/GPU Particles (Stateless)"
    /// シェーダー用の静的クアッドクラウドメッシュを生成する。
    ///
    /// メッシュ規約 (Mesh convention):
    ///   1パーティクル = 1クアッド (4頂点 / 2トライアングル)。
    ///   POSITION : 4頂点すべてがパーティクルの中心 (エミッションボックス内のランダム座標)。
    ///   UV0      : クアッドの角 (各成分 0 or 1)。スプライトUVも兼ねる。
    ///   UV1.x    : パーティクル乱数シード [0,1)。
    ///   UV1.y    : パーティクルインデックス正規化 (i / count) [0,1]。
    ///   COLOR    : パーティクル毎のティント色。
    /// </summary>
    public class GPUParticleMeshGenerator : EditorWindow
    {
        private int particleCount = 256;
        private const int MaxParticles = 4096;
        private Vector3 spread = new Vector3(2f, 2f, 2f);
        private int seed = 12345;
        private Color tint = Color.white;
        private float tintVariation = 0.15f;
        private Vector2 scroll;

        [MenuItem("Tools/Natane/メッシュ Mesh/GPUパーティクルメッシュ生成 GPU Particle Mesh", false, 61)]
        public static void ShowWindow()
        {
            var window = GetWindow<GPUParticleMeshGenerator>(
                L("GPUパーティクルメッシュ生成", "GPU Particle Mesh"));
            window.minSize = new Vector2(420, 380);
            window.Show();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                L("GPUパーティクルメッシュ生成", "GPU Particle Mesh Generator"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("ステートレスGPUパーティクルシェーダー用のクアッドクラウドを生成",
                  "Generate a quad-cloud for the stateless GPU particle shader"),
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            particleCount = EditorGUILayout.IntSlider(
                L("パーティクル数", "Particle Count"), particleCount, 1, MaxParticles);
            spread = EditorGUILayout.Vector3Field(
                L("スプレッド (エミッション範囲)", "Spread (emission box)"), spread);
            seed = EditorGUILayout.IntField(L("乱数シード", "Random Seed"), seed);
            tint = EditorGUILayout.ColorField(L("基本ティント", "Base Tint"), tint);
            tintVariation = EditorGUILayout.Slider(
                L("ティントのばらつき", "Tint Variation"), tintVariation, 0f, 1f);

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                L($"頂点数: {particleCount * 4} / トライアングル数: {particleCount * 2}",
                  $"Vertices: {particleCount * 4} / Triangles: {particleCount * 2}"),
                MessageType.Info);

            EditorGUILayout.Space(10);
            if (GUILayout.Button(
                L("メッシュを生成して保存", "Generate & Save Mesh"), GUILayout.Height(32)))
            {
                GenerateAndSave();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                L("生成後、静的メッシュとして MeshFilter に割り当て、マテリアルに " +
                  "\"Natane/Effects/GPU Particles (Stateless)\" を設定してください。",
                  "After generating, assign the mesh to a MeshFilter and use the " +
                  "\"Natane/Effects/GPU Particles (Stateless)\" material."),
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void GenerateAndSave()
        {
            particleCount = Mathf.Clamp(particleCount, 1, MaxParticles);

            Mesh mesh = BuildMesh();

            string path = EditorUtility.SaveFilePanelInProject(
                L("GPUパーティクルメッシュを保存", "Save GPU Particle Mesh"),
                "GPUParticleCloud",
                "asset",
                L("メッシュの保存場所を選択してください", "Choose a location to save the mesh"),
                "Assets");

            if (string.IsNullOrEmpty(path))
            {
                Object.DestroyImmediate(mesh);
                return;
            }

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Object saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            EditorGUIUtility.PingObject(saved);
            Selection.activeObject = saved;
            Debug.Log(L($"GPUパーティクルメッシュを生成しました: {path}",
                        $"Generated GPU particle mesh: {path}"));
        }

        private Mesh BuildMesh()
        {
            var rng = new System.Random(seed);
            int vCount = particleCount * 4;

            var vertices = new Vector3[vCount];
            var uv0 = new Vector2[vCount];   // quad corner (0/1) + sprite uv
            var uv1 = new Vector2[vCount];   // x = seed, y = index normalized
            var colors = new Color[vCount];
            var indices = new int[particleCount * 6];

            // The four quad corners (UV0 convention).
            Vector2[] corners =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };

            float denom = Mathf.Max(1, particleCount - 1);

            for (int p = 0; p < particleCount; p++)
            {
                // Per-particle center inside the spread box.
                Vector3 center = new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * spread.x,
                    ((float)rng.NextDouble() - 0.5f) * spread.y,
                    ((float)rng.NextDouble() - 0.5f) * spread.z);

                float pSeed = (float)rng.NextDouble();               // [0,1)
                float indexNorm = p / denom;                         // [0,1]

                // Per-particle tint with random variation (kept in valid range).
                float v = 1f - (float)rng.NextDouble() * tintVariation;
                Color pColor = new Color(tint.r * v, tint.g * v, tint.b * v, tint.a);

                int vBase = p * 4;
                for (int c = 0; c < 4; c++)
                {
                    int vi = vBase + c;
                    vertices[vi] = center;                // all 4 verts at center
                    uv0[vi] = corners[c];
                    uv1[vi] = new Vector2(pSeed, indexNorm);
                    colors[vi] = pColor;
                }

                int iBase = p * 6;
                indices[iBase + 0] = vBase + 0;
                indices[iBase + 1] = vBase + 2;
                indices[iBase + 2] = vBase + 1;
                indices[iBase + 3] = vBase + 0;
                indices[iBase + 4] = vBase + 3;
                indices[iBase + 5] = vBase + 2;
            }

            var mesh = new Mesh { name = "GPUParticleCloud" };
            mesh.indexFormat = vCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uv0;
            mesh.uv2 = uv1;
            mesh.colors = colors;
            mesh.triangles = indices;

            // Large bounds so the billboards are never frustum-culled early.
            float r = Mathf.Max(spread.x, Mathf.Max(spread.y, spread.z));
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (r + 2f));
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
