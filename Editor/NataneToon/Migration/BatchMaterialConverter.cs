using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    using static NataneToonLocalization;

    /// <summary>
    /// Batch converts materials that match a source shader name.
    /// </summary>
    public class BatchMaterialConverter : EditorWindow
    {
        private const float CompactLayoutWidth = 720f;
        private const int ResultsPerPage = 100;

        private string sourceShaderName = "lilToon";
        private string targetShaderPath = "Natane/Toon Shader";
        private bool searchInScenes = true;
        private bool searchInPrefabs = true;
        private bool updateReferences = true;
        private Vector2 windowScrollPosition;
        private Vector2 scrollPosition;
        private int currentPage;
        private readonly List<MaterialConversionInfo> conversionInfos = new List<MaterialConversionInfo>();

        private sealed class MaterialConversionInfo
        {
            public MaterialIndexEntry materialEntry;
            public Material cachedMaterial;
            public int affectedObjectCount;
            public bool willConvert = true;

            public Material LoadMaterial()
            {
                if (cachedMaterial == null && materialEntry != null)
                {
                    cachedMaterial = NataneAssetIndexService.LoadMaterial(materialEntry);
                }

                return cachedMaterial;
            }
        }

        [MenuItem("Tools/Natane/Migration/一括マテリアル変換 Batch Material Converter", false, 53)]
        public static void ShowWindow()
        {
            var window = GetWindow<BatchMaterialConverter>(L("一括マテリアル変換", "Batch Material Converter"));
            window.minSize = new Vector2(600, 500);
            window.Show();
        }

        private void OnGUI()
        {
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
            NataneToonShaderGUIUtility.DrawToolHeader("一括マテリアル変換", "Batch Material Converter", nameof(BatchMaterialConverter));
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                L("このツールは、プロジェクト全体から対象マテリアルを見つけて別シェーダーへ一括変換します。\n" +
                  "プレハブ依存はインデックス済みアセットグラフから解決し、全件再スキャンを避けます。",
                  "This tool finds and converts materials from one shader to another across your project.\n" +
                  "Prefab dependencies are resolved from the indexed asset graph to avoid full-project rescans."),
                MessageType.Info);

            EditorGUILayout.Space();
            DrawSettings();

            if (GUILayout.Button(L("プロジェクトをスキャン", "Scan Project"), GUILayout.Height(30)))
            {
                ScanProject();
            }

            EditorGUILayout.Space();

            if (conversionInfos.Count > 0)
            {
                DrawResults();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L("変換設定", "Conversion Settings"), EditorStyles.boldLabel);

            sourceShaderName = EditorGUILayout.TextField(L("変換元シェーダー名", "Source Shader Name"), sourceShaderName);
            targetShaderPath = EditorGUILayout.TextField(L("変換先シェーダー", "Target Shader"), targetShaderPath);

            EditorGUILayout.Space();

            searchInScenes = EditorGUILayout.Toggle(L("シーンを検索", "Search in Scenes"), searchInScenes);
            searchInPrefabs = EditorGUILayout.Toggle(L("プレハブを検索", "Search in Prefabs"), searchInPrefabs);
            updateReferences = EditorGUILayout.Toggle(L("オブジェクト参照を更新", "Update Object References"), updateReferences);

            if (updateReferences)
            {
                EditorGUILayout.HelpBox(
                    L("このツールはマテリアルをその場で変換するため、オブジェクト参照は維持されます。"
                      + " このトグルは既存ワークフロー互換のため残しています。",
                      "This tool converts materials in place, so object references stay valid. " +
                      "The toggle is kept for workflow compatibility."),
                    MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField(L($"{conversionInfos.Count} 個のマテリアルが見つかりました", $"Found {conversionInfos.Count} materials"), EditorStyles.boldLabel);

            int totalPages = Mathf.Max(1, Mathf.CeilToInt(conversionInfos.Count / (float)ResultsPerPage));
            currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);
            DrawPageControls(totalPages);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(GetAdaptiveListHeight(160f, 320f, 0.35f)));

            IEnumerable<MaterialConversionInfo> pageItems = conversionInfos
                .Skip(currentPage * ResultsPerPage)
                .Take(ResultsPerPage);

            foreach (MaterialConversionInfo info in pageItems)
            {
                Material material = info.LoadMaterial();

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                info.willConvert = EditorGUILayout.Toggle(info.willConvert, GUILayout.Width(20));
                EditorGUILayout.ObjectField(material, typeof(Material), false);
                EditorGUILayout.EndHorizontal();

                if (info.affectedObjectCount > 0)
                {
                    EditorGUILayout.LabelField(L($"{info.affectedObjectCount} 個のオブジェクトで使用", $"Used by {info.affectedObjectCount} objects"), EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            int selectedCount = conversionInfos.Count(info => info.willConvert);
            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button(L($"選択中の {selectedCount} 個を変換", $"Convert {selectedCount} Selected Materials"), GUILayout.Height(40)))
            {
                ConvertSelectedMaterials();
            }

            GUI.enabled = true;
            EditorGUILayout.Space();

            if (IsCompactLayout())
            {
                if (GUILayout.Button(L("すべて選択", "Select All")))
                {
                    conversionInfos.ForEach(info => info.willConvert = true);
                }

                if (GUILayout.Button(L("すべて解除", "Deselect All")))
                {
                    conversionInfos.ForEach(info => info.willConvert = false);
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(L("すべて選択", "Select All")))
                {
                    conversionInfos.ForEach(info => info.willConvert = true);
                }

                if (GUILayout.Button(L("すべて解除", "Deselect All")))
                {
                    conversionInfos.ForEach(info => info.willConvert = false);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPageControls(int totalPages)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = currentPage > 0;
            if (GUILayout.Button("<", GUILayout.Width(32)))
            {
                currentPage--;
            }

            GUI.enabled = currentPage < totalPages - 1;
            if (GUILayout.Button(">", GUILayout.Width(32)))
            {
                currentPage++;
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{currentPage + 1}/{totalPages}", GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
        }

        private static bool MatchesShaderName(string shaderName, string source)
        {
            if (string.IsNullOrEmpty(shaderName) || string.IsNullOrEmpty(source))
            {
                return false;
            }

            if (string.Equals(shaderName, source, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (shaderName.StartsWith(source + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (shaderName.EndsWith("/" + source, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return shaderName.IndexOf("/" + source + "/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ScanProject()
        {
            conversionInfos.Clear();
            currentPage = 0;

            List<MaterialIndexEntry> matchingMaterials = NataneAssetIndexService
                .EnumerateMaterialEntries(entry => MatchesShaderName(entry.shaderName, sourceShaderName))
                .OrderBy(entry => entry.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matchingGuids = new HashSet<string>(matchingMaterials.Select(entry => entry.guid), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> affectedObjectCounts = BuildAffectedObjectCounts(matchingGuids);

            for (int i = 0; i < matchingMaterials.Count; i++)
            {
                MaterialIndexEntry entry = matchingMaterials[i];
                conversionInfos.Add(new MaterialConversionInfo
                {
                    materialEntry = entry,
                    affectedObjectCount = affectedObjectCounts.TryGetValue(entry.guid, out int count) ? count : 0
                });
            }

            Debug.Log($"[Batch Material Converter] Scan complete. Found {conversionInfos.Count} matching materials.");
        }

        private Dictionary<string, int> BuildAffectedObjectCounts(HashSet<string> matchingMaterialGuids)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (matchingMaterialGuids == null || matchingMaterialGuids.Count == 0)
            {
                return counts;
            }

            if (searchInPrefabs)
            {
                List<PrefabDependencyEntry> prefabs = NataneAssetIndexService.GetPrefabsUsingMaterialGuids(matchingMaterialGuids);
                for (int i = 0; i < prefabs.Count; i++)
                {
                    PrefabDependencyEntry prefab = prefabs[i];
                    for (int j = 0; j < prefab.materialGuids.Count; j++)
                    {
                        string materialGuid = prefab.materialGuids[j];
                        if (!matchingMaterialGuids.Contains(materialGuid))
                        {
                            continue;
                        }

                        counts[materialGuid] = counts.TryGetValue(materialGuid, out int count) ? count + 1 : 1;
                    }
                }
            }

            if (searchInScenes)
            {
                Renderer[] renderers = NataneEditorCompat.FindObjectsOfTypeCompat<Renderer>(true);
                var sceneObjectIdsByMaterialGuid = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    Material[] sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials == null || sharedMaterials.Length == 0)
                    {
                        continue;
                    }

                    var rendererMaterialGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int j = 0; j < sharedMaterials.Length; j++)
                    {
                        Material material = sharedMaterials[j];
                        if (material == null)
                        {
                            continue;
                        }

                        string materialPath = AssetDatabase.GetAssetPath(material);
                        string materialGuid = AssetDatabase.AssetPathToGUID(materialPath);
                        if (!string.IsNullOrEmpty(materialGuid) && matchingMaterialGuids.Contains(materialGuid))
                        {
                            rendererMaterialGuids.Add(materialGuid);
                        }
                    }

                    foreach (string materialGuid in rendererMaterialGuids)
                    {
                        if (!sceneObjectIdsByMaterialGuid.TryGetValue(materialGuid, out HashSet<int> ids))
                        {
                            ids = new HashSet<int>();
                            sceneObjectIdsByMaterialGuid.Add(materialGuid, ids);
                        }

                        ids.Add(renderer.gameObject.GetInstanceID());
                    }
                }

                foreach (KeyValuePair<string, HashSet<int>> pair in sceneObjectIdsByMaterialGuid)
                {
                    counts[pair.Key] = counts.TryGetValue(pair.Key, out int count) ? count + pair.Value.Count : pair.Value.Count;
                }
            }

            return counts;
        }

        private void ConvertSelectedMaterials()
        {
            List<MaterialConversionInfo> selectedInfos = conversionInfos.Where(info => info.willConvert).ToList();
            if (selectedInfos.Count == 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                L("マテリアル変換", "Convert Materials"),
                L($"{selectedInfos.Count} 個のマテリアルを変換しますか？\n" +
                  "Unity の Undo で変更は戻せますが、プロジェクトのバックアップも推奨します。",
                  $"Are you sure you want to convert {selectedInfos.Count} materials?\n" +
                  "You can revert the material changes with Unity Undo, but a project backup is still recommended."),
                L("変換", "Convert"),
                L("キャンセル", "Cancel")))
            {
                return;
            }

            Shader targetShader = Shader.Find(targetShaderPath);
            if (targetShader == null)
            {
                EditorUtility.DisplayDialog(
                    L("エラー", "Error"),
                    L($"変換先シェーダー '{targetShaderPath}' が見つかりません。", $"Target shader '{targetShaderPath}' not found!"),
                    "OK");
                return;
            }

            int successCount = 0;

            try
            {
                for (int i = 0; i < selectedInfos.Count; i++)
                {
                    MaterialConversionInfo info = selectedInfos[i];
                    Material material = info.LoadMaterial();
                    if (material == null)
                    {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        L("マテリアル変換中", "Converting Materials"),
                        L($"{i + 1}/{selectedInfos.Count} を変換中: {material.name}", $"Converting {i + 1}/{selectedInfos.Count}: {material.name}"),
                        (float)i / selectedInfos.Count);

                    try
                    {
                        Undo.RecordObject(material, "Convert Material Shader");
                        Dictionary<string, MaterialPropertyData> originalProps = CaptureAllProperties(material);
                        material.shader = targetShader;
                        RestoreCompatibleProperties(material, originalProps);
                        EditorUtility.SetDirty(material);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Batch Material Converter] Failed to convert {material.name}: {ex.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                L("変換完了", "Conversion Complete"),
                L($"{successCount}/{selectedInfos.Count} 個のマテリアルを正常に変換しました。", $"Successfully converted {successCount}/{selectedInfos.Count} materials."),
                "OK");

            ScanProject();
        }

        private Dictionary<string, MaterialPropertyData> CaptureAllProperties(Material material)
        {
            var properties = new Dictionary<string, MaterialPropertyData>();
            Shader shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);

            for (int i = 0; i < propertyCount; i++)
            {
                string propName = ShaderUtil.GetPropertyName(shader, i);
                ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);
                var property = new MaterialPropertyData
                {
                    type = propType
                };

                try
                {
                    switch (propType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            property.colorValue = material.GetColor(propName);
                            break;
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            property.floatValue = material.GetFloat(propName);
                            break;
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            property.textureValue = material.GetTexture(propName);
                            break;
                        case ShaderUtil.ShaderPropertyType.Vector:
                            property.vectorValue = material.GetVector(propName);
                            break;
                    }

                    properties[propName] = property;
                }
                catch
                {
                    // Ignore properties that cannot be read on this shader.
                }
            }

            return properties;
        }

        private void RestoreCompatibleProperties(Material material, Dictionary<string, MaterialPropertyData> originalProps)
        {
            Shader shader = material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);

            for (int i = 0; i < propertyCount; i++)
            {
                string propName = ShaderUtil.GetPropertyName(shader, i);
                if (!originalProps.TryGetValue(propName, out MaterialPropertyData originalProp))
                {
                    continue;
                }

                try
                {
                    switch (originalProp.type)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            material.SetColor(propName, originalProp.colorValue);
                            break;
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            material.SetFloat(propName, originalProp.floatValue);
                            break;
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            if (originalProp.textureValue != null)
                            {
                                material.SetTexture(propName, originalProp.textureValue);
                            }

                            break;
                        case ShaderUtil.ShaderPropertyType.Vector:
                            material.SetVector(propName, originalProp.vectorValue);
                            break;
                    }
                }
                catch
                {
                    // Ignore incompatible properties between the source and target shaders.
                }
            }
        }

        private bool IsCompactLayout()
        {
            return position.width < CompactLayoutWidth;
        }

        private float GetAdaptiveListHeight(float minHeight, float maxHeight, float ratio)
        {
            return Mathf.Clamp(position.height * ratio, minHeight, maxHeight);
        }

        private sealed class MaterialPropertyData
        {
            public ShaderUtil.ShaderPropertyType type;
            public Color colorValue;
            public float floatValue;
            public Texture textureValue;
            public Vector4 vectorValue;
        }
    }
}
