using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// Cross-assembly tool health validator and diagnostics window.
    /// クロスアセンブリ ツール健全性バリデータと診断ウィンドウ。
    ///
    /// Validates that all registered tools are reachable across assembly boundaries
    /// before launching, and provides a diagnostics UI for troubleshooting.
    /// </summary>
    public class NataneToolHealthValidator : EditorWindow
    {
        // =====================================================================
        // Data Types
        // =====================================================================

        public enum HealthStatus
        {
            Healthy,
            Warning,
            Error
        }

        public enum DiagnosticSeverity
        {
            OK,
            Warning,
            Error
        }

        public class DiagnosticResult
        {
            public string toolKey;
            public string displayName;
            public string menuPath;
            public DiagnosticSeverity severity;
            public string message;
            public string suggestion;
        }

        /// <summary>
        /// Single Source of Truth for tool registration.
        /// ツール登録の唯一の真実の情報源。
        /// </summary>
        private class ToolRegistryEntry
        {
            public string toolKey;
            public string displayName;
            public string menuPath;
            public string typeName;
            public string assemblyName;

            public ToolRegistryEntry(string toolKey, string displayName, string menuPath,
                string typeName, string assemblyName)
            {
                this.toolKey = toolKey;
                this.displayName = displayName;
                this.menuPath = menuPath;
                this.typeName = typeName;
                this.assemblyName = assemblyName;
            }
        }

        // =====================================================================
        // Tool Registry (Single Source of Truth)
        // =====================================================================

        private const string ASSEMBLY_EDITOR = "NataneToon.Editor";
        private const string ASSEMBLY_TOOLS = "NataneToon.Editor.Tools";
        private const string ASSEMBLY_MIGRATION = "NataneToon.Editor.Migration";
        private const string DEPENDENCY_SETUP_MENU = "Tools/Natane/VRChat/Natane Dependency Setup";
        private const string NATANE_SHADER_NAME_PREFIX = "Natane/Toon Shader";

        private static readonly List<ToolRegistryEntry> ToolRegistry = new List<ToolRegistryEntry>
        {
            // --- Material Tools (Tools assembly) ---
            new ToolRegistryEntry("MaterialValidator", "マテリアル検証 Material Validator",
                NataneToolMenuPaths.MaterialValidator, "NataneToon.Editor.MaterialValidator", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("MaterialEditor", "マテリアルエディタ Material Editor",
                NataneToolMenuPaths.MaterialEditor, "NataneToon.Editor.UnifiedMaterialEditor", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("MaterialPreview", "マテリアルプレビュー Material Preview",
                NataneToolMenuPaths.MaterialPreview, "NataneToon.Editor.MaterialPreviewWindow", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("MaterialComparison", "マテリアル比較 Material Comparison",
                NataneToolMenuPaths.MaterialComparison, "NataneToon.Editor.MaterialComparisonTool", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("MakeupLayerManager", "メイクアップレイヤー管理 Makeup Layer Manager",
                NataneToolMenuPaths.MakeupLayerManager, "NataneToon.Editor.MakeupLayerManager", ASSEMBLY_TOOLS),

            // --- Preset Tools (Editor assembly) ---
            new ToolRegistryEntry("MaterialPresetBrowser", "Material Preset Browser",
                NataneToolMenuPaths.MaterialPresetBrowser, "NataneToon.Editor.MaterialPresetBrowser", ASSEMBLY_EDITOR),
            new ToolRegistryEntry("ColorPaletteManager", "カラーパレット管理 Color Palette Manager",
                NataneToolMenuPaths.ColorPaletteManager, "NataneToon.Editor.NataneToonColorPaletteManager", ASSEMBLY_EDITOR),

            // --- Effects Tools (Tools assembly) ---
            new ToolRegistryEntry("ShadowAdjustmentWizard", "シャドウ調整ウィザード Shadow Wizard",
                NataneToolMenuPaths.ShadowAdjustmentWizard, "NataneToon.Editor.ShadowAdjustmentWizard", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("MatCapLayerComposer", "MatCapレイヤーコンポーザー MatCap Composer",
                NataneToolMenuPaths.MatCapLayerComposer, "NataneToon.Editor.MatCapLayerComposer", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("DissolvePatternGenerator", "ディゾルブパターン生成 Dissolve Generator",
                NataneToolMenuPaths.DissolvePatternGenerator, "NataneToon.Editor.DissolvePatternGenerator", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("RimLightDirectionVisualizer", "リムライト方向ビジュアライザー Rim Visualizer",
                NataneToolMenuPaths.RimLightDirectionVisualizer, "NataneToon.Editor.RimLightDirectionVisualizer", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("ScreenFXSetup", "スクリーンエフェクト設定 Screen FX Setup",
                NataneToolMenuPaths.ScreenFXSetup, "NataneToon.Editor.ScreenFXSetupTool", ASSEMBLY_TOOLS),

            // --- Optimization Tools (Tools assembly) ---
            new ToolRegistryEntry("PerformanceBudget", "パフォーマンスバジェット Performance Budget",
                NataneToolMenuPaths.PerformanceBudgetTool, "NataneToon.Editor.PerformanceBudgetTool", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("TextureOptimizer", "テクスチャ最適化 Texture Optimizer",
                NataneToolMenuPaths.TextureOptimizer, "NataneToon.Editor.TextureOptimizer", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("OutlineOptimizer", "アウトライン最適化 Outline Optimizer",
                NataneToolMenuPaths.OutlineOptimizer, "NataneToon.Editor.OutlineOptimizer", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("RefractionQualityBalancer", "屈折品質バランサー Refraction Balancer",
                NataneToolMenuPaths.RefractionQualityBalancer, "NataneToon.Editor.RefractionQualityBalancer", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("ShaderVariantCollector", "シェーダーバリアント収集 Shader Variant Collector",
                NataneToolMenuPaths.ShaderVariantCollector, "NataneToon.Editor.ShaderVariantCollector", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("ShaderVariantStripper", "シェーダーバリアントストリッパー Shader Variant Stripper",
                NataneToolMenuPaths.ShaderVariantStripper, "NataneToon.Editor.ShaderVariantStripper", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("VRCLightVolumesHelper", "VRCライトボリュームヘルパー VRC Light Volumes Helper",
                NataneToolMenuPaths.VRCLightVolumesHelper, "NataneToon.Editor.VRCLightVolumesHelper", ASSEMBLY_TOOLS),

            // --- Migration Tools (Migration assembly) ---
            new ToolRegistryEntry("LilToonMigration", "lilToon移行ツール lilToon Migration",
                NataneToolMenuPaths.LilToonMigration, "NataneToon.Editor.LilToonMigrationTool", ASSEMBLY_MIGRATION),
            new ToolRegistryEntry("BatchMaterialConverter", "一括マテリアル変換 Batch Converter",
                NataneToolMenuPaths.BatchMaterialConverter, "NataneToon.Editor.BatchMaterialConverter", ASSEMBLY_MIGRATION),
            new ToolRegistryEntry("PrefabVariantConverter", "プレハブバリアント変換 Prefab Converter",
                NataneToolMenuPaths.PrefabVariantConverter, "NataneToon.Editor.PrefabVariantConverter", ASSEMBLY_TOOLS),


            new ToolRegistryEntry("ShaderPrewarming", "シェーダープリウォーミング Shader Prewarming",
                NataneToolMenuPaths.ShaderPrewarming, "NataneToon.Editor.ShaderPrewarmingEditor", ASSEMBLY_TOOLS),

            // --- Optimization Tools (Tools assembly, continued) ---
            new ToolRegistryEntry("AssetReferenceChecker", "アセット参照チェック Asset Reference Checker",
                NataneToolMenuPaths.AssetReferenceChecker, "NataneToon.Editor.AssetReferenceChecker", ASSEMBLY_TOOLS),

            // --- Mesh Tools (Tools assembly) ---
            new ToolRegistryEntry("SmoothNormalBaker", "スムース法線ベイク Smooth Normal Baker",
                NataneToolMenuPaths.SmoothNormalBaker, "NataneToon.Editor.SmoothNormalBaker", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("EyeSetupTool", "目のセットアップ Eye Setup Tool",
                NataneToolMenuPaths.EyeSetupTool, "NataneToon.Editor.EyeSetupTool", ASSEMBLY_TOOLS),

            // --- Texture Tools (Tools assembly) ---
            new ToolRegistryEntry("MaskPainter", "マスクペインター Mask Painter",
                NataneToolMenuPaths.MaskPainter, "NataneToon.Editor.NataneMaskPainter", ASSEMBLY_TOOLS),
            new ToolRegistryEntry("TextureStudio", "テクスチャスタジオ Texture Studio",
                NataneToolMenuPaths.TextureStudio, "NataneToon.Editor.NataneTextureStudioWindow", ASSEMBLY_TOOLS),

            // --- Preset Tools (Editor assembly, continued) ---
            new ToolRegistryEntry("GenerateDefaultPresets", "デフォルトプリセット生成 Generate Default Presets",
                NataneToolMenuPaths.GenerateDefaultPresets, "NataneToon.Editor.DefaultPresetGenerator", ASSEMBLY_EDITOR),
            new ToolRegistryEntry("RegenerateAllPresets", "全プリセット再生成 Regenerate All Presets",
                NataneToolMenuPaths.RegenerateAllPresets, "NataneToon.Editor.DefaultPresetGenerator", ASSEMBLY_EDITOR),

            // --- Core Tools (Editor assembly) ---
            new ToolRegistryEntry("Dashboard", "ダッシュボード Dashboard",
                NataneToolMenuPaths.Dashboard, "NataneToon.Editor.NataneDashboard", ASSEMBLY_EDITOR),
            new ToolRegistryEntry("HelpWindow", "ヘルプ Help",
                NataneToolMenuPaths.HelpWindow, "NataneToon.Editor.UnifiedHelpSystem", ASSEMBLY_EDITOR),

            // --- Other Tools (Editor assembly) ---
            new ToolRegistryEntry("ParticleEffectEditor", "パーティクルエフェクトエディタ Particle Effect Editor",
                NataneToolMenuPaths.ParticleEffectEditor, "NataneParticleSystemEditor.ParticleEffectEditorWindow", ASSEMBLY_EDITOR),
        };

        private static string DiagnosticsWindowTitle => L("診断", "Diagnostics");

        private static DiagnosticResult CreateDiagnosticResult(
            string toolKey,
            string displayNameJa,
            string displayNameEn,
            string menuPath,
            DiagnosticSeverity severity,
            string messageJa,
            string messageEn,
            string suggestionJa = "",
            string suggestionEn = "")
        {
            return new DiagnosticResult
            {
                toolKey = toolKey,
                displayName = L(displayNameJa, displayNameEn),
                menuPath = menuPath,
                severity = severity,
                message = L(messageJa, messageEn),
                suggestion = string.IsNullOrEmpty(suggestionJa) && string.IsNullOrEmpty(suggestionEn)
                    ? string.Empty
                    : L(suggestionJa, suggestionEn)
            };
        }

        private static DiagnosticResult CreateHealthyResult(ToolRegistryEntry entry)
        {
            return new DiagnosticResult
            {
                toolKey = entry.toolKey,
                displayName = entry.displayName,
                menuPath = entry.menuPath,
                severity = DiagnosticSeverity.OK,
                message = L("正常", "Healthy"),
                suggestion = string.Empty
            };
        }

        // =====================================================================
        // Static Validation API
        // =====================================================================

        /// <summary>
        /// Validate a single tool by menu path. Returns null if healthy, DiagnosticResult otherwise.
        /// 単一ツールのメニューパスで事前チェック。正常ならnull、問題があればDiagnosticResultを返す。
        /// </summary>
        public static DiagnosticResult ValidateTool(string menuPath)
        {
            var entry = ToolRegistry.FirstOrDefault(e => e.menuPath == menuPath);
            if (entry == null)
            {
                return CreateDiagnosticResult(
                    "__unregistered__",
                    "未登録メニュー",
                    "Unregistered Menu",
                    menuPath,
                    DiagnosticSeverity.Warning,
                    "ツールレジストリに登録されていないメニューパスです。",
                    "The menu path is not registered in the tool registry.",
                    "NataneToolHealthValidator.ToolRegistry にエントリを追加してください。",
                    "Add an entry to NataneToolHealthValidator.ToolRegistry.");
            }

            return ValidateEntry(entry);
        }

        /// <summary>
        /// Validate and launch a tool. Shows a dialog on failure with cause and remedy.
        /// チェック後に起動。失敗時は原因と対処法をダイアログ表示。
        /// </summary>
        public static bool ValidateAndLaunch(string menuPath, string displayName)
        {
            var result = ValidateTool(menuPath);

            if (result != null && result.severity == DiagnosticSeverity.Error)
            {
                bool openDiagnostics = EditorUtility.DisplayDialog(
                    L("ツール起動エラー", "Tool Launch Error"),
                    $"{L("ツールを起動できません:", "Failed to launch the tool:")}\n{displayName}\n\n" +
                    $"{L("原因", "Cause")}:\n{result.message}\n\n" +
                    $"{L("対処法", "Remedy")}:\n{result.suggestion}",
                    L("診断を開く", "Open Diagnostics"),
                    L("閉じる", "Close"));

                if (openDiagnostics)
                {
                    ShowWindow();
                }

                return false;
            }

            // Attempt to launch via NataneToolMenuPaths
            if (!NataneToolMenuPaths.TryExecute(menuPath))
            {
                EditorUtility.DisplayDialog(
                    L("ツール起動エラー", "Tool Launch Error"),
                    $"{L("メニュー項目を実行できませんでした:", "Failed to execute the menu item:")}\n{displayName}\n\n" +
                    $"{L("メニューパス", "Menu path")}: {menuPath}\n\n" +
                    L("メニューパスが正しいか、対象アセンブリがロードされているか確認してください。",
                      "Verify that the menu path is correct and the target assembly is loaded."),
                    "OK");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Lightweight health check for Dashboard toolbar indicator.
        /// Dashboard ツールバーインジケータ用の軽量ヘルスチェック。
        /// </summary>
        public static HealthStatus GetOverallHealth()
        {
            bool hasError = false;
            bool hasWarning = false;

            // Check critical assemblies are loaded
            var loadedAssemblyNames = new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetName().Name));

            if (!loadedAssemblyNames.Contains(ASSEMBLY_TOOLS))
            {
                return HealthStatus.Error;
            }

            if (!loadedAssemblyNames.Contains(ASSEMBLY_MIGRATION))
            {
                return HealthStatus.Error;
            }

            // Spot-check a few critical tools
            var spotCheckKeys = new[] { "MaterialValidator", "LilToonMigration", "ShaderVariantCollector" };
            foreach (var key in spotCheckKeys)
            {
                var entry = ToolRegistry.FirstOrDefault(e => e.toolKey == key);
                if (entry == null) continue;

                var result = ValidateEntry(entry);
                if (result != null)
                {
                    if (result.severity == DiagnosticSeverity.Error) hasError = true;
                    else if (result.severity == DiagnosticSeverity.Warning) hasWarning = true;
                }
            }

            if (hasError) return HealthStatus.Error;
            if (hasWarning) return HealthStatus.Warning;
            return HealthStatus.Healthy;
        }

        /// <summary>
        /// Run full diagnostics on all registered tools.
        /// 全登録ツールの包括的チェックを実行。
        /// </summary>
        public static List<DiagnosticResult> RunFullDiagnostics()
        {
            var results = new List<DiagnosticResult>();

            // 1. Assembly load checks
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var loadedAssemblyNames = new HashSet<string>(loadedAssemblies.Select(a => a.GetName().Name));

            var requiredAssemblies = new[] { ASSEMBLY_EDITOR, ASSEMBLY_TOOLS, ASSEMBLY_MIGRATION };
            foreach (var asmName in requiredAssemblies)
            {
                if (!loadedAssemblyNames.Contains(asmName))
                {
                    results.Add(CreateDiagnosticResult(
                        "__assembly__",
                        $"アセンブリ: {asmName}",
                        $"Assembly: {asmName}",
                        "",
                        DiagnosticSeverity.Error,
                        $"アセンブリ '{asmName}' がロードされていません。",
                        $"Assembly '{asmName}' is not loaded.",
                        "対応する .asmdef ファイルが存在し、参照が正しく設定されているか確認してください。",
                        "Verify the .asmdef file exists and references are correctly configured."));
                }
            }

            // 2. Per-tool checks
            foreach (var entry in ToolRegistry)
            {
                var result = ValidateEntry(entry);
                if (result != null)
                {
                    results.Add(result);
                }
                else
                {
                    // Add OK result for display purposes
                    results.Add(CreateHealthyResult(entry));
                }
            }

            // 3. Third-party integration dependency checks
            results.AddRange(RunDependencyDiagnostics());

            return results;
        }

        // =====================================================================
        // Internal Validation Logic
        // =====================================================================

        private static IEnumerable<DiagnosticResult> RunDependencyDiagnostics()
        {
            var results = new List<DiagnosticResult>();
            bool hasLightVolumePackage = NataneDependencyStatus.IsInstalled(NataneDependencyStatus.VRCLightVolumes);
            bool hasLtcgiPackage = NataneDependencyStatus.IsInstalled(NataneDependencyStatus.LTCGI);

            if (hasLightVolumePackage && hasLtcgiPackage)
            {
                return results;
            }

            List<Material> nataneMaterials = FindNataneMaterials();

            if (!hasLightVolumePackage)
            {
                List<Material> lightVolumeMaterials = nataneMaterials
                    .Where(material => IsMaterialFeatureEnabled(material, "_UseLightVolume"))
                    .ToList();

                if (lightVolumeMaterials.Count > 0)
                {
                    results.Add(CreateDiagnosticResult(
                        "__dependency_lightvolume__",
                        "VRC Light Volumes 依存関係",
                        "VRC Light Volumes Dependency",
                        DEPENDENCY_SETUP_MENU,
                        DiagnosticSeverity.Warning,
                        $"{lightVolumeMaterials.Count}件のNataneマテリアルで VRC Light Volumes が有効ですが、パッケージがインストールされていません。バンドル済みフォールバックは引き続き有効です。例: {FormatMaterialExamples(lightVolumeMaterials)}",
                        $"VRC Light Volumes is enabled on {lightVolumeMaterials.Count} Natane material(s), but the package is not installed. The bundled fallback remains active. Examples: {FormatMaterialExamples(lightVolumeMaterials)}",
                        $"{DEPENDENCY_SETUP_MENU} を開き、パッケージ版も使いたい場合は red.sim.lightvolumes をインストールしてください。",
                        $"Open {DEPENDENCY_SETUP_MENU} and install red.sim.lightvolumes if you want the package version as well."));
                }
            }

            if (!hasLtcgiPackage)
            {
                List<Material> ltcgiMaterials = nataneMaterials
                    .Where(material => IsMaterialFeatureEnabled(material, "_LTCGI"))
                    .ToList();

                if (ltcgiMaterials.Count > 0)
                {
                    results.Add(CreateDiagnosticResult(
                        "__dependency_ltcgi__",
                        "LTCGI 依存関係",
                        "LTCGI Dependency",
                        DEPENDENCY_SETUP_MENU,
                        DiagnosticSeverity.Warning,
                        $"{ltcgiMaterials.Count}件のNataneマテリアルで LTCGI が有効ですが、パッケージがインストールされていません。at.pimaker.ltcgi を追加するまで効果は無効のままです。例: {FormatMaterialExamples(ltcgiMaterials)}",
                        $"LTCGI is enabled on {ltcgiMaterials.Count} Natane material(s), but the package is not installed. The effect remains disabled until at.pimaker.ltcgi is added. Examples: {FormatMaterialExamples(ltcgiMaterials)}",
                        $"{DEPENDENCY_SETUP_MENU} を開き、at.pimaker.ltcgi をインストールしてください。",
                        $"Open {DEPENDENCY_SETUP_MENU} and install at.pimaker.ltcgi."));
                }
            }

            return results;
        }

        private static List<Material> FindNataneMaterials()
        {
            return NataneMaterialAssetCache.GetMaterialsByShaderPrefix(NATANE_SHADER_NAME_PREFIX).ToList();
        }

        private static bool IsMaterialFeatureEnabled(Material material, string propertyName)
        {
            return material != null &&
                   material.HasProperty(propertyName) &&
                   material.GetFloat(propertyName) > 0.5f;
        }

        private static string FormatMaterialExamples(IReadOnlyList<Material> materials)
        {
            if (materials == null || materials.Count == 0)
            {
                return L("なし", "None");
            }

            List<string> names = materials
                .Take(3)
                .Select(material => material.name)
                .ToList();
            string suffix = materials.Count > names.Count
                ? L($" ほか{materials.Count - names.Count}件", $" (+{materials.Count - names.Count} more)")
                : string.Empty;
            return string.Join(", ", names) + suffix;
        }

        private static DiagnosticResult ValidateEntry(ToolRegistryEntry entry)
        {
            // Skip non-menu-path entries (e.g. HelpToolTab sentinel)
            if (string.IsNullOrEmpty(entry.menuPath) || entry.menuPath.StartsWith("__"))
                return null;

            // 1. Check assembly is loaded
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var targetAssembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == entry.assemblyName);

            if (targetAssembly == null)
            {
                return CreateDiagnosticResult(
                    entry.toolKey,
                    entry.displayName,
                    entry.displayName,
                    entry.menuPath,
                    DiagnosticSeverity.Error,
                    $"アセンブリ '{entry.assemblyName}' が見つかりません。",
                    $"Assembly '{entry.assemblyName}' not found.",
                    ".asmdef ファイルが正しく配置されているか確認してください。",
                    "Verify the .asmdef file is correctly placed.");
            }

            // 2. Check type exists in assembly
            Type toolType = null;
            try
            {
                toolType = targetAssembly.GetType(entry.typeName);

                // If not found by full name, search all types (for global namespace types)
                if (toolType == null)
                {
                    try
                    {
                        toolType = targetAssembly.GetTypes()
                            .FirstOrDefault(t => t.FullName == entry.typeName || t.Name == entry.typeName);
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Some types may fail to load; check loaded types
                        toolType = ex.Types
                            .Where(t => t != null)
                            .FirstOrDefault(t => t.FullName == entry.typeName || t.Name == entry.typeName);
                    }
                }
            }
            catch (Exception)
            {
                // Assembly.GetType can throw in rare cases
            }

            if (toolType == null)
            {
                return CreateDiagnosticResult(
                    entry.toolKey,
                    entry.displayName,
                    entry.displayName,
                    entry.menuPath,
                    DiagnosticSeverity.Error,
                    $"型 '{entry.typeName}' がアセンブリ '{entry.assemblyName}' 内に見つかりません。",
                    $"Type '{entry.typeName}' not found in assembly '{entry.assemblyName}'.",
                    "クラス名または名前空間が変更されていないか確認してください。",
                    "Check if the class name or namespace has been changed.");
            }

            // 3. Check MenuItem attribute exists (for non-static-class tools)
            if (!toolType.IsAbstract || !toolType.IsSealed) // not a static class
            {
                bool hasMenuItem = toolType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(m => m.GetCustomAttributes(typeof(MenuItem), false).Length > 0);

                if (!hasMenuItem)
                {
                    return CreateDiagnosticResult(
                        entry.toolKey,
                        entry.displayName,
                        entry.displayName,
                        entry.menuPath,
                        DiagnosticSeverity.Warning,
                        $"型 '{entry.typeName}' に [MenuItem] 属性が見つかりません。",
                        $"No [MenuItem] attribute found on type '{entry.typeName}'.",
                        "メニューパスから起動できない可能性があります。",
                        "Launching from the menu path may not work.");
                }
            }

            return null; // Healthy
        }

        // =====================================================================
        // EditorWindow UI
        // =====================================================================

        private List<DiagnosticResult> diagnosticResults = new List<DiagnosticResult>();
        private Vector2 scrollPosition;
        private bool showOnlyProblems = false;

        [MenuItem("Tools/Natane/診断 Diagnostics", false, 2)]
        public static void ShowWindow()
        {
            var window = GetWindow<NataneToolHealthValidator>(DiagnosticsWindowTitle);
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshWindowTitle();
            RunDiagnostics();
        }

        private void RefreshWindowTitle()
        {
            titleContent = new GUIContent(DiagnosticsWindowTitle);
        }

        private void RunDiagnostics()
        {
            diagnosticResults = RunFullDiagnostics();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(5);
            DrawSummary();
            EditorGUILayout.Space(5);
            DrawToolbar();
            EditorGUILayout.Space(5);
            DrawResultsList();
        }

        private void DrawHeader()
        {
            NataneToonShaderGUIUtility.DrawToolHeader(
                "ツール診断",
                "Tool Health Diagnostics",
                "Diagnostics");
            RefreshWindowTitle();
        }

        private void DrawSummary()
        {
            int errorCount = diagnosticResults.Count(r => r.severity == DiagnosticSeverity.Error);
            int warningCount = diagnosticResults.Count(r => r.severity == DiagnosticSeverity.Warning);
            int okCount = diagnosticResults.Count(r => r.severity == DiagnosticSeverity.OK);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            Color oldColor = GUI.color;

            GUI.color = errorCount > 0 ? NataneToonColorPalette.Error : Color.white;
            EditorGUILayout.LabelField($"{L("エラー", "Errors")}: {errorCount}", EditorStyles.boldLabel, GUILayout.Width(140));

            GUI.color = warningCount > 0 ? NataneToonColorPalette.Warning : Color.white;
            EditorGUILayout.LabelField($"{L("警告", "Warnings")}: {warningCount}", EditorStyles.boldLabel, GUILayout.Width(150));

            GUI.color = NataneToonColorPalette.Success;
            EditorGUILayout.LabelField($"{L("正常", "Healthy")}: {okCount}", EditorStyles.boldLabel, GUILayout.Width(140));

            GUI.color = oldColor;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(L("再チェック", "Re-check"), EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                RunDiagnostics();
            }

            showOnlyProblems = GUILayout.Toggle(showOnlyProblems, L("問題のみ表示", "Problems Only"),
                EditorStyles.toolbarButton, GUILayout.Width(140));

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawResultsList()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var result in diagnosticResults)
            {
                if (showOnlyProblems && result.severity == DiagnosticSeverity.OK)
                    continue;

                DrawResultItem(result);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawResultItem(DiagnosticResult result)
        {
            Color bgColor;
            string icon;

            switch (result.severity)
            {
                case DiagnosticSeverity.Error:
                    bgColor = new Color(1f, 0.5f, 0.5f, 0.3f);
                    icon = "X";
                    break;
                case DiagnosticSeverity.Warning:
                    bgColor = new Color(1f, 1f, 0.5f, 0.3f);
                    icon = "!";
                    break;
                default:
                    bgColor = new Color(0.5f, 1f, 0.5f, 0.2f);
                    icon = "OK";
                    break;
            }

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();

            // Severity indicator
            var oldColor = GUI.color;
            GUI.color = result.severity == DiagnosticSeverity.Error ? NataneToonColorPalette.Error :
                        result.severity == DiagnosticSeverity.Warning ? NataneToonColorPalette.Warning :
                        NataneToonColorPalette.Success;
            EditorGUILayout.LabelField($"[{icon}]", EditorStyles.boldLabel, GUILayout.Width(30));
            GUI.color = oldColor;

            // Display name
            EditorGUILayout.LabelField(result.displayName, EditorStyles.boldLabel);

            EditorGUILayout.EndHorizontal();

            // Message
            if (result.severity != DiagnosticSeverity.OK)
            {
                EditorGUILayout.LabelField(result.message, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(result.suggestion))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField($"{L("対処法", "Remedy")}:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(result.suggestion, EditorStyles.wordWrappedLabel);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }
}