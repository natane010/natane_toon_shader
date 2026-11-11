using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    using static NataneUIConstants;

    /// <summary>
    /// Material validation and optimization checker
    /// Provides VRChat optimization checks, performance ratings, and auto-fix suggestions
    /// </summary>
    public class MaterialValidator : EditorWindow
    {
        private const string NataneShaderPrefix = "Natane/Toon Shader";

        private static readonly string[] VrchatTextureProperties =
        {
            "_MainTex", "_BumpMap", "_MicroNormalMap", "_ClearCoatMask", "_ClearCoatNormalMap", "_CavityMap", "_TransmissionMask", "_SkinSpecMask", "_HairStrandDirectionMap", "_HairTransmissionMask", "_EmissionMap", "_MatCapTex", "_MatCapTex2", "_MatCapTex3", "_RampTex",
            "_DissolveTex", "_DissolveMap", "_ThicknessMap", "_SpecularMask", "_RimMask",
            "_SSSMask", "_MatCapMask", "_EmissionMask", "_DissolveMask",
            "_ReflectionMask", "_EnvRimMask", "_ParallaxMap", "_RefractionMask"
        };

        private static readonly string[] TextureSizeProperties =
        {
            "_MainTex", "_BumpMap", "_MicroNormalMap", "_ClearCoatMask", "_ClearCoatNormalMap", "_CavityMap", "_TransmissionMask", "_SkinSpecMask", "_HairStrandDirectionMap", "_HairTransmissionMask", "_EmissionMap", "_MatCapTex", "_MatCapTex2", "_MatCapTex3", "_RampTex", "_ParallaxMap"
        };

        // 実体は NatanePerformanceFeatureKeywords（上流の NataneToon.Editor 側）。
        // ここに private で置いていた間は監査から突合できず、v1.6/1.7 で追加された
        // 機能の登録漏れが検出されないままになっていた。
        private static string[] ActiveFeatureKeywords => NatanePerformanceFeatureKeywords.Counted;

        private static readonly string[] LookMixerNprKeywords =
        {
            "_COLOR_QUANTIZE",
            "_LUT_3D",
            "_HATCHING",
            "_WATERCOLOR",
            "_SOFT_FILTER",
            "_KUWAHARA_FILTER",
            "_SCREEN_EDGE",
            "_COLOR_BLEEDING",
            "_CHROMATIC_ABERRATION",
            "_OUTLINE_HAND_DRAWN"
        };

        private static readonly Dictionary<string, string[]> UnusedFeatureTextureRequirements = new Dictionary<string, string[]>
        {
            { "_MATCAP", new[] { "_MatCapTex" } },
            { "_MATCAP_2", new[] { "_MatCapTex2" } },
            { "_MATCAP_3", new[] { "_MatCapTex3" } },
            { "_NORMALMAP", new[] { "_BumpMap" } },
            { "_EMISSION", new[] { "_EmissionMap" } },
            { "_DISSOLVE", new[] { "_DissolveTex", "_DissolveMap" } },
            { "_SSS", new[] { "_ThicknessMap" } },
            { "_PARALLAX", new[] { "_ParallaxMap" } }
        };

        private Vector2 scrollPosition;
        private List<Material> materialsToValidate = new List<Material>();
        private List<ValidationResult> validationResults = new List<ValidationResult>();
        private bool autoFixAvailable = false;

        // Validation settings
        private bool checkVRChatOptimization = true;
        private bool checkTextureSize = true;
        private bool checkPerformance = true;
        private bool checkUnusedFeatures = true;
        private bool checkTextureCompression = true;

        private enum ValidationSeverity
        {
            Info,
            Warning,
            Error
        }

        private class ValidationResult
        {
            public Material material;
            public string issue;
            public ValidationSeverity severity;
            public string suggestion;
            public System.Action autoFixAction;
            public string category;
        }

        [MenuItem(NataneToolMenuPaths.MaterialValidator, false, 11)]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialValidator>(L("Material Validator", "Material Validator"));
            window.minSize = new Vector2(WINDOW_WIDTH_STANDARD, 400);
            window.Show();
        }

        private void OnGUI()
        {
            NataneToonInspectorComponents.DrawWindowBackground(position);
            DrawHeader();
            EditorGUILayout.Space(SPACE_SMALL);
            DrawMaterialSelection();
            EditorGUILayout.Space(SPACE_SMALL);
            DrawValidationSettings();
            EditorGUILayout.Space(SPACE_STANDARD);
            DrawValidationResults();
        }

        private void DrawHeader()
        {
            NataneToonShaderGUIUtility.DrawToolHeader("マテリアル検証&最適化", "Material Validator & Optimizer", "MaterialValidator");
            EditorGUILayout.HelpBox(
                L("Validates materials for VRChat optimization, performance issues, and common problems.\nAuto-fix suggestions provided where possible.", "Validates materials for VRChat optimization, performance issues, and common problems.\nAuto-fix suggestions provided where possible."),
                MessageType.Info);
        }

        private void DrawMaterialSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("Materials to Validate", "Materials to Validate"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(L("Add Selected Materials", "Add Selected Materials"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                AddSelectedMaterials();
            }

            if (GUILayout.Button(L("Add All Natane Toon Materials", "Add All Natane Toon Materials"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                AddAllNataneToonMaterials();
            }

            if (GUILayout.Button(L("Clear List", "Clear List"), GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                materialsToValidate.Clear();
                validationResults.Clear();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(SPACE_SMALL);

            // Display material list
            if (materialsToValidate.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("No materials selected. Use the buttons above to add materials.", "No materials selected. Use the buttons above to add materials."),
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField(L($"Selected Materials ({materialsToValidate.Count}):", $"Selected Materials ({materialsToValidate.Count}):"), EditorStyles.miniLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
                for (int i = materialsToValidate.Count - 1; i >= 0; i--)
                {
                    if (materialsToValidate[i] == null)
                    {
                        materialsToValidate.RemoveAt(i);
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(materialsToValidate[i], typeof(Material), false);
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        materialsToValidate.RemoveAt(i);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("Validation Settings", "Validation Settings"), EditorStyles.boldLabel);

            checkVRChatOptimization = EditorGUILayout.ToggleLeft(L("VRChat Optimization Check", "VRChat Optimization Check"), checkVRChatOptimization);
            checkTextureSize = EditorGUILayout.ToggleLeft(L("Texture Size Check", "Texture Size Check"), checkTextureSize);
            checkPerformance = EditorGUILayout.ToggleLeft(L("Performance Rating Check", "Performance Rating Check"), checkPerformance);
            checkUnusedFeatures = EditorGUILayout.ToggleLeft(L("Unused Features Check", "Unused Features Check"), checkUnusedFeatures);
            checkTextureCompression = EditorGUILayout.ToggleLeft(L("Texture Compression Check", "Texture Compression Check"), checkTextureCompression);

            EditorGUILayout.Space(SPACE_SMALL);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(L("Validate All", "Validate All"), GUILayout.Height(BUTTON_HEIGHT_LARGE)))
            {
                ValidateAllMaterials();
            }

            using (new EditorGUI.DisabledScope(!autoFixAvailable))
            {
                if (GUILayout.Button(L("Auto-Fix All Issues", "Auto-Fix All Issues"), GUILayout.Height(BUTTON_HEIGHT_LARGE)))
                {
                    AutoFixAllIssues();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationResults()
        {
            if (validationResults.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("Validation Results", "Validation Results"), EditorStyles.boldLabel);

            // Summary
            int errors = validationResults.Count(r => r.severity == ValidationSeverity.Error);
            int warnings = validationResults.Count(r => r.severity == ValidationSeverity.Warning);
            int infos = validationResults.Count(r => r.severity == ValidationSeverity.Info);

            EditorGUILayout.BeginHorizontal();
            GUI.color = errors > 0 ? Color.red : Color.white;
            EditorGUILayout.LabelField(L($"Errors: {errors}", $"Errors: {errors}"), EditorStyles.boldLabel, GUILayout.Width(150));
            GUI.color = warnings > 0 ? Color.yellow : Color.white;
            EditorGUILayout.LabelField(L($"Warnings: {warnings}", $"Warnings: {warnings}"), EditorStyles.boldLabel, GUILayout.Width(150));
            GUI.color = Color.white;
            EditorGUILayout.LabelField(L($"Info: {infos}", $"Info: {infos}"), EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(SPACE_SMALL);

            // Results list
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var result in validationResults)
            {
                DrawValidationResult(result);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawValidationResult(ValidationResult result)
        {
            Color bgColor = Color.white;
            switch (result.severity)
            {
                case ValidationSeverity.Error:
                    bgColor = new Color(1f, 0.5f, 0.5f, 0.3f);
                    break;
                case ValidationSeverity.Warning:
                    bgColor = new Color(1f, 1f, 0.5f, 0.3f);
                    break;
                case ValidationSeverity.Info:
                    bgColor = new Color(0.5f, 0.8f, 1f, 0.3f);
                    break;
            }

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // Header
            EditorGUILayout.BeginHorizontal();

            // Severity icon
            string icon = result.severity == ValidationSeverity.Error ? "!"
                         : result.severity == ValidationSeverity.Warning ? "!"
                         : "i";
            EditorGUILayout.LabelField(icon, GUILayout.Width(20));

            // Category and material
            EditorGUILayout.LabelField($"[{result.category}] {result.material.name}", EditorStyles.boldLabel);

            // Ping button
            if (GUILayout.Button("Ping", GUILayout.Width(40)))
            {
                EditorGUIUtility.PingObject(result.material);
                Selection.activeObject = result.material;
            }

            EditorGUILayout.EndHorizontal();

            // Issue description
            EditorGUILayout.LabelField(L("Issue:", "Issue:"), EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(result.issue, EditorStyles.wordWrappedLabel);

            // Suggestion
            if (!string.IsNullOrEmpty(result.suggestion))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(L("Suggestion:", "Suggestion:"), EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(result.suggestion, EditorStyles.wordWrappedLabel);
            }

            // Auto-fix button
            if (result.autoFixAction != null)
            {
                EditorGUILayout.Space(3);
                if (GUILayout.Button(L("Auto-Fix", "Auto-Fix"), GUILayout.Height(20)))
                {
                    result.autoFixAction.Invoke();
                    EditorUtility.DisplayDialog(
                        L("Auto-Fix Applied", "Auto-Fix Applied"),
                        L($"Fixed issue for material: {result.material.name}", $"Fixed issue for material: {result.material.name}"),
                        "OK");
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(SPACE_SMALL);
        }

        private void AddSelectedMaterials()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is Material material)
                {
                    if (!materialsToValidate.Contains(material))
                    {
                        materialsToValidate.Add(material);
                    }
                }
            }
        }

        private void AddAllNataneToonMaterials()
        {
            foreach (Material material in NataneMaterialAssetCache.GetMaterialsByShaderPrefix(NataneShaderPrefix))
            {
                if (material != null && !materialsToValidate.Contains(material))
                {
                    materialsToValidate.Add(material);
                }
            }

            Debug.Log($"[MaterialValidator] Found {materialsToValidate.Count} Natane Toon materials");
        }

        private void ValidateAllMaterials()
        {
            validationResults.Clear();
            autoFixAvailable = false;

            foreach (var material in materialsToValidate)
            {
                if (material == null) continue;

                if (checkVRChatOptimization) ValidateVRChatOptimization(material);
                if (checkTextureSize) ValidateTextureSize(material);
                if (checkPerformance) ValidatePerformance(material);
                if (checkUnusedFeatures) ValidateUnusedFeatures(material);
                if (checkTextureCompression) ValidateTextureCompression(material);
                ValidateLookMixer(material);
                ValidateDissolveToggleMigration(material);
                ValidateStencilPairing(material);
                ValidateShadowShapeRig(material);
            }

            autoFixAvailable = validationResults.Any(r => r.autoFixAction != null);

            Debug.Log($"[MaterialValidator] Validation complete. Found {validationResults.Count} issues.");
        }

        private void ValidateVRChatOptimization(Material material)
        {
            ValidateDependencyPackages(material);

            // Check texture memory usage
            long totalMemory = 0;
            var countedTextures = new HashSet<Texture2D>();

            foreach (string texProp in VrchatTextureProperties)
            {
                if (material.HasProperty(texProp))
                {
                    var tex = material.GetTexture(texProp) as Texture2D;
                    if (tex != null)
                    {
                        // Count each Texture2D once even if reused across multiple slots.
                        if (countedTextures.Add(tex))
                        {
                            long memory = CalculateTextureMemory(tex);
                            totalMemory += memory;
                        }

                        // Check individual texture size
                        if (tex.width > 2048 || tex.height > 2048)
                        {
                            validationResults.Add(new ValidationResult
                            {
                                material = material,
                                category = "VRChat",
                                severity = ValidationSeverity.Warning,
                                issue = $"Texture '{texProp}' is {tex.width}x{tex.height}, exceeds VRChat recommended 2048x2048",
                                suggestion = "Consider resizing to 2048x2048 or lower for better performance",
                                autoFixAction = null // Can't auto-resize textures safely
                            });
                        }
                    }
                }
            }

            // Total memory check (VRChat recommends < 40MB per material)
            if (totalMemory > 40 * 1024 * 1024)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "VRChat",
                    severity = ValidationSeverity.Error,
                    issue = $"Total texture memory: {totalMemory / (1024 * 1024)}MB exceeds VRChat recommended 40MB",
                    suggestion = "Reduce texture sizes or use texture atlases"
                });
            }
        }

        private void ValidateTextureSize(Material material)
        {
            foreach (string texProp in TextureSizeProperties)
            {
                if (material.HasProperty(texProp))
                {
                    var tex = material.GetTexture(texProp) as Texture2D;
                    if (tex != null)
                    {
                        // Check if power of 2
                        if (!IsPowerOfTwo(tex.width) || !IsPowerOfTwo(tex.height))
                        {
                            validationResults.Add(new ValidationResult
                            {
                                material = material,
                                category = "Texture",
                                severity = ValidationSeverity.Warning,
                                issue = $"Texture '{texProp}' ({tex.width}x{tex.height}) is not power-of-2",
                                suggestion = "Use power-of-2 dimensions (256, 512, 1024, 2048) for better GPU performance"
                            });
                        }

                        // Check for excessive size on simple textures
                        if (texProp == "_RampTex" && (tex.width > 256 || tex.height > 256))
                        {
                            validationResults.Add(new ValidationResult
                            {
                                material = material,
                                category = "Texture",
                                severity = ValidationSeverity.Info,
                                issue = $"Ramp texture is {tex.width}x{tex.height}, unnecessarily large",
                                suggestion = "Ramp textures typically only need to be 256x16 or smaller"
                            });
                        }
                    }
                }
            }
        }

        private void ValidatePerformance(Material material)
        {
            int activeFeatures = CountActiveFeatures(material);
            NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate samplerEstimate =
                NataneToonSamplerBudgetEstimator.Estimate(material);

            string samplerSummary = BuildSamplerContributorSummary(samplerEstimate, 4);

            if (samplerEstimate.IsOverLimit)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Performance",
                    severity = ValidationSeverity.Error,
                    issue = L($"Estimated sampler usage exceeds the limit ({samplerEstimate.EstimatedSamplers}/{samplerEstimate.Limit})", $"Estimated sampler usage exceeds the limit ({samplerEstimate.EstimatedSamplers}/{samplerEstimate.Limit})"),
                    suggestion = L($"Disable heavy features to get back under the limit. Main contributors: {samplerSummary}", $"Disable heavy features to get back under the limit. Main contributors: {samplerSummary}")
                });
            }
            else if (samplerEstimate.HasCriticalLightingCombo)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Performance",
                    severity = ValidationSeverity.Warning,
                    issue = L("Light Volume + LTCGI + Hatching is a high-risk sampler combination", "Light Volume + LTCGI + Hatching is a high-risk sampler combination"),
                    suggestion = L("Review Hatching or third-party lighting before adding more heavy texture features.", "Review Hatching or third-party lighting before adding more heavy texture features.")
                });
            }
            else if (samplerEstimate.HasScreenSpaceLightingCombo)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Performance",
                    severity = ValidationSeverity.Warning,
                    issue = L("Light Volume + LTCGI + Screen Edge is a high-risk sampler combination", "Light Volume + LTCGI + Screen Edge is a high-risk sampler combination"),
                    suggestion = L($"Review Screen Edge or third-party lighting. Main contributors: {samplerSummary}", $"Review Screen Edge or third-party lighting. Main contributors: {samplerSummary}")
                });
            }
            else if (samplerEstimate.HasLightVolumeLtcgiCombo)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Performance",
                    severity = ValidationSeverity.Warning,
                    issue = L($"Light Volume and LTCGI are enabled together (estimated {samplerEstimate.EstimatedSamplers}/{samplerEstimate.Limit})", $"Light Volume and LTCGI are enabled together (estimated {samplerEstimate.EstimatedSamplers}/{samplerEstimate.Limit})"),
                    suggestion = L($"Check sampler headroom before enabling more heavy features. Main contributors: {samplerSummary}", $"Check sampler headroom before enabling more heavy features. Main contributors: {samplerSummary}")
                });
            }
            else if (samplerEstimate.IsNearLimit)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Performance",
                    severity = ValidationSeverity.Warning,
                    issue = L($"Estimated sampler usage is near the limit ({samplerEstimate.EstimatedSamplers}/{samplerEstimate.Limit})", $"Estimated sampler usage is near the limit ({samplerEstimate.EstimatedSamplers}/{samplerEstimate.Limit})"),
                    suggestion = L($"Review the current setup before enabling more heavy features. Main contributors: {samplerSummary}", $"Review the current setup before enabling more heavy features. Main contributors: {samplerSummary}")
                });
            }
            else if (samplerEstimate.IsWarning)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Performance",
                    severity = ValidationSeverity.Info,
                    issue = L($"Estimated sampler usage is in the caution range ({samplerEstimate.EstimatedSamplers}/{samplerEstimate.Limit})", $"Estimated sampler usage is in the caution range ({samplerEstimate.EstimatedSamplers}/{samplerEstimate.Limit})"),
                    suggestion = L($"Check the current setup before enabling Light Volume or LTCGI. Main contributors: {samplerSummary}", $"Check the current setup before enabling Light Volume or LTCGI. Main contributors: {samplerSummary}")
                });
            }

            if (activeFeatures > 12)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Performance",
                    severity = ValidationSeverity.Warning,
                    issue = L($"{activeFeatures} features are enabled. GPU cost is heavy (Rating: D)", $"{activeFeatures} features are enabled. GPU cost is heavy (Rating: D)"),
                    suggestion = L("Disable unused features and simplify the setup for Quest or VR targets.", "Disable unused features and simplify the setup for Quest or VR targets.")
                });
            }
            else if (activeFeatures > 8 && !samplerEstimate.IsWarning)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Performance",
                    severity = ValidationSeverity.Info,
                    issue = L($"{activeFeatures} features are enabled. GPU cost is moderate (Rating: C)", $"{activeFeatures} features are enabled. GPU cost is moderate (Rating: C)"),
                    suggestion = L("Disabling less important features will add headroom for mobile or VR.", "Disabling less important features will add headroom for mobile or VR.")
                });
            }
        }

        private void ValidateUnusedFeatures(Material material)
        {
            foreach (KeyValuePair<string, string[]> check in UnusedFeatureTextureRequirements)
            {
                if (material.IsKeywordEnabled(check.Key))
                {
                    bool hasTexture = false;
                    foreach (var texProp in check.Value)
                    {
                        if (material.HasProperty(texProp) && material.GetTexture(texProp) != null)
                        {
                            hasTexture = true;
                            break;
                        }
                    }

                    if (!hasTexture)
                    {
                        string keyword = check.Key;
                        validationResults.Add(new ValidationResult
                        {
                            material = material,
                            category = "Unused Feature",
                            severity = ValidationSeverity.Warning,
                            issue = $"Feature '{keyword}' is enabled but no texture assigned",
                            suggestion = $"Either assign a texture or disable the feature to save performance",
                            autoFixAction = () =>
                            {
                                material.DisableKeyword(keyword);
                                EditorUtility.SetDirty(material);
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// v1.7 で「効いていなかったトグル」を実際に効かせた 2 件（NPR2026 F3 / F4）の移行検出。
        ///
        /// 以前はどちらもシェーダー側が無視していたため、トグルが OFF のままでも
        /// マスク／AudioLink ディゾルブは動いていた。今はトグルが実際のゲートなので、
        /// 「設定はしてあるがトグルが OFF」のマテリアルは見た目が変わる。
        /// 該当するものを検出し、ワンクリックで元の見た目へ戻せるようにする。
        /// </summary>
        private void ValidateDissolveToggleMigration(Material material)
        {
            // F3: マスクを割り当てているのに _UseDissolveMask が OFF
            if (material.HasProperty("_UseDissolveMask") &&
                material.HasProperty("_DissolveMask") &&
                material.GetFloat("_UseDissolveMask") < 0.5f &&
                material.GetTexture("_DissolveMask") != null)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Migration",
                    severity = ValidationSeverity.Warning,
                    issue = "ディゾルブマスクが割り当てられていますが「ディゾルブマスクを使う」が OFF です",
                    suggestion = "v1.7 でこのトグルが実際のゲートになりました。以前の見た目を保つには ON にしてください",
                    autoFixAction = () =>
                    {
                        Undo.RecordObject(material, "Enable Dissolve Mask");
                        material.SetFloat("_UseDissolveMask", 1f);
                        EditorUtility.SetDirty(material);
                    }
                });
            }

            // F4: AudioLink ディゾルブ強度を設定しているのに _AudioLinkDissolve が OFF
            if (material.HasProperty("_AudioLinkDissolve") &&
                material.HasProperty("_AudioLinkDissolveIntensity") &&
                material.GetFloat("_AudioLinkDissolve") < 0.5f &&
                material.GetFloat("_AudioLinkDissolveIntensity") > 0.001f)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Migration",
                    severity = ValidationSeverity.Warning,
                    issue = "AudioLink ディゾルブ強度が設定されていますが「ディゾルブ連動を有効化」が OFF です",
                    suggestion = "v1.7 でこのトグルが実際のゲートになりました。以前の見た目を保つには ON にしてください",
                    autoFixAction = () =>
                    {
                        Undo.RecordObject(material, "Enable AudioLink Dissolve");
                        material.SetFloat("_AudioLinkDissolve", 1f);
                        EditorUtility.SetDirty(material);
                    }
                });
            }
        }

        /// <summary>
        /// ステンシルの Writer / Cutter の片方だけが存在する状態を検出する。
        ///
        /// See Through Hair 系のセットアップは 2 つのマテリアルが対で初めて意味を持つ。
        /// Writer だけなら「眉が手前に描かれるだけ」、Cutter だけなら「くり抜く領域が無い」で、
        /// どちらも見た目上は何も起きない。原因が分かりにくいので明示的に検出する。
        /// </summary>
        private void ValidateStencilPairing(Material material)
        {
            if (!material.HasProperty("_StencilRef") ||
                !material.HasProperty("_StencilComp") ||
                !material.HasProperty("_StencilOp"))
            {
                return;
            }

            int reference = Mathf.RoundToInt(material.GetFloat("_StencilRef"));
            if (reference <= 0) return;   // ステンシル未使用

            int comp = Mathf.RoundToInt(material.GetFloat("_StencilComp"));
            int op = Mathf.RoundToInt(material.GetFloat("_StencilOp"));

            bool isWriter = comp == (int)UnityEngine.Rendering.CompareFunction.Always &&
                            op == (int)UnityEngine.Rendering.StencilOp.Replace;
            bool isCutter = comp == (int)UnityEngine.Rendering.CompareFunction.NotEqual ||
                            comp == (int)UnityEngine.Rendering.CompareFunction.Equal;

            if (!isWriter && !isCutter) return;

            // 同じ参照値を持つ相方が、検証対象のマテリアル群の中に居るかを見る。
            bool hasCounterpart = false;
            foreach (Material other in materialsToValidate)
            {
                if (other == null || other == material) continue;
                if (!other.HasProperty("_StencilRef") || !other.HasProperty("_StencilComp")) continue;
                if (Mathf.RoundToInt(other.GetFloat("_StencilRef")) != reference) continue;

                int otherComp = Mathf.RoundToInt(other.GetFloat("_StencilComp"));
                int otherOp = other.HasProperty("_StencilOp")
                    ? Mathf.RoundToInt(other.GetFloat("_StencilOp"))
                    : (int)UnityEngine.Rendering.StencilOp.Keep;

                bool otherIsWriter = otherComp == (int)UnityEngine.Rendering.CompareFunction.Always &&
                                     otherOp == (int)UnityEngine.Rendering.StencilOp.Replace;
                bool otherIsCutter = otherComp == (int)UnityEngine.Rendering.CompareFunction.NotEqual ||
                                     otherComp == (int)UnityEngine.Rendering.CompareFunction.Equal;

                if ((isWriter && otherIsCutter) || (isCutter && otherIsWriter))
                {
                    hasCounterpart = true;
                    break;
                }
            }

            if (hasCounterpart) return;

            validationResults.Add(new ValidationResult
            {
                material = material,
                category = "Stencil",
                severity = ValidationSeverity.Warning,
                issue = isWriter
                    ? $"ステンシル Writer（参照値 {reference}）に対応する Cutter が見つかりません"
                    : $"ステンシル Cutter（参照値 {reference}）に対応する Writer が見つかりません",
                suggestion = "Writer（眉・目）と Cutter（前髪）は対で初めて機能します。" +
                             "ステンシルプリセットツールで両方に同じ参照値を設定してください"
            });
        }

        /// <summary>
        /// Shadow Shape Rig（NPR2026 P1）の使い方の検出。
        ///
        /// 1. 有効なのに全スロットの半径が 0 → 何も起きない。設定したつもりの取りこぼし。
        /// 2. _SHADOW_EDGE_NOISE との併用 → 影の境界を 2 系統が同時に変形するため、
        ///    どちらの効果を見ているのか分からなくなる。
        /// </summary>
        private void ValidateShadowShapeRig(Material material)
        {
            if (!material.HasProperty("_ShadowShapeRig")) return;
            if (!material.IsKeywordEnabled("_SHADOW_SHAPE_RIG")) return;

            bool anySlotActive = false;
            for (int slot = 0; slot < 4; slot++)
            {
                string prop = "_ShadowRigParams" + slot;
                if (!material.HasProperty(prop)) continue;

                Vector4 p = material.GetVector(prop);
                if (p.z > 0.00001f && p.w > 0.00001f)
                {
                    anySlotActive = true;
                    break;
                }
            }

            if (!anySlotActive)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Unused Feature",
                    severity = ValidationSeverity.Warning,
                    issue = "影シェイプリグが有効ですが、全スロットの半径が 0 で何も起きていません",
                    suggestion = "スロットの半径を設定するか、機能を無効化してください",
                    autoFixAction = () =>
                    {
                        Undo.RecordObject(material, "Disable Shadow Shape Rig");
                        material.SetFloat("_ShadowShapeRig", 0f);
                        material.DisableKeyword("_SHADOW_SHAPE_RIG");
                        EditorUtility.SetDirty(material);
                    }
                });
                return;
            }

            if (material.IsKeywordEnabled("_SHADOW_EDGE_NOISE"))
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Combination",
                    severity = ValidationSeverity.Info,
                    issue = "影シェイプリグと影エッジノイズを併用しています",
                    suggestion = "どちらも影の境界を変形させるため効果が重なります。" +
                                 "狙った形にならない場合は片方を切って確認してください"
                });
            }
        }

        private void ValidateLookMixer(Material material)
        {
            if (!material.HasProperty("_LookMode") ||
                !material.HasProperty("_ToonWeight") ||
                !material.HasProperty("_NprWeight") ||
                !material.HasProperty("_PbrWeight") ||
                material.GetFloat("_LookMode") <= 0.5f)
            {
                return;
            }

            float toonWeight = material.GetFloat("_ToonWeight");
            float nprWeight = material.GetFloat("_NprWeight");
            float pbrWeight = material.GetFloat("_PbrWeight");

            if (toonWeight <= 0.01f && pbrWeight <= 0.01f)
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Look Mixer",
                    severity = ValidationSeverity.Warning,
                    issue = L("Both Toon Weight and PBR Weight are near zero. The base shading may become weaker than intended.", "Both Toon Weight and PBR Weight are near zero. The base shading may become weaker than intended."),
                    suggestion = L("Raise either Toon or PBR a bit so the material keeps a clear base response.", "Raise either Toon or PBR a bit so the material keeps a clear base response.")
                });
            }

            if (pbrWeight > 0.6f)
            {
                float smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0f;
                float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                bool hasReflection = material.IsKeywordEnabled("_REFLECTION") || material.IsKeywordEnabled("_PBR");

                if (smoothness < 0.15f && metallic < 0.1f && !hasReflection)
                {
                    validationResults.Add(new ValidationResult
                    {
                        material = material,
                        category = "Look Mixer",
                        severity = ValidationSeverity.Warning,
                        issue = L("PBR Weight is high, but smoothness / metallic / reflection are still very weak.", "PBR Weight is high, but smoothness / metallic / reflection are still very weak."),
                        suggestion = L("Review Smoothness, Metallic, and Reflection so the PBR-heavy look reads more clearly.", "Review Smoothness, Metallic, and Reflection so the PBR-heavy look reads more clearly.")
                    });
                }
            }

            if (nprWeight > 0.5f && !LookMixerNprKeywords.Any(material.IsKeywordEnabled))
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "Look Mixer",
                    severity = ValidationSeverity.Info,
                    issue = L("NPR Weight is high, but no illustration-style NPR keywords are currently enabled.", "NPR Weight is high, but no illustration-style NPR keywords are currently enabled."),
                    suggestion = L("Enable Hatching, Watercolor, Color Quantize, or other NPR features so the NPR slider has visible impact.", "Enable Hatching, Watercolor, Color Quantize, or other NPR features so the NPR slider has visible impact.")
                });
            }
        }

        private void ValidateTextureCompression(Material material)
        {
            var textures = new[] { "_MainTex", "_BumpMap", "_EmissionMap" };

            foreach (var texProp in textures)
            {
                if (material.HasProperty(texProp))
                {
                    var tex = material.GetTexture(texProp) as Texture2D;
                    if (tex != null)
                    {
                        string path = AssetDatabase.GetAssetPath(tex);
                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                        if (importer != null)
                        {
                            var platformSettings = importer.GetDefaultPlatformTextureSettings();

                            if (platformSettings.format == TextureImporterFormat.RGBA32 ||
                                platformSettings.format == TextureImporterFormat.RGB24)
                            {
                                validationResults.Add(new ValidationResult
                                {
                                    material = material,
                                    category = "Compression",
                                    severity = ValidationSeverity.Warning,
                                    issue = $"Texture '{texProp}' is uncompressed (format: {platformSettings.format})",
                                    suggestion = "Use DXT/BC compression for PC, ASTC for mobile",
                                    autoFixAction = () =>
                                    {
                                        importer.textureCompression = TextureImporterCompression.Compressed;
                                        importer.SaveAndReimport();
                                        EditorUtility.SetDirty(material);
                                    }
                                });
                            }
                        }
                    }
                }
            }
        }

        private void AutoFixAllIssues()
        {
            int fixedCount = 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Natane Auto-Fix All Issues");

            foreach (var result in validationResults)
            {
                if (result.autoFixAction != null)
                {
                    result.autoFixAction.Invoke();
                    fixedCount++;
                }
            }

            EditorUtility.DisplayDialog(
                L("Auto-Fix Complete", "Auto-Fix Complete"),
                L($"Fixed {fixedCount} issues automatically.\nRemaining issues require manual intervention.", $"Fixed {fixedCount} issues automatically.\nRemaining issues require manual intervention."),
                "OK");

            // Re-validate
            ValidateAllMaterials();
        }

        private long CalculateTextureMemory(Texture2D tex)
        {
            if (tex == null) return 0;

            // Rough estimation based on format
            int bytesPerPixel = 4; // Assume RGBA32

            string path = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                var format = importer.GetDefaultPlatformTextureSettings().format;
                switch (format)
                {
                    case TextureImporterFormat.DXT1:
                        bytesPerPixel = 0; // 0.5 bytes per pixel
                        return tex.width * tex.height / 2;
                    case TextureImporterFormat.DXT5:
                        bytesPerPixel = 1;
                        break;
                    case TextureImporterFormat.RGBA32:
                    case TextureImporterFormat.ARGB32:
                        bytesPerPixel = 4;
                        break;
                    case TextureImporterFormat.RGB24:
                        bytesPerPixel = 3;
                        break;
                }
            }

            return tex.width * tex.height * bytesPerPixel;
        }

        private int CountActiveFeatures(Material material)
        {
            int count = 0;

            foreach (string keyword in ActiveFeatureKeywords)
            {
                if (material.IsKeywordEnabled(keyword))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private void ValidateDependencyPackages(Material material)
        {
            if (material.IsKeywordEnabled("_USE_LIGHT_VOLUME") &&
                !NataneDependencyStatus.IsVRCLightVolumesInstalled())
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "VRChat",
                    severity = ValidationSeverity.Warning,
                    issue = L("Light Volume is enabled, but the VRC Light Volumes package is not detected", "Light Volume is enabled, but the VRC Light Volumes package is not detected"),
                    suggestion = L("Run Tools > Natane > VRChat > VRC Light Volumes Re-detect, or disable Light Volume if you do not need it.", "Run Tools > Natane > VRChat > VRC Light Volumes Re-detect, or disable Light Volume if you do not need it.")
                });
            }

            if (material.IsKeywordEnabled("_LTCGI") &&
                !NataneDependencyStatus.IsLTCGIInstalled())
            {
                validationResults.Add(new ValidationResult
                {
                    material = material,
                    category = "VRChat",
                    severity = ValidationSeverity.Warning,
                    issue = L("LTCGI is enabled, but the LTCGI package is not detected", "LTCGI is enabled, but the LTCGI package is not detected"),
                    suggestion = L("Run Tools > Natane > VRChat > LTCGI Re-detect, or disable LTCGI if you do not need it.", "Run Tools > Natane > VRChat > LTCGI Re-detect, or disable LTCGI if you do not need it.")
                });
            }
        }

        private string BuildSamplerContributorSummary(
            NataneToonSamplerBudgetEstimator.SamplerBudgetEstimate estimate,
            int maxCount)
        {
            if (estimate.Contributors == null || estimate.Contributors.Length == 0)
            {
                return L("No major contributors", "No major contributors");
            }

            int count = Mathf.Min(maxCount, estimate.Contributors.Length);
            string[] parts = new string[count];

            for (int i = 0; i < count; i++)
            {
                parts[i] = NataneToonSamplerBudgetEstimator.FormatContributor(estimate.Contributors[i]);
            }

            return string.Join(", ", parts);
        }
    }
}
