using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// Auto Setup Hub — Stage 1 UI + pipeline integration + Stage 2 role-aware toggle support.
    /// Draws the auto-setup panel in the ShaderGUI and orchestrates the full setup pipeline.
    /// </summary>
    public static class NataneAutoSetupHub
    {
        private static readonly Dictionary<int, AutoSetupResult> LastResults = new Dictionary<int, AutoSetupResult>();

        // ===== Stage 1 UI =====

        /// <summary>
        /// Draw the Auto Setup panel (Role/Look selector + Execute button).
        /// Called from NataneToonShaderGUI.DrawQuickSetupSection.
        /// </summary>
        /// <param name="applyStyleCallback">Callback that applies the base style for the given look.
        /// Receives (Material, AutoSetupLook) so the correct style can be chosen.</param>
        public static void DrawAutoSetupPanel(Material mat, System.Action<Material, AutoSetupLook> applyStyleCallback)
        {
            if (mat == null) return;

            var record = AutoSetupRecord.Load(mat);
            string selectionPrefsKey = AutoSetupRecord.GetPrefsKey(mat) + "_Selection";

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    L("▶ 自動キャラクターセットアップ", "▶ Auto Character Setup"),
                    EditorStyles.boldLabel);

                EditorGUILayout.HelpBox(
                    L("素材の種類と見た目を選んでセットアップを実行すると、最適なシェーダー設定を自動適用します。Ctrl+Z で元に戻せます。",
                      "Select the material role and visual style, then execute setup to auto-apply optimal shader settings. Ctrl+Z to undo."),
                    MessageType.Info);

                EditorGUILayout.Space(4);

                // Role selector
                EditorGUILayout.LabelField(L("何を作る？", "What is this material for?"), EditorStyles.miniLabel);
                AutoSetupRole role = (AutoSetupRole)EditorPrefs.GetInt(
                    selectionPrefsKey + "_Role", (int)(record?.role ?? AutoSetupRole.Face));
                AutoSetupLook look = (AutoSetupLook)EditorPrefs.GetInt(
                    selectionPrefsKey + "_Look", (int)(record?.look ?? AutoSetupLook.GameCharacter));
                AutoSetupQuality quality = (AutoSetupQuality)EditorPrefs.GetInt(
                    selectionPrefsKey + "_Quality", (int)(record?.quality ?? AutoSetupQuality.Standard));

                EditorGUI.BeginChangeCheck();
                role = (AutoSetupRole)EditorGUILayout.EnumPopup(
                    L("素材の種類", "Material Role"), role);

                // Look selector
                EditorGUILayout.LabelField(L("どんな見た目？", "What visual style?"), EditorStyles.miniLabel);
                look = (AutoSetupLook)EditorGUILayout.EnumPopup(
                    L("見た目スタイル", "Visual Style"), look);

                quality = (AutoSetupQuality)EditorGUILayout.EnumPopup(
                    L("品質ターゲット", "Quality Target"), quality);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetInt(selectionPrefsKey + "_Role", (int)role);
                    EditorPrefs.SetInt(selectionPrefsKey + "_Look", (int)look);
                    EditorPrefs.SetInt(selectionPrefsKey + "_Quality", (int)quality);
                }

                DrawSetupPreview(mat, role, look, quality);

                EditorGUILayout.Space(8);

                // Execute button
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.5f);
                if (GUILayout.Button(
                    L("▶ セットアップ実行", "▶ Execute Setup"),
                    GUILayout.Height(30)))
                {
                    LastResults[mat.GetInstanceID()] = ExecuteSetup(mat, role, look, quality, applyStyleCallback);
                }
                GUI.backgroundColor = oldBg;
            }

            // Show existing record info
            if (record != null)
            {
                DrawRecordInfo(record);
            }

            if (LastResults.TryGetValue(mat.GetInstanceID(), out AutoSetupResult lastResult))
                NataneAutoSetupReport.DrawResultSummary(lastResult);
        }

        private static void DrawSetupPreview(Material mat, AutoSetupRole role, AutoSetupLook look, AutoSetupQuality quality)
        {
            AutoSetupProfile profile = NataneAutoSetupProfiles.Get(role, look);
            var changes = new List<string>();

            if (profile.enableKeywords != null)
            {
                foreach (string keyword in profile.enableKeywords)
                {
                    if (!mat.IsKeywordEnabled(keyword))
                        changes.Add("+ " + keyword.TrimStart('_').Replace('_', ' '));
                }
            }

            if (profile.floatOverrides != null)
            {
                foreach (var item in profile.floatOverrides)
                {
                    if (!mat.HasProperty(item.name)) continue;
                    float current = mat.GetFloat(item.name);
                    if (!Mathf.Approximately(current, item.value))
                        changes.Add($"{item.name}: {current:0.##} → {item.value:0.##}");
                }
            }

            string heading = $"{NataneAutoSetupReport.GetRoleName(role)}  ×  {NataneAutoSetupReport.GetLookName(look)}  ·  {NataneAutoSetupReport.GetQualityName(quality)}";
            string summary = changes.Count == 0
                ? L("現在の設定は選択したプロファイルと一致しています。", "The current settings already match this profile.")
                : string.Join("\n", changes.GetRange(0, Mathf.Min(changes.Count, 5)).ToArray())
                    + (changes.Count > 5 ? L($"\n他 {changes.Count - 5} 件", $"\n{changes.Count - 5} more") : string.Empty);

            NataneToonInspectorComponents.DrawInlineMessage(
                heading + "\n" + summary,
                changes.Count == 0 ? NataneInspectorStatus.Success : NataneInspectorStatus.Neutral);
        }

        /// <summary>
        /// Draw a compact info box about the current setup record.
        /// </summary>
        private static void DrawRecordInfo(AutoSetupRecord record)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    L("現在の設定: ", "Current: ") +
                    $"{NataneAutoSetupReport.GetRoleName(record.role)} × {NataneAutoSetupReport.GetLookName(record.look)}",
                    EditorStyles.miniLabel);
            }
        }

        // ===== Pipeline =====

        /// <summary>
        /// Execute the full Stage 1 auto-setup pipeline.
        /// </summary>
        public static AutoSetupResult ExecuteSetup(
            Material mat,
            AutoSetupRole role,
            AutoSetupLook look,
            AutoSetupQuality quality,
            System.Action<Material, AutoSetupLook> applyStyleCallback)
        {
            var result = new AutoSetupResult
            {
                role = role,
                look = look,
                quality = quality,
            };

            if (mat == null)
            {
                result.success = false;
                return result;
            }

            // 1. Undo group
            Undo.RecordObject(mat, L("自動キャラクターセットアップ", "Auto Character Setup"));
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                // 2. Get profile
                var profile = NataneAutoSetupProfiles.Get(role, look);

                // 3. Apply base style via existing ApplyXxxStyle methods
                applyStyleCallback?.Invoke(mat, look);

                // 4. Create setup record
                var record = new AutoSetupRecord
                {
                    role = role,
                    look = look,
                    quality = quality,
                };

                // 5. Apply profile keywords
                if (profile.enableKeywords != null)
                {
                    foreach (string keyword in profile.enableKeywords)
                    {
                        string propName = KeywordToPropertyName(keyword);
                        if (propName != null && mat.HasProperty(propName))
                        {
                            mat.SetFloat(propName, 1f);
                            mat.EnableKeyword(keyword);
                            record.stage1EnabledKeywords.Add(keyword);
                            result.enabledFeatures.Add(keyword);
                        }
                    }
                }

                // 6. Apply float overrides
                if (profile.floatOverrides != null)
                {
                    foreach (var (name, value) in profile.floatOverrides)
                    {
                        if (mat.HasProperty(name))
                        {
                            mat.SetFloat(name, value);
                            record.RecordAutoFloat(name, value);
                        }
                    }
                }

                // 7. Apply color overrides
                if (profile.colorOverrides != null)
                {
                    foreach (var (name, color) in profile.colorOverrides)
                    {
                        if (mat.HasProperty(name))
                            mat.SetColor(name, color);
                    }
                }

                // 8. Texture analysis → auto colors
                result.colorResult = NataneTextureAnalyzer.AnalyzeAndApplyColors(mat, role, record);

                // 9. Mesh + UV analysis → auto parameters
                Mesh mesh = NataneMeshAnalyzer.FindMeshForMaterial(mat);
                if (mesh != null)
                {
                    // Use texture-aware overload for accurate texel density
                    var mainTex = mat.HasProperty("_MainTex")
                        ? mat.GetTexture("_MainTex") as Texture2D : null;
                    result.meshResult = mainTex != null
                        ? NataneMeshAnalyzer.Analyze(mesh, role, mainTex)
                        : NataneMeshAnalyzer.Analyze(mesh, role);

                    if (result.meshResult.valid)
                    {
                        // Apply mesh-derived parameters if profile didn't set them
                        if (!HasOverride(profile, "_OutlineWidth") && mat.HasProperty("_OutlineWidth")
                            && IsFeatureEnabled(mat, "_Outline"))
                        {
                            mat.SetFloat("_OutlineWidth", result.meshResult.outlineWidth);
                            record.RecordAutoFloat("_OutlineWidth", result.meshResult.outlineWidth);
                        }

                        if (!HasOverride(profile, "_NormalFlattenY") && mat.HasProperty("_NormalFlattenY"))
                        {
                            mat.SetFloat("_NormalFlattenY", result.meshResult.normalFlattenY);
                            record.RecordAutoFloat("_NormalFlattenY", result.meshResult.normalFlattenY);
                        }

                        // 9b. Apply UV-derived parameters
                        var uv = result.meshResult.uvResult;
                        if (uv.valid)
                        {
                            ApplyUVDerivedParameters(mat, uv, profile, record);
                        }
                    }
                }

                // 10. Supplementary data generation
                if (profile.generateSDF)
                {
                    if (NataneToonSdfAutoGenerator.TryGenerateAndAssign(mat, out string sdfMsg))
                    {
                        result.sdfGenerated = true;
                    }
                    else
                    {
                        result.warnings.Add(sdfMsg);
                    }
                }

                if (profile.bakeSmoothNormals && mesh != null)
                {
                    if (NataneMeshAnalyzer.NeedsSmoothNormals(mesh))
                    {
                        // SmoothNormalBaker lives in NataneToon.Editor.Tools assembly.
                        // Flag for the user rather than calling across assembly boundaries.
                        result.smoothNormalsBaked = false;
                        result.warnings.Add(L(
                            "このメッシュにはスムース法線ベイクが推奨されます。\nTools > Natane > スムース法線ベイク を実行してください。",
                            "Smooth normal baking is recommended for this mesh.\nRun Tools > Natane > Smooth Normal Baker."));
                    }
                }

                // 11. Cross-feature resolution
                NataneCrossFeatureResolver.Resolve(mat);

                // 12. Artifact detection & fix
                NataneCrossFeatureResolver.DetectAndFixArtifacts(mat);

                // 13. Keyword synchronization
                NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(mat);

                // 14. Performance metrics
                result.samplerCount = NataneAutoSetupReport.CountSamplers(mat);
                result.performanceRating = NataneAutoSetupReport.CalculatePerformanceRating(mat);

                // 15. Save record
                record.Save(mat);

                // 16. Finalize
                EditorUtility.SetDirty(mat);
                Undo.SetCurrentGroupName(L("自動キャラクターセットアップ", "Auto Character Setup"));
                Undo.CollapseUndoOperations(undoGroup);

                result.success = true;
                return result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NataneAutoSetup] {e.Message}\n{e.StackTrace}");
                result.success = false;
                result.warnings.Add(e.Message);
                return result;
            }
        }

        // ===== Stage 2: Role-Aware Feature Toggle =====

        /// <summary>
        /// Called when a feature toggle is turned ON while a SetupRecord exists.
        /// Applies role-optimized parameters instead of generic defaults.
        /// Returns true if role-aware parameters were applied.
        /// </summary>
        public static bool ApplyRoleAwareFeatureEnable(Material mat, string keyword)
        {
            if (mat == null) return false;
            var record = AutoSetupRecord.Load(mat);
            if (record == null) return false;

            var preset = NataneFeaturePresetTable.Get(record.role, record.look, keyword);

            // Check if not recommended
            if (preset.notRecommended)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    L("非推奨機能", "Not Recommended"),
                    preset.notRecommendedReason ??
                    L("この機能はこの素材タイプには通常使用しません。有効にしますか？",
                      "This feature is not typically used for this material type. Enable anyway?"),
                    L("有効にする", "Enable"),
                    L("キャンセル", "Cancel"));

                if (!proceed) return false;
            }

            // Apply optimal parameters
            if (preset.floatProperties != null)
            {
                foreach (var (name, value) in preset.floatProperties)
                {
                    if (mat.HasProperty(name))
                    {
                        mat.SetFloat(name, value);
                        record.RecordAutoFloat(name, value);
                    }
                }
            }

            if (preset.colorProperties != null)
            {
                foreach (var (name, color) in preset.colorProperties)
                {
                    if (mat.HasProperty(name))
                        mat.SetColor(name, color);
                }
            }

            // Supplementary data
            if (preset.requiresSDF)
            {
                if (!HasTexture(mat, "_SDFMap"))
                    NataneToonSdfAutoGenerator.TryGenerateAndAssign(mat, out _);
            }

            if (preset.requiresSmoothNormal)
            {
                Mesh mesh = NataneMeshAnalyzer.FindMeshForMaterial(mat);
                if (mesh != null && NataneMeshAnalyzer.NeedsSmoothNormals(mesh))
                {
                    Debug.Log(NataneToonLocalization.L(
                        "[NataneAutoSetup] スムース法線ベイクが推奨されます。Tools > Natane > スムース法線ベイク を実行してください。",
                        "[NataneAutoSetup] Smooth normal baking is recommended. Run Tools > Natane > Smooth Normal Baker."));
                }
            }

            // Track in record
            if (!record.stage2EnabledKeywords.Contains(keyword))
                record.stage2EnabledKeywords.Add(keyword);
            record.Save(mat);

            return true;
        }

        /// <summary>
        /// Get the current performance rating string for display.
        /// </summary>
        public static string GetPerformanceDisplay(Material mat)
        {
            if (mat == null) return "";
            string rating = NataneAutoSetupReport.CalculatePerformanceRating(mat);
            int samplers = NataneAutoSetupReport.CountSamplers(mat);
            return $"{L("パフォーマンス", "Performance")}: {rating}  |  {L("サンプラー", "Samplers")}: {samplers}/18";
        }

        // ===== UV-derived parameter application =====

        /// <summary>
        /// Apply shader parameters derived from UV analysis (texel density, seams, channels).
        /// Only sets properties that weren't already overridden by the profile.
        /// </summary>
        private static void ApplyUVDerivedParameters(
            Material mat,
            NataneMeshAnalyzer.UVAnalysisResult uv,
            AutoSetupProfile profile,
            AutoSetupRecord record)
        {
            // Texel density → BumpScale (if NormalMap is enabled)
            if (IsFeatureEnabled(mat, "_UseNormalMap") && !HasOverride(profile, "_BumpScale")
                && mat.HasProperty("_BumpScale"))
            {
                mat.SetFloat("_BumpScale", uv.recommendedBumpScale);
                record.RecordAutoFloat("_BumpScale", uv.recommendedBumpScale);
            }

            // Texel density → MicroNormal parameters
            if (!HasOverride(profile, "_MicroNormalTiling") && mat.HasProperty("_MicroNormalTiling"))
            {
                mat.SetFloat("_MicroNormalTiling", uv.recommendedMicroNormalTiling);
                record.RecordAutoFloat("_MicroNormalTiling", uv.recommendedMicroNormalTiling);
            }
            if (!HasOverride(profile, "_MicroNormalStrength") && mat.HasProperty("_MicroNormalStrength"))
            {
                mat.SetFloat("_MicroNormalStrength", uv.recommendedMicroNormalStrength);
                record.RecordAutoFloat("_MicroNormalStrength", uv.recommendedMicroNormalStrength);
            }

            // Texel density → Specular size (if Specular is enabled)
            if (IsFeatureEnabled(mat, "_Specular") && !HasOverride(profile, "_SpecularSize")
                && mat.HasProperty("_SpecularSize"))
            {
                mat.SetFloat("_SpecularSize", uv.recommendedSpecularSize);
                record.RecordAutoFloat("_SpecularSize", uv.recommendedSpecularSize);
            }

            // Vertex color detection → SmoothNormalMode
            if (mat.HasProperty("_SmoothNormalMode") && !HasOverride(profile, "_SmoothNormalMode"))
            {
                mat.SetFloat("_SmoothNormalMode", uv.recommendedSmoothNormalMode);
                record.RecordAutoFloat("_SmoothNormalMode", uv.recommendedSmoothNormalMode);
            }

            // UV1 detection → DetailUVSet recommendation
            if (uv.hasUV1 && mat.HasProperty("_DetailUVSet") && !HasOverride(profile, "_DetailUVSet"))
            {
                mat.SetFloat("_DetailUVSet", 1f);
                record.RecordAutoFloat("_DetailUVSet", 1f);
            }
        }

        // ===== Helpers =====

        private static string KeywordToPropertyName(string keyword)
        {
            switch (keyword)
            {
                case "_SSS": return "_SSS";
                case "_SDF_MAP": return "_UseSDFMap";
                case "_SPECULAR": return "_Specular";
                case "_HAIR_SPECULAR": return "_HairSpecular";
                case "_RIM_LIGHT": return "_RimLight";
                case "_OUTLINE": return "_Outline";
                case "_MAT_CAP": return "_MatCap";
                case "_EMISSION": return "_Emission";
                case "_REFLECTION": return "_Reflection";
                case "_PARALLAX": return "_Parallax";
                case "_USE_NORMAL_MAP": return "_UseNormalMap";
                case "_ENV_RIM": return "_EnvRim";
                case "_DITHERING": return "_UseDithering";
                default: return null;
            }
        }

        private static bool HasOverride(AutoSetupProfile profile, string propName)
        {
            if (profile.floatOverrides == null) return false;
            foreach (var (name, _) in profile.floatOverrides)
            {
                if (name == propName) return true;
            }
            return false;
        }

        private static bool IsFeatureEnabled(Material mat, string propName)
        {
            return mat.HasProperty(propName) && mat.GetFloat(propName) > 0.5f;
        }

        private static bool HasTexture(Material mat, string propName)
        {
            return mat.HasProperty(propName) && mat.GetTexture(propName) != null;
        }
    }
}
