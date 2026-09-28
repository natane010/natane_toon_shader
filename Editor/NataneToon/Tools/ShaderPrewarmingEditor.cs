using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.Linq;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Editor-only Shader Prewarming for Natane Toon Shader
    /// Automatically warms up shader variants before build to prevent runtime compilation stutters
    /// Safe for VRChat - no runtime scripts required
    /// </summary>
    public class ShaderPrewarmingEditor : IPreprocessBuildWithReport
    {
        // Build callback priority (lower = earlier execution)
        public int callbackOrder => 0;

        private const string PREWARM_ENABLED_KEY = "NataneToon_PrewarmOnBuild";
        private const string AUTO_FIND_ENABLED_KEY = "NataneToon_AutoFindVariants";

        /// <summary>
        /// Automatically prewarm shaders before build.
        /// Also auto-collects variants if the collection is empty.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!EditorPrefs.GetBool(PREWARM_ENABLED_KEY, true))
                return;

            NataneBuildPreparationService.PrepareForBuild(forceRefresh: false, logSummary: false);
            Debug.Log("[Natane Toon] Pre-build shader prewarming started...");

            // Auto-collect if collection is empty
            ShaderVariantCollection collection = FindShaderVariantCollection();
            if (collection != null && collection.variantCount == 0)
            {
                Debug.Log("[Natane Toon] Collection is empty. Auto-collecting from materials...");
                AutoCollectVariantsFromMaterials(collection);
            }

            PrewarmShaders(false);
        }

        /// <summary>
        /// Initialize shader prewarming on Unity editor load
        /// </summary>
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            // Optional: Prewarm shaders when Unity starts
            // Uncomment if you want automatic prewarming on editor startup
            // PrewarmShaders(false);
        }

        [MenuItem("Tools/Natane/シェーダー Shader/シェーダープリウォーム Shader Prewarming/すべてプリウォーム Prewarm All Shaders", false, 72)]
        private static void PrewarmShadersMenu()
        {
            PrewarmShaders(true);
        }

        [MenuItem("Tools/Natane/シェーダー Shader/シェーダープリウォーム Shader Prewarming/バリアントコレクションをプリウォーム Prewarm Shader Variant Collection", false, 721)]
        private static void PrewarmShaderVariantCollectionMenu()
        {
            ShaderVariantCollection collection = FindShaderVariantCollection();
            if (collection != null)
            {
                PrewarmShaderVariantCollection(collection);
            }
        }

        [MenuItem(NataneToolMenuPaths.ShaderPrewarming, false, 722)]
        private static void ShowSettings()
        {
            ShaderPrewarmingSettingsWindow.ShowWindow();
        }

        /// <summary>
        /// Main prewarming logic with progress bar support.
        /// </summary>
        private static void PrewarmShaders(bool showDialog)
        {
            float startTime = Time.realtimeSinceStartup;
            int totalWarmed = 0;

            try
            {
                NataneBuildPreparationService.PrepareForBuild(forceRefresh: showDialog, logSummary: false);

                // Stage 1: Prewarm ShaderVariantCollection if exists
                EditorUtility.DisplayProgressBar(
                    L("Shader Prewarming", "Shader Prewarming"),
                    L("Finding ShaderVariantCollection...", "Finding ShaderVariantCollection..."),
                    0.1f);

                ShaderVariantCollection collection = FindShaderVariantCollection();
                if (collection != null)
                {
                    EditorUtility.DisplayProgressBar(
                        L("Shader Prewarming", "Shader Prewarming"),
                        L($"Warming up collection ({collection.variantCount} variants)...", $"Warming up collection ({collection.variantCount} variants)..."),
                        0.3f);

                    PrewarmShaderVariantCollection(collection);
                    totalWarmed += collection.variantCount;
                }

                // Stage 2: Auto-find and prewarm all Natane Toon Shader materials
                if (EditorPrefs.GetBool(AUTO_FIND_ENABLED_KEY, true))
                {
                    EditorUtility.DisplayProgressBar(
                        L("Shader Prewarming", "Shader Prewarming"),
                        L("Warming up Natane Toon materials...", "Warming up Natane Toon materials..."),
                        0.6f);

                    int materialCount = PrewarmAllNataneToonMaterials();
                    totalWarmed += materialCount;
                }

                EditorUtility.DisplayProgressBar(
                    L("Shader Prewarming", "Shader Prewarming"),
                    L("Complete!", "Complete!"),
                    1.0f);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            float elapsedTime = Time.realtimeSinceStartup - startTime;

            string message = $"[Natane Toon] Shader prewarming completed!\n" +
                           $"Time: {elapsedTime:F3} seconds\n" +
                           $"Variants/Materials warmed: {totalWarmed}";

            Debug.Log(message);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    L("Shader Prewarming Complete", "Shader Prewarming Complete"),
                    message,
                    "OK");
            }
        }

        /// <summary>
        /// Prewarm a ShaderVariantCollection
        /// </summary>
        private static void PrewarmShaderVariantCollection(ShaderVariantCollection collection)
        {
            if (collection == null) return;

            Debug.Log($"[Natane Toon] Prewarming ShaderVariantCollection: {collection.name}\n" +
                     $"Shader Count: {collection.shaderCount}\n" +
                     $"Variant Count: {collection.variantCount}");

            collection.WarmUp();
        }

        /// <summary>
        /// Find all materials using Natane Toon Shader and prewarm them
        /// using a temporary ShaderVariantCollection (targeted, not Shader.WarmupAllShaders).
        /// </summary>
        private static int PrewarmAllNataneToonMaterials()
        {
            Dictionary<string, List<string[]>> shaderKeywordSets = NataneBuildPreparationService.LoadShaderKeywordSets();
            ShaderVariantCollection tempCollection = new ShaderVariantCollection();
            int uniqueShaderCount = 0;

            for (int i = 0; i < NataneShaderCatalog.ShaderConfigs.Length; i++)
            {
                NataneShaderCatalog.ShaderPassConfig config = NataneShaderCatalog.ShaderConfigs[i];
                Shader shader = Shader.Find(config.name);
                if (shader == null)
                {
                    continue;
                }

                uniqueShaderCount++;
                if (!shaderKeywordSets.TryGetValue(config.name, out List<string[]> keywordSets) || keywordSets.Count == 0)
                {
                    keywordSets = new List<string[]> { Array.Empty<string>() };
                }

                for (int j = 0; j < keywordSets.Count; j++)
                {
                    string[] keywords = keywordSets[j];
                    AddVariantSafe(tempCollection, shader, PassType.ForwardBase, keywords);
                    if (config.hasForwardAdd)
                    {
                        AddVariantSafe(tempCollection, shader, PassType.ForwardAdd, keywords);
                    }
                    if (config.hasNormalPass)
                    {
                        AddVariantSafe(tempCollection, shader, PassType.Normal, keywords);
                    }
                }

                if (config.hasShadowCaster)
                {
                    AddVariantSafe(tempCollection, shader, PassType.ShadowCaster, Array.Empty<string>());
                }
                if (config.hasMeta)
                {
                    AddVariantSafe(tempCollection, shader, PassType.Meta, Array.Empty<string>());
                }
            }

            if (tempCollection.variantCount > 0)
            {
                tempCollection.WarmUp();
                Debug.Log($"[Natane Toon] Prewarmed {tempCollection.variantCount} manifest variants across {uniqueShaderCount} shaders");
            }

            return tempCollection.variantCount;
        }

        /// <summary>
        /// Dynamically find the ShaderVariantCollection asset.
        /// Searches multiple known paths to support both Assets and UPM package layouts.
        /// </summary>
        private static ShaderVariantCollection FindShaderVariantCollection()
        {
            string resolvedPath = FindShaderVariantPath();

            if (!string.IsNullOrEmpty(resolvedPath))
            {
                ShaderVariantCollection collection =
                    AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(resolvedPath);
                if (collection != null)
                    return collection;
            }

            // Fallback: search all ShaderVariantCollections by name
            string[] collectionGUIDs = AssetDatabase.FindAssets("t:ShaderVariantCollection");

            foreach (string guid in collectionGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ShaderVariantCollection collection =
                    AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);

                if (collection != null && collection.name.Contains("NataneToon"))
                {
                    Debug.Log($"[Natane Toon] Found ShaderVariantCollection at: {path}");
                    return collection;
                }
            }

            Debug.LogWarning("[Natane Toon] No ShaderVariantCollection found. " +
                           "Use Tools > Natane > Shader > Shader Variant Collector to create one.");
            return null;
        }

        /// <summary>
        /// Dynamically resolve the ShaderVariantCollection file path.
        /// Checks AssetDatabase search first, then known fallback paths.
        /// </summary>
        private static string FindShaderVariantPath()
        {
            // 1. Search via AssetDatabase
            string[] guids = AssetDatabase.FindAssets("NataneToonShaderVariants t:ShaderVariantCollection");
            if (guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            // 2. Resolve the path relative to the current Natane package root
            if (NatanePackagePathResolver.TryResolvePackageAssetPath(
                "ShaderVariants/NataneToonShaderVariants.shadervariants",
                out string packageVariantPath) &&
                AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(packageVariantPath) != null)
            {
                return packageVariantPath;
            }

            return null;
        }

        /// <summary>
        /// Shader pass type configuration for auto-collection.
        /// </summary>
        private struct ShaderPassConfig
        {
            public string name;
            public bool hasForwardAdd;
            public bool hasShadowCaster;
            public bool hasMeta;
            public bool hasNormalPass;

            public ShaderPassConfig(string name, bool hasForwardAdd, bool hasShadowCaster, bool hasMeta = false, bool hasNormalPass = false)
            {
                this.name = name;
                this.hasForwardAdd = hasForwardAdd;
                this.hasShadowCaster = hasShadowCaster;
                this.hasMeta = hasMeta;
                this.hasNormalPass = hasNormalPass;
            }
        }

        private static readonly ShaderPassConfig[] AllShaderConfigs = new ShaderPassConfig[]
        {
            new ShaderPassConfig("Natane/Toon Shader",                   true,  true),
            new ShaderPassConfig("Natane/Toon Shader (ScreenEdge Split)", true,  true, false, true),
            new ShaderPassConfig("Natane/Toon Shader (Cutout)",           true,  true),
            new ShaderPassConfig("Natane/Toon Shader (Transparent)",      true,  false),
            new ShaderPassConfig("Natane/Toon Shader (Lite)",             true,  true),
            new ShaderPassConfig("Natane/Toon Shader (Cutout Lite)",      true,  true),
            new ShaderPassConfig("Natane/Toon Shader (Transparent Lite)", true,  false),
            new ShaderPassConfig("Natane/Toon Shader (Fur)",              true,  true),
            new ShaderPassConfig("Natane/Toon Shader (Fur Lite)",         true,  true),
            new ShaderPassConfig("Natane/Toon Shader (Background)",       true,  true, true),
            new ShaderPassConfig("Natane/Toon Shader (Tessellation)",     true,  true),
            new ShaderPassConfig("Natane/Toon Shader (Cutout Tessellation)", true, true),
            new ShaderPassConfig("Natane/Toon Shader (Transparent Tessellation)", true, false),
            new ShaderPassConfig("Natane/Toon Shader Wirelight",          false, false),
            new ShaderPassConfig("Natane/Eye",                            false, false),
            new ShaderPassConfig("Natane/Screen FX Overlay",              false, false),
        };

        /// <summary>
        /// Auto-collect variants from project materials into the given collection.
        /// Runs when the collection is empty at build time.
        /// </summary>
        private static void AutoCollectVariantsFromMaterials(ShaderVariantCollection collection)
        {
            if (collection == null) return;

            Dictionary<string, List<string[]>> perShaderKeywordSets = NataneBuildPreparationService.LoadShaderKeywordSets();
            int nataneMaterialCount = perShaderKeywordSets.Sum(pair => pair.Value.Count);

            int totalAdded = 0;
            int uniqueKeywordSets = 0;

            foreach (var config in AllShaderConfigs)
            {
                Shader shader = Shader.Find(config.name);
                if (shader == null) continue;

                // Always include base variant
                var keywordSets = new List<string[]> { new string[] { } };

                if (perShaderKeywordSets.TryGetValue(config.name, out List<string[]> loadedKeywordSets))
                {
                    foreach (string[] loadedKeywordSet in loadedKeywordSets)
                    {
                        if (loadedKeywordSet.Length > 0)
                            keywordSets.Add(loadedKeywordSet);
                    }
                }

                uniqueKeywordSets += keywordSets.Count;

                foreach (string[] keywords in keywordSets)
                {
                    totalAdded += AddVariantSafe(collection, shader, PassType.ForwardBase, keywords);
                    if (config.hasForwardAdd)
                        totalAdded += AddVariantSafe(collection, shader, PassType.ForwardAdd, keywords);
                    if (config.hasNormalPass)
                        totalAdded += AddVariantSafe(collection, shader, PassType.Normal, keywords);
                }

                if (config.hasShadowCaster)
                    totalAdded += AddVariantSafe(collection, shader, PassType.ShadowCaster, new string[] { });
                if (config.hasNormalPass)
                    totalAdded += AddVariantSafe(collection, shader, PassType.Normal, new string[] { });
                if (config.hasMeta)
                    totalAdded += AddVariantSafe(collection, shader, PassType.Meta, new string[] { });
            }

            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Natane Toon] Auto-collected {totalAdded} variants from {nataneMaterialCount} materials " +
                      $"({uniqueKeywordSets} unique keyword sets across all shaders)");
        }

        private static int AddVariantSafe(ShaderVariantCollection collection, Shader shader,
            PassType passType, string[] keywords)
        {
            if (shader == null || collection == null) return 0;

            var variant = new ShaderVariantCollection.ShaderVariant
            {
                shader = shader,
                passType = passType,
                keywords = keywords ?? Array.Empty<string>()
            };

            if (!collection.Contains(variant))
            {
                collection.Add(variant);
                return 1;
            }

            return 0;
        }
    }

    /// <summary>
    /// Settings window for Shader Prewarming
    /// </summary>
    public class ShaderPrewarmingSettingsWindow : EditorWindow
    {
        private const string PREWARM_ENABLED_KEY = "NataneToon_PrewarmOnBuild";
        private const string AUTO_FIND_ENABLED_KEY = "NataneToon_AutoFindVariants";
        private const string RUNTIME_SCRIPT_PATH = "Assets/Scripts/RuntimeShaderPrewarming.cs";

        private bool prewarmOnBuild;
        private bool autoFindMaterials;
        private bool runtimeScriptExists;
        private Vector2 scrollPosition;

        public static void ShowWindow()
        {
            var window = GetWindow<ShaderPrewarmingSettingsWindow>(L("シェーダープリウォーム設定", "Shader Prewarming Settings"));
            window.minSize = new Vector2(450, 400);
            window.Show();
        }

        private void OnEnable()
        {
            prewarmOnBuild = EditorPrefs.GetBool(PREWARM_ENABLED_KEY, true);
            autoFindMaterials = EditorPrefs.GetBool(AUTO_FIND_ENABLED_KEY, true);
            CheckRuntimeScriptExists();
        }

        private void CheckRuntimeScriptExists()
        {
            runtimeScriptExists = System.IO.File.Exists(RUNTIME_SCRIPT_PATH);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(L("Natane Toon Shader プリウォーム設定", "Natane Toon Shader Prewarming Settings"), EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                L("シェーダープリウォームはバリアントを事前コンパイルし、プレイ中のカクつきを減らします。\n" +
                  "VRChat のワールドやアバターでは特に効果的です。", "Shader prewarming compiles shader variants in advance to prevent stuttering during gameplay.\n" +
                "This is especially important for VRChat worlds and avatars."),
                MessageType.Info);

            EditorGUILayout.Space(10);

            // === Editor-Only Prewarming Section ===
            EditorGUILayout.LabelField(L("Editor 限定プリウォーム (VRChat 安全)", "Editor-Only Prewarming (VRChat Safe)"), EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Prewarm on build setting
            EditorGUI.BeginChangeCheck();
            prewarmOnBuild = EditorGUILayout.Toggle(
                new GUIContent(
                    L("ビルド時にプリウォーム", "Prewarm on Build"),
                    L("ビルド前に自動でシェーダーをプリウォームします", "Automatically prewarm shaders before building")),
                prewarmOnBuild);

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PREWARM_ENABLED_KEY, prewarmOnBuild);
            }

            EditorGUILayout.Space(5);

            // Auto-find materials setting
            EditorGUI.BeginChangeCheck();
            autoFindMaterials = EditorGUILayout.Toggle(
                new GUIContent(
                    L("マテリアルを自動検出", "Auto-Find Materials"),
                    L("プロジェクト内の Natane Toon マテリアルを自動検出してプリウォームします", "Automatically find and prewarm all Natane Toon materials in the project")),
                autoFindMaterials);

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(AUTO_FIND_ENABLED_KEY, autoFindMaterials);
            }

            EditorGUILayout.Space(20);

            // Manual prewarm button
            if (GUILayout.Button(L("今すぐプリウォーム", "Prewarm Shaders Now"), GUILayout.Height(30)))
            {
                EditorApplication.delayCall += () =>
                {
                    // Use reflection to call private method
                    var method = typeof(ShaderPrewarmingEditor).GetMethod(
                        "PrewarmShaders",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    method.Invoke(null, new object[] { true });
                };
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                L("この機能は Unity Editor 上でのみ動作し、ランタイムには入らないため VRChat でも安全です。", "Note: This system is VRChat-safe as it only runs in the Unity Editor, not at runtime."),
                MessageType.None);

            EditorGUILayout.Space(20);
            DrawSeparator();
            EditorGUILayout.Space(20);

            // === Runtime Prewarming Section ===
            EditorGUILayout.LabelField(L("ランタイムプリウォーム (非 VRChat 専用)", "Runtime Prewarming (Non-VRChat Only)"), EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                L("警告: ランタイムプリウォームスクリプトは VRChat では動作しません。\n" +
                  "ランタイム側でプリウォームが必要な非 VRChat プロジェクトでのみ使ってください。", "WARNING: Runtime prewarming scripts DO NOT work in VRChat!\n" +
                "Only enable this for non-VRChat projects where you need runtime shader prewarming."),
                MessageType.Warning);

            EditorGUILayout.Space(10);

            // Runtime script status
            CheckRuntimeScriptExists();

            if (runtimeScriptExists)
            {
                EditorGUILayout.HelpBox(
                    L("ランタイムプリウォームスクリプトは有効です", "Runtime prewarming script is ENABLED") + "\n" +
                    L("保存場所", "Location") + ": " + RUNTIME_SCRIPT_PATH,
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    L("ランタイムプリウォームスクリプトは無効です", "Runtime prewarming script is DISABLED"),
                    MessageType.None);
            }

            EditorGUILayout.Space(10);

            // Generate/Delete buttons
            using (new EditorGUI.DisabledScope(runtimeScriptExists))
            {
                if (GUILayout.Button(L("ランタイムプリウォームスクリプトを生成", "Generate Runtime Prewarming Script"), GUILayout.Height(30)))
                {
                    GenerateRuntimeScript();
                }
            }

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(!runtimeScriptExists))
            {
                if (GUILayout.Button(L("ランタイムプリウォームスクリプトを削除", "Delete Runtime Prewarming Script"), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog(
                        L("ランタイムスクリプト削除", "Delete Runtime Script"),
                        L("ランタイムプリウォームスクリプトを削除しますか？", "Are you sure you want to delete the runtime prewarming script?") + "\n\n" +
                        L("削除対象", "This will remove") + ": " + RUNTIME_SCRIPT_PATH,
                        L("削除", "Delete"),
                        L("キャンセル", "Cancel")))
                    {
                        DeleteRuntimeScript();
                    }
                }
            }

            EditorGUILayout.Space(10);

            if (runtimeScriptExists)
            {
                EditorGUILayout.HelpBox(
                    L("使い方: シーン内の GameObject に RuntimeShaderPrewarming を追加し、ShaderVariantCollection を割り当ててください。", "Usage: Add the RuntimeShaderPrewarming component to a GameObject in your scene and assign the ShaderVariantCollection."),
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSeparator()
        {
            EditorGUILayout.Space(5);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space(5);
        }

        private void GenerateRuntimeScript()
        {
            string scriptContent = @"using UnityEngine;

/// <summary>
/// Runtime Shader Prewarming Script for Natane Toon Shader
/// Warms up shader variants at runtime to prevent compilation stutters
///
/// WARNING: This script does NOT work in VRChat!
/// For VRChat projects, use the editor-only prewarming system instead.
/// </summary>
public class RuntimeShaderPrewarming : MonoBehaviour
{
    [Header(""Shader Variant Collection"")]
    [Tooltip(""Reference to the ShaderVariantCollection asset"")]
    public ShaderVariantCollection shaderVariants;

    [Header(""Prewarming Options"")]
    [Tooltip(""Warm shaders on Awake (immediate)"")]
    public bool prewarmOnAwake = true;

    [Tooltip(""Warm shaders on Start (delayed)"")]
    public bool prewarmOnStart = false;

    [Tooltip(""Show debug logs"")]
    public bool showDebugLogs = false;

    private void Awake()
    {
        if (prewarmOnAwake && shaderVariants != null)
        {
            PrewarmShaders();
        }
    }

    private void Start()
    {
        if (prewarmOnStart && !prewarmOnAwake && shaderVariants != null)
        {
            PrewarmShaders();
        }
    }

    /// <summary>
    /// Manually prewarm shader variants
    /// Call this method when you want to prewarm shaders at a specific time
    /// </summary>
    public void PrewarmShaders()
    {
        if (shaderVariants == null)
        {
            Debug.LogWarning(""[RuntimeShaderPrewarming] No ShaderVariantCollection assigned!"");
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($""[RuntimeShaderPrewarming] Starting shader prewarming...\n"" +
                     $""Shader Count: {shaderVariants.shaderCount}\n"" +
                     $""Variant Count: {shaderVariants.variantCount}"");
        }

        float startTime = Time.realtimeSinceStartup;

        // Warm up all shader variants in the collection
        shaderVariants.WarmUp();

        float elapsedTime = Time.realtimeSinceStartup - startTime;

        if (showDebugLogs)
        {
            Debug.Log($""[RuntimeShaderPrewarming] Shader prewarming completed in {elapsedTime:F3} seconds"");
        }
    }
}
";

            try
            {
                // Ensure directory exists
                string directory = System.IO.Path.GetDirectoryName(RUNTIME_SCRIPT_PATH);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // Write script file
                System.IO.File.WriteAllText(RUNTIME_SCRIPT_PATH, scriptContent);

                // Refresh AssetDatabase
                AssetDatabase.Refresh();

                Debug.Log($"[Natane Toon] Runtime prewarming script generated at: {RUNTIME_SCRIPT_PATH}");
                EditorUtility.DisplayDialog(
                    L("Script Generated", "Script Generated"),
                    L("Runtime prewarming script has been generated successfully!", "Runtime prewarming script has been generated successfully!") + "\n\n" +
                    L("Location", "Location") + ": " + RUNTIME_SCRIPT_PATH + "\n\n" +
                    L("Usage: Add this component to a GameObject in your scene and assign the ShaderVariantCollection.", "Usage: Add this component to a GameObject in your scene and assign the ShaderVariantCollection."),
                    "OK");

                CheckRuntimeScriptExists();

                // Ping the asset in the Project window
                EditorApplication.delayCall += () =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(RUNTIME_SCRIPT_PATH);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                };
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Natane Toon] Failed to generate runtime script: {ex.Message}");
                EditorUtility.DisplayDialog(
                    L("Error", "Error"),
                    L("Failed to generate runtime script:", "Failed to generate runtime script:") + "\n" + ex.Message,
                    "OK");
            }
        }

        private void DeleteRuntimeScript()
        {
            try
            {
                if (System.IO.File.Exists(RUNTIME_SCRIPT_PATH))
                {
                    // Delete the script file
                    AssetDatabase.DeleteAsset(RUNTIME_SCRIPT_PATH);
                    AssetDatabase.Refresh();

                    Debug.Log($"[Natane Toon] Runtime prewarming script deleted: {RUNTIME_SCRIPT_PATH}");
                    EditorUtility.DisplayDialog(
                        L("Script Deleted", "Script Deleted"),
                        L("Runtime prewarming script has been deleted successfully.", "Runtime prewarming script has been deleted successfully."),
                        "OK");

                    CheckRuntimeScriptExists();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Natane Toon] Failed to delete runtime script: {ex.Message}");
                EditorUtility.DisplayDialog(
                    L("Error", "Error"),
                    L("Failed to delete runtime script:", "Failed to delete runtime script:") + "\n" + ex.Message,
                    "OK");
            }
        }
    }
}
