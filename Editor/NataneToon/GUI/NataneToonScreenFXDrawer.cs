using UnityEngine;
using UnityEditor;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    /// <summary>
    /// Screen FX Overlay シェーダー用のDrawer
    /// NataneToonShaderGUIから委譲される
    /// </summary>
    public class NataneToonScreenFXDrawer : NataneToonShaderGUITab
    {
        private static GUIStyle _headerStyle;
        private static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 16,
                        alignment = TextAnchor.MiddleCenter
                    };
                    _headerStyle.normal.textColor = new Color(1f, 0.6f, 0.8f);
                }

                return _headerStyle;
            }
        }

        private static GUIStyle _footerStyle;
        private static GUIStyle FooterStyle
        {
            get
            {
                if (_footerStyle == null)
                {
                    _footerStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };
                }

                return _footerStyle;
            }
        }

        private bool showBlend = true;
        private bool showToonize = true;
        private bool showDistortion = true;
        private bool showCinematic = true;

        private string prefsPrefix;

        public override void Initialize(MaterialEditor materialEditor, MaterialProperty[] properties, Material targetMaterial)
        {
            base.Initialize(materialEditor, properties, targetMaterial);
            prefsPrefix = NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, "SFX_");
            LoadFoldoutStates();
        }

        public override void LoadFoldoutStates()
        {
            if (targetMaterial == null) return;
            showBlend = EditorPrefs.GetBool(prefsPrefix + "Blend", true);
            showToonize = EditorPrefs.GetBool(prefsPrefix + "Toonize", true);
            showDistortion = EditorPrefs.GetBool(prefsPrefix + "Distortion", true);
            showCinematic = EditorPrefs.GetBool(prefsPrefix + "Cinematic", true);
        }

        public override void SaveFoldoutStates()
        {
            if (targetMaterial == null) return;
            EditorPrefs.SetBool(prefsPrefix + "Blend", showBlend);
            EditorPrefs.SetBool(prefsPrefix + "Toonize", showToonize);
            EditorPrefs.SetBool(prefsPrefix + "Distortion", showDistortion);
            EditorPrefs.SetBool(prefsPrefix + "Cinematic", showCinematic);
        }

        public override void Draw()
        {
            DrawHeader();
            EditorGUILayout.Space(5);

            SafeDrawSection(() => DrawBlendSection(), "Blend");
            SafeDrawSection(() => DrawToonizeSection(), "Toonize");
            SafeDrawSection(() => DrawDistortionSection(), "Distortion");
            SafeDrawSection(() => DrawCinematicSection(), "Cinematic");

            EditorGUILayout.Space(10);
            DrawFooter();
        }

        private void DrawHeader()
        {
            NataneToonInspectorComponents.DrawInspectorHeader(
                "Natane Toon Shader",
                L("カメラ全体のスクリーン演出", "Full-screen Camera Effects"),
                "Screen FX",
                "GrabPass",
                NataneInspectorStatus.Warning,
                () =>
                {
                    NataneToonLocalization.ToggleLanguage();
                    materialEditor?.Repaint();
                },
                null);
        }

        private void DrawBlendSection()
        {
            EditorGUI.BeginChangeCheck();
            showBlend = EditorGUILayout.Foldout(showBlend, L("ブレンド / エフェクト基本", "Blend / Effect Basics"), true, EditorStyles.foldoutHeader);
            if (EditorGUI.EndChangeCheck()) SaveFoldoutStates();

            if (showBlend)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_Intensity", L("エフェクト強度", "Effect Intensity"));
                DrawProperty("_TintColor", L("ティントカラー", "Tint Color"));
                DrawProperty("_Contrast", L("コントラスト", "Contrast"));
                DrawProperty("_Saturation", L("彩度", "Saturation"));
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(L("Intensityで全体のエフェクト適用率を調整できます。0=オフ、1=フル適用。", "Adjust the overall effect application rate with Intensity. 0=Off, 1=Full."), MessageType.None);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawToonizeSection()
        {
            EditorGUI.BeginChangeCheck();
            showToonize = EditorGUILayout.Foldout(showToonize, L("トゥーン化 / ポスタリゼーション", "Toonize / Posterization"), true, EditorStyles.foldoutHeader);
            if (EditorGUI.EndChangeCheck()) SaveFoldoutStates();

            if (showToonize)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_PosterizeStrength", L("ポスタリゼーション強度", "Posterization Strength"));
                DrawProperty("_PosterizeSteps", L("ポスタリゼーション段数", "Posterization Steps"));
                DrawProperty("_EdgeStrength", L("エッジ暗化強度", "Edge Darkening Strength"));
                DrawProperty("_EdgeThreshold", L("エッジしきい値", "Edge Threshold"));
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(L("ポスタリゼーションで画面全体をトゥーン調にします。Steps数が少ないほど強い効果。", "Posterization applies a toon look to the entire screen. Fewer steps = stronger effect."), MessageType.None);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawDistortionSection()
        {
            EditorGUI.BeginChangeCheck();
            showDistortion = EditorGUILayout.Foldout(showDistortion, L("画面歪み / 色収差", "Screen Distortion / Chromatic Aberration"), true, EditorStyles.foldoutHeader);
            if (EditorGUI.EndChangeCheck()) SaveFoldoutStates();

            if (showDistortion)
            {
                EditorGUI.indentLevel++;
                DrawProperty("_ChromaticAberration", L("色収差", "Chromatic Aberration"));
                DrawProperty("_AberrationScale", L("収差スケール", "Aberration Scale"));
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(L("色収差でRGBの分離エフェクトを適用します。値を大きくするほど強い歪み。", "Applies RGB separation effect with chromatic aberration. Higher values = stronger distortion."), MessageType.None);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawCinematicSection()
        {
            EditorGUI.BeginChangeCheck();
            showCinematic = EditorGUILayout.Foldout(showCinematic, L("シネマティック", "Cinematic"), true, EditorStyles.foldoutHeader);
            if (EditorGUI.EndChangeCheck()) SaveFoldoutStates();

            if (showCinematic)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField(L("ビネット", "Vignette"), EditorStyles.miniBoldLabel);
                DrawProperty("_Vignette", L("ビネット強度", "Vignette Strength"));
                DrawProperty("_VignetteSoftness", L("ビネット柔らかさ", "Vignette Softness"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("スキャンライン", "Scanline"), EditorStyles.miniBoldLabel);
                DrawProperty("_ScanlineStrength", L("スキャンライン強度", "Scanline Strength"));
                DrawProperty("_ScanlineDensity", L("スキャンライン密度", "Scanline Density"));
                DrawProperty("_ScanlineSpeed", L("スキャンライン速度", "Scanline Speed"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("フィルムグレイン", "Film Grain"), EditorStyles.miniBoldLabel);
                DrawProperty("_GrainStrength", L("グレイン強度", "Grain Strength"));
                DrawProperty("_GrainScale", L("グレインスケール", "Grain Scale"));
                DrawProperty("_GrainSpeed", L("グレイン速度", "Grain Speed"));

                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(L("ビネット=画面端の暗化、スキャンライン=CRTモニター風、グレイン=フィルムノイズ", "Vignette=edge darkening, Scanline=CRT monitor style, Grain=film noise"), MessageType.None);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("ナタネ スクリーンFXオーバーレイ v1.1", "Natane Screen FX Overlay v1.1"), FooterStyle);
            EditorGUILayout.LabelField(L("GrabPassを使用 - パフォーマンスに注意", "Uses GrabPass - watch performance"), FooterStyle);
            EditorGUILayout.EndVertical();
        }
    }
}
