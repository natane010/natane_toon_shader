using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;

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

        /// <summary>
        /// ビルド前にシェーダーをプリウォームする（既定 OFF）。
        /// 有効判定は NataneBuildPolicySettings（EditorPrefs ではなく設定資産が正）。
        /// ビルド中に Package 内 SVC を自動収集・保存することはしない（読取と WarmUp のみ）。
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!NataneBuildPolicySettings.instance.BuildPrewarmEnabled)
                return;

            NataneBuildPreparationService.PrepareForBuild(forceRefresh: false, logSummary: false);
            Debug.Log("[Natane Toon] Pre-build shader prewarming started...");

            // ビルド中の空 SVC 自動収集(SaveAssets 発生)は廃止。手動メニューからの収集のみ許可。
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

        [MenuItem("Tools/Natane/Shader/シェーダープリウォーム Shader Prewarming/すべてプリウォーム Prewarm All Shaders", false, 72)]
        private static void PrewarmShadersMenu()
        {
            PrewarmShaders(true);
        }

        [MenuItem("Tools/Natane/Shader/シェーダープリウォーム Shader Prewarming/バリアントコレクションをプリウォーム Prewarm Shader Variant Collection", false, 721)]
        private static void PrewarmShaderVariantCollectionMenu()
        {
            ShaderVariantCollection collection = FindShaderVariantCollection();
            if (collection != null)
            {
                PrewarmShaderVariantCollection(collection);
            }
        }

        [MenuItem("Tools/Natane/Shader/シェーダープリウォーム Shader Prewarming/設定 Settings", false, 722)]
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
                // 設定資産(prewarmAutoFindVariants, 既定 false)を正とする。
                if (NataneBuildPolicySettings.instance.PrewarmAutoFindVariants)
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
            // ビルド結果に影響する設定は設定資産を正とする（EditorPrefs には残さない）。
            prewarmOnBuild = NataneBuildPolicySettings.instance.BuildPrewarmEnabled;
            autoFindMaterials = NataneBuildPolicySettings.instance.PrewarmAutoFindVariants;
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
                NataneBuildPolicySettings.instance.BuildPrewarmEnabled = prewarmOnBuild;
                NataneBuildPolicySettings.instance.SaveSettings();
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
                NataneBuildPolicySettings.instance.PrewarmAutoFindVariants = autoFindMaterials;
                NataneBuildPolicySettings.instance.SaveSettings();
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

            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                L("注意: Editor の WarmUp() はビルド済み Player や VRChat クライアントをプリウォームしません（Editor 内のシェーダーキャッシュのみを温めます）。",
                  "Note: Editor WarmUp() does NOT prewarm built Players or VRChat clients (it only warms the Editor's own shader cache)."),
                MessageType.Warning);

            EditorGUILayout.Space(20);
            DrawSeparator();
            EditorGUILayout.Space(20);

            // === Runtime Prewarming Section ===
            EditorGUILayout.LabelField(L("ランタイムプリウォーム (非 VRChat 専用)", "Runtime Prewarming (Non-VRChat Only)"), EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                L("警告: ランタイムプリウォームは VRChat では動作しません（VRChat はランタイムスクリプトを許可しません）。\n" +
                  "ランタイム側でプリウォームが必要な非 VRChat プロジェクトでのみ使ってください。", "WARNING: Runtime prewarming does NOT work in VRChat (VRChat disallows runtime scripts).\n" +
                "Only use this for non-VRChat projects where you need runtime shader prewarming."),
                MessageType.Warning);

            EditorGUILayout.Space(10);

            // C# スクリプト生成方式は廃止。固定コンポーネントへ誘導する。
            EditorGUILayout.HelpBox(
                L("固定コンポーネント NataneRuntimeShaderPrewarmer を使用してください（スクリプト生成方式は廃止）。\n" +
                  "シーン内の GameObject に追加し、ShaderVariantCollection を Inspector で割り当ててください。Start() で WarmUp します。",
                  "Use the fixed component NataneRuntimeShaderPrewarmer (script generation has been removed).\n" +
                  "Add it to a GameObject in your scene and assign a ShaderVariantCollection in the Inspector; it warms up on Start()."),
                MessageType.Info);

            EditorGUILayout.Space(10);

            // 旧生成物(RUNTIME_SCRIPT_PATH)が残っている場合のみ掃除ボタンを表示する。
            CheckRuntimeScriptExists();
            if (runtimeScriptExists)
            {
                EditorGUILayout.HelpBox(
                    L("旧方式で生成されたスクリプトが残っています", "A script generated by the old method still exists") + "\n" +
                    L("保存場所", "Location") + ": " + RUNTIME_SCRIPT_PATH,
                    MessageType.Warning);

                if (GUILayout.Button(L("旧ランタイムプリウォームスクリプトを削除", "Delete Legacy Runtime Prewarming Script"), GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog(
                        L("旧ランタイムスクリプト削除", "Delete Legacy Runtime Script"),
                        L("旧方式で生成されたランタイムプリウォームスクリプトを削除しますか？", "Delete the legacy runtime prewarming script generated by the old method?") + "\n\n" +
                        L("削除対象", "This will remove") + ": " + RUNTIME_SCRIPT_PATH,
                        L("削除", "Delete"),
                        L("キャンセル", "Cancel")))
                    {
                        DeleteRuntimeScript();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 旧方式(スクリプト生成)で作られたランタイムスクリプトが存在するか。存在時のみ掃除対象と判定する。
        /// 純粋にパス存在で判定する（テスト用に公開）。
        /// </summary>
        public static bool IsLegacyRuntimeScriptPresent(string path)
        {
            return !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
        }

        private void DrawSeparator()
        {
            EditorGUILayout.Space(5);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space(5);
        }

        // Runtime C# スクリプト生成方式(GenerateRuntimeScript)は廃止した。
        // 代替として Runtime/NataneRuntimeShaderPrewarmer.cs（固定コンポーネント）を使用する。
        // 旧方式で生成された残骸(RUNTIME_SCRIPT_PATH)の掃除のみ以下で提供する。

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
