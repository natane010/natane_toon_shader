using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Fit Shadow Shape Rig slots from a painted mask.
    ///
    /// Typing four ellipses in by hand — centre, two radii, rotation — is the tedious part
    /// of the feature. Painting "make the shadow bigger here" is the natural way to express
    /// it, so this reads a mask, finds up to four blobs, and solves each blob's centre and
    /// principal axes directly.
    ///
    /// The axes come from the second moment matrix of the blob's pixels. For a 2x2 symmetric
    /// matrix the eigen-decomposition has a closed form, so no iterative solver is needed.
    /// </summary>
    public class ShadowRigFitter : EditorWindow
    {
        private const int MaxSlots = 4;

        private Material _material;
        private Texture2D _maskTexture;
        private float _threshold = 0.5f;
        private float _radiusScale = 2f;
        private float _defaultStrength = -0.2f;
        private float _defaultFalloff = 0.5f;
        private bool _clearUnusedSlots = true;

        private readonly List<Blob> _preview = new List<Blob>();

        [MenuItem(NataneToolMenuPaths.ShadowRigFitter, false, 48)]
        public static void ShowWindow()
        {
            var window = GetWindow<ShadowRigFitter>(L("影シェイプリグのフィット", "Shadow Rig Fitter"));
            window.minSize = new Vector2(460, 520);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                L("マスクから影シェイプリグを生成", "Fit Shadow Shape Rig from a mask"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("「影を膨らませたい場所」を塗ったマスクから、楕円リグの中心・半径・回転を推定します。",
                  "Estimates each ellipse's centre, radii and rotation from a mask you painted where the shadow should grow."),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            _material = (Material)EditorGUILayout.ObjectField(
                L("マテリアル", "Material"), _material, typeof(Material), false);
            _maskTexture = (Texture2D)EditorGUILayout.ObjectField(
                L("マスクテクスチャ", "Mask Texture"), _maskTexture, typeof(Texture2D), false);

            if (_material != null && !_material.HasProperty("_ShadowRigParams0"))
            {
                EditorGUILayout.HelpBox(
                    L("このマテリアルは影シェイプリグに対応していません。",
                      "This material does not support the Shadow Shape Rig."),
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(6);
            _threshold = EditorGUILayout.Slider(L("二値化しきい値", "Binarize Threshold"), _threshold, 0.05f, 0.95f);
            _radiusScale = EditorGUILayout.Slider(L("半径スケール", "Radius Scale"), _radiusScale, 0.5f, 4f);
            EditorGUILayout.LabelField(
                L("標準偏差に掛ける倍率。2.0 で塗り領域のおおよそ全体を覆います。",
                  "Multiplier on the standard deviation. 2.0 covers roughly the whole painted area."),
                EditorStyles.miniLabel);

            _defaultStrength = EditorGUILayout.Slider(
                L("初期の強さ", "Initial Strength"), _defaultStrength, -1f, 1f);
            _defaultFalloff = EditorGUILayout.Slider(
                L("初期の減衰", "Initial Falloff"), _defaultFalloff, 0f, 1f);
            _clearUnusedSlots = EditorGUILayout.Toggle(
                L("余ったスロットを無効化", "Clear unused slots"), _clearUnusedSlots);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(_maskTexture == null))
            {
                if (GUILayout.Button(L("マスクを解析", "Analyze Mask"), GUILayout.Height(24)))
                {
                    Analyze();
                }
            }

            if (_preview.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(L("検出した領域", "Detected regions"), EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                for (int i = 0; i < _preview.Count; i++)
                {
                    Blob b = _preview[i];
                    EditorGUILayout.LabelField(
                        $"  Rig {i}",
                        $"center ({b.Center.x:0.00}, {b.Center.y:0.00})  " +
                        $"radius ({b.RadiusMajor:0.000}, {b.RadiusMinor:0.000})  " +
                        $"rot {b.RotationDegrees:0.0}°  px {b.PixelCount}");
                }
                EditorGUILayout.EndVertical();

                using (new EditorGUI.DisabledScope(_material == null))
                {
                    if (GUILayout.Button(L("マテリアルへ適用", "Apply to Material"), GUILayout.Height(28)))
                    {
                        Apply();
                    }
                }
            }
            else if (_maskTexture != null)
            {
                EditorGUILayout.HelpBox(
                    L("「マスクを解析」を押してください。", "Press \"Analyze Mask\"."),
                    MessageType.Info);
            }
        }

        // ---- 解析 ----

        private struct Blob
        {
            public Vector2 Center;          // UV
            public float RadiusMajor;       // UV
            public float RadiusMinor;       // UV
            public float RotationDegrees;
            public int PixelCount;
        }

        private void Analyze()
        {
            _preview.Clear();
            if (_maskTexture == null) return;

            Texture2D readable = MakeReadable(_maskTexture);
            if (readable == null)
            {
                EditorUtility.DisplayDialog(
                    L("影シェイプリグのフィット", "Shadow Rig Fitter"),
                    L("マスクテクスチャを読み取れませんでした。", "Could not read the mask texture."),
                    "OK");
                return;
            }

            try
            {
                int w = readable.width;
                int h = readable.height;
                Color[] pixels = readable.GetPixels();

                var labels = new int[w * h];
                int nextLabel = 0;
                var components = new List<List<int>>();

                // 連結成分抽出。塗りが複数の島に分かれている前提なので、
                // 4 近傍の幅優先で島ごとにまとめる。
                var queue = new Queue<int>();
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (labels[i] != 0 || pixels[i].r < _threshold) continue;

                    nextLabel++;
                    var member = new List<int>();
                    queue.Clear();
                    queue.Enqueue(i);
                    labels[i] = nextLabel;

                    while (queue.Count > 0)
                    {
                        int idx = queue.Dequeue();
                        member.Add(idx);

                        int x = idx % w;
                        int y = idx / w;

                        TryEnqueue(x - 1, y, w, h, pixels, labels, nextLabel, queue);
                        TryEnqueue(x + 1, y, w, h, pixels, labels, nextLabel, queue);
                        TryEnqueue(x, y - 1, w, h, pixels, labels, nextLabel, queue);
                        TryEnqueue(x, y + 1, w, h, pixels, labels, nextLabel, queue);
                    }

                    components.Add(member);
                }

                // 面積降順で上位 4 つ。小さな塗り残しに枠を取られないようにする。
                components.Sort((a, b) => b.Count.CompareTo(a.Count));

                for (int c = 0; c < components.Count && c < MaxSlots; c++)
                {
                    List<int> member = components[c];
                    if (member.Count < 8) continue;   // ノイズ相当は捨てる

                    _preview.Add(FitEllipse(member, w, h));
                }

                if (_preview.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        L("影シェイプリグのフィット", "Shadow Rig Fitter"),
                        L("しきい値を超える領域が見つかりませんでした。しきい値を下げてみてください。",
                          "No region exceeded the threshold. Try lowering it."),
                        "OK");
                }
            }
            finally
            {
                if (readable != _maskTexture) DestroyImmediate(readable);
            }
        }

        private void TryEnqueue(int x, int y, int w, int h, Color[] pixels, int[] labels, int label, Queue<int> queue)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int idx = y * w + x;
            if (labels[idx] != 0 || pixels[idx].r < _threshold) return;

            labels[idx] = label;
            queue.Enqueue(idx);
        }

        /// <summary>
        /// 重心と二次モーメント行列から楕円を当てる。
        ///
        /// 2x2 対称行列 [[a, b], [b, d]] の固有値は
        ///   lambda = (a + d) / 2 +/- sqrt(((a - d) / 2)^2 + b^2)
        /// で閉じた形に解ける。長軸の向きは atan2(2b, a - d) / 2。
        /// </summary>
        private Blob FitEllipse(List<int> member, int w, int h)
        {
            double sumX = 0, sumY = 0;
            foreach (int idx in member)
            {
                sumX += (idx % w) + 0.5;
                sumY += (idx / w) + 0.5;
            }

            double meanX = sumX / member.Count;
            double meanY = sumY / member.Count;

            double varX = 0, varY = 0, covXY = 0;
            foreach (int idx in member)
            {
                double dx = (idx % w) + 0.5 - meanX;
                double dy = (idx / w) + 0.5 - meanY;
                varX += dx * dx;
                varY += dy * dy;
                covXY += dx * dy;
            }

            varX /= member.Count;
            varY /= member.Count;
            covXY /= member.Count;

            double half = (varX + varY) * 0.5;
            double diff = (varX - varY) * 0.5;
            double root = Math.Sqrt(diff * diff + covXY * covXY);

            double lambdaMajor = half + root;
            double lambdaMinor = Math.Max(half - root, 0.0);

            // 角度は「長軸が X 軸となす角」。covXY が 0 なら軸に揃っている。
            double angleRad = 0.5 * Math.Atan2(2.0 * covXY, varX - varY);

            // 標準偏差 → UV の半径。テクスチャが正方形とは限らないので軸ごとに割る。
            float radiusMajor = (float)(Math.Sqrt(lambdaMajor) * _radiusScale / w);
            float radiusMinor = (float)(Math.Sqrt(lambdaMinor) * _radiusScale / h);

            return new Blob
            {
                Center = new Vector2((float)(meanX / w), (float)(meanY / h)),
                RadiusMajor = Mathf.Max(radiusMajor, 0.001f),
                RadiusMinor = Mathf.Max(radiusMinor, 0.001f),
                RotationDegrees = Mathf.Repeat((float)(angleRad * Mathf.Rad2Deg), 360f),
                PixelCount = member.Count
            };
        }

        // ---- 適用 ----

        private void Apply()
        {
            if (_material == null) return;

            Undo.RecordObject(_material, "Fit Shadow Rig");

            for (int slot = 0; slot < MaxSlots; slot++)
            {
                string paramsName = "_ShadowRigParams" + slot;
                string shapeName = "_ShadowRigShape" + slot;
                if (!_material.HasProperty(paramsName) || !_material.HasProperty(shapeName)) continue;

                if (slot < _preview.Count)
                {
                    Blob b = _preview[slot];
                    _material.SetVector(paramsName,
                        new Vector4(b.Center.x, b.Center.y, b.RadiusMajor, b.RadiusMinor));
                    _material.SetVector(shapeName,
                        new Vector4(b.RotationDegrees, _defaultStrength, _defaultFalloff, 0f));
                }
                else if (_clearUnusedSlots)
                {
                    // 半径 0 = 無効。前回の設定が残って意図しない形が出るのを防ぐ。
                    _material.SetVector(paramsName, new Vector4(0.5f, 0.5f, 0f, 0f));
                    _material.SetVector(shapeName, new Vector4(0f, 0f, 0.5f, 0f));
                }
            }

            _material.SetFloat("_ShadowShapeRig", 1f);
            _material.EnableKeyword("_SHADOW_SHAPE_RIG");

            if (_material.HasProperty("_ShadowRigMask") && _maskTexture != null)
            {
                // 塗った領域の外へリグが effect しないよう、同じマスクを適用範囲にも使う。
                _material.SetTexture("_ShadowRigMask", _maskTexture);
            }

            EditorUtility.SetDirty(_material);

            EditorUtility.DisplayDialog(
                L("影シェイプリグのフィット", "Shadow Rig Fitter"),
                L($"{_preview.Count} 個のリグを適用しました。強さは初期値なので、インスペクタで調整してください。",
                  $"Applied {_preview.Count} rig(s). Strength is at its initial value — tune it in the inspector."),
                "OK");
        }

        /// <summary>
        /// 読み取り不可のテクスチャを RenderTexture 経由でコピーする。
        /// Linear 指定にしないと sRGB 変換が二重にかかり、しきい値がずれる。
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            if (source == null) return null;
            if (source.isReadable) return source;

            RenderTexture rt = RenderTexture.GetTemporary(
                source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;

                var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply(false, false);

                RenderTexture.active = previous;
                return readable;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneToon] マスクテクスチャの読み取りに失敗しました: {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
