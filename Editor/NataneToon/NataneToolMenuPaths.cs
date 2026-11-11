using System;
using System.Collections.Generic;
using UnityEditor;

namespace NataneToon.Editor
{
    /// <summary>
    /// Centralized menu path registry for Natane editor tools.
    /// Keeps Dashboard/Help launcher paths in sync with actual MenuItem definitions.
    /// </summary>
    public static class NataneToolMenuPaths
    {
        public const string Dashboard = "Tools/Natane/Dashboard %j";
        public const string HelpWindow = "Tools/Natane/ヘルプ Help _h";

        public const string MaterialValidator = "Tools/Natane/マテリアル Material/マテリアル検証 Material Validator _m";
        public const string MaterialEditor = "Tools/Natane/マテリアル Material/統合マテリアルエディタ Unified Material Editor";
        public const string MaterialPreview = "Tools/Natane/マテリアル Material/マテリアルプレビュー Material Preview";
        public const string MaterialComparison = "Tools/Natane/マテリアル Material/マテリアル比較 Material Comparison Tool";
        public const string MakeupLayerManager = "Tools/Natane/マテリアル Material/メイクアップレイヤー管理 Makeup Layer Manager";
        public const string HierarchyBatchEditor = "Tools/Natane/マテリアル Material/ヒエラルキー一括編集 Hierarchy Batch Editor";

        public const string MaterialPresetBrowser = "Tools/Natane/プリセット Presets/マテリアルプリセットブラウザ Material Preset Browser _p";
        public const string ColorPaletteManager = "Tools/Natane/プリセット Presets/カラーパレット管理 Color Palette Manager";
        public const string GenerateDefaultPresets = "Tools/Natane/プリセット Presets/デフォルトプリセット生成 Generate Default Presets";
        public const string RegenerateAllPresets = "Tools/Natane/プリセット Presets/全プリセット再生成 Regenerate All Presets";

        public const string ShadowAdjustmentWizard = "Tools/Natane/エフェクト Effects/シャドウ調整ウィザード Shadow Adjustment Wizard";
        public const string MatCapLayerComposer = "Tools/Natane/エフェクト Effects/MatCapレイヤーコンポーザー MatCap Layer Composer";
        public const string DissolvePatternGenerator = "Tools/Natane/エフェクト Effects/ディゾルブパターン生成 Dissolve Pattern Generator";
        public const string HatchingToneGenerator = "Tools/Natane/エフェクト Effects/ハッチング・水彩素材生成 Hatching & Watercolor Generator";
        public const string FakeShadowSetup = "Tools/Natane/エフェクト Effects/フェイクシャドウ設定 Fake Shadow Setup";
        public const string StencilPresetTool = "Tools/Natane/エフェクト Effects/ステンシルプリセット Stencil Preset";
        public const string DissolveStudio = "Tools/Natane/エフェクト Effects/ディゾルブスタジオ Dissolve Studio";
        public const string ShadowRigFitter = "Tools/Natane/エフェクト Effects/影シェイプリグのフィット Shadow Rig Fitter";
        public const string RimLightDirectionVisualizer = "Tools/Natane/エフェクト Effects/リムライト方向ビジュアライザー Rim Light Direction Visualizer";
        public const string ScreenFXSetup = "Tools/Natane/エフェクト Effects/スクリーンエフェクト設定 Screen FX Setup";

        public const string PerformanceBudgetTool = "Tools/Natane/最適化 Optimization/パフォーマンスバジェット Performance Budget Tool";
        public const string TextureOptimizer = "Tools/Natane/最適化 Optimization/テクスチャ最適化 Texture Optimizer";
        public const string OutlineOptimizer = "Tools/Natane/最適化 Optimization/アウトライン最適化 Outline Optimizer";
        public const string RefractionQualityBalancer = "Tools/Natane/最適化 Optimization/屈折品質バランサー Refraction Quality Balancer";
        public const string AssetReferenceChecker = "Tools/Natane/最適化 Optimization/アセット参照チェッカー Asset Reference Checker";

        public const string LilToonMigration = "Tools/Natane/移行 Migration/lilToon Migration Tool";
        public const string BatchMaterialConverter = "Tools/Natane/移行 Migration/一括マテリアル変換 Batch Material Converter";
        public const string PrefabVariantConverter = "Tools/Natane/移行 Migration/プレハブバリアント変換 Prefab Variant Converter";

        public const string ShaderVariantCollector = "Tools/Natane/シェーダー Shader/シェーダーバリアント収集 Shader Variant Collector";
        public const string ShaderPrewarming = "Tools/Natane/シェーダー Shader/シェーダープリウォーム Shader Prewarming/設定 Settings";
        public const string ShaderVariantStripper = "Tools/Natane/シェーダー Shader/バリアントストリッピング設定 Variant Stripping Settings";
        public const string VRCLightVolumesHelper = "Tools/Natane/VRChat/VRCライトボリュームヘルパー VRC Light Volumes Helper";

        public const string SmoothNormalBaker = "Tools/Natane/メッシュ Mesh/スムース法線ベイク Smooth Normal Baker";
        public const string EyeSetupTool = "Tools/Natane/メッシュ Mesh/目のセットアップ Eye Setup Tool";

        public const string MaskPainter = "Tools/Natane/テクスチャ Texture/マスクペインター Mask Painter";
        public const string TextureStudio = "Tools/Natane/テクスチャ Texture/テクスチャスタジオ Texture Studio";


        public const string ParticleEffectEditor = "Tools/Natane/パーティクルエフェクトエディタ Particle Effect Editor";

        // ===== Consolidated Windows =====
        public const string MaterialAnalysis = "Tools/Natane/マテリアル Material/マテリアル分析 Material Analysis";
        public const string EffectStudio = "Tools/Natane/エフェクト Effects/エフェクトスタジオ Effect Studio";
        public const string OptimizationHub = "Tools/Natane/最適化 Optimization/最適化ハブ Optimization Hub";
        public const string ShaderBuildManager = "Tools/Natane/シェーダー Shader/ビルド管理 Build Manager";
        public const string MigrationHub = "Tools/Natane/移行 Migration/マイグレーションハブ Migration Hub";
        public const string PresetManager = "Tools/Natane/プリセット Presets/プリセット管理 Preset Manager";
        public const string VRChatIntegration = "Tools/Natane/VRChat/VRChat統合 VRChat Integration";

        // Not an actual Unity menu path. Used by launchers for direct tab open.
        public const string HelpToolTab = "__NATANE_HELP_TOOL_TAB__";

        private static readonly Dictionary<string, string> ToolKeyToMenuPath = new Dictionary<string, string>
        {
            // --- Core ---
            { "Dashboard", Dashboard },
            { "HelpWindow", HelpWindow },

            // --- Material ---
            { "MaterialValidator", MaterialValidator },
            { "BatchMaterialProcessor", MaterialEditor }, // Legacy key mapped to unified editor
            { "MaterialEditor", MaterialEditor },
            { "MaterialPreview", MaterialPreview },
            { "MaterialComparison", MaterialComparison },
            { "MakeupLayerManager", MakeupLayerManager },
            { "HierarchyBatchEditor", HierarchyBatchEditor },

            // --- Presets ---
            { "MaterialPresetBrowser", MaterialPresetBrowser },
            { "ColorPaletteManager", ColorPaletteManager },
            { "GenerateDefaultPresets", GenerateDefaultPresets },
            { "RegenerateAllPresets", RegenerateAllPresets },
            // --- Effects ---
            { "ShadowAdjustmentWizard", ShadowAdjustmentWizard },
            { "MatCapLayerComposer", MatCapLayerComposer },
            { "DissolvePatternGenerator", DissolvePatternGenerator },
            { "RimLightDirectionVisualizer", RimLightDirectionVisualizer },
            // { "ScreenFXSetup", ScreenFXSetup },

            // --- Optimization ---
            { "PerformanceBudget", PerformanceBudgetTool },
            { "TextureOptimizer", TextureOptimizer },
            { "OutlineOptimizer", OutlineOptimizer },
            { "RefractionQualityBalancer", RefractionQualityBalancer },
            { "AssetReferenceChecker", AssetReferenceChecker },

            // --- Migration ---
            { "LilToonMigration", LilToonMigration },
            { "BatchMaterialConverter", BatchMaterialConverter },
            { "PrefabVariantConverter", PrefabVariantConverter },

            // --- Shader ---
            { "ShaderVariantCollector", ShaderVariantCollector },
            { "ShaderPrewarming", ShaderPrewarming },
            { "ShaderVariantStripper", ShaderVariantStripper },
            { "VRCLightVolumes", VRCLightVolumesHelper },

            // --- Mesh ---
            { "SmoothNormalBaker", SmoothNormalBaker },
            { "EyeSetupTool", EyeSetupTool },

            // --- Texture ---
            { "MaskPainter", MaskPainter },
            { "TextureStudio", TextureStudio },

            // --- Generator / Other ---
            { "ParticleEffectEditor", ParticleEffectEditor },

            // --- Consolidated Windows ---
            { "MaterialAnalysis", MaterialAnalysis },
            { "EffectStudio", EffectStudio },
            { "OptimizationHub", OptimizationHub },
            { "ShaderBuildManager", ShaderBuildManager },
            { "MigrationHub", MigrationHub },
            { "PresetManager", PresetManager },
            { "VRChatIntegration", VRChatIntegration },
        };

        private static readonly Dictionary<string, string> LegacyPathAliases = new Dictionary<string, string>
        {
            { "Tools/Natane/Dashboard", Dashboard },
            { "Tools/Natane/Help", HelpWindow },
            { "Tools/Natane/ヘルプ Help", HelpWindow },
            { "Tools/Natane/ツールヘルプ Tool Help", HelpToolTab },
            { "Tools/Natane/統合ヘルプ Interactive Help", HelpWindow },
            { "Tools/Natane/Material Validator", MaterialValidator },
            { "Tools/Natane/Batch Material Processor", MaterialEditor },
            { "Tools/Natane/Shadow Adjustment Wizard", ShadowAdjustmentWizard },
            { "Tools/Natane/Material Preview", MaterialPreview },
            { "Tools/Natane/Texture Optimizer", TextureOptimizer },
            { "Tools/Natane/Outline Optimizer", OutlineOptimizer },
            { "Tools/Natane/Dissolve Pattern Generator", DissolvePatternGenerator },
            { "Tools/Natane/MatCap Layer Composer", MatCapLayerComposer },
            { "Tools/Natane/Performance Budget Tool", PerformanceBudgetTool },
            { "Tools/Natane/VRC Light Volumes Helper", VRCLightVolumesHelper },
            { "Tools/Natane/Prefab Variant Converter", PrefabVariantConverter },

            // H-7: 旧個別ツール → 統合ウィンドウへのリダイレクト
            { ShadowAdjustmentWizard, EffectStudio },
            { MatCapLayerComposer, EffectStudio },
            { DissolvePatternGenerator, EffectStudio },
            { RimLightDirectionVisualizer, EffectStudio },
            { PerformanceBudgetTool, MaterialAnalysis },
            { TextureOptimizer, OptimizationHub },
            { OutlineOptimizer, OptimizationHub },
            { RefractionQualityBalancer, OptimizationHub },
            { AssetReferenceChecker, MaterialAnalysis },
            { ColorPaletteManager, PresetManager },
            { GenerateDefaultPresets, PresetManager },
            { ShaderVariantCollector, ShaderBuildManager },
            { ShaderPrewarming, ShaderBuildManager },
            { ShaderVariantStripper, ShaderBuildManager },
            { VRCLightVolumesHelper, VRChatIntegration },
            { BatchMaterialConverter, MigrationHub },
            { PrefabVariantConverter, MigrationHub },

            // H-8: 旧書式パスの後方互換性
            { "Tools/Natane/Material/統合マテリアルエディタ Unified Material Editor", MaterialEditor },
            { "Tools/Natane/Presets/マテリアルプリセットブラウザ Material Preset Browser _p", MaterialPresetBrowser },
            { "Tools/Natane/Optimization/アセット参照チェッカー Asset Reference Checker", AssetReferenceChecker },
            { "Tools/Natane/Migration/lilToon Migration Tool", LilToonMigration },
            { "Tools/Natane/Migration/一括マテリアル変換 Batch Material Converter", BatchMaterialConverter },
            { "Tools/Natane/Shader/シェーダーバリアント収集 Shader Variant Collector", ShaderVariantCollector },
            { "Tools/Natane/Shader/シェーダープリウォーム Shader Prewarming/設定 Settings", ShaderPrewarming },
            { "Tools/Natane/Shader/バリアントストリッピング設定 Variant Stripping Settings", ShaderVariantStripper },
        };

        public static bool TryOpenByToolKey(string toolKey)
        {
            if (string.IsNullOrEmpty(toolKey))
            {
                return false;
            }

            if (!ToolKeyToMenuPath.TryGetValue(toolKey, out var menuPath))
            {
                return false;
            }

            return TryExecute(menuPath);
        }

        public static bool TryExecute(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath))
            {
                return false;
            }

            if (menuPath == HelpToolTab)
            {
                UnifiedHelpSystem.ShowTab(1);
                return true;
            }

            if (ExecuteMenuPath(menuPath))
            {
                return true;
            }

            if (LegacyPathAliases.TryGetValue(menuPath, out var aliasedPath))
            {
                if (aliasedPath == HelpToolTab)
                {
                    UnifiedHelpSystem.ShowTab(1);
                    return true;
                }

                return ExecuteMenuPath(aliasedPath);
            }

            return false;
        }

        private static bool ExecuteMenuPath(string menuPath)
        {
            if (EditorApplication.ExecuteMenuItem(menuPath))
            {
                return true;
            }

            // ExecuteMenuItem expects menu labels, not shortcut suffixes like " _h".
            // Retry with a sanitized path to keep callers robust against MenuItem hotkey changes.
            var normalizedPath = RemoveTrailingShortcut(menuPath);
            return !string.Equals(normalizedPath, menuPath, StringComparison.Ordinal) &&
                   EditorApplication.ExecuteMenuItem(normalizedPath);
        }

        /// <summary>
        /// Returns all registered menu paths for external validation.
        /// Returns all registered menu paths for editor-side validation and health checks.
        /// </summary>
        public static List<string> GetAllMenuPaths()
        {
            var paths = new List<string>
            {
                Dashboard, HelpWindow,
                MaterialValidator, MaterialEditor, MaterialPreview, MaterialComparison, MakeupLayerManager, HierarchyBatchEditor,
                MaterialPresetBrowser, ColorPaletteManager, GenerateDefaultPresets, RegenerateAllPresets,
                ShadowAdjustmentWizard, MatCapLayerComposer, DissolvePatternGenerator,
                RimLightDirectionVisualizer,
                PerformanceBudgetTool, TextureOptimizer, OutlineOptimizer, RefractionQualityBalancer, AssetReferenceChecker,
                LilToonMigration, BatchMaterialConverter, PrefabVariantConverter,
                ShaderVariantCollector, ShaderPrewarming, ShaderVariantStripper, VRCLightVolumesHelper,
                SmoothNormalBaker,
                MaskPainter, TextureStudio,
                ParticleEffectEditor,
                // Consolidated Windows
                MaterialAnalysis, EffectStudio, OptimizationHub, ShaderBuildManager,
                MigrationHub, PresetManager, VRChatIntegration,
            };
            return paths;
        }

        private static string RemoveTrailingShortcut(string menuPath)
        {
            const int shortcutTokenLength = 3; // " _x"
            int shortcutIndex = menuPath.LastIndexOf(" _", StringComparison.Ordinal);
            if (shortcutIndex < 0 || shortcutIndex != menuPath.Length - shortcutTokenLength)
            {
                return menuPath;
            }

            return menuPath.Substring(0, shortcutIndex);
        }
    }
}
