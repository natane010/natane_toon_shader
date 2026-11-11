using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// <c>Natane/Screen FX Overlay</c> 専用インスペクタ。
    ///
    /// これまで本体の <see cref="NataneToonShaderGUI"/>（990 プロパティ前提）が当たっていたが、
    /// ScreenFX が持つのは 30 数個だけで、大半のセクションが空振りしていた。
    /// 撮影模倣プリセット（NPR2026 P3）の入口も兼ねて専用に切り出す。
    ///
    /// ScreenFX は GrabPass ベースなので <b>PC 限定</b>。Quest では使わない。
    /// </summary>
    public class NataneScreenFXShaderGUI : ShaderGUI
    {
        private bool _foldToonize = true;
        private bool _foldDistortion = true;
        private bool _foldCinematic = true;
        private bool _foldGradation = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var material = materialEditor.target as Material;
            if (material == null)
            {
                base.OnGUI(materialEditor, properties);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                L("スクリーンFXオーバーレイ", "Screen FX Overlay"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("アニメ撮影（コンポジット）工程の処理を画面全体へ適用します。",
                  "Applies anime compositing-style processing across the whole frame."),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.HelpBox(
                L("⚠ GrabPass ベースのため PC 限定です。Quest では使用しないでください。\n" +
                  "Bloom と被写界深度は非対応です。Bloom は GrabPass 1 枚では多段ダウンサンプルができず、" +
                  "被写界深度は深度テクスチャ前提で VRChat アバターからの利用が不安定なためです。",
                  "⚠ GrabPass based, so PC only. Do not use it on Quest.\n" +
                  "Bloom and depth of field are not supported: bloom needs multi-step downsampling that a " +
                  "single GrabPass cannot provide, and DoF depends on the depth texture, which is unreliable " +
                  "from a VRChat avatar."),
                MessageType.Warning);

            EditorGUILayout.Space(6);
            DrawPresets(material);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(L("合成", "Blend"), EditorStyles.boldLabel);
            Draw(materialEditor, properties, "_Intensity", L("効果の強さ", "Effect Blend"));
            Draw(materialEditor, properties, "_TintColor", L("ティント", "Tint Color"));
            Draw(materialEditor, properties, "_Contrast", L("コントラスト", "Contrast"));
            Draw(materialEditor, properties, "_Saturation", L("彩度", "Saturation"));

            EditorGUILayout.Space(6);
            _foldToonize = EditorGUILayout.Foldout(_foldToonize, L("トゥーン化", "Toonize"), true);
            if (_foldToonize)
            {
                EditorGUI.indentLevel++;
                Draw(materialEditor, properties, "_PosterizeStrength", L("ポスタリゼーション", "Posterize Strength"));
                Draw(materialEditor, properties, "_PosterizeSteps", L("階調数", "Posterize Steps"));
                Draw(materialEditor, properties, "_EdgeStrength", L("輪郭の暗さ", "Edge Darken Strength"));
                Draw(materialEditor, properties, "_EdgeThreshold", L("輪郭のしきい値", "Edge Threshold"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
            _foldDistortion = EditorGUILayout.Foldout(_foldDistortion, L("歪み・ブラー", "Distortion & Blur"), true);
            if (_foldDistortion)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(L("色収差", "Chromatic Aberration"), EditorStyles.miniBoldLabel);
                Draw(materialEditor, properties, "_ChromaticAberration", L("強さ", "Amount"));
                Draw(materialEditor, properties, "_AberrationScale", L("スケール", "Scale"));
                Draw(materialEditor, properties, "_AberrationEdgeOnly", L("周辺のみに限定", "Edge Only"));
                Draw(materialEditor, properties, "_AberrationEdgeStart", L("周辺の開始半径", "Edge Start Radius"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(L("放射ブラー", "Radial Blur"), EditorStyles.miniBoldLabel);
                Draw(materialEditor, properties, "_RadialBlurStrength", L("強さ", "Strength"));
                Draw(materialEditor, properties, "_RadialBlurCenter", L("中心 (screen UV)", "Center (screen UV)"));
                Draw(materialEditor, properties, "_RadialBlurSamples", L("サンプル数", "Samples"));
                Draw(materialEditor, properties, "_RadialBlurEdgeOnly", L("周辺のみに限定", "Edge Only"));
                EditorGUILayout.LabelField(
                    L("サンプル数の上限は 8 に固定しています（最悪コストを抑えるため）。",
                      "Samples are capped at 8 to bound the worst-case cost."),
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
            _foldGradation = EditorGUILayout.Foldout(_foldGradation, L("グラデーション合成", "Gradation"), true);
            if (_foldGradation)
            {
                EditorGUI.indentLevel++;
                Draw(materialEditor, properties, "_GradationBlend", L("合成量", "Blend"));
                Draw(materialEditor, properties, "_GradationMode", L("合成方法", "Mode"));
                Draw(materialEditor, properties, "_GradationAngle", L("方向", "Angle"));
                Draw(materialEditor, properties, "_GradationColorA", L("色 A（進行方向側）", "Color A (toward the angle)"));
                Draw(materialEditor, properties, "_GradationColorB", L("色 B（反対側）", "Color B (opposite)"));
                Draw(materialEditor, properties, "_GradationTexture", L("ランプテクスチャ（任意）", "Ramp Texture (optional)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
            _foldCinematic = EditorGUILayout.Foldout(_foldCinematic, L("撮影処理", "Cinematic"), true);
            if (_foldCinematic)
            {
                EditorGUI.indentLevel++;
                Draw(materialEditor, properties, "_Vignette", L("ビネット", "Vignette"));
                Draw(materialEditor, properties, "_VignetteSoftness", L("ビネットのやわらかさ", "Vignette Softness"));
                Draw(materialEditor, properties, "_MonochromeStrength", L("モノクロ化", "Monochrome"));
                Draw(materialEditor, properties, "_MonochromeEdgeOnly", L("周辺のみモノクロ", "Monochrome Edge Only"));
                Draw(materialEditor, properties, "_GrainStrength", L("フィルムグレイン", "Film Grain"));
                Draw(materialEditor, properties, "_GrainScale", L("グレインの粒度", "Grain Scale"));
                Draw(materialEditor, properties, "_GrainSpeed", L("グレインの速さ", "Grain Speed"));
                Draw(materialEditor, properties, "_ScanlineStrength", L("走査線", "Scanline"));
                Draw(materialEditor, properties, "_ScanlineDensity", L("走査線の密度", "Scanline Density"));
                Draw(materialEditor, properties, "_ScanlineSpeed", L("走査線の速さ", "Scanline Speed"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
            materialEditor.RenderQueueField();
        }

        private static void DrawPresets(Material material)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("撮影模倣プリセット", "Cinematic Presets"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L("適用すると、追加パラメータは一度ニュートラルへ戻してから設定します。",
                  "Applying a preset resets the added parameters to neutral first."),
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            PresetButton(material, ScreenFXSetupTool.CinematicPreset.Cinematic, L("劇場", "Cinematic"));
            PresetButton(material, ScreenFXSetupTool.CinematicPreset.Impact, L("必殺技", "Impact"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            PresetButton(material, ScreenFXSetupTool.CinematicPreset.Flashback, L("回想", "Flashback"));
            PresetButton(material, ScreenFXSetupTool.CinematicPreset.Serious, L("シリアス", "Serious"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private static void PresetButton(Material material, ScreenFXSetupTool.CinematicPreset preset, string label)
        {
            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                ScreenFXSetupTool.ApplyCinematicPreset(material, preset);
            }
        }

        /// <summary>
        /// 無いプロパティは黙って飛ばす。古いマテリアルを開いても例外にならないようにする。
        /// </summary>
        private static void Draw(MaterialEditor editor, MaterialProperty[] properties, string name, string label)
        {
            MaterialProperty property = FindProperty(name, properties, false);
            if (property == null) return;
            editor.ShaderProperty(property, label);
        }
    }
}
