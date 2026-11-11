using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// Natane Toon Shader Dashboard - Central hub for all tools
    /// Natane Toon Shader ダッシュボード - 全ツールへの統合アクセス
    /// Provides categorized access to 25+ tools and features
    /// Shortcut: Ctrl+J
    /// </summary>
    public class NataneDashboard : EditorWindow
    {
        private static string DashboardWindowTitle => L("Natane ダッシュボード", "Natane Dashboard");

        private Vector2 scrollPosition;
        private string searchQuery = "";
        private ToolCategory selectedCategory = ToolCategory.All;
        private DashboardView dashboardView = DashboardView.Workflows;
        private const float MinToolCardWidth = 260f;
        private const float MinWorkflowCardWidth = 320f;
        private const float ToolCardSpacing = 8f;
        private const float ToolGridHorizontalPadding = 24f;
        private const string DashboardViewPrefKey = "NataneDashboard_View";
        private const string RecentToolsPrefKey = "NataneDashboard_RecentTools";
        private const int MaxRecentTools = 3;

        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle selectedCategoryButtonStyle;
        private GUIStyle toolIconStyle;
        private GUIStyle toolNameStyle;
        private GUIStyle toolSecondaryNameStyle;
        private GUIStyle toolDescriptionStyle;
        private GUIStyle toolLaunchButtonStyle;
        private int cachedThemeKey = int.MinValue;

        private enum DashboardView
        {
            Workflows,
            AllTools
        }

        private enum ToolCategory
        {
            All,
            Material,
            Presets,
            Advanced,
            Migration,
            Performance,
            Help
        }

        private class ToolInfo
        {
            public readonly string nameJP;
            public readonly string nameEN;
            public readonly string descriptionJP;
            public readonly string descriptionEN;
            public readonly string menuPath;
            public readonly ToolCategory category;
            public readonly string icon;

            public string DisplayName => L(nameJP, nameEN);
            public string SecondaryName => L(nameEN, nameJP);
            public string DisplayDescription => L(descriptionJP, descriptionEN);

            public ToolInfo(
                string nameJP,
                string nameEN,
                string descriptionJP,
                string descriptionEN,
                string menuPath,
                ToolCategory category,
                string icon = "🔧")
            {
                this.nameJP = nameJP;
                this.nameEN = nameEN;
                this.descriptionJP = descriptionJP;
                this.descriptionEN = descriptionEN;
                this.menuPath = menuPath;
                this.category = category;
                this.icon = icon;
            }
        }

        private class WorkflowInfo
        {
            public readonly string nameJP;
            public readonly string nameEN;
            public readonly string descriptionJP;
            public readonly string descriptionEN;
            public readonly ToolCategory category;
            public readonly string icon;
            public readonly List<ToolInfo> tools;

            public string DisplayName => L(nameJP, nameEN);
            public string SecondaryName => L(nameEN, nameJP);
            public string DisplayDescription => L(descriptionJP, descriptionEN);

            public WorkflowInfo(
                string nameJP,
                string nameEN,
                string descriptionJP,
                string descriptionEN,
                ToolCategory category,
                string icon,
                List<ToolInfo> tools)
            {
                this.nameJP = nameJP;
                this.nameEN = nameEN;
                this.descriptionJP = descriptionJP;
                this.descriptionEN = descriptionEN;
                this.category = category;
                this.icon = icon;
                this.tools = tools;
            }
        }

        private readonly List<ToolInfo> allTools = new List<ToolInfo>();
        private readonly List<WorkflowInfo> workflows = new List<WorkflowInfo>();
        private readonly List<string> recentToolPaths = new List<string>();

        [MenuItem(NataneToolMenuPaths.Dashboard, false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<NataneDashboard>(DashboardWindowTitle);
            window.minSize = new Vector2(700, 500);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshWindowTitle();
            EnsureStyles();
            dashboardView = (DashboardView)Mathf.Clamp(
                EditorPrefs.GetInt(DashboardViewPrefKey, (int)DashboardView.Workflows),
                0,
                System.Enum.GetValues(typeof(DashboardView)).Length - 1);
            InitializeToolsList();
            InitializeWorkflows();
            LoadRecentTools();
        }

        private void RefreshWindowTitle()
        {
            titleContent = new GUIContent(DashboardWindowTitle);
        }

        private void InitializeToolsList()
        {
            allTools.Clear();

            // Material Tools
            AddTool("マテリアル検証", "Material Validator", "VRChat最適化チェック、パフォーマンス評価、自動修正提案", "Validate materials for VRChat readiness, performance, and suggested fixes.", NataneToolMenuPaths.MaterialValidator, ToolCategory.Material, "✅");
            AddTool("マテリアルエディタ", "Material Editor", "Batch ModeとScene Modeを含む統合マテリアル編集ツール。複数マテリアルの一括処理とシーン内リアルタイム編集に対応します。", "Edit multiple materials in batch or scene mode with live updates.", NataneToolMenuPaths.MaterialEditor, ToolCategory.Material, "✏️");
            AddTool("マテリアルプレビュー", "Material Preview", "リアルタイムマテリアルプレビューとライティングテスト。", "Preview materials in real time with lighting controls.", NataneToolMenuPaths.MaterialPreview, ToolCategory.Material, "👁");
            AddTool("マテリアル比較", "Material Comparison Tool", "2つのマテリアルのパラメータ差分を比較します。", "Compare parameter differences between two materials.", NataneToolMenuPaths.MaterialComparison, ToolCategory.Material, "⚖");
            AddTool("ヒエラルキー一括編集", "Hierarchy Batch Editor", "選択した階層内のマテリアルをまとめて編集します。", "Edit materials under the selected hierarchy in one pass.", NataneToolMenuPaths.HierarchyBatchEditor, ToolCategory.Material, "📚");

            // Presets & Assets
            AddTool("マテリアルプリセットブラウザ", "Material Preset Browser", "マテリアルプリセットを視覚的に閲覧して適用します。", "Browse and apply material presets with visual previews.", NataneToolMenuPaths.MaterialPresetBrowser, ToolCategory.Presets, "🎨");
            AddTool("カラーパレット管理", "Color Palette Manager", "カラーパレットの作成と管理を行います。", "Create and manage shared color palettes.", NataneToolMenuPaths.ColorPaletteManager, ToolCategory.Presets, "🌈");
            AddTool("デフォルトプリセット生成", "Generate Default Presets", "基本的なマテリアルプリセットを自動生成します。", "Generate the standard starter material presets.", NataneToolMenuPaths.GenerateDefaultPresets, ToolCategory.Presets, "⚡");
            AddTool("全プリセット再生成", "Regenerate All Presets", "全てのプリセットを再生成します。開発・メンテナンス向けです。", "Rebuild every preset asset for development or maintenance.", NataneToolMenuPaths.RegenerateAllPresets, ToolCategory.Presets, "🔄");
            AddTool("VTuberプリセット生成", "VTuber Preset Generator", "VTuber向けの肌・髪・衣装・瞳・ライブ用プリセットを Material Preset Browser 内で自動生成します。", "Generate VTuber-focused presets for skin, hair, outfits, eyes, and live scenes from the preset browser.", NataneToolMenuPaths.MaterialPresetBrowser, ToolCategory.Presets, "🎤");

            // Advanced Tools (Effects + Makeup)
            AddTool("シャドウ調整ウィザード", "Shadow Adjustment Wizard", "トゥーンシャドウを視覚的に段階調整します。", "Tune toon shadow parameters with guided visual controls.", NataneToolMenuPaths.ShadowAdjustmentWizard, ToolCategory.Advanced, "🌓");
            AddTool("メイクアップレイヤー管理", "Makeup Layer Manager", "キャラクターメイクアップを多層で管理します。", "Manage layered character makeup and masks.", NataneToolMenuPaths.MakeupLayerManager, ToolCategory.Advanced, "💄");
            AddTool("MatCapレイヤーコンポーザー", "MatCap Layer Composer", "複数の MatCap テクスチャを合成します。", "Blend and composite multiple MatCap textures.", NataneToolMenuPaths.MatCapLayerComposer, ToolCategory.Advanced, "🎭");
            AddTool("ディゾルブパターン生成", "Dissolve Pattern Generator", "ディゾルブエフェクト用のパターンテクスチャを生成します。", "Create pattern textures for dissolve effects.", NataneToolMenuPaths.DissolvePatternGenerator, ToolCategory.Advanced, "✨");
            AddTool("スクリーンエフェクト設定", "Screen FX Setup", "VRC向け画面効果オーバーレイをカメラへ自動セットアップします。", "Automatically set up a VRC-friendly full-screen overlay on a camera.", NataneToolMenuPaths.ScreenFXSetup, ToolCategory.Advanced, "🖥");
            AddTool("リムライト方向ビジュアライザー", "Rim Light Direction Visualizer", "リムライト方向を視覚的に確認します。", "Visualize rim light direction before committing settings.", NataneToolMenuPaths.RimLightDirectionVisualizer, ToolCategory.Advanced, "💡");

            AddTool("パーティクルエフェクトエディタ", "Particle Effect Editor", "パーティクルエフェクトの見た目とプリセットを編集します。", "Edit particle effect visuals and presets.", NataneToolMenuPaths.ParticleEffectEditor, ToolCategory.Advanced, "✨");
            AddTool("スムース法線ベイク", "Smooth Normal Baker", "アウトライン向けのスムース法線をメッシュへベイクします。", "Bake smooth normals into meshes for stable outlines.", NataneToolMenuPaths.SmoothNormalBaker, ToolCategory.Advanced, "🧊");
            AddTool("目のセットアップ", "Eye Setup Tool", "目の表現に必要なメッシュとマテリアル設定をガイドします。", "Guide mesh and material setup for eye rendering.", NataneToolMenuPaths.EyeSetupTool, ToolCategory.Advanced, "👁");
            AddTool("マスクペインター", "Mask Painter", "シーンビューでマスクテクスチャを直接ペイントします。R/G/Bチャンネル対応。", "Paint mask textures directly in the Scene view with per-channel control.", NataneToolMenuPaths.MaskPainter, ToolCategory.Advanced, "🖌");

            // Migration Tools
            AddTool("lilToon移行ツール", "lilToon Migration Tool", "lilToon シェーダーからマテリアルを自動移行します。", "Migrate materials from lilToon automatically.", NataneToolMenuPaths.LilToonMigration, ToolCategory.Migration, "🔀");
            AddTool("一括マテリアル変換", "Batch Material Converter", "汎用マテリアルを一括変換します。", "Convert many generic materials in one pass.", NataneToolMenuPaths.BatchMaterialConverter, ToolCategory.Migration, "📤");
            AddTool("プレハブバリアント変換", "Prefab Variant Converter", "プレハブ内マテリアルを一括変換します。", "Convert materials inside prefab variants in bulk.", NataneToolMenuPaths.PrefabVariantConverter, ToolCategory.Migration, "📦");

            // Performance Tools
            AddTool("パフォーマンスバジェット", "Performance Budget Tool", "シーン全体のパフォーマンス予算を分析します。", "Analyze scene-wide material performance budgets.", NataneToolMenuPaths.PerformanceBudgetTool, ToolCategory.Performance, "⚡");
            AddTool("テクスチャ最適化", "Texture Optimizer", "テクスチャサイズと圧縮設定を自動最適化します。", "Optimize texture size and compression settings automatically.", NataneToolMenuPaths.TextureOptimizer, ToolCategory.Performance, "🖼");
            AddTool("アウトライン最適化", "Outline Optimizer", "アウトライン設定を最適化します。", "Optimize outline settings for better performance.", NataneToolMenuPaths.OutlineOptimizer, ToolCategory.Performance, "🎯");
            AddTool("屈折品質バランサー", "Refraction Quality Balancer", "屈折エフェクトの品質とパフォーマンスをバランス調整します。", "Balance refraction quality against performance.", NataneToolMenuPaths.RefractionQualityBalancer, ToolCategory.Performance, "🔮");
            AddTool("アセット参照チェッカー", "Asset Reference Checker", "未参照アセットや依存関係を確認します。", "Inspect asset references and unused dependencies.", NataneToolMenuPaths.AssetReferenceChecker, ToolCategory.Performance, "🔗");
            AddTool("シェーダーバリアント収集", "Shader Variant Collector", "使用中のシェーダーバリアントを収集してビルドサイズを削減します。", "Collect used shader variants to cut build size.", NataneToolMenuPaths.ShaderVariantCollector, ToolCategory.Performance, "📊");
            AddTool("シェーダープリウォーミング", "Shader Prewarming", "ビルド前のシェーダーウォーミングで VRChat 初回フリーズを防ぎます。", "Warm shaders before build to reduce first-load stalls in VRChat.", NataneToolMenuPaths.ShaderPrewarming, ToolCategory.Performance, "🔥");
            AddTool("バリアントストリッピング", "Variant Stripping Settings", "未使用シェーダーバリアントの除外設定を管理します。", "Configure removal of unused shader variants.", NataneToolMenuPaths.ShaderVariantStripper, ToolCategory.Performance, "✂");
            AddTool("VRCライトボリュームヘルパー", "VRC Light Volumes Helper", "VRChat Light Volumes のセットアップを支援します。", "Assist with VRC Light Volumes setup.", NataneToolMenuPaths.VRCLightVolumesHelper, ToolCategory.Performance, "💡");

            // Consolidated Windows (統合ウィンドウ)
            AddTool("マテリアル分析", "Material Analysis", "マテリアル検証・パフォーマンス・比較・参照チェックを統合したウィンドウです。", "Unified window for material validation, performance, comparison, and reference checks.", NataneToolMenuPaths.MaterialAnalysis, ToolCategory.Material, "📋");
            AddTool("エフェクトスタジオ", "Effect Studio", "シャドウ・MatCap・リムライト・ディゾルブ・レイヤー管理を統合したウィンドウです。", "Unified window for shadow, MatCap, rim light, dissolve, and layer management.", NataneToolMenuPaths.EffectStudio, ToolCategory.Advanced, "🎬");
            AddTool("最適化ハブ", "Optimization Hub", "テクスチャ・アウトライン・屈折の最適化を統合したウィンドウです。", "Unified window for texture, outline, and refraction optimization.", NataneToolMenuPaths.OptimizationHub, ToolCategory.Performance, "⚙");
            AddTool("シェーダービルド管理", "Shader Build Manager", "バリアント収集・プリウォーミング・ストリッピングを統合したウィンドウです。", "Unified window for variant collection, prewarming, and stripping.", NataneToolMenuPaths.ShaderBuildManager, ToolCategory.Performance, "🔨");
            AddTool("マイグレーションハブ", "Migration Hub", "lilToon移行・一括変換・Prefab変換を統合したウィンドウです。", "Unified window for lilToon migration, batch conversion, and prefab conversion.", NataneToolMenuPaths.MigrationHub, ToolCategory.Migration, "🔄");
            AddTool("プリセット管理", "Preset Manager", "プリセットブラウザ・カラーパレット・プリセット生成を統合したウィンドウです。", "Unified window for preset browsing, color palettes, and preset generation.", NataneToolMenuPaths.PresetManager, ToolCategory.Presets, "📦");
            AddTool("VRChat統合", "VRChat Integration", "Light Volumes・パッケージ設定・自動検出を統合したウィンドウです。", "Unified window for Light Volumes, package setup, and auto-detection.", NataneToolMenuPaths.VRChatIntegration, ToolCategory.Performance, "🌐");
            AddTool("テクスチャスタジオ", "Texture Studio", "マスクペイント・テクスチャ最適化・生成ツールを統合したウィンドウです。", "Unified window for mask painting, texture optimization, and generation tools.", NataneToolMenuPaths.TextureStudio, ToolCategory.Advanced, "🖼");

            // Help & Documentation
            AddTool("ツールヘルプ", "Tool Help", "全ツールの使い方と説明を確認します。", "Browse usage guides and explanations for every tool.", NataneToolMenuPaths.HelpToolTab, ToolCategory.Help, "❓");
            AddTool("統合ヘルプ", "Interactive Help", "インタラクティブな統合ヘルプシステムを開きます。", "Open the unified interactive help system.", NataneToolMenuPaths.HelpWindow, ToolCategory.Help, "📚");
        }

        private void InitializeWorkflows()
        {
            workflows.Clear();

            AddWorkflow(
                "マテリアルを作る・整える",
                "Create & Edit Materials",
                "選択中のマテリアルを編集し、プリセットやプレビューで見た目を仕上げます。",
                "Edit selected materials, apply presets, and verify the look with previews.",
                ToolCategory.Material,
                "🎨",
                NataneToolMenuPaths.MaterialEditor,
                NataneToolMenuPaths.MaterialPresetBrowser,
                NataneToolMenuPaths.ColorPaletteManager,
                NataneToolMenuPaths.MaterialPreview);

            AddWorkflow(
                "マテリアルを確認・一括修正",
                "Validate & Batch Fix",
                "問題の検出、差分比較、階層単位の一括編集をまとめた確認フローです。",
                "Validate materials, compare differences, and batch-edit a hierarchy.",
                ToolCategory.Material,
                "✅",
                NataneToolMenuPaths.MaterialValidator,
                NataneToolMenuPaths.MaterialComparison,
                NataneToolMenuPaths.HierarchyBatchEditor);

            AddWorkflow(
                "プリセットと色を管理",
                "Manage Presets & Colors",
                "再利用する見た目、配色、初期プリセットを一か所から管理します。",
                "Manage reusable looks, palettes, and starter presets from one place.",
                ToolCategory.Presets,
                "🌈",
                NataneToolMenuPaths.MaterialPresetBrowser,
                NataneToolMenuPaths.ColorPaletteManager,
                NataneToolMenuPaths.GenerateDefaultPresets);

            AddWorkflow(
                "ルック・エフェクトを仕上げる",
                "Polish Look & Effects",
                "シャドウ、MatCap、リムライト、ディゾルブなど、見た目の仕上げを行います。",
                "Polish shadows, MatCaps, rim lights, dissolve effects, and layered makeup.",
                ToolCategory.Advanced,
                "✨",
                NataneToolMenuPaths.ShadowAdjustmentWizard,
                NataneToolMenuPaths.MatCapLayerComposer,
                NataneToolMenuPaths.RimLightDirectionVisualizer,
                NataneToolMenuPaths.DissolvePatternGenerator,
                NataneToolMenuPaths.MakeupLayerManager);

            AddWorkflow(
                "メッシュ・目・パーティクルを準備",
                "Prepare Mesh, Eyes & Particles",
                "シェーダー表現に必要なメッシュ加工と特殊表現のセットアップを行います。",
                "Prepare mesh data and specialized eye or particle rendering.",
                ToolCategory.Advanced,
                "🧩",
                NataneToolMenuPaths.SmoothNormalBaker,
                NataneToolMenuPaths.EyeSetupTool,
                NataneToolMenuPaths.ParticleEffectEditor);

            AddWorkflow(
                "負荷を調べて最適化",
                "Analyze & Optimize",
                "シーン負荷を確認し、テクスチャ・アウトライン・屈折を段階的に軽量化します。",
                "Measure scene cost, then optimize textures, outlines, and refraction.",
                ToolCategory.Performance,
                "⚡",
                NataneToolMenuPaths.PerformanceBudgetTool,
                NataneToolMenuPaths.TextureOptimizer,
                NataneToolMenuPaths.OutlineOptimizer,
                NataneToolMenuPaths.RefractionQualityBalancer,
                NataneToolMenuPaths.AssetReferenceChecker);

            AddWorkflow(
                "既存アセットを移行",
                "Migrate Existing Assets",
                "lilToonや一般マテリアル、Prefab内のマテリアルをNatane Toonへ移行します。",
                "Migrate lilToon, generic materials, and materials inside prefabs.",
                ToolCategory.Migration,
                "🔄",
                NataneToolMenuPaths.LilToonMigration,
                NataneToolMenuPaths.BatchMaterialConverter,
                NataneToolMenuPaths.PrefabVariantConverter);

            AddWorkflow(
                "ビルドとVRChat向け準備",
                "Prepare Build & VRChat",
                "バリアント収集・プリウォーム・ストリッピングとLight Volumes設定を行います。",
                "Collect, warm, and strip variants, then configure VRC Light Volumes.",
                ToolCategory.Performance,
                "🔨",
                NataneToolMenuPaths.ShaderVariantCollector,
                NataneToolMenuPaths.ShaderPrewarming,
                NataneToolMenuPaths.ShaderVariantStripper,
                NataneToolMenuPaths.VRCLightVolumesHelper);

            AddWorkflow(
                "使い方・トラブルを確認",
                "Learn & Troubleshoot",
                "ツールの使い方を調べ、起動できない機能や依存関係を確認します。",
                "Browse tool guidance and diagnose missing dependencies or launch issues.",
                ToolCategory.Help,
                "❓",
                NataneToolMenuPaths.HelpToolTab,
                NataneToolMenuPaths.HelpWindow);
        }

        private void AddTool(
            string nameJP,
            string nameEN,
            string descriptionJP,
            string descriptionEN,
            string menuPath,
            ToolCategory category,
            string icon)
        {
            allTools.Add(new ToolInfo(nameJP, nameEN, descriptionJP, descriptionEN, menuPath, category, icon));
        }

        private void AddWorkflow(
            string nameJP,
            string nameEN,
            string descriptionJP,
            string descriptionEN,
            ToolCategory category,
            string icon,
            params string[] toolPaths)
        {
            var tools = new List<ToolInfo>();
            foreach (var toolPath in toolPaths)
            {
                var tool = FindTool(toolPath);
                if (tool != null && !tools.Contains(tool))
                {
                    tools.Add(tool);
                }
            }

            if (tools.Count > 0)
            {
                workflows.Add(new WorkflowInfo(
                    nameJP,
                    nameEN,
                    descriptionJP,
                    descriptionEN,
                    category,
                    icon,
                    tools));
            }
        }

        private ToolInfo FindTool(string menuPath)
        {
            return allTools.Find(tool => tool.menuPath == menuPath);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null && cachedThemeKey == NataneToonEditorTheme.CacheKey)
            {
                return;
            }

            cachedThemeKey = NataneToonEditorTheme.CacheKey;

            var secondaryTextColor = NataneToonEditorTheme.TextDim;
            var descriptionTextColor = NataneToonEditorTheme.TextDim;

            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = NataneToonEditorTheme.Accent;

            subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            subtitleStyle.normal.textColor = NataneToonEditorTheme.TextMuted;

            selectedCategoryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };
            // ヘルプウィンドウの選択ハイライトと同じ、ブランドアクセントの薄色地
            Color accent = NataneToonEditorTheme.Accent;
            Texture2D selectedBg = NataneToonEditorTextures.Solid(
                new Color(accent.r, accent.g, accent.b, NataneToonEditorTheme.IsDark ? 0.30f : 0.18f));
            selectedCategoryButtonStyle.normal.background = selectedBg;
            selectedCategoryButtonStyle.hover.background = selectedBg;
            selectedCategoryButtonStyle.active.background = selectedBg;
            selectedCategoryButtonStyle.focused.background = selectedBg;
            selectedCategoryButtonStyle.normal.textColor = NataneToonEditorTheme.Text;

            toolIconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 24,
                alignment = TextAnchor.UpperLeft
            };

            toolNameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                wordWrap = true
            };

            toolSecondaryNameStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };
            toolSecondaryNameStyle.normal.textColor = secondaryTextColor;

            toolDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 10,
                wordWrap = true
            };
            toolDescriptionStyle.normal.textColor = descriptionTextColor;

            toolLaunchButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnGUI()
        {
            NataneToonInspectorComponents.DrawWindowBackground(position);
            EnsureStyles();
            RefreshWindowTitle();
            DrawHeader();
            DrawToolbar();
            DrawRecentTools();
            EditorGUILayout.Space(5);
            DrawCategoryTabs();
            EditorGUILayout.Space(5);
            DrawSearchBar();
            EditorGUILayout.Space(10);
            DrawDashboardContent();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("🎨 Natane Toon Shader ダッシュボード", "🎨 Natane Toon Shader Dashboard"), titleStyle, GUILayout.Height(30));
            EditorGUILayout.LabelField(
                L("やりたい作業から選び、必要なツールへ直接アクセスできます。", "Choose a workflow and jump directly to the tool you need."),
                subtitleStyle);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(L("更新", "Refresh"), EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                InitializeToolsList();
                InitializeWorkflows();
            }

            GUILayout.Space(8f);

            if (GUILayout.Toggle(
                dashboardView == DashboardView.Workflows,
                L("作業別", "Workflows"),
                EditorStyles.toolbarButton,
                GUILayout.Width(90f)) && dashboardView != DashboardView.Workflows)
            {
                SetDashboardView(DashboardView.Workflows);
            }

            if (GUILayout.Toggle(
                dashboardView == DashboardView.AllTools,
                L("全ツール", "All Tools"),
                EditorStyles.toolbarButton,
                GUILayout.Width(90f)) && dashboardView != DashboardView.AllTools)
            {
                SetDashboardView(DashboardView.AllTools);
            }

            GUILayout.FlexibleSpace();

            // Health indicator
            var health = NataneToolHealthValidator.GetOverallHealth();
            Color indicatorColor;
            string indicatorLabel;
            switch (health)
            {
                case NataneToolHealthValidator.HealthStatus.Error:
                    indicatorColor = NataneToonColorPalette.Error;
                    indicatorLabel = L("異常", "Error");
                    break;
                case NataneToolHealthValidator.HealthStatus.Warning:
                    indicatorColor = NataneToonColorPalette.Warning;
                    indicatorLabel = L("警告", "Warning");
                    break;
                default:
                    indicatorColor = NataneToonColorPalette.Success;
                    indicatorLabel = L("正常", "Healthy");
                    break;
            }

            var oldColor = GUI.color;
            GUI.color = indicatorColor;
            EditorGUILayout.LabelField(indicatorLabel, EditorStyles.miniLabel, GUILayout.Width(70));
            GUI.color = oldColor;

            if (GUILayout.Button(L("診断", "Diagnostics"), EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                NataneToolHealthValidator.ShowWindow();
            }

            int filteredItemCount = dashboardView == DashboardView.Workflows
                ? GetFilteredWorkflows().Count
                : GetFilteredTools().Count;
            string countLabel = dashboardView == DashboardView.Workflows
                ? L($"{filteredItemCount} 作業", $"{filteredItemCount} workflows")
                : L($"{filteredItemCount} ツール", $"{filteredItemCount} tools");
            EditorGUILayout.LabelField(countLabel, EditorStyles.miniLabel, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();
        }

        private void SetDashboardView(DashboardView view)
        {
            dashboardView = view;
            scrollPosition = Vector2.zero;
            EditorPrefs.SetInt(DashboardViewPrefKey, (int)view);
            Repaint();
        }

        private void DrawRecentTools()
        {
            if (recentToolPaths.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("最近使ったツール", "Recent"), EditorStyles.miniBoldLabel, GUILayout.Width(110f));

            var paths = recentToolPaths.ToArray();
            foreach (var path in paths)
            {
                var tool = FindTool(path);
                if (tool == null)
                {
                    continue;
                }

                if (GUILayout.Button(
                    new GUIContent(tool.DisplayName, tool.DisplayDescription),
                    EditorStyles.miniButton,
                    GUILayout.Width(140f)))
                {
                    LaunchTool(tool);
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L("履歴を消去", "Clear"), EditorStyles.miniButton, GUILayout.Width(80f)))
            {
                recentToolPaths.Clear();
                SaveRecentTools();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryTabs()
        {
            EditorGUILayout.BeginHorizontal();

            var categories = System.Enum.GetValues(typeof(ToolCategory));
            foreach (ToolCategory category in categories)
            {
                string categoryName = GetCategoryDisplayName(category);
                int count = GetItemCountForCategory(category);
                string label = $"{categoryName} ({count})";

                bool isSelected = selectedCategory == category;
                GUIStyle buttonStyle = isSelected ? selectedCategoryButtonStyle : GUI.skin.button;

                if (GUILayout.Button(label, buttonStyle, GUILayout.MinHeight(28f)))
                {
                    selectedCategory = category;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(L("🔍 検索", "🔍 Search"), GUILayout.Width(72f));
            searchQuery = EditorGUILayout.TextField(searchQuery);

            if (GUILayout.Button(L("クリア", "Clear"), GUILayout.Width(60f)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDashboardContent()
        {
            if (dashboardView == DashboardView.Workflows)
            {
                DrawWorkflowGrid();
                return;
            }

            DrawToolGrid();
        }

        private void DrawWorkflowGrid()
        {
            var filteredWorkflows = GetFilteredWorkflows();

            if (filteredWorkflows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("該当する作業が見つかりません。全ツール表示に切り替えるか、検索条件を変更してください。", "No workflow matched. Switch to All Tools or change the search."),
                    MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            float availableWidth = Mathf.Max(240f, position.width - ToolGridHorizontalPadding);
            int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + ToolCardSpacing) / (MinWorkflowCardWidth + ToolCardSpacing)));
            float cardWidth = Mathf.Max(220f, (availableWidth - ((columns - 1) * ToolCardSpacing)) / columns);
            int rows = Mathf.CeilToInt((float)filteredWorkflows.Count / columns);

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();

                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= filteredWorkflows.Count)
                    {
                        break;
                    }

                    DrawWorkflowCard(filteredWorkflows[index], cardWidth);

                    if (col < columns - 1 && index < filteredWorkflows.Count - 1)
                    {
                        GUILayout.Space(ToolCardSpacing);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(ToolCardSpacing);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawWorkflowCard(WorkflowInfo workflow, float cardWidth)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(cardWidth), GUILayout.MinHeight(210f));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(workflow.icon, toolIconStyle, GUILayout.Width(34f));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(workflow.DisplayName, toolNameStyle);
            if (workflow.SecondaryName != workflow.DisplayName)
            {
                EditorGUILayout.LabelField(workflow.SecondaryName, toolSecondaryNameStyle);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(workflow.DisplayDescription, toolDescriptionStyle);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(L("この作業で使うツール", "Tools in this workflow"), EditorStyles.miniBoldLabel);

            foreach (var tool in workflow.tools)
            {
                if (GUILayout.Button(
                    new GUIContent(L($"{tool.DisplayName} を開く", $"Open {tool.DisplayName}"), tool.DisplayDescription),
                    GUILayout.Height(24f)))
                {
                    LaunchTool(tool);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawToolGrid()
        {
            var filteredTools = GetFilteredTools();

            if (filteredTools.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("該当するツールが見つかりません。検索条件かカテゴリを変更してください。", "No tools matched your current search. Change the search text or category."),
                    MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            float availableWidth = Mathf.Max(200f, position.width - ToolGridHorizontalPadding);
            int columns = GetResponsiveColumnCount(availableWidth);
            float cardWidth = Mathf.Max(180f, (availableWidth - ((columns - 1) * ToolCardSpacing)) / columns);
            int rows = Mathf.CeilToInt((float)filteredTools.Count / columns);

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();

                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= filteredTools.Count) break;

                    DrawToolCard(filteredTools[index], cardWidth);

                    if (col < columns - 1 && index < filteredTools.Count - 1)
                    {
                        GUILayout.Space(ToolCardSpacing);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(ToolCardSpacing);
            }

            EditorGUILayout.EndScrollView();
        }

        private int GetResponsiveColumnCount(float availableWidth)
        {
            return Mathf.Max(1, Mathf.FloorToInt((availableWidth + ToolCardSpacing) / (MinToolCardWidth + ToolCardSpacing)));
        }

        private void DrawToolCard(ToolInfo tool, float cardWidth)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(cardWidth), GUILayout.MinHeight(120f));

            // Tool name with icon
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(tool.icon, toolIconStyle, GUILayout.Width(32f));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(tool.DisplayName, toolNameStyle);
            string secondaryName = tool.SecondaryName;
            if (!string.IsNullOrEmpty(secondaryName) && secondaryName != tool.DisplayName)
            {
                EditorGUILayout.LabelField(secondaryName, toolSecondaryNameStyle);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            // Description
            EditorGUILayout.LabelField(tool.DisplayDescription, toolDescriptionStyle);
            EditorGUILayout.Space(8f);

            // Launch button
            if (GUILayout.Button(L("起動", "Launch"), toolLaunchButtonStyle, GUILayout.Height(28f)))
            {
                LaunchTool(tool);
            }

            EditorGUILayout.EndVertical();
        }

        private void LaunchTool(ToolInfo tool)
        {
            if (NataneToolHealthValidator.ValidateAndLaunch(tool.menuPath, tool.DisplayName))
            {
                RecordRecentTool(tool.menuPath);
            }
        }

        private void RecordRecentTool(string menuPath)
        {
            recentToolPaths.Remove(menuPath);
            recentToolPaths.Insert(0, menuPath);

            if (recentToolPaths.Count > MaxRecentTools)
            {
                recentToolPaths.RemoveRange(MaxRecentTools, recentToolPaths.Count - MaxRecentTools);
            }

            SaveRecentTools();
        }

        private void LoadRecentTools()
        {
            recentToolPaths.Clear();
            string serializedPaths = EditorPrefs.GetString(RecentToolsPrefKey, string.Empty);
            if (string.IsNullOrEmpty(serializedPaths))
            {
                return;
            }

            var paths = serializedPaths.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in paths)
            {
                if (recentToolPaths.Count >= MaxRecentTools)
                {
                    break;
                }

                if (FindTool(path) != null && !recentToolPaths.Contains(path))
                {
                    recentToolPaths.Add(path);
                }
            }
        }

        private void SaveRecentTools()
        {
            EditorPrefs.SetString(RecentToolsPrefKey, string.Join("\n", recentToolPaths.ToArray()));
        }

        private List<WorkflowInfo> GetFilteredWorkflows()
        {
            var filtered = new List<WorkflowInfo>();

            foreach (var workflow in workflows)
            {
                if (selectedCategory != ToolCategory.All && workflow.category != selectedCategory)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    string query = searchQuery.Trim();
                    bool matchesWorkflow =
                        ContainsSearchText(workflow.nameJP, query) ||
                        ContainsSearchText(workflow.nameEN, query) ||
                        ContainsSearchText(workflow.descriptionJP, query) ||
                        ContainsSearchText(workflow.descriptionEN, query);

                    bool matchesTool = workflow.tools.Exists(tool => ToolMatchesSearch(tool, query));
                    if (!matchesWorkflow && !matchesTool)
                    {
                        continue;
                    }
                }

                filtered.Add(workflow);
            }

            return filtered;
        }

        private List<ToolInfo> GetFilteredTools()
        {
            var filtered = new List<ToolInfo>();

            foreach (var tool in allTools)
            {
                // Category filter
                if (selectedCategory != ToolCategory.All && tool.category != selectedCategory)
                    continue;

                // Search filter
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    string query = searchQuery.Trim();
                    if (!ToolMatchesSearch(tool, query))
                        continue;
                }

                filtered.Add(tool);
            }

            return filtered;
        }

        private static bool ToolMatchesSearch(ToolInfo tool, string query)
        {
            return ContainsSearchText(tool.nameJP, query) ||
                   ContainsSearchText(tool.nameEN, query) ||
                   ContainsSearchText(tool.descriptionJP, query) ||
                   ContainsSearchText(tool.descriptionEN, query);
        }

        private static bool ContainsSearchText(string source, string query)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetCategoryDisplayName(ToolCategory category)
        {
            switch (category)
            {
                case ToolCategory.All: return L("すべて", "All");
                case ToolCategory.Material: return L("マテリアル", "Materials");
                case ToolCategory.Presets: return L("プリセット", "Presets");
                case ToolCategory.Advanced: return L("高度な機能", "Advanced");
                case ToolCategory.Migration: return L("移行", "Migration");
                case ToolCategory.Performance: return L("パフォーマンス", "Performance");
                case ToolCategory.Help: return L("ヘルプ", "Help");
                default: return category.ToString();
            }
        }

        private int GetItemCountForCategory(ToolCategory category)
        {
            if (dashboardView == DashboardView.Workflows)
            {
                if (category == ToolCategory.All)
                {
                    return workflows.Count;
                }

                int workflowCount = 0;
                foreach (var workflow in workflows)
                {
                    if (workflow.category == category)
                    {
                        workflowCount++;
                    }
                }

                return workflowCount;
            }

            if (category == ToolCategory.All)
            {
                return allTools.Count;
            }

            int count = 0;
            foreach (var tool in allTools)
            {
                if (tool.category == category)
                    count++;
            }
            return count;
        }
    }
}
