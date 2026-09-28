using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Build-time shader variant stripper for Natane Toon shaders.
    /// </summary>
    public class ShaderVariantStripper : IPreprocessShaders
    {
        private const string STRIP_ENABLED_KEY = "NataneToon_VariantStrippingEnabled";
        private const string STRIP_LOG_KEY = "NataneToon_VariantStrippingLog";

        private static HashSet<string> whitelistedKeywordSets;
        private static bool initialized;
        private static int strippedCount;
        private static int keptCount;

        public int callbackOrder => 100;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!EditorPrefs.GetBool(STRIP_ENABLED_KEY, false))
            {
                return;
            }

            if (!NataneShaderCatalog.IsNataneShader(shader.name))
            {
                return;
            }

            if (!initialized)
            {
                InitializeKeywordWhitelist();
                initialized = true;
                strippedCount = 0;
                keptCount = 0;
            }

            bool logEnabled = EditorPrefs.GetBool(STRIP_LOG_KEY, true);

            for (int i = data.Count - 1; i >= 0; i--)
            {
                string keywordSetKey = GetKeywordSetKey(data[i].shaderKeywordSet);
                if (whitelistedKeywordSets.Contains(keywordSetKey))
                {
                    keptCount++;
                }
                else
                {
                    data.RemoveAt(i);
                    strippedCount++;
                }
            }

            if (logEnabled && (strippedCount + keptCount) > 0)
            {
                Debug.Log($"[Natane Toon Stripper] {shader.name} ({snippet.passType}): kept={keptCount}, stripped={strippedCount}");
            }
        }

        private static void InitializeKeywordWhitelist()
        {
            NataneBuildPreparationService.PrepareForBuild(forceRefresh: false, logSummary: false);
            whitelistedKeywordSets = NataneBuildPreparationService.LoadKeywordWhitelist();
            AddVariantsFromCollections();

            if (EditorPrefs.GetBool(STRIP_LOG_KEY, true))
            {
                Debug.Log($"[Natane Toon Stripper] Whitelist initialized: {CountNataneMaterials()} indexed materials, {whitelistedKeywordSets.Count} unique keyword sets whitelisted");
            }
        }

        private static void AddVariantsFromCollections()
        {
            string[] collectionGuids = AssetDatabase.FindAssets("t:ShaderVariantCollection");
            for (int i = 0; i < collectionGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(collectionGuids[i]);
                ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
                if (collection == null || !path.Contains("NataneToon"))
                {
                    continue;
                }

                if (EditorPrefs.GetBool(STRIP_LOG_KEY, true))
                {
                    Debug.Log($"[Natane Toon Stripper] Found ShaderVariantCollection: {path} ({collection.variantCount} variants)");
                }
            }
        }

        private static string GetKeywordSetKey(ShaderKeywordSet keywordSet)
        {
            var keywords = new List<string>();
            foreach (ShaderKeyword keyword in keywordSet.GetShaderKeywords())
            {
                if (!string.IsNullOrEmpty(keyword.name) && keyword.name.StartsWith("_"))
                {
                    keywords.Add(keyword.name);
                }
            }

            keywords.Sort();
            return string.Join(";", keywords);
        }

        private static int CountNataneMaterials()
        {
            return NataneAssetIndexService.EnumerateMaterialEntries(entry => entry.isNataneShader).Count();
        }

        [InitializeOnLoadMethod]
        private static void ResetBuildSessionState()
        {
            initialized = false;
            whitelistedKeywordSets = null;
            strippedCount = 0;
            keptCount = 0;
        }

        [MenuItem(NataneToolMenuPaths.ShaderVariantStripper, false, 73)]
        public static void ShowSettingsWindow()
        {
            ShaderVariantStripperSettingsWindow.ShowWindow();
        }
    }

    /// <summary>
    /// Settings window for shader variant stripping.
    /// </summary>
    public class ShaderVariantStripperSettingsWindow : EditorWindow
    {
        private const string STRIP_ENABLED_KEY = "NataneToon_VariantStrippingEnabled";
        private const string STRIP_LOG_KEY = "NataneToon_VariantStrippingLog";

        private bool strippingEnabled;
        private bool logEnabled;

        public static void ShowWindow()
        {
            var window = GetWindow<ShaderVariantStripperSettingsWindow>(L("バリアントストリッピング設定", "Variant Stripping Settings"));
            window.minSize = new Vector2(450, 320);
            window.Show();
        }

        private void OnEnable()
        {
            strippingEnabled = EditorPrefs.GetBool(STRIP_ENABLED_KEY, false);
            logEnabled = EditorPrefs.GetBool(STRIP_LOG_KEY, true);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(L("Natane Toon バリアントストリッピング設定", "Natane Toon Variant Stripping Settings"), EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                L("インデックス済みマテリアルを基準に、未使用シェーダーバリアントをビルド時に自動除去します。\n" +
                  "これによりビルドサイズを大きく削減できます。",
                  "Automatically strips unused shader variants at build time based on indexed materials.\n" +
                  "This significantly reduces build size."),
                MessageType.Info);

            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            strippingEnabled = EditorGUILayout.Toggle(
                new GUIContent(L("バリアントストリッピングを有効化", "Enable Variant Stripping"), L("ビルド時に未使用バリアントを自動除去します", "Automatically strip unused variants at build time")),
                strippingEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(STRIP_ENABLED_KEY, strippingEnabled);
            }

            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();
            logEnabled = EditorGUILayout.Toggle(
                new GUIContent("Enable Log Output", "Output stripping results to the Console"),
                logEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(STRIP_LOG_KEY, logEnabled);
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            int materialCount = CountNataneMaterials();
            EditorGUILayout.LabelField($"Natane Toon Material Count: {materialCount}");

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                strippingEnabled
                    ? "Stripping is enabled. It will run automatically on the next build."
                    : "Stripping is disabled. Build size may be larger than necessary.",
                strippingEnabled ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Scan Material Keywords", GUILayout.Height(30)))
            {
                ScanAndDisplayKeywords();
            }
        }

        private static int CountNataneMaterials()
        {
            return NataneAssetIndexService.EnumerateMaterialEntries(entry => entry.isNataneShader).Count();
        }

        private static void ScanAndDisplayKeywords()
        {
            int materialCount = CountNataneMaterials();
            NataneBuildPreparationService.PrepareForBuild(forceRefresh: false, logSummary: false);
            Dictionary<string, List<string[]>> perShaderKeywordSets = NataneBuildPreparationService.LoadShaderKeywordSets();
            var uniqueSets = new HashSet<string>();
            var reportLines = new List<string>();

            for (int i = 0; i < NataneShaderCatalog.ShaderConfigs.Length; i++)
            {
                NataneShaderCatalog.ShaderPassConfig config = NataneShaderCatalog.ShaderConfigs[i];
                if (!perShaderKeywordSets.TryGetValue(config.name, out List<string[]> keywordSets) || keywordSets == null || keywordSets.Count == 0)
                {
                    continue;
                }

                reportLines.Add(config.name);
                for (int j = 0; j < keywordSets.Count; j++)
                {
                    string[] keywords = keywordSets[j];
                    string key = keywords == null || keywords.Length == 0
                        ? "(base variant)"
                        : string.Join(", ", keywords.OrderBy(keyword => keyword));
                    uniqueSets.Add(key);
                    reportLines.Add($"  - {key}");
                }

                reportLines.Add(string.Empty);
            }

            string report = $"Natane Toon materials: {materialCount}\n" +
                           $"Unique keyword sets: {uniqueSets.Count}\n\n" +
                           string.Join("\n", reportLines);

            Debug.Log($"[Natane Toon Stripper] Keyword scan results:\n{report}");

            EditorUtility.DisplayDialog(
                "Scan Results",
                $"Materials: {materialCount}\nUnique Keyword Sets: {uniqueSets.Count}\n\nSee Console for details.",
                "OK");
        }
    }
}
