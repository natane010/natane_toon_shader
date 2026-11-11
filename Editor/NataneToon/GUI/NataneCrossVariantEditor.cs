using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Cross-Variant Material Editor — EditorWindow that allows bulk editing of common
    /// properties across Natane Toon Shader variants (Opaque / Cutout / Transparent / etc.).
    ///
    /// Unity's built-in multi-material editing only works when all selected materials share
    /// the same shader. This window bridges that gap by using Selection.objects and direct
    /// material property access (mat.SetFloat/SetColor/etc.) to edit properties that are
    /// common across different shader variants.
    ///
    /// The UI mimics Unity's standard material inspector: sliders for Range, color fields
    /// for Color, object fields for Texture, checkboxes for toggle properties, and
    /// EditorGUI.showMixedValue for mixed-state display.
    /// </summary>
    public class NataneCrossVariantEditor : EditorWindow
    {
        // ===== State =====
        private Material[] targetMaterials = new Material[0];
        private Vector2 scrollPos;
        private bool needsRefresh = true;

        // ===== Common property info =====
        private struct PropInfo
        {
            public string name;
            public string displayName;
            public ShaderUtil.ShaderPropertyType type;
            public float rangeMin, rangeMax;
            public bool isToggle;         // true if this property maps to a shader keyword
            public string toggleKeyword;  // the keyword it controls (if isToggle)
        }

        private List<PropInfo> commonProps = new List<PropInfo>();
        private string[] variantNames = new string[0];

        // Cached toggle property set from NataneShaderKeywordSynchronizer
        private static Dictionary<string, string> _togglePropertyToKeyword;
        private static Dictionary<string, string> TogglePropertyToKeyword
        {
            get
            {
                if (_togglePropertyToKeyword == null)
                {
                    _togglePropertyToKeyword = new Dictionary<string, string>();
                    foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
                    {
                        _togglePropertyToKeyword[mapping.propertyName] = mapping.keyword;
                    }
                }
                return _togglePropertyToKeyword;
            }
        }

        // Section toggle keywords (for drawing section headers)
        // These are the "main" toggles that correspond to ShaderGUI sections.
        private static readonly HashSet<string> SectionToggleProperties = new HashSet<string>
        {
            "_Specular", "_HairSpecular", "_RimLight", "_SSS", "_MatCap",
            "_Glitter", "_Reflection", "_Iridescence", "_EnvRim",
            "_Outline", "_Emission", "_UseNormalMap", "_Parallax",
            "_Refraction", "_AudioLink", "_DistanceFade", "_VertexAnimation",
            "_VAT", "_WaterDrip", "_Smear", "_Hologram", "_Decal",
            "_BackfaceTexture", "_VideoTexture", "_UseLightVolume", "_LTCGI",
            "_ScreenTone", "_GradientBaseColor", "_UseAO", "_UseDithering",
            "_HalftoneShadow", "_ShadowEdgeNoise", "_CastShadowColorEnable",
            "_LightSnap", "_ProceduralMatCap", "_FakeReflection",
            "_PerspectiveFlat", "_HeightFade", "_IntersectionFade",
            "_Tessellation", "_DepthColorFade", "_MirrorControl",
            "_SurfaceCover", "_Triplanar", "_HeightFog", "_Fur",
            "_DetailMap", "_PBR", "_UseColorQuantize",
        };

        // ===== Window lifecycle =====

        [MenuItem("Tools/Natane/Cross-Variant Material Editor")]
        public static NataneCrossVariantEditor ShowWindow()
        {
            var win = GetWindow<NataneCrossVariantEditor>();
            win.titleContent = new GUIContent(L("Natane 一括編集", "Natane Cross-Variant Editor"));
            win.minSize = new Vector2(340, 400);
            win.Show();
            return win;
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            needsRefresh = true;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            needsRefresh = true;
            Repaint();
        }

        // ===== Selection & property discovery =====

        private void RefreshTargets()
        {
            var mats = new List<Material>();
            foreach (var obj in Selection.objects)
            {
                Material mat = obj as Material;
                if (mat != null && mat.shader != null && NataneShaderCatalog.IsNataneShader(mat.shader.name))
                    mats.Add(mat);
            }
            targetMaterials = mats.ToArray();
            variantNames = targetMaterials.Select(m => m.shader.name).Distinct().ToArray();
            FindCommonProperties();
            needsRefresh = false;
        }

        private void FindCommonProperties()
        {
            commonProps.Clear();
            if (targetMaterials.Length == 0) return;

            // Use first material's shader as the property reference
            Shader refShader = targetMaterials[0].shader;
            int propCount = ShaderUtil.GetPropertyCount(refShader);

            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.IsShaderPropertyHidden(refShader, i)) continue;

                string propName = ShaderUtil.GetPropertyName(refShader, i);

                // Check all targets have this property
                bool allHave = true;
                foreach (var mat in targetMaterials)
                {
                    if (!mat.HasProperty(propName)) { allHave = false; break; }
                }
                if (!allHave) continue;

                var info = new PropInfo
                {
                    name = propName,
                    displayName = ShaderUtil.GetPropertyDescription(refShader, i),
                    type = ShaderUtil.GetPropertyType(refShader, i),
                };

                if (info.type == ShaderUtil.ShaderPropertyType.Range)
                {
                    info.rangeMin = ShaderUtil.GetRangeLimits(refShader, i, 1);
                    info.rangeMax = ShaderUtil.GetRangeLimits(refShader, i, 2);
                }

                // Identify toggle properties
                if (TogglePropertyToKeyword.TryGetValue(propName, out string keyword))
                {
                    info.isToggle = true;
                    info.toggleKeyword = keyword;
                }

                commonProps.Add(info);
            }
        }

        // ===== Main GUI =====

        private void OnGUI()
        {
            if (needsRefresh) RefreshTargets();

            DrawHeader();

            if (targetMaterials.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    L("Project ウインドウで Natane Toon Shader のマテリアルを複数選択してください。\n異なるバリアント（Opaque / Cutout / Transparent）でも一括編集できます。",
                      "Select multiple Natane Toon Shader materials in the Project window.\nYou can bulk-edit across different variants (Opaque / Cutout / Transparent)."),
                    MessageType.Info);
                return;
            }

            DrawSelectionInfo();
            EditorGUILayout.Space(4);

            // Toolbar: Sync keywords
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(L("キーワード同期", "Sync Keywords"), EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                foreach (var mat in targetMaterials)
                {
                    NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(mat);
                    EditorUtility.SetDirty(mat);
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                L($"共通プロパティ: {commonProps.Count}", $"Common properties: {commonProps.Count}"),
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Properties
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawAllProperties();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                L("Natane クロスバリアント一括編集", "Natane Cross-Variant Editor"),
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L("更新", "Refresh"), GUILayout.Width(50)))
            {
                needsRefresh = true;
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void DrawSelectionInfo()
        {
            // Material count & variant list
            string variantList = string.Join(", ", variantNames.Select(v => v.Replace("Natane/Toon Shader", "").Trim().TrimStart('(')).Select(v => string.IsNullOrEmpty(v) ? "Opaque" : v.TrimEnd(')')));
            MessageType msgType = variantNames.Length > 1 ? MessageType.Info : MessageType.None;
            EditorGUILayout.HelpBox(
                L($"{targetMaterials.Length} マテリアル選択中（{variantNames.Length} バリアント: {variantList}）",
                  $"{targetMaterials.Length} materials selected ({variantNames.Length} variants: {variantList})"),
                msgType);
        }

        // ===== Property drawing =====

        private void DrawAllProperties()
        {
            bool inSection = false;

            for (int i = 0; i < commonProps.Count; i++)
            {
                var prop = commonProps[i];

                // Section toggle → draw as section header with checkbox
                if (prop.isToggle && SectionToggleProperties.Contains(prop.name))
                {
                    if (inSection)
                    {
                        EditorGUILayout.Space(6);
                    }
                    DrawSectionToggle(prop);
                    inSection = true;
                    continue;
                }

                DrawCommonProperty(prop);
            }
        }

        private void DrawSectionToggle(PropInfo prop)
        {
            bool mixed = GetMixedFloat(prop.name, out float value);
            bool enabled = value > 0.5f;

            EditorGUILayout.Space(4);

            // Draw a styled section header with toggle
            Rect headerRect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));

            // Background
            if (Event.current.type == EventType.Repaint)
            {
                Color bgColor = NataneToonColorPalette.PanelHeaderBackground;
                EditorGUI.DrawRect(headerRect, bgColor);

                // Left accent bar
                Color accentColor = enabled
                    ? NataneToonShaderGUIStyles.ToggleEnabledColor
                    : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 3, headerRect.height), accentColor);
            }

            // Toggle checkbox + label
            Rect toggleRect = new Rect(headerRect.x + 8, headerRect.y + 2, 18, 18);
            Rect labelRect = new Rect(headerRect.x + 28, headerRect.y + 2, headerRect.width - 80, 18);
            Rect badgeRect = new Rect(headerRect.xMax - 40, headerRect.y + 3, 34, 16);

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUI.Toggle(toggleRect, enabled);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyToggleToAll(prop.name, prop.toggleKeyword, newEnabled);
            }
            EditorGUI.showMixedValue = false;

            // Section label (bold)
            var oldStyle = EditorStyles.boldLabel.fontSize;
            EditorGUI.LabelField(labelRect, prop.displayName, EditorStyles.boldLabel);

            // ON/OFF/Mixed badge
            if (mixed)
            {
                GUI.Label(badgeRect, "---", EditorStyles.miniBoldLabel);
            }
            else
            {
                var oldColor = GUI.contentColor;
                GUI.contentColor = enabled ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(badgeRect, enabled ? "ON" : "OFF", EditorStyles.miniBoldLabel);
                GUI.contentColor = oldColor;
            }
        }

        private void DrawCommonProperty(PropInfo prop)
        {
            // Non-section toggle properties (sub-toggles like _SpecularDither, _RimLight2, etc.)
            if (prop.isToggle)
            {
                DrawToggleProp(prop);
                return;
            }

            switch (prop.type)
            {
                case ShaderUtil.ShaderPropertyType.Float:
                    // Check if it looks like a toggle (0/1 value)
                    DrawFloatProp(prop);
                    break;
                case ShaderUtil.ShaderPropertyType.Range:
                    DrawRangeProp(prop);
                    break;
                case ShaderUtil.ShaderPropertyType.Color:
                    DrawColorProp(prop);
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    DrawTextureProp(prop);
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    DrawVectorProp(prop);
                    break;
            }
        }

        // ===== Property type drawers =====

        private void DrawToggleProp(PropInfo prop)
        {
            bool mixed = GetMixedFloat(prop.name, out float value);
            bool enabled = value > 0.5f;

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUILayout.Toggle(prop.displayName, enabled);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyToggleToAll(prop.name, prop.toggleKeyword, newEnabled);
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawFloatProp(PropInfo prop)
        {
            bool mixed = GetMixedFloat(prop.name, out float value);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.FloatField(prop.displayName, value);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyFloatToAll(prop.name, newValue);
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawRangeProp(PropInfo prop)
        {
            bool mixed = GetMixedFloat(prop.name, out float value);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(prop.displayName, value, prop.rangeMin, prop.rangeMax);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyFloatToAll(prop.name, newValue);
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawColorProp(PropInfo prop)
        {
            bool mixed = GetMixedColor(prop.name, out Color value);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(new GUIContent(prop.displayName), value, true, true, false);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyColorToAll(prop.name, newColor);
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawTextureProp(PropInfo prop)
        {
            bool mixed = GetMixedTexture(prop.name, out Texture value);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            Texture newTex = (Texture)EditorGUILayout.ObjectField(
                prop.displayName, value, typeof(Texture), false,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                ApplyTextureToAll(prop.name, newTex);
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawVectorProp(PropInfo prop)
        {
            bool mixed = GetMixedVector(prop.name, out Vector4 value);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            Vector4 newVec = EditorGUILayout.Vector4Field(prop.displayName, value);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyVectorToAll(prop.name, newVec);
            }
            EditorGUI.showMixedValue = false;
        }

        // ===== Mixed value detection =====

        private bool GetMixedFloat(string propName, out float value)
        {
            value = targetMaterials[0].GetFloat(propName);
            for (int i = 1; i < targetMaterials.Length; i++)
            {
                if (Mathf.Abs(targetMaterials[i].GetFloat(propName) - value) > 0.0001f)
                    return true;
            }
            return false;
        }

        private bool GetMixedColor(string propName, out Color value)
        {
            value = targetMaterials[0].GetColor(propName);
            for (int i = 1; i < targetMaterials.Length; i++)
            {
                if (targetMaterials[i].GetColor(propName) != value) return true;
            }
            return false;
        }

        private bool GetMixedTexture(string propName, out Texture value)
        {
            value = targetMaterials[0].GetTexture(propName);
            for (int i = 1; i < targetMaterials.Length; i++)
            {
                if (targetMaterials[i].GetTexture(propName) != value) return true;
            }
            return false;
        }

        private bool GetMixedVector(string propName, out Vector4 value)
        {
            value = targetMaterials[0].GetVector(propName);
            for (int i = 1; i < targetMaterials.Length; i++)
            {
                if (targetMaterials[i].GetVector(propName) != value) return true;
            }
            return false;
        }

        // ===== Apply helpers (Undo-safe) =====

        private void ApplyFloatToAll(string propName, float value)
        {
            foreach (var mat in targetMaterials)
            {
                if (!mat.HasProperty(propName)) continue;
                Undo.RecordObject(mat, "Edit " + propName);
                mat.SetFloat(propName, value);
                EditorUtility.SetDirty(mat);
            }
        }

        private void ApplyToggleToAll(string propName, string keyword, bool enabled)
        {
            foreach (var mat in targetMaterials)
            {
                if (!mat.HasProperty(propName)) continue;
                Undo.RecordObject(mat, "Toggle " + propName);
                mat.SetFloat(propName, enabled ? 1f : 0f);
                if (!string.IsNullOrEmpty(keyword))
                {
                    if (enabled)
                        mat.EnableKeyword(keyword);
                    else
                        mat.DisableKeyword(keyword);
                }
                EditorUtility.SetDirty(mat);
            }
        }

        private void ApplyColorToAll(string propName, Color color)
        {
            foreach (var mat in targetMaterials)
            {
                if (!mat.HasProperty(propName)) continue;
                Undo.RecordObject(mat, "Edit " + propName);
                mat.SetColor(propName, color);
                EditorUtility.SetDirty(mat);
            }
        }

        private void ApplyTextureToAll(string propName, Texture tex)
        {
            foreach (var mat in targetMaterials)
            {
                if (!mat.HasProperty(propName)) continue;
                Undo.RecordObject(mat, "Edit " + propName);
                mat.SetTexture(propName, tex);
                EditorUtility.SetDirty(mat);
            }
        }

        private void ApplyVectorToAll(string propName, Vector4 vec)
        {
            foreach (var mat in targetMaterials)
            {
                if (!mat.HasProperty(propName)) continue;
                Undo.RecordObject(mat, "Edit " + propName);
                mat.SetVector(propName, vec);
                EditorUtility.SetDirty(mat);
            }
        }

        // ===== Static helper for ShaderGUI integration =====

        /// <summary>
        /// Checks if the current Selection contains Natane materials with multiple shader variants.
        /// Called from NataneToonShaderGUI to decide whether to show the cross-variant editor button.
        /// </summary>
        public static bool HasCrossVariantSelection()
        {
            var shaderNames = new HashSet<string>();
            bool hasAnyNatane = false;

            foreach (var obj in Selection.objects)
            {
                Material mat = obj as Material;
                if (mat == null || mat.shader == null) continue;
                if (!NataneShaderCatalog.IsNataneShader(mat.shader.name)) continue;
                hasAnyNatane = true;
                shaderNames.Add(mat.shader.name);
            }

            return hasAnyNatane && shaderNames.Count > 1;
        }
    }
}
