using UnityEngine;
using UnityEditor;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// Drawer for the Eye shader inspector tab.
    /// Extracted from NataneToonShaderGUI to keep the main inspector modular.
    /// </summary>
    public class NataneToonEyeDrawer : NataneToonShaderGUITab
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
                    _headerStyle.normal.textColor = new Color(0.8f, 0.6f, 1f);
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

        // Foldout states
        private bool showEyeState = true;
        private bool showMainSettings = true;
        private bool showRealisticEye = true;
        private bool showDualCenter = true;
        private bool showRegionMask = false;
        private bool showStateNormal = true;
        private bool showStateStar = false;
        private bool showStateHeart = false;
        private bool showStateDead = false;
        private bool showStateNervous = false;
        private bool showHueColor = true;
        private bool showBubble = false;
        private bool showIrisCaustics = false;
        private bool showIrisRingPulse = false;
        private bool showTexturePolish = false;
        private bool showExpression = false;
        private bool showInnerMeshPriority = false;
        private bool showPerformance = false;
        private bool showVignette = false;
        private bool showAudioLink = false;
        private bool showRendering = false;

        private string prefsPrefix;

        public override void Initialize(MaterialEditor materialEditor, MaterialProperty[] properties, Material targetMaterial)
        {
            base.Initialize(materialEditor, properties, targetMaterial);
            prefsPrefix = NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, "Eye_");
            LoadFoldoutStates();
        }

        public override void LoadFoldoutStates()
        {
            if (targetMaterial == null) return;
            showEyeState = EditorPrefs.GetBool(prefsPrefix + "EyeState", true);
            showMainSettings = EditorPrefs.GetBool(prefsPrefix + "Main", true);
            showRealisticEye = EditorPrefs.GetBool(prefsPrefix + "RealisticEye", true);
            showDualCenter = EditorPrefs.GetBool(prefsPrefix + "DualCenter", true);
            showRegionMask = EditorPrefs.GetBool(prefsPrefix + "RegionMask", false);
            showStateNormal = EditorPrefs.GetBool(prefsPrefix + "StateNormal", true);
            showStateStar = EditorPrefs.GetBool(prefsPrefix + "StateStar", false);
            showStateHeart = EditorPrefs.GetBool(prefsPrefix + "StateHeart", false);
            showStateDead = EditorPrefs.GetBool(prefsPrefix + "StateDead", false);
            showStateNervous = EditorPrefs.GetBool(prefsPrefix + "StateNervous", false);
            showHueColor = EditorPrefs.GetBool(prefsPrefix + "HueColor", true);
            showBubble = EditorPrefs.GetBool(prefsPrefix + "Bubble", false);
            showIrisCaustics = EditorPrefs.GetBool(prefsPrefix + "IrisCaustics", false);
            showIrisRingPulse = EditorPrefs.GetBool(prefsPrefix + "IrisRingPulse", false);
            showTexturePolish = EditorPrefs.GetBool(prefsPrefix + "TexturePolish", false);
            showExpression = EditorPrefs.GetBool(prefsPrefix + "Expression", false);
            showInnerMeshPriority = EditorPrefs.GetBool(prefsPrefix + "InnerMesh", false);
            showPerformance = EditorPrefs.GetBool(prefsPrefix + "Performance", false);
            showVignette = EditorPrefs.GetBool(prefsPrefix + "Vignette", false);
            showAudioLink = EditorPrefs.GetBool(prefsPrefix + "AudioLink", false);
            showRendering = EditorPrefs.GetBool(prefsPrefix + "Rendering", false);
        }

        public override void SaveFoldoutStates()
        {
            if (targetMaterial == null) return;
            EditorPrefs.SetBool(prefsPrefix + "EyeState", showEyeState);
            EditorPrefs.SetBool(prefsPrefix + "Main", showMainSettings);
            EditorPrefs.SetBool(prefsPrefix + "RealisticEye", showRealisticEye);
            EditorPrefs.SetBool(prefsPrefix + "DualCenter", showDualCenter);
            EditorPrefs.SetBool(prefsPrefix + "RegionMask", showRegionMask);
            EditorPrefs.SetBool(prefsPrefix + "StateNormal", showStateNormal);
            EditorPrefs.SetBool(prefsPrefix + "StateStar", showStateStar);
            EditorPrefs.SetBool(prefsPrefix + "StateHeart", showStateHeart);
            EditorPrefs.SetBool(prefsPrefix + "StateDead", showStateDead);
            EditorPrefs.SetBool(prefsPrefix + "StateNervous", showStateNervous);
            EditorPrefs.SetBool(prefsPrefix + "HueColor", showHueColor);
            EditorPrefs.SetBool(prefsPrefix + "Bubble", showBubble);
            EditorPrefs.SetBool(prefsPrefix + "IrisCaustics", showIrisCaustics);
            EditorPrefs.SetBool(prefsPrefix + "IrisRingPulse", showIrisRingPulse);
            EditorPrefs.SetBool(prefsPrefix + "TexturePolish", showTexturePolish);
            EditorPrefs.SetBool(prefsPrefix + "Expression", showExpression);
            EditorPrefs.SetBool(prefsPrefix + "InnerMesh", showInnerMeshPriority);
            EditorPrefs.SetBool(prefsPrefix + "Performance", showPerformance);
            EditorPrefs.SetBool(prefsPrefix + "Vignette", showVignette);
            EditorPrefs.SetBool(prefsPrefix + "AudioLink", showAudioLink);
            EditorPrefs.SetBool(prefsPrefix + "Rendering", showRendering);
        }

        public override void Draw()
        {
            DrawHeader();
            EditorGUILayout.Space(5);

            // Usage notes for partial and symmetrical UV layouts.
            EditorGUILayout.HelpBox(
                L("Partial UV (top-right/top-left etc.): Set Eye Select Mode to 'Nearest Center'.\n" +
                  "Symmetrical UV: Set Eye Center Mode to 'Symmetry From Center1' and adjust Symmetry Pivot X.", "Partial UV (top-right/top-left etc.): Set Eye Select Mode to 'Nearest Center'.\n" +
                  "Symmetrical UV: Set Eye Center Mode to 'Symmetry From Center1' and adjust Symmetry Pivot X."),
                MessageType.Info);
            EditorGUILayout.Space(5);

            SafeDrawSection(() => DrawEyeStateSection(), "Eye State");
            SafeDrawSection(() => DrawMainSettingsSection(), "Main Settings");
            SafeDrawSection(() => DrawRealisticEyeSection(), "Realistic Eye");
            SafeDrawSection(() => DrawDualCenterSection(), "Dual Center");
            SafeDrawSection(() => DrawRegionMaskSection(), "Region Mask");

            // Eye state sections.
            SafeDrawSection(() => DrawStateNormalSection(), "State Normal");
            SafeDrawSection(() => DrawStateStarSection(), "State Star");
            SafeDrawSection(() => DrawStateHeartSection(), "State Heart");
            SafeDrawSection(() => DrawStateDeadSection(), "State Dead");
            SafeDrawSection(() => DrawStateNervousSection(), "State Nervous");

            SafeDrawSection(() => DrawHueColorSection(), "Hue Color");
            SafeDrawSection(() => DrawBubbleSection(), "Bubble");
            SafeDrawSection(() => DrawIrisCausticsSection(), "Iris Caustics");
            SafeDrawSection(() => DrawIrisRingPulseSection(), "Iris Ring Pulse");
            SafeDrawSection(() => DrawTexturePolishSection(), "Texture Polish");
            SafeDrawSection(() => DrawExpressionSection(), "Expression");
            SafeDrawSection(() => DrawInnerMeshPrioritySection(), "Inner Mesh Priority");
            SafeDrawSection(() => DrawPerformanceSection(), "Performance");
            SafeDrawSection(() => DrawVignetteSection(), "Vignette");
            SafeDrawSection(() => DrawAudioLinkSection(), "Audio Link");
            SafeDrawSection(() => DrawRenderingSection(), "Rendering");

            EditorGUILayout.Space(10);
            DrawFooter();
        }

        private void DrawHeader()
        {
            NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate budget = NataneToonSamplerBudgetEstimator.Estimate(targetMaterial);
            NataneToonInspectorComponents.DrawInspectorHeader(
                "Natane Toon Shader",
                L("瞳と表情のセットアップ", "Eye & Expression Setup"),
                "Eye",
                $"S {budget.EstimatedSamplers}/{budget.Limit}",
                budget.IsOverLimit
                    ? NataneInspectorStatus.Error
                    : budget.IsWarning ? NataneInspectorStatus.Warning : NataneInspectorStatus.Success,
                () =>
                {
                    NataneToonLocalization.ToggleLanguage();
                    materialEditor?.Repaint();
                },
                null);
        }

        // ===== Foldout Helper =====
        private bool DrawFoldout(ref bool state, string label)
        {
            EditorGUI.BeginChangeCheck();
            state = EditorGUILayout.Foldout(state, label, true, NataneToonShaderGUIStyles.SectionHeaderFoldout);
            if (EditorGUI.EndChangeCheck()) SaveFoldoutStates();
            return state;
        }

        // ===== Section Implementations =====

        private void DrawEyeStateSection()
        {
            if (!DrawFoldout(ref showEyeState, L("目の状態", "Eye State"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_EyeState", L("目の状態 (通常/星/ハート/死んだ目/緊張)", "Eye State (Normal/Star/Heart/Dead/Nervous)"));
            EditorGUILayout.HelpBox(
                L("通常 / 星 / ハート / 死んだ目 / 緊張", "Normal / Star / Heart / Dead / Nervous"),
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawMainSettingsSection()
        {
            if (!DrawFoldout(ref showMainSettings, L("基本設定", "Main Settings"))) return;
            EditorGUI.indentLevel++;
            MaterialProperty mainTexProp = FindProperty("_MainTex", properties, false);
            if (mainTexProp != null) materialEditor.TextureProperty(mainTexProp, L("Base Texture", "Base Texture"));
            DrawToggle("_USE_TEXTURE", "_UseTexture", L("Use Texture", "Use Texture"));
            DrawProperty("_TextureBlend", L("Texture Blend", "Texture Blend"));
            DrawProperty("_BlendMode", L("Blend Mode", "Blend Mode"));
            DrawProperty("_MainParallax", L("Parallax Intensity", "Parallax Intensity"));
            DrawProperty("_MainColor", L("Glare Color (HDR)", "Glare Color (HDR)"));
            DrawProperty("_BackgroundColor", L("Background Color (HDR)", "Background Color (HDR)"));
            DrawProperty("_PupilSize", L("Pupil Size", "Pupil Size"));
            DrawProperty("_PupilAspect", L("Pupil Aspect XY (1,1=Circle)", "Pupil Aspect XY (1,1=Circle)"));
            EditorGUI.indentLevel--;
        }

        private void DrawRealisticEyeSection()
        {
            if (!DrawFoldout(ref showRealisticEye, L("リアルな瞳", "Realistic Eye"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_UseRealisticEye", L("Enable Realistic Eye", "Enable Realistic Eye"));
            DrawProperty("_IrisDepth", L("Iris Depth", "Iris Depth"));
            DrawProperty("_IrisDepthRadius", L("Iris Radius", "Iris Radius"));
            DrawProperty("_LimbalRingColor", L("Limbal Ring Color (HDR)", "Limbal Ring Color (HDR)"));
            DrawProperty("_LimbalRingWidth", L("Limbal Ring Width", "Limbal Ring Width"));
            DrawProperty("_LimbalRingIntensity", L("Limbal Ring Intensity", "Limbal Ring Intensity"));
            DrawProperty("_ScleraTint", L("Sclera Tint (HDR)", "Sclera Tint (HDR)"));
            DrawProperty("_ScleraShadowStrength", L("Sclera Shadow Strength", "Sclera Shadow Strength"));
            DrawProperty("_CorneaSpecColor", L("Cornea Spec Color (HDR)", "Cornea Spec Color (HDR)"));
            DrawProperty("_CorneaSpecIntensity", L("Cornea Spec Intensity", "Cornea Spec Intensity"));
            DrawProperty("_CorneaSpecSmoothness", L("Cornea Smoothness", "Cornea Smoothness"));
            DrawProperty("_CorneaFresnelPower", L("Cornea Fresnel", "Cornea Fresnel"));
            EditorGUILayout.HelpBox(
                L("虹彩の奥行き、輪部リング、白目の陰影、角膜風のハイライトを追加します。まず『リアルな瞳』を有効にしてから、虹彩の奥行きや角膜まわりを調整してください。", "Adds iris depth, limbal ring, sclera shading, and a cornea-like top highlight. Enable Realistic Eye first, then tune Iris Depth and Cornea controls."),
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawDualCenterSection()
        {
            if (!DrawFoldout(ref showDualCenter, L("左右の目中心", "Dual Eye Center"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_EyeCenter1", L("Eye Center 1 (UV)", "Eye Center 1 (UV)"));
            DrawProperty("_EyeCenter2", L("Eye Center 2 (UV)", "Eye Center 2 (UV)"));
            DrawProperty("_EyeSeparationX", L("Left/Right Separation X Threshold", "Left/Right Separation X Threshold"));
            DrawProperty("_EyeSelectMode", L("Eye Select Mode", "Eye Select Mode"));
            DrawProperty("_EyeCenterMode", L("Eye Center Mode", "Eye Center Mode"));
            DrawProperty("_EyeSymmetryPivotX", L("Symmetry Pivot X", "Symmetry Pivot X"));
            DrawProperty("_MirrorRightEyeUV", L("Mirror Right Eye UV", "Mirror Right Eye UV"));
            EditorGUILayout.HelpBox(
                L("Nearest Center: UV 座標に最も近い中心を選びます。\n" +
                  "Symmetry From Center1: Center1 を基準に左右対称化します。", "Nearest Center: Selects the center closest to the UV coordinate.\n" +
                  "Symmetry From Center1: Symmetrizes based on Center1."),
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawRegionMaskSection()
        {
            if (!DrawFoldout(ref showRegionMask, L("目の領域マスク", "Eye Region Mask"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_UseEyeRegionMask", L("Use Region Mask", "Use Region Mask"));
            MaterialProperty maskProp = FindProperty("_EyeRegionMask", properties, false);
            if (maskProp != null) materialEditor.TextureProperty(maskProp, L("Mask Texture", "Mask Texture"));
            DrawProperty("_EyeMaskChannel", L("Mask Channel", "Mask Channel"));
            DrawProperty("_EyeMaskThreshold", L("Mask Threshold", "Mask Threshold"));
            DrawProperty("_EyeMaskSoftness", L("Mask Softness", "Mask Softness"));
            DrawProperty("_EyeMaskInvert", L("Invert Mask", "Invert Mask"));
            EditorGUILayout.HelpBox(L("顔と目が同じマテリアルを共有しているときに使います。マスクで目の範囲を指定してください。", "Use when the face and eyes share the same material. Specify the eye region with a mask."), MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawStateNormalSection()
        {
            if (!DrawFoldout(ref showStateNormal, L("通常状態", "Normal State"))) return;
            EditorGUI.indentLevel++;
            MaterialProperty texProp = FindProperty("_NormalStateTex", properties, false);
            if (texProp != null) materialEditor.TextureProperty(texProp, L("Normal State Texture", "Normal State Texture"));
            DrawProperty("_NormalPupilColor", L("Pupil Color (HDR)", "Pupil Color (HDR)"));
            DrawProperty("_DetailBrightness", L("Detail Brightness", "Detail Brightness"));
            EditorGUI.indentLevel--;
        }

        private void DrawStateStarSection()
        {
            if (!DrawFoldout(ref showStateStar, L("星状態", "Star State"))) return;
            EditorGUI.indentLevel++;
            MaterialProperty texProp = FindProperty("_StarStateTex", properties, false);
            if (texProp != null) materialEditor.TextureProperty(texProp, L("Star Texture", "Star Texture"));
            DrawProperty("_StarColor", L("Star Color (HDR)", "Star Color (HDR)"));
            DrawProperty("_StarRockSharpness", L("Rock Sharpness", "Rock Sharpness"));
            DrawProperty("_StarRockAngle", L("Rock Angle", "Rock Angle"));
            DrawProperty("_StarRockSpeed", L("Rock Speed", "Rock Speed"));
            EditorGUI.indentLevel--;
        }

        private void DrawStateHeartSection()
        {
            if (!DrawFoldout(ref showStateHeart, L("ハート状態", "Heart State"))) return;
            EditorGUI.indentLevel++;
            MaterialProperty texProp = FindProperty("_HeartStateTex", properties, false);
            if (texProp != null) materialEditor.TextureProperty(texProp, L("Heart Texture", "Heart Texture"));
            DrawProperty("_HeartPupilColor", L("Heart Color (HDR)", "Heart Color (HDR)"));
            DrawProperty("_HeartPulseSpeed", L("Pulse Speed", "Pulse Speed"));
            DrawProperty("_HeartPulsePower", L("Pulse Power", "Pulse Power"));
            EditorGUI.indentLevel--;
        }

        private void DrawStateDeadSection()
        {
            if (!DrawFoldout(ref showStateDead, L("死んだ目状態", "Dead State"))) return;
            EditorGUI.indentLevel++;
            MaterialProperty texProp = FindProperty("_DeadStateTex", properties, false);
            if (texProp != null) materialEditor.TextureProperty(texProp, L("Dead Eye Texture", "Dead Eye Texture"));
            DrawProperty("_DeadGradientOffset", L("Gradient Offset", "Gradient Offset"));
            DrawProperty("_DeadTopColor", L("Top Color", "Top Color"));
            DrawProperty("_DeadBottomColor", L("Bottom Color", "Bottom Color"));
            EditorGUI.indentLevel--;
        }

        private void DrawStateNervousSection()
        {
            if (!DrawFoldout(ref showStateNervous, L("緊張状態", "Nervous State"))) return;
            EditorGUI.indentLevel++;
            MaterialProperty texProp = FindProperty("_NervousStateTex", properties, false);
            if (texProp != null) materialEditor.TextureProperty(texProp, L("Nervous Texture", "Nervous Texture"));
            DrawProperty("_NervousBackgroundColor", L("Background Color (HDR)", "Background Color (HDR)"));
            DrawProperty("_NervousLinesColor", L("Lines Color (HDR)", "Lines Color (HDR)"));
            DrawProperty("_NervousLinesRandSeed", L("Random Seed", "Random Seed"));
            DrawProperty("_NervousLinesRandOffs", L("Lines Offset", "Lines Offset"));
            DrawProperty("_NervousLinesSize", L("Lines Size", "Lines Size"));
            DrawProperty("_NervousLinesThickness", L("Lines Thickness", "Lines Thickness"));
            DrawProperty("_NervousCenterFill", L("Center Fill", "Center Fill"));
            EditorGUI.indentLevel--;
        }

        private void DrawHueColorSection()
        {
            if (!DrawFoldout(ref showHueColor, L("色味調整", "Hue & Color"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_Contrast", L("Contrast", "Contrast"));
            DrawProperty("_MainSaturation", L("Saturation", "Saturation"));
            DrawProperty("_MainHueShift", L("Hue Offset", "Hue Offset"));
            DrawProperty("_MainHueSpeed", L("Hue Animation Speed", "Hue Animation Speed"));
            EditorGUI.indentLevel--;
        }

        private void DrawBubbleSection()
        {
            if (!DrawFoldout(ref showBubble, L("バブルエフェクト", "Bubble Effect"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_BubbleSize", L("Bubble Size", "Bubble Size"));
            DrawProperty("_BubbleBrightness", L("Bubble Brightness", "Bubble Brightness"));
            DrawProperty("_BubbleWobbleSpeed", L("Wobble Speed", "Wobble Speed"));
            DrawProperty("_BubbleWobbleStrength", L("Wobble Strength", "Wobble Strength"));
            EditorGUILayout.HelpBox(L("虹彩のハイライトにきらめきを足すエフェクトです。", "Iris highlight particle effect. Adds sparkling shine."), MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawIrisCausticsSection()
        {
            if (!DrawFoldout(ref showIrisCaustics, L("虹彩コースティクス", "Iris Caustics"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_UseIrisCaustics", L("Enable Caustics", "Enable Caustics"));
            DrawProperty("_IrisCausticsColor", L("Caustics Color (HDR)", "Caustics Color (HDR)"));
            DrawProperty("_IrisCausticsIntensity", L("Intensity", "Intensity"));
            DrawProperty("_IrisCausticsScale", L("Scale", "Scale"));
            DrawProperty("_IrisCausticsSpeed", L("Speed", "Speed"));
            DrawProperty("_IrisCausticsParallax", L("Parallax", "Parallax"));
            DrawProperty("_IrisCausticsTwist", L("Twist", "Twist"));
            DrawProperty("_IrisCausticsBlendMode", L("Blend Mode", "Blend Mode"));
            EditorGUI.indentLevel--;
        }

        private void DrawIrisRingPulseSection()
        {
            if (!DrawFoldout(ref showIrisRingPulse, L("虹彩リングパルス", "Iris Ring Pulse"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_UseIrisRingPulse", L("Enable Ring Pulse", "Enable Ring Pulse"));
            DrawProperty("_IrisRingColor", L("Ring Color (HDR)", "Ring Color (HDR)"));
            DrawProperty("_IrisRingRadius", L("Ring Radius", "Ring Radius"));
            DrawProperty("_IrisRingWidth", L("Ring Width", "Ring Width"));
            DrawProperty("_IrisRingPulseSpeed", L("Pulse Speed", "Pulse Speed"));
            DrawProperty("_IrisRingPulseAmount", L("Pulse Amount", "Pulse Amount"));
            DrawProperty("_IrisRingParallax", L("Parallax", "Parallax"));
            DrawProperty("_IrisRingIntensity", L("Intensity", "Intensity"));
            DrawProperty("_IrisRingBlendMode", L("Blend Mode", "Blend Mode"));
            EditorGUI.indentLevel--;
        }

        private void DrawTexturePolishSection()
        {
            if (!DrawFoldout(ref showTexturePolish, L("テクスチャ磨き", "Texture Polish"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_UseTexturePolish", L("Enable Texture Polish", "Enable Texture Polish"));
            DrawProperty("_TexturePolishBlendMode", L("Blend Mode", "Blend Mode"));
            DrawProperty("_TexturePolishStrength", L("Strength", "Strength"));
            DrawProperty("_TexturePolishContrast", L("Contrast", "Contrast"));
            DrawProperty("_TexturePolishSaturation", L("Saturation", "Saturation"));
            DrawProperty("_TexturePolishIrisFocus", L("Iris Focus", "Iris Focus"));
            DrawProperty("_TexturePolishTint", L("Tint Color (HDR)", "Tint Color (HDR)"));
            EditorGUI.indentLevel--;
        }

        private void DrawExpressionSection()
        {
            if (!DrawFoldout(ref showExpression, L("表情オーバーレイ", "Expression Overlay"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_ExpressionPreset", L("Expression Preset", "Expression Preset"));
            DrawProperty("_ExpressionMode", L("Expression Mode (Off/Spiral/Tearful/Shock)", "Expression Mode (Off/Spiral/Tearful/Shock)"));
            DrawProperty("_ExpressionColor", L("Expression Color (HDR)", "Expression Color (HDR)"));
            DrawProperty("_ExpressionIntensity", L("Intensity", "Intensity"));
            DrawProperty("_ExpressionScale", L("Scale", "Scale"));
            DrawProperty("_ExpressionSpeed", L("Speed", "Speed"));
            DrawProperty("_ExpressionParallax", L("Parallax", "Parallax"));
            DrawProperty("_ExpressionDetail", L("Detail", "Detail"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField(L("スパイラル設定", "Spiral Settings"), EditorStyles.miniBoldLabel);
            DrawProperty("_SpiralTightness", L("Spiral Tightness", "Spiral Tightness"));
            DrawProperty("_SpiralLineWidth", L("Spiral Line Width", "Spiral Line Width"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField(L("涙設定", "Tear Settings"), EditorStyles.miniBoldLabel);
            DrawProperty("_TearFlow", L("Tear Flow", "Tear Flow"));
            DrawProperty("_TearRim", L("Tear Rim Highlight", "Tear Rim Highlight"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField(L("ショックリング設定", "Shock Ring Settings"), EditorStyles.miniBoldLabel);
            DrawProperty("_ShockRingCount", L("Ring Count", "Ring Count"));
            DrawProperty("_ShockRingWidth", L("Ring Width", "Ring Width"));
            DrawProperty("_ExpressionBlendMode", L("Blend Mode", "Blend Mode"));
            EditorGUI.indentLevel--;
        }

        private void DrawInnerMeshPrioritySection()
        {
            if (!DrawFoldout(ref showInnerMeshPriority, L("内側メッシュ優先度 (BlendShape)", "Inner Mesh Priority (BlendShape)"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_UseInnerMeshPriority", L("Enable Inner Mesh Priority", "Enable Inner Mesh Priority"));
            MaterialProperty maskProp = FindProperty("_InnerMeshPriorityMask", properties, false);
            if (maskProp != null) materialEditor.TextureProperty(maskProp, L("Priority Mask", "Priority Mask"));
            DrawProperty("_InnerMeshPriorityChannel", L("Mask Channel", "Mask Channel"));
            DrawProperty("_InnerMeshPriorityThreshold", L("Threshold", "Threshold"));
            DrawProperty("_InnerMeshPrioritySoftness", L("Softness", "Softness"));
            DrawProperty("_InnerMeshPriorityInvert", L("Invert", "Invert"));
            DrawProperty("_InnerMeshPriorityMode", L("Priority Mode", "Priority Mode"));
            DrawProperty("_InnerMeshPriorityStrength", L("Strength", "Strength"));
            EditorGUILayout.HelpBox(L("BlendShape 使用時に、内側メッシュをどちら優先で描くかを調整します。", "Controls inner mesh draw priority when using BlendShapes."), MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawPerformanceSection()
        {
            if (!DrawFoldout(ref showPerformance, L("パフォーマンス", "Performance"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_PerformanceTier", L("Performance Tier (Quality/Balanced/Lite)", "Performance Tier (Quality/Balanced/Lite)"));
            EditorGUILayout.HelpBox(L("Lite は一部エフェクトを無効化して軽量化します。Quest 向けでは特におすすめです。", "Lite: Disables some effects for optimization. Recommended for Quest."), MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawVignetteSection()
        {
            if (!DrawFoldout(ref showVignette, L("ビネット効果", "Vignette Effect"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_VignetteTransparency", L("Enable Transparency", "Enable Transparency"));
            DrawProperty("_VignetteDitherScale", L("Dither Scale", "Dither Scale"));
            DrawProperty("_VignetteThickness", L("Thickness", "Thickness"));
            DrawProperty("_VignetteFalloff", L("Falloff", "Falloff"));
            EditorGUI.indentLevel--;
        }

        private void DrawAudioLinkSection()
        {
            if (!DrawFoldout(ref showAudioLink, L("Audio Link", "Audio Link"))) return;
            EditorGUI.indentLevel++;
            DrawProperty("_BandSelection", L("Frequency Band", "Frequency Band"));
            DrawProperty("_Intensity", L("Intensity", "Intensity"));
            DrawProperty("_MinValue", L("Minimum Brightness", "Minimum Brightness"));
            EditorGUILayout.HelpBox(L("VRChat ワールドに AudioLink が入っている場合、音に合わせて変化します。", "Syncs with music when AudioLink is installed in the VRChat world."), MessageType.None);
            EditorGUI.indentLevel--;
        }

        private void DrawRenderingSection()
        {
            if (!DrawFoldout(ref showRendering, L("描画設定", "Rendering Settings"))) return;
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField(L("ステンシル", "Stencil"), EditorStyles.miniBoldLabel);
            DrawProperty("_StencilRef", L("Reference Value", "Reference Value"));
            DrawProperty("_StencilComp", L("Comparison Function", "Comparison Function"));
            DrawProperty("_StencilPass", L("Pass Operation", "Pass Operation"));
            DrawProperty("_StencilFail", L("Fail Operation", "Fail Operation"));
            DrawProperty("_StencilZFail", L("Z Fail Operation", "Z Fail Operation"));
            DrawProperty("_StencilReadMask", L("Read Mask", "Read Mask"));
            DrawProperty("_StencilWriteMask", L("Write Mask", "Write Mask"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("ブレンドと透明", "Blend & Transparency"), EditorStyles.miniBoldLabel);
            DrawProperty("_SrcBlend", L("Source Blend", "Source Blend"));
            DrawProperty("_DstBlend", L("Destination Blend", "Destination Blend"));
            DrawProperty("_BlendOp", L("Blend Operation", "Blend Operation"));
            DrawProperty("_TransparencyResolveMode", L("Transparency Resolve Mode", "Transparency Resolve Mode"));
            DrawProperty("_TransparencyDitherScale", L("Transparency Dither Scale", "Transparency Dither Scale"));
            DrawProperty("_GlobalOpacity", L("Global Opacity", "Global Opacity"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("深度とカリング", "Depth & Culling"), EditorStyles.miniBoldLabel);
            DrawProperty("_ZTest", L("Z Test", "Z Test"));
            DrawProperty("_ZWrite", L("Z Write", "Z Write"));
            DrawProperty("_OffsetFactor", L("Offset Factor", "Offset Factor"));
            DrawProperty("_OffsetUnits", L("Offset Units", "Offset Units"));
            DrawProperty("_AlphaClipThreshold", L("Alpha Clip Threshold", "Alpha Clip Threshold"));
            DrawProperty("_Cull", L("Culling Mode", "Culling Mode"));
            DrawProperty("_ColorMask", L("Color Mask", "Color Mask"));
            EditorGUI.indentLevel--;
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("Natane Eye Shader v1.1", "Natane Eye Shader v1.1"), FooterStyle);
            EditorGUILayout.LabelField(L("5つの目状態 + 表情オーバーレイ対応", "5 Eye States + Expression Overlay Support"), FooterStyle);
            EditorGUILayout.EndVertical();
        }
    }
}
