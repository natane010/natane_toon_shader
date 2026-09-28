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

        // A build that goes quiet for longer than this is treated as finished, so the
        // next OnProcessShader call re-reads the whitelist. IPreprocessShaders is also
        // invoked by AssetBundle builds (VRChat SDK uploads) which never call
        // IPreprocessBuildWithReport, so we cannot rely on that hook alone for resets.
        private const double BUILD_SESSION_IDLE_SECONDS = 60.0;

        private static HashSet<string> whitelistedKeywordSets;
        // Same whitelist split into keyword arrays, for per-pass projection.
        private static List<string[]> whitelistedKeywordArrays;
        private static bool initialized;
        private static double lastProcessTime;
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

            double now = EditorApplication.timeSinceStartup;
            if (initialized && now - lastProcessTime > BUILD_SESSION_IDLE_SECONDS)
            {
                // New build in the same editor session: materials may have changed since the
                // previous build, so refresh the whitelist instead of reusing the stale one.
                ResetBuildSessionState();
            }
            lastProcessTime = now;

            if (!initialized)
            {
                InitializeKeywordWhitelist();
                initialized = true;
                strippedCount = 0;
                keptCount = 0;
            }

            bool logEnabled = EditorPrefs.GetBool(STRIP_LOG_KEY, true);

            // The whitelist holds each material's FULL keyword set (all passes merged),
            // but each pass only declares a subset of the shader's keywords (OUTLINE ~10,
            // FORWARD_ADD ~40, SHADOW_CASTER 0). Comparing the full set against a per-pass
            // variant key never matches for those passes and strips e.g. every outline
            // variant. Project the whitelist onto the keywords this snippet actually
            // declares before comparing.
            HashSet<string> passKeywords = CollectPassKeywords(data);
            HashSet<string> projectedWhitelist = ProjectWhitelist(passKeywords);

            for (int i = data.Count - 1; i >= 0; i--)
            {
                string keywordSetKey = GetKeywordSetKey(data[i].shaderKeywordSet);
                if (projectedWhitelist.Contains(keywordSetKey))
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
            whitelistedKeywordSets = NataneBuildPreparationService.LoadKeywordWhitelist() ?? new HashSet<string>();
            whitelistedKeywordArrays = new List<string[]>(whitelistedKeywordSets.Count);
            foreach (string key in whitelistedKeywordSets)
            {
                whitelistedKeywordArrays.Add(string.IsNullOrEmpty(key)
                    ? new string[0]
                    : key.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
            }
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

        /// <summary>
        /// Union of all local ("_"-prefixed) keywords that appear in any variant of this
        /// snippet, i.e. the keywords the pass declares via shader_feature/multi_compile.
        /// </summary>
        private static HashSet<string> CollectPassKeywords(IList<ShaderCompilerData> data)
        {
            var passKeywords = new HashSet<string>();
            for (int i = 0; i < data.Count; i++)
            {
                foreach (ShaderKeyword keyword in data[i].shaderKeywordSet.GetShaderKeywords())
                {
                    if (!string.IsNullOrEmpty(keyword.name) && keyword.name.StartsWith("_"))
                    {
                        passKeywords.Add(keyword.name);
                    }
                }
            }
            return passKeywords;
        }

        /// <summary>
        /// Intersects every whitelisted material keyword set with the pass's declared
        /// keywords and returns the resulting per-pass variant keys. The empty key (base
        /// variant) is always included so passes without features survive.
        /// </summary>
        private static HashSet<string> ProjectWhitelist(HashSet<string> passKeywords)
        {
            var projected = new HashSet<string> { string.Empty };
            if (whitelistedKeywordArrays == null)
            {
                return projected;
            }

            var buffer = new List<string>();
            for (int i = 0; i < whitelistedKeywordArrays.Count; i++)
            {
                buffer.Clear();
                string[] materialKeywords = whitelistedKeywordArrays[i];
                for (int k = 0; k < materialKeywords.Length; k++)
                {
                    if (passKeywords.Contains(materialKeywords[k]))
                    {
                        buffer.Add(materialKeywords[k]);
                    }
                }
                buffer.Sort(System.StringComparer.Ordinal);
                projected.Add(string.Join(";", buffer));
            }
            return projected;
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

            keywords.Sort(System.StringComparer.Ordinal);
            return string.Join(";", keywords);
        }

        private static int CountNataneMaterials()
        {
            return NataneAssetIndexService.EnumerateMaterialEntries(entry => entry.isNataneShader).Count();
        }

        /// <summary>
        /// Forget the cached whitelist so the next OnProcessShader call re-reads the
        /// current material state. Called from the VRChat SDK build-requested callback.
        /// </summary>
        public static void ResetForNewBuild()
        {
            ResetBuildSessionState();
        }

        [InitializeOnLoadMethod]
        private static void ResetBuildSessionState()
        {
            initialized = false;
            whitelistedKeywordSets = null;
            whitelistedKeywordArrays = null;
            lastProcessTime = 0.0;
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
