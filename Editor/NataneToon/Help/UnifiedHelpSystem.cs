using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    /// <summary>
    /// 統合ヘルプシステム - すべてのヘルプ機能を1つのウィンドウで提供
    /// Unified Help System - Provides all help functionality in one window
    ///
    /// 統合内容 Integrated Components:
    /// - InteractiveHelpSystem (クイックスタート、用語集、チュートリアル、トラブルシューティング、ヒント)
    /// - NataneToonToolsHelp (各ツールの使い方)
    /// - NataneToonToolsDocumentation (ドキュメントデータベース)
    /// </summary>
    public class UnifiedHelpSystem : EditorWindow
    {
        private static string HelpWindowTitle => L("Natane統合ヘルプ", "Natane Unified Help");

        private Vector2 scrollPosition;
        private Vector2 toolListScroll;
        private int selectedMainTab = 0;
        private string[] mainTabs = new[] {
            "🚀 クイックスタート Quick Start",
            "🛠️ ツールヘルプ Tool Help",
            "📖 用語集 Glossary",
            "📚 チュートリアル Tutorials",
            "🔧 トラブル Troubleshooting",
            "💡 ヒント Tips",
            "📄 ドキュメント Documentation"
        };

        // InteractiveHelpSystem関連
        private string searchQuery = "";
        private Dictionary<string, string> glossary;
        private Dictionary<string, Tutorial> tutorials;

        // ToolHelp関連
        private string selectedTool = "";
        private string toolSearchQuery = "";
        private int selectedToolCategory = 0;
        private string[] toolCategories = new[] {
            "すべて All",
            "品質 Quality",
            "最適化 Optimization",
            "プレビュー Preview",
            "エフェクト Effect",
            "VRChat",
            "ユーティリティ Utility"
        };

        // Styles
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle bodyStyle;
        private GUIStyle tipBoxStyle;
        private GUIStyle toolListButtonStyle;
        private GUIStyle selectedToolListButtonStyle;
        private Texture2D selectedToolListBackground;
        private int cachedThemeKey = int.MinValue;

        private class Tutorial
        {
            public string title;
            public string description;
            public List<string> steps;
            public string category;
        }

        [MenuItem(NataneToolMenuPaths.HelpWindow, false, 2)]
        public static void ShowWindow()
        {
            var window = GetWindow<UnifiedHelpSystem>(HelpWindowTitle);
            window.minSize = new Vector2(900, 650);
            window.Show();
        }

        /// <summary>
        /// 特定のタブを開く
        /// Open specific tab
        /// </summary>
        public static void ShowTab(int tabIndex)
        {
            var window = GetWindow<UnifiedHelpSystem>(HelpWindowTitle);
            window.selectedMainTab = tabIndex;
            window.minSize = new Vector2(900, 650);
            window.Show();
        }

        /// <summary>
        /// 特定のツールヘルプを開く
        /// Open specific tool help
        /// </summary>
        public static void ShowToolHelp(string toolKey)
        {
            var window = GetWindow<UnifiedHelpSystem>(HelpWindowTitle);
            window.selectedMainTab = 1; // Tool Help tab
            window.selectedTool = toolKey;
            window.minSize = new Vector2(900, 650);
            window.Show();
        }

        /// <summary>
        /// 用語集で特定のトピックを検索
        /// Search for specific topic in glossary
        /// </summary>
        public static void ShowHelp(string topic)
        {
            var window = GetWindow<UnifiedHelpSystem>(HelpWindowTitle);
            window.selectedMainTab = 2; // Glossary tab
            window.searchQuery = topic;
            window.minSize = new Vector2(900, 650);
            window.Show();
        }

        /// <summary>
        /// チュートリアルを表示
        /// Show tutorial
        /// </summary>
        public static void ShowTutorial(string tutorialId)
        {
            var window = GetWindow<UnifiedHelpSystem>(HelpWindowTitle);
            window.selectedMainTab = 3; // Tutorials tab
            window.minSize = new Vector2(900, 650);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshWindowTitle();
            InitializeStyles();
            InitializeGlossary();
            InitializeTutorials();
        }

        private void OnDisable()
        {
            // selectedToolListBackground は NataneToonEditorTextures の共有キャッシュ
            // なのでここでは破棄しない（参照を手放すだけ）。
            selectedToolListBackground = null;
            headerStyle = null;
            subHeaderStyle = null;
            bodyStyle = null;
            tipBoxStyle = null;
            toolListButtonStyle = null;
            selectedToolListButtonStyle = null;
        }

        private void RefreshWindowTitle()
        {
            titleContent = new GUIContent(HelpWindowTitle);
        }

        private void InitializeStyles()
        {
            bool themeChanged = cachedThemeKey != NataneToonEditorTheme.CacheKey;
            bool stylesMissing = headerStyle == null ||
                                 subHeaderStyle == null ||
                                 bodyStyle == null ||
                                 tipBoxStyle == null ||
                                 toolListButtonStyle == null ||
                                 selectedToolListButtonStyle == null ||
                                 selectedToolListBackground == null;

            if (!themeChanged && !stylesMissing)
            {
                return;
            }

            cachedThemeKey = NataneToonEditorTheme.CacheKey;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                margin = new RectOffset(0, 0, 10, 10)
            };

            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(0, 0, 8, 5)
            };

            bodyStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true
            };

            tipBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 5, 5)
            };

            toolListButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                padding = new RectOffset(10, 10, 6, 6)
            };

            // 選択中ツールのハイライト: ブランドアクセントの薄色地（共有キャッシュから取得）
            Color accent = NataneToonEditorTheme.Accent;
            selectedToolListBackground = NataneToonEditorTextures.Solid(
                new Color(accent.r, accent.g, accent.b, NataneToonEditorTheme.IsDark ? 0.30f : 0.18f));

            selectedToolListButtonStyle = new GUIStyle(toolListButtonStyle)
            {
                fontStyle = FontStyle.Bold
            };
            ApplyBackground(selectedToolListButtonStyle, selectedToolListBackground);
        }

        private void OnGUI()
        {
            NataneToonInspectorComponents.DrawWindowBackground(position);
            InitializeStyles();
            RefreshWindowTitle();

            // ヘッダー
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("✨ Natane Toon Shader - 統合ヘルプシステム", "✨ Natane Toon Shader - Unified Help System"), headerStyle);
            EditorGUILayout.LabelField(L("すべての機能、ツール、チュートリアルを1か所で確認", "Access all features, tools, and tutorials in one place"), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // メインタブ
            selectedMainTab = GUILayout.Toolbar(selectedMainTab, mainTabs, GUILayout.Height(30));

            EditorGUILayout.Space(10);

            // タブコンテンツ
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (selectedMainTab)
            {
                case 0: DrawQuickStart(); break;
                case 1: DrawToolHelp(); break;
                case 2: DrawGlossary(); break;
                case 3: DrawTutorials(); break;
                case 4: DrawTroubleshooting(); break;
                case 5: DrawTips(); break;
                case 6: DrawDocumentation(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ==================== クイックスタート ====================
        private void DrawQuickStart()
        {
            DrawSection("🚀 クイックスタートガイド Quick Start Guide", () =>
            {
                DrawSubSection("はじめに Getting Started (5分 5 minutes)", () =>
                {
                    DrawStep("1", "マテリアルを作成し、'Natane/Toon Shader'またはそのバリアントを選択\nCreate a material and select 'Natane/Toon Shader' or one of its variants");
                    DrawStep("2", "マテリアルプリセットブラウザを使用 (Tools > Natane > Material Preset Browser)\nUse Material Preset Browser (Tools > Natane > Material Preset Browser)");
                    DrawStep("3", "ニーズに合ったプリセットを選択 (例: Character_Skin_Soft)\nSelect a preset that matches your needs (e.g., Character_Skin_Soft)");
                    DrawStep("4", "プリセットをマテリアルに適用\nApply the preset to your material");
                    DrawStep("5", "Inspectorで必要に応じてパラメーターを調整\nAdjust parameters as needed in the Inspector");
                });

                EditorGUILayout.Space(10);

                DrawSubSection("初回セットアップ First Time Setup", () =>
                {
                    DrawBullet("デフォルトプリセットを生成: Tools > Natane > Generate Default Presets\nGenerate default presets: Tools > Natane > Generate Default Presets");
                    DrawBullet("マテリアルプリセットブラウザを開く: Tools > Natane > Material Preset Browser\nOpen Material Preset Browser: Tools > Natane > Material Preset Browser");
                    DrawBullet("マテリアルを検証: Tools > Natane > Material Validator\nValidate materials: Tools > Natane > Material Validator");
                });

                EditorGUILayout.Space(10);

                DrawSubSection("一般的なワークフロー Common Workflows", () =>
                {
                    DrawWorkflow("キャラクター作成 Character Creation", new[]
                    {
                        "肌にCharacter_Skin_Softプリセットを使用 Use Character_Skin_Soft preset for skin",
                        "髪にCharacter_Hair_Standardプリセットを使用 Use Character_Hair_Standard preset for hair",
                        "服にCharacter_Clothing_Fabricを使用 Use Character_Clothing_Fabric for clothes",
                        "目にCharacter_Eyes_Standardを使用 Use Character_Eyes_Standard for eyes",
                        "スタイルに合わせて色とライティングを微調整 Fine-tune colors and lighting to match your style"
                    });

                    EditorGUILayout.Space(5);

                    DrawWorkflow("環境作成 Environment Creation", new[]
                    {
                        "草にEnvironment_Nature_Grassを使用 Use Environment_Nature_Grass for grass",
                        "建物にEnvironment_Architecture_Stoneを使用 Use Environment_Architecture_Stone for buildings",
                        "スタイライゼーションレベルに応じてトゥーンステップを調整 Adjust toon steps for stylization level"
                    });
                });

                EditorGUILayout.Space(15);

                // 外部ドキュメントリンク
                DrawSubSection("📄 外部ドキュメント External Documentation", () =>
                {
                    if (GUILayout.Button("📖 README.md を開く Open README.md", GUILayout.Height(30)))
                    {
                        OpenDocumentationFile("README.md");
                    }
                    if (GUILayout.Button("🔧 TECHNICAL.md を開く Open TECHNICAL.md", GUILayout.Height(30)))
                    {
                        OpenDocumentationFile("TECHNICAL.md");
                    }
                    if (GUILayout.Button("🚀 QUICK_START.md を開く Open QUICK_START.md", GUILayout.Height(30)))
                    {
                        OpenDocumentationFile("QUICK_START.md");
                    }
                    if (GUILayout.Button("📋 CHANGELOG.md を開く Open CHANGELOG.md", GUILayout.Height(30)))
                    {
                        OpenDocumentationFile("CHANGELOG.md");
                    }
                });
            });
        }

        // ==================== ツールヘルプ ====================
        private void DrawToolHelp()
        {
            EditorGUILayout.BeginHorizontal();

            // 左側: ツールリスト
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            DrawToolList();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 右側: ツール詳細
            EditorGUILayout.BeginVertical();
            DrawToolDetails();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolList()
        {
            EditorGUILayout.LabelField(L("🛠️ ツール一覧", "🛠️ Tool List"), EditorStyles.boldLabel);

            // 検索バー
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔍", GUILayout.Width(20));
            toolSearchQuery = EditorGUILayout.TextField(toolSearchQuery, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("✕", GUILayout.Width(25)))
            {
                toolSearchQuery = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // カテゴリフィルター
            selectedToolCategory = GUILayout.SelectionGrid(selectedToolCategory, toolCategories, 1, GUILayout.Height(toolCategories.Length * 25));

            EditorGUILayout.Space(5);

            toolListScroll = EditorGUILayout.BeginScrollView(toolListScroll, EditorStyles.helpBox, GUILayout.MinHeight(300));

            var allDocs = NataneToonToolsDocumentation.GetAllDocumentation();
            var filteredDocs = allDocs.Where(kvp => FilterTool(kvp.Value)).ToList();

            foreach (var kvp in filteredDocs)
            {
                bool isSelected = selectedTool == kvp.Key;
                GUIStyle buttonStyle = isSelected ? selectedToolListButtonStyle : toolListButtonStyle;

                if (GUILayout.Button($"{kvp.Value.toolNameJP}\n{kvp.Value.toolName}", buttonStyle, GUILayout.Height(45)))
                {
                    selectedTool = kvp.Key;
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField(
                L($"表示: {filteredDocs.Count} / {allDocs.Count} ツール", $"{filteredDocs.Count} / {allDocs.Count} tools"),
                EditorStyles.miniLabel);
        }

        private bool FilterTool(NataneToonToolsDocumentation.ToolDocumentation doc)
        {
            // カテゴリフィルター
            if (selectedToolCategory > 0)
            {
                string categoryFilter = toolCategories[selectedToolCategory].Split(' ')[0];
                if (doc.category != null && !doc.category.Contains(categoryFilter) &&
                    !toolCategories[selectedToolCategory].Contains(doc.category))
                {
                    return false;
                }
            }

            // 検索フィルター
            if (!string.IsNullOrEmpty(toolSearchQuery))
            {
                string query = toolSearchQuery.ToLower();
                return doc.toolName.ToLower().Contains(query) ||
                       doc.toolNameJP.Contains(query) ||
                       (doc.description != null && doc.description.ToLower().Contains(query)) ||
                       (doc.descriptionJP != null && doc.descriptionJP.Contains(query));
            }

            return true;
        }

        private void DrawToolDetails()
        {
            if (string.IsNullOrEmpty(selectedTool))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.Space(50);
                EditorGUILayout.LabelField("← 左のリストからツールを選択してください", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField("← Select a tool from the list on the left", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(50);
                EditorGUILayout.EndVertical();
                return;
            }

            var doc = NataneToonToolsDocumentation.GetDocumentation(selectedTool);
            if (doc == null)
            {
                EditorGUILayout.HelpBox("ドキュメントが見つかりません Documentation not found", MessageType.Warning);
                return;
            }

            // タイトル
            EditorGUILayout.LabelField(doc.toolNameJP, headerStyle);
            EditorGUILayout.LabelField(doc.toolName, EditorStyles.miniLabel);

            EditorGUILayout.Space(10);

            // 説明
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📝 説明 Description", subHeaderStyle);
            EditorGUILayout.LabelField(doc.descriptionJP, bodyStyle);
            if (!string.IsNullOrEmpty(doc.description))
            {
                EditorGUILayout.LabelField(doc.description, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 機能一覧
            if (doc.featuresJP != null && doc.featuresJP.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("✨ 主な機能 Key Features", subHeaderStyle);
                foreach (var feature in doc.featuresJP)
                {
                    EditorGUILayout.LabelField("• " + feature, bodyStyle);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);
            }

            // 使い方ステップ
            if (doc.steps != null && doc.steps.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("📖 使い方 How to Use", subHeaderStyle);

                for (int i = 0; i < doc.steps.Count; i++)
                {
                    var step = doc.steps[i];
                    EditorGUILayout.BeginVertical(tipBoxStyle);
                    EditorGUILayout.LabelField($"ステップ {i + 1}: {step.titleJP}", EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(step.title))
                    {
                        EditorGUILayout.LabelField($"Step {i + 1}: {step.title}", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField(step.descriptionJP, bodyStyle);
                    if (!string.IsNullOrEmpty(step.description))
                    {
                        EditorGUILayout.LabelField(step.description, EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(3);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);
            }

            // ヒント
            if (doc.tipsJP != null && doc.tipsJP.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("💡 ヒント Tips", subHeaderStyle);
                foreach (var tip in doc.tipsJP)
                {
                    EditorGUILayout.BeginHorizontal(tipBoxStyle);
                    EditorGUILayout.LabelField("💡", GUILayout.Width(20));
                    EditorGUILayout.LabelField(tip, bodyStyle);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // フッター - ツールを開くボタン
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (GUILayout.Button($"🚀 {doc.toolNameJP}を開く Open {doc.toolName}", GUILayout.Height(35)))
            {
                OpenTool(selectedTool);
            }
            EditorGUILayout.EndVertical();
        }

        private void OpenTool(string toolKey)
        {
            if (!NataneToolMenuPaths.TryOpenByToolKey(toolKey))
            {
                EditorUtility.DisplayDialog(
                    "情報 Info",
                    "このツールのメニュー項目が見つかりません\nMenu item not found for this tool",
                    "OK");
            }
        }

        // ==================== 用語集 ====================
        private void DrawGlossary()
        {
            DrawSection("📖 技術用語集 Technical Glossary", () =>
            {
                // 検索バー
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("🔍 検索 Search:", GUILayout.Width(100));
                searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("クリア Clear", GUILayout.Width(80)))
                {
                    searchQuery = "";
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                int displayCount = 0;
                foreach (var entry in glossary)
                {
                    if (string.IsNullOrEmpty(searchQuery) ||
                        entry.Key.ToLower().Contains(searchQuery.ToLower()) ||
                        entry.Value.ToLower().Contains(searchQuery.ToLower()))
                    {
                        DrawGlossaryEntry(entry.Key, entry.Value);
                        displayCount++;
                    }
                }

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField($"表示: {displayCount} / {glossary.Count} 用語", EditorStyles.miniLabel);
            });
        }

        // ==================== チュートリアル ====================
        private void DrawTutorials()
        {
            DrawSection("📚 ステップバイステップ・チュートリアル Step-by-Step Tutorials", () =>
            {
                var categories = new[] { "Beginner", "Intermediate", "Advanced", "VRChat" };

                foreach (var category in categories)
                {
                    var categoryTutorials = new List<Tutorial>();
                    foreach (var tut in tutorials.Values)
                    {
                        if (tut.category == category)
                        {
                            categoryTutorials.Add(tut);
                        }
                    }

                    if (categoryTutorials.Count > 0)
                    {
                        DrawSubSection($"{category} Tutorials", () =>
                        {
                            foreach (var tutorial in categoryTutorials)
                            {
                                DrawTutorial(tutorial);
                                EditorGUILayout.Space(5);
                            }
                        });
                    }
                }
            });
        }

        // ==================== トラブルシューティング ====================
        private void DrawTroubleshooting()
        {
            DrawSection("🔧 トラブルシューティングガイド Troubleshooting Guide", () =>
            {
                DrawProblemSolution(
                    "マテリアルが暗すぎる、または明るすぎる Material appears too dark or too bright",
                    new[]
                    {
                        "シーンライティングを確認 - Directional Lightがあることを確認 Check scene lighting - ensure you have a Directional Light",
                        "Shadow Receiveパラメーターを調整 (0-1) Adjust Shadow Receive parameter (0-1)",
                        "Light Influenceパラメーターを調整 (0-1) Adjust Light Influence parameter (0-1)",
                        "Shadow Colorを確認 - 明るくまたは暗く Check Shadow Color - make it lighter or darker",
                        "ライティング設定のAmbient Colorを確認 Verify Ambient Color in Lighting settings"
                    });

                DrawProblemSolution(
                    "アウトラインが表示されない Outline is not visible",
                    new[]
                    {
                        "Outline機能のチェックボックスを有効化 Enable the Outline feature checkbox",
                        "Outline Widthを増やす (0.1-0.2を試す) Increase Outline Width (try 0.1-0.2)",
                        "Outline Colorをマテリアルと対照的な色に変更 Change Outline Color to contrast with material",
                        "モデルの法線が正しいか確認 Check model's normals are correct",
                        "OpaqueまたはCutoutバリアントを使用していることを確認 Ensure you're using Opaque or Cutout variant"
                    });

                DrawProblemSolution(
                    "透明マテリアルが正しくレンダリングされない Transparent materials render incorrectly",
                    new[]
                    {
                        "Transparentバリアントシェーダーを使用 Use the Transparent variant shader",
                        "Render Queueを調整 (透明には3000を試す) Adjust Render Queue (try 3000 for transparency)",
                        "Z Writeが正しく設定されているか確認 Check Z Write is set correctly",
                        "必要に応じてCull Modeを有効/無効化 Enable/disable Cull Mode as needed",
                        "透明オブジェクトを後ろから前にソート Sort transparent objects back-to-front"
                    });

                DrawProblemSolution(
                    "テクスチャがぼやけている、またはピクセル化している Textures look blurry or pixelated",
                    new[]
                    {
                        "テクスチャのインポート設定を確認 Check texture import settings",
                        "テクスチャインポーターでMax Sizeを増やす Increase Max Size in texture importer",
                        "必要に応じてGenerate Mip Mapsを無効化 Disable Generate Mip Maps if needed",
                        "Filter Modeを確認 (Point/Bilinear/Trilinear) Check Filter Mode (Point/Bilinear/Trilinear)",
                        "テクスチャが十分な解像度であることを確認 Verify texture is high enough resolution"
                    });

                DrawProblemSolution(
                    "パフォーマンスが遅すぎる Performance is too slow",
                    new[]
                    {
                        "Material Validatorを使用して問題を確認 Use Material Validator to check issues",
                        "未使用の機能を無効化 (キーワード) Disable unused features (keywords)",
                        "テクスチャサイズを削減 Reduce texture sizes",
                        "テクスチャ圧縮を使用 Use texture compression",
                        "マテリアルインスペクターでPerformance評価を確認 Check Performance rating in material inspector",
                        "高コストな機能の使用を制限 (SSS, Reflection, Parallax) Limit use of expensive features (SSS, Reflection, Parallax)"
                    });

                DrawProblemSolution(
                    "VRChatアップロードが失敗、またはアバターが重すぎる VRChat upload fails or avatar is too heavy",
                    new[]
                    {
                        "VRChatチェック付きでMaterial Validatorを実行 Run Material Validator with VRChat checks",
                        "テクスチャサイズを2048x2048以下に削減 Reduce texture sizes to 2048x2048 or lower",
                        "テクスチャ圧縮を使用 (DXT/BC) Use texture compression (DXT/BC)",
                        "不要なシェーダー機能を無効化 Disable unnecessary shader features",
                        "可能な場合はマテリアルを結合 Combine materials where possible",
                        "テクスチャアトラスを使用してマテリアル数を削減 Use texture atlases to reduce material count"
                    });
            });
        }

        // ==================== ヒント ====================
        private void DrawTips()
        {
            DrawSection("💡 ヒントとベストプラクティス Tips & Best Practices", () =>
            {
                DrawTipCategory("パフォーマンス Performance", new[]
                {
                    "使用しない機能は無効化 - 有効化された各機能にはパフォーマンスコストがあります Disable features you don't use - each enabled feature has a performance cost",
                    "Material Validatorを定期的に使用して早期に問題を発見 Use Material Validator regularly to catch issues early",
                    "VRではPerformance Rating BまたはA以上を目指す Aim for Performance Rating B or better for VR",
                    "テクスチャサイズは大きな影響 - 見た目が良い最小サイズを使用 Texture size has huge impact - use smallest size that looks good",
                    "メモリ使用量を改善するためテクスチャ圧縮 (DXT/ASTC)を使用 Use texture compression (DXT/ASTC) for better memory usage"
                });

                DrawTipCategory("ワークフロー Workflow", new[]
                {
                    "プリセットから始めてカスタマイズ - 時間を節約し良いデフォルトを確保 Start with presets, then customize - saves time and ensures good defaults",
                    "カスタマイズしたマテリアルを新しいプリセットとして保存して再利用 Save your customized materials as new presets for reuse",
                    "クリップボードコピー/ペーストを使用して設定を素早く転送 Use clipboard copy/paste to quickly transfer settings",
                    "チーム共有のためマテリアルをファイルにエクスポート Export materials to files for team sharing",
                    "お気に入りの設定のライブラリを保持 Keep a library of your favorite settings"
                });

                DrawTipCategory("ビジュアル品質 Visual Quality", new[]
                {
                    "Toon Stepsでセルシェーディングを制御 - アニメ調には2-3ステップ Toon Steps controls cel-shading - 2-3 steps for anime look",
                    "Toon Sharpnessでシャドウのエッジを制御 - 高い値=シャープ Toon Sharpness controls shadow edges - higher = sharper",
                    "Rim Lightを使用してキャラクターを背景から際立たせる Use Rim Light to make characters pop from background",
                    "SSS (Subsurface Scattering)で肌や葉をよりリアルに SSS (Subsurface Scattering) makes skin and leaves more realistic",
                    "MatCapで安価に偽の反射を追加 MatCap can add fake reflections cheaply"
                });

                DrawTipCategory("VRChat固有 VRChat Specific", new[]
                {
                    "確定前にVRChatでテスト - UnityとライティングLightingが異なります Test in VRChat before finalizing - lighting differs from Unity",
                    "Editor Prewarmingを使用 - ランタイムスクリプト不要 Use Editor Prewarming - no runtime scripts needed",
                    "マテリアルあたりの総テクスチャメモリを40MB以下に Keep total texture memory under 40MB per material",
                    "最低目標としてPerformance Rank Good (PC)を使用 Use Performance Rank Good (PC) as minimum target",
                    "モバイルVRをターゲットにする場合はQuestでテスト Test on Quest if targeting mobile VR"
                });

                DrawTipCategory("学習 Learning", new[]
                {
                    "デフォルトプリセットを調べてパラメーターの組み合わせを学ぶ Examine default presets to learn parameter combinations",
                    "Performanceインジケーターを使用して機能コストを理解 Use Performance indicator to understand feature costs",
                    "Material Validatorでマテリアルを比較 Compare materials with Material Validator",
                    "用語集を読んで技術用語を理解 Read glossary to understand technical terms",
                    "実験しましょう！変更をテストする前にコピーを作成 Experiment! Make copies before testing changes"
                });
            });
        }

        // ==================== ドキュメント ====================
        private void DrawDocumentation()
        {
            DrawSection("📄 追加ドキュメント Additional Documentation", () =>
            {
                EditorGUILayout.LabelField("以下のドキュメントファイルをエクスプローラーで開きます Open these documentation files in your file explorer", EditorStyles.miniLabel);
                EditorGUILayout.Space(10);

                if (GUILayout.Button("📖 README.md - メインドキュメント Main Documentation", GUILayout.Height(40)))
                {
                    OpenDocumentationFile("README.md");
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("🔧 TECHNICAL.md - 技術仕様 Technical Specifications", GUILayout.Height(40)))
                {
                    OpenDocumentationFile("TECHNICAL.md");
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("🚀 QUICK_START.md - 5分クイックスタート 5-Minute Quick Start", GUILayout.Height(40)))
                {
                    OpenDocumentationFile("QUICK_START.md");
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("🔄 MIGRATION_GUIDE.md - マイグレーションガイド Migration Guide", GUILayout.Height(40)))
                {
                    OpenDocumentationFile("MIGRATION_GUIDE.md");
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("📂 FOLDER_STRUCTURE.md - フォルダ構造 Folder Structure", GUILayout.Height(40)))
                {
                    OpenDocumentationFile("FOLDER_STRUCTURE.md");
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("📋 CHANGELOG.md - 変更履歴 Change Log", GUILayout.Height(40)))
                {
                    OpenDocumentationFile("CHANGELOG.md");
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("✨ PARTICLE_SYSTEM_GUIDE.md - パーティクルシステムガイド Particle System Guide", GUILayout.Height(40)))
                {
                    OpenDocumentationFile("PARTICLE_SYSTEM_GUIDE.md");
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("🎨 SHADER_VARIANTS.md - シェーダーバリアント最適化 Shader Variants Optimization", GUILayout.Height(40)))
                {
                    OpenDocumentationFile("SHADER_VARIANTS.md");
                }
            });
        }

        // ==================== UI Helper Methods ====================
        private void DrawSection(string title, System.Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, headerStyle);
            EditorGUILayout.Space(5);
            content();
            EditorGUILayout.EndVertical();
        }

        private void DrawSubSection(string title, System.Action content)
        {
            EditorGUILayout.LabelField(title, subHeaderStyle);
            EditorGUI.indentLevel++;
            content();
            EditorGUI.indentLevel--;
        }

        private void DrawStep(string number, string text)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(number + ".", EditorStyles.boldLabel, GUILayout.Width(25));
            EditorGUILayout.LabelField(text, bodyStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBullet(string text)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("•", EditorStyles.boldLabel, GUILayout.Width(15));
            EditorGUILayout.LabelField(text, bodyStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWorkflow(string workflowName, string[] steps)
        {
            EditorGUILayout.LabelField("→ " + workflowName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (var step in steps)
            {
                DrawBullet(step);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawGlossaryEntry(string term, string definition)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(term, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(definition, bodyStyle);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        private void DrawTutorial(Tutorial tutorial)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📖 " + tutorial.title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(tutorial.description, bodyStyle);
            EditorGUILayout.Space(5);

            for (int i = 0; i < tutorial.steps.Count; i++)
            {
                DrawStep((i + 1).ToString(), tutorial.steps[i]);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProblemSolution(string problem, string[] solutions)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("❓ " + problem, EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("解決策 Solutions:", EditorStyles.miniBoldLabel);
            foreach (var solution in solutions)
            {
                DrawBullet(solution);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void DrawTipCategory(string category, string[] tips)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("💡 " + category, EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            foreach (var tip in tips)
            {
                DrawBullet(tip);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private static void ApplyBackground(GUIStyle style, Texture2D background)
        {
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
        }

        private void OpenDocumentationFile(string filename)
        {
            string packagePath = "Packages/com.natane.toonshader/" + filename;
            string fullPath = System.IO.Path.GetFullPath(packagePath);

            if (System.IO.File.Exists(fullPath))
            {
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                // フォールバック: Assets内を探す
                string assetsPath = Application.dataPath + "/../" + filename;
                if (System.IO.File.Exists(assetsPath))
                {
                    EditorUtility.RevealInFinder(assetsPath);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "ファイルが見つかりません File Not Found",
                        $"{filename}が見つかりませんでした。\nパッケージディレクトリを確認してください。\n\n{filename} was not found.\nPlease check your package directory.",
                        "OK"
                    );
                }
            }
        }

        // ==================== Data Initialization ====================
        private void InitializeGlossary()
        {
            glossary = new Dictionary<string, string>
            {
                { "Cel Shading / Toon Shading", "Non-photorealistic rendering technique that creates flat, cartoon-like shading with distinct color regions instead of smooth gradients.\nセルシェーディング/トゥーンシェーディング：滑らかなグラデーションの代わりに、明確な色の領域を持つ平坦なカートゥーン風のシェーディングを作成する非フォトリアリスティックレンダリング技術。" },
                { "NPR (Non-Photorealistic Rendering)", "Rendering style that intentionally avoids photorealism, used for artistic effects like cartoon, sketch, or painterly looks.\n非フォトリアリスティックレンダリング：意図的にフォトリアリズムを避けたレンダリングスタイル。カートゥーン、スケッチ、ペイント風などの芸術的効果に使用されます。" },
                { "Toon Steps", "Number of discrete shading levels in cel shading. 2-3 steps creates classic anime look, more steps creates softer shading.\nトゥーンステップ：セルシェーディングの離散的なシェーディングレベルの数。2-3ステップで古典的なアニメ調、より多くのステップで柔らかいシェーディングになります。" },
                { "Toon Sharpness", "Controls how sharp the transition between shading levels is. Higher = harder edges, lower = softer transitions.\nトゥーンシャープネス：シェーディングレベル間の遷移の鋭さを制御。高い値=よりハードなエッジ、低い値=より柔らかい遷移。" },
                { "Rim Light", "Lighting effect that highlights edges of objects, making them stand out from the background. Creates a 'halo' effect.\nリムライト：オブジェクトのエッジをハイライトするライティング効果。背景から際立たせ、「ハロー」効果を生み出します。" },
                { "SSS (Subsurface Scattering)", "Light penetrating and scattering beneath a surface, creating soft translucent effect. Important for skin, leaves, wax.\nサブサーフェススキャタリング：表面下で光が浸透・散乱し、柔らかい半透明効果を生み出します。肌、葉、ワックスに重要。" },
                { "MatCap (Material Capture)", "Texture that encodes lighting information in a sphere, providing cheap fake reflections and lighting.\nマットキャップ：球体にライティング情報をエンコードしたテクスチャ。安価な偽の反射とライティングを提供します。" },
                { "Fresnel Effect", "Optical phenomenon where reflections become stronger at glancing angles. Makes edges more reflective than flat surfaces.\nフレネル効果：視線角が浅い角度で反射が強くなる光学現象。平面よりエッジをより反射的にします。" },
                { "Parallax Occlusion Mapping (POM)", "Technique that simulates depth on a flat surface using height maps, creating 3D relief without actual geometry.\nパララックスオクルージョンマッピング：ハイトマップを使用して平面上の深度をシミュレートし、実際のジオメトリなしで3Dレリーフを作成する技術。" },
                { "Cubemap / Environment Map", "360-degree texture used for reflections and environment lighting, captured from or representing surroundings.\nキューブマップ/環境マップ：反射と環境ライティングに使用される360度テクスチャ。周囲からキャプチャまたは表現されたもの。" },
                { "Refraction", "Bending of light as it passes through transparent materials like glass or water.\n屈折：ガラスや水などの透明な素材を通過する際の光の屈折。" },
                { "IOR (Index of Refraction)", "Number that describes how much light bends when entering a material. Glass ≈ 1.5, Water ≈ 1.33, Diamond ≈ 2.4.\n屈折率：素材に入るときに光がどれだけ曲がるかを示す数値。ガラス≈1.5、水≈1.33、ダイヤモンド≈2.4。" },
                { "Specular Highlight", "Bright spot of reflected light on shiny surfaces, representing direct reflection of light source.\nスペキュラーハイライト：光源の直接反射を表す、光沢のある表面上の明るい反射スポット。" },
                { "Shader Keyword", "Compiler directive that includes/excludes code blocks, allowing features to be toggled without performance cost when disabled.\nシェーダーキーワード：コードブロックを含める/除外するコンパイラディレクティブ。無効時にパフォーマンスコストなしで機能を切り替え可能。" },
                { "Render Queue", "Order in which materials are rendered. Opaque (2000), AlphaTest (2450), Transparent (3000). Lower renders first.\nレンダーキュー：マテリアルがレンダリングされる順序。Opaque (2000)、AlphaTest (2450)、Transparent (3000)。低い値が先にレンダリング。" },
                { "Cull Mode", "Which face of triangles to render. Back (default) = front facing only, Front = back facing only, Off = both sides.\nカルモード：三角形のどの面をレンダリングするか。Back (デフォルト) = 前面のみ、Front = 背面のみ、Off = 両面。" },
                { "Z Write", "Whether to write depth information. Usually on for opaque, off for transparent objects.\nZライト：深度情報を書き込むかどうか。通常、不透明はオン、透明オブジェクトはオフ。" },
                { "Shader Variant", "Specific compiled version of shader with certain features enabled/disabled. Each keyword combination creates a variant.\nシェーダーバリアント：特定の機能が有効/無効になったシェーダーのコンパイル済みバージョン。各キーワードの組み合わせがバリアントを作成。" },
                { "GPU Instancing", "Technique to render multiple copies of same mesh/material with single draw call, greatly improving performance.\nGPUインスタンシング：1回のドローコールで同じメッシュ/マテリアルの複数コピーをレンダリングする技術。パフォーマンスを大幅に向上させます。" },
                { "Emission", "Self-illumination of material, making it glow. Can be HDR (>1.0) for bloom effects.\nエミッション：マテリアルの自己発光。グローする効果。ブルーム効果にはHDR (>1.0)が可能。" },
                { "Normal Map / Bump Map", "Texture that encodes surface detail information, creating illusion of bumps and grooves without actual geometry.\n法線マップ/バンプマップ：表面の詳細情報をエンコードしたテクスチャ。実際のジオメトリなしで凹凸の錯覚を作り出します。" },
                { "Mask Texture", "Grayscale texture controlling where/how much an effect applies. White = full effect, black = no effect, gray = partial.\nマスクテクスチャ：効果が適用される場所/量を制御するグレースケールテクスチャ。白=完全な効果、黒=効果なし、灰色=部分的。" },
                { "Dissolve Effect", "Animated transition that makes objects appear/disappear using noise patterns, common in VR avatar systems.\nディゾルブ効果：ノイズパターンを使用してオブジェクトを出現/消失させるアニメーション遷移。VRアバターシステムで一般的。" },
                { "Hue Shift", "Color manipulation that rotates colors around the color wheel, allowing dynamic recoloring.\n色相シフト：カラーホイールの周りで色を回転させる色操作。動的な再着色を可能にします。" }
            };
        }

        private void InitializeTutorials()
        {
            tutorials = new Dictionary<string, Tutorial>
            {
                {
                    "basic_setup",
                    new Tutorial
                    {
                        title = "Basic Material Setup 基本的なマテリアル設定",
                        category = "Beginner",
                        description = "Create your first toon material from scratch 最初のトゥーンマテリアルをゼロから作成",
                        steps = new List<string>
                        {
                            "新しいマテリアルを作成 (右クリック > Create > Material) Create a new material (Right-click > Create > Material)",
                            "シェーダーを選択: Natane/Toon Shader (またはバリアント) Select the shader: Natane/Toon Shader (or variant)",
                            "Main Textureを割り当て (オプションだが推奨) Assign a Main Texture (optional but recommended)",
                            "Main Colorを希望のベースカラーに設定 Set Main Color to your desired base color",
                            "Shadow Colorをアートスタイルに合わせて設定 Set Shadow Color to match your art style",
                            "Toon Stepsを2に設定してアニメ調に Set Toon Steps to 2 for anime look",
                            "モデルに適用してScene viewでテスト Apply to your model and test in Scene view"
                        }
                    }
                },
                {
                    "using_presets",
                    new Tutorial
                    {
                        title = "Using Material Presets マテリアルプリセットの使用",
                        category = "Beginner",
                        description = "Quickest way to get professional results プロフェッショナルな結果を得る最速の方法",
                        steps = new List<string>
                        {
                            "初回のみ実行: Tools > Natane > Generate Default Presets Run: Tools > Natane > Generate Default Presets (first time only)",
                            "開く: Tools > Natane > Material Preset Browser Open: Tools > Natane > Material Preset Browser",
                            "Target Materialフィールドでマテリアルを選択 Select your material in the Target Material field",
                            "プリセットを閲覧 - カテゴリフィルターで絞り込み Browse presets - use Category filter to narrow down",
                            "ニーズに合ったプリセットでApplyをクリック Click Apply on a preset that fits your needs",
                            "Material Inspectorでパラメーターを微調整 Fine-tune parameters in Material Inspector",
                            "設定を再利用したい場合は新しいプリセットとして保存 Save as new preset if you want to reuse these settings"
                        }
                    }
                },
                {
                    "character_skin",
                    new Tutorial
                    {
                        title = "Creating Realistic Skin Material リアルな肌マテリアルの作成",
                        category = "Intermediate",
                        description = "Set up skin with SSS for believable character appearance 信頼できるキャラクター外観のためにSSSで肌を設定",
                        steps = new List<string>
                        {
                            "Character_Skin_Softプリセットから始める Start with Character_Skin_Soft preset",
                            "SSS (Subsurface Scattering)機能を有効化 Enable SSS (Subsurface Scattering) feature",
                            "SSS Colorを赤みがかったオレンジに設定 (肌の下の血を模倣) Set SSS Color to reddish-orange (simulates blood under skin)",
                            "SSS Intensityを調整 (0.3-0.6が効果的) Adjust SSS Intensity (0.3-0.6 works well)",
                            "エッジ定義のためRim Lightを有効化 Enable Rim Light for edge definition",
                            "Rim Colorを肌より少し明るく設定 Set Rim Color to slightly brighter than skin",
                            "肌が少し光沢を持つべきなら微妙なSpecularを追加 Add subtle Specular if skin should be slightly shiny",
                            "異なるライティング条件でテスト Test under different lighting conditions"
                        }
                    }
                },
                {
                    "sharing_params",
                    new Tutorial
                    {
                        title = "Sharing Material Parameters マテリアルパラメーターの共有",
                        category = "Beginner",
                        description = "Collaborate with team members チームメンバーと協力",
                        steps = new List<string>
                        {
                            "共有したいマテリアルを選択 Select material you want to share",
                            "Material Inspectorで'Export to File'をクリック Click 'Export to File' in Material Inspector",
                            "場所を選択し.ntmaterialファイルとして保存 Choose location and save as .ntmaterial file",
                            "ファイルをチームと共有 (メール、Slack、Gitなど) Share file with team (email, Slack, Git, etc.)",
                            "チームメンバーがMaterial Preset Browserを開く Team member opens Material Preset Browser",
                            "'Import from File'をクリックし.ntmaterialを選択 Click 'Import from File' and select the .ntmaterial",
                            "自分のマテリアルに適用 - 完了！ Apply to their material - done!"
                        }
                    }
                },
                {
                    "vrchat_optimization",
                    new Tutorial
                    {
                        title = "VRChat Optimization VRChat最適化",
                        category = "VRChat",
                        description = "Optimize materials for VRChat performance VRChatパフォーマンスのためマテリアルを最適化",
                        steps = new List<string>
                        {
                            "すべてのマテリアルをMaterial Validatorに追加 Add all materials to Material Validator",
                            "VRChat Optimizationチェックを有効化 Enable VRChat Optimization check",
                            "Validate Allをクリック Click Validate All",
                            "エラー (赤) と警告 (黄) を確認 Review errors (red) and warnings (yellow)",
                            "修正可能な問題にはAuto-Fixを使用 Use Auto-Fix for fixable issues",
                            "大きすぎるとフラグされたテクスチャを手動でリサイズ Manually resize textures flagged as too large",
                            "未使用の機能を無効化してバリアントを削減 Disable unused features to reduce variants",
                            "緑/情報メッセージのみが残るまで再検証 Re-validate until only green/info messages remain",
                            "Tools > Natane > Shader Prewarming > Settingsを使用 Use Tools > Natane > Shader Prewarming > Settings"
                        }
                    }
                },
                {
                    "glass_effect",
                    new Tutorial
                    {
                        title = "Creating Glass Material ガラスマテリアルの作成",
                        category = "Advanced",
                        description = "Realistic glass with refraction and reflection 屈折と反射のあるリアルなガラス",
                        steps = new List<string>
                        {
                            "Transparentシェーダーバリアントを使用 Use Transparent shader variant",
                            "Effects_Glass_Clearプリセットから始める Start with Effects_Glass_Clear preset",
                            "Refraction機能を有効化 Enable Refraction feature",
                            "Refraction Indexを1.5 (ガラス)に設定 Set Refraction Index to 1.5 (glass)",
                            "Reflection機能を有効化 Enable Reflection feature",
                            "Reflection cubemapを割り当て Assign reflection cubemap",
                            "Smoothnessを0.95-1.0に設定 Set Smoothness to 0.95-1.0",
                            "ハイライトのため微妙なSpecularを追加 Add subtle Specular for highlights",
                            "透明度を制御するためalphaを調整 Adjust alpha to control transparency",
                            "ガラスの後ろのオブジェクトでテスト Test with objects behind glass"
                        }
                    }
                }
            };
        }
    }

    // Note: HelpButton class is defined in InteractiveHelpSystem.cs to avoid duplication
    // HelpButtonクラスはInteractiveHelpSystem.csで定義されています（重複を避けるため）
}
