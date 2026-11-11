using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using NataneToon.MaterialSystem;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    using static NataneUIConstants;

    /// <summary>
    /// Unified Material Editor - Integrates BatchMaterialProcessor and SceneMaterialEditor functionality
    ///
    /// Provides two workflows in one window:
    /// - Batch Mode: edit shared values across many materials at once.
    /// - Scene Mode: inspect the selected renderer material and adjust supported properties.
    /// </summary>
    public class UnifiedMaterialEditor : EditorWindow
    {
        // ========== Mode Selection ==========
        private enum EditorMode { Batch, Scene }
        private EditorMode currentMode = EditorMode.Batch;

        // ========== Common State ==========
        private Vector2 windowScrollPosition;
        private List<Material> selectedMaterials = new List<Material>();
        private Material currentMaterial;

        // ========== Batch Mode State ==========
        private Vector2 materialListScroll;
        private int selectedTab = 0;

        private string[] GetTabs() => new[] { L("パラメータ", "Parameter"), L("色", "Color"), L("テクスチャ", "Texture"), L("機能", "Feature"), L("バリアント", "Variant") };

        // Parameter adjustment (Batch Mode)
        private enum AdjustMode { Set, Add, Multiply }
        private string[] floatParameters = new[] { "_ShadowSteps", "_ShadowSharpness", "_ShadowReceive", "_OutlineWidth",
                                                   "_SpecularBlend", "_RimIntensity", "_SSSIntensity",
                                                   "_EmissionGlow", "_ReflectionIntensity", "_Metallic", "_Smoothness" };
        private int selectedParameter = 0;
        private AdjustMode adjustMode = AdjustMode.Set;
        private float adjustValue = 1.0f;

        // Color adjustment (Batch Mode)
        private enum ColorParameter { MainColor, ShadowColor, RimColor, EmissionColor, OutlineColor, SpecularColor }
        private ColorParameter selectedColorParam = ColorParameter.MainColor;
        private Color targetColor = Color.white;
        private bool adjustHue = false;
        private bool adjustSaturation = false;
        private bool adjustBrightness = false;
        private float hueShift = 0f;
        private float saturationMultiplier = 1f;
        private float valueMultiplier = 1f;

        // Texture replacement (Batch Mode)
        private string textureProperty = "_MainTex";
        private Texture2D replacementTexture;

        // Feature toggle (Batch Mode)
        private string[] features = new[] { "_SPECULAR", "_RIM_LIGHT", "_SSS", "_MATCAP", "_OUTLINE", "_EMISSION",
                                           "_NORMALMAP", "_REFLECTION", "_ENV_RIM", "_PARALLAX", "_REFRACTION",
                                           "_DETAIL_MAP", "_TRIPLANAR", "_HEIGHT_FOG",
                                           "_SURFACE_COVER", "_MIRROR_CONTROL", "_QUEST_LITE",
                                           "_WATER_DRIP", "_VIDEO_TEXTURE", "_INTERSECTION_FADE" };
        private bool[] featureStates;

        // Variant conversion (Batch Mode)
        private enum ShaderVariant { Opaque, Cutout, Transparent }
        private ShaderVariant targetVariant = ShaderVariant.Opaque;

        // ========== Scene Mode State ==========
        private GameObject selectedObject;
        private int selectedMaterialIndex = 0;

        private bool showBasicSettings = true;
        private bool showShadingSettings = true;
        private bool showSpecularSettings = false;
        private bool showRimLightSettings = false;
        private bool showOutlineSettings = false;
        private bool showEmissionSettings = false;
        private bool showAdvancedSettings = false;

        // ========== Common Settings ==========
        private bool filterNataneToonOnly = true;
        private string searchFilter = "";
        private const float CompactLayoutWidth = 720f;
        private const float NarrowLayoutWidth = 600f;

        [MenuItem("Tools/Natane/マテリアル Material/統合マテリアルエディタ Unified Material Editor", false, 12)]
        public static void ShowWindow()
        {
            var window = GetWindow<UnifiedMaterialEditor>(L("統合マテリアルエディタ", "Material Editor"));
            window.minSize = new Vector2(650, WINDOW_HEIGHT_STANDARD);
            window.Show();
        }

        private void OnEnable()
        {
            featureStates = new bool[features.Length];

            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (currentMode == EditorMode.Scene && Selection.activeGameObject != null)
            {
                selectedObject = Selection.activeGameObject;
                RefreshSceneMaterials();
            }
        }

        private void OnGUI()
        {
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
            DrawHeader();
            EditorGUILayout.Space(SPACE_SMALL);

            DrawModeSelector();
            EditorGUILayout.Space(SPACE_SMALL);

            if (currentMode == EditorMode.Batch)
            {
                DrawBatchMode();
            }
            else
            {
                DrawSceneMode();
            }

            EditorGUILayout.EndScrollView();
        }

        // ========== Header & Mode Selection ==========

        private void DrawHeader()
        {
            NataneToonShaderGUIUtility.DrawToolHeader("統合マテリアルエディタ", "Unified Material Editor", "UnifiedMaterialEditor");
            EditorGUILayout.HelpBox(
                L("一括モード: 一括処理 | シーンモード: リアルタイム編集", "Batch Mode: Batch processing | Scene Mode: Realtime editing"),
                MessageType.Info);
        }

        private void DrawModeSelector()
        {
            bool compactLayout = IsCompactLayout();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("モード", "Mode"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            if (compactLayout)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(currentMode == EditorMode.Batch, L("一括モード", "Batch Mode"), EditorStyles.miniButtonLeft))
                {
                    currentMode = EditorMode.Batch;
                }
                if (GUILayout.Toggle(currentMode == EditorMode.Scene, L("シーンモード", "Scene Mode"), EditorStyles.miniButtonRight))
                {
                    currentMode = EditorMode.Scene;
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(currentMode == EditorMode.Batch, L("一括モード", "Batch Mode"), EditorStyles.miniButtonLeft))
                {
                    currentMode = EditorMode.Batch;
                }
                if (GUILayout.Toggle(currentMode == EditorMode.Scene, L("シーンモード", "Scene Mode"), EditorStyles.miniButtonRight))
                {
                    currentMode = EditorMode.Scene;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck())
            {
                OnModeChanged();
            }

            EditorGUILayout.EndVertical();
        }

        private void OnModeChanged()
        {
            if (currentMode == EditorMode.Scene)
            {
                OnSelectionChanged();
            }
            else
            {
                currentMaterial = null;
            }
        }

        // ========== Batch Mode UI ==========

        private void DrawBatchMode()
        {
            DrawBatchMaterialSelection();
            EditorGUILayout.Space(SPACE_SMALL);

            if (selectedMaterials.Count > 0)
            {
                DrawTabs();
                EditorGUILayout.Space(SPACE_SMALL);
                DrawBatchTabContent();
            }
            else
            {
                EditorGUILayout.HelpBox(L("一括処理を開始するにはマテリアルを選択してください", "Select materials to begin batch processing"), MessageType.Info);
            }
        }

        private void DrawBatchMaterialSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("マテリアル選択", "Material Selection"), EditorStyles.boldLabel);
            DrawBatchSelectionButtons();

            EditorGUILayout.Space(SPACE_SMALL);

            if (selectedMaterials.Count > 0)
            {
                EditorGUILayout.LabelField(L($"Selected: {selectedMaterials.Count} materials", $"Selected: {selectedMaterials.Count} materials"), EditorStyles.boldLabel);

                materialListScroll = EditorGUILayout.BeginScrollView(materialListScroll, GUILayout.Height(GetAdaptiveListHeight(100f, 220f, 0.2f)));
                for (int i = selectedMaterials.Count - 1; i >= 0; i--)
                {
                    if (selectedMaterials[i] == null)
                    {
                        selectedMaterials.RemoveAt(i);
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(selectedMaterials[i], typeof(Material), false);
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        selectedMaterials.RemoveAt(i);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTabs()
        {
            string[] tabs = GetTabs();
            if (IsCompactLayout())
            {
                int columns = position.width < NarrowLayoutWidth ? 2 : 3;
                selectedTab = GUILayout.SelectionGrid(selectedTab, tabs, columns, EditorStyles.miniButton);
            }
            else
            {
                selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(BUTTON_HEIGHT_STANDARD));
            }
        }

        private void DrawBatchTabContent()
        {
            switch (selectedTab)
            {
                case 0: DrawParameterAdjust(); break;
                case 1: DrawColorAdjust(); break;
                case 2: DrawTextureReplace(); break;
                case 3: DrawFeatureToggle(); break;
                case 4: DrawVariantConvert(); break;
            }
        }

        // ========== Scene Mode UI ==========

        private void DrawSceneMode()
        {
            DrawSceneToolbar();
            EditorGUILayout.Space(SPACE_STANDARD);

            if (selectedObject == null)
            {
                EditorGUILayout.HelpBox(L("シーン内のオブジェクトを選択してください。", "Please select an object in the scene."), MessageType.Info);
                return;
            }

            if (selectedMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox(L("選択したオブジェクトにマテリアルが見つかりません。", "No materials found on selected object."), MessageType.Warning);
                return;
            }

            DrawObjectInfo();
            EditorGUILayout.Space(SPACE_STANDARD);

            DrawSceneMaterialSelector();
            EditorGUILayout.Space(SPACE_STANDARD);

            if (currentMaterial != null)
            {
                DrawSceneMaterialEditor();
            }
        }

        private void DrawSceneToolbar()
        {
            bool compactLayout = IsCompactLayout();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(L("更新", "Refresh"), EditorStyles.toolbarButton))
            {
                RefreshSceneMaterials();
            }

            if (!compactLayout)
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUI.BeginChangeCheck();
            filterNataneToonOnly = GUILayout.Toggle(filterNataneToonOnly, L("Natane のみ", "Natane Only"), EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshSceneMaterials();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(L("検索", "Search") + ":", GUILayout.Width(compactLayout ? 55f : 80f));
            EditorGUI.BeginChangeCheck();
            searchFilter = EditorGUILayout.TextField(searchFilter);
            if (GUILayout.Button(L("クリア", "Clear"), GUILayout.Width(60f)))
            {
                searchFilter = "";
                RefreshSceneMaterials();
                GUI.FocusControl(null);
            }
            if (EditorGUI.EndChangeCheck())
            {
                RefreshSceneMaterials();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawObjectInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("選択オブジェクト", "Selected Object"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L("名前:", "Name:"), selectedObject.name);
            EditorGUILayout.LabelField(L("マテリアル数:", "Material Count:"), selectedMaterials.Count.ToString());
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneMaterialSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("マテリアル選択", "Material Selection"), EditorStyles.boldLabel);

            if (selectedMaterials.Count > 1)
            {
                string[] materialNames = selectedMaterials.Select((m, i) => $"{i}: {(m != null ? m.name : "null")}").ToArray();
                EditorGUI.BeginChangeCheck();
                selectedMaterialIndex = EditorGUILayout.Popup(L("マテリアル:", "Material:"), selectedMaterialIndex, materialNames);
                if (EditorGUI.EndChangeCheck())
                {
                    currentMaterial = selectedMaterials[selectedMaterialIndex];
                }
            }
            else
            {
                EditorGUILayout.LabelField(L("マテリアル:", "Material:"), currentMaterial != null ? currentMaterial.name : "null");
            }

            if (currentMaterial != null)
            {
                EditorGUILayout.LabelField(L("シェーダー:", "Shader:"), currentMaterial.shader.name);

                if (!IsNataneToonShader(currentMaterial))
                {
                    EditorGUILayout.HelpBox(L("このマテリアルは Natane Toon Shader を使用していません。", "This material is not using Natane Toon Shader."), MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSceneMaterialEditor()
        {
            showBasicSettings = DrawFoldoutSection(L("基本設定", "Basic Settings"), showBasicSettings, () =>
            {
                DrawColorProperty("_Color", L("メインカラー", "Main Color"));
                DrawFloatProperty("_Alpha", L("アルファ", "Alpha"), 0f, 1f);
            });

            showShadingSettings = DrawFoldoutSection(L("陰影", "Shading"), showShadingSettings, () =>
            {
                DrawColorProperty("_ShadowColor", L("影色", "Shadow Color"));
                DrawIntProperty("_ShadowSteps", L("トゥーン段数", "Toon Steps"), 1, 10);
                DrawFloatProperty("_ShadowSharpness", L("シャープさ", "Sharpness"), 0f, 1f);
                DrawFloatProperty("_ShadowReceive", L("影受け", "Shadow Receive"), 0f, 1f);
            });

            showSpecularSettings = DrawFoldoutSection(L("スペキュラー", "Specular"), showSpecularSettings, () =>
            {
                if (currentMaterial.HasProperty("_Specular"))
                {
                    DrawToggleProperty("_Specular", L("スペキュラーを使用", "Use Specular"), "_SPECULAR");
                    if (currentMaterial.GetFloat("_Specular") > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        DrawColorProperty("_SpecularColor", L("色", "Color"));
                        DrawFloatProperty("_SpecularBlend", L("強度", "Intensity"), 0f, 1f);
                        DrawFloatProperty("_SpecularSize", L("サイズ", "Size"), 0f, 1f);
                        DrawFloatProperty("_SpecularSoftness", L("シャープさ", "Sharpness"), 0f, 1f);
                        EditorGUI.indentLevel--;
                    }
                }
            });

            showRimLightSettings = DrawFoldoutSection(L("リムライト", "Rim Light"), showRimLightSettings, () =>
            {
                if (currentMaterial.HasProperty("_RimLight"))
                {
                    DrawToggleProperty("_RimLight", L("リムライトを使用", "Use Rim Light"), "_RIM_LIGHT");
                    if (currentMaterial.GetFloat("_RimLight") > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        DrawColorProperty("_RimColor", L("色", "Color"));
                        DrawFloatProperty("_RimIntensity", L("強度", "Intensity"), 0f, 2f);
                        DrawFloatProperty("_RimPower", L("強さ", "Power"), 0.1f, 10f);
                        EditorGUI.indentLevel--;
                    }
                }
            });

            showOutlineSettings = DrawFoldoutSection(L("アウトライン", "Outline"), showOutlineSettings, () =>
            {
                if (currentMaterial.HasProperty("_Outline"))
                {
                    DrawToggleProperty("_Outline", L("アウトラインを使用", "Use Outline"), "_OUTLINE");
                    if (currentMaterial.GetFloat("_Outline") > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        DrawColorProperty("_OutlineColor", L("色", "Color"));
                        DrawFloatProperty("_OutlineWidth", L("幅", "Width"), 0f, 0.1f);
                        EditorGUI.indentLevel--;
                    }
                }
            });

            showEmissionSettings = DrawFoldoutSection(L("エミッション", "Emission"), showEmissionSettings, () =>
            {
                if (currentMaterial.HasProperty("_Emission"))
                {
                    DrawToggleProperty("_Emission", L("エミッションを使用", "Use Emission"), "_EMISSION");
                    if (currentMaterial.GetFloat("_Emission") > 0.5f)
                    {
                        EditorGUI.indentLevel++;
                        DrawColorProperty("_EmissionColor", L("色", "Color"));
                        DrawFloatProperty("_EmissionGlow", L("強度", "Intensity"), 0f, 1f);
                        EditorGUI.indentLevel--;
                    }
                }
            });

            showAdvancedSettings = DrawFoldoutSection(L("高度な設定", "Advanced"), showAdvancedSettings, () =>
            {
                DrawFloatProperty("_Metallic", L("メタリック", "Metallic"), 0f, 1f);
                DrawFloatProperty("_Smoothness", L("スムースネス", "Smoothness"), 0f, 1f);
                if (currentMaterial.HasProperty("_Cull"))
                {
                    DrawIntProperty("_Cull", L("カリングモード", "Cull Mode"), 0, 2);
                }
            });

            EditorGUILayout.Space(SPACE_STANDARD);

            if (IsCompactLayout())
            {
                if (GUILayout.Button(L("プリセット適用", "Apply Preset")))
                {
                    ShowPresetMenu();
                }
                if (GUILayout.Button(L("リセット", "Reset")))
                {
                    if (EditorUtility.DisplayDialog(L("確認", "Confirm"),
                        L("マテリアルを初期値に戻しますか？", "Reset material to default values?"),
                        L("はい", "Yes"), L("いいえ", "No")))
                    {
                        ResetMaterialToDefault();
                    }
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(L("プリセット適用", "Apply Preset")))
                {
                    ShowPresetMenu();
                }
                if (GUILayout.Button(L("リセット", "Reset")))
                {
                    if (EditorUtility.DisplayDialog(L("確認", "Confirm"),
                        L("マテリアルを初期値に戻しますか？", "Reset material to default values?"),
                        L("はい", "Yes"), L("いいえ", "No")))
                    {
                        ResetMaterialToDefault();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ========== Batch Mode: Parameter Adjustment ==========

        private void DrawParameterAdjust()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("一括パラメータ調整", "Batch Parameter Adjustment"), EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                L("Set = 値を置き換え / Add = 現在値に加算 / Multiply = 現在値に乗算", "Set = Replace value, Add = Add to current, Multiply = Multiply current"),
                MessageType.Info);

            EditorGUILayout.Space(SPACE_SMALL);

            selectedParameter = EditorGUILayout.Popup(L("パラメータ", "Parameter"), selectedParameter, floatParameters);
            adjustMode = (AdjustMode)EditorGUILayout.EnumPopup(L("方式", "Mode"), adjustMode);

            string label = adjustMode == AdjustMode.Set ? L("新しい値", "New Value") :
                          adjustMode == AdjustMode.Add ? L("加算量", "Add Amount") : L("乗算値", "Multiply By");
            adjustValue = EditorGUILayout.FloatField(label, adjustValue);

            EditorGUILayout.Space(SPACE_STANDARD);

            // Preview
            if (selectedMaterials.Count > 0 && selectedMaterials[0] != null)
            {
                string paramName = floatParameters[selectedParameter];
                if (selectedMaterials[0].HasProperty(paramName))
                {
                    float currentValue = selectedMaterials[0].GetFloat(paramName);
                    float newValue = CalculateNewValue(currentValue, adjustValue, adjustMode);
                    EditorGUILayout.LabelField(L($"Example: {currentValue:F3} -> {newValue:F3}", $"Example: {currentValue:F3} -> {newValue:F3}"));
                }
            }

            EditorGUILayout.Space(SPACE_STANDARD);

            if (GUILayout.Button(L("選択中すべてに適用", "Apply to All Selected Materials"), GUILayout.Height(BUTTON_HEIGHT_LARGE)))
            {
                ApplyParameterAdjustment();
            }

            EditorGUILayout.EndVertical();
        }

        // ========== Batch Mode: Color Adjustment ==========

        private void DrawColorAdjust()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("一括色調整", "Batch Color Adjustment"), EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                L("絶対色の設定、または HSV の相対調整ができます。", "Can set absolute color or adjust HSV values relatively."),
                MessageType.Info);

            EditorGUILayout.Space(SPACE_SMALL);

            selectedColorParam = (ColorParameter)EditorGUILayout.EnumPopup(L("色パラメータ", "Color Parameter"), selectedColorParam);

            EditorGUILayout.Space(SPACE_STANDARD);

            // Absolute color setting
            EditorGUILayout.LabelField(L("絶対色設定", "Absolute Color Setting"), EditorStyles.boldLabel);
            targetColor = EditorGUILayout.ColorField(L("設定色", "Set Color To"), targetColor);

            if (GUILayout.Button(L("色を設定", "Set Color"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                ApplyColorSet();
            }

            EditorGUILayout.Space(SPACE_STANDARD);
            DrawSeparator();
            EditorGUILayout.Space(SPACE_STANDARD);

            // Relative HSV adjustment
            EditorGUILayout.LabelField(L("相対 HSV 調整", "Relative HSV Adjustment"), EditorStyles.boldLabel);

            adjustHue = EditorGUILayout.Toggle(L("色相を調整", "Adjust Hue"), adjustHue);
            if (adjustHue)
            {
                hueShift = EditorGUILayout.Slider(L("色相シフト", "Hue Shift"), hueShift, -180f, 180f);
            }

            adjustSaturation = EditorGUILayout.Toggle(L("彩度を調整", "Adjust Saturation"), adjustSaturation);
            if (adjustSaturation)
            {
                saturationMultiplier = EditorGUILayout.Slider(L("彩度倍率", "Saturation Multiply"), saturationMultiplier, 0f, 2f);
            }

            adjustBrightness = EditorGUILayout.Toggle(L("明るさを調整", "Adjust Brightness"), adjustBrightness);
            if (adjustBrightness)
            {
                valueMultiplier = EditorGUILayout.Slider(L("明るさ倍率", "Brightness Multiply"), valueMultiplier, 0f, 2f);
            }

            EditorGUILayout.Space(SPACE_STANDARD);

            if (GUILayout.Button(L("HSV 調整を適用", "Apply HSV Adjustment"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                ApplyHSVAdjustment();
            }

            EditorGUILayout.EndVertical();
        }

        // ========== Batch Mode: Texture Replacement ==========

        private void DrawTextureReplace()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("一括テクスチャ置換", "Batch Texture Replacement"), EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                L("選択中のすべてのマテリアルでテクスチャを置き換えます。", "Replace textures across all selected materials."),
                MessageType.Info);

            EditorGUILayout.Space(SPACE_SMALL);

            string[] textureProps = new[] { "_MainTex", "_BumpMap", "_EmissionMap", "_MatCapTex", "_MatCapTex2", "_MatCapTex3", "_RampTex",
                                           "_SpecularMask", "_RimMask", "_SSSMask", "_MatCapMask", "_MatCapMask2", "_MatCapMask3", "_EmissionMask",
                                           "_ReflectionMask", "_ParallaxMap" };
            int selectedProp = System.Array.IndexOf(textureProps, textureProperty);
            if (selectedProp < 0) selectedProp = 0;

            selectedProp = EditorGUILayout.Popup(L("テクスチャプロパティ", "Texture Property"), selectedProp, textureProps);
            textureProperty = textureProps[selectedProp];

            replacementTexture = (Texture2D)EditorGUILayout.ObjectField(
                L("置換テクスチャ", "Replacement Texture"),
                replacementTexture,
                typeof(Texture2D),
                false);

            EditorGUILayout.Space(SPACE_STANDARD);

            int materialsWithThisTexture = selectedMaterials.Count(m =>
                m != null && m.HasProperty(textureProperty) && m.GetTexture(textureProperty) != null);

            EditorGUILayout.LabelField(L($"Materials with {textureProperty}: {materialsWithThisTexture}/{selectedMaterials.Count}", $"Materials with {textureProperty}: {materialsWithThisTexture}/{selectedMaterials.Count}"));

            EditorGUILayout.Space(SPACE_STANDARD);

            using (new EditorGUI.DisabledScope(replacementTexture == null))
            {
                if (GUILayout.Button(L("選択中すべてのテクスチャを置換", "Replace Texture in All Selected"), GUILayout.Height(BUTTON_HEIGHT_LARGE)))
                {
                    ApplyTextureReplacement();
                }
            }

            EditorGUILayout.Space(SPACE_STANDARD);

            if (GUILayout.Button(L("選択中すべてのテクスチャをクリア", "Clear Texture in All Selected"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                if (EditorUtility.DisplayDialog(
                    L("テクスチャをクリア", "Clear Texture"),
                    L($"選択中の全マテリアルから {textureProperty} を削除しますか？", $"Remove {textureProperty} from all selected materials?"),
                    L("クリア", "Clear"),
                    L("キャンセル", "Cancel")))
                {
                    ClearTexture();
                }
            }

            EditorGUILayout.EndVertical();
        }

        // ========== Batch Mode: Feature Toggle ==========

        private void DrawFeatureToggle()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("一括機能切り替え", "Batch Feature Toggle"), EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                L("選択中の全マテリアルでシェーダー機能を有効/無効にします。", "Enable or disable shader features across all selected materials."),
                MessageType.Info);

            EditorGUILayout.Space(SPACE_SMALL);

            for (int i = 0; i < features.Length; i++)
            {
                featureStates[i] = EditorGUILayout.Toggle(GetFeatureName(features[i]), featureStates[i]);
            }

            EditorGUILayout.Space(SPACE_STANDARD);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(L("選択機能を有効化", "Enable Selected Features"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                ApplyFeatureToggle(true);
            }

            if (GUILayout.Button(L("選択機能を無効化", "Disable Selected Features"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                ApplyFeatureToggle(false);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(SPACE_SMALL);

            if (GUILayout.Button(L("全機能を無効化（最高性能）", "Disable All Features (Max Performance)"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                if (EditorUtility.DisplayDialog(
                    L("全機能を無効化", "Disable All Features"),
                    L("最高性能向けに全シェーダー機能を無効化しますか？", "Disable all shader features for maximum performance?"),
                    L("すべて無効化", "Disable All"),
                    L("キャンセル", "Cancel")))
                {
                    DisableAllFeatures();
                }
            }

            EditorGUILayout.EndVertical();
        }

        // ========== Batch Mode: Variant Conversion ==========

        private void DrawVariantConvert()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("一括バリアント変換", "Batch Variant Conversion"), EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                L("シェーダーバリアント（Opaque/Cutout/Transparent）間でマテリアルを変換します。", "Convert materials between shader variants (Opaque/Cutout/Transparent)."),
                MessageType.Info);

            EditorGUILayout.Space(SPACE_SMALL);

            targetVariant = (ShaderVariant)EditorGUILayout.EnumPopup(L("変換先バリアント", "Target Variant"), targetVariant);

            EditorGUILayout.Space(SPACE_STANDARD);

            // Statistics
            int opaqueCount = selectedMaterials.Count(m => m != null && m.shader.name.Contains("Toon Shader") && !m.shader.name.Contains("Cutout") && !m.shader.name.Contains("Transparent"));
            int cutoutCount = selectedMaterials.Count(m => m != null && m.shader.name.Contains("Cutout"));
            int transparentCount = selectedMaterials.Count(m => m != null && m.shader.name.Contains("Transparent"));

            EditorGUILayout.LabelField(L("現在の内訳:", "Current Distribution:"));
            EditorGUILayout.LabelField($"  Opaque: {opaqueCount}");
            EditorGUILayout.LabelField($"  Cutout: {cutoutCount}");
            EditorGUILayout.LabelField($"  Transparent: {transparentCount}");

            EditorGUILayout.Space(SPACE_STANDARD);

            if (GUILayout.Button(L($"Convert All to {targetVariant}", $"Convert All to {targetVariant}"), GUILayout.Height(BUTTON_HEIGHT_LARGE)))
            {
                ApplyVariantConversion();
            }

            EditorGUILayout.EndVertical();
        }

        // ========== Common UI Helper Methods ==========

        private bool DrawFoldoutSection(string title, bool foldout, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            if (foldout)
            {
                EditorGUI.indentLevel++;
                try
                {
                    drawContent?.Invoke();
                }
                catch (System.Exception e)
                {
                    EditorGUILayout.HelpBox(L($"Error: {e.Message}", $"Error: {e.Message}"), MessageType.Error);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            return foldout;
        }

        private void DrawColorProperty(string propertyName, string label)
        {
            if (currentMaterial == null || !currentMaterial.HasProperty(propertyName)) return;

            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(label, currentMaterial.GetColor(propertyName));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(currentMaterial, "Change Material Color");
                currentMaterial.SetColor(propertyName, newColor);
                EditorUtility.SetDirty(currentMaterial);
            }
        }

        private void DrawFloatProperty(string propertyName, string label, float min, float max)
        {
            if (currentMaterial == null || !currentMaterial.HasProperty(propertyName)) return;

            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(label, currentMaterial.GetFloat(propertyName), min, max);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(currentMaterial, "Change Material Float");
                currentMaterial.SetFloat(propertyName, newValue);
                EditorUtility.SetDirty(currentMaterial);
            }
        }

        private void DrawIntProperty(string propertyName, string label, int min, int max)
        {
            if (currentMaterial == null || !currentMaterial.HasProperty(propertyName)) return;

            EditorGUI.BeginChangeCheck();
            int newValue = EditorGUILayout.IntSlider(label, (int)currentMaterial.GetFloat(propertyName), min, max);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(currentMaterial, "Change Material Int");
                currentMaterial.SetFloat(propertyName, newValue);
                EditorUtility.SetDirty(currentMaterial);
            }
        }

        private void DrawToggleProperty(string propertyName, string label, string keyword = null)
        {
            if (currentMaterial == null || !currentMaterial.HasProperty(propertyName)) return;

            EditorGUI.BeginChangeCheck();
            bool newValue = EditorGUILayout.Toggle(label, currentMaterial.GetFloat(propertyName) > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(currentMaterial, "Change Material Toggle");
                currentMaterial.SetFloat(propertyName, newValue ? 1f : 0f);
                if (!string.IsNullOrEmpty(keyword))
                {
                    if (newValue)
                    {
                        currentMaterial.EnableKeyword(keyword);
                    }
                    else
                    {
                        currentMaterial.DisableKeyword(keyword);
                    }
                }
                EditorUtility.SetDirty(currentMaterial);
            }
        }

        private void DrawSeparator()
        {
            EditorGUILayout.Space(SPACE_SMALL);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space(SPACE_SMALL);
        }

        private void DrawBatchSelectionButtons()
        {
            if (IsCompactLayout())
            {
                EditorGUILayout.BeginHorizontal();
                DrawBatchSelectionButton(L("選択中を追加", "Add Selected"), AddSelectedMaterials);
                DrawBatchSelectionButton(L("すべて追加", "Add All"), AddAllNataneToonMaterials);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawBatchSelectionButton(L("名前で追加", "By Name"), ShowAddByNameDialog);
                DrawBatchSelectionButton(L("クリア", "Clear"), () => selectedMaterials.Clear());
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawBatchSelectionButton(L("選択中を追加", "Add Selected"), AddSelectedMaterials);
            DrawBatchSelectionButton(L("すべて追加", "Add All"), AddAllNataneToonMaterials);
            DrawBatchSelectionButton(L("名前で追加", "By Name"), ShowAddByNameDialog);
            DrawBatchSelectionButton(L("クリア", "Clear"), () => selectedMaterials.Clear());
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBatchSelectionButton(string label, System.Action action)
        {
            if (GUILayout.Button(label, GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                action?.Invoke();
            }
        }

        private bool IsCompactLayout()
        {
            return position.width < CompactLayoutWidth;
        }

        private float GetAdaptiveListHeight(float minHeight, float maxHeight, float ratio)
        {
            return Mathf.Clamp(position.height * ratio, minHeight, maxHeight);
        }

        // ========== Material Management ==========

        private void AddSelectedMaterials()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is Material material)
                {
                    if (!selectedMaterials.Contains(material))
                    {
                        selectedMaterials.Add(material);
                    }
                }
            }
        }

        private void AddAllNataneToonMaterials()
        {
            foreach (MaterialIndexEntry entry in NataneAssetIndexService.EnumerateMaterialEntries(materialEntry => materialEntry.isNataneShader))
            {
                Material material = NataneAssetIndexService.LoadMaterial(entry);
                if (material != null && !selectedMaterials.Contains(material))
                {
                    selectedMaterials.Add(material);
                }
            }

            Debug.Log($"[UnifiedMaterialEditor] Added {selectedMaterials.Count} Natane Toon materials.");
        }

        private void ShowAddByNameDialog()
        {
            string searchTerm = EditorInputDialog.Show(
                L("名前でマテリアルを追加", "Add Materials by Name"),
                L("名前を入力:", "Enter name:"),
                string.Empty);
            if (!string.IsNullOrEmpty(searchTerm))
            {
                AddMaterialsByName(searchTerm);
            }
        }

        private void AddMaterialsByName(string searchTerm)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material");
            int addedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                if (material.name.IndexOf(searchTerm, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!selectedMaterials.Contains(material))
                {
                    selectedMaterials.Add(material);
                    addedCount++;
                }
            }

            Debug.Log($"[UnifiedMaterialEditor] Added {addedCount} materials matching '{searchTerm}'.");
        }

        private void RefreshSceneMaterials()
        {
            selectedMaterials.Clear();

            if (selectedObject == null)
            {
                currentMaterial = null;
                return;
            }

            Renderer renderer = selectedObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null)
                    {
                        continue;
                    }

                    if (filterNataneToonOnly && !IsNataneToonShader(mat))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(searchFilter) &&
                        mat.name.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    selectedMaterials.Add(mat);
                }
            }

            if (selectedMaterials.Count > 0)
            {
                selectedMaterialIndex = Mathf.Clamp(selectedMaterialIndex, 0, selectedMaterials.Count - 1);
                currentMaterial = selectedMaterials[selectedMaterialIndex];
            }
            else
            {
                currentMaterial = null;
            }
        }

        private bool IsNataneToonShader(Material mat)
        {
            if (mat == null || mat.shader == null)
            {
                return false;
            }

            return mat.shader.name.Contains("Natane") && mat.shader.name.Contains("Toon");
        }

        // ========== Batch Operations ==========

        private void ApplyParameterAdjustment()
        {
            string paramName = floatParameters[selectedParameter];
            int successCount = 0;

            foreach (Material material in selectedMaterials)
            {
                if (material == null || !material.HasProperty(paramName))
                {
                    continue;
                }

                Undo.RecordObject(material, "Batch Parameter Adjustment");

                float currentValue = material.GetFloat(paramName);
                float newValue = CalculateNewValue(currentValue, adjustValue, adjustMode);
                material.SetFloat(paramName, newValue);

                EditorUtility.SetDirty(material);
                successCount++;
            }

            EditorUtility.DisplayDialog(
                L("パラメータ調整完了", "Parameter Adjusted"),
                L($"Adjusted {paramName} in {successCount} materials", $"Adjusted {paramName} in {successCount} materials"),
                "OK");
        }

        private float CalculateNewValue(float current, float adjust, AdjustMode mode)
        {
            switch (mode)
            {
                case AdjustMode.Set:
                    return adjust;
                case AdjustMode.Add:
                    return current + adjust;
                case AdjustMode.Multiply:
                    return current * adjust;
                default:
                    return current;
            }
        }

        private void ApplyColorSet()
        {
            string colorPropName = GetColorPropertyName(selectedColorParam);
            int successCount = 0;

            foreach (Material material in selectedMaterials)
            {
                if (material == null || !material.HasProperty(colorPropName))
                {
                    continue;
                }

                Undo.RecordObject(material, "Batch Color Set");
                material.SetColor(colorPropName, targetColor);
                EditorUtility.SetDirty(material);
                successCount++;
            }

            EditorUtility.DisplayDialog(
                L("色設定完了", "Color Set"),
                L($"Set {colorPropName} in {successCount} materials", $"Set {colorPropName} in {successCount} materials"),
                "OK");
        }

        private void ApplyHSVAdjustment()
        {
            string colorPropName = GetColorPropertyName(selectedColorParam);
            int successCount = 0;

            foreach (Material material in selectedMaterials)
            {
                if (material == null || !material.HasProperty(colorPropName))
                {
                    continue;
                }

                Undo.RecordObject(material, "Batch HSV Adjustment");

                Color currentColor = material.GetColor(colorPropName);
                Color newColor = AdjustColorHSV(currentColor);
                material.SetColor(colorPropName, newColor);

                EditorUtility.SetDirty(material);
                successCount++;
            }

            EditorUtility.DisplayDialog(
                L("HSV 調整完了", "HSV Adjusted"),
                L($"Adjusted {colorPropName} in {successCount} materials", $"Adjusted {colorPropName} in {successCount} materials"),
                "OK");
        }

        private Color AdjustColorHSV(Color color)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(color, out h, out s, out v);

            if (adjustHue)
            {
                h = (h + hueShift / 360f) % 1f;
                if (h < 0f)
                {
                    h += 1f;
                }
            }

            if (adjustSaturation)
            {
                s = Mathf.Clamp01(s * saturationMultiplier);
            }

            if (adjustBrightness)
            {
                v = Mathf.Clamp01(v * valueMultiplier);
            }

            Color result = Color.HSVToRGB(h, s, v);
            result.a = color.a;
            return result;
        }

        private void ApplyTextureReplacement()
        {
            int successCount = 0;

            foreach (Material material in selectedMaterials)
            {
                if (material == null || !material.HasProperty(textureProperty))
                {
                    continue;
                }

                Undo.RecordObject(material, "Batch Texture Replace");
                material.SetTexture(textureProperty, replacementTexture);
                EditorUtility.SetDirty(material);
                successCount++;
            }

            EditorUtility.DisplayDialog(
                L("テクスチャ置換完了", "Texture Replaced"),
                L($"Replaced {textureProperty} in {successCount} materials", $"Replaced {textureProperty} in {successCount} materials"),
                "OK");
        }

        private void ClearTexture()
        {
            int successCount = 0;

            foreach (Material material in selectedMaterials)
            {
                if (material == null || !material.HasProperty(textureProperty))
                {
                    continue;
                }

                Undo.RecordObject(material, "Batch Texture Clear");
                material.SetTexture(textureProperty, null);
                EditorUtility.SetDirty(material);
                successCount++;
            }

            EditorUtility.DisplayDialog(
                L("テクスチャクリア完了", "Texture Cleared"),
                L($"{successCount} 個のマテリアルで {textureProperty} をクリアしました。", $"Cleared {textureProperty} in {successCount} materials"),
                "OK");
        }

        private void ApplyFeatureToggle(bool enable)
        {
            int successCount = 0;

            foreach (Material material in selectedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                Undo.RecordObject(material, "Batch Feature Toggle");

                for (int i = 0; i < features.Length; i++)
                {
                    if (!featureStates[i])
                    {
                        continue;
                    }

                    if (enable)
                    {
                        material.EnableKeyword(features[i]);
                        SetTogglePropertyForKeyword(material, features[i], 1.0f);
                    }
                    else
                    {
                        material.DisableKeyword(features[i]);
                        SetTogglePropertyForKeyword(material, features[i], 0.0f);
                    }
                }

                EditorUtility.SetDirty(material);
                successCount++;
            }

            int featureCount = featureStates.Count(f => f);
            string action = enable ? "Enabled" : "Disabled";

            EditorUtility.DisplayDialog(
                L("機能切り替え完了", "Features Toggled"),
                L($"{action} {featureCount} features in {successCount} materials", $"{action} {featureCount} features in {successCount} materials"),
                "OK");
        }

        private void DisableAllFeatures()
        {
            int successCount = 0;

            foreach (Material material in selectedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                Undo.RecordObject(material, "Disable All Features");

                foreach (string feature in features)
                {
                    material.DisableKeyword(feature);
                    SetTogglePropertyForKeyword(material, feature, 0.0f);
                }

                EditorUtility.SetDirty(material);
                successCount++;
            }

            EditorUtility.DisplayDialog(
                L("全機能を無効化しました", "All Features Disabled"),
                L($"Disabled all features in {successCount} materials", $"Disabled all features in {successCount} materials"),
                "OK");
        }

        private static void SetTogglePropertyForKeyword(Material material, string keyword, float value)
        {
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
            {
                if (mapping.keyword == keyword && material.HasProperty(mapping.propertyName))
                {
                    material.SetFloat(mapping.propertyName, value);
                    return;
                }
            }
        }

        private void ApplyVariantConversion()
        {
            string targetShaderName = GetShaderNameForVariant(targetVariant);
            Shader targetShader = Shader.Find(targetShaderName);

            if (targetShader == null)
            {
                EditorUtility.DisplayDialog(
                    L("エラー", "Error"),
                    L($"Shader not found: {targetShaderName}", $"Shader not found: {targetShaderName}"),
                    "OK");
                return;
            }

            int successCount = 0;

            foreach (Material material in selectedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                Undo.RecordObject(material, "Batch Variant Conversion");
                material.shader = targetShader;
                EditorUtility.SetDirty(material);
                successCount++;
            }

            EditorUtility.DisplayDialog(
                L("バリアント変換完了", "Variant Converted"),
                L($"Converted {successCount} materials to {targetVariant} variant", $"Converted {successCount} materials to {targetVariant} variant"),
                "OK");
        }

        // ========== Scene Mode: Preset & Reset ==========

        private void ShowPresetMenu()
        {
            GenericMenu menu = new GenericMenu();

            string[] presetGuids = AssetDatabase.FindAssets("t:NataneToonMaterialPreset");
            if (presetGuids.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent(L("プリセットが見つかりません", "No Presets Found")));
            }
            else
            {
                foreach (string guid in presetGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    NataneToonMaterialPreset preset = AssetDatabase.LoadAssetAtPath<NataneToonMaterialPreset>(path);
                    if (preset != null)
                    {
                        menu.AddItem(new GUIContent(preset.presetName), false, () => ApplyPreset(preset));
                    }
                }
            }

            menu.ShowAsContext();
        }

        private void ApplyPreset(NataneToonMaterialPreset preset)
        {
            if (preset == null || currentMaterial == null)
            {
                return;
            }

            Undo.RecordObject(currentMaterial, "Apply Preset");
            NataneToonMaterialPresetEditor.ApplyPresetWithUIUpdate(preset, currentMaterial);
            EditorUtility.SetDirty(currentMaterial);
            Debug.Log($"Applied preset '{preset.presetName}' to material '{currentMaterial.name}'.");
        }

        private void ResetMaterialToDefault()
        {
            if (currentMaterial == null)
            {
                return;
            }

            Undo.RecordObject(currentMaterial, "Reset Material");

            if (currentMaterial.HasProperty("_Color"))
            {
                currentMaterial.SetColor("_Color", Color.white);
            }
            if (currentMaterial.HasProperty("_Alpha"))
            {
                currentMaterial.SetFloat("_Alpha", 1f);
            }
            if (currentMaterial.HasProperty("_ShadowColor"))
            {
                currentMaterial.SetColor("_ShadowColor", new Color(0.5f, 0.5f, 0.5f, 1f));
            }
            if (currentMaterial.HasProperty("_ShadowSteps"))
            {
                currentMaterial.SetFloat("_ShadowSteps", 2f);
            }
            if (currentMaterial.HasProperty("_ShadowSharpness"))
            {
                currentMaterial.SetFloat("_ShadowSharpness", 0.5f);
            }

            EditorUtility.SetDirty(currentMaterial);
            Debug.Log($"Reset material '{currentMaterial.name}' to default values.");
        }

        // ========== Helper Methods ==========

        private string GetColorPropertyName(ColorParameter param)
        {
            switch (param)
            {
                case ColorParameter.MainColor: return "_Color";
                case ColorParameter.ShadowColor: return "_ShadowColor";
                case ColorParameter.RimColor: return "_RimColor";
                case ColorParameter.EmissionColor: return "_EmissionColor";
                case ColorParameter.OutlineColor: return "_OutlineColor";
                case ColorParameter.SpecularColor: return "_SpecularColor";
                default: return "_Color";
            }
        }

        private string GetFeatureName(string keyword)
        {
            return keyword.Replace("_", " ").Trim();
        }

        private string GetShaderNameForVariant(ShaderVariant variant)
        {
            switch (variant)
            {
                case ShaderVariant.Opaque: return "Natane/Toon Shader";
                case ShaderVariant.Cutout: return "Natane/Toon Shader Cutout";
                case ShaderVariant.Transparent: return "Natane/Toon Shader Transparent";
                default: return "Natane/Toon Shader";
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (currentMode == EditorMode.Scene)
            {
                Repaint();
            }
        }

        private void Update()
        {
            if (currentMode == EditorMode.Scene)
            {
                Repaint();
            }
        }
    }

    /// <summary>
    /// Simple input dialog helper
    /// </summary>
    public static class EditorInputDialog
    {
        public static string Show(string title, string message, string defaultValue)
        {
            // Unity doesn't have built-in input dialog, so we return default for now
            // In real implementation, would create a custom EditorWindow
            return defaultValue;
        }
    }
}
