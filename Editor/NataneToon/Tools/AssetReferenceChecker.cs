using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Asset Reference Checker - Detects missing/broken references in scenes, prefabs, and materials
    /// </summary>
    public class AssetReferenceChecker : EditorWindow
    {
        private enum ScanMode { Scene, Prefab, Material }
        private enum ReferenceIssueType { MissingScript, MissingMaterial, MissingTexture, MissingShader, NullReference }
        private enum IssueSeverity { Error, Warning, Info }

        private class ReferenceIssue
        {
            public ReferenceIssueType type;
            public IssueSeverity severity;
            public string objectPath;
            public string componentName;
            public string propertyName;
            public string description;
            public Object targetObject;
            public string assetPath;
            public bool foldout;
        }

        private ScanMode scanMode = ScanMode.Scene;
        private Vector2 scrollPosition;
        private List<ReferenceIssue> issues = new List<ReferenceIssue>();
        private int currentIssuePage;
        private int droppedIssueCount;

        // Scene mode settings
        private bool includeInactive = true;

        // Prefab mode settings
        private GameObject targetPrefab;

        // Material mode settings
        private bool nataneToonOnly = false;

        // Filter toggles
        private bool showErrors = true;
        private bool showWarnings = true;
        private bool showInfos = true;

        private bool hasScanned = false;

        private static string[] ScanModeLabels => new[]
        {
            L("シーン", "Scene"),
            L("プレハブ", "Prefab"),
            L("マテリアル", "Material")
        };

        [MenuItem("Tools/Natane/Optimization/アセット参照チェッカー Asset Reference Checker", false, 35)]
        public static void ShowWindow()
        {
            var window = GetWindow<AssetReferenceChecker>(L("アセット参照チェッカー", "Asset Reference Checker"));
            window.minSize = new Vector2(550, 450);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(5);
            DrawScanModeSelector();
            EditorGUILayout.Space(5);
            DrawModeSettings();
            EditorGUILayout.Space(5);

            if (hasScanned)
            {
                DrawResultSummary();
                EditorGUILayout.Space(5);
                DrawFilterToggles();
                EditorGUILayout.Space(5);
                DrawResults();
            }
        }

        private void DrawHeader()
        {
            NataneToonShaderGUIUtility.DrawToolHeader(
                "アセット参照チェッカー",
                "Asset Reference Checker",
                "AssetReferenceChecker");

            EditorGUILayout.HelpBox(
                L("シーン、プレハブ、マテリアル内の欠損・破損参照を検出します。\n" +
                  "Missing Script、マテリアル欠損、テクスチャ欠損、シェーダーエラーなどを走査します。",
                  "Detects missing/broken references in scenes, prefabs, and materials.\n" +
                  "Scans for Missing Scripts, missing materials, textures, shader errors, and more."),
                MessageType.Info);
        }

        private void DrawScanModeSelector()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("スキャンモード", "Scan Mode"), EditorStyles.boldLabel);
            scanMode = (ScanMode)GUILayout.Toolbar((int)scanMode, ScanModeLabels);
            EditorGUILayout.EndVertical();
        }

        private void DrawModeSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            switch (scanMode)
            {
                case ScanMode.Scene:
                    DrawSceneModeSettings();
                    break;
                case ScanMode.Prefab:
                    DrawPrefabModeSettings();
                    break;
                case ScanMode.Material:
                    DrawMaterialModeSettings();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSceneModeSettings()
        {
            EditorGUILayout.LabelField(L("シーンスキャン設定", "Scene Scan Settings"), EditorStyles.boldLabel);
            includeInactive = EditorGUILayout.ToggleLeft(
                L("非アクティブオブジェクトを含める", "Include Inactive Objects"), includeInactive);

            EditorGUILayout.Space(5);

            if (GUILayout.Button(L("シーンをスキャン", "Scan Scene"), GUILayout.Height(30)))
            {
                ScanScene();
            }
        }

        private void DrawPrefabModeSettings()
        {
            EditorGUILayout.LabelField(L("プレハブスキャン設定", "Prefab Scan Settings"), EditorStyles.boldLabel);
            targetPrefab = (GameObject)EditorGUILayout.ObjectField(
                L("対象プレハブ", "Target Prefab"), targetPrefab, typeof(GameObject), false);

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(targetPrefab == null))
            {
                if (GUILayout.Button(L("プレハブをスキャン", "Scan Prefab"), GUILayout.Height(30)))
                {
                    ScanPrefab(targetPrefab);
                }
            }

            if (GUILayout.Button(L("プロジェクト内の全プレハブをスキャン", "Scan All Project Prefabs"), GUILayout.Height(30)))
            {
                ScanProjectPrefabs();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaterialModeSettings()
        {
            EditorGUILayout.LabelField(L("マテリアルスキャン設定", "Material Scan Settings"), EditorStyles.boldLabel);
            nataneToonOnly = EditorGUILayout.ToggleLeft(
                L("Natane Toon Shader のみ", "Natane Toon Shader Only"), nataneToonOnly);

            EditorGUILayout.Space(5);

            if (GUILayout.Button(L("マテリアルをスキャン", "Scan Materials"), GUILayout.Height(30)))
            {
                ScanMaterials();
            }
        }

        private void DrawResultSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("結果サマリー", "Result Summary"), EditorStyles.boldLabel);

            int errorCount = issues.Count(i => i.severity == IssueSeverity.Error);
            int warningCount = issues.Count(i => i.severity == IssueSeverity.Warning);
            int infoCount = issues.Count(i => i.severity == IssueSeverity.Info);

            EditorGUILayout.BeginHorizontal();

            var originalColor = GUI.color;

            GUI.color = errorCount > 0 ? new Color(1f, 0.4f, 0.4f) : Color.white;
            EditorGUILayout.LabelField($"{L("エラー", "Errors")}: {errorCount}", EditorStyles.boldLabel, GUILayout.Width(160));

            GUI.color = warningCount > 0 ? new Color(1f, 0.9f, 0.3f) : Color.white;
            EditorGUILayout.LabelField($"{L("警告", "Warnings")}: {warningCount}", EditorStyles.boldLabel, GUILayout.Width(170));

            GUI.color = Color.white;
            EditorGUILayout.LabelField($"{L("情報", "Info")}: {infoCount}", EditorStyles.boldLabel, GUILayout.Width(130));

            GUI.color = originalColor;

            EditorGUILayout.EndHorizontal();

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    L("問題は検出されませんでした。", "No issues detected."),
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFilterToggles()
        {
            if (issues.Count == 0) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("フィルター:", "Filter:"), EditorStyles.miniLabel, GUILayout.Width(80));
            showErrors = EditorGUILayout.ToggleLeft(L("エラー", "Error"), showErrors, GUILayout.Width(70));
            showWarnings = EditorGUILayout.ToggleLeft(L("警告", "Warning"), showWarnings, GUILayout.Width(80));
            showInfos = EditorGUILayout.ToggleLeft(L("情報", "Info"), showInfos, GUILayout.Width(60));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(L("コンソールへ出力", "Export to Console"), GUILayout.Width(200)))
            {
                ExportToConsole();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawResults()
        {
            if (issues.Count == 0) return;

            var filteredIssues = issues.Where(i =>
                (i.severity == IssueSeverity.Error && showErrors) ||
                (i.severity == IssueSeverity.Warning && showWarnings) ||
                (i.severity == IssueSeverity.Info && showInfos)).ToList();

            EditorGUILayout.LabelField($"{L("表示中", "Showing")}: {filteredIssues.Count} / {issues.Count}", EditorStyles.miniLabel);

            if (droppedIssueCount > 0)
            {
                EditorGUILayout.HelpBox(
                    L($"This scan keeps the first {NataneBuildPolicySettings.instance.IssueSampleLimit} issues in memory.\nAdditional issues not stored: {droppedIssueCount}", $"This scan keeps the first {NataneBuildPolicySettings.instance.IssueSampleLimit} issues in memory.\nAdditional issues not stored: {droppedIssueCount}"),
                    MessageType.Info);
            }

            int pageSize = NataneBuildPolicySettings.instance.PageSize;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)filteredIssues.Count / pageSize));
            currentIssuePage = Mathf.Clamp(currentIssuePage, 0, pageCount - 1);
            DrawIssuePagination(pageCount);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var issue in filteredIssues.Skip(currentIssuePage * pageSize).Take(pageSize))
            {
                DrawIssueRow(issue);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIssueRow(ReferenceIssue issue)
        {
            Color bgColor;
            switch (issue.severity)
            {
                case IssueSeverity.Error:
                    bgColor = new Color(1f, 0.5f, 0.5f, 0.3f);
                    break;
                case IssueSeverity.Warning:
                    bgColor = new Color(1f, 1f, 0.5f, 0.3f);
                    break;
                default:
                    bgColor = new Color(0.5f, 0.8f, 1f, 0.3f);
                    break;
            }

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // Main row
            EditorGUILayout.BeginHorizontal();

            string icon = issue.severity == IssueSeverity.Error ? "[E]" :
                         issue.severity == IssueSeverity.Warning ? "[W]" : "[I]";
            EditorGUILayout.LabelField(icon, GUILayout.Width(25));

            EditorGUILayout.LabelField(issue.objectPath, EditorStyles.boldLabel);

            if (issue.targetObject != null)
            {
                if (GUILayout.Button("Select", GUILayout.Width(55)))
                {
                    EditorGUIUtility.PingObject(issue.targetObject);
                    Selection.activeObject = issue.targetObject;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Description
            EditorGUILayout.LabelField(issue.description, EditorStyles.wordWrappedLabel);

            // Foldout for details
            issue.foldout = EditorGUILayout.Foldout(issue.foldout, L("詳細", "Details"));
            if (issue.foldout)
            {
                EditorGUI.indentLevel++;
                if (!string.IsNullOrEmpty(issue.componentName))
                    EditorGUILayout.LabelField($"{L("コンポーネント", "Component")}: {issue.componentName}");
                if (!string.IsNullOrEmpty(issue.propertyName))
                    EditorGUILayout.LabelField($"{L("プロパティ", "Property")}: {issue.propertyName}");
                if (!string.IsNullOrEmpty(issue.assetPath))
                    EditorGUILayout.LabelField($"{L("アセットパス", "Asset Path")}: {issue.assetPath}");
                EditorGUILayout.LabelField($"{L("種類", "Type")}: {issue.type}");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // --- Scan Methods ---

        private void ScanScene()
        {
            issues.Clear();
            hasScanned = true;
            currentIssuePage = 0;
            droppedIssueCount = 0;

            var allObjects = FindObjectsOfType<GameObject>(includeInactive);

            for (int i = 0; i < allObjects.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    L("シーンをスキャン中", "Scanning Scene"),
                    $"{allObjects[i].name} ({i + 1}/{allObjects.Length})",
                    (float)i / allObjects.Length))
                {
                    break;
                }

                CheckGameObject(allObjects[i], GetGameObjectPath(allObjects[i]));
            }

            EditorUtility.ClearProgressBar();
            LogScanSummary("Scene");
            Repaint();
        }

        private void ScanPrefab(GameObject prefab)
        {
            if (prefab == null) return;

            issues.Clear();
            hasScanned = true;
            currentIssuePage = 0;
            droppedIssueCount = 0;

            string assetPath = AssetDatabase.GetAssetPath(prefab);
            var transforms = prefab.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    L("プレハブをスキャン中", "Scanning Prefab"),
                    $"{transforms[i].name} ({i + 1}/{transforms.Length})",
                    (float)i / transforms.Length))
                {
                    break;
                }

                string path = assetPath + "/" + GetRelativePath(prefab.transform, transforms[i]);
                CheckGameObject(transforms[i].gameObject, path);
            }

            EditorUtility.ClearProgressBar();
            LogScanSummary("Prefab: " + prefab.name);
            Repaint();
        }

        private void ScanProjectPrefabs()
        {
            issues.Clear();
            hasScanned = true;
            currentIssuePage = 0;
            droppedIssueCount = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", NataneBuildPolicySettings.instance.GetPrefabSearchFolders());

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (EditorUtility.DisplayCancelableProgressBar(
                    L("プロジェクト内プレハブをスキャン中", "Scanning Project Prefabs"),
                    $"{path} ({i + 1}/{guids.Length})",
                    (float)i / guids.Length))
                {
                    break;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var transforms = prefab.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    string objectPath = path + "/" + GetRelativePath(prefab.transform, t);
                    CheckGameObject(t.gameObject, objectPath, path);
                }
            }

            EditorUtility.ClearProgressBar();
            LogScanSummary("Project Prefabs");
            Repaint();
        }

        private void ScanMaterials()
        {
            issues.Clear();
            hasScanned = true;
            currentIssuePage = 0;
            droppedIssueCount = 0;

            List<MaterialIndexEntry> entries = NataneAssetIndexService
                .EnumerateMaterialEntries(entry => !nataneToonOnly || entry.isNataneShader)
                .OrderBy(entry => entry.path, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                MaterialIndexEntry entry = entries[i];
                string path = entry.path;

                if (EditorUtility.DisplayCancelableProgressBar(
                    L("マテリアルをスキャン中", "Scanning Materials"),
                    $"{path} ({i + 1}/{entries.Count})",
                    entries.Count == 0 ? 0f : (float)i / entries.Count))
                {
                    break;
                }

                Material material = NataneAssetIndexService.LoadMaterial(entry);
                if (material == null) continue;

                CheckMaterial(material, path);
            }

            EditorUtility.ClearProgressBar();
            LogScanSummary("Materials");
            Repaint();
        }

        // --- Check Methods ---

        private void CheckGameObject(GameObject go, string objectPath, string assetPath = "")
        {
            if (go == null) return;

            // 1. Missing Script detection
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingCount > 0)
            {
                TryAddIssue(new ReferenceIssue
                {
                    type = ReferenceIssueType.MissingScript,
                    severity = IssueSeverity.Error,
                    objectPath = objectPath,
                    componentName = "",
                    propertyName = "",
                    description = $"{missingCount} missing script(s) detected.",
                    targetObject = go,
                    assetPath = assetPath
                });
            }

            // 2. Missing Material detection via Renderers
            var renderers = go.GetComponents<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                var materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    if (materials[j] == null)
                    {
                        TryAddIssue(new ReferenceIssue
                        {
                            type = ReferenceIssueType.MissingMaterial,
                            severity = IssueSeverity.Error,
                            objectPath = objectPath,
                            componentName = renderer.GetType().Name,
                            propertyName = $"sharedMaterials[{j}]",
                            description = $"Renderer material slot [{j}] is null.",

                            targetObject = go,
                            assetPath = assetPath
                        });
                    }
                }
            }

            // 3. Null Reference detection via SerializedObject
            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue; // Missing script - already handled above

                var so = new SerializedObject(component);
                var sp = so.GetIterator();

                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (sp.objectReferenceInstanceIDValue != 0 && sp.objectReferenceValue == null)
                        {
                            TryAddIssue(new ReferenceIssue
                            {
                                type = ReferenceIssueType.NullReference,
                                severity = IssueSeverity.Warning,
                                objectPath = objectPath,
                                componentName = component.GetType().Name,
                                propertyName = sp.propertyPath,
                                description = $"Missing reference '{sp.propertyPath}' on {component.GetType().Name}.",

                                targetObject = go,
                                assetPath = assetPath
                            });
                        }
                    }
                }
            }
        }

        private void CheckMaterial(Material mat, string matPath)
        {
            if (mat == null) return;

            // 1. Missing Shader detection
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                TryAddIssue(new ReferenceIssue
                {
                    type = ReferenceIssueType.MissingShader,
                    severity = IssueSeverity.Error,
                    objectPath = mat.name,
                    componentName = "Material",
                    propertyName = "shader",
                    description = mat.shader == null
                        ? "Material shader is null."
                        : "Material shader is in error state (Hidden/InternalErrorShader).",

                    targetObject = mat,
                    assetPath = matPath
                });
                return; // Cannot inspect properties if shader is broken
            }

            // 2. Missing Texture detection
            int propertyCount = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < propertyCount; i++)
            {
                if (ShaderUtil.GetPropertyType(mat.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(mat.shader, i);
                var tex = mat.GetTexture(propName);

                if (tex == null)
                {
                    // _MainTex is considered required -> Error; others -> Info
                    bool isRequired = propName == "_MainTex";
                    TryAddIssue(new ReferenceIssue
                    {
                        type = ReferenceIssueType.MissingTexture,
                        severity = isRequired ? IssueSeverity.Warning : IssueSeverity.Info,
                        objectPath = mat.name,
                        componentName = "Material",
                        propertyName = propName,
                        description = isRequired
                            ? $"Required texture '{propName}' is not assigned."
                            : $"Texture '{propName}' is not assigned (optional).",
                        targetObject = mat,
                        assetPath = matPath
                    });
                }
            }
        }

        // --- Utility Methods ---

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return root.name;

            var parts = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Add(root.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private void ExportToConsole()
        {
            int errorCount = issues.Count(i => i.severity == IssueSeverity.Error);
            int warningCount = issues.Count(i => i.severity == IssueSeverity.Warning);
            int infoCount = issues.Count(i => i.severity == IssueSeverity.Info);

            Debug.Log($"[AssetReferenceChecker] === スキャン結果 Scan Results === " +
                      $"Errors: {errorCount}, Warnings: {warningCount}, Info: {infoCount}");


            foreach (var issue in issues)
            {
                string prefix = issue.severity == IssueSeverity.Error ? "[ERROR]" :
                               issue.severity == IssueSeverity.Warning ? "[WARN]" : "[INFO]";
                string message = $"[AssetReferenceChecker] {prefix} {issue.objectPath} - {issue.description.Replace("\n", " ")}";

                switch (issue.severity)
                {
                    case IssueSeverity.Error:
                        Debug.LogError(message, issue.targetObject);
                        break;
                    case IssueSeverity.Warning:
                        Debug.LogWarning(message, issue.targetObject);
                        break;
                    default:
                        Debug.Log(message, issue.targetObject);
                        break;
                }
            }
        }

        private void LogScanSummary(string scanType)
        {
            int errorCount = issues.Count(i => i.severity == IssueSeverity.Error);
            int warningCount = issues.Count(i => i.severity == IssueSeverity.Warning);
            int infoCount = issues.Count(i => i.severity == IssueSeverity.Info);

            Debug.Log($"[AssetReferenceChecker] {scanType} スキャン完了. " +
                      $"Total: {issues.Count} issues " +
                      $"(Errors: {errorCount}, Warnings: {warningCount}, Info: {infoCount})");


        }

        private void TryAddIssue(ReferenceIssue issue)
        {
            if (issue == null)
            {
                return;
            }

            if (issues.Count >= NataneBuildPolicySettings.instance.IssueSampleLimit)
            {
                droppedIssueCount++;
                return;
            }

            issues.Add(issue);
        }

        private void DrawIssuePagination(int pageCount)
        {
            if (pageCount <= 1)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.enabled = currentIssuePage > 0;
            if (GUILayout.Button(L("前へ", "Prev"), GUILayout.Width(90)))
            {
                currentIssuePage--;
            }

            GUI.enabled = currentIssuePage < pageCount - 1;
            if (GUILayout.Button("Next", GUILayout.Width(90)))
            {
                currentIssuePage++;
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{currentIssuePage + 1} / {pageCount}", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        private static new T[] FindObjectsOfType<T>(bool includeInactive) where T : Object
        {
            if (includeInactive)
            {
                // Resources.FindObjectsOfTypeAll includes inactive but also prefabs/assets,
                // so we filter to scene objects only
                return Resources.FindObjectsOfTypeAll<T>()
                    .Where(obj =>
                    {
                        if (obj is GameObject go)
                            return go.scene.isLoaded;
                        return false;
                    })
                    .ToArray();
            }
            return NataneEditorCompat.FindObjectsOfTypeCompat<T>();
        }
    }
}
