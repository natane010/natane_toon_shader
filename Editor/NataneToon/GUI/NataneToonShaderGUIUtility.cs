using UnityEngine;
using UnityEditor;
using NataneToon.MaterialSystem;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    /// <summary>
    /// Utility class for Natane Toon Shader GUI operations
    /// Provides reusable UI components and helper functions
    /// </summary>
    public static class NataneToonShaderGUIUtility
    {
        private static GUIStyle headerStyle;
        private static GUIStyle boxStyle;
        private static readonly string[] PerformanceKeywords =
        {
            "_SPECULAR", "_HAIR_SPECULAR", "_RIM_LIGHT", "_RIM_LIGHT_2", "_OFFSET_RIM_LIGHT", "_SSS", "_MATCAP", "_OUTLINE", "_EMISSION",
            "_DISSOLVE", "_HUE_SHIFT", "_SCREEN_TONE", "_SCREEN_EDGE", "_HATCHING",
            "_REFLECTION", "_ENV_RIM", "_PARALLAX", "_REFRACTION",
            "_IRIDESCENCE", "_GLITTER", "_MATCAP_2", "_MATCAP_3",
            "_AUDIOLINK", "_HOLOGRAM", "_GLITCH", "_DECAL",
            "_VAT", "_VERTEX_ANIMATION", "_PIXEL_VERTEX_LIGHTS",
            "_NORMALMAP", "_USE_LIGHT_VOLUME", "_LTCGI", "_WATERCOLOR", "_BACKFACE_TEXTURE", "_SMEAR"
        };

        /// <summary>
        /// Initialize styles
        /// </summary>
        public static void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    padding = new RectOffset(5, 5, 5, 5)
                };
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10)
                };
            }
        }

        /// <summary>
        /// Draw a section header with foldout
        /// </summary>
        public static bool DrawFoldoutHeader(string title, bool foldout)
        {
            InitializeStyles();
            EditorGUILayout.Space(5);
            return EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
        }

        /// <summary>
        /// Draw a property with optional tooltip
        /// </summary>
        public static void DrawProperty(MaterialEditor editor, MaterialProperty prop, string label = null, string tooltip = null)
        {
            GUIContent content;
            if (string.IsNullOrEmpty(label))
            {
                content = new GUIContent(prop.displayName, tooltip ?? "");
            }
            else
            {
                content = new GUIContent(label, tooltip ?? "");
            }

            editor.ShaderProperty(prop, content);
        }

        /// <summary>
        /// Draw a color property with HDR support check
        /// </summary>
        public static void DrawColorProperty(MaterialEditor editor, MaterialProperty prop, string label = null, string tooltip = null, bool hdr = false)
        {
            GUIContent content;
            if (string.IsNullOrEmpty(label))
            {
                content = new GUIContent(prop.displayName, tooltip ?? "");
            }
            else
            {
                content = new GUIContent(label, tooltip ?? "");
            }

            if (hdr)
            {
                editor.ColorProperty(prop, content.text);
            }
            else
            {
                Color oldColor = prop.colorValue;
                Color newColor = EditorGUILayout.ColorField(content, oldColor);
                if (newColor != oldColor)
                {
                    prop.colorValue = newColor;
                }
            }
        }

        /// <summary>
        /// Draw a toggle with keyword enable/disable
        /// </summary>
        public static bool DrawToggleWithKeyword(Material material, MaterialProperty prop, string keyword, string label = null, string tooltip = null)
        {
            GUIContent content;
            if (string.IsNullOrEmpty(label))
            {
                content = new GUIContent(prop.displayName, tooltip ?? "");
            }
            else
            {
                content = new GUIContent(label, tooltip ?? "");
            }

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(content, material.IsKeywordEnabled(keyword));

            if (EditorGUI.EndChangeCheck())
            {
                if (enabled)
                {
                    material.EnableKeyword(keyword);
                    prop.floatValue = 1f;
                }
                else
                {
                    material.DisableKeyword(keyword);
                    prop.floatValue = 0f;
                }
            }

            return enabled;
        }

        /// <summary>
        /// Draw help button for tool window
        /// </summary>
        public static void DrawHelpButton(string toolKey)
        {
            if (GUILayout.Button(L("Help", "Help"), GUILayout.Width(100), GUILayout.Height(25)))
            {
                UnifiedHelpSystem.ShowToolHelp(toolKey);
            }
        }

        /// <summary>
        /// Draw header with help button
        /// </summary>
        public static void DrawHeaderWithHelp(string titleJP, string titleEN, string toolKey)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(L(titleJP, titleEN), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            DrawLanguageToggleButton();
            DrawHelpButton(toolKey);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draw texture property with tiling/offset
        /// </summary>
        public static void DrawTextureProperty(MaterialEditor editor, MaterialProperty texProp, bool showScaleOffset = true)
        {
            editor.TexturePropertySingleLine(new GUIContent(texProp.displayName), texProp);

            if (showScaleOffset && texProp.textureValue != null)
            {
                EditorGUI.indentLevel++;
                editor.TextureScaleOffsetProperty(texProp);
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draw a separator line
        /// </summary>
        public static void DrawSeparator()
        {
            EditorGUILayout.Space(5);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, NataneToonEditorTheme.BorderStrong);
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// Draw a category divider with centered label
        /// </summary>
        public static void DrawCategoryDivider(string label)
        {
            EditorGUILayout.Space(8);

            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            float lineY = rect.y + rect.height * 0.5f;

            // Determine colors based on theme
            Color lineColor = NataneToonEditorTheme.BorderStrong;

            // Measure text width
            GUIStyle labelStyle = NataneToonShaderGUIStyles.CategoryDividerCenteredLabel;
            GUIContent content = new GUIContent(label);
            float textWidth = labelStyle.CalcSize(content).x + 16; // padding

            float centerX = rect.x + rect.width * 0.5f;
            float halfTextWidth = textWidth * 0.5f;

            // Draw left line
            EditorGUI.DrawRect(new Rect(rect.x, lineY, centerX - halfTextWidth - rect.x, 1), lineColor);

            // Draw right line
            EditorGUI.DrawRect(new Rect(centerX + halfTextWidth, lineY, rect.x + rect.width - centerX - halfTextWidth, 1), lineColor);

            // Draw label
            EditorGUI.LabelField(rect, label, labelStyle);

            EditorGUILayout.Space(4);
        }

        /// <summary>
        /// Draw a help box with icon
        /// </summary>
        public static void DrawHelpBox(string message, MessageType type = MessageType.Info)
        {
            EditorGUILayout.HelpBox(message, type);
        }

        /// <summary>
        /// Draw preset and sharing buttons
        /// </summary>
        public static void DrawMaterialActionsToolbar(Material material, MaterialEditor editor)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("Material Actions", "Material Actions"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Preset Browser
            if (GUILayout.Button(new GUIContent(
                L("Preset Browser", "Preset Browser"),
                L("Open Material Preset Browser", "Open Material Preset Browser")),
                GUILayout.Height(25)))
            {
                MaterialPresetBrowser.ShowWindow();
            }

            // Create Preset
            if (GUILayout.Button(new GUIContent(
                L("Save as Preset", "Save as Preset"),
                L("Create preset from this material", "Create preset from this material")),
                GUILayout.Height(25)))
            {
                CreatePresetFromMaterial(material);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // Export to File
            if (GUILayout.Button(new GUIContent(
                L("Export to File", "Export to File"),
                L("Export parameters to file", "Export parameters to file")),
                GUILayout.Height(25)))
            {
                ExportMaterialToFile(material);
            }

            // Copy to Clipboard
            if (GUILayout.Button(new GUIContent(
                L("Copy", "Copy"),
                L("Copy parameters to clipboard", "Copy parameters to clipboard")),
                GUILayout.Height(25)))
            {
                CopyToClipboard(material);
            }

            // Paste from Clipboard
            bool clipboardValid = MaterialParameterShareSystem.IsClipboardValid();
            using (new EditorGUI.DisabledScope(!clipboardValid))
            {
                if (GUILayout.Button(new GUIContent(
                    L("Paste", "Paste"),
                    L("Paste parameters from clipboard", "Paste parameters from clipboard")),
                    GUILayout.Height(25)))
                {
                    PasteFromClipboard(material);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// Create preset from material
        /// </summary>
        private static void CreatePresetFromMaterial(Material material)
        {
            string defaultName = $"{material.name}_Preset";
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Preset from Material",
                defaultName,
                "asset",
                "Choose where to save the preset");

            if (!string.IsNullOrEmpty(path))
            {
                var preset = ScriptableObject.CreateInstance<NataneToonMaterialPreset>();
                preset.presetName = System.IO.Path.GetFileNameWithoutExtension(path);
                preset.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");
                preset.CreateFromMaterial(material);

                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();

                EditorGUIUtility.PingObject(preset);

                EditorUtility.DisplayDialog(
                    L("プリセット作成完了", "Preset Created"),
                    L($"マテリアル '{material.name}' からプリセット '{preset.presetName}' を作成しました。\n\nこのプリセットファイルを他のユーザーと共有できます！",
                      $"Created preset '{preset.presetName}' from material '{material.name}'\n\nYou can now share this preset file with others!"),
                    L("OK", "OK"));
            }
        }

        /// <summary>
        /// Export material to file
        /// </summary>
        private static void ExportMaterialToFile(Material material)
        {
            string defaultFolder = MaterialParameterShareSystem.GetDefaultExportFolder();
            string defaultName = $"{material.name}_Parameters";
            string extension = MaterialParameterShareSystem.GetFileExtension();

            string path = EditorUtility.SaveFilePanel(
                "Export Material Parameters",
                defaultFolder,
                defaultName,
                extension.TrimStart('.'));

            if (!string.IsNullOrEmpty(path))
            {
                string result = MaterialParameterShareSystem.ExportToFile(material, path);
                if (!string.IsNullOrEmpty(result))
                {
                    EditorUtility.DisplayDialog(
                        L("エクスポート完了", "Export Successful"),
                        L($"マテリアルパラメータをエクスポートしました:\n{result}\n\nこのファイルを共有して設定を転送できます！",
                          $"Material parameters exported to:\n{result}\n\nShare this file with others to transfer settings!"),
                        L("OK", "OK"));
                }
            }
        }

        /// <summary>
        /// Copy material to clipboard
        /// </summary>
        private static void CopyToClipboard(Material material)
        {
            bool success = MaterialParameterShareSystem.CopyToClipboard(material);
            if (success)
            {
                EditorUtility.DisplayDialog(
                    L("クリップボードにコピー完了", "Copied to Clipboard"),
                    L($"マテリアル '{material.name}' のパラメータをクリップボードにコピーしました。\n\n他のマテリアルに貼り付けたり、共有したりできます。",
                      $"Material '{material.name}' parameters copied to clipboard.\n\nYou can now paste these parameters to another material or share with others."),
                    L("OK", "OK"));
            }
        }

        /// <summary>
        /// Paste material from clipboard
        /// </summary>
        private static void PasteFromClipboard(Material material)
        {
            var info = MaterialParameterShareSystem.GetClipboardInfo();
            bool proceed = EditorUtility.DisplayDialog(
                L("マテリアルパラメータの貼り付け", "Paste Material Parameters"),
                L($"以下のパラメータを貼り付けます:\n\n" +
                  $"マテリアル: {info.materialName}\n" +
                  $"エクスポート元: {info.exportedBy}\n" +
                  $"エクスポート日: {info.exportDate}\n" +
                  $"メモ: {info.notes}\n\n" +
                  $"'{material.name}' の現在の設定が上書きされます",
                  $"Paste parameters from:\n\n" +
                  $"Material: {info.materialName}\n" +
                  $"Exported by: {info.exportedBy}\n" +
                  $"Export date: {info.exportDate}\n" +
                  $"Notes: {info.notes}\n\n" +
                  $"This will overwrite current settings of '{material.name}'"),
                L("貼り付け", "Paste"),
                L("キャンセル", "Cancel"));

            if (proceed)
            {
                Undo.RecordObject(material, "Paste Material Parameters");
                bool success = MaterialParameterShareSystem.PasteFromClipboard(material);

                if (success)
                {
                    EditorUtility.SetDirty(material);
                    EditorUtility.DisplayDialog(
                        L("貼り付け完了", "Paste Successful"),
                        L($"マテリアルパラメータを '{material.name}' に貼り付けました",
                          $"Material parameters pasted to '{material.name}'"),
                        L("OK", "OK"));
                }
            }
        }

        /// <summary>
        /// Draw a property with inline toggle
        /// </summary>
        public static void DrawPropertyWithToggle(MaterialEditor editor, MaterialProperty toggleProp, MaterialProperty valueProp, string label, string tooltip = null)
        {
            EditorGUILayout.BeginHorizontal();

            // Toggle
            bool enabled = toggleProp.floatValue > 0.5f;
            enabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(15));
            toggleProp.floatValue = enabled ? 1f : 0f;

            // Property
            using (new EditorGUI.DisabledScope(!enabled))
            {
                DrawProperty(editor, valueProp, label, tooltip);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Get MaterialProperty by name safely
        /// </summary>
        public static MaterialProperty FindProperty(string propertyName, MaterialProperty[] properties, bool optional = false)
        {
            foreach (MaterialProperty prop in properties)
            {
                if (prop.name == propertyName)
                {
                    return prop;
                }
            }

            if (!optional)
            {
                throw new System.ArgumentException($"Could not find MaterialProperty: '{propertyName}'");
            }

            return null;
        }

        /// <summary>
        /// Validate material shader compatibility
        /// </summary>
        public static bool ValidateShaderCompatibility(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            string shaderName = material.shader.name;
            return shaderName.Contains("Natane") && shaderName.Contains("Toon");
        }

        /// <summary>
        /// Draw performance indicator
        /// </summary>
        public static void DrawPerformanceIndicator(Material material)
        {
            int activeFeatures = CountActiveFeatures(material);
            string rating = GetPerformanceRating(activeFeatures);
            Color ratingColor = GetRatingColor(activeFeatures);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(L("Performance Rating", "Performance Rating") + ":", GUILayout.Width(130));

            Color oldColor = GUI.color;
            GUI.color = ratingColor;
            EditorGUILayout.LabelField(rating, EditorStyles.boldLabel);
            GUI.color = oldColor;

            EditorGUILayout.EndHorizontal();
            // Color-independent text label for accessibility (WCAG)
            EditorGUILayout.LabelField(
                $"{L("評価ランク", "Rating")}: {GetRatingLetter(activeFeatures)}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"{L("Active Features", "Active Features")}: {activeFeatures}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"{L("Estimated Cost", "Estimated Cost")}: {GetEstimatedCostLabel(activeFeatures)}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        public static void DrawCompactPerformanceSummary(Material material)
        {
            DrawCompactPerformanceSummary(material, NataneToonSamplerBudgetEstimator.Estimate(material));
        }

        public static void DrawCompactPerformanceSummary(Material material, NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate samplerBudget)
        {
            if (material == null)
            {
                return;
            }

            int activeFeatures = CountActiveFeatures(material);
            string rating = GetPerformanceRating(activeFeatures);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(L("Performance", "Performance"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                $"{L("Sampler", "Sampler")}: {samplerBudget.EstimatedSamplers}/{samplerBudget.Limit}",
                EditorStyles.miniBoldLabel,
                GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"{L("Features", "Features")}: {activeFeatures} / {L("Rating", "Rating")}: {rating} / {L("Status", "Status")}: {GetSamplerBudgetStatus(samplerBudget)}" +
                (samplerBudget.ExtraPassCount > 0 ? $" / {L("Extra Pass", "Extra Pass")}: +{samplerBudget.ExtraPassCount}" : string.Empty),
                EditorStyles.wordWrappedMiniLabel);
            DrawSamplerBudgetBar(samplerBudget);
            EditorGUILayout.EndVertical();
        }

        private static int CountActiveFeatures(Material material)
        {
            int count = 0;

            foreach (string keyword in PerformanceKeywords)
            {
                if (material.IsKeywordEnabled(keyword))
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetPerformanceRating(int featureCount)
        {
            // P-15: WCAG-compliant dual encoding (shape + color)
            if (featureCount <= 3) return L("\u2713 \u512A\u79C0 (A)", "\u2713 Excellent (A)");
            if (featureCount <= 6) return L("\u2192 \u826F\u597D (B)", "\u2192 Good (B)");
            if (featureCount <= 9) return L("\u25B3 \u6CE8\u610F (C)", "\u25B3 Fair (C)");
            return L("\u2715 \u91CD\u3044 (D)", "\u2715 Heavy (D)");
        }

        private static string GetRatingLetter(int featureCount)
        {
            if (featureCount <= 3) return "A";
            if (featureCount <= 6) return "B";
            if (featureCount <= 9) return "C";
            return "D";
        }

        private static string GetEstimatedCostLabel(int featureCount)
        {
            if (featureCount <= 3) return L("Low", "Low");
            if (featureCount <= 6) return L("Medium", "Medium");
            if (featureCount <= 9) return L("Moderate", "Moderate");
            return L("High", "High");
        }

        private static Color GetRatingColor(int featureCount)
        {
            // P-15: WCAG-compliant colors paired with shape indicators
            if (featureCount <= 3) return new Color(0.3f, 0.8f, 0.3f);  // Green
            if (featureCount <= 6) return new Color(0.3f, 0.6f, 1.0f);  // Blue
            if (featureCount <= 9) return new Color(1.0f, 0.7f, 0.2f);  // Orange
            return new Color(1.0f, 0.3f, 0.3f);                         // Red
        }

        public static void DrawPerformanceIndicatorWithSamplerBudget(Material material)
        {
            DrawPerformanceIndicatorWithSamplerBudget(material, NataneToonSamplerBudgetEstimator.Estimate(material));
        }

        public static void DrawPerformanceIndicatorWithSamplerBudget(Material material, NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate samplerBudget)
        {
            DrawPerformanceIndicator(material);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("Estimated Sampler Budget", "Estimated Sampler Budget"), EditorStyles.boldLabel);
            DrawSamplerBudgetBar(samplerBudget);
            EditorGUILayout.LabelField(
                $"{L("Base", "Base")}: {samplerBudget.BaseSamplers} / {L("Optional", "Optional")}: {samplerBudget.OptionalSamplers}",
                EditorStyles.miniLabel);
            if (samplerBudget.ExtraPassCount > 0)
            {
                EditorGUILayout.LabelField(
                    $"{L("Extra Pass", "Extra Pass")}: +{samplerBudget.ExtraPassCount} ({L("Screen Edge Split", "Screen Edge Split")})",
                    EditorStyles.miniLabel);
            }

            string contributorSummary = GetContributorSummary(samplerBudget, 4);
            if (!string.IsNullOrEmpty(contributorSummary))
            {
                EditorGUILayout.LabelField(
                    $"{L("Top Contributors", "Top Contributors")}: {contributorSummary}",
                    EditorStyles.wordWrappedMiniLabel);
            }

            if (samplerBudget.IsOverLimit)
            {
                EditorGUILayout.HelpBox(
                    L($"Estimated sampler usage is over the limit ({samplerBudget.EstimatedSamplers}/{samplerBudget.Limit}). The current look is preserved, but new heavy features cannot be enabled.", $"Estimated sampler usage is over the limit ({samplerBudget.EstimatedSamplers}/{samplerBudget.Limit}). The current look is preserved, but new heavy features cannot be enabled."),
                    MessageType.Warning);

                // Specific reduction suggestions based on top contributors
                if (samplerBudget.Contributors != null && samplerBudget.Contributors.Length > 0)
                {
                    int overBy = samplerBudget.EstimatedSamplers - samplerBudget.Limit;
                    EditorGUILayout.LabelField(
                        L($"💡 削減の提案 (あと {overBy} sampler 減らす必要があります):",
                          $"💡 Suggestions to reduce (need to free {overBy} sampler(s)):"),
                        EditorStyles.miniBoldLabel);
                    foreach (var contributor in samplerBudget.Contributors)
                    {
                        string name = NataneToonSamplerBudgetEstimator.GetDisplayName(contributor);
                        // "__" 始まりは共有コスト/リザーブの擬似エントリで、単体ではOFFにできない
                        bool isToggleable = material != null && !contributor.Keyword.StartsWith("__");

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(
                            L($"  • {name} を OFF → -{contributor.SamplerCost} samplers",
                              $"  • Turn off {name} → -{contributor.SamplerCost} samplers"),
                            EditorStyles.miniLabel);
                        if (isToggleable && GUILayout.Button(
                                new GUIContent(
                                    L("OFFにする", "Turn Off"),
                                    L($"{name} を無効化してサンプラーを {contributor.SamplerCost} 節約します",
                                      $"Disable {name} to free {contributor.SamplerCost} sampler(s)")),
                                EditorStyles.miniButton, GUILayout.Width(70)))
                        {
                            DisableFeatureKeyword(material, contributor.Keyword);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            else if (samplerBudget.IsNearLimit)
            {
                EditorGUILayout.HelpBox(
                    L($"Estimated sampler usage is close to the limit ({samplerBudget.EstimatedSamplers}/{samplerBudget.Limit}). Review the current setup before enabling more heavy features.", $"Estimated sampler usage is close to the limit ({samplerBudget.EstimatedSamplers}/{samplerBudget.Limit}). Review the current setup before enabling more heavy features."),
                    MessageType.Warning);
            }
            else if (samplerBudget.IsWarning)
            {
                EditorGUILayout.HelpBox(
                    L($"Estimated sampler usage is in the caution range ({samplerBudget.EstimatedSamplers}/{samplerBudget.Limit}). Be careful when adding features such as Light Volume or LTCGI.", $"Estimated sampler usage is in the caution range ({samplerBudget.EstimatedSamplers}/{samplerBudget.Limit}). Be careful when adding features such as Light Volume or LTCGI."),
                    MessageType.Info);
            }

            if (samplerBudget.HasCriticalLightingCombo)
            {
                EditorGUILayout.HelpBox(
                    L("Light Volume + LTCGI + Hatching is a high-risk sampler combination. Be especially careful on D3D11.", "Light Volume + LTCGI + Hatching is a high-risk sampler combination. Be especially careful on D3D11."),
                    MessageType.Warning);
            }
            else if (samplerBudget.HasScreenSpaceLightingCombo)
            {
                EditorGUILayout.HelpBox(
                    L("Light Volume + LTCGI + Screen Edge can hit the sampler limit quickly. Review the setup before layering more texture-heavy effects.", "Light Volume + LTCGI + Screen Edge can hit the sampler limit quickly. Review the setup before layering more texture-heavy effects."),
                    MessageType.Warning);
            }
            else if (samplerBudget.HasLightVolumeLtcgiCombo)
            {
                EditorGUILayout.HelpBox(
                    L("Using Light Volume and LTCGI together can quickly approach the sampler limit. Be careful when combining them with other heavy features.", "Using Light Volume and LTCGI together can quickly approach the sampler limit. Be careful when combining them with other heavy features."),
                    MessageType.Info);
            }

            if (samplerBudget.UsesScreenEdgeSplitVariant)
            {
                EditorGUILayout.HelpBox(
                    L("This material uses the Screen Edge split variant. Sampler pressure is lower, but the extra pass adds one draw call.", "This material uses the Screen Edge split variant. Sampler pressure is lower, but the extra pass adds one draw call."),
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawSamplerBudgetBar(NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate estimate)
        {
            Rect rect = GUILayoutUtility.GetRect(18, 18f);
            float fill = Mathf.Clamp01((float)estimate.EstimatedSamplers / estimate.Limit);
            Color color = GetSamplerBudgetColor(estimate);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.45f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * fill, rect.height), color);
            }

            EditorGUI.ProgressBar(
                rect,
                fill,
                $"{estimate.EstimatedSamplers} / {estimate.Limit} ({GetSamplerBudgetStatus(estimate)})");
        }

        private static Color GetSamplerBudgetColor(NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate estimate)
        {
            if (estimate.IsOverLimit) return new Color(0.86f, 0.42f, 0.18f);
            if (estimate.IsNearLimit) return new Color(0.95f, 0.65f, 0.2f);
            if (estimate.IsWarning) return new Color(0.95f, 0.82f, 0.24f);
            return new Color(0.32f, 0.78f, 0.44f);
        }

        /// <summary>
        /// 超過提案の「OFFにする」ボタン用: 機能キーワードをUndo付きで無効化する。
        /// インスペクタートグルと状態が食い違わないよう、対応するToggleプロパティも0にする。
        /// </summary>
        private static void DisableFeatureKeyword(Material material, string keyword)
        {
            if (material == null || string.IsNullOrEmpty(keyword))
            {
                return;
            }

            Undo.RecordObject(material, L("サンプラー削減: 機能をOFF", "Sampler Reduction: Turn Off Feature"));
            material.DisableKeyword(keyword);
            foreach (var mapping in NataneShaderKeywordSynchronizer.KeywordMappings)
            {
                if (mapping.keyword == keyword && material.HasProperty(mapping.propertyName))
                {
                    material.SetFloat(mapping.propertyName, 0f);
                    break;
                }
            }
            EditorUtility.SetDirty(material);
        }

        private static string GetSamplerBudgetStatus(NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate estimate)
        {
            if (estimate.IsOverLimit) return L("Over Limit", "Over Limit");
            if (estimate.IsNearLimit) return L("Near Limit", "Near Limit");
            if (estimate.IsWarning) return L("Caution", "Caution");
            return L("Safe", "Safe");
        }

        private static string GetContributorSummary(NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate estimate, int maxCount)
        {
            if (estimate.Contributors == null || estimate.Contributors.Length == 0 || maxCount <= 0)
            {
                return string.Empty;
            }

            int count = Mathf.Min(maxCount, estimate.Contributors.Length);
            string[] parts = new string[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = NataneToonSamplerBudgetEstimator.FormatContributor(estimate.Contributors[i]);
            }

            return string.Join(" / ", parts);
        }

        /// <summary>
        /// Draw unified tool header with help button
        /// </summary>
        public static void DrawToolHeader(string titleJP, string titleEN, string toolKey)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField(L(titleJP, titleEN), titleStyle);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            DrawLanguageToggleButton();
            DrawHelpButton(toolKey);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Draw JP/EN language toggle button
        /// </summary>
        public static void DrawLanguageToggleButton()
        {
            string label = NataneToonLocalization.IsJapanese ? "EN" : "JP";
            if (GUILayout.Button(label, GUILayout.Width(35), GUILayout.Height(25)))
            {
                NataneToonLocalization.ToggleLanguage();
            }
        }

        /// <summary>
        /// Draw primary action button
        /// </summary>
        public static bool DrawPrimaryButton(string label, float width = 150, float height = 30)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            return GUILayout.Button(label, style, GUILayout.Width(width), GUILayout.Height(height));
        }

        /// <summary>
        /// Draw secondary action button
        /// </summary>
        public static bool DrawSecondaryButton(string label, float width = 150, float height = 25)
        {
            return GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(height));
        }

        // ===== Inspector → Consolidated Window Links =====

        /// <summary>
        /// Draw a compact button that opens a consolidated tool window tab.
        /// Placed inside inspector sections to provide quick access to the full tool.
        /// </summary>
        public static void DrawOpenInStudioButton(string labelJP, string labelEN, System.Action onClick)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L(labelJP, labelEN), EditorStyles.miniButton, GUILayout.Width(160), GUILayout.Height(18)))
            {
                onClick?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draw foldout section with unified style
        /// </summary>
        public static bool DrawFoldoutSection(string title, bool foldout, System.Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            if (foldout)
            {
                EditorGUI.indentLevel++;
                content?.Invoke();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            return foldout;
        }

        /// <summary>
        /// Draw success message box
        /// </summary>
        public static void DrawSuccessBox(string message)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = NataneToonColorPalette.Success;
            EditorGUILayout.HelpBox(message, MessageType.Info);
            GUI.backgroundColor = oldColor;
        }

        /// <summary>
        /// Draw warning message box
        /// </summary>
        public static void DrawWarningBox(string message)
        {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        /// <summary>
        /// Draw error message box
        /// </summary>
        public static void DrawErrorBox(string message)
        {
            EditorGUILayout.HelpBox(message, MessageType.Error);
        }
    }
}
