using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;
    using static NataneUIConstants;

    /// <summary>
    /// Shader Variant Collection Tool for Natane Toon Shader
    /// Collects and manages shader variants to optimize build size and loading times
    /// </summary>
    public class ShaderVariantCollector : EditorWindow
    {
        /// <summary>
        /// Defines shader capabilities used during variant collection.
        /// </summary>
        private struct ShaderInfo
        {
            public string name;
            public bool hasForwardAdd;
            public bool hasShadowCaster;
            public bool hasMeta;
            public bool hasNormalPass;

            public ShaderInfo(string name, bool hasForwardAdd, bool hasShadowCaster, bool hasMeta = false, bool hasNormalPass = false)
            {
                this.name = name;
                this.hasForwardAdd = hasForwardAdd;
                this.hasShadowCaster = hasShadowCaster;
                this.hasMeta = hasMeta;
                this.hasNormalPass = hasNormalPass;
            }
        }

        /// <summary>
        /// Known Natane Toon shaders used during collection.
        /// ForwardBase is assumed for every listed shader.
        /// </summary>
        private static readonly ShaderInfo[] AllShaders = new ShaderInfo[]
        {
            // Standard variants (ForwardBase + ForwardAdd + ShadowCaster)
            new ShaderInfo("Natane/Toon Shader",                  true,  true),
            new ShaderInfo("Natane/Toon Shader (ScreenEdge Split)", true,  true, false, true),
            new ShaderInfo("Natane/Toon Shader (Cutout)",          true,  true),
            new ShaderInfo("Natane/Toon Shader (Lite)",            true,  true),
            new ShaderInfo("Natane/Toon Shader (Cutout Lite)",     true,  true),
            // Transparent variants (ForwardBase + ForwardAdd, no ShadowCaster)
            new ShaderInfo("Natane/Toon Shader (Transparent)",     true,  false),
            new ShaderInfo("Natane/Toon Shader (Transparent Lite)",true,  false),
            // Fur variants (ForwardBase + ForwardAdd + ShadowCaster)
            new ShaderInfo("Natane/Toon Shader (Fur)",             true,  true),
            new ShaderInfo("Natane/Toon Shader (Fur Lite)",        true,  true),
            // Background (ForwardBase + ForwardAdd + ShadowCaster + Meta)
            new ShaderInfo("Natane/Toon Shader (Background)",      true,  true, true),
            // Tessellation variants (same passes as their base variant)
            new ShaderInfo("Natane/Toon Shader (Tessellation)",    true,  true),
            new ShaderInfo("Natane/Toon Shader (Cutout Tessellation)", true, true),
            new ShaderInfo("Natane/Toon Shader (Transparent Tessellation)", true, false),
            // Wirelight (ForwardBase only)
            new ShaderInfo("Natane/Toon Shader Wirelight",         false, false),
            // Eye (ForwardBase only)
            new ShaderInfo("Natane/Eye",                           false, false),
            // Screen FX Overlay (ForwardBase only)
            // new ShaderInfo("Natane/Screen FX Overlay",             false, false),
        };

        private ShaderVariantCollection collection;
        private bool includeBasic = true;
        private bool includeAdvanced = true;
        private bool includeVirtualExpression = true;
        private bool includeAllCombinations = false;
        private bool collectFromUsedMaterialKeywords = true;

        private Vector2 scrollPosition;
        private int estimatedVariants = 0;

        [MenuItem(NataneToolMenuPaths.ShaderVariantCollector, false, 71)]
        public static void ShowWindow()
        {
            var window = GetWindow<ShaderVariantCollector>(L("Shader Variant Collector", "Shader Variant Collector"));
            window.minSize = new Vector2(WINDOW_WIDTH_STANDARD, WINDOW_HEIGHT_STANDARD);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField(L("Natane Toon Shader Variant Collector", "Natane Toon Shader Variant Collector"), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            collection = (ShaderVariantCollection)EditorGUILayout.ObjectField(
                L("Variant Collection", "Variant Collection"),
                collection,
                typeof(ShaderVariantCollection),
                false
            );

            if (collection == null)
            {
                EditorGUILayout.HelpBox(
                    L("No collection selected. Click 'Create New Collection' to create one.", "No collection selected. Click 'Create New Collection' to create one."),
                    MessageType.Warning
                );

                if (GUILayout.Button(L("Create New Collection", "Create New Collection")))
                {
                    CreateNewCollection();
                }

                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(L("Variant Options", "Variant Options"), EditorStyles.boldLabel);
            collectFromUsedMaterialKeywords = EditorGUILayout.Toggle(
                L("Collect Used Material Keywords Only", "Collect Used Material Keywords Only"),
                collectFromUsedMaterialKeywords
            );
            if (collectFromUsedMaterialKeywords)
            {
                EditorGUILayout.HelpBox(
                    L("Scan Natane Toon materials and collect only actually used keyword sets.\n" +
                    "Variants are automatically registered based on each material's shader.", "Scan Natane Toon materials and collect only actually used keyword sets.\n" +
                    "Variants are automatically registered based on each material's shader."),
                    MessageType.Info
                );
            }
            EditorGUI.BeginDisabledGroup(collectFromUsedMaterialKeywords);
            includeBasic = EditorGUILayout.Toggle(L("Include Basic Variants", "Include Basic Variants"), includeBasic);
            EditorGUILayout.HelpBox(L("Common combinations: No features, Outline only, Emission only", "Common combinations: No features, Outline only, Emission only"), MessageType.None);

            includeAdvanced = EditorGUILayout.Toggle(L("Include Advanced Variants", "Include Advanced Variants"), includeAdvanced);
            EditorGUILayout.HelpBox(L("Advanced combinations: SSS, MatCap, Specular, Rim Light", "Advanced combinations: SSS, MatCap, Specular, Rim Light"), MessageType.None);

            includeVirtualExpression = EditorGUILayout.Toggle(L("Include Virtual Expression", "Include Virtual Expression"), includeVirtualExpression);
            EditorGUILayout.HelpBox(L("Virtual expression: Dissolve, Hue Shift, Emission Animations", "Virtual expression: Dissolve, Hue Shift, Emission Animations"), MessageType.None);

            EditorGUILayout.Space();
            includeAllCombinations = EditorGUILayout.Toggle(L("Include All Combinations (WARNING)", "Include All Combinations (WARNING)"), includeAllCombinations);

            if (includeAllCombinations)
            {
                EditorGUILayout.HelpBox(
                    L("This will create thousands of variants and significantly increase build size!\n" +
                    "Only use for testing or if you need every possible combination.", "This will create thousands of variants and significantly increase build size!\n" +
                    "Only use for testing or if you need every possible combination."),
                    MessageType.Warning
                );
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            // Estimate variants
            if (collectFromUsedMaterialKeywords)
            {
                EditorGUILayout.LabelField(L("Estimated Variants: material keyword sets (auto)", "Estimated Variants: material keyword sets (auto)"), EditorStyles.boldLabel);
            }
            else
            {
                estimatedVariants = EstimateVariantCount();
                EditorGUILayout.LabelField($"{L("Estimated Variants", "Estimated Variants")}: {estimatedVariants}", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space();

            // Action Buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(L("Collect Variants", "Collect Variants"), GUILayout.Height(BUTTON_HEIGHT_LARGE)))
            {
                CollectVariants();
            }

            if (GUILayout.Button(L("Clear Collection", "Clear Collection"), GUILayout.Height(BUTTON_HEIGHT_LARGE)))
            {
                ClearCollection();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(L("Save Collection", "Save Collection"), GUILayout.Height(BUTTON_HEIGHT_LARGE)))
            {
                SaveCollection();
            }

            EditorGUILayout.Space();

            // Current Collection Info
            if (collection != null)
            {
                EditorGUILayout.LabelField(L("Current Collection Info", "Current Collection Info"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{L("Shader Count", "Shader Count")}: {collection.shaderCount}");
                EditorGUILayout.LabelField($"{L("Variant Count", "Variant Count")}: {collection.variantCount}");

                // Empty collection warning
                if (collection.variantCount == 0)
                {
                    EditorGUILayout.Space(SPACE_SMALL);
                    EditorGUILayout.HelpBox(
                        L("Collection is empty. Run 'Collect Variants' to add variants.\n" +
                        "An empty collection provides no build optimization or prewarming benefit.", "Collection is empty. Run 'Collect Variants' to add variants.\n" +
                        "An empty collection provides no build optimization or prewarming benefit."),
                        MessageType.Warning
                    );
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void CreateNewCollection()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                L("Create Shader Variant Collection", "Create Shader Variant Collection"),
                "NataneToonShaderVariants",
                "shadervariants",
                L("Choose a location to save the shader variant collection", "Choose a location to save the shader variant collection"),
                "Assets/ShaderVariants"
            );

            if (!string.IsNullOrEmpty(path))
            {
                collection = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(collection, path);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(
                    L("Success", "Success"),
                    L("Shader Variant Collection created successfully!", "Shader Variant Collection created successfully!"),
                    "OK");
            }
        }

        private int EstimateVariantCount()
        {
            int count = 0;

            if (includeBasic)
                count += 10; // Basic combinations

            if (includeAdvanced)
                count += 30; // Advanced feature combinations

            if (includeVirtualExpression)
                count += 20; // Virtual expression combinations

            if (includeAllCombinations)
                count = 8192; // 2^13 keywords (rough estimate)

            // Multiply by shader count (11 shaders ﾃ・avg 2.5 passes)
            count *= 28;

            return count;
        }

        private void CollectVariants()
        {
            if (collection == null)
            {
                EditorUtility.DisplayDialog(
                    L("Error", "Error"),
                    L("Please create or select a collection first!", "Please create or select a collection first!"),
                    "OK");
                return;
            }

            collection.Clear();

            // Find all shaders
            var foundShaders = new List<(Shader shader, ShaderInfo info)>();
            int missingCount = 0;

            foreach (var info in AllShaders)
            {
                Shader shader = Shader.Find(info.name);
                if (shader != null)
                {
                    foundShaders.Add((shader, info));
                }
                else
                {
                    missingCount++;
                    Debug.LogWarning($"[Natane Variant Collector] Shader not found: {info.name}");
                }
            }

            if (foundShaders.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    L("Error", "Error"),
                    L("Could not find any Natane Toon Shaders!", "Could not find any Natane Toon Shaders!"),
                    "OK");
                return;
            }

            int totalVariants = 0;

            if (collectFromUsedMaterialKeywords)
            {
                // Per-shader material keyword collection
                totalVariants = CollectVariantsFromMaterials(foundShaders);
            }
            else
            {
                // Manual keyword combination mode
                List<string[]> variantCombinations = GenerateVariantCombinations();

                foreach (string[] keywords in variantCombinations)
                {
                    foreach (var (shader, info) in foundShaders)
                    {
                        // ForwardBase for all shaders
                        AddVariant(shader, PassType.ForwardBase, keywords);
                        totalVariants++;

                        if (info.hasForwardAdd)
                        {
                            AddVariant(shader, PassType.ForwardAdd, keywords);
                            totalVariants++;
                        }
                        if (info.hasNormalPass)
                        {
                            AddVariant(shader, PassType.Normal, keywords);
                            totalVariants++;
                        }
                    }
                }

                // Add ShadowCaster and Meta passes with empty keywords
                foreach (var (shader, info) in foundShaders)
                {
                    if (info.hasShadowCaster)
                    {
                        AddVariant(shader, PassType.ShadowCaster, new string[] { });
                        totalVariants++;
                    }
                    if (info.hasNormalPass)
                    {
                        AddVariant(shader, PassType.Normal, new string[] { });
                        totalVariants++;
                    }
                    if (info.hasMeta)
                    {
                        AddVariant(shader, PassType.Meta, new string[] { });
                        totalVariants++;
                    }
                }
            }

            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            string details = $"{L("Shader Count", "Shader Count")}: {collection.shaderCount}\n" +
                             $"{L("Variant Count", "Variant Count")}: {collection.variantCount}";

            if (missingCount > 0)
            {
                details += $"\n{L("Missing Shaders", "Missing Shaders")}: {missingCount}";
            }

            EditorUtility.DisplayDialog(
                L("Success", "Success"),
                L($"Collected {totalVariants} shader variants!", $"Collected {totalVariants} shader variants!") + "\n" + details,
                "OK"
            );
        }

        /// <summary>
        /// Collect variants from project materials with per-shader keyword mapping.
        /// Collect variants from project materials that use Natane Toon shaders.
        /// </summary>
        private int CollectVariantsFromMaterials(List<(Shader shader, ShaderInfo info)> foundShaders)
        {
            // Build shader lookup
            var shaderLookup = new Dictionary<string, (Shader shader, ShaderInfo info)>();
            foreach (var entry in foundShaders)
            {
                shaderLookup[entry.info.name] = entry;
            }

            // Collect per-shader keyword sets
            var perShaderKeywordSets = new Dictionary<string, HashSet<string>>();
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");

            try
            {
                for (int i = 0; i < materialGuids.Length; i++)
                {
                    if (i % 50 == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            L("Material Scan", "Material Scan"),
                            L($"Scanning materials... ({i}/{materialGuids.Length})", $"Scanning materials... ({i}/{materialGuids.Length})"),
                            (float)i / materialGuids.Length);
                    }

                    string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null)
                        continue;

                    string shaderName = material.shader.name;
                    if (!IsNataneToonShader(shaderName))
                        continue;

                    string[] keywords = material.shaderKeywords
                        .Where(keyword => !string.IsNullOrEmpty(keyword))
                        .Distinct()
                        .OrderBy(keyword => keyword)
                        .ToArray();

                    string key = shaderName + ":" + string.Join(";", keywords);

                    if (!perShaderKeywordSets.ContainsKey(shaderName))
                    {
                        perShaderKeywordSets[shaderName] = new HashSet<string>();
                    }

                    perShaderKeywordSets[shaderName].Add(string.Join(";", keywords));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            int totalVariants = 0;

            foreach (var (shader, info) in foundShaders)
            {
                // Always add base variant (empty keywords)
                var keywordSets = new List<string[]> { new string[] { } };

                // Add material-collected keyword sets
                if (perShaderKeywordSets.ContainsKey(info.name))
                {
                    foreach (string keySet in perShaderKeywordSets[info.name])
                    {
                        if (!string.IsNullOrEmpty(keySet))
                        {
                            keywordSets.Add(keySet.Split(';'));
                        }
                    }
                }

                // Register variants for this shader
                foreach (string[] keywords in keywordSets)
                {
                    AddVariant(shader, PassType.ForwardBase, keywords);
                    totalVariants++;

                    if (info.hasForwardAdd)
                    {
                        AddVariant(shader, PassType.ForwardAdd, keywords);
                        totalVariants++;
                    }
                    if (info.hasNormalPass)
                    {
                        AddVariant(shader, PassType.Normal, keywords);
                        totalVariants++;
                    }
                }

                // ShadowCaster and Meta with empty keywords
                if (info.hasShadowCaster)
                {
                    AddVariant(shader, PassType.ShadowCaster, new string[] { });
                    totalVariants++;
                }
                if (info.hasNormalPass)
                {
                    AddVariant(shader, PassType.Normal, new string[] { });
                    totalVariants++;
                }
                if (info.hasMeta)
                {
                    AddVariant(shader, PassType.Meta, new string[] { });
                    totalVariants++;
                }
            }

            return totalVariants;
        }

        /// <summary>
        /// Collect keyword sets from all Natane Toon materials in the project.
        /// Also used by ShaderPrewarmingEditor for auto-collection.
        /// Also reused by ShaderPrewarmingEditor during auto-collection.
        /// </summary>
        public List<string[]> CollectKeywordSetsFromProjectMaterials()
        {
            var combinations = new List<string[]>();
            var uniqueSets = new HashSet<string>();
            string[] materialGuids = AssetDatabase.FindAssets("t:Material");

            try
            {
                for (int i = 0; i < materialGuids.Length; i++)
                {
                    // Show progress bar every 50 materials
                    if (i % 50 == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            L("Material Scan", "Material Scan"),
                            L($"Scanning materials... ({i}/{materialGuids.Length})", $"Scanning materials... ({i}/{materialGuids.Length})"),
                            (float)i / materialGuids.Length);
                    }

                    string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null)
                    {
                        continue;
                    }

                    if (!IsNataneToonShader(material.shader.name))
                    {
                        continue;
                    }

                    string[] keywords = material.shaderKeywords
                        .Where(keyword => !string.IsNullOrEmpty(keyword))
                        .Distinct()
                        .OrderBy(keyword => keyword)
                        .ToArray();

                    string key = string.Join(";", keywords);
                    if (uniqueSets.Add(key))
                    {
                        combinations.Add(keywords);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Always include the base variant.
            if (uniqueSets.Add(string.Empty))
            {
                combinations.Insert(0, new string[] { });
            }

            return combinations;
        }

        /// <summary>
        /// Check if a shader name belongs to the Natane Toon shader family.
        /// Returns true when the shader name belongs to the Natane Toon family.
        /// </summary>
        public static bool IsNataneToonShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
                return false;

            return shaderName == "Natane/Toon Shader" ||
                   shaderName == "Natane/Toon Shader (ScreenEdge Split)" ||
                   shaderName == "Natane/Toon Shader (Cutout)" ||
                   shaderName == "Natane/Toon Shader (Transparent)" ||
                   shaderName == "Natane/Toon Shader (Lite)" ||
                   shaderName == "Natane/Toon Shader (Cutout Lite)" ||
                   shaderName == "Natane/Toon Shader (Transparent Lite)" ||
                   shaderName == "Natane/Toon Shader (Fur)" ||
                   shaderName == "Natane/Toon Shader (Fur Lite)" ||
                   shaderName == "Natane/Toon Shader (Background)" ||
                   shaderName == "Natane/Toon Shader (Tessellation)" ||
                   shaderName == "Natane/Toon Shader (Cutout Tessellation)" ||
                   shaderName == "Natane/Toon Shader (Transparent Tessellation)" ||
                   shaderName == "Natane/Toon Shader Wirelight" ||
                   shaderName == "Natane/Eye" ||
                   false;
        }

        private List<string[]> GenerateVariantCombinations()
        {
            List<string[]> combinations = new List<string[]>();

            if (includeAllCombinations)
            {
                // Generate all possible combinations (warning: huge!)
                combinations.AddRange(GenerateAllCombinations());
            }
            else
            {
                // Generate curated combinations
                if (includeBasic)
                {
                    combinations.AddRange(GenerateBasicVariants());
                }

                if (includeAdvanced)
                {
                    combinations.AddRange(GenerateAdvancedVariants());
                }

                if (includeVirtualExpression)
                {
                    combinations.AddRange(GenerateVirtualExpressionVariants());
                }
            }

            return combinations;
        }

        private List<string[]> GenerateBasicVariants()
        {
            return new List<string[]>
            {
                new string[] { }, // No keywords (base variant)
                new string[] { "_NORMALMAP" },
                new string[] { "_EMISSION" },
                new string[] { "_EMISSION", "_NORMALMAP" },
                new string[] { "_USE_RAMP" },
                new string[] { "_USE_RAMP", "_NORMALMAP" },
                new string[] { "_USE_DITHERING" },
                new string[] { "_USE_DITHERING", "_NORMALMAP" },
                new string[] { "_SCREEN_TONE" },
                new string[] { "_SCREEN_TONE", "_NORMALMAP" },
            };
        }

        private List<string[]> GenerateAdvancedVariants()
        {
            return new List<string[]>
            {
                new string[] { "_SPECULAR" },
                new string[] { "_SPECULAR", "_NORMALMAP" },
                new string[] { "_HAIR_SPECULAR" },
                new string[] { "_HAIR_SPECULAR", "_NORMALMAP" },
                new string[] { "_RIM_LIGHT" },
                new string[] { "_RIM_LIGHT", "_NORMALMAP" },
                new string[] { "_MATCAP" },
                new string[] { "_MATCAP", "_NORMALMAP" },
                new string[] { "_SSS" },
                new string[] { "_SSS", "_NORMALMAP" },
                new string[] { "_SPECULAR", "_RIM_LIGHT", "_NORMALMAP" },
                new string[] { "_EMISSION", "_MATCAP", "_NORMALMAP" },
                new string[] { "_SPECULAR", "_HAIR_SPECULAR", "_NORMALMAP" },
                new string[] { "_SPECULAR", "_SSS", "_NORMALMAP" },
                new string[] { "_GLITTER" },
                new string[] { "_GLITTER", "_NORMALMAP" },
            };
        }

        private List<string[]> GenerateVirtualExpressionVariants()
        {
            return new List<string[]>
            {
                new string[] { "_DISSOLVE" },
                new string[] { "_DISSOLVE", "_NORMALMAP" },
                new string[] { "_HUE_SHIFT" },
                new string[] { "_HUE_SHIFT", "_NORMALMAP" },
                new string[] { "_DISSOLVE", "_HUE_SHIFT" },
                new string[] { "_EMISSION" },
                new string[] { "_EMISSION", "_NORMALMAP" },
                new string[] { "_DISSOLVE", "_EMISSION" },
                new string[] { "_HUE_SHIFT", "_EMISSION" },
                new string[] { "_HOLOGRAM" },
                new string[] { "_GLITCH" },
                new string[] { "_DITHERING_ALPHA" },
                new string[] { "_HASHED_ALPHA" },
                new string[] { "_DISTANCE_FADE" },
                new string[] { "_HEIGHT_FADE" },
            };
        }

        private List<string[]> GenerateAllCombinations()
        {
            // WARNING: This generates thousands of combinations!
            List<string[]> combinations = new List<string[]>();

            string[] allKeywords = new string[]
            {
                "_USE_RAMP", "_SPECULAR", "_HAIR_SPECULAR", "_RIM_LIGHT", "_SSS",
                "_MATCAP", "_EMISSION", "_GLITTER",
                "_NORMALMAP", "_DISSOLVE", "_HUE_SHIFT"
            };

            // Generate all possible combinations (2^11 = 2048 combinations)
            int totalCombinations = 1 << allKeywords.Length;

            for (int i = 0; i < totalCombinations; i++)
            {
                List<string> keywords = new List<string>();

                for (int j = 0; j < allKeywords.Length; j++)
                {
                    if ((i & (1 << j)) != 0)
                    {
                        keywords.Add(allKeywords[j]);
                    }
                }

                combinations.Add(keywords.ToArray());
            }

            return combinations;
        }

        private void AddVariant(Shader shader, PassType passType, string[] keywords)
        {
            ShaderVariantCollection.ShaderVariant variant = new ShaderVariantCollection.ShaderVariant
            {
                shader = shader,
                passType = passType,
                keywords = keywords
            };

            if (!collection.Contains(variant))
            {
                collection.Add(variant);
            }
        }

        private void ClearCollection()
        {
            if (collection != null)
            {
                if (EditorUtility.DisplayDialog(
                    L("Clear Collection", "Clear Collection"),
                    L("Are you sure you want to clear all variants from this collection?", "Are you sure you want to clear all variants from this collection?"),
                    L("Yes", "Yes"),
                    L("No", "No")))
                {
                    collection.Clear();
                    EditorUtility.SetDirty(collection);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog(
                        L("Success", "Success"),
                        L("Collection cleared!", "Collection cleared!"),
                        "OK");
                }
            }
        }

        private void SaveCollection()
        {
            if (collection != null)
            {
                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(
                    L("Success", "Success"),
                    L("Collection saved!", "Collection saved!"),
                    "OK");
            }
        }
    }
}
