using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// </summary>
    public class LilToonMigrationTool : EditorWindow
    {
        private const string MigrationMetadataVersion = "2026-03-11-liltoon-ui-v1";

        [System.Flags]
        private enum LilToonParityFlags
        {
            None = 0,
            RimShadeUnsupported = 1 << 0,
            Emission2ndUnsupported = 1 << 1,
            ShadowBorderRangeUnsupported = 1 << 2,
            ShadowMaskTypeUnsupported = 1 << 3,
            BackfaceForceShadowUnsupported = 1 << 4,
            ShadowPostAOUnsupported = 1 << 5,
            MatCapNeedsReview = 1 << 6,
            OutlineNeedsReview = 1 << 7
        }

        private List<Material> lilToonMaterials = new List<Material>();
        private Vector2 windowScrollPosition;
        private Vector2 scrollPosition;
        private bool createBackup = true;
        private bool replaceOriginal = false;
        private bool showPreview = false;
        private Material previewMaterial = null;
        private bool autoOptimize = true;
        private const float CompactLayoutWidth = 720f;
        private const float NarrowLayoutWidth = 560f;

        private enum ConversionMode { VisualMatch, MinimalSafe }
        private ConversionMode conversionMode = ConversionMode.VisualMatch;
        private string[] conversionModeDisplayNames => new[] {
            L("見た目優先 (近似)", "Visual Match (Approximate)"),
            L("最小安全構成 (旧仕様向け)", "Minimal Safe (Legacy)")
        };

        private enum MigrationMode { Project, Prefab }
        private MigrationMode currentMode = MigrationMode.Project;
        private string[] modeNames => new[] { L("プロジェクト", "Project"), L("アバター/Prefab", "Avatar/Prefab") };

        private GameObject targetPrefab = null;
        private List<PrefabMaterialInfo> prefabMaterials = new List<PrefabMaterialInfo>();
        private Vector2 prefabScrollPosition;
        private bool updatePrefabReferences = true;
        private bool duplicateInHierarchy = false;
        private int prefabMaterialPageIndex = 0;
        private const int PrefabMaterialsPerPage = 20;
        private GameObject lastDuplicatedObject = null;
        private bool hasScannedProjectMaterials = false;
        private int projectPageIndex = 0;
        private int projectMaterialTotalCount = 0;
        private const int ProjectResultsPerPage = 100;

        private class PrefabMaterialInfo
        {
            public Material original;
            public Material converted;
            public Renderer renderer;
            public int materialIndex;
            public bool willConvert = true;
            public string rendererPath;
        }

        private class ConversionReport
        {
            public string materialName;
            public bool success;
            public List<string> warnings = new List<string>();
            public List<string> infos = new List<string>();
            public bool hasMultipleShadowLayers;
            public bool outlineWidthAdjusted;
            public float originalOutlineWidth;
            public float convertedOutlineWidth;
        }

        [MenuItem(NataneToolMenuPaths.LilToonMigration, false, 51)]
        public static void ShowWindow()
        {
            var window = GetWindow<LilToonMigrationTool>(L("lilToon Migration", "lilToon Migration"));
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            hasScannedProjectMaterials = false;
            projectPageIndex = 0;
            projectMaterialTotalCount = 0;
            lilToonMaterials.Clear();
        }

        private void OnGUI()
        {
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
            NataneToonShaderGUIUtility.DrawToolHeader("lilToon移行ツール", "lilToon Migration Tool", nameof(LilToonMigrationTool));
            EditorGUILayout.Space(4);

            // --- 対象選択 ---
            currentMode = DrawResponsiveSelection(currentMode, modeNames);
            EditorGUILayout.Space(4);

            // --- 設定 (コンパクト) ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            autoOptimize = EditorGUILayout.Toggle(L("Auto Setup で自動最適化", "Auto-optimize with Auto Setup"), autoOptimize);
            createBackup = EditorGUILayout.Toggle(L("バックアップを作成", "Create Backup"), createBackup);
            conversionMode = (ConversionMode)EditorGUILayout.Popup(
                L("変換モード", "Conversion Mode"),
                (int)conversionMode, conversionModeDisplayNames);

            // 上級オプション (折りたたみ)
            replaceOriginal = EditorGUILayout.Toggle(L("元マテリアルを直接置換", "Replace Original"), replaceOriginal);
            if (replaceOriginal)
            {
                EditorGUILayout.HelpBox(
                    L("元マテリアルを直接書き換えます。バックアップを推奨します。",
                      "Original materials will be overwritten. Backup recommended."),
                    MessageType.Warning);
            }
            showPreview = EditorGUILayout.Toggle(L("変換後にプレビュー", "Preview After Conversion"), showPreview);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            switch (currentMode)
            {
                case MigrationMode.Project:
                    DrawProjectMode();
                    break;
                case MigrationMode.Prefab:
                    DrawPrefabMode();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// </summary>
        private void DrawProjectMode()
        {
            // Scan button
            if (GUILayout.Button(L("lilToon マテリアルを検索", "Scan for lilToon Materials"), GUILayout.Height(30)))
            {
                ScanForLilToonMaterials();
            }

            EditorGUILayout.Space();
            if (!hasScannedProjectMaterials)
            {
                return;
            }

            // Materials list
            EditorGUILayout.LabelField(L($"Found {lilToonMaterials.Count} lilToon Materials", $"Found {lilToonMaterials.Count} lilToon Materials"), EditorStyles.boldLabel);

            DrawProjectPageControls();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(GetAdaptiveListHeight(180f, 340f, 0.35f)));

            foreach (var material in lilToonMaterials)
            {
                if (IsCompactLayout())
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.ObjectField(material, typeof(Material), false);

                    if (GUILayout.Button(L("Convert", "Convert"), GUILayout.Height(24f)))
                    {
                        var report = ConvertMaterialWithReport(material);
                        if (report.success)
                        {
                            AssetDatabase.SaveAssets();
                            ShowConversionReport(new List<ConversionReport> { report }, 1);
                            ScanForLilToonMaterials();
                        }
                    }

                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(material, typeof(Material), false);

                    if (GUILayout.Button(L("Convert", "Convert"), GUILayout.Width(80)))
                    {
                        var report = ConvertMaterialWithReport(material);
                        if (report.success)
                        {
                            AssetDatabase.SaveAssets();
                            ShowConversionReport(new List<ConversionReport> { report }, 1);
                            ScanForLilToonMaterials();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Convert all button
            GUI.enabled = lilToonMaterials.Count > 0;
            if (GUILayout.Button(L("すべてのマテリアルを変換", "Convert All Materials"), GUILayout.Height(40)))
            {
                ConvertAllMaterials();
            }
            GUI.enabled = true;
        }

        private void DrawProjectPageControls()
        {
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(projectMaterialTotalCount / (float)ProjectResultsPerPage));
            projectPageIndex = Mathf.Clamp(projectPageIndex, 0, totalPages - 1);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = projectPageIndex > 0;
            if (GUILayout.Button("<", GUILayout.Width(32)))
            {
                projectPageIndex--;
                RefreshProjectMaterialPage();
            }

            GUI.enabled = projectPageIndex < totalPages - 1;
            if (GUILayout.Button(">", GUILayout.Width(32)))
            {
                projectPageIndex++;
                RefreshProjectMaterialPage();
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{projectPageIndex + 1}/{totalPages}", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// </summary>
        private void DrawPrefabMode()
        {
            EditorGUILayout.LabelField(L("Prefab 選択", "Prefab Selection"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            targetPrefab = (GameObject)EditorGUILayout.ObjectField(
                L("対象 Prefab", "Target Prefab"),
                targetPrefab,
                typeof(GameObject),
                true
            );
            if (EditorGUI.EndChangeCheck() && targetPrefab != null)
            {
                ScanPrefabMaterials();
            }

            if (targetPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    L("Prefab またはシーン上のアバターをここにドラッグ＆ドロップすると、lilToon マテリアルを検出します。", "Drag & drop a prefab or scene avatar here to scan for lilToon materials."),
                    MessageType.Info
                );
                return;
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField(L("Prefab 設定", "Prefab Settings"), EditorStyles.boldLabel);

            duplicateInHierarchy = EditorGUILayout.Toggle(
                L("Create Duplicate in Hierarchy", "Create Duplicate in Hierarchy"),
                duplicateInHierarchy
            );
            if (duplicateInHierarchy)
            {
                EditorGUILayout.HelpBox(
                    L("Creates a duplicate in the hierarchy with converted materials, keeping the original prefab untouched.\n" +
                    "You can compare the original and converted avatars side by side.", "Creates a duplicate in the hierarchy with converted materials, keeping the original prefab untouched.\n" +
                    "You can compare the original and converted avatars side by side."),
                    MessageType.Info
                );
            }

            using (new EditorGUI.DisabledScope(duplicateInHierarchy))
            {
                updatePrefabReferences = EditorGUILayout.Toggle(
                    L("Auto-update References", "Auto-update References"),
                    updatePrefabReferences
                );
            }

            if (duplicateInHierarchy)
            {
                EditorGUILayout.HelpBox(
                    L("「Hierarchy に複製」が有効の場合、元の Prefab は変更されないため「参照自動更新」は無効になります。",
                      "When 'Create Duplicate in Hierarchy' is enabled, the original prefab is left untouched, so 'Auto-update References' is disabled."),
                    MessageType.Info
                );
            }
            else if (updatePrefabReferences && !replaceOriginal)
            {
                EditorGUILayout.HelpBox(
                    L("After conversion, Renderer references in the prefab will be automatically updated to new materials.", "After conversion, Renderer references in the prefab will be automatically updated to new materials."),
                    MessageType.Info
                );
            }
            else if (replaceOriginal)
            {
                EditorGUILayout.HelpBox(
                    L("In 'Replace Original' mode, the original material is modified in-place, so reference updates are unnecessary.", "In 'Replace Original' mode, the original material is modified in-place, so reference updates are unnecessary."),
                    MessageType.Info
                );
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(L("マテリアルを再検索", "Rescan Materials"), GUILayout.Height(25)))
            {
                ScanPrefabMaterials();
            }

            EditorGUILayout.Space();

            if (prefabMaterials.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("この Prefab に lilToon マテリアルは見つかりませんでした。", "No lilToon materials found in this prefab."),
                    MessageType.Warning
                );
                return;
            }

            EditorGUILayout.LabelField(
                L($"Found {prefabMaterials.Select(m => m.original).Distinct().Count()} lilToon Materials", $"Found {prefabMaterials.Select(m => m.original).Distinct().Count()} lilToon Materials"),
                EditorStyles.boldLabel
            );

            prefabScrollPosition = EditorGUILayout.BeginScrollView(prefabScrollPosition, GUILayout.Height(GetAdaptiveListHeight(220f, 400f, 0.38f)));

            var grouped = prefabMaterials.GroupBy(m => m.original).ToList();
            int totalGroups = grouped.Count;
            bool usePagination = totalGroups > PrefabMaterialsPerPage;
            int pageStart = 0;
            int pageEnd = totalGroups;

            if (usePagination)
            {
                int maxPage = (totalGroups - 1) / PrefabMaterialsPerPage;
                prefabMaterialPageIndex = Mathf.Clamp(prefabMaterialPageIndex, 0, maxPage);
                pageStart = prefabMaterialPageIndex * PrefabMaterialsPerPage;
                pageEnd = Mathf.Min(pageStart + PrefabMaterialsPerPage, totalGroups);

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(prefabMaterialPageIndex <= 0))
                {
                    if (GUILayout.Button(L("< Prev", "< Prev"), GUILayout.Width(80)))
                        prefabMaterialPageIndex--;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    L($"{prefabMaterialPageIndex + 1} / {maxPage + 1}", $"{prefabMaterialPageIndex + 1} / {maxPage + 1}"),
                    new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter },
                    GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(prefabMaterialPageIndex >= maxPage))
                {
                    if (GUILayout.Button(L("Next >", "Next >"), GUILayout.Width(80)))
                        prefabMaterialPageIndex++;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            for (int gi = pageStart; gi < pageEnd; gi++)
            {
                var group = grouped[gi];
                Material mat = group.Key;
                var entries = group.ToList();
                bool willConvert = entries[0].willConvert;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                bool newWillConvert;
                if (IsCompactLayout())
                {
                    EditorGUILayout.BeginHorizontal();
                    newWillConvert = EditorGUILayout.Toggle(willConvert, GUILayout.Width(20));
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);
                    EditorGUILayout.EndHorizontal();

                    if (entries[0].converted != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("->", GUILayout.Width(20));
                        EditorGUILayout.ObjectField(entries[0].converted, typeof(Material), false);
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button(L("Convert", "Convert"), GUILayout.Height(24f)))
                    {
                        ConvertSinglePrefabMaterial(mat);
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    newWillConvert = EditorGUILayout.Toggle(willConvert, GUILayout.Width(20));
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);

                    if (entries[0].converted != null)
                    {
                        EditorGUILayout.LabelField("->", GUILayout.Width(20));
                        EditorGUILayout.ObjectField(entries[0].converted, typeof(Material), false);
                    }

                    if (GUILayout.Button(L("Convert", "Convert"), GUILayout.Width(100)))
                    {
                        ConvertSinglePrefabMaterial(mat);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (newWillConvert != willConvert)
                {
                    foreach (var entry in entries)
                    {
                        entry.willConvert = newWillConvert;
                    }
                }

                EditorGUI.indentLevel++;
                foreach (var entry in entries)
                {
                    EditorGUILayout.LabelField(
                        L($"Used by: {entry.rendererPath} [Slot {entry.materialIndex}]", $"Used by: {entry.rendererPath} [Slot {entry.materialIndex}]"),
                        EditorStyles.miniLabel
                    );
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            int convertCount = prefabMaterials.Where(m => m.willConvert && m.converted == null)
                                              .Select(m => m.original).Distinct().Count();

            if (convertCount > 0)
            {
                EditorGUILayout.LabelField(
                    L($"Target: {convertCount} materials", $"Target: {convertCount} materials"),
                    EditorStyles.boldLabel
                );
            }

            GUI.enabled = convertCount > 0;
            string buttonLabel = duplicateInHierarchy
                ? L($"Duplicate & Convert ({convertCount})", $"Duplicate & Convert ({convertCount})")
                : L($"Convert Selected ({convertCount})", $"Convert Selected ({convertCount})");
            if (GUILayout.Button(buttonLabel, GUILayout.Height(40)))
            {
                ConvertPrefabMaterials();
            }
            GUI.enabled = true;

            if (lastDuplicatedObject != null)
            {
                EditorGUILayout.Space(5);
                if (IsCompactLayout())
                {
                    EditorGUILayout.LabelField(L("Last Duplicate", "Last Duplicate"), EditorStyles.boldLabel);
                    EditorGUILayout.ObjectField(lastDuplicatedObject, typeof(GameObject), true);
                    if (GUILayout.Button(L("Select", "Select"), GUILayout.Height(24f)))
                    {
                        Selection.activeGameObject = lastDuplicatedObject;
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(L("Last Duplicate:", "Last Duplicate:"), GUILayout.Width(100));
                    EditorGUILayout.ObjectField(lastDuplicatedObject, typeof(GameObject), true);
                    if (GUILayout.Button(L("Select", "Select"), GUILayout.Width(60)))
                    {
                        Selection.activeGameObject = lastDuplicatedObject;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private bool IsCompactLayout()
        {
            return position.width < CompactLayoutWidth;
        }

        private TEnum DrawResponsiveSelection<TEnum>(TEnum currentValue, string[] labels) where TEnum : System.Enum
        {
            int selectedIndex = System.Convert.ToInt32(currentValue);
            if (position.width < NarrowLayoutWidth)
            {
                selectedIndex = GUILayout.SelectionGrid(selectedIndex, labels, 1, EditorStyles.miniButton);
            }
            else if (IsCompactLayout())
            {
                selectedIndex = GUILayout.SelectionGrid(selectedIndex, labels, labels.Length, EditorStyles.miniButton);
            }
            else
            {
                selectedIndex = GUILayout.Toolbar(selectedIndex, labels);
            }

            return (TEnum)System.Enum.ToObject(typeof(TEnum), selectedIndex);
        }

        private float GetAdaptiveListHeight(float minHeight, float maxHeight, float ratio)
        {
            return Mathf.Clamp(position.height * ratio, minHeight, maxHeight);
        }

        private void ScanForLilToonMaterials()
        {
            lilToonMaterials.Clear();
            hasScannedProjectMaterials = true;
            projectPageIndex = 0;
            RefreshProjectMaterialPage();
            Debug.Log($"Found {projectMaterialTotalCount} lilToon materials via asset index.");
        }

        private void RefreshProjectMaterialPage()
        {
            lilToonMaterials.Clear();

            List<MaterialIndexEntry> pageEntries = NataneAssetIndexService.GetMaterialEntriesPage(
                entry => entry.isLilToonShader,
                projectPageIndex,
                ProjectResultsPerPage,
                out int totalCount);

            projectMaterialTotalCount = totalCount;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(projectMaterialTotalCount / (float)ProjectResultsPerPage));
            projectPageIndex = Mathf.Clamp(projectPageIndex, 0, totalPages - 1);

            if (projectMaterialTotalCount > 0 && pageEntries.Count == 0)
            {
                pageEntries = NataneAssetIndexService.GetMaterialEntriesPage(
                    entry => entry.isLilToonShader,
                    projectPageIndex,
                    ProjectResultsPerPage,
                    out totalCount);
                projectMaterialTotalCount = totalCount;
            }

            for (int i = 0; i < pageEntries.Count; i++)
            {
                Material material = NataneAssetIndexService.LoadMaterial(pageEntries[i]);
                if (material != null)
                {
                    lilToonMaterials.Add(material);
                }
            }
        }

        private void ConvertAllMaterials()
        {
            List<MaterialIndexEntry> allEntries = NataneAssetIndexService
                .EnumerateMaterialEntries(entry => entry.isLilToonShader)
                .OrderBy(entry => entry.name)
                .ThenBy(entry => entry.path)
                .ToList();

            int successCount = 0;
            List<ConversionReport> reports = new List<ConversionReport>();

            try
            {
                for (int i = 0; i < allEntries.Count; i++)
                {
                    Material material = NataneAssetIndexService.LoadMaterial(allEntries[i]);
                    if (material == null)
                    {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        L("Converting Materials", "Converting Materials"),
                        L($"Converting {i + 1}/{lilToonMaterials.Count}: {lilToonMaterials[i].name}", $"Converting {i + 1}/{lilToonMaterials.Count}: {lilToonMaterials[i].name}"),
                        (float)i / lilToonMaterials.Count
                    );

                    var report = ConvertMaterialWithReport(lilToonMaterials[i]);
                    reports.Add(report);
                    if (report.success)
                    {
                        successCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            ShowConversionReport(reports, successCount);

            // Rescan
            ScanForLilToonMaterials();
        }

        private bool ConvertMaterial(Material sourceMaterial)
        {
            var report = ConvertMaterialWithReport(sourceMaterial);
            return report.success;
        }

        private static string GetBackupMaterialPath(string sourcePath)
        {
            return AssetDatabase.GenerateUniqueAssetPath(sourcePath.Replace(".mat", "_lilToon_backup.mat"));
        }

        private static string GetConvertedMaterialFolderPath(string sourcePath)
        {
            string sourceFolder = System.IO.Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(sourceFolder))
            {
                return "Assets/NataneToon";
            }

            sourceFolder = sourceFolder.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(sourceFolder);
            if (string.Equals(folderName, "NataneToon", System.StringComparison.OrdinalIgnoreCase))
            {
                return sourceFolder;
            }

            return $"{sourceFolder}/NataneToon";
        }

        private static string GetLegacyConvertedMaterialPath(string sourcePath)
        {
            return sourcePath.Replace(".mat", "_NataneToon.mat");
        }

        private static string GetConvertedMaterialPath(string sourcePath)
        {
            string targetFolder = GetConvertedMaterialFolderPath(sourcePath);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            return $"{targetFolder}/{fileName}_NataneToon.mat";
        }

        private static void EnsureConvertedMaterialFolderExists(string sourcePath)
        {
            string sourceFolder = System.IO.Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(sourceFolder))
            {
                sourceFolder = "Assets";
            }

            sourceFolder = sourceFolder.Replace("\\", "/");
            string targetFolder = GetConvertedMaterialFolderPath(sourcePath);

            if (targetFolder == sourceFolder || AssetDatabase.IsValidFolder(targetFolder))
            {
                return;
            }

            AssetDatabase.CreateFolder(sourceFolder, "NataneToon");
        }

        private Material GetOrCreateConversionTargetMaterial(
            Material sourceMaterial,
            ConversionReport report,
            out Material existingMaterial,
            out string targetPath)
        {
            existingMaterial = null;
            targetPath = string.Empty;

            if (replaceOriginal)
            {
                report.infos.Add("Replace Original mode: source material will be updated in-place.");
                return sourceMaterial;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new System.InvalidOperationException($"Source material is not a project asset: {sourceMaterial.name}");
            }

            targetPath = GetConvertedMaterialPath(sourcePath);

            UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(targetPath);
            if (existingAsset != null && !(existingAsset is Material))
            {
                throw new System.InvalidOperationException($"Target path is already occupied by a non-material asset: {targetPath}");
            }

            existingMaterial = existingAsset as Material;
            if (existingMaterial == null)
            {
                string legacyPath = GetLegacyConvertedMaterialPath(sourcePath);
                if (legacyPath != targetPath)
                {
                    UnityEngine.Object legacyAsset = AssetDatabase.LoadMainAssetAtPath(legacyPath);
                    if (legacyAsset != null && !(legacyAsset is Material))
                    {
                        throw new System.InvalidOperationException($"Legacy target path is occupied by a non-material asset: {legacyPath}");
                    }

                    Material legacyMaterial = legacyAsset as Material;
                    if (legacyMaterial != null)
                    {
                        EnsureConvertedMaterialFolderExists(sourcePath);
                        string moveError = AssetDatabase.MoveAsset(legacyPath, targetPath);
                        if (!string.IsNullOrEmpty(moveError))
                        {
                            throw new System.InvalidOperationException($"Failed to move existing converted material into NataneToon folder: {moveError}");
                        }

                        existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                        if (existingMaterial == null)
                        {
                            throw new System.InvalidOperationException($"Moved converted material could not be reloaded: {targetPath}");
                        }

                        report.infos.Add($"Moved existing converted material into NataneToon folder: {targetPath} (GUID preserved)");
                    }
                }
            }

            if (existingMaterial != null)
            {
                report.infos.Add($"Updating existing converted material in-place: {targetPath} (GUID preserved)");
                return new Material(sourceMaterial);
            }

            EnsureConvertedMaterialFolderExists(sourcePath);
            Material newMaterial = new Material(sourceMaterial);
            AssetDatabase.CreateAsset(newMaterial, targetPath);
            report.infos.Add($"Created converted material: {targetPath}");
            return newMaterial;
        }

        private ConversionReport ConvertMaterialWithReport(Material sourceMaterial)
        {
            var report = new ConversionReport();
            report.materialName = sourceMaterial.name;

            try
            {
                string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
                if (string.IsNullOrEmpty(sourcePath))
                {
                    throw new System.InvalidOperationException($"Source material is not a project asset: {sourceMaterial.name}");
                }

                // Create backup if requested
                if (createBackup)
                {
                    string backupPath = GetBackupMaterialPath(sourcePath);
                    AssetDatabase.CopyAsset(sourcePath, backupPath);
                    Debug.Log($"Created backup: {backupPath}");
                    report.infos.Add($"Created backup material: {backupPath}");
                }

                Material existingTargetMaterial;
                string targetPath;
                Material targetMaterial = GetOrCreateConversionTargetMaterial(sourceMaterial, report, out existingTargetMaterial, out targetPath);

                // Store original properties before changing shader
                var originalProperties = CaptureProperties(sourceMaterial);
                // ソースシェーダー名を保存（MapPropertiesWithReport でアウトラインバリアント検出に使用）
                originalProperties["__sourceShaderName"] = sourceMaterial.shader != null ? sourceMaterial.shader.name : "";

                DetectMultipleShadowLayers(originalProperties, report);

                Shader nataneToonShader = DetectNataneShaderVariant(sourceMaterial);
                if (nataneToonShader == null)
                {
                    Debug.LogError(L("Natane Toon Shader が見つかりません。プロジェクトにインポートされているか確認してください。", "Natane Toon Shader not found! Please make sure it's in your project."));
                    report.success = false;
                    report.warnings.Add("Natane Toon Shader was not found in the project.");
                    return report;
                }
                report.infos.Add($"Using shader: {nataneToonShader.name}");

                // Change shader
                targetMaterial.shader = nataneToonShader;
                ApplyLilToonMigrationMetadata(targetMaterial, sourceMaterial, sourceMaterial.shader != null ? sourceMaterial.shader.name : "lilToon");
                if (targetMaterial.HasProperty("_LilToonExactCompatibility"))
                {
                    targetMaterial.SetFloat("_LilToonExactCompatibility", 0.0f);
                }

                // Map properties
                MapPropertiesWithReport(originalProperties, targetMaterial, report);
                ApplyLilToonParityFlags(targetMaterial, originalProperties);

                // renderQueue をソースから維持
                targetMaterial.renderQueue = sourceMaterial.renderQueue;
                report.infos.Add($"Render Queue: {sourceMaterial.renderQueue}");

                if (conversionMode == ConversionMode.MinimalSafe)
                {
                    DisableNonBasicFeatures(targetMaterial, report);
                }
                else
                {
                    report.infos.Add("Visual Match mode keeps migrated features enabled when possible.");
                }

                if (existingTargetMaterial != null)
                {
                    EditorUtility.CopySerialized(targetMaterial, existingTargetMaterial);
                    UnityEngine.Object.DestroyImmediate(targetMaterial);
                    targetMaterial = existingTargetMaterial;
                    report.infos.Add($"Existing converted material was refreshed without changing GUID: {targetPath}");
                }

                if (showPreview)
                {
                    previewMaterial = targetMaterial;
                    Selection.activeObject = targetMaterial;
                }

                // Auto-optimize with AutoFixer
                if (autoOptimize)
                {
                    try
                    {
                        var fixResult = NataneLilToonAutoFixer.Fix(targetMaterial, originalProperties);
                        report.infos.Add($"Auto-fix applied: quality {fixResult.qualityScore}/100, {fixResult.actions.Count} optimizations");
                        foreach (var warning in fixResult.warnings)
                        {
                            report.warnings.Add($"AutoFix: {warning}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        report.warnings.Add($"Auto-fix failed: {ex.Message}");
                    }
                }

                EditorUtility.SetDirty(targetMaterial);

                Debug.Log($"Successfully converted: {sourceMaterial.name}");
                report.success = true;
                return report;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to convert {sourceMaterial.name}: {e.Message}");
                report.success = false;
                report.warnings.Add($"Conversion failed: {e.Message}");
                return report;
            }
        }

        private void DetectMultipleShadowLayers(Dictionary<string, object> properties, ConversionReport report)
        {
            bool has2ndShadow = properties.ContainsKey("_Shadow2ndColor");
            bool has3rdShadow = properties.ContainsKey("_Shadow3rdColor");

            if (has2ndShadow || has3rdShadow)
            {
                report.hasMultipleShadowLayers = true;
                string layerInfo = has3rdShadow ? "3 layers" : "2 layers";
                report.infos.Add($"Detected {layerInfo} and converted them as multi-shadow.");
            }
        }

        private Dictionary<string, object> CaptureProperties(Material material)
        {
            var properties = new Dictionary<string, object>();

            // =============================================
            // =============================================

            // === Core ===
            CaptureTexture(material, "_MainTex", properties);
            CaptureColor(material, "_Color", properties);
            CaptureFloat(material, "_Cutoff", properties);

            // === Normal Map ===
            CaptureTexture(material, "_BumpMap", properties);
            CaptureFloat(material, "_BumpScale", properties);

            CaptureFloat(material, "_UseShadow", properties);
            CaptureFloat(material, "_UseRim", properties);
            CaptureFloat(material, "_UseRimShade", properties);
            CaptureFloat(material, "_UseMatCap", properties);
            CaptureFloat(material, "_UseMatCap2nd", properties);
            CaptureFloat(material, "_UseEmission", properties);
            CaptureFloat(material, "_UseEmission2nd", properties);
            CaptureFloat(material, "_UseOutline", properties);

            CaptureColor(material, "_ShadowColor", properties);
            CaptureFloat(material, "_ShadowBorder", properties);
            CaptureFloat(material, "_ShadowBlur", properties);
            CaptureFloat(material, "_ShadowStrength", properties);
            CaptureFloat(material, "_ShadowMainStrength", properties);
            CaptureFloat(material, "_ShadowBorderRange", properties);
            CaptureFloat(material, "_BackfaceForceShadow", properties);
            CaptureFloat(material, "_ShadowMaskType", properties);
            CaptureFloat(material, "_ShadowFlatBorder", properties);
            CaptureFloat(material, "_ShadowFlatBlur", properties);
            CaptureFloat(material, "_ShadowPostAO", properties);
            CaptureFloat(material, "_ShadowNormalStrength", properties);

            // Shadow 2nd/3rd
            CaptureColor(material, "_Shadow2ndColor", properties);
            CaptureFloat(material, "_Shadow2ndBorder", properties);
            CaptureFloat(material, "_Shadow2ndBlur", properties);
            CaptureColor(material, "_Shadow3rdColor", properties);
            CaptureFloat(material, "_Shadow3rdBorder", properties);
            CaptureFloat(material, "_Shadow3rdBlur", properties);

            // Shadow textures
            CaptureTexture(material, "_ShadowColorTex", properties);
            CaptureTexture(material, "_ShadowStrengthMask", properties);
            CaptureTexture(material, "_ShadowBorderMask", properties);
            CaptureTexture(material, "_ShadowBlurMask", properties);

            CaptureFloat(material, "_ShadowEnvStrength", properties);
            CaptureFloat(material, "_ShadowReceive", properties);

            // === Rim Light ===
            CaptureColor(material, "_RimColor", properties);
            CaptureTexture(material, "_RimColorTex", properties);
            CaptureFloat(material, "_RimBorder", properties);
            CaptureFloat(material, "_RimBlur", properties);
            CaptureFloat(material, "_RimFresnelPower", properties);
            CaptureFloat(material, "_RimBlendMode", properties);
            CaptureFloat(material, "_RimEnableLighting", properties);
            CaptureFloat(material, "_RimShadowMask", properties);

            // === Outline ===
            CaptureColor(material, "_OutlineColor", properties);
            CaptureFloat(material, "_OutlineWidth", properties);
            CaptureFloat(material, "_OutlineFixWidth", properties);
            CaptureTexture(material, "_OutlineTex", properties);
            CaptureTexture(material, "_OutlineWidthMask", properties);
            CaptureTexture(material, "_OutlineVectorTex", properties);
            CaptureFloat(material, "_OutlineEnableLighting", properties);
            CaptureFloat(material, "_OutlineZBias", properties);
            CaptureVector(material, "_OutlineTexHSVG", properties);

            // === Emission ===
            CaptureTexture(material, "_EmissionMap", properties);
            CaptureColor(material, "_EmissionColor", properties);
            CaptureTexture(material, "_EmissionBlendMask", properties);
            CaptureVector(material, "_EmissionMap_ScrollRotate", properties);
            CaptureVector(material, "_EmissionBlendMask_ScrollRotate", properties);
            CaptureFloat(material, "_EmissionBlend", properties);
            CaptureFloat(material, "_EmissionBlendMode", properties);
            CaptureTexture(material, "_EmissionGradTex", properties);
            CaptureFloat(material, "_EmissionGradSpeed", properties);
            CaptureVector(material, "_EmissionBlink", properties);
            CaptureTexture(material, "_Emission2ndMap", properties);
            CaptureColor(material, "_Emission2ndColor", properties);
            CaptureTexture(material, "_Emission2ndBlendMask", properties);

            // === MatCap ===
            CaptureTexture(material, "_MatCapTex", properties);
            CaptureTexture(material, "_MatCapBlendMask", properties);
            CaptureColor(material, "_MatCapColor", properties);
            CaptureFloat(material, "_MatCapBlend", properties);
            CaptureFloat(material, "_MatCapBlendMode", properties);
            // MatCap 2nd
            CaptureTexture(material, "_MatCap2ndTex", properties);
            CaptureTexture(material, "_MatCap2ndBlendMask", properties);
            CaptureColor(material, "_MatCap2ndColor", properties);
            CaptureFloat(material, "_MatCap2ndBlend", properties);
            CaptureFloat(material, "_MatCap2ndBlendMode", properties);

            // === Specular / Surface ===
            CaptureFloat(material, "_Smoothness", properties);
            CaptureFloat(material, "_Metallic", properties);
            CaptureFloat(material, "_SpecularToon", properties);
            CaptureFloat(material, "_SpecularBorder", properties);
            CaptureFloat(material, "_SpecularBlur", properties);

            CaptureFloat(material, "_LightMinLimit", properties);
            CaptureFloat(material, "_LightMaxLimit", properties);
            CaptureFloat(material, "_MonochromeLighting", properties);
            CaptureFloat(material, "_AsUnlit", properties);

            // === Gem / Refraction (lilToonGem specific) ===
            CaptureFloat(material, "_GemChromaticAberration", properties);
            CaptureColor(material, "_GemParticleColor", properties);
            CaptureFloat(material, "_GemEnvContrast", properties);
            CaptureFloat(material, "_GemVRParallaxStrength", properties);
            CaptureFloat(material, "_RefractionStrength", properties);
            CaptureFloat(material, "_RefractionFresnelPower", properties);

            // === Render State ===
            CaptureFloat(material, "_Cull", properties);
            CaptureFloat(material, "_ZWrite", properties);
            CaptureFloat(material, "_SrcBlend", properties);
            CaptureFloat(material, "_DstBlend", properties);
            CaptureFloat(material, "_AlphaToMask", properties);

            // === Stencil ===
            // 命名が lilToon と一致するため素通しで引き継げる。捨てると、
            // See Through Hair 系のセットアップ済みアバターが移行で壊れる。
            CaptureFloat(material, "_StencilRef", properties);
            CaptureFloat(material, "_StencilComp", properties);
            CaptureFloat(material, "_StencilOp", properties);
            CaptureFloat(material, "_StencilFail", properties);
            CaptureFloat(material, "_StencilZFail", properties);
            CaptureFloat(material, "_StencilReadMask", properties);
            CaptureFloat(material, "_StencilWriteMask", properties);

            return properties;
        }

        private void ApplyLilToonMigrationMetadata(Material targetMaterial, Material sourceMaterial, string sourceShaderName)
        {
            if (targetMaterial == null)
            {
                return;
            }

            if (targetMaterial.HasProperty("_LilToonMigrated"))
            {
                targetMaterial.SetFloat("_LilToonMigrated", 1.0f);
            }

            if (targetMaterial.HasProperty("_LilToonMigrationMode"))
            {
                targetMaterial.SetFloat("_LilToonMigrationMode", (float)ToLilToonMigrationModeValue(conversionMode));
            }

            targetMaterial.SetOverrideTag("NataneLilToonSourceShader", string.IsNullOrEmpty(sourceShaderName) ? "lilToon" : sourceShaderName);
            targetMaterial.SetOverrideTag("NataneLilToonMigrationVersion", MigrationMetadataVersion);
            targetMaterial.SetOverrideTag("NataneLilToonSourceMaterial", sourceMaterial != null ? sourceMaterial.name : string.Empty);
        }

        private void ApplyLilToonParityFlags(Material targetMaterial, Dictionary<string, object> sourceProps)
        {
            if (targetMaterial == null || !targetMaterial.HasProperty("_LilToonParityFlags"))
            {
                return;
            }

            targetMaterial.SetFloat("_LilToonParityFlags", (float)BuildLilToonParityFlags(sourceProps));
        }

        private int BuildLilToonParityFlags(Dictionary<string, object> sourceProps)
        {
            LilToonParityFlags flags = LilToonParityFlags.None;

            if (GetFloatOr(sourceProps, "_UseRimShade", 0.0f) > 0.5f)
            {
                flags |= LilToonParityFlags.RimShadeUnsupported;
            }

            if (GetFloatOr(sourceProps, "_UseEmission2nd", 0.0f) > 0.5f)
            {
                flags |= LilToonParityFlags.Emission2ndUnsupported;
            }

            if (GetFloatOr(sourceProps, "_ShadowBorderRange", 0.0f) > 0.001f)
            {
                flags |= LilToonParityFlags.ShadowBorderRangeUnsupported;
            }

            if (Mathf.RoundToInt(GetFloatOr(sourceProps, "_ShadowMaskType", 0.0f)) != 0)
            {
                flags |= LilToonParityFlags.ShadowMaskTypeUnsupported;
            }

            if (GetFloatOr(sourceProps, "_BackfaceForceShadow", 0.0f) > 0.001f)
            {
                flags |= LilToonParityFlags.BackfaceForceShadowUnsupported;
            }

            if (GetFloatOr(sourceProps, "_ShadowPostAO", 0.0f) > 0.5f)
            {
                flags |= LilToonParityFlags.ShadowPostAOUnsupported;
            }

            if (GetFloatOr(sourceProps, "_UseMatCap", 0.0f) > 0.5f || GetFloatOr(sourceProps, "_UseMatCap2nd", 0.0f) > 0.5f)
            {
                flags |= LilToonParityFlags.MatCapNeedsReview;
            }

            if (GetFloatOr(sourceProps, "_UseOutline", 0.0f) > 0.5f)
            {
                flags |= LilToonParityFlags.OutlineNeedsReview;
            }

            return (int)flags;
        }

        private static int ToLilToonMigrationModeValue(ConversionMode mode)
        {
            switch (mode)
            {
                case ConversionMode.VisualMatch:
                    return 2;
                case ConversionMode.MinimalSafe:
                    return 3;
                default:
                    return 0;
            }
        }

        private void CaptureTexture(Material material, string propertyName, Dictionary<string, object> properties)
        {
            if (material.HasProperty(propertyName))
            {
                properties[propertyName] = material.GetTexture(propertyName);
            }
        }

        private void CaptureColor(Material material, string propertyName, Dictionary<string, object> properties)
        {
            if (material.HasProperty(propertyName))
            {
                properties[propertyName] = material.GetColor(propertyName);
            }
        }

        private void CaptureFloat(Material material, string propertyName, Dictionary<string, object> properties)
        {
            if (material.HasProperty(propertyName))
            {
                properties[propertyName] = material.GetFloat(propertyName);
            }
        }

        private void CaptureVector(Material material, string propertyName, Dictionary<string, object> properties)
        {
            if (material.HasProperty(propertyName))
            {
                properties[propertyName] = material.GetVector(propertyName);
            }
        }

        private float GetNataneShadingModeFromLilToon(Dictionary<string, object> sourceProps)
        {
            float metallic = GetFloatOr(sourceProps, "_Metallic", 0.0f);
            float smoothness = GetFloatOr(sourceProps, "_Smoothness", 0.0f);
            float shadowBlur = GetFloatOr(sourceProps, "_ShadowBlur", 0.1f);
            float reflectionStrength = GetFloatOr(sourceProps, "_ReflectionSpecular", 0.0f);

            if (metallic >= 0.35f || smoothness >= 0.75f || reflectionStrength >= 0.35f)
            {
                return 3.0f;
            }

            if (shadowBlur >= 0.18f)
            {
                return 1.0f;
            }

            return 0.0f;
        }

        private void ApplyNataneShadowSettingsFromLilToon(
            Dictionary<string, object> sourceProps,
            Material targetMaterial,
            ConversionReport report)
        {
            float border = Mathf.Clamp01(GetFloatOr(sourceProps, "_ShadowBorder", 0.5f));
            float blur = Mathf.Clamp01(GetFloatOr(sourceProps, "_ShadowBlur", 0.1f));
            float nataneShadingMode = GetNataneShadingModeFromLilToon(sourceProps);

            targetMaterial.SetFloat("_ShadingMode", nataneShadingMode);
            // lilToon border grows the shadowed region, while positive Natane offset grows the lit region.
            targetMaterial.SetFloat("_ShadowOffset", Mathf.Clamp(0.5f - border, -1.0f, 1.0f));
            targetMaterial.SetFloat("_ShadowSharpness", Mathf.Clamp(Mathf.Max(blur, 0.05f), 0.001f, 1.0f));
            targetMaterial.SetFloat("_ShadingGradientWidth", Mathf.Clamp(Mathf.Max(blur * 1.5f, 0.05f), 0.001f, 1.0f));
            targetMaterial.SetFloat("_ShadowSteps", 2.0f);
            targetMaterial.SetFloat("_ShadowSmoothing", Mathf.Clamp(blur * 0.3f, 0.05f, 0.5f));
            targetMaterial.SetFloat("_StepBorderSmooth", Mathf.Clamp(blur * 0.5f, 0.1f, 0.5f));

            string nataneModeLabel = nataneShadingMode >= 2.5f
                ? "PBR-Like"
                : nataneShadingMode >= 0.5f
                    ? "Gradient"
                    : "Toon";
            report.infos.Add(
                $"Natane base shading derived: Mode={nataneModeLabel}, Offset={targetMaterial.GetFloat("_ShadowOffset"):F2}, " +
                $"Sharpness={targetMaterial.GetFloat("_ShadowSharpness"):F2}, GradientWidth={targetMaterial.GetFloat("_ShadingGradientWidth"):F2}");
        }

        private void ApplyMigratedLightingWorkflow(Material targetMaterial, ConversionReport report)
        {
            if (targetMaterial == null || !targetMaterial.HasProperty("_UsePixelVertexLights"))
            {
                return;
            }
            targetMaterial.SetFloat("_UsePixelVertexLights", 1.0f);
            targetMaterial.EnableKeyword("_PIXEL_VERTEX_LIGHTS");
            report.infos.Add("Natane lighting workflow: Pixel-Precision Vertex Lights ON.");
        }

        private void MapProperties(Dictionary<string, object> sourceProps, Material targetMaterial)
        {
            var dummyReport = new ConversionReport();
            MapPropertiesWithReport(sourceProps, targetMaterial, dummyReport);
        }

        private void MapPropertiesWithReport(Dictionary<string, object> sourceProps, Material targetMaterial, ConversionReport report)
        {
            // =============================================
            // =============================================

            // === Main Texture & Color ===
            SetTextureIfExists(sourceProps, "_MainTex", targetMaterial, "_MainTex");
            SetColorIfExists(sourceProps, "_Color", targetMaterial, "_Color");

            // === Shadow ===
            bool useShadow = GetFloatOr(sourceProps, "_UseShadow", 0) > 0.5f;

            if (sourceProps.ContainsKey("_ShadowColor"))
            {
                Color shadowColor = (Color)sourceProps["_ShadowColor"];

                float shadowStrength = GetFloatOr(sourceProps, "_ShadowStrength", 1.0f);
                if (shadowStrength < 0.99f)
                {
                    shadowColor = Color.Lerp(Color.white, shadowColor, shadowStrength);
                    report.infos.Add($"ShadowStrength={shadowStrength:F2}: shadow color was blended toward white.");
                }

                targetMaterial.SetColor("_ShadowColor", shadowColor);

                // Natane: directResult = lerp(_ShadowColor, (1,1,1), shadingValue)
                if (shadowColor.r < 0.95f || shadowColor.g < 0.95f || shadowColor.b < 0.95f)
                {
                    report.infos.Add(
                        $"Shadow Color: ({shadowColor.r:F2},{shadowColor.g:F2},{shadowColor.b:F2})" +
                        " Mapping may differ slightly because lilToon multiplies indirect color while Natane uses lerp-based shading.");
                }
            }

            // =============================================
            //
            // =============================================
            float border = GetFloatOr(sourceProps, "_ShadowBorder", 0.5f);
            float blur = GetFloatOr(sourceProps, "_ShadowBlur", 0.1f);
            float strength = GetFloatOr(sourceProps, "_ShadowStrength", 1.0f);

            targetMaterial.SetFloat("_STShadowBorder", border);
            targetMaterial.SetFloat("_STShadowBlur", blur);
            targetMaterial.SetFloat("_STShadowStrength", strength);
            report.infos.Add($"Compatibility shadow captured: Border={border:F2}, Blur={blur:F2}, Strength={strength:F2}");

            float shadowReceive = useShadow ? GetFloatOr(sourceProps, "_ShadowReceive", 1.0f) : 0.0f;
            if (targetMaterial.HasProperty("_ShadowReceive"))
            {
                targetMaterial.SetFloat("_ShadowReceive", shadowReceive);
            }
            report.infos.Add($"Shadow Receive mapped: {shadowReceive:F2}");

            ApplyNataneShadowSettingsFromLilToon(sourceProps, targetMaterial, report);
            ApplyMigratedLightingWorkflow(targetMaterial, report);

            if (useShadow)
            {
                MapMultiShadowLayers(sourceProps, targetMaterial, report);
            }
            else
            {
                targetMaterial.SetColor("_ShadowColor", Color.white);
                targetMaterial.SetFloat("_STShadowStrength", 0.0f);
                if (targetMaterial.HasProperty("_ShadowColorTexStrength"))
                {
                    targetMaterial.SetFloat("_ShadowColorTexStrength", 0.0f);
                }
                targetMaterial.DisableKeyword("_USE_MULTI_SHADOW");
                report.infos.Add("Source shadow was disabled, so Natane shadow tint/receive were neutralized.");
            }

            // === Shadow Color Texture ===
            // Natane: _ShadowColorTex + _ShadowColorTexStrength
            if (sourceProps.ContainsKey("_ShadowColorTex") && sourceProps["_ShadowColorTex"] != null)
            {
                targetMaterial.SetTexture("_ShadowColorTex", (Texture)sourceProps["_ShadowColorTex"]);
                targetMaterial.SetFloat("_ShadowColorTexStrength", 1.0f);
                report.infos.Add("Shadow Color Texture mapped.");
            }

            float shadowNormalStrength = GetFloatOr(sourceProps, "_ShadowNormalStrength", 1.0f);
            float mappedBumpScale = GetFloatOr(sourceProps, "_BumpScale", 1.0f);
            if (shadowNormalStrength < 0.99f)
            {
                float originalBumpScale = mappedBumpScale;
                mappedBumpScale *= shadowNormalStrength;
                report.infos.Add($"Shadow Normal: Strength={shadowNormalStrength:F2}, BumpScale {originalBumpScale:F2} -> {mappedBumpScale:F2}");
            }

            // === Normal Map ===
            SetTextureIfExists(sourceProps, "_BumpMap", targetMaterial, "_BumpMap");
            if (sourceProps.ContainsKey("_BumpScale") || shadowNormalStrength < 0.99f)
            {
                targetMaterial.SetFloat("_BumpScale", mappedBumpScale);
            }

            if (sourceProps.ContainsKey("_BumpMap") && sourceProps["_BumpMap"] != null)
            {
                targetMaterial.SetFloat("_UseNormalMap", 1.0f);
                targetMaterial.EnableKeyword("_NORMALMAP");
            }

            // === Rim Light ===
            bool useRim = GetFloatOr(sourceProps, "_UseRim", 0) > 0.5f;
            bool useRimShade = GetFloatOr(sourceProps, "_UseRimShade", 0) > 0.5f;

            if (useRim)
            {
                targetMaterial.SetFloat("_RimLight", 1.0f);
                targetMaterial.EnableKeyword("_RIM_LIGHT");

                // RimColor
                if (sourceProps.ContainsKey("_RimColor"))
                {
                    targetMaterial.SetColor("_RimColor", (Color)sourceProps["_RimColor"]);
                }

                float rimPower = GetFloatOr(sourceProps, "_RimFresnelPower", 3.5f);
                targetMaterial.SetFloat("_RimPower", Mathf.Clamp(rimPower, 0.1f, 10f));
                report.infos.Add($"Rim: FresnelPower={rimPower:F2} -> RimPower={Mathf.Clamp(rimPower, 0.1f, 10f):F2}");

                targetMaterial.SetFloat("_RimIntensity", 1.0f);

                float rimBorder = GetFloatOr(sourceProps, "_RimBorder", 0.5f);
                float rimSpread = Mathf.Clamp01(1.0f - rimBorder);
                targetMaterial.SetFloat("_RimSpread", rimSpread);
                report.infos.Add($"Rim: Border={rimBorder:F2} -> RimSpread={rimSpread:F2}");

                float rimBlur = GetFloatOr(sourceProps, "_RimBlur", 0.65f);
                if (rimBlur > 0.01f)
                {
                    float currentPower = targetMaterial.GetFloat("_RimPower");
                    float adjustedPower = currentPower * (1.0f - rimBlur * 0.5f);
                    targetMaterial.SetFloat("_RimPower", Mathf.Max(adjustedPower, 0.1f));
                    report.infos.Add($"Rim: Blur={rimBlur:F2} -> adjusted RimPower={adjustedPower:F2}");
                }

                float rimEnableLighting = GetFloatOr(sourceProps, "_RimEnableLighting", 1.0f);
                targetMaterial.SetFloat("_RimDirStrength", rimEnableLighting);

                // RimShadowMask
                float rimShadowMask = GetFloatOr(sourceProps, "_RimShadowMask", 0.5f);
                targetMaterial.SetFloat("_RimShadowMask", rimShadowMask);

                int lilRimBlendMode = sourceProps.ContainsKey("_RimBlendMode")
                    ? (int)(float)sourceProps["_RimBlendMode"]
                    : 0;
                int nataneRimBlendMode = ConvertRimBlendMode(lilRimBlendMode);
                targetMaterial.SetFloat("_RimBlendMode", nataneRimBlendMode);

                string[] lilNames = { "Normal", "Add", "Screen", "Multiply" };
                string[] natNames = { "Normal", "Soft", "Screen", "Overlay" };
                report.infos.Add($"Rim Light enabled: DirStrength={rimEnableLighting:F2}, ShadowMask={rimShadowMask:F2}, BlendMode={lilNames[Mathf.Clamp(lilRimBlendMode, 0, 3)]}→{natNames[nataneRimBlendMode]}");
            }

            if (useRimShade)
            {
                report.warnings.Add("Rim Shade is enabled in lilToon, but Natane has no direct compatible path yet. Manual adjustment is still required.");
            }

            // === Outline ===
            // lilToon のアウトラインバリアント (lilToonOutline, lilToonCutoutOutline,
            // lilToonOnePassTransparentOutline 等) はシェーダー名に "outline" を含む。
            // _UseOutline フラグやシェーダー名からアウトライン有効を判定する。
            string sourceShaderName = sourceProps.ContainsKey("__sourceShaderName") ? (string)sourceProps["__sourceShaderName"] : "";
            string sourceShaderLower = sourceShaderName.ToLower();
            bool isOutlineVariant = sourceShaderLower.Contains("outline");
            bool useOutlineFlag = GetFloatOr(sourceProps, "_UseOutline", 0.0f) > 0.5f;
            bool hasOutline = false;

            if (sourceProps.ContainsKey("_OutlineWidth"))
            {
                float originalWidth = (float)sourceProps["_OutlineWidth"];
                if (originalWidth > 0)
                {
                    float convertedWidth = originalWidth * 0.1f;
                    convertedWidth = Mathf.Clamp(convertedWidth, 0.001f, 1.0f);
                    targetMaterial.SetFloat("_OutlineWidth", convertedWidth);
                    hasOutline = true;

                    report.outlineWidthAdjusted = true;
                    report.originalOutlineWidth = originalWidth;
                    report.convertedOutlineWidth = convertedWidth;
                    report.infos.Add($"Outline Width: lilToon {originalWidth:F4} -> Natane {convertedWidth:F4} (0.1 scale)");
                }
                else if (isOutlineVariant || useOutlineFlag)
                {
                    // アウトラインバリアントなのに width=0 → デフォルト幅を設定
                    targetMaterial.SetFloat("_OutlineWidth", 0.05f);
                    hasOutline = true;
                    report.infos.Add("Outline Width was 0 but source is outline variant. Set default width 0.05.");
                }
            }
            else if (isOutlineVariant || useOutlineFlag)
            {
                // _OutlineWidth プロパティが存在しない場合もデフォルト設定
                targetMaterial.SetFloat("_OutlineWidth", 0.05f);
                hasOutline = true;
                report.infos.Add("Outline enabled from source shader variant. Set default width 0.05.");
            }

            if (sourceProps.ContainsKey("_OutlineColor"))
            {
                Color outlineColor = (Color)sourceProps["_OutlineColor"];

                // 半透明アウトラインバリアント (OnePassTransparent, TwoPassTransparent 等) は
                // アウトライン色をアルファ0の黒に設定し、半透明描画と自然に馴染ませる
                bool isTransparentOutline = sourceShaderLower.Contains("transparent") && isOutlineVariant;
                if (isTransparentOutline)
                {
                    outlineColor = new Color(0f, 0f, 0f, 0f);
                    report.infos.Add("Outline Color set to transparent black (0,0,0,0) for transparent outline variant.");
                }

                targetMaterial.SetColor("_OutlineColor", outlineColor);
                hasOutline = true;
            }
            else if (sourceShaderLower.Contains("transparent") && isOutlineVariant)
            {
                // _OutlineColor が未設定でも半透明バリアントなら透明黒をセット
                targetMaterial.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0f));
                hasOutline = true;
                report.infos.Add("Outline Color defaulted to transparent black (0,0,0,0) for transparent outline variant.");
            }

            // Outline Texture (lilToon _OutlineTex → Natane _OutlineTex)
            SetTextureIfExists(sourceProps, "_OutlineTex", targetMaterial, "_OutlineTex");

            if (sourceProps.ContainsKey("_OutlineWidthMask") && sourceProps["_OutlineWidthMask"] != null)
            {
                targetMaterial.SetTexture("_OutlineWidthMap", (Texture)sourceProps["_OutlineWidthMask"]);
                targetMaterial.SetFloat("_UseOutlineWidthMap", 1.0f);
                targetMaterial.EnableKeyword("_OUTLINE_WIDTH_MAP");
                hasOutline = true;
                report.infos.Add("Outline Width Mask mapped to Natane Outline Width Map.");
            }

            // Smooth Normal (lilToon _OutlineVectorTex → Natane smooth normal)
            if (sourceProps.ContainsKey("_OutlineVectorTex") && sourceProps["_OutlineVectorTex"] != null)
            {
                targetMaterial.SetTexture("_SmoothNormalTex", (Texture)sourceProps["_OutlineVectorTex"]);
                targetMaterial.SetFloat("_SmoothNormal", 1.0f);
                targetMaterial.EnableKeyword("_SMOOTH_NORMAL");
                report.infos.Add("Outline Vector Texture mapped to Smooth Normal.");
            }

            // アウトラインバリアントまたはフラグで強制有効化
            if (isOutlineVariant || useOutlineFlag)
            {
                hasOutline = true;
            }

            if (hasOutline)
            {
                targetMaterial.SetFloat("_Outline", 1.0f);
                targetMaterial.EnableKeyword("_OUTLINE");
                if (isOutlineVariant)
                    report.infos.Add($"Outline enabled (source: {sourceShaderName}).");
            }

            // === Emission ===
            bool useEmission = GetFloatOr(sourceProps, "_UseEmission", 0) > 0.5f;
            bool useEmission2nd = GetFloatOr(sourceProps, "_UseEmission2nd", 0) > 0.5f;

            if (useEmission)
            {
                // Emission Map (色マップ)
                SetTextureIfExists(sourceProps, "_EmissionMap", targetMaterial, "_EmissionMap");
                if (sourceProps.ContainsKey("_EmissionColor"))
                {
                    targetMaterial.SetColor("_EmissionColor", (Color)sourceProps["_EmissionColor"]);
                }

                // Emission Mask (マスクテクスチャ)
                // lilToon: _EmissionBlendMask → Natane: _EmissionMask
                SetTextureIfExists(sourceProps, "_EmissionBlendMask", targetMaterial, "_EmissionMask");

                // Blend 強度・モード
                float emissionBlend = GetFloatOr(sourceProps, "_EmissionBlend", 1.0f);
                targetMaterial.SetFloat("_EmissionBlend", emissionBlend);

                if (sourceProps.ContainsKey("_EmissionBlendMode"))
                {
                    // lilToon: 0=Normal, 1=Add, 2=Screen, 3=Multiply
                    // Natane:  0=Normal, 1=Soft, 2=Screen, 3=Overlay
                    int lilBlendMode = (int)GetFloatOr(sourceProps, "_EmissionBlendMode", 0.0f);
                    int nataneBlendMode = ConvertEffectBlendMode(lilBlendMode);
                    targetMaterial.SetFloat("_EmissionBlendMode", nataneBlendMode);
                    string[] lilNames = { "Normal", "Add", "Screen", "Multiply" };
                    string[] natNames = { "Normal", "Soft", "Screen", "Overlay" };
                    report.infos.Add($"Emission BlendMode: lilToon {lilNames[Mathf.Clamp(lilBlendMode, 0, 3)]} → Natane {natNames[nataneBlendMode]}");
                }

                // Scroll/Rotate: lilToon _EmissionMap_ScrollRotate (Vector4: scrollX, scrollY, ?, rotate)
                if (sourceProps.ContainsKey("_EmissionMap_ScrollRotate"))
                {
                    Vector4 scrollRotate = (Vector4)sourceProps["_EmissionMap_ScrollRotate"];
                    // Natane: 個別プロパティに分解
                    if (Mathf.Abs(scrollRotate.x) > 0.001f || Mathf.Abs(scrollRotate.y) > 0.001f)
                    {
                        targetMaterial.SetFloat("_EmissionScroll", 1.0f);
                        targetMaterial.SetFloat("_EmissionScrollSpeed", scrollRotate.x);
                        targetMaterial.SetFloat("_EmissionScrollSpeedY", scrollRotate.y);
                        report.infos.Add($"Emission Scroll: X={scrollRotate.x:F2}, Y={scrollRotate.y:F2}");
                    }
                    if (Mathf.Abs(scrollRotate.w) > 0.001f)
                    {
                        targetMaterial.SetFloat("_EmissionRotateSpeed", scrollRotate.w);
                        report.infos.Add($"Emission Rotate Speed: {scrollRotate.w:F2}");
                    }
                }

                // Mask Scroll/Rotate
                if (sourceProps.ContainsKey("_EmissionBlendMask_ScrollRotate"))
                {
                    Vector4 maskScrollRotate = (Vector4)sourceProps["_EmissionBlendMask_ScrollRotate"];
                    if (targetMaterial.HasProperty("_EmissionMaskScrollSpeed"))
                    {
                        targetMaterial.SetVector("_EmissionMaskScrollSpeed", new Vector4(maskScrollRotate.x, maskScrollRotate.y, 0, 0));
                    }
                    if (Mathf.Abs(maskScrollRotate.w) > 0.001f && targetMaterial.HasProperty("_EmissionMaskRotateSpeed"))
                    {
                        targetMaterial.SetFloat("_EmissionMaskRotateSpeed", maskScrollRotate.w);
                    }
                }

                // Blink → Pulse 変換
                // lilToon _EmissionBlink: (strength, speed, offset, ?)
                if (sourceProps.ContainsKey("_EmissionBlink"))
                {
                    Vector4 blink = (Vector4)sourceProps["_EmissionBlink"];
                    if (blink.x > 0.001f)
                    {
                        targetMaterial.SetFloat("_EmissionPulse", 1.0f);
                        targetMaterial.SetFloat("_EmissionPulseAmplitude", blink.x);
                        targetMaterial.SetFloat("_EmissionPulseSpeed", blink.y);
                        report.infos.Add($"Emission Blink → Pulse: Amplitude={blink.x:F2}, Speed={blink.y:F2}");
                    }
                }

                // Gradient Texture (lilToon 固有、Natane 非対応の場合は警告)
                if (sourceProps.ContainsKey("_EmissionGradTex") && sourceProps["_EmissionGradTex"] != null)
                {
                    report.warnings.Add("Emission Gradient Texture (_EmissionGradTex) は Natane に直接対応がないため、手動調整が必要です。");
                }

                targetMaterial.SetFloat("_Emission", 1.0f);
                targetMaterial.EnableKeyword("_EMISSION");
                report.infos.Add($"Emission mapped (Blend={emissionBlend:F2}).");
            }

            if (useEmission2nd)
            {
                report.warnings.Add("Emission 2nd is enabled in lilToon, but Natane has no equivalent second emission layer yet.");
            }

            // === MatCap ===
            bool useMatCap = GetFloatOr(sourceProps, "_UseMatCap", 0) > 0.5f;

            if (useMatCap && sourceProps.ContainsKey("_MatCapTex") && sourceProps["_MatCapTex"] != null)
            {
                SetTextureIfExists(sourceProps, "_MatCapTex", targetMaterial, "_MatCapTex");

                targetMaterial.SetFloat("_MatCap", 0.0f);
                targetMaterial.DisableKeyword("_MATCAP");

                float matCapBlend = GetFloatOr(sourceProps, "_MatCapBlend", 1.0f);
                targetMaterial.SetFloat("_MatCapIntensity", 1.0f);
                targetMaterial.SetFloat("_MatCapBlend", matCapBlend);

                int lilBlendMode = 1;
                if (sourceProps.ContainsKey("_MatCapBlendMode"))
                {
                    lilBlendMode = (int)(float)sourceProps["_MatCapBlendMode"];
                }
                int nataneBlendMode = ConvertMatCapBlendMode(lilBlendMode);
                targetMaterial.SetFloat("_MatCapBlendMode", nataneBlendMode);

                SetTextureIfExists(sourceProps, "_MatCapBlendMask", targetMaterial, "_MatCapMask");

                report.infos.Add("MatCap texture, blend mask, and blend mode were captured. The feature stays disabled by default.");
                report.warnings.Add(
                    "MatCap is implemented differently between lilToon and Natane. " +
                    "Review the final look and enable the feature manually if needed.");
            }

            // === MatCap 2nd ===
            bool useMatCap2nd = GetFloatOr(sourceProps, "_UseMatCap2nd", 0) > 0.5f;

            if (useMatCap2nd && sourceProps.ContainsKey("_MatCap2ndTex") && sourceProps["_MatCap2ndTex"] != null)
            {
                SetTextureIfExists(sourceProps, "_MatCap2ndTex", targetMaterial, "_MatCapTex2");

                targetMaterial.SetFloat("_MatCap2", 0.0f);
                targetMaterial.DisableKeyword("_MATCAP_2");

                float matCap2ndBlend = GetFloatOr(sourceProps, "_MatCap2ndBlend", 1.0f);
                targetMaterial.SetFloat("_MatCapIntensity2", 1.0f);
                targetMaterial.SetFloat("_MatCapBlend2", matCap2ndBlend);

                int lilBlendMode2 = 1;
                if (sourceProps.ContainsKey("_MatCap2ndBlendMode"))
                {
                    lilBlendMode2 = (int)(float)sourceProps["_MatCap2ndBlendMode"];
                }
                int nataneBlendMode2 = ConvertMatCapBlendMode(lilBlendMode2);
                targetMaterial.SetFloat("_MatCapBlendMode2", nataneBlendMode2);

                SetTextureIfExists(sourceProps, "_MatCap2ndBlendMask", targetMaterial, "_MatCapMask2");

                report.infos.Add("MatCap 2nd texture, blend mask, and blend mode were captured. The feature stays disabled by default.");
                report.warnings.Add(
                    "MatCap 2nd is implemented differently between lilToon and Natane. " +
                    "Review the final look and enable the feature manually if needed.");
            }

            // === Specular ===
            float metallic = GetFloatOr(sourceProps, "_Metallic", 0);
            if (metallic > 0.01f)
            {
                bool hasSpecular = false;

                if (sourceProps.ContainsKey("_SpecularToon"))
                {
                    float specToon = (float)sourceProps["_SpecularToon"];
                    if (specToon > 0.5f && sourceProps.ContainsKey("_SpecularBorder"))
                    {
                        float specBorder = (float)sourceProps["_SpecularBorder"];
                        float specBlur = GetFloatOr(sourceProps, "_SpecularBlur", 0.0f);

                        float size = Mathf.Clamp01(1.0f - specBorder);
                        float softness = Mathf.Clamp01(specBlur);
                        targetMaterial.SetFloat("_SpecularSize", size);
                        targetMaterial.SetFloat("_SpecularSoftness", Mathf.Max(softness, 0.05f));
                        hasSpecular = true;
                        report.infos.Add($"Specular(Toon): Border={specBorder:F2} -> Size={size:F2}, Metallic={metallic:F2}");
                    }
                }

                if (!hasSpecular && sourceProps.ContainsKey("_Smoothness"))
                {
                    float smoothness = (float)sourceProps["_Smoothness"];
                    float specularSize = OptimizeSpecularSize(smoothness);
                    targetMaterial.SetFloat("_SpecularSize", specularSize);
                    hasSpecular = true;
                    report.infos.Add($"Specular(PBR): Smoothness={smoothness:F2} -> Size={specularSize:F3}");
                }

                if (hasSpecular)
                {
                    targetMaterial.SetFloat("_Specular", 1.0f);
                    targetMaterial.EnableKeyword("_SPECULAR");
                    targetMaterial.SetColor("_SpecularColor", new Color(1, 1, 1, 1));
                    report.infos.Add("Specular enabled (fallback from Metallic).");
                }
            }
            else
            {
                report.infos.Add($"Specular mapped from Metallic: Metallic={metallic:F2}");
            }

            // === Texture-less Material Compensation (Gem / Metal / etc.) ===
            ApplyTexturelessMaterialCompensation(sourceProps, targetMaterial, report);

            // === Alpha Cutoff ===
            if (sourceProps.ContainsKey("_Cutoff"))
            {
                float cutoff = (float)sourceProps["_Cutoff"];
                targetMaterial.SetFloat("_Cutoff", cutoff);
                report.infos.Add($"Alpha Cutoff: {cutoff:F2}");
            }

            targetMaterial.SetFloat("_LightIntensity", 1.0f);
            targetMaterial.SetFloat("_LightMaxInfluence", 2.0f);
            targetMaterial.SetFloat("_Brightness", 1.0f);
            targetMaterial.SetFloat("_Saturation", 1.0f);
            report.infos.Add("Natane light defaults applied (_LightIntensity=1.0).");

            targetMaterial.SetFloat("_ShadowMaxDarkness", 0.15f);
            targetMaterial.SetFloat("_LightMinInfluence", 0.05f);
            targetMaterial.SetFloat("_GIIntensity", 0.0f);
            report.infos.Add("Floor/GI defaults applied (_GIIntensity=0).");

            float lightMinLimit = GetFloatOr(sourceProps, "_LightMinLimit", 0.05f);
            float lightMaxLimit = GetFloatOr(sourceProps, "_LightMaxLimit", 1.0f);
            float monochromeLighting = GetFloatOr(sourceProps, "_MonochromeLighting", 0.0f);

            targetMaterial.SetFloat("_LightColorMin", lightMinLimit);
            targetMaterial.SetFloat("_LightColorMax", lightMaxLimit);
            targetMaterial.SetFloat("_MonochromeLighting", monochromeLighting);
            report.infos.Add($"StandardToon light color limits mapped: ColorMax={lightMaxLimit:F2}, ColorMin={lightMinLimit:F2}, Monochrome={monochromeLighting:F2}");

            float asUnlit = GetFloatOr(sourceProps, "_AsUnlit", 0.0f);
            targetMaterial.SetFloat("_STAsUnlit", asUnlit);
            if (asUnlit > 0.01f)
            {
                report.infos.Add($"AsUnlit mapped to _STAsUnlit={asUnlit:F2}");
            }

            // StandardToon v2: stIndirectCol = lerp(stIndirectCol, stAlbedo, saturate(stIndLightColor * _STShadowEnvStrength))
            float shadowEnvStrength = GetFloatOr(sourceProps, "_ShadowEnvStrength", 1.0f);
            targetMaterial.SetFloat("_STShadowEnvStrength", shadowEnvStrength);
            if (targetMaterial.HasProperty("_ShadowEnvStrength"))
            {
                targetMaterial.SetFloat("_ShadowEnvStrength", shadowEnvStrength);
            }
            report.infos.Add($"Shadow Env Strength mapped: compatibility={shadowEnvStrength:F2}, natane={shadowEnvStrength:F2}");

            // Exact compatibility hidden payloads used by the StandardToon branch.
            if (targetMaterial.HasProperty("_ShadowMainStrength"))
            {
                targetMaterial.SetFloat("_ShadowMainStrength", GetFloatOr(sourceProps, "_ShadowMainStrength", 0.0f));
            }
            if (targetMaterial.HasProperty("_Shadow2ndBlur"))
            {
                targetMaterial.SetFloat("_Shadow2ndBlur", GetFloatOr(sourceProps, "_Shadow2ndBlur", 0.1f));
            }
            if (targetMaterial.HasProperty("_Shadow3rdBlur"))
            {
                targetMaterial.SetFloat("_Shadow3rdBlur", GetFloatOr(sourceProps, "_Shadow3rdBlur", 0.1f));
            }

            SetTextureIfExists(sourceProps, "_ShadowStrengthMask", targetMaterial, "_ShadowStrengthMask");
            SetTextureIfExists(sourceProps, "_ShadowBorderMask", targetMaterial, "_ShadowBorderMask");
            SetTextureIfExists(sourceProps, "_ShadowBlurMask", targetMaterial, "_ShadowBlurMask");

            if (sourceProps.ContainsKey("_ShadowStrengthMask") && sourceProps["_ShadowStrengthMask"] != null)
            {
                report.infos.Add("Shadow Strength Mask copied for Exact Compatibility.");
            }
            if (sourceProps.ContainsKey("_ShadowBorderMask") && sourceProps["_ShadowBorderMask"] != null)
            {
                report.infos.Add("Shadow Border Mask copied for Exact Compatibility.");
            }
            if (sourceProps.ContainsKey("_ShadowBlurMask") && sourceProps["_ShadowBlurMask"] != null)
            {
                report.infos.Add("Shadow Blur Mask copied for Exact Compatibility.");
            }

            float shadowBorderRange = GetFloatOr(sourceProps, "_ShadowBorderRange", 0.08f);
            if (shadowBorderRange > 0.001f)
            {
                report.warnings.Add($"Shadow Border Range ({shadowBorderRange:F2}) still has no exact Natane equivalent. Final gradation can differ.");
            }

            float backfaceForceShadow = GetFloatOr(sourceProps, "_BackfaceForceShadow", 0.0f);
            if (backfaceForceShadow > 0.001f)
            {
                report.warnings.Add($"Backface Force Shadow ({backfaceForceShadow:F2}) is not reproduced yet in Exact Compatibility.");
            }

            int shadowMaskType = Mathf.RoundToInt(GetFloatOr(sourceProps, "_ShadowMaskType", 0.0f));
            if (shadowMaskType != 0)
            {
                float shadowFlatBorder = GetFloatOr(sourceProps, "_ShadowFlatBorder", 1.0f);
                float shadowFlatBlur = GetFloatOr(sourceProps, "_ShadowFlatBlur", 1.0f);
                report.warnings.Add(
                    $"Shadow Mask Type {shadowMaskType} with FlatBorder={shadowFlatBorder:F2}, FlatBlur={shadowFlatBlur:F2} " +
                    "is not reproduced yet in Exact Compatibility.");
            }

            if (GetFloatOr(sourceProps, "_ShadowPostAO", 0.0f) > 0.5f)
            {
                report.warnings.Add("Shadow Post AO is enabled in lilToon, but Natane Exact Compatibility still ignores that branch.");
            }

            // === Render State Migration ===
            // _Cull: lilToon 0=Off(両面), 1=Front, 2=Back(デフォルト)
            if (sourceProps.ContainsKey("_Cull"))
            {
                float cull = (float)sourceProps["_Cull"];
                targetMaterial.SetFloat("_Cull", cull);
                string cullName = cull < 0.5f ? "Off (両面)" : cull < 1.5f ? "Front" : "Back";
                report.infos.Add($"Cull Mode: {cullName}");
            }

            // _ZWrite: lilToon と Natane で同じ意味 (0=Off, 1=On)
            if (sourceProps.ContainsKey("_ZWrite"))
            {
                float zwrite = (float)sourceProps["_ZWrite"];
                targetMaterial.SetFloat("_ZWrite", zwrite);
                report.infos.Add($"ZWrite: {(zwrite > 0.5f ? "On" : "Off")}");
            }

            // _SrcBlend / _DstBlend: Transparent シェーダーの場合のみ
            string targetShaderName = targetMaterial.shader != null ? targetMaterial.shader.name : "";
            if (targetShaderName.Contains("Transparent"))
            {
                if (sourceProps.ContainsKey("_SrcBlend"))
                    targetMaterial.SetFloat("_SrcBlend", (float)sourceProps["_SrcBlend"]);
                if (sourceProps.ContainsKey("_DstBlend"))
                    targetMaterial.SetFloat("_DstBlend", (float)sourceProps["_DstBlend"]);
                report.infos.Add("Blend mode properties migrated for Transparent variant.");
            }

            // renderQueue: 呼び出し側で sourceMaterial.renderQueue を設定すること
        }

        /// <summary>
        /// </summary>
        private float GetFloatOr(Dictionary<string, object> props, string key, float defaultValue)
        {
            if (props.ContainsKey(key))
                return (float)props[key];
            return defaultValue;
        }

        private void SetTextureIfExists(Dictionary<string, object> source, string sourceKey, Material target, string targetKey)
        {
            if (source.ContainsKey(sourceKey) && source[sourceKey] != null)
            {
                target.SetTexture(targetKey, (Texture)source[sourceKey]);
            }
        }

        private void SetColorIfExists(Dictionary<string, object> source, string sourceKey, Material target, string targetKey)
        {
            if (source.ContainsKey(sourceKey))
            {
                target.SetColor(targetKey, (Color)source[sourceKey]);
            }
        }

        private void SetFloatIfExists(Dictionary<string, object> source, string sourceKey, Material target, string targetKey)
        {
            if (source.ContainsKey(sourceKey))
            {
                target.SetFloat(targetKey, (float)source[sourceKey]);
            }
        }

        /// <summary>
        /// Compensate for materials that have no MainTex and rely on effects (gem, metal, etc.)
        /// for their visual appearance. Without this, they appear black after migration.
        /// </summary>
        private void ApplyTexturelessMaterialCompensation(
            Dictionary<string, object> sourceProps,
            Material targetMaterial,
            ConversionReport report)
        {
            // Check if this material has a MainTex assigned
            bool hasMainTex = sourceProps.ContainsKey("_MainTex") && sourceProps["_MainTex"] != null;

            // Detect gem shader from source shader name
            string sourceShader = sourceProps.ContainsKey("__sourceShaderName")
                ? ((string)sourceProps["__sourceShaderName"]).ToLower()
                : "";
            bool isGemShader = sourceShader.Contains("gem");
            bool isFakeShadowShader = sourceShader.Contains("fakeshadow");

            // Detect metallic surface (no texture, relies on reflections)
            float sourceSmoothness = GetFloatOr(sourceProps, "_Smoothness", 0.0f);
            float sourceMetallic = GetFloatOr(sourceProps, "_Metallic", 0.0f);
            bool isMetallicSurface = !hasMainTex && (sourceMetallic > 0.3f || sourceSmoothness > 0.5f);

            // Detect gem-specific properties (lilToonGem captures)
            bool hasGemProperties = sourceProps.ContainsKey("_GemChromaticAberration")
                || sourceProps.ContainsKey("_GemEnvContrast");

            if (isGemShader || hasGemProperties)
            {
                ApplyGemCompensation(sourceProps, targetMaterial, report);
                return;
            }

            if (isFakeShadowShader)
            {
                ApplyFakeShadowMigration(sourceProps, targetMaterial, report);
                return;
            }

            if (isMetallicSurface)
            {
                ApplyMetallicCompensation(sourceProps, targetMaterial, report);
                return;
            }

            // General: if no MainTex and _Color is very dark, brighten it
            if (!hasMainTex)
            {
                ApplyDarkColorCompensation(sourceProps, targetMaterial, report);
            }
        }

        /// <summary>
        /// lilToon FakeShadow → <c>Natane/Toon Shader FakeShadow</c>。
        ///
        /// 以前はここが「色だけ写して終わり」で、移行してきたユーザーは前髪の落ち影を失っていた。
        /// 相当バリアントを実装したので、実際にプロパティを対応付ける。
        ///
        /// ステンシルは両者で命名が同じなので素通しできる。See Through Hair 系のセットアップを
        /// 済ませたアバターが、移行後もそのまま動くようにするために重要。
        /// </summary>
        private void ApplyFakeShadowMigration(
            Dictionary<string, object> sourceProps,
            Material targetMaterial,
            ConversionReport report)
        {
            if (targetMaterial == null) return;

            bool isFakeShadowTarget = targetMaterial.shader != null &&
                                      targetMaterial.shader.name == "Natane/Toon Shader FakeShadow";

            if (!isFakeShadowTarget)
            {
                report.infos.Add(
                    "FakeShadow shader detected, but the FakeShadow variant was not available. " +
                    "Only the color was migrated — the drop shadow will not render correctly.");
                return;
            }

            var migrated = new List<string>();

            // 色とアルファ。lilToon 側は _Color のアルファに濃さを持たせている。
            if (sourceProps.TryGetValue("_Color", out object colorObj) && colorObj is Color sourceColor)
            {
                targetMaterial.SetColor("_ShadowColor", new Color(sourceColor.r, sourceColor.g, sourceColor.b, 1f));
                targetMaterial.SetFloat("_ShadowAlpha", Mathf.Clamp01(sourceColor.a));
                migrated.Add("_Color → _ShadowColor / _ShadowAlpha");
            }

            // 影の形テクスチャ。
            if (sourceProps.TryGetValue("_MainTex", out object texObj) && texObj is Texture mainTex)
            {
                targetMaterial.SetTexture("_ShadowTex", mainTex);
                migrated.Add("_MainTex → _ShadowTex");
            }

            // Stencil は同名なのでそのまま引き継ぐ。
            string[] stencilProps =
            {
                "_StencilRef", "_StencilComp", "_StencilOp",
                "_StencilFail", "_StencilZFail", "_StencilReadMask", "_StencilWriteMask"
            };

            var stencilMigrated = new List<string>();
            foreach (string prop in stencilProps)
            {
                if (!sourceProps.TryGetValue(prop, out object value)) continue;
                if (!targetMaterial.HasProperty(prop)) continue;
                if (!(value is float f)) continue;

                targetMaterial.SetFloat(prop, f);
                stencilMigrated.Add(prop);
            }

            if (stencilMigrated.Count > 0)
            {
                migrated.Add($"Stencil ({stencilMigrated.Count} properties, names match so values pass through)");
            }

            // カリング。lilToon の _Cull と同じ enum。
            if (sourceProps.TryGetValue("_Cull", out object cullObj) && cullObj is float cull &&
                targetMaterial.HasProperty("_Cull"))
            {
                targetMaterial.SetFloat("_Cull", cull);
                migrated.Add("_Cull");
            }

            report.infos.Add(migrated.Count > 0
                ? "FakeShadow migrated to Natane/Toon Shader FakeShadow: " + string.Join(", ", migrated)
                : "FakeShadow migrated to Natane/Toon Shader FakeShadow (no source properties to carry over).");
        }

        private void ApplyGemCompensation(
            Dictionary<string, object> sourceProps,
            Material targetMaterial,
            ConversionReport report)
        {
            report.infos.Add("Gem shader detected: applying gem-to-Natane compensation.");

            // Brighten gem color — gems are dark because refraction adds brightness
            if (sourceProps.ContainsKey("_Color"))
            {
                Color gemColor = (Color)sourceProps["_Color"];
                float luminance = gemColor.r * 0.299f + gemColor.g * 0.587f + gemColor.b * 0.114f;
                if (luminance < 0.4f && luminance > 0.001f)
                {
                    // Boost color to compensate for missing refraction brightness
                    float boost = Mathf.Lerp(2.5f, 1.0f, luminance / 0.4f);
                    Color boosted = new Color(
                        Mathf.Clamp01(gemColor.r * boost),
                        Mathf.Clamp01(gemColor.g * boost),
                        Mathf.Clamp01(gemColor.b * boost),
                        gemColor.a);
                    targetMaterial.SetColor("_Color", boosted);
                    report.infos.Add($"Gem color brightened: ({gemColor.r:F2},{gemColor.g:F2},{gemColor.b:F2}) → ({boosted.r:F2},{boosted.g:F2},{boosted.b:F2}) (boost={boost:F1}x)");
                }
            }

            // Enable refraction (gem's primary visual)
            if (targetMaterial.HasProperty("_Refraction"))
            {
                targetMaterial.SetFloat("_Refraction", 1.0f);
                targetMaterial.EnableKeyword("_REFRACTION");

                float ior = GetFloatOr(sourceProps, "_RefractionStrength", 0.0f);
                // lilToon _RefractionStrength → Natane IOR: remap to 1.3-2.0 range
                float nataneIOR = ior > 0.01f ? Mathf.Lerp(1.3f, 2.0f, Mathf.Clamp01(ior)) : 1.5f;
                targetMaterial.SetFloat("_RefractionIndex", nataneIOR);
                targetMaterial.SetFloat("_RefractionIntensity", 0.8f);
                targetMaterial.SetFloat("_RefractionBlur", 0.1f);
                report.infos.Add($"Refraction enabled: IOR={nataneIOR:F2}, Intensity=0.8");
            }

            // Enable specular for gem sparkle
            if (targetMaterial.HasProperty("_Specular"))
            {
                targetMaterial.SetFloat("_Specular", 1.0f);
                targetMaterial.EnableKeyword("_SPECULAR");
                targetMaterial.SetColor("_SpecularColor", new Color(1, 1, 1, 1));
                targetMaterial.SetFloat("_SpecularSize", 0.15f);
                targetMaterial.SetFloat("_SpecularSoftness", 0.3f);
                report.infos.Add("Specular enabled for gem sparkle: Size=0.15, Softness=0.3");
            }

            // Enable reflection for environment mapping
            if (targetMaterial.HasProperty("_Reflection"))
            {
                float smoothness = GetFloatOr(sourceProps, "_Smoothness", 0.85f);
                targetMaterial.SetFloat("_Reflection", 1.0f);
                targetMaterial.EnableKeyword("_REFLECTION");
                targetMaterial.SetFloat("_ReflectionIntensity", 0.6f);
                targetMaterial.SetFloat("_Smoothness", smoothness);
                targetMaterial.SetFloat("_Metallic", 0.0f);
                report.infos.Add($"Reflection enabled for gem: Intensity=0.6, Smoothness={smoothness:F2}");
            }

            // Cull Off for gems (typically double-sided)
            targetMaterial.SetFloat("_Cull", 0.0f);
            report.infos.Add("Cull set to Off (double-sided) for gem material.");
        }

        private void ApplyMetallicCompensation(
            Dictionary<string, object> sourceProps,
            Material targetMaterial,
            ConversionReport report)
        {
            float sourceMetallic = GetFloatOr(sourceProps, "_Metallic", 0.0f);
            float sourceSmoothness = GetFloatOr(sourceProps, "_Smoothness", 0.5f);

            report.infos.Add($"Metallic surface detected (no MainTex, Metallic={sourceMetallic:F2}, Smoothness={sourceSmoothness:F2}): applying compensation.");

            // Brighten dark metallic colors
            if (sourceProps.ContainsKey("_Color"))
            {
                Color metalColor = (Color)sourceProps["_Color"];
                float luminance = metalColor.r * 0.299f + metalColor.g * 0.587f + metalColor.b * 0.114f;
                if (luminance < 0.3f && luminance > 0.001f)
                {
                    float boost = Mathf.Lerp(2.0f, 1.0f, luminance / 0.3f);
                    Color boosted = new Color(
                        Mathf.Clamp01(metalColor.r * boost),
                        Mathf.Clamp01(metalColor.g * boost),
                        Mathf.Clamp01(metalColor.b * boost),
                        metalColor.a);
                    targetMaterial.SetColor("_Color", boosted);
                    report.infos.Add($"Metal color brightened: ({metalColor.r:F2},{metalColor.g:F2},{metalColor.b:F2}) → ({boosted.r:F2},{boosted.g:F2},{boosted.b:F2})");
                }
            }

            // Ensure specular is enabled for metallic materials
            if (targetMaterial.HasProperty("_Specular") && !targetMaterial.IsKeywordEnabled("_SPECULAR"))
            {
                float specularSize = OptimizeSpecularSize(sourceSmoothness);
                targetMaterial.SetFloat("_Specular", 1.0f);
                targetMaterial.EnableKeyword("_SPECULAR");
                targetMaterial.SetColor("_SpecularColor", new Color(1, 1, 1, 1));
                targetMaterial.SetFloat("_SpecularSize", specularSize);
                targetMaterial.SetFloat("_SpecularSoftness", Mathf.Max(0.1f, 1.0f - sourceSmoothness));
                report.infos.Add($"Specular enabled for metal: Size={specularSize:F3}, Softness={1.0f - sourceSmoothness:F2}");
            }

            // Enable reflection for environment mapping
            if (targetMaterial.HasProperty("_Reflection"))
            {
                targetMaterial.SetFloat("_Reflection", 1.0f);
                targetMaterial.EnableKeyword("_REFLECTION");
                targetMaterial.SetFloat("_ReflectionIntensity", Mathf.Clamp(sourceMetallic, 0.3f, 1.0f));
                targetMaterial.SetFloat("_Smoothness", sourceSmoothness);
                targetMaterial.SetFloat("_Metallic", sourceMetallic);
                report.infos.Add($"Reflection enabled for metal: Intensity={sourceMetallic:F2}, Smoothness={sourceSmoothness:F2}");
            }

            // MatCap as fallback if no cubemap is available
            if (targetMaterial.HasProperty("_MatCap") && targetMaterial.GetTexture("_MatCapTex") != null
                && !targetMaterial.IsKeywordEnabled("_MATCAP"))
            {
                targetMaterial.SetFloat("_MatCap", 1.0f);
                targetMaterial.EnableKeyword("_MATCAP");
                report.infos.Add("MatCap enabled (existing texture found) for metallic fallback.");
            }
        }

        private void ApplyDarkColorCompensation(
            Dictionary<string, object> sourceProps,
            Material targetMaterial,
            ConversionReport report)
        {
            if (!sourceProps.ContainsKey("_Color")) return;

            Color color = (Color)sourceProps["_Color"];
            float luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;

            // Only compensate if very dark and no visual features are enabled
            if (luminance >= 0.15f) return;
            if (luminance < 0.001f) return; // Intentionally black, don't touch

            bool hasVisualFeature =
                targetMaterial.IsKeywordEnabled("_SPECULAR") ||
                targetMaterial.IsKeywordEnabled("_MATCAP") ||
                targetMaterial.IsKeywordEnabled("_REFLECTION") ||
                targetMaterial.IsKeywordEnabled("_REFRACTION") ||
                targetMaterial.IsKeywordEnabled("_EMISSION") ||
                targetMaterial.IsKeywordEnabled("_RIM_LIGHT");

            if (hasVisualFeature) return;

            float boost = Mathf.Lerp(3.0f, 1.0f, luminance / 0.15f);
            Color boosted = new Color(
                Mathf.Clamp01(color.r * boost),
                Mathf.Clamp01(color.g * boost),
                Mathf.Clamp01(color.b * boost),
                color.a);
            targetMaterial.SetColor("_Color", boosted);
            report.warnings.Add(
                $"No MainTex and very dark _Color detected ({luminance:F3}). Color brightened to prevent black appearance: " +
                $"({color.r:F2},{color.g:F2},{color.b:F2}) → ({boosted.r:F2},{boosted.g:F2},{boosted.b:F2}). Manual review recommended.");
        }

        /// <summary>
        /// </summary>
        private float OptimizeSpecularSize(float smoothness)
        {

            float normalized = Mathf.Clamp01(smoothness);
            float curved = Mathf.Pow(normalized, 1.5f);
            float specularSize = Mathf.Lerp(0.01f, 0.5f, curved);

            return specularSize;
        }

        /// <summary>
        /// lilToon: 0=Normal(lerp), 1=Add, 2=Screen, 3=Multiply
        /// Natane:  0=Add, 1=Multiply, 2=Replace
        ///
        /// </summary>
        private int ConvertMatCapBlendMode(int lilBlendMode)
        {
            switch (lilBlendMode)
            {
                case 0: return 2;
                case 1: return 0;
                case 2: return 0;
                case 3: return 1;
                default: return 2;
            }
        }

        /// <summary>
        /// lilToon: 0=Normal(lerp), 1=Add, 2=Screen, 3=Multiply
        /// Natane:  0=Normal, 1=Soft, 2=Screen, 3=Overlay
        /// Emission, Rim Light, Env Rim 共通の変換ロジック。
        /// </summary>
        private int ConvertRimBlendMode(int lilBlendMode)
        {
            return ConvertEffectBlendMode(lilBlendMode);
        }

        /// <summary>
        /// lilToon → Natane 汎用エフェクト合成モード変換。
        /// lilToon: 0=Normal, 1=Add, 2=Screen, 3=Multiply
        /// Natane:  0=Normal, 1=Soft, 2=Screen, 3=Overlay
        /// </summary>
        private int ConvertEffectBlendMode(int lilBlendMode)
        {
            switch (lilBlendMode)
            {
                case 0: return 0; // Normal → Normal
                case 1: return 1; // Add → Soft (近似)
                case 2: return 2; // Screen → Screen
                case 3: return 3; // Multiply → Overlay (近似)
                default: return 0;
            }
        }

        /// <summary>
        /// ソースマテリアルのシェーダー名・renderQueue・プロパティから
        /// 適切な Natane シェーダーバリアント (Opaque/Cutout/Transparent) を判定する。
        /// </summary>
        private Shader DetectNataneShaderVariant(Material sourceMaterial)
        {
            string shaderName = sourceMaterial.shader.name.ToLower();

            // --- Step 0: FakeShadow ---
            // 前髪の落ち影専用シェーダー。本体バリアントへ変換すると
            // 「146 機能を持つ不透明マテリアルになった板ポリ」になってしまい、
            // 落ち影として成立しない。専用バリアントへ振り分ける。
            if (shaderName.Contains("fakeshadow"))
            {
                Shader fakeShadow = Shader.Find("Natane/Toon Shader FakeShadow");
                if (fakeShadow != null) return fakeShadow;
                // 未導入なら従来どおり半透明バリアントへ落とす（絵は崩れるが黒板にはならない）。
                Shader fallback = Shader.Find("Natane/Toon Shader (Transparent)");
                if (fallback != null) return fallback;
            }

            // --- Step 1: シェーダー名から判定 ---
            // lilToon の命名パターン: "cutout", "transparent", "fade", "gem" 等
            // Gem は透過シェーダーとして扱う（リフラクション使用のため）
            if (shaderName.Contains("gem"))
            {
                Shader transparentShader = Shader.Find("Natane/Toon Shader (Transparent)");
                if (transparentShader != null) return transparentShader;
            }
            else if (shaderName.Contains("transparent") || shaderName.Contains("fade"))
            {
                Shader transparentShader = Shader.Find("Natane/Toon Shader (Transparent)");
                if (transparentShader != null) return transparentShader;
            }
            else if (shaderName.Contains("cutout"))
            {
                Shader cutoutShader = Shader.Find("Natane/Toon Shader (Cutout)");
                if (cutoutShader != null) return cutoutShader;
            }

            // --- Step 2: renderQueue から判定（シェーダー名で判別できなかった場合） ---
            // AlphaTest queue = 2450, Transparent queue >= 2501
            int queue = sourceMaterial.renderQueue;
            if (queue >= 2501)
            {
                Shader transparentShader = Shader.Find("Natane/Toon Shader (Transparent)");
                if (transparentShader != null) return transparentShader;
            }
            else if (queue >= 2450 && queue <= 2500)
            {
                Shader cutoutShader = Shader.Find("Natane/Toon Shader (Cutout)");
                if (cutoutShader != null) return cutoutShader;
            }

            // --- Step 3: マテリアルプロパティから判定（最終フォールバック） ---
            // _Cutoff > 0 かつブレンドモードが Opaque 系 → Cutout
            if (sourceMaterial.HasProperty("_Cutoff"))
            {
                float cutoff = sourceMaterial.GetFloat("_Cutoff");
                if (cutoff > 0.001f)
                {
                    bool isTransparentBlend = sourceMaterial.HasProperty("_SrcBlend") &&
                                              sourceMaterial.GetFloat("_SrcBlend") > 1.5f;
                    if (isTransparentBlend)
                    {
                        Shader transparentShader = Shader.Find("Natane/Toon Shader (Transparent)");
                        if (transparentShader != null) return transparentShader;
                    }
                    else
                    {
                        Shader cutoutShader = Shader.Find("Natane/Toon Shader (Cutout)");
                        if (cutoutShader != null) return cutoutShader;
                    }
                }
            }

            return Shader.Find("Natane/Toon Shader");
        }

        /// <summary>
        /// lilToon: _Shadow2ndColor, _Shadow2ndBorder (0-1), _Shadow3rdColor, _Shadow3rdBorder (0-1)
        /// Natane:  _Shadow2ndColor, _Shadow2ndBorder, _Shadow3rdColor, _Shadow3rdBorder + _USE_MULTI_SHADOW keyword
        /// </summary>
        private void MapMultiShadowLayers(Dictionary<string, object> sourceProps, Material targetMaterial, ConversionReport report)
        {
            bool has2nd = sourceProps.ContainsKey("_Shadow2ndColor");
            bool has3rd = sourceProps.ContainsKey("_Shadow3rdColor");

            if (!has2nd && !has3rd) return;

            // Check if 2nd shadow is actually meaningful (not just default values)
            // lilToon always has _Shadow2ndColor as a property, but it may be unused
            bool meaningful2nd = false;
            if (has2nd)
            {
                Color shadow2nd = (Color)sourceProps["_Shadow2ndColor"];
                // Consider meaningful if alpha > 0 and color isn't pure white (default/unused)
                meaningful2nd = shadow2nd.a > 0.01f &&
                    (shadow2nd.r < 0.99f || shadow2nd.g < 0.99f || shadow2nd.b < 0.99f);
            }

            bool meaningful3rd = false;
            if (has3rd)
            {
                Color shadow3rd = (Color)sourceProps["_Shadow3rdColor"];
                meaningful3rd = shadow3rd.a > 0.01f &&
                    (shadow3rd.r < 0.99f || shadow3rd.g < 0.99f || shadow3rd.b < 0.99f);
            }

            if (!meaningful2nd && !meaningful3rd)
            {
                report.infos.Add("Multi-shadow skipped: 2nd/3rd shadow colors are effectively unused.");
                return;
            }

            // Enable multi-shadow keyword
            targetMaterial.SetFloat("_UseMultiShadow", 1.0f);
            targetMaterial.EnableKeyword("_USE_MULTI_SHADOW");

            if (meaningful2nd)
            {
                Color shadow2nd = (Color)sourceProps["_Shadow2ndColor"];
                targetMaterial.SetColor("_Shadow2ndColor", shadow2nd);
                float border2nd = GetFloatOr(sourceProps, "_Shadow2ndBorder", 0.15f);
                targetMaterial.SetFloat("_Shadow2ndBorder", border2nd);
                report.infos.Add($"2nd Shadow: Color={shadow2nd}, Border={border2nd:F2}");
            }

            if (meaningful3rd)
            {
                Color shadow3rd = (Color)sourceProps["_Shadow3rdColor"];
                targetMaterial.SetColor("_Shadow3rdColor", shadow3rd);
                float border3rd = GetFloatOr(sourceProps, "_Shadow3rdBorder", 0.25f);
                targetMaterial.SetFloat("_Shadow3rdBorder", border3rd);
                report.infos.Add($"3rd Shadow: Color={shadow3rd}, Border={border3rd:F2}");
            }
        }

        /// <summary>
        /// </summary>
        private void DisableNonBasicFeatures(Material material, ConversionReport report)
        {
            DisableFeature(material, "_SoftLightingMode", "_SOFT_LIGHTING_MODE");
            DisableFeature(material, "_UseLightVolume", "_USE_LIGHT_VOLUME");
            DisableFeature(material, "_LightVolumeSpecular", "_LIGHT_VOLUME_SPECULAR");
            DisableFeature(material, "_LTCGI", "_LTCGI");
            DisableFeature(material, "_UseAO", "_USE_AO");
            DisableFeature(material, "_UseDithering", "_USE_DITHERING");

            DisableFeature(material, "_Specular", "_SPECULAR");
            DisableFeature(material, "_RimLight", "_RIM_LIGHT");
            DisableFeature(material, "_RimLight2", "_RIM_LIGHT_2");
            DisableFeature(material, "_SSS", "_SSS");
            DisableFeature(material, "_MatCap", "_MATCAP");
            DisableFeature(material, "_MatCap2", "_MATCAP_2");
            DisableFeature(material, "_MatCap3", "_MATCAP_3");
            DisableFeature(material, "_Glitter", "_GLITTER");
            DisableFeature(material, "_WaterDrip", "_WATER_DRIP");
            DisableFeature(material, "_Hologram", "_HOLOGRAM");
            DisableFeature(material, "_UseHologramNoise", "_HOLOGRAM_NOISE");
            DisableFeature(material, "_Glitch", "_GLITCH");
            DisableFeature(material, "_Outline", "_OUTLINE");
            DisableFeature(material, "_Emission", "_EMISSION");
            DisableFeature(material, "_Dissolve", "_DISSOLVE");
            DisableFeature(material, "_HueShiftEnable", "_HUE_SHIFT");
            DisableFeature(material, "_UseAlphaMask", "_ALPHA_MASK");
            DisableFeature(material, "_AudioLink", "_AUDIOLINK");

            DisableFeature(material, "_Reflection", "_REFLECTION");
            DisableFeature(material, "_Iridescence", "_IRIDESCENCE");
            DisableFeature(material, "_EnvRim", "_ENV_RIM");
            DisableFeature(material, "_Refraction", "_REFRACTION");

            DisableFeature(material, "_UseNormalMap", "_NORMALMAP");
            DisableFeature(material, "_Parallax", "_PARALLAX");
            DisableFeature(material, "_VAT", "_VAT");
            DisableFeature(material, "_VATNormal", "_VAT_NORMAL");
            DisableFeature(material, "_BackfaceTexture", "_BACKFACE_TEXTURE");
            DisableFeature(material, "_VideoTexture", "_VIDEO_TEXTURE");
            DisableFeature(material, "_DistanceFade", "_DISTANCE_FADE");

            report.infos.Add("Disabled non-basic features. Their stored values were preserved for manual re-enable.");
        }

        /// <summary>
        /// </summary>
        private void DisableFeature(Material material, string propertyName, string keyword)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, 0f);
            }
            material.DisableKeyword(keyword);
        }

        // ===================================
        // ===================================

        /// <summary>
        /// </summary>
        private void ScanPrefabMaterials()
        {
            prefabMaterials.Clear();

            if (targetPrefab == null) return;

            Renderer[] renderers = targetPrefab.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in renderers)
            {
                Material[] sharedMats = renderer.sharedMaterials;
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    Material mat = sharedMats[i];
                    if (mat == null || mat.shader == null) continue;

                    string shaderName = mat.shader.name;
                    if (shaderName.Contains("lilToon") || shaderName.StartsWith("_lil/"))
                    {
                        prefabMaterials.Add(new PrefabMaterialInfo
                        {
                            original = mat,
                            converted = null,
                            renderer = renderer,
                            materialIndex = i,
                            willConvert = true,
                            rendererPath = GetHierarchyPath(renderer.transform, targetPrefab.transform)
                        });
                    }
                }
            }

            Debug.Log($"Found {prefabMaterials.Select(m => m.original).Distinct().Count()} lilToon materials in prefab '{targetPrefab.name}'.");
        }

        /// <summary>
        /// </summary>
        private string GetHierarchyPath(Transform target, Transform root)
        {
            var parts = new List<string>();
            Transform current = target;

            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }

            return parts.Count > 0 ? string.Join("/", parts) : target.name;
        }

        /// <summary>
        /// </summary>
        private void ConvertPrefabMaterials()
        {
            var materialsToConvert = prefabMaterials
                .Where(m => m.willConvert && m.converted == null)
                .Select(m => m.original)
                .Distinct()
                .ToList();

            if (materialsToConvert.Count == 0) return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"lilToon Migration - {targetPrefab.name}");

            int successCount = 0;
            List<ConversionReport> reports = new List<ConversionReport>();
            Dictionary<Material, Material> materialMapping = new Dictionary<Material, Material>();

            try
            {
                for (int i = 0; i < materialsToConvert.Count; i++)
                {
                    Material sourceMat = materialsToConvert[i];

                EditorUtility.DisplayProgressBar(
                    L("Converting Prefab Materials", "Converting Prefab Materials"),
                    L($"Converting {i + 1}/{materialsToConvert.Count}: {sourceMat.name}", $"Converting {i + 1}/{materialsToConvert.Count}: {sourceMat.name}"),
                    (float)i / materialsToConvert.Count
                );

                var report = ConvertMaterialWithReport(sourceMat);
                reports.Add(report);

                if (report.success)
                {
                    successCount++;

                    if (!replaceOriginal || duplicateInHierarchy)
                    {
                        string sourcePath = AssetDatabase.GetAssetPath(sourceMat);
                        string newPath = GetConvertedMaterialPath(sourcePath);
                        Material newMat = AssetDatabase.LoadAssetAtPath<Material>(newPath);
                        if (newMat != null)
                        {
                            materialMapping[sourceMat] = newMat;
                        }
                    }
                }
            }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (duplicateInHierarchy && materialMapping.Count > 0)
            {
                DuplicateAndApplyMaterials(materialMapping);
            }
            else if (updatePrefabReferences && !replaceOriginal && materialMapping.Count > 0)
            {
                UpdatePrefabReferences(materialMapping);
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);

            ShowConversionReport(reports, successCount);

            ScanPrefabMaterials();
        }

        /// <summary>
        /// </summary>
        private void ConvertSinglePrefabMaterial(Material sourceMaterial)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"lilToon Migration - {sourceMaterial.name}");

            var report = ConvertMaterialWithReport(sourceMaterial);

            if (report.success)
            {
                string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
                string newPath = GetConvertedMaterialPath(sourcePath);
                Material newMat = AssetDatabase.LoadAssetAtPath<Material>(newPath);

                if (newMat != null)
                {
                    var mapping = new Dictionary<Material, Material> { { sourceMaterial, newMat } };

                    if (duplicateInHierarchy)
                    {
                        DuplicateAndApplyMaterials(mapping);
                    }
                    else if (updatePrefabReferences && !replaceOriginal)
                    {
                        UpdatePrefabReferences(mapping);
                    }
                }

                AssetDatabase.SaveAssets();
                ShowConversionReport(new List<ConversionReport> { report }, 1);
            }

            Undo.CollapseUndoOperations(undoGroup);

            ScanPrefabMaterials();
        }

        /// <summary>
        /// </summary>
        private void UpdatePrefabReferences(Dictionary<Material, Material> materialMapping)
        {
            if (targetPrefab == null || materialMapping.Count == 0) return;

            string prefabPath = AssetDatabase.GetAssetPath(targetPrefab);
            bool isSceneInstance = string.IsNullOrEmpty(prefabPath);

            if (isSceneInstance)
            {
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(targetPrefab);
            }

            if (!string.IsNullOrEmpty(prefabPath))
            {
                GameObject prefabContents = null;
                try
                {
                    prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

                    Renderer[] renderers = prefabContents.GetComponentsInChildren<Renderer>(true);
                    bool changed = false;

                    foreach (var renderer in renderers)
                    {
                        Material[] materials = renderer.sharedMaterials;
                        bool rendererChanged = false;

                        for (int i = 0; i < materials.Length; i++)
                        {
                            if (materials[i] != null && materialMapping.ContainsKey(materials[i]))
                            {
                                materials[i] = materialMapping[materials[i]];
                                rendererChanged = true;
                            }
                        }

                        if (rendererChanged)
                        {
                            renderer.sharedMaterials = materials;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                        Debug.Log($"Updated material references in prefab '{System.IO.Path.GetFileName(prefabPath)}'.");
                    }
                }
                finally
                {
                    if (prefabContents != null)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabContents);
                    }
                }
            }

            if (isSceneInstance)
            {
                Renderer[] sceneRenderers = targetPrefab.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in sceneRenderers)
                {
                    Undo.RecordObject(renderer, "Update Material References");
                    Material[] materials = renderer.sharedMaterials;
                    bool rendererChanged = false;

                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null && materialMapping.ContainsKey(materials[i]))
                        {
                            materials[i] = materialMapping[materials[i]];
                            rendererChanged = true;
                        }
                    }

                    if (rendererChanged)
                    {
                        renderer.sharedMaterials = materials;
                    }
                }

                Debug.Log($"Updated material references in scene instance '{targetPrefab.name}'.");
            }
        }

        /// <summary>
        /// </summary>
        private void DuplicateAndApplyMaterials(Dictionary<Material, Material> materialMapping)
        {
            if (targetPrefab == null || materialMapping.Count == 0) return;

            GameObject duplicate;
            string prefabAssetPath = AssetDatabase.GetAssetPath(targetPrefab);
            bool isProjectAsset = !string.IsNullOrEmpty(prefabAssetPath) && !targetPrefab.scene.IsValid();

            if (isProjectAsset)
            {
                duplicate = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab);
            }
            else
            {
                duplicate = Object.Instantiate(targetPrefab);
                duplicate.transform.SetParent(targetPrefab.transform.parent, false);
            }

            duplicate.name = targetPrefab.name + "_NataneToon";
            Undo.RegisterCreatedObjectUndo(duplicate, $"Duplicate {targetPrefab.name} for NataneToon Migration");

            if (isProjectAsset)
            {
            }
            else
            {
                Vector3 offset = duplicate.transform.right * GetBoundsWidth(duplicate);
                if (offset.magnitude < 0.1f) offset = Vector3.right * 1.0f;
                duplicate.transform.position = targetPrefab.transform.position + offset;
            }

            Renderer[] renderers = duplicate.GetComponentsInChildren<Renderer>(true);
            int replacedCount = 0;

            foreach (var renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool rendererChanged = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materialMapping.ContainsKey(materials[i]))
                    {
                        materials[i] = materialMapping[materials[i]];
                        rendererChanged = true;
                        replacedCount++;
                    }
                }

                if (rendererChanged)
                {
                    renderer.sharedMaterials = materials;
                }
            }

            if (PrefabUtility.IsPartOfPrefabInstance(duplicate))
            {
                PrefabUtility.UnpackPrefabInstance(duplicate, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            lastDuplicatedObject = duplicate;
            Selection.activeGameObject = duplicate;

            Debug.Log(L($"Created duplicate '{duplicate.name}' of '{targetPrefab.name}' in hierarchy ({replacedCount} material slots replaced).", $"Created duplicate '{duplicate.name}' of '{targetPrefab.name}' in hierarchy ({replacedCount} material slots replaced)."));
        }

        /// <summary>
        /// </summary>
        private float GetBoundsWidth(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 1.0f;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return Mathf.Max(bounds.size.x, 0.5f);
        }

        /// <summary>
        /// </summary>
        private void ShowConversionReport(List<ConversionReport> reports, int successCount)
        {
            StringBuilder reportText = new StringBuilder();
            reportText.AppendLine(L($"Conversion Complete: Successfully converted {successCount}/{reports.Count} materials.", $"Conversion Complete: Successfully converted {successCount}/{reports.Count} materials."));
            reportText.AppendLine();

            int warningCount = 0;

            foreach (var report in reports)
            {
                if (!report.success)
                {
                    reportText.AppendLine($"x {report.materialName}: {L("Failed", "Failed")}");
                    foreach (var warning in report.warnings)
                    {
                        reportText.AppendLine($"   ! {warning}");
                    }
                    reportText.AppendLine();
                    continue;
                }

                bool hasWarnings = report.warnings.Count > 0;
                string statusIcon = hasWarnings ? "!" : "*";
                reportText.AppendLine($"{statusIcon} {report.materialName}");

                if (hasWarnings)
                {
                    foreach (var warning in report.warnings)
                    {
                        reportText.AppendLine($"   ! {warning}");
                        warningCount++;
                    }
                }

                if (report.outlineWidthAdjusted)
                {
                    reportText.AppendLine(L($"   > Outline Width Adjusted: {report.originalOutlineWidth:F2} -> {report.convertedOutlineWidth:F4}", $"   > Outline Width Adjusted: {report.originalOutlineWidth:F2} -> {report.convertedOutlineWidth:F4}"));
                }
                if (report.hasMultipleShadowLayers)
                {
                    reportText.AppendLine(L($"   i Multiple shadow layers detected (converted as multi-shadow)", $"   i Multiple shadow layers detected (converted as multi-shadow)"));
                }

                reportText.AppendLine();
            }

            reportText.AppendLine(L("=== Summary ===", "=== Summary ==="));
            reportText.AppendLine(L($"Success: {successCount}", $"Success: {successCount}"));
            reportText.AppendLine(L($"Failed: {reports.Count - successCount}", $"Failed: {reports.Count - successCount}"));
            reportText.AppendLine(L($"Warnings: {warningCount}", $"Warnings: {warningCount}"));

            Debug.Log("=== Detailed Conversion Report ===");
            foreach (var report in reports)
            {
                if (report.success && report.infos.Count > 0)
                {
                    Debug.Log($"[{report.materialName}]");
                    foreach (var info in report.infos)
                    {
                        Debug.Log($"  {info}");
                    }
                }
            }

            EditorUtility.DisplayDialog(
                L("Conversion Report", "Conversion Report"),
                reportText.ToString(),
                "OK"
            );

            if (warningCount > 0)
            {
                bool openConsole = EditorUtility.DisplayDialog(
                    L("Warnings Detected", "Warnings Detected"),
                    L($"{warningCount} warnings detected.\nCheck the console log for details.\n\nOpen Console?", $"{warningCount} warnings detected.\nCheck the console log for details.\n\nOpen Console?"),
                    L("Open Console", "Open Console"),
                    L("Close", "Close")
                );

                if (openConsole)
                {
                    EditorWindow.GetWindow(System.Type.GetType("UnityEditor.ConsoleWindow,UnityEditor"));
                }
            }
        }

        [MenuItem("Tools/Natane/移行 Migration/互換モード一括アップグレード Upgrade Compat Materials")]
        public static void UpgradeExactCompatMaterials()
        {
            var guids = AssetDatabase.FindAssets("t:Material");
            var upgraded = new List<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                if (!mat.HasProperty("_LilToonExactCompatibility")) continue;
                if (mat.GetFloat("_LilToonExactCompatibility") <= 0.5f) continue;

                Undo.RecordObject(mat, "Upgrade Exact Compat");

                mat.SetFloat("_LilToonExactCompatibility", 0.0f);

                // Convert ST params to Natane native
                float border = mat.HasProperty("_STShadowBorder") ? mat.GetFloat("_STShadowBorder") : 0.5f;
                float blur = mat.HasProperty("_STShadowBlur") ? mat.GetFloat("_STShadowBlur") : 0.1f;

                mat.SetFloat("_ShadowSharpness", Mathf.Max(blur * 1.2f, 0.05f));
                mat.SetFloat("_ShadowOffset", Mathf.Clamp((0.5f - border) * (1.0f + blur * 0.3f), -1.0f, 1.0f));

                // Set appropriate shading mode
                float currentMode = mat.HasProperty("_ShadingMode") ? mat.GetFloat("_ShadingMode") : 0.0f;
                if (currentMode >= 1.5f && currentMode < 2.5f)
                {
                    // Was StandardToon mode (2.0), convert to Toon (0.0)
                    mat.SetFloat("_ShadingMode", 0.0f);
                }

                // Enable Natane lighting
                if (mat.HasProperty("_UsePixelVertexLights"))
                {
                    mat.SetFloat("_UsePixelVertexLights", 1.0f);
                    mat.EnableKeyword("_PIXEL_VERTEX_LIGHTS");
                }

                // Run AutoFixer
                try
                {
                    NataneLilToonAutoFixer.Fix(mat, new Dictionary<string, object>());
                }
                catch (System.Exception) { }

                EditorUtility.SetDirty(mat);
                upgraded.Add(mat.name);
            }

            AssetDatabase.SaveAssets();

            if (upgraded.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Upgrade Complete",
                    $"Upgraded {upgraded.Count} materials from Exact Compatibility mode:\n" + string.Join("\n", upgraded.Take(20)),
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog("No Materials Found", "No materials with Exact Compatibility mode were found.", "OK");
            }
        }
    }
}
