using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Creates a camera-attached full-screen quad for Natane screen FX.
    /// Runtime scripts are not required, so this is VRC-safe for worlds.
    /// </summary>
    internal static class ScreenFXSetupTool
    {
        private const string ScreenFxShaderName = "Natane/Screen FX Overlay";
        private const string MaterialFolderRoot = "Assets/NataneToon";
        private const string MaterialFolder = "Assets/NataneToon/ScreenFX";

        // Temporarily disabled while ScreenFX support is on hold.
        // [MenuItem(NataneToolMenuPaths.ScreenFXSetup, false, 45)]
        private static void CreateScreenFxOverlay()
        {
            var camera = ResolveTargetCamera();
            if (camera == null)
            {
                EditorUtility.DisplayDialog(
                    L("Camera Not Found", "Camera Not Found"),
                    L("No camera was found in the scene.\nSelect a camera object, or set the MainCamera tag and try again.", "No camera was found in the scene.\nSelect a camera object, or set the MainCamera tag and try again."),
                    "OK");
                return;
            }

            var shader = Shader.Find(ScreenFxShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog(
                    L("Shader Not Found", "Shader Not Found"),
                    L($"Shader '{ScreenFxShaderName}' was not found.", $"Shader '{ScreenFxShaderName}' was not found."),
                    "OK");
                return;
            }

            EnsureFolders();
            string materialPath = AssetDatabase.GenerateUniqueAssetPath($"{MaterialFolder}/NataneScreenFXOverlay.mat");
            var material = new Material(shader);
            ApplyRecommendedDefaults(material);
            AssetDatabase.CreateAsset(material, materialPath);

            var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "NataneScreenFXOverlay";
            overlay.transform.SetParent(camera.transform, false);
            PlaceOverlayToCamera(overlay.transform, camera);

            var collider = overlay.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            var renderer = overlay.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            Undo.RegisterCreatedObjectUndo(overlay, "Create Screen FX Overlay");
            Selection.activeGameObject = overlay;

            EditorUtility.DisplayDialog(
                L("Screen FX Overlay Created", "Screen FX Overlay Created"),
                L("A Screen FX overlay quad was created, parented to the target camera, and assigned a new material asset.", "A Screen FX overlay quad was created, parented to the target camera, and assigned a new material asset."),
                "OK");
        }

        private static Camera ResolveTargetCamera()
        {
            if (Selection.activeGameObject != null)
            {
                var selectedCamera = Selection.activeGameObject.GetComponent<Camera>();
                if (selectedCamera != null)
                {
                    return selectedCamera;
                }
            }

            if (Camera.main != null)
            {
                return Camera.main;
            }

            return NataneEditorCompat.FindObjectOfTypeCompat<Camera>();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolderRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NataneToon");
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder(MaterialFolderRoot, "ScreenFX");
            }
        }

        private static void ApplyRecommendedDefaults(Material material)
        {
            material.SetFloat("_Intensity", 1.0f);
            material.SetFloat("_PosterizeStrength", 0.15f);
            material.SetFloat("_PosterizeSteps", 8.0f);
            material.SetFloat("_EdgeStrength", 0.25f);
            material.SetFloat("_EdgeThreshold", 0.1f);
            material.SetFloat("_Vignette", 0.1f);
            material.SetFloat("_VignetteSoftness", 0.35f);
            material.SetFloat("_ScanlineStrength", 0.0f);
            material.SetFloat("_GrainStrength", 0.04f);
        }

        // ===== 撮影模倣プリセット (NPR2026 P3) =====
        //
        // アニメの撮影（コンポジット）工程で定番になっている処理の組み合わせを、
        // そのまま選べる形にしたもの。個々のパラメータを一から詰めるより早く、
        // 「どの数値が効いているか」を読み取る教材にもなる。
        //
        // ScreenFX は GrabPass ベースなので PC 限定。Quest では使わないこと。

        /// <summary>撮影模倣プリセットの種類。</summary>
        public enum CinematicPreset
        {
            /// <summary>劇場: 落ち着いた画。周辺だけ色収差、寒暖のグラデ。</summary>
            Cinematic,

            /// <summary>必殺技: 放射ブラー + 周辺モノクロで中心へ視線を集める。</summary>
            Impact,

            /// <summary>回想: 全画面モノクロ寄り + セピア + 粒子。</summary>
            Flashback,

            /// <summary>シリアス: 彩度を落としコントラストを上げ、下方向を暗く沈める。</summary>
            Serious
        }

        /// <summary>
        /// プリセットを適用する。追加パラメータを持たない古いマテリアルでも
        /// 落ちないよう、全て HasProperty で確認してから書き込む。
        /// </summary>
        public static void ApplyCinematicPreset(Material material, CinematicPreset preset)
        {
            if (material == null) return;

            Undo.RecordObject(material, "Apply ScreenFX Preset");

            // 追加分を一度ニュートラルへ戻す。前のプリセットの設定が混ざると
            // 「効いていないのに残っている」パラメータで混乱するため。
            Set(material, "_RadialBlurStrength", 0f);
            Set(material, "_MonochromeStrength", 0f);
            Set(material, "_GradationBlend", 0f);
            Set(material, "_AberrationEdgeOnly", 0f);

            switch (preset)
            {
                case CinematicPreset.Cinematic:
                    Set(material, "_Vignette", 0.35f);
                    Set(material, "_GrainStrength", 0.03f);
                    Set(material, "_Contrast", 1.08f);
                    Set(material, "_ChromaticAberration", 0.3f);
                    Set(material, "_AberrationEdgeOnly", 1f);
                    Set(material, "_AberrationEdgeStart", 0.4f);
                    Set(material, "_GradationBlend", 0.15f);
                    Set(material, "_GradationMode", 0f);                   // Multiply
                    Set(material, "_GradationAngle", 0f);                  // 上下方向
                    SetColor(material, "_GradationColorA", new Color(1.00f, 0.92f, 0.80f, 1f)); // 上: 暖色
                    SetColor(material, "_GradationColorB", new Color(0.78f, 0.86f, 1.00f, 1f)); // 下: 寒色
                    break;

                case CinematicPreset.Impact:
                    Set(material, "_RadialBlurStrength", 0.6f);
                    Set(material, "_RadialBlurSamples", 6f);
                    Set(material, "_RadialBlurEdgeOnly", 1f);
                    Set(material, "_MonochromeStrength", 0.5f);
                    Set(material, "_MonochromeEdgeOnly", 1f);
                    Set(material, "_ChromaticAberration", 0.8f);
                    Set(material, "_AberrationEdgeOnly", 1f);
                    Set(material, "_Vignette", 0.5f);
                    Set(material, "_Contrast", 1.12f);
                    break;

                case CinematicPreset.Flashback:
                    Set(material, "_MonochromeStrength", 0.75f);
                    Set(material, "_MonochromeEdgeOnly", 0f);              // 全画面
                    Set(material, "_GrainStrength", 0.08f);
                    Set(material, "_Vignette", 0.45f);
                    Set(material, "_GradationBlend", 0.25f);
                    Set(material, "_GradationMode", 1f);                   // Screen
                    SetColor(material, "_GradationColorA", new Color(0.75f, 0.60f, 0.38f, 1f)); // セピア
                    SetColor(material, "_GradationColorB", new Color(0.30f, 0.24f, 0.18f, 1f));
                    break;

                case CinematicPreset.Serious:
                    Set(material, "_Saturation", 0.7f);
                    Set(material, "_Contrast", 1.15f);
                    Set(material, "_GradationBlend", 0.3f);
                    Set(material, "_GradationMode", 0f);                   // Multiply
                    Set(material, "_GradationAngle", 0f);
                    SetColor(material, "_GradationColorA", new Color(1f, 1f, 1f, 1f));          // 上はそのまま
                    SetColor(material, "_GradationColorB", new Color(0.45f, 0.47f, 0.55f, 1f)); // 下を沈める
                    Set(material, "_Vignette", 0.3f);
                    break;
            }

            EditorUtility.SetDirty(material);
        }

        private static void Set(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void PlaceOverlayToCamera(Transform overlay, Camera camera)
        {
            float distance = Mathf.Max(camera.nearClipPlane + 0.05f, 0.1f);
            float width;
            float height;

            if (camera.orthographic)
            {
                height = camera.orthographicSize * 2.0f;
                width = height * camera.aspect;
            }
            else
            {
                height = 2.0f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                width = height * camera.aspect;
            }

            overlay.localPosition = new Vector3(0.0f, 0.0f, distance);
            overlay.localRotation = Quaternion.identity;
            overlay.localScale = new Vector3(width, height, 1.0f);
        }
    }
}
