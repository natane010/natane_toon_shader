using UnityEngine;
using UnityEditor;
using System;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    /// <summary>
    /// Wirelight シェーダー用のDrawer
    /// NataneToonShaderGUIから委譲される
    /// NataneToonWirelightGUI.cs から移植
    /// </summary>
    public class NataneToonWirelightDrawer : NataneToonShaderGUITab
    {
        // Foldout states
        private bool showCageSettings = true;
        private bool showTriangleSettings = true;
        private bool showDitherSettings = true;
        private bool showNoiseSettings = true;
        private bool showColorSettings = true;
        private bool showCyberSettings = true;
        private bool showTextureSettings = true;
        private bool showAudioLinkSettings = false;
        private bool showVRCOptimizationSettings = true;
        private bool showRenderingSettings = false;
        private bool showStencilSettings = false;
        private bool showQuickTips = true;

        private string prefsPrefix;

        // Styles
        private static GUIStyle _boldFoldout;
        private static GUIStyle _headerBox;
        private static GUIStyle _presetButton;
        private static GUIStyle _tipBox;
        private static GUIStyle _labelWithValue;

        private static GUIStyle BoldFoldout
        {
            get
            {
                if (_boldFoldout == null)
                {
                    _boldFoldout = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = 12
                    };
                }
                return _boldFoldout;
            }
        }

        private static GUIStyle HeaderBox
        {
            get
            {
                if (_headerBox == null)
                {
                    _headerBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(10, 10, 5, 5)
                    };
                }
                return _headerBox;
            }
        }

        private static GUIStyle PresetButton
        {
            get
            {
                if (_presetButton == null)
                {
                    _presetButton = new GUIStyle(GUI.skin.button)
                    {
                        fontStyle = FontStyle.Bold,
                        fixedHeight = 25
                    };
                }
                return _presetButton;
            }
        }

        private static GUIStyle TipBox
        {
            get
            {
                if (_tipBox == null)
                {
                    _tipBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(10, 10, 5, 5),
                        fontSize = 11,
                        wordWrap = true
                    };
                }
                return _tipBox;
            }
        }

        private static GUIStyle LabelWithValue
        {
            get
            {
                if (_labelWithValue == null)
                {
                    _labelWithValue = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        normal = { textColor = new Color(0.5f, 0.8f, 1f) }
                    };
                }
                return _labelWithValue;
            }
        }

        public override void Initialize(MaterialEditor materialEditor, MaterialProperty[] properties, Material targetMaterial)
        {
            base.Initialize(materialEditor, properties, targetMaterial);
            prefsPrefix = NataneToonMaterialPresetEditor.GetMaterialPrefsKey(targetMaterial, "WL_");
            LoadFoldoutStates();
        }

        public override void LoadFoldoutStates()
        {
            if (targetMaterial == null) return;
            showCageSettings = EditorPrefs.GetBool(prefsPrefix + "Cage", true);
            showTriangleSettings = EditorPrefs.GetBool(prefsPrefix + "Triangle", true);
            showDitherSettings = EditorPrefs.GetBool(prefsPrefix + "Dither", true);
            showNoiseSettings = EditorPrefs.GetBool(prefsPrefix + "Noise", true);
            showColorSettings = EditorPrefs.GetBool(prefsPrefix + "Color", true);
            showCyberSettings = EditorPrefs.GetBool(prefsPrefix + "Cyber", true);
            showTextureSettings = EditorPrefs.GetBool(prefsPrefix + "Texture", true);
            showAudioLinkSettings = EditorPrefs.GetBool(prefsPrefix + "AudioLink", false);
            showVRCOptimizationSettings = EditorPrefs.GetBool(prefsPrefix + "VRC", true);
            showRenderingSettings = EditorPrefs.GetBool(prefsPrefix + "Rendering", false);
            showStencilSettings = EditorPrefs.GetBool(prefsPrefix + "Stencil", false);
            showQuickTips = EditorPrefs.GetBool(prefsPrefix + "Tips", true);
        }

        public override void SaveFoldoutStates()
        {
            if (targetMaterial == null) return;
            EditorPrefs.SetBool(prefsPrefix + "Cage", showCageSettings);
            EditorPrefs.SetBool(prefsPrefix + "Triangle", showTriangleSettings);
            EditorPrefs.SetBool(prefsPrefix + "Dither", showDitherSettings);
            EditorPrefs.SetBool(prefsPrefix + "Noise", showNoiseSettings);
            EditorPrefs.SetBool(prefsPrefix + "Color", showColorSettings);
            EditorPrefs.SetBool(prefsPrefix + "Cyber", showCyberSettings);
            EditorPrefs.SetBool(prefsPrefix + "Texture", showTextureSettings);
            EditorPrefs.SetBool(prefsPrefix + "AudioLink", showAudioLinkSettings);
            EditorPrefs.SetBool(prefsPrefix + "VRC", showVRCOptimizationSettings);
            EditorPrefs.SetBool(prefsPrefix + "Rendering", showRenderingSettings);
            EditorPrefs.SetBool(prefsPrefix + "Stencil", showStencilSettings);
            EditorPrefs.SetBool(prefsPrefix + "Tips", showQuickTips);
        }

        public override void Draw()
        {
            DrawWirelightHeader();
            EditorGUILayout.Space(5);

            // Main toggle
            MaterialProperty wirelightProp = FindProperty("_Wirelight", properties, false);
            if (wirelightProp == null) return;
            materialEditor.ShaderProperty(wirelightProp, new GUIContent(L("Wirelightを有効化", "Enable Wirelight"), L("ワイヤーフレームエフェクトを有効化", "Enable wireframe effect")));

            if (wirelightProp.floatValue > 0.5f)
            {
                EditorGUILayout.Space(10);

                DrawFoldoutSection(L("クイックヒント", "Quick Tips"), ref showQuickTips, DrawQuickTips);
                EditorGUILayout.Space(5);
                DrawPresetButtons();
                EditorGUILayout.Space(10);

                DrawFoldoutSection(L("ケージ & 頂点丸め込み", "Cage & Vertex Rounding"), ref showCageSettings, DrawCageSettings);
                DrawFoldoutSection(L("三角形の可視化", "Triangle Visualization"), ref showTriangleSettings, DrawTriangleSettings);
                DrawFoldoutSection(L("ディザリングパターン", "Dithering Pattern"), ref showDitherSettings, DrawDitherSettings);
                DrawFoldoutSection(L("ノイズアニメーション", "Noise Animation"), ref showNoiseSettings, DrawNoiseSettings);
                DrawFoldoutSection(L("デュアルカラーシステム", "Dual Color System"), ref showColorSettings, DrawColorSettings);
                DrawFoldoutSection(L("サイバーワイヤー拡張", "Cyber Wire Extension"), ref showCyberSettings, DrawCyberSettings);
                DrawFoldoutSection(L("テクスチャ", "Texture"), ref showTextureSettings, DrawTextureSettings);
                DrawFoldoutSection("AudioLink (VRChat)", ref showAudioLinkSettings, DrawAudioLinkSettings);
                DrawFoldoutSection(L("VRC最適化", "VRC Optimization"), ref showVRCOptimizationSettings, DrawVRCOptimizationSettings);
                DrawFoldoutSection(L("レンダリング設定", "Rendering Settings"), ref showRenderingSettings, DrawRenderingSettings);
                DrawFoldoutSection(L("ステンシル設定", "Stencil Settings"), ref showStencilSettings, DrawStencilSettings);
            }
            else
            {
                EditorGUILayout.HelpBox(L("Wirelightを有効化すると設定が表示されます。", "Enable Wirelight to show settings."), MessageType.Info);
            }

            EditorGUILayout.Space(10);
            DrawWirelightFooter();
        }

        // ===== Drawing Helpers =====

        private void DrawWirelightHeader()
        {
            NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate budget = NataneToonSamplerBudgetEstimator.Estimate(targetMaterial);
            NataneToonInspectorComponents.DrawInspectorHeader(
                "Natane Toon Shader",
                L("ワイヤーフレームとサイバー表現", "Wireframe & Cyber Look"),
                "Wirelight",
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

        private void DrawFoldoutSection(string title, ref bool foldout, Action content)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(HeaderBox);

            EditorGUI.BeginChangeCheck();
            foldout = EditorGUILayout.Foldout(foldout, title, true, BoldFoldout);
            if (EditorGUI.EndChangeCheck()) SaveFoldoutStates();

            if (foldout)
            {
                EditorGUILayout.Space(5);
                EditorGUI.indentLevel++;
                content?.Invoke();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSliderWithValue(MaterialProperty prop, string label, string unit = "")
        {
            if (prop == null) return;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            materialEditor.ShaderProperty(prop, label);
            EditorGUILayout.EndVertical();
            GUILayout.Label($"{prop.floatValue:F3}{unit}", LabelWithValue, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVectorWithValue(MaterialProperty prop, string label)
        {
            if (prop == null) return;
            EditorGUILayout.BeginHorizontal();
            materialEditor.ShaderProperty(prop, label);
            Vector4 val = prop.vectorValue;
            string valText = $"({val.x:F2}, {val.y:F2}, {val.z:F2}, {val.w:F2})";
            GUILayout.Label(valText, LabelWithValue, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
        }

        // ===== Section Implementations =====

        private void DrawQuickTips()
        {
            EditorGUILayout.BeginVertical(TipBox);
            EditorGUILayout.LabelField(L("初めて使う方へ", "Getting Started"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L("  まずはプリセットボタンを試してみましょう", "  Try the preset buttons first"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(L("  各セクションの設定値で微調整できます", "  Fine-tune with each section's settings"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("パフォーマンスヒント", "Performance Tips"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L("  Cage値: 低い = 重い, 高い = 軽い (30-150推奨)", "  Cage: low = heavy, high = light (30-150 recommended)"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(L("  Noise Scale: 低い = 重い, 高い = 軽い (5-20推奨)", "  Noise Scale: low = heavy, high = light (5-20 recommended)"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(L("  VRC最適化: Performance ModeをLiteにすると軽量化", "  VRC Optimization: Set Performance Mode to Lite for lighter load"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("よくある設定", "Common Settings"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L("  エッジを太く: Triangle Edge Width を増やす", "  Thicker edges: Increase Triangle Edge Width"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(L("  動きを速く: Movement の W (速度) を増やす", "  Faster motion: Increase Movement W (speed)"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(L("  発光を強く: HDRカラーの Intensity を 2-5 に", "  Stronger glow: Set HDR Color Intensity to 2-5"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetButtons()
        {
            EditorGUILayout.LabelField(L("クイックプリセット", "Quick Presets"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("サイバーパンクネオン", "Cyberpunk Neon"), PresetButton)) ApplyCyberpunkPreset();
            if (GUILayout.Button(L("ローポリグリッド", "Low Poly Grid"), PresetButton)) ApplyLowPolyPreset();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("オーガニックフロー", "Organic Flow"), PresetButton)) ApplyOrganicPreset();
            if (GUILayout.Button(L("エネルギーシールド", "Energy Shield"), PresetButton)) ApplyEnergyShieldPreset();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("AudioLinkクラブ", "AudioLink Club"), PresetButton)) ApplyAudioLinkPreset();
            if (GUILayout.Button(L("ホログラムグリッド", "Hologram Grid"), PresetButton)) ApplyHologramPreset();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("サイバーワイヤー(AL)", "Cyber Wire (AL)"), PresetButton)) ApplyCyberWirePreset();
            if (GUILayout.Button(L("データストーム", "Data Storm"), PresetButton)) ApplyDataStormPreset();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L("VRC軽量(PC)", "VRC Lite (PC)"), PresetButton)) ApplyVRCLitePreset();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCageSettings()
        {
            EditorGUILayout.LabelField(L("ケージ 1", "Cage 1"), EditorStyles.miniBoldLabel);
            DrawSliderWithValue(FindProperty("_Cage", properties, false), L("丸め係数", "Rounding Factor"));
            DrawSliderWithValue(FindProperty("_Extrude", properties, false), L("押し出し量", "Extrude Amount"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("ケージ 2 (ノイズブレンド)", "Cage 2 (Noise Blend)"), EditorStyles.miniBoldLabel);
            DrawSliderWithValue(FindProperty("_Cage2", properties, false), L("丸め係数 2", "Rounding Factor 2"));
            DrawSliderWithValue(FindProperty("_Extrude2", properties, false), L("押し出し量 2", "Extrude Amount 2"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("ブレンド設定", "Blend Settings"), EditorStyles.miniBoldLabel);
            DrawSliderWithValue(FindProperty("_RoundingFudge", properties, false), L("丸めブレンドオフセット", "Rounding Blend Offset"));
            DrawSliderWithValue(FindProperty("_RoundingPow", properties, false), L("丸めブレンド累乗", "Rounding Blend Power"));
            DrawSliderWithValue(FindProperty("_ExtrudeFudge", properties, false), L("押し出しブレンドオフセット", "Extrude Blend Offset"));
            DrawSliderWithValue(FindProperty("_ExtrudePow", properties, false), L("押し出しブレンド累乗", "Extrude Blend Power"));

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(L("ヒント: 丸め係数を下げる = 粗いグリッド (ローポリ風)", "Tip: Lower rounding = coarser grid (low-poly style)"), MessageType.None);
        }

        private void DrawTriangleSettings()
        {
            MaterialProperty shapeProp = FindProperty("_TriangleShape", properties, false);
            if (shapeProp != null)
            {
                DrawSliderWithValue(shapeProp, L("丸み (0=シャープ, 1=ラウンド)", "Roundness (0=Sharp, 1=Round)"));
                string shapeIcon = shapeProp.floatValue < 0.3f ? L("辺モード", "Edge Mode") :
                                   shapeProp.floatValue > 0.7f ? L("頂点モード", "Vertex Mode") : L("混合モード", "Mixed Mode");
                EditorGUILayout.LabelField(shapeIcon, EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(3);
            DrawSliderWithValue(FindProperty("_TriangleNumerator", properties, false), L("エッジ幅", "Edge Width"));
            DrawSliderWithValue(FindProperty("_TrianglePower", properties, false), L("エッジ鋭さ", "Edge Sharpness"));
            DrawSliderWithValue(FindProperty("_TriangleRoundnessBreakout", properties, false), L("追加の丸み", "Extra Roundness"));

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(L("ヒント: 丸み 0 = 辺を強調 | 丸み 1 = 頂点を強調", "Tip: Roundness 0 = emphasize edges | Roundness 1 = emphasize vertices"), MessageType.None);
        }

        private void DrawDitherSettings()
        {
            DrawSliderWithValue(FindProperty("_DitherMultiple", properties, false), L("面のディザー密度", "Face Dither Density"));
            DrawSliderWithValue(FindProperty("_LineMultiple", properties, false), L("辺のディザー密度", "Edge Dither Density"));
            DrawSliderWithValue(FindProperty("_DitherFudge", properties, false), L("ディザー閾値", "Dither Threshold"));

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(L("ヒント: 密度を上げる = ドットが細かく", "Tip: Higher density = finer dots"), MessageType.None);
        }

        private void DrawNoiseSettings()
        {
            DrawVectorWithValue(FindProperty("_NoiseSettings", properties, false), L("スケール & 回転 (XYZW)", "Scale & Rotation (XYZW)"));
            EditorGUILayout.LabelField(L("X: スケール | Y,Z,W: 回転", "X: Scale | Y,Z,W: Rotation"), EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(3);
            MaterialProperty moveProp = FindProperty("_NoiseMovementDirection", properties, false);
            if (moveProp != null)
            {
                DrawVectorWithValue(moveProp, L("移動 (XYZ=方向, W=速度)", "Movement (XYZ=Direction, W=Speed)"));
                Vector4 move = moveProp.vectorValue;
                float speed = move.w;
                string moveStatus = speed < 0.01f ? L("静止", "Still") :
                                    speed < 0.5f ? L("ゆっくり", "Slow") :
                                    speed < 1.5f ? L("普通", "Normal") : L("速い", "Fast");
                EditorGUILayout.LabelField(moveStatus, EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.Space(3);
            DrawVectorWithValue(FindProperty("_NoiseSmoothstep", properties, false), L("ノイズ範囲 (最小, 最大)", "Noise Range (Min, Max)"));
            DrawSliderWithValue(FindProperty("_NoiseSubtract", properties, false), L("ノイズオフセット", "Noise Offset"));
            DrawSliderWithValue(FindProperty("_NoiseMultiple", properties, false), L("ノイズ強度", "Noise Intensity"));

            EditorGUILayout.Space(3);
            MaterialProperty spaceSelectorProp = FindProperty("_SpaceSelector", properties, false);
            if (spaceSelectorProp != null)
            {
                materialEditor.ShaderProperty(spaceSelectorProp, L("頂点カラー位置を使用", "Use Vertex Color Position"));

                if (spaceSelectorProp.floatValue > 0.5f)
                {
                    EditorGUILayout.HelpBox(L("頂点カラーが位置として使用されます。安定したアニメーションのため、頂点位置を頂点カラーにベイクしてください。", "Vertex colors are used as position. Bake vertex positions to vertex colors for stable animation."), MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(L("ヒント: アニメーションでパターンが崩れる場合は「頂点カラー位置」を有効化", "Tip: Enable 'Vertex Color Position' if patterns break during animation"), MessageType.None);
                }
            }
        }

        private void DrawColorSettings()
        {
            EditorGUILayout.LabelField(L("カラー 1", "Color 1"), EditorStyles.miniBoldLabel);
            MaterialProperty col1 = FindProperty("_Col", properties, false);
            if (col1 != null)
            {
                materialEditor.ShaderProperty(col1, L("HDRカラー 1", "HDR Color 1"));
                Color c1 = col1.colorValue;
                float intensity1 = Mathf.Max(c1.r, c1.g, c1.b);
                string intensityIcon1 = intensity1 > 2f ? L("強い発光", "Strong Glow") :
                                        intensity1 > 1f ? L("発光", "Glow") : L("通常", "Normal");
                EditorGUILayout.LabelField(intensityIcon1, EditorStyles.centeredGreyMiniLabel);
            }

            DrawSliderWithValue(FindProperty("_ColHueShift", properties, false), L("色相シフト", "Hue Shift"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("カラー 2", "Color 2"), EditorStyles.miniBoldLabel);
            MaterialProperty col2 = FindProperty("_Col2", properties, false);
            if (col2 != null)
            {
                materialEditor.ShaderProperty(col2, L("HDRカラー 2", "HDR Color 2"));
                Color c2 = col2.colorValue;
                float intensity2 = Mathf.Max(c2.r, c2.g, c2.b);
                string intensityIcon2 = intensity2 > 2f ? L("強い発光", "Strong Glow") :
                                        intensity2 > 1f ? L("発光", "Glow") : L("通常", "Normal");
                EditorGUILayout.LabelField(intensityIcon2, EditorStyles.centeredGreyMiniLabel);
            }

            DrawSliderWithValue(FindProperty("_Col2HueShift", properties, false), L("色相シフト", "Hue Shift"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("アニメーション & ブレンド", "Animation & Blend"), EditorStyles.miniBoldLabel);
            DrawVectorWithValue(FindProperty("_AutoHueShift", properties, false), L("自動色相シフト (C1, C2)", "Auto Hue Shift (C1, C2)"));
            DrawSliderWithValue(FindProperty("_ColorRange", properties, false), L("遷移幅", "Transition Width"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("ポストプロセス", "Post Process"), EditorStyles.miniBoldLabel);
            DrawSliderWithValue(FindProperty("_ColorBrightness", properties, false), L("明るさ", "Brightness"));
            DrawSliderWithValue(FindProperty("_ColorPower", properties, false), L("ガンマ累乗", "Gamma Power"));
            DrawSliderWithValue(FindProperty("_MultAlphaAndColor", properties, false), L("アルファを色に乗算", "Multiply Alpha to Color"));

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(L("ヒント: 発光エフェクトはIntensity 2-5 を設定!", "Tip: Set Intensity to 2-5 for glow effects!"), MessageType.None);
        }

        private void DrawCyberSettings()
        {
            MaterialProperty styleModeProp = FindProperty("_WireStyleMode", properties, false);
            if (styleModeProp == null) return;
            materialEditor.ShaderProperty(styleModeProp, L("ワイヤースタイル", "Wire Style"));

            if (styleModeProp.floatValue < 0.5f)
            {
                EditorGUILayout.HelpBox(L("Defaultモードでは従来のWirelight描画です。Cyber Wireに切り替えると追加演出が有効になります。", "Default mode uses standard Wirelight rendering. Switch to Cyber Wire for additional effects."), MessageType.None);
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Cyber Core", EditorStyles.miniBoldLabel);
            DrawProperty("_CyberNeonColor", L("ネオンカラー", "Neon Color"));
            DrawSliderWithValue(FindProperty("_CyberEdgeBoost", properties, false), L("エッジブースト", "Edge Boost"));
            DrawSliderWithValue(FindProperty("_CyberPulseSpeed", properties, false), L("パルス速度", "Pulse Speed"));
            DrawSliderWithValue(FindProperty("_CyberPulseIntensity", properties, false), L("パルス強度", "Pulse Intensity"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("おすすめ特殊表現 (On/Off)", "Recommended Effects (On/Off)"), EditorStyles.miniBoldLabel);

            // Scanline
            MaterialProperty scanlineToggle = FindProperty("_CyberScanline", properties, false);
            if (scanlineToggle != null)
            {
                materialEditor.ShaderProperty(scanlineToggle, L("スキャンライン", "Scanline"));
                if (scanlineToggle.floatValue > 0.5f)
                {
                    DrawSliderWithValue(FindProperty("_CyberScanlineDensity", properties, false), L("密度", "Density"));
                    DrawSliderWithValue(FindProperty("_CyberScanlineSpeed", properties, false), L("速度", "Speed"));
                    DrawSliderWithValue(FindProperty("_CyberScanlineStrength", properties, false), L("強度", "Strength"));
                }
            }

            // Chroma
            MaterialProperty chromaToggle = FindProperty("_CyberChromaShift", properties, false);
            if (chromaToggle != null)
            {
                materialEditor.ShaderProperty(chromaToggle, L("クロマシフト", "Chroma Shift"));
                if (chromaToggle.floatValue > 0.5f)
                {
                    DrawSliderWithValue(FindProperty("_CyberChromaAmount", properties, false), L("量", "Amount"));
                }
            }

            // Glitch
            MaterialProperty glitchToggle = FindProperty("_CyberGlitch", properties, false);
            if (glitchToggle != null)
            {
                materialEditor.ShaderProperty(glitchToggle, L("グリッチフリッカー", "Glitch Flicker"));
                if (glitchToggle.floatValue > 0.5f)
                {
                    DrawSliderWithValue(FindProperty("_CyberGlitchStrength", properties, false), L("強度", "Strength"));
                    DrawSliderWithValue(FindProperty("_CyberGlitchSpeed", properties, false), L("速度", "Speed"));
                }
            }

            // Data Stream
            MaterialProperty dataToggle = FindProperty("_CyberDataStream", properties, false);
            if (dataToggle != null)
            {
                materialEditor.ShaderProperty(dataToggle, L("データストリーム", "Data Stream"));
                if (dataToggle.floatValue > 0.5f)
                {
                    DrawSliderWithValue(FindProperty("_CyberDataDensity", properties, false), L("密度", "Density"));
                    DrawSliderWithValue(FindProperty("_CyberDataSpeed", properties, false), L("速度", "Speed"));
                    DrawSliderWithValue(FindProperty("_CyberDataStrength", properties, false), L("強度", "Strength"));
                    DrawSliderWithValue(FindProperty("_CyberDataJitter", properties, false), L("ジッター", "Jitter"));
                }
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(L("推奨: Scanline + Chromaを弱めにON。Data Streamは情報走査風、Glitchは必要時だけON。", "Recommended: Turn on Scanline + Chroma at low intensity. Data Stream for data-scan look, Glitch only when needed."), MessageType.None);
        }

        private void DrawTextureSettings()
        {
            MaterialProperty colorTexProp = FindProperty("_ColorTexture", properties, false);
            if (colorTexProp != null) materialEditor.TextureProperty(colorTexProp, L("カラーテクスチャ", "Color Texture"));
            MaterialProperty maskTexProp = FindProperty("_MaskTexture", properties, false);
            if (maskTexProp != null) materialEditor.TextureProperty(maskTexProp, L("マスクテクスチャ", "Mask Texture"));
        }

        private void DrawAudioLinkSettings()
        {
            MaterialProperty audioLinkProp = FindProperty("_AudioLink", properties, false);
            if (audioLinkProp == null) return;
            materialEditor.ShaderProperty(audioLinkProp, L("AudioLinkを有効化", "Enable AudioLink"));

            if (audioLinkProp.floatValue > 0.5f)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("押し出し変調", "Extrude Modulation"), EditorStyles.miniBoldLabel);
                DrawProperty("_ExtrudeAudiolink", L("強度", "Intensity"));
                DrawProperty("_ExtrudeBand", L("周波数帯域", "Frequency Band"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("カラー変調", "Color Modulation"), EditorStyles.miniBoldLabel);
                DrawProperty("_ColorFudgeAudiolink", L("強度", "Intensity"));
                DrawProperty("_ColorFudgeBand", L("周波数帯域", "Frequency Band"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("Chronotensity (タイミング制御)", "Chronotensity (Timing Control)"), EditorStyles.miniBoldLabel);
                DrawProperty("_NoiseMovementChronoAudiolink", L("強度", "Intensity"));
                DrawProperty("_NoiseMovementChronoMode", L("モード", "Mode"));
                DrawProperty("_NoiseMovementChronoBand", L("周波数帯域", "Frequency Band"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField(L("テーマカラー", "Theme Color"), EditorStyles.miniBoldLabel);
                DrawProperty("_ColorOneTheme", L("カラー1テーマ", "Color 1 Theme"));
                DrawProperty("_ColorTwoTheme", L("カラー2テーマ", "Color 2 Theme"));
                DrawProperty("_InvertCol", L("テーマカラーを反転", "Invert Theme Color"));

                // Cyber sync (only if Cyber mode enabled)
                MaterialProperty wireStyleProp = FindProperty("_WireStyleMode", properties, false);
                if (wireStyleProp != null && wireStyleProp.floatValue > 0.5f)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField(L("Cyber連動", "Cyber Sync"), EditorStyles.miniBoldLabel);
                    DrawProperty("_CyberAudioPulse", L("パルス連動強度", "Pulse Sync Intensity"));
                    DrawProperty("_CyberAudioPulseBand", L("パルス帯域", "Pulse Band"));
                    DrawProperty("_CyberAudioGlitch", L("グリッチ連動強度", "Glitch Sync Intensity"));
                    DrawProperty("_CyberAudioGlitchBand", L("グリッチ帯域", "Glitch Band"));
                    DrawProperty("_CyberAudioData", L("データストリーム連動強度", "Data Stream Sync Intensity"));
                    DrawProperty("_CyberAudioDataBand", L("データストリーム帯域", "Data Stream Band"));
                }

                EditorGUILayout.HelpBox(L("これらの機能を使用するには、VRChatワールドにAudioLinkがセットアップされている必要があります。", "AudioLink must be set up in the VRChat world to use these features."), MessageType.Info);
            }
        }

        private void DrawVRCOptimizationSettings()
        {
            EditorGUILayout.LabelField(L("品質モード", "Quality Mode"), EditorStyles.miniBoldLabel);
            DrawProperty("_VRCPerfMode", "Performance Mode");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("遠距離フェード", "Distance Fade"), EditorStyles.miniBoldLabel);
            MaterialProperty distanceFadeToggle = FindProperty("_UseWLDistanceFade", properties, false);
            if (distanceFadeToggle != null)
            {
                materialEditor.ShaderProperty(distanceFadeToggle, L("Distance Fadeを有効化", "Enable Distance Fade"));
                if (distanceFadeToggle.floatValue > 0.5f)
                {
                    DrawSliderWithValue(FindProperty("_WLDistanceFadeStart", properties, false), L("開始距離", "Start Distance"));
                    DrawSliderWithValue(FindProperty("_WLDistanceFadeEnd", properties, false), L("終了距離", "End Distance"));
                    DrawSliderWithValue(FindProperty("_WLDistanceFadePower", properties, false), L("フェードカーブ", "Fade Curve"));
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("透明オーバードロー対策", "Transparent Overdraw Prevention"), EditorStyles.miniBoldLabel);
            MaterialProperty alphaClipToggle = FindProperty("_UseWLAlphaClip", properties, false);
            if (alphaClipToggle != null)
            {
                materialEditor.ShaderProperty(alphaClipToggle, L("Alpha Clipを有効化", "Enable Alpha Clip"));
                if (alphaClipToggle.floatValue > 0.5f)
                {
                    DrawSliderWithValue(FindProperty("_WLAlphaClipThreshold", properties, false), L("しきい値", "Threshold"));
                }
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(L("Lite + Distance Fade + Alpha Clipは、VRCワールドでの重なり負荷を抑える設定です。", "Lite + Distance Fade + Alpha Clip reduces overdraw load in VRC worlds."), MessageType.None);
        }

        private void DrawRenderingSettings()
        {
            EditorGUILayout.LabelField(L("ブレンディング", "Blending"), EditorStyles.miniBoldLabel);
            DrawProperty("_SrcBlendAlphaWL", L("ソースブレンド", "Source Blend"));
            DrawProperty("_DstBlendAlphaWL", L("デストブレンド", "Destination Blend"));

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(L("深度", "Depth"), EditorStyles.miniBoldLabel);
            DrawProperty("_ZWriteWL", L("Z書き込み", "Z Write"));
            DrawProperty("_ZTestWL", L("Zテスト", "Z Test"));

            EditorGUILayout.Space(5);
            DrawProperty("_RenderQueue", L("レンダーキューオーバーライド", "Render Queue Override"));

            EditorGUILayout.HelpBox(L("加算合成: Src=One, Dst=One\n標準アルファ: Src=SrcAlpha, Dst=OneMinusSrcAlpha", "Additive: Src=One, Dst=One\nStandard Alpha: Src=SrcAlpha, Dst=OneMinusSrcAlpha"), MessageType.Info);
        }

        private void DrawStencilSettings()
        {
            DrawProperty("_StencilRef", L("参照値", "Reference Value"));
            DrawProperty("_StencilCompareFunctionWL", L("比較関数", "Compare Function"));
            DrawProperty("_StencilPassOpWL", L("パス操作", "Pass Operation"));
            DrawProperty("_StencilFailOpWL", L("失敗操作", "Fail Operation"));
            DrawProperty("_StencilZFailOpWL", L("Z失敗操作", "Z Fail Operation"));
        }

        private void DrawWirelightFooter()
        {
            EditorGUILayout.BeginVertical(HeaderBox);
            GUIStyle footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            EditorGUILayout.LabelField(L("ナタネトゥーン ワイヤーライトシェーダー v1.1", "Natane Toon Wirelight Shader v1.1"), footerStyle);
            EditorGUILayout.LabelField(L("PC版VRChatのみ対応 (Quest非対応)", "PC VRChat only (Quest not supported)"), footerStyle);
            EditorGUILayout.EndVertical();
        }

        // ===== Preset Methods =====

        private void SetToggle(string propertyName, string keyword, bool enabled)
        {
            targetMaterial.SetFloat(propertyName, enabled ? 1f : 0f);
            if (enabled)
                targetMaterial.EnableKeyword(keyword);
            else
                targetMaterial.DisableKeyword(keyword);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyCyberDefaults(bool enabled)
        {
            targetMaterial.SetInt("_WireStyleMode", enabled ? 1 : 0);
            SetToggle("_CyberScanline", "_CYBER_SCANLINE", enabled);
            SetToggle("_CyberChromaShift", "_CYBER_CHROMA", enabled);
            SetToggle("_CyberGlitch", "_CYBER_GLITCH", false);
            SetToggle("_CyberDataStream", "_CYBER_DATASTREAM", false);

            if (!enabled)
            {
                targetMaterial.SetFloat("_CyberAudioPulse", 0f);
                targetMaterial.SetFloat("_CyberAudioGlitch", 0f);
                targetMaterial.SetFloat("_CyberAudioData", 0f);
            }
        }

        private void ApplyVRCDefaults(bool liteMode)
        {
            targetMaterial.SetFloat("_VRCPerfMode", liteMode ? 2f : 1f);
            SetToggle("_UseWLDistanceFade", "_WL_DISTANCE_FADE", liteMode);
            targetMaterial.SetFloat("_WLDistanceFadeStart", liteMode ? 8f : 10f);
            targetMaterial.SetFloat("_WLDistanceFadeEnd", liteMode ? 20f : 24f);
            targetMaterial.SetFloat("_WLDistanceFadePower", 1.2f);
            targetMaterial.SetFloat("_UseWLAlphaClip", liteMode ? 1f : 0f);
            targetMaterial.SetFloat("_WLAlphaClipThreshold", liteMode ? 0.12f : 0.08f);

            if (!liteMode)
                targetMaterial.DisableKeyword("_WL_DISTANCE_FADE");

            targetMaterial.SetFloat("_CyberDataDensity", 260f);
            targetMaterial.SetFloat("_CyberDataSpeed", 2.2f);
            targetMaterial.SetFloat("_CyberDataStrength", 0.45f);
            targetMaterial.SetFloat("_CyberDataJitter", 0.2f);
            targetMaterial.SetFloat("_CyberAudioData", 0f);
            targetMaterial.SetInt("_CyberAudioDataBand", 1);
        }

        private void ApplyCyberpunkPreset()
        {
            Undo.RecordObject(targetMaterial, "Apply Cyberpunk Preset");
            ApplyVRCDefaults(false);
            targetMaterial.SetInt("_WireStyleMode", 1);
            SetToggle("_CyberScanline", "_CYBER_SCANLINE", true);
            SetToggle("_CyberChromaShift", "_CYBER_CHROMA", true);
            SetToggle("_CyberGlitch", "_CYBER_GLITCH", false);
            targetMaterial.SetColor("_CyberNeonColor", new Color(0.1f, 0.95f, 1.0f, 1.0f) * 1.8f);
            targetMaterial.SetFloat("_CyberEdgeBoost", 1.8f);
            targetMaterial.SetFloat("_CyberPulseSpeed", 4.0f);
            targetMaterial.SetFloat("_CyberPulseIntensity", 0.6f);
            targetMaterial.SetFloat("_CyberScanlineDensity", 520f);
            targetMaterial.SetFloat("_CyberScanlineSpeed", 1.1f);
            targetMaterial.SetFloat("_CyberScanlineStrength", 0.22f);
            targetMaterial.SetFloat("_CyberChromaAmount", 0.18f);
            targetMaterial.SetFloat("_Cage", 100);
            targetMaterial.SetFloat("_Extrude", 0.05f);
            targetMaterial.SetFloat("_Cage2", 100);
            targetMaterial.SetFloat("_Extrude2", 0);
            targetMaterial.SetFloat("_TriangleShape", 0.3f);
            targetMaterial.SetFloat("_TriangleNumerator", 0.015f);
            targetMaterial.SetFloat("_TrianglePower", 2.5f);
            targetMaterial.SetFloat("_DitherMultiple", 15);
            targetMaterial.SetFloat("_LineMultiple", 20);
            targetMaterial.SetVector("_NoiseSettings", new Vector4(15, 1, 0.5f, 0.2f));
            targetMaterial.SetVector("_NoiseMovementDirection", new Vector4(0, 0.2f, 0, 0.5f));
            targetMaterial.SetFloat("_NoiseMultiple", 25);
            targetMaterial.SetColor("_Col", new Color(0, 0.5f, 1f, 1) * 2f);
            targetMaterial.SetColor("_Col2", new Color(1f, 0, 0.5f, 1) * 2f);
            targetMaterial.SetVector("_AutoHueShift", new Vector2(0.05f, -0.05f));
            targetMaterial.SetFloat("_ColorBrightness", 1.5f);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyLowPolyPreset()
        {
            Undo.RecordObject(targetMaterial, "Apply Low Poly Preset");
            ApplyCyberDefaults(false);
            ApplyVRCDefaults(true);
            targetMaterial.SetFloat("_Cage", 30);
            targetMaterial.SetFloat("_Extrude", 0.01f);
            targetMaterial.SetFloat("_Cage2", 30);
            targetMaterial.SetFloat("_Extrude2", 0);
            targetMaterial.SetFloat("_TriangleShape", 0);
            targetMaterial.SetFloat("_TriangleNumerator", 0.01f);
            targetMaterial.SetFloat("_TrianglePower", 3);
            targetMaterial.SetVector("_NoiseSettings", new Vector4(10, 1, 0, 0));
            targetMaterial.SetVector("_NoiseMovementDirection", new Vector4(0, 0, 0, 0));
            targetMaterial.SetFloat("_NoiseMultiple", 15);
            targetMaterial.SetColor("_Col", Color.white);
            targetMaterial.SetColor("_Col2", new Color(0.5f, 0.5f, 0.5f, 1));
            targetMaterial.SetVector("_AutoHueShift", Vector2.zero);
            targetMaterial.SetFloat("_ColorBrightness", 1.0f);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyOrganicPreset()
        {
            Undo.RecordObject(targetMaterial, "Apply Organic Preset");
            ApplyCyberDefaults(false);
            ApplyVRCDefaults(false);
            targetMaterial.SetFloat("_Cage", 80);
            targetMaterial.SetFloat("_Extrude", 0.03f);
            targetMaterial.SetFloat("_Cage2", 120);
            targetMaterial.SetFloat("_Extrude2", 0.01f);
            targetMaterial.SetFloat("_TriangleShape", 0.8f);
            targetMaterial.SetFloat("_TriangleNumerator", 0.02f);
            targetMaterial.SetFloat("_TrianglePower", 2);
            targetMaterial.SetVector("_NoiseSettings", new Vector4(5, 2, 1, 0.5f));
            targetMaterial.SetVector("_NoiseMovementDirection", new Vector4(0.1f, 0.2f, 0.1f, 0.5f));
            targetMaterial.SetFloat("_NoiseMultiple", 30);
            targetMaterial.SetColor("_Col", new Color(1f, 0.5f, 0, 1) * 1.5f);
            targetMaterial.SetColor("_Col2", new Color(0, 1f, 0.5f, 1) * 1.5f);
            targetMaterial.SetVector("_AutoHueShift", new Vector2(0.1f, -0.1f));
            targetMaterial.SetFloat("_ColorRange", 0.2f);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyEnergyShieldPreset()
        {
            Undo.RecordObject(targetMaterial, "Apply Energy Shield Preset");
            ApplyCyberDefaults(false);
            ApplyVRCDefaults(false);
            targetMaterial.SetFloat("_Cage", 150);
            targetMaterial.SetFloat("_Extrude", 0.01f);
            targetMaterial.SetFloat("_Cage2", 150);
            targetMaterial.SetFloat("_Extrude2", 0.005f);
            targetMaterial.SetFloat("_TriangleShape", 1.0f);
            targetMaterial.SetFloat("_TriangleNumerator", 0.008f);
            targetMaterial.SetFloat("_TrianglePower", 3.5f);
            targetMaterial.SetFloat("_DitherMultiple", 25);
            targetMaterial.SetFloat("_LineMultiple", 30);
            targetMaterial.SetVector("_NoiseSettings", new Vector4(8, 0.5f, 0.3f, 0.2f));
            targetMaterial.SetVector("_NoiseMovementDirection", new Vector4(0, 0.05f, 0, 0.3f));
            targetMaterial.SetFloat("_NoiseMultiple", 18);
            targetMaterial.SetFloat("_NoiseSubtract", 0.2f);
            targetMaterial.SetColor("_Col", new Color(0.3f, 0.7f, 1f, 1) * 2.5f);
            targetMaterial.SetColor("_Col2", new Color(0.5f, 0.9f, 1f, 1) * 1.8f);
            targetMaterial.SetVector("_AutoHueShift", new Vector2(0.02f, -0.02f));
            targetMaterial.SetFloat("_ColorRange", 0.15f);
            targetMaterial.SetFloat("_ColorBrightness", 1.3f);
            targetMaterial.SetInt("_SrcBlendAlphaWL", 1);
            targetMaterial.SetInt("_DstBlendAlphaWL", 1);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyAudioLinkPreset()
        {
            Undo.RecordObject(targetMaterial, "Apply AudioLink Preset");
            ApplyVRCDefaults(false);
            targetMaterial.SetFloat("_AudioLink", 1);
            targetMaterial.EnableKeyword("_AUDIOLINK");
            targetMaterial.SetInt("_WireStyleMode", 1);
            SetToggle("_CyberScanline", "_CYBER_SCANLINE", true);
            SetToggle("_CyberChromaShift", "_CYBER_CHROMA", true);
            SetToggle("_CyberGlitch", "_CYBER_GLITCH", true);
            targetMaterial.SetColor("_CyberNeonColor", new Color(0.2f, 0.85f, 1.0f, 1.0f) * 1.7f);
            targetMaterial.SetFloat("_CyberEdgeBoost", 2.2f);
            targetMaterial.SetFloat("_CyberPulseSpeed", 5.0f);
            targetMaterial.SetFloat("_CyberPulseIntensity", 0.8f);
            targetMaterial.SetFloat("_CyberScanlineDensity", 600f);
            targetMaterial.SetFloat("_CyberScanlineSpeed", 1.3f);
            targetMaterial.SetFloat("_CyberScanlineStrength", 0.25f);
            targetMaterial.SetFloat("_CyberChromaAmount", 0.24f);
            targetMaterial.SetFloat("_CyberGlitchStrength", 0.25f);
            targetMaterial.SetFloat("_CyberGlitchSpeed", 5.5f);
            targetMaterial.SetFloat("_Cage", 60);
            targetMaterial.SetFloat("_Extrude", 0.02f);
            targetMaterial.SetFloat("_ExtrudeAudiolink", 0.1f);
            targetMaterial.SetInt("_ExtrudeBand", 0);
            targetMaterial.SetFloat("_ColorFudgeAudiolink", 0.5f);
            targetMaterial.SetInt("_ColorFudgeBand", 3);
            targetMaterial.SetFloat("_NoiseMovementChronoAudiolink", 2.0f);
            targetMaterial.SetInt("_NoiseMovementChronoMode", 2);
            targetMaterial.SetInt("_ColorOneTheme", 1);
            targetMaterial.SetInt("_ColorTwoTheme", 2);
            targetMaterial.SetFloat("_CyberAudioPulse", 1.1f);
            targetMaterial.SetInt("_CyberAudioPulseBand", 2);
            targetMaterial.SetFloat("_CyberAudioGlitch", 0.45f);
            targetMaterial.SetInt("_CyberAudioGlitchBand", 3);
            targetMaterial.SetFloat("_CyberAudioData", 0.2f);
            targetMaterial.SetInt("_CyberAudioDataBand", 1);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyHologramPreset()
        {
            Undo.RecordObject(targetMaterial, "Apply Hologram Preset");
            ApplyVRCDefaults(false);
            targetMaterial.SetInt("_WireStyleMode", 1);
            SetToggle("_CyberScanline", "_CYBER_SCANLINE", true);
            SetToggle("_CyberChromaShift", "_CYBER_CHROMA", false);
            SetToggle("_CyberGlitch", "_CYBER_GLITCH", true);
            targetMaterial.SetColor("_CyberNeonColor", new Color(0.3f, 1.0f, 0.9f, 1.0f) * 1.3f);
            targetMaterial.SetFloat("_CyberEdgeBoost", 1.4f);
            targetMaterial.SetFloat("_CyberPulseSpeed", 3.0f);
            targetMaterial.SetFloat("_CyberPulseIntensity", 0.35f);
            targetMaterial.SetFloat("_CyberScanlineDensity", 680f);
            targetMaterial.SetFloat("_CyberScanlineSpeed", 1.0f);
            targetMaterial.SetFloat("_CyberScanlineStrength", 0.24f);
            targetMaterial.SetFloat("_CyberGlitchStrength", 0.28f);
            targetMaterial.SetFloat("_CyberGlitchSpeed", 6.0f);
            targetMaterial.SetFloat("_Cage", 40);
            targetMaterial.SetFloat("_Extrude", 0.015f);
            targetMaterial.SetFloat("_Cage2", 60);
            targetMaterial.SetFloat("_Extrude2", 0.01f);
            targetMaterial.SetFloat("_TriangleShape", 0.1f);
            targetMaterial.SetFloat("_TriangleNumerator", 0.012f);
            targetMaterial.SetFloat("_TrianglePower", 2.8f);
            targetMaterial.SetFloat("_DitherMultiple", 8);
            targetMaterial.SetFloat("_LineMultiple", 18);
            targetMaterial.SetVector("_NoiseSettings", new Vector4(12, 3, 2, 1));
            targetMaterial.SetVector("_NoiseMovementDirection", new Vector4(0, 0.3f, 0, 1.5f));
            targetMaterial.SetFloat("_NoiseMultiple", 22);
            targetMaterial.SetFloat("_NoiseSubtract", 0.4f);
            targetMaterial.SetColor("_Col", new Color(0, 1f, 0.8f, 1) * 1.5f);
            targetMaterial.SetColor("_Col2", new Color(0.2f, 0.8f, 1f, 1) * 1.2f);
            targetMaterial.SetVector("_AutoHueShift", new Vector2(0, 0));
            targetMaterial.SetFloat("_ColorRange", 0.25f);
            targetMaterial.SetFloat("_ColorBrightness", 1.2f);
            targetMaterial.SetFloat("_MultAlphaAndColor", 0.3f);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyCyberWirePreset()
        {
            Undo.RecordObject(targetMaterial, "Apply Cyber Wire Preset");
            ApplyVRCDefaults(false);
            targetMaterial.SetInt("_WireStyleMode", 1);
            SetToggle("_CyberScanline", "_CYBER_SCANLINE", true);
            SetToggle("_CyberChromaShift", "_CYBER_CHROMA", true);
            SetToggle("_CyberGlitch", "_CYBER_GLITCH", false);
            targetMaterial.SetColor("_CyberNeonColor", new Color(0.0f, 0.9f, 1.0f, 1.0f) * 1.8f);
            targetMaterial.SetFloat("_CyberEdgeBoost", 2.0f);
            targetMaterial.SetFloat("_CyberPulseSpeed", 4.2f);
            targetMaterial.SetFloat("_CyberPulseIntensity", 0.7f);
            targetMaterial.SetFloat("_CyberScanlineDensity", 520f);
            targetMaterial.SetFloat("_CyberScanlineSpeed", 1.2f);
            targetMaterial.SetFloat("_CyberScanlineStrength", 0.2f);
            targetMaterial.SetFloat("_CyberChromaAmount", 0.2f);
            targetMaterial.SetFloat("_AudioLink", 1);
            targetMaterial.EnableKeyword("_AUDIOLINK");
            targetMaterial.SetFloat("_CyberAudioPulse", 0.9f);
            targetMaterial.SetInt("_CyberAudioPulseBand", 2);
            targetMaterial.SetFloat("_CyberAudioGlitch", 0.25f);
            targetMaterial.SetInt("_CyberAudioGlitchBand", 3);
            targetMaterial.SetFloat("_CyberAudioData", 0.15f);
            targetMaterial.SetInt("_CyberAudioDataBand", 1);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyDataStormPreset()
        {
            Undo.RecordObject(targetMaterial, "Apply Data Storm Preset");
            ApplyVRCDefaults(false);
            targetMaterial.SetInt("_WireStyleMode", 1);
            SetToggle("_CyberScanline", "_CYBER_SCANLINE", true);
            SetToggle("_CyberChromaShift", "_CYBER_CHROMA", true);
            SetToggle("_CyberGlitch", "_CYBER_GLITCH", true);
            SetToggle("_CyberDataStream", "_CYBER_DATASTREAM", true);
            targetMaterial.SetColor("_CyberNeonColor", new Color(0.55f, 0.95f, 1.0f, 1.0f) * 2.1f);
            targetMaterial.SetFloat("_CyberEdgeBoost", 2.8f);
            targetMaterial.SetFloat("_CyberPulseSpeed", 7.0f);
            targetMaterial.SetFloat("_CyberPulseIntensity", 1.1f);
            targetMaterial.SetFloat("_CyberScanlineDensity", 760f);
            targetMaterial.SetFloat("_CyberScanlineSpeed", 2.0f);
            targetMaterial.SetFloat("_CyberScanlineStrength", 0.32f);
            targetMaterial.SetFloat("_CyberChromaAmount", 0.32f);
            targetMaterial.SetFloat("_CyberGlitchStrength", 0.42f);
            targetMaterial.SetFloat("_CyberGlitchSpeed", 8.0f);
            targetMaterial.SetFloat("_CyberDataDensity", 380f);
            targetMaterial.SetFloat("_CyberDataSpeed", 3.2f);
            targetMaterial.SetFloat("_CyberDataStrength", 0.95f);
            targetMaterial.SetFloat("_CyberDataJitter", 0.35f);
            targetMaterial.SetFloat("_AudioLink", 1);
            targetMaterial.EnableKeyword("_AUDIOLINK");
            targetMaterial.SetFloat("_CyberAudioPulse", 1.3f);
            targetMaterial.SetInt("_CyberAudioPulseBand", 0);
            targetMaterial.SetFloat("_CyberAudioGlitch", 0.6f);
            targetMaterial.SetInt("_CyberAudioGlitchBand", 3);
            targetMaterial.SetFloat("_CyberAudioData", 0.7f);
            targetMaterial.SetInt("_CyberAudioDataBand", 1);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void ApplyVRCLitePreset()
        {
            Undo.RecordObject(targetMaterial, "Apply VRC Lite Preset");
            ApplyCyberDefaults(false);
            ApplyVRCDefaults(true);
            targetMaterial.SetFloat("_AudioLink", 0f);
            targetMaterial.DisableKeyword("_AUDIOLINK");
            targetMaterial.SetFloat("_Cage", 80f);
            targetMaterial.SetFloat("_Cage2", 110f);
            targetMaterial.SetFloat("_Extrude", 0.008f);
            targetMaterial.SetFloat("_Extrude2", 0.0f);
            targetMaterial.SetFloat("_TriangleShape", 0.2f);
            targetMaterial.SetFloat("_TriangleNumerator", 0.01f);
            targetMaterial.SetFloat("_TrianglePower", 2.2f);
            targetMaterial.SetFloat("_NoiseMultiple", 12f);
            targetMaterial.SetVector("_NoiseMovementDirection", new Vector4(0f, 0.05f, 0f, 0.3f));
            targetMaterial.SetFloat("_DitherMultiple", 10f);
            targetMaterial.SetFloat("_LineMultiple", 10f);
            targetMaterial.SetColor("_Col", new Color(0.6f, 0.85f, 1f, 1f) * 1.2f);
            targetMaterial.SetColor("_Col2", new Color(0.25f, 0.4f, 0.65f, 1f) * 1.1f);
            targetMaterial.SetFloat("_ColorBrightness", 1.0f);
            EditorUtility.SetDirty(targetMaterial);
        }
    }
}
