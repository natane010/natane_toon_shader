using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NataneToon.MaterialSystem;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    using static NataneUIConstants;

    /// <summary>
    /// Material Preset Browser for easy material management
    /// Provides visual preset selection, filtering, and application
    /// </summary>
    public class MaterialPresetBrowser : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchQuery = "";
        private PresetCategory selectedCategory = PresetCategory.Custom;
        private List<NataneToonMaterialPreset> allPresets = new List<NataneToonMaterialPreset>();
        private List<NataneToonMaterialPreset> filteredPresets = new List<NataneToonMaterialPreset>();
        private Material selectedMaterial;
        private NataneToonMaterialPreset selectedPreset;
        private bool showAllCategories = true;

        private const float THUMBNAIL_MAX_SIZE = 132f;
        private const float PRESET_CARD_MIN_HEIGHT = 188f;
        private const float MIN_CARD_WIDTH = 170f;
        private const float GRID_SPACING = 8f;
        private static List<NataneToonMaterialPreset> cachedPresets = new List<NataneToonMaterialPreset>();
        private static bool presetCacheInitialized;
        private static bool presetCacheDirty = true;
        private static bool presetCacheSubscribed;

        private static string WindowTitle => L("マテリアルプリセット", "Material Presets");

        private static GUIStyle _vtuberToolbarButtonStyle;
        private static GUIStyle VtuberToolbarButtonStyle
        {
            get
            {
                if (_vtuberToolbarButtonStyle == null)
                {
                    _vtuberToolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
                    {
                        fontStyle = FontStyle.Bold
                    };
                    _vtuberToolbarButtonStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                }

                return _vtuberToolbarButtonStyle;
            }
        }

        private static GUIStyle _presetNameStyle;
        private static GUIStyle PresetNameStyle
        {
            get
            {
                if (_presetNameStyle == null)
                {
                    _presetNameStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 10,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };
                }

                return _presetNameStyle;
            }
        }

        [MenuItem("Tools/Natane/プリセット Presets/マテリアルプリセットブラウザ Material Preset Browser _p", false, 21)]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialPresetBrowser>(WindowTitle);
            window.minSize = new Vector2(WINDOW_WIDTH_STANDARD, 400);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshPresetList();

            // Auto-select material if one is selected in the project
            if (Selection.activeObject is Material)
            {
                selectedMaterial = Selection.activeObject as Material;
            }
        }

        private void OnGUI()
        {
            NataneToonShaderGUIUtility.DrawToolHeader("マテリアルプリセットブラウザ", "Material Preset Browser", "MaterialPresetBrowser");
            EditorGUILayout.Space(SPACE_SMALL);
            DrawToolbar();
            EditorGUILayout.Space(SPACE_SMALL);
            DrawMaterialSelection();
            EditorGUILayout.Space(SPACE_SMALL);
            DrawFilterBar();
            EditorGUILayout.Space(SPACE_STANDARD);
            DrawPresetGrid();
        }

        private void DrawToolbar()
        {
            bool compactToolbar = position.width < 760f;
            EditorGUILayout.BeginVertical(EditorStyles.toolbar);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("更新", "Refresh"), EditorStyles.toolbarButton))
                {
                    RefreshPresetList(forceRefresh: true);
                }

                if (GUILayout.Button(L("プリセット作成", "Create Preset"), EditorStyles.toolbarButton))
                {
                    ShowCreatePresetDialog();
                }

                if (!compactToolbar &&
                    GUILayout.Button(
                        new GUIContent(
                            L("VTuberプリセット生成", "Generate VTuber Presets"),
                            L("VTuber向けに最適化されたマテリアルプリセットを生成します", "Generate VTuber-optimized material presets")),
                        VtuberToolbarButtonStyle))
                {
                    GenerateVTuberPresets();
                }

                if (GUILayout.Button(L("ファイルから読み込み", "Import from File"), EditorStyles.toolbarButton))
                {
                    ImportMaterialFromFile();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(L("ヘルプ", "Help"), EditorStyles.toolbarButton, GUILayout.MinWidth(60f)))
                {
                    ShowHelp();
                }
            }

            if (compactToolbar)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(
                        new GUIContent(
                            L("VTuberプリセット生成", "Generate VTuber Presets"),
                            L("VTuber向けに最適化されたマテリアルプリセットを生成します", "Generate VTuber-optimized material presets")),
                        VtuberToolbarButtonStyle,
                        GUILayout.MinWidth(220f)))
                    {
                        GenerateVTuberPresets();
                    }
                    GUILayout.FlexibleSpace();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("対象マテリアル", "Target Material"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            selectedMaterial = (Material)EditorGUILayout.ObjectField(
                selectedMaterial,
                typeof(Material),
                false,
                GUILayout.Height(60));

            if (EditorGUI.EndChangeCheck() && selectedMaterial != null)
            {
                if (!selectedMaterial.shader.name.Contains("Natane") || !selectedMaterial.shader.name.Contains("Toon"))
                {
                    EditorUtility.DisplayDialog(
                        L("警告", "Warning"),
                        L($"選択したマテリアル '{selectedMaterial.name}' は Natane Toon Shader を使用していません。\n\nプリセットが正しく適用されない可能性があります。", $"Selected material '{selectedMaterial.name}' is not using Natane Toon Shader.\n\nPresets may not apply correctly."),
                        "OK");
                }
            }

            DrawMaterialActionButtons(position.width < 760f);
            DrawMaterialSelectionStatus();

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialActionButtons(bool compactActions)
        {
            using (new EditorGUI.DisabledScope(selectedMaterial == null))
            {
                if (compactActions)
                {
                    DrawMaterialActionButton(L("このマテリアルからプリセット作成", "Create Preset from This Material"), () => CreatePresetFromMaterial(selectedMaterial));
                    DrawMaterialActionButton(L("ファイルへ書き出し", "Export to File"), () => ExportMaterialToFile(selectedMaterial));
                    DrawMaterialActionButton(L("クリップボードへコピー", "Copy to Clipboard"), () => CopyMaterialToClipboard(selectedMaterial));
                    DrawMaterialActionButton(L("クリップボードから貼り付け", "Paste from Clipboard"), () => PasteMaterialFromClipboard(selectedMaterial));
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                DrawMaterialActionButton(L("このマテリアルからプリセット作成", "Create Preset from This Material"), () => CreatePresetFromMaterial(selectedMaterial));
                DrawMaterialActionButton(L("ファイルへ書き出し", "Export to File"), () => ExportMaterialToFile(selectedMaterial));
                DrawMaterialActionButton(L("クリップボードへコピー", "Copy to Clipboard"), () => CopyMaterialToClipboard(selectedMaterial));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawMaterialActionButton(L("クリップボードから貼り付け", "Paste from Clipboard"), () => PasteMaterialFromClipboard(selectedMaterial));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawMaterialActionButton(string label, System.Action action)
        {
            if (GUILayout.Button(label, GUILayout.Height(BUTTON_HEIGHT_STANDARD)))
            {
                action?.Invoke();
            }
        }

        private void DrawMaterialSelectionStatus()
        {
            if (selectedMaterial == null)
            {
                EditorGUILayout.HelpBox(L("プリセット適用先のマテリアルを選択してください", "Select a material to apply presets"), MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(L($"選択中: {selectedMaterial.name}", $"Selected: {selectedMaterial.name}"), MessageType.Info);
        }

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                showAllCategories = GUILayout.Toggle(showAllCategories, L("すべて", "All"), EditorStyles.toolbarButton, GUILayout.MinWidth(50f));
                if (EditorGUI.EndChangeCheck())
                {
                    FilterPresets();
                }

                using (new EditorGUI.DisabledScope(showAllCategories))
                {
                    EditorGUILayout.LabelField(L("カテゴリ", "Category") + ":", GUILayout.Width(70));
                    EditorGUI.BeginChangeCheck();
                    selectedCategory = (PresetCategory)EditorGUILayout.EnumPopup(selectedCategory, GUILayout.MinWidth(position.width < 760f ? 150f : 220f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        showAllCategories = false;
                        FilterPresets();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L("検索", "Search") + ":", GUILayout.Width(55));
                EditorGUI.BeginChangeCheck();
                searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                {
                    FilterPresets();
                }

                if (GUILayout.Button(L("クリア", "Clear"), GUILayout.MinWidth(65f)))
                {
                    searchQuery = "";
                    showAllCategories = true;
                    FilterPresets();
                }
            }

            EditorGUILayout.LabelField(
                L($"{filteredPresets.Count} presets found", $"{filteredPresets.Count} presets found"),
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetGrid()
        {
            if (filteredPresets.Count == 0)
            {
                EditorGUILayout.HelpBox(L("プリセットが見つかりません。作成するかフィルター条件を調整してください。", "No presets found. Create one or adjust filters."), MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            float availableWidth = Mathf.Max(MIN_CARD_WIDTH, position.width - 32f);
            int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + GRID_SPACING) / (MIN_CARD_WIDTH + GRID_SPACING)));
            float cardWidth = Mathf.Max(MIN_CARD_WIDTH, (availableWidth - ((columns - 1) * GRID_SPACING)) / columns);
            int rows = Mathf.CeilToInt((float)filteredPresets.Count / columns);

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();

                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= filteredPresets.Count) break;

                    DrawPresetCard(filteredPresets[index], cardWidth);

                    if (col < columns - 1)
                    {
                        GUILayout.Space(GRID_SPACING);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPresetCard(NataneToonMaterialPreset preset, float cardWidth)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(cardWidth), GUILayout.MinHeight(PRESET_CARD_MIN_HEIGHT));

            // Thumbnail
            float thumbnailSize = Mathf.Clamp(cardWidth - 24f, 110f, THUMBNAIL_MAX_SIZE);
            Rect thumbnailLayoutRect = GUILayoutUtility.GetRect(cardWidth - 20f, thumbnailSize, GUILayout.ExpandWidth(true));
            Rect thumbnailRect = new Rect(
                thumbnailLayoutRect.x + Mathf.Max(0f, (thumbnailLayoutRect.width - thumbnailSize) * 0.5f),
                thumbnailLayoutRect.y,
                thumbnailSize,
                thumbnailSize);
            if (preset.thumbnail != null)
            {
                GUI.DrawTexture(thumbnailRect, preset.thumbnail, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(thumbnailRect, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(thumbnailRect, L("プレビューなし", "No Preview"), EditorStyles.centeredGreyMiniLabel);
            }

            // Preset name
            EditorGUILayout.LabelField(preset.presetName, PresetNameStyle, GUILayout.MinHeight(34f));
            EditorGUILayout.Space(4f);
            GUILayout.FlexibleSpace();

            // Apply button
            bool canApply = selectedMaterial != null;
            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (GUILayout.Button(L("適用", "Apply"), GUILayout.Height(24)))
                {
                    ApplyPreset(preset);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void EnsurePresetCacheSubscription()
        {
            if (presetCacheSubscribed)
            {
                return;
            }

            EditorApplication.projectChanged += InvalidatePresetCache;
            presetCacheSubscribed = true;
        }

        private static void InvalidatePresetCache()
        {
            presetCacheDirty = true;
        }

        private void RefreshPresetList(bool forceRefresh = false)
        {
            EnsurePresetCacheSubscription();

            if (!forceRefresh && presetCacheInitialized && !presetCacheDirty)
            {
                allPresets = cachedPresets.Where(preset => preset != null).ToList();
                FilterPresets();
                return;
            }

            allPresets.Clear();

            string[] guids = AssetDatabase.FindAssets("t:NataneToonMaterialPreset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<NataneToonMaterialPreset>(path);
                if (preset != null)
                {
                    allPresets.Add(preset);
                }
            }

            allPresets = allPresets.OrderBy(p => p.category).ThenBy(p => p.presetName).ToList();
            cachedPresets = allPresets.Where(preset => preset != null).ToList();
            presetCacheInitialized = true;
            presetCacheDirty = false;
            FilterPresets();

            Debug.Log($"[MaterialPresetBrowser] Found {allPresets.Count} presets");
        }

        private void FilterPresets()
        {
            filteredPresets = allPresets.Where(p =>
            {
                // Category filter
                bool categoryMatch = showAllCategories || p.category == selectedCategory;

                // Search filter
                bool searchMatch = string.IsNullOrEmpty(searchQuery) ||
                                   (!string.IsNullOrEmpty(p.presetName) && p.presetName.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                                   (!string.IsNullOrEmpty(p.description) && p.description.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0);

                return categoryMatch && searchMatch;
            }).ToList();
        }

        private void ApplyPreset(NataneToonMaterialPreset preset)
        {
            if (selectedMaterial == null || preset == null) return;

            Undo.RecordObject(selectedMaterial, "Apply Material Preset");
            NataneToonMaterialPresetEditor.ApplyPresetWithUIUpdate(preset, selectedMaterial);

            EditorUtility.DisplayDialog(
                L("プリセット適用完了", "Preset Applied"),
                L($"Successfully applied preset '{preset.presetName}' to material '{selectedMaterial.name}'\n", $"Successfully applied preset '{preset.presetName}' to material '{selectedMaterial.name}'\n" +
                $"Inspector UI has been updated to show active features."),
                "OK");
        }

        private void ShowCreatePresetDialog()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                L("マテリアルプリセットを作成", "Create Material Preset"),
                L("新しいマテリアルプリセット", "New Material Preset"),
                "asset",
                L("プリセットの保存先を選択してください", "Choose where to save the preset"));

            if (!string.IsNullOrEmpty(path))
            {
                var preset = CreateInstance<NataneToonMaterialPreset>();
                preset.presetName = Path.GetFileNameWithoutExtension(path);
                preset.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");

                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();

                EditorGUIUtility.PingObject(preset);
                Selection.activeObject = preset;

                RefreshPresetList(forceRefresh: true);
            }
        }

        private void CreatePresetFromMaterial(Material material)
        {
            string defaultName = $"{material.name}_Preset";
            string path = EditorUtility.SaveFilePanelInProject(
                L("マテリアルからプリセット作成", "Create Preset from Material"),
                defaultName,
                "asset",
                L("プリセットの保存先を選択してください", "Choose where to save the preset"));

            if (!string.IsNullOrEmpty(path))
            {
                var preset = CreateInstance<NataneToonMaterialPreset>();
                preset.presetName = Path.GetFileNameWithoutExtension(path);
                preset.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");
                preset.CreateFromMaterial(material);

                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssets();

                EditorGUIUtility.PingObject(preset);
                RefreshPresetList(forceRefresh: true);

                EditorUtility.DisplayDialog(
                    L("プリセット作成完了", "Preset Created"),
                    L($"マテリアル '{material.name}' からプリセット '{preset.presetName}' を作成しました。", $"Created preset '{preset.presetName}' from material '{material.name}'"),
                    "OK");
            }
        }

        private void ExportMaterialToFile(Material material)
        {
            string defaultFolder = MaterialParameterShareSystem.GetDefaultExportFolder();
            string defaultName = $"{material.name}_Parameters";
            string extension = MaterialParameterShareSystem.GetFileExtension();

            string path = EditorUtility.SaveFilePanel(
                L("マテリアルパラメータを書き出し", "Export Material Parameters"),
                defaultFolder,
                defaultName,
                extension.TrimStart('.'));

            if (!string.IsNullOrEmpty(path))
            {
                string result = MaterialParameterShareSystem.ExportToFile(material, path);
                if (!string.IsNullOrEmpty(result))
                {
                    EditorUtility.DisplayDialog(
                        L("書き出し完了", "Export Successful"),
                        L($"マテリアルパラメータを書き出しました:\n{result}", $"Material parameters exported to:\n{result}"),
                        "OK");
                }
            }
        }

        private void ImportMaterialFromFile()
        {
            if (selectedMaterial == null)
            {
                EditorUtility.DisplayDialog(
                    L("マテリアル未選択", "No Material Selected"),
                    L("先に対象マテリアルを選択してください", "Please select a target material first"),
                    "OK");
                return;
            }

            string defaultFolder = MaterialParameterShareSystem.GetDefaultExportFolder();
            string extension = MaterialParameterShareSystem.GetFileExtension();

            string path = EditorUtility.OpenFilePanel(
                L("マテリアルパラメータを読み込み", "Import Material Parameters"),
                defaultFolder,
                extension.TrimStart('.'));

            if (!string.IsNullOrEmpty(path))
            {
                Undo.RecordObject(selectedMaterial, "Import Material Parameters");
                bool success = MaterialParameterShareSystem.ImportFromFile(path, selectedMaterial);

                if (success)
                {
                    EditorUtility.SetDirty(selectedMaterial);
                    EditorUtility.DisplayDialog(
                        L("読み込み完了", "Import Successful"),
                        L($"'{selectedMaterial.name}' にマテリアルパラメータを読み込みました。", $"Material parameters imported to '{selectedMaterial.name}'"),
                        "OK");
                }
            }
        }

        private void CopyMaterialToClipboard(Material material)
        {
            bool success = MaterialParameterShareSystem.CopyToClipboard(material);
            if (success)
            {
                EditorUtility.DisplayDialog(
                    L("クリップボードへコピー", "Copied to Clipboard"),
                    L($"マテリアル '{material.name}' のパラメータをクリップボードへコピーしました。\n\n別のマテリアルへ貼り付けたり、ほかの人へ共有したりできます。", $"Material '{material.name}' parameters copied to clipboard.\n\nYou can now paste these parameters to another material or share with others."),
                    "OK");
            }
        }

        private void PasteMaterialFromClipboard(Material material)
        {
            if (!MaterialParameterShareSystem.IsClipboardValid())
            {
                EditorUtility.DisplayDialog(
                    L("無効なクリップボード", "Invalid Clipboard"),
                    L("クリップボードに有効なマテリアルパラメータが含まれていません。", "Clipboard does not contain valid material parameter data."),
                    "OK");
                return;
            }

            var info = MaterialParameterShareSystem.GetClipboardInfo();
            bool proceed = EditorUtility.DisplayDialog(
                L("マテリアルパラメータを貼り付け", "Paste Material Parameters"),
                L($"Paste parameters from:\n\nMaterial: {info.materialName}\nExported by: {info.exportedBy}\nExport date: {info.exportDate}\nNotes: {info.notes}\n\nThis will overwrite current settings of '{material.name}'", $"Paste parameters from:\n\nMaterial: {info.materialName}\nExported by: {info.exportedBy}\nExport date: {info.exportDate}\nNotes: {info.notes}\n\nThis will overwrite current settings of '{material.name}'"),
                  L("貼り付け", "Paste"),
                  L("キャンセル", "Cancel"));

            if (proceed)
            {
                Undo.RecordObject(material, "Paste Material Parameters");
                bool success = MaterialParameterShareSystem.PasteFromClipboard(material);

                if (success)
                {
                    EditorUtility.SetDirty(material);
                }
            }
        }

        private void ShowHelp()
        {
            EditorUtility.DisplayDialog(
                L("マテリアルプリセットブラウザ - ヘルプ", "Material Preset Browser - Help"),
                L("機能:\n" +
                    "・マテリアルプリセットを見ながら適用できます\n" +
                    "・カテゴリや名前で絞り込みできます\n" +
                    "・既存マテリアルからプリセットを作成できます\n" +
                    "・ファイルやクリップボードで共有できます\n" +
                    "・VTuber 向けプリセットを自動生成できます\n\n" +
                    "使い方:\n" +
                    "1. 対象マテリアルを選択\n" +
                    "2. プリセットを選んで Apply を押す\n" +
                    "3. 必要ならマテリアルからプリセットを作成\n" +
                    "4. Export / Import で共有", "Features:\n" +
                    "Browse and apply material presets visually\n" +
                    "Filter by category and search by name\n" +
                    "Create presets from existing materials\n" +
                    "Share parameters via file or clipboard\n" +
                    "Generate VTuber-optimized presets\n\n" +
                    "Usage:\n" +
                    "1. Select a target material\n" +
                    "2. Browse presets and click Apply\n" +
                    "3. Create presets from materials\n" +
                    "4. Share with Export/Import"),
                "OK");
        }

        /// <summary>
        /// Generate VTuber-optimized material presets
        /// Creates 5 professional presets for VTuber character rendering
        /// </summary>
        private void GenerateVTuberPresets()
        {
            // Show confirmation dialog
            bool proceed = EditorUtility.DisplayDialog(
                L("VTuber プリセット生成", "Generate VTuber Presets"),
                L("高品質な VTuber 向けマテリアルプリセットを生成します。\n\n" +
                "次の 5 種類を作成します:\n" +
                "1. Character Skin - SSS 付きの柔らかい肌\n" +
                "2. Character Hair - 光沢感のある髪\n" +
                "3. Character Clothing - クリーンな衣装表現\n" +
                "4. Character Eyes - きらめく瞳\n" +
                "5. Live Performance - 軽量ライブ向け\n\n" +
                "保存先: Assets/NataneToon/Runtime/Presets/VTuber/\n\n" +
                "生成しますか？", "Generate high-quality VTuber material presets.\n\n" +
                "The following 5 presets will be created:\n" +
                "1. Character Skin - Soft cell shading with SSS\n" +
                "2. Character Hair - Glossy anime-style hair\n" +
                "3. Character Clothing - Clean anime style\n" +
                "4. Character Eyes - Sparkling eyes\n" +
                "5. Live Performance - Lightweight & high performance\n\n" +
                "Location: Assets/NataneToon/Runtime/Presets/VTuber/\n\n" +
                "Generate?"),
                L("生成", "Generate"),
                L("キャンセル", "Cancel"));

            if (!proceed) return;

            // Ensure directory exists with robust creation logic
            string presetPath = "Assets/NataneToon/Runtime/Presets/VTuber";

            // Create directory using System.IO for more reliable handling
            string fullPath = System.IO.Path.GetFullPath(presetPath);
            if (!System.IO.Directory.Exists(fullPath))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(fullPath);
                    Debug.Log($"[MaterialPresetBrowser] Created directory: {fullPath}");

                    // Refresh AssetDatabase to recognize new directory
                    AssetDatabase.Refresh();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MaterialPresetBrowser] Failed to create directory: {e.Message}");
                    EditorUtility.DisplayDialog(
                        L("エラー", "Error"),
                        L($"フォルダの作成に失敗しました:\n{e.Message}\n\n手動で次のフォルダを作成してください:\n{presetPath}", $"Failed to create directory:\n{e.Message}\n\n" +
                        $"Please manually create the directory:\n{presetPath}"),
                        "OK");
                    return;
                }
            }

            int presetsCreated = 0;

            // Generate presets
            presetsCreated += GenerateCharacterSkinPreset(presetPath) ? 1 : 0;
            presetsCreated += GenerateCharacterHairPreset(presetPath) ? 1 : 0;
            presetsCreated += GenerateCharacterClothingPreset(presetPath) ? 1 : 0;
            presetsCreated += GenerateCharacterEyesPreset(presetPath) ? 1 : 0;
            presetsCreated += GenerateLivePerformancePreset(presetPath) ? 1 : 0;

            AssetDatabase.SaveAssets();

            // Refresh preset list to show new presets
            RefreshPresetList(forceRefresh: true);

            // Auto-filter to show VTuber presets
            showAllCategories = false;
            selectedCategory = PresetCategory.Character_Skin;
            FilterPresets();

            // Show completion dialog
            EditorUtility.DisplayDialog(
                L("完了", "Complete"),
                L($"VTuber プリセットの生成が完了しました。\n\n作成数: {presetsCreated}\n保存先: {presetPath}\n\nこのブラウザに新しいプリセットが表示されています。\n対象マテリアルを選んで Apply を押すと使えます。", $"VTuber preset generation complete!\n\n" +
                $"Presets created: {presetsCreated}\n" +
                $"Location: {presetPath}\n\n" +
                $"Presets are now displayed in this browser.\n" +
                $"Select a material and click 'Apply' to use them."),
                "OK");

            Debug.Log($"[MaterialPresetBrowser] Generated {presetsCreated} VTuber presets.");
        }

        // VTuber Preset Generation Methods
        private bool GenerateCharacterSkinPreset(string basePath)
        {
            try
            {
                // Verify directory exists
                string fullBasePath = System.IO.Path.GetFullPath(basePath);
                if (!System.IO.Directory.Exists(fullBasePath))
                {
                    Debug.LogError($"[GenerateCharacterSkinPreset] Directory does not exist: {fullBasePath}");
                    return false;
                }

                var preset = CreateInstance<NataneToonMaterialPreset>();
                preset.presetName = "VTuber - Character Skin";
                preset.description = "VTuber skin preset for face and body materials.\n" +
                                   "Balances soft shading, gentle SSS response, and clean rim lighting.\n" +
                                   "Designed for stable skin tones under mixed lighting.\n" +
                                   "Use as a base, then tune shadow and specular response per character.";
                preset.category = PresetCategory.Character_Skin;
                preset.author = "Natane Toon Shader";
                preset.version = "1.0";
                preset.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");

                var p = preset.parameters;
                p.mainColor = new Color(1.0f, 1.0f, 1.0f, 1f);
                p.alpha = 1f;
                p.shadowColor = new Color(0.95f, 0.75f, 0.68f, 1f);
                p.toonSteps = 2;
                p.toonSharpness = 0.08f;
                p.shadowReceive = 0.8f;
                p.shadowIntensityMax = 0.3f;
                p.lightInfluence = 1.2f;
                p.lightColorInfluence = 1.0f;
                p.backlight = 0.2f;
                p.useSSS = true;
                p.sssColor = new Color(1f, 0.65f, 0.55f, 1f);
                p.sssIntensity = 0.45f;
                p.sssDistortion = 0.3f;
                p.sssPower = 2.5f;
                p.sssScale = 0.8f;
                p.useRimLight = true;
                p.rimColor = new Color(1f, 0.9f, 0.85f, 1f);
                p.rimIntensity = 0.6f;
                p.rimPower = 4f;
                p.useSpecular = true;
                p.specularColor = new Color(1f, 1f, 1f, 1f);
                p.specularIntensity = 0.3f;
                p.specularSize = 0.2f;
                p.specularSharpness = 0.4f;
                p.useOutline = true;
                p.outlineColor = new Color(0.3f, 0.2f, 0.15f, 1f);
                p.outlineWidth = 0.003f;
                p.renderQueue = 2000;
                p.cullMode = 2;

                string assetPath = $"{basePath}/VTuber_CharacterSkin.asset";
                AssetDatabase.CreateAsset(preset, assetPath);
                Debug.Log($"[GenerateCharacterSkinPreset] Created preset at: {assetPath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GenerateCharacterSkinPreset] Failed to generate: {e.Message}\nStack: {e.StackTrace}");
                return false;
            }
        }

        private bool GenerateCharacterHairPreset(string basePath)
        {
            try
            {
                // Verify directory exists
                string fullBasePath = System.IO.Path.GetFullPath(basePath);
                if (!System.IO.Directory.Exists(fullBasePath))
                {
                    Debug.LogError($"[GenerateCharacterHairPreset] Directory does not exist: {fullBasePath}");
                    return false;
                }

                var preset = CreateInstance<NataneToonMaterialPreset>();
                preset.presetName = "VTuber - Character Hair";
                preset.description = "VTuber hair preset for stylized hair materials.\n" +
                                   "Balances readable strand shading, stable highlights, and soft rim response.\n" +
                                   "Includes MatCap support for controlled sheen accents.\n" +
                                   "Use as a base, then tune shadow and highlight colors per character.";
                preset.category = PresetCategory.Character_Hair;
                preset.author = "Natane Toon Shader";
                preset.version = "1.0";
                preset.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");

                var p = preset.parameters;
                p.mainColor = new Color(1.0f, 1.0f, 1.0f, 1f);
                p.alpha = 1f;
                p.shadowColor = new Color(0.45f, 0.35f, 0.28f, 1f);
                p.toonSteps = 2;
                p.toonSharpness = 0.05f;
                p.shadowReceive = 0.9f;
                p.shadowIntensityMax = 0.2f;
                p.lightInfluence = 1.3f;
                p.lightColorInfluence = 1.0f;
                p.backlight = 0.3f;
                p.useSpecular = true;
                p.specularColor = new Color(1f, 1f, 1f, 1f);
                p.specularIntensity = 1.2f;
                p.specularSize = 0.15f;
                p.specularSharpness = 0.9f;
                p.useMatCap = true;
                p.matCapIntensity = 0.4f;
                p.matCapBlendMode = 0;
                p.useRimLight = true;
                p.rimColor = new Color(0.8f, 0.7f, 0.6f, 1f);
                p.rimIntensity = 0.8f;
                p.rimPower = 3f;
                p.useOutline = true;
                p.outlineColor = new Color(0.1f, 0.05f, 0.05f, 1f);
                p.outlineWidth = 0.004f;
                p.renderQueue = 2000;
                p.cullMode = 0;

                string assetPath = $"{basePath}/VTuber_CharacterHair.asset";
                AssetDatabase.CreateAsset(preset, assetPath);
                Debug.Log($"[GenerateCharacterHairPreset] Created preset at: {assetPath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GenerateCharacterHairPreset] Failed to generate: {e.Message}\nStack: {e.StackTrace}");
                return false;
            }
        }

        private bool GenerateCharacterClothingPreset(string basePath)
        {
            try
            {
                // Verify directory exists
                string fullBasePath = System.IO.Path.GetFullPath(basePath);
                if (!System.IO.Directory.Exists(fullBasePath))
                {
                    Debug.LogError($"[GenerateCharacterClothingPreset] Directory does not exist: {fullBasePath}");
                    return false;
                }

                var preset = CreateInstance<NataneToonMaterialPreset>();
                preset.presetName = "VTuber - Character Clothing";
                preset.description = "VTuber clothing preset for stylized fabric materials.\n" +
                                   "Balances readable folds, stable highlights, and clean shadow separation.\n" +
                                   "Good for outfits that need a clear silhouette under mixed lighting.\n" +
                                   "Tune specular and detail parameters to match the costume.";
                preset.category = PresetCategory.Character_Clothing;
                preset.author = "Natane Toon Shader";
                preset.version = "1.0";
                preset.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");

                var p = preset.parameters;
                p.mainColor = new Color(1.0f, 1.0f, 1.0f, 1f);
                p.alpha = 1f;
                p.shadowColor = new Color(0.75f, 0.73f, 0.72f, 1f);
                p.toonSteps = 2;
                p.toonSharpness = 0.1f;
                p.shadowReceive = 1f;
                p.shadowIntensityMax = 0.25f;
                p.lightInfluence = 1f;
                p.lightColorInfluence = 1.0f;
                p.backlight = 0.1f;
                p.useSpecular = false;
                p.useRimLight = true;
                p.rimColor = new Color(1f, 1f, 1f, 1f);
                p.rimIntensity = 0.4f;
                p.rimPower = 5f;
                p.useOutline = true;
                p.outlineColor = new Color(0f, 0f, 0f, 1f);
                p.outlineWidth = 0.005f;
                p.renderQueue = 2000;
                p.cullMode = 2;

                string assetPath = $"{basePath}/VTuber_CharacterClothing.asset";
                AssetDatabase.CreateAsset(preset, assetPath);
                Debug.Log($"[GenerateCharacterClothingPreset] Created preset at: {assetPath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GenerateCharacterClothingPreset] Failed to generate: {e.Message}\nStack: {e.StackTrace}");
                return false;
            }
        }

        private bool GenerateCharacterEyesPreset(string basePath)
        {
            try
            {
                // Verify directory exists
                string fullBasePath = System.IO.Path.GetFullPath(basePath);
                if (!System.IO.Directory.Exists(fullBasePath))
                {
                    Debug.LogError($"[GenerateCharacterEyesPreset] Directory does not exist: {fullBasePath}");
                    return false;
                }

                var preset = CreateInstance<NataneToonMaterialPreset>();
                preset.presetName = "VTuber - Character Eyes";
                preset.description = "VTuber eye preset for iris, sclera, and highlight setups.\n" +
                                   "Balances readability, sparkle, and stable shadow response.\n" +
                                   "Includes settings for clean highlights and consistent eye shading.\n" +
                                   "Use as a starting point, then tune specular and rim response.";
                preset.category = PresetCategory.Character_Eyes;
                preset.author = "Natane Toon Shader";
                preset.version = "1.0";
                preset.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");

                var p = preset.parameters;
                p.mainColor = new Color(1.0f, 1.0f, 1.0f, 1f);
                p.alpha = 1f;
                p.shadowColor = new Color(0.65f, 0.7f, 0.85f, 1f);
                p.toonSteps = 3;
                p.toonSharpness = 0.15f;
                p.shadowReceive = 0.5f;
                p.shadowIntensityMax = 0.4f;
                p.lightInfluence = 1.5f;
                p.lightColorInfluence = 1.0f;
                p.backlight = 0.4f;
                p.useSpecular = true;
                p.specularColor = new Color(1f, 1f, 1f, 1f);
                p.specularIntensity = 1.5f;
                p.specularSize = 0.3f;
                p.specularSharpness = 0.95f;
                p.useEmission = true;
                p.emissionColor = new Color(0.5f, 0.7f, 1f, 1f);
                p.emissionIntensity = 0.3f;
                p.useRimLight = true;
                p.rimColor = new Color(1f, 1f, 1f, 1f);
                p.rimIntensity = 1f;
                p.rimPower = 3f;
                p.useOutline = true;
                p.outlineColor = new Color(0.1f, 0.1f, 0.2f, 1f);
                p.outlineWidth = 0.002f;
                p.renderQueue = 2000;
                p.cullMode = 2;

                string assetPath = $"{basePath}/VTuber_CharacterEyes.asset";
                AssetDatabase.CreateAsset(preset, assetPath);
                Debug.Log($"[GenerateCharacterEyesPreset] Created preset at: {assetPath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GenerateCharacterEyesPreset] Failed to generate: {e.Message}\nStack: {e.StackTrace}");
                return false;
            }
        }

        private bool GenerateLivePerformancePreset(string basePath)
        {
            try
            {
                // Verify directory exists
                string fullBasePath = System.IO.Path.GetFullPath(basePath);
                if (!System.IO.Directory.Exists(fullBasePath))
                {
                    Debug.LogError($"[GenerateLivePerformancePreset] Directory does not exist: {fullBasePath}");
                    return false;
                }

                var preset = CreateInstance<NataneToonMaterialPreset>();
                preset.presetName = "VTuber - Live Performance";
                preset.description = "VTuber preset tuned for stable stage and event lighting.\n" +
                                   "Keeps shadows clean, highlights restrained, and silhouettes readable at distance.\n" +
                                   "Good for scenes that need predictable performance under mixed lighting.\n" +
                                   "Use as a baseline, then adjust color and outline strength to fit the show.";
                preset.category = PresetCategory.Style_Toon;
                preset.author = "Natane Toon Shader";
                preset.version = "1.0";
                preset.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");

                var p = preset.parameters;
                p.mainColor = Color.white;
                p.alpha = 1f;
                p.shadowColor = new Color(0.75f, 0.72f, 0.7f, 1f);
                p.toonSteps = 2;
                p.toonSharpness = 0.1f;
                p.shadowReceive = 1f;
                p.shadowIntensityMax = 0.2f;
                p.lightInfluence = 1f;
                p.lightColorInfluence = 1.0f;
                p.backlight = 0f;
                p.useSpecular = false;
                p.useRimLight = false;
                p.useSSS = false;
                p.useMatCap = false;
                p.useEmission = false;
                p.useReflection = false;
                p.useEnvRim = false;
                p.useParallax = false;
                p.useRefraction = false;
                p.useOutline = true;
                p.outlineColor = Color.black;
                p.outlineWidth = 0.004f;
                p.renderQueue = 2000;
                p.cullMode = 2;

                string assetPath = $"{basePath}/VTuber_LivePerformance.asset";
                AssetDatabase.CreateAsset(preset, assetPath);
                Debug.Log($"[GenerateLivePerformancePreset] Created preset at: {assetPath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GenerateLivePerformancePreset] Failed to generate: {e.Message}\nStack: {e.StackTrace}");
                return false;
            }
        }
    }
}
