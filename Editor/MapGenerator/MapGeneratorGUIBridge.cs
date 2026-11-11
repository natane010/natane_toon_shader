using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bridge class for calling MapGeneratorEngine from NataneToon ShaderGUI.
/// Provides material-centric generation methods that auto-detect mesh/albedo from the scene.
/// </summary>
public static class MapGeneratorGUIBridge
{
    private static MapGenSettings _defaultSettings = new MapGenSettings();

    public static bool CanGenerateNormalForMaterial(Material material)
    {
        if (material == null)
            return false;

        if (GetAlbedo(material) != null)
            return true;

        TryGetMeshAndRenderer(material, out Mesh mesh, out Renderer renderer, false);
        return mesh != null;
    }

    /// <summary>
    /// Generate Normal map from material's _MainTex and assign to _BumpMap.
    /// </summary>
    public static void GenerateNormalForMaterial(Material material)
    {
        if (material == null) return;

        Texture2D albedo = GetAlbedo(material);
        TryGetMeshAndRenderer(material, out Mesh mesh, out Renderer renderer, false);

        if (albedo == null && mesh == null)
        {
            EditorUtility.DisplayDialog(MapGenL.L("マップジェネレーター", "Map Generator"),
                MapGenL.L("Albedo テクスチャ (_MainTex) またはシーン内のメッシュが必要です。",
                           "Albedo texture (_MainTex) or a mesh in the scene is required."), "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(MapGenL.L("マップジェネレーター", "Map Generator"),
                MapGenL.L("Normal Map を生成中...", "Generating Normal Map..."), 0.3f);
            Texture2D result = MapGeneratorEngine.GenerateNormalMap(albedo, _defaultSettings, mesh, renderer);
            if (result == null)
            {
                EditorUtility.DisplayDialog(MapGenL.L("マップジェネレーター", "Map Generator"),
                    MapGenL.L("Normal Map の生成に失敗しました。", "Failed to generate Normal Map."), "OK");
                return;
            }

            string path = SaveForMaterial(material, result, "Normal", true);
            AssignToMaterial(material, path, "_BumpMap", "_UseNormalMap", "_NORMALMAP", "_USE_NORMAL_MAP");
            Object.DestroyImmediate(result);

            EditorUtility.DisplayDialog(MapGenL.L("完了", "Done"),
                MapGenL.L($"Normal Map を生成して _BumpMap に適用しました。\n保存先: {path}",
                           $"Generated Normal Map and assigned to _BumpMap.\nSaved to: {path}"),
                MapGenL.L("閉じる", "Close"));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Generate Shadow map from mesh and assign to _ShadowReceiveMask.
    /// </summary>
    public static void GenerateShadowForMaterial(Material material)
    {
        if (material == null) return;

        if (!TryGetMeshAndRenderer(material, out Mesh mesh, out Renderer renderer))
            return;

        Texture2D result = MapGeneratorEngine.GenerateShadowMap(mesh, renderer, _defaultSettings);
        if (result == null)
        {
            EditorUtility.DisplayDialog(MapGenL.L("マップジェネレーター", "Map Generator"),
                MapGenL.L("Shadow Map の生成に失敗しました。", "Failed to generate Shadow Map."), "OK");
            return;
        }

        string path = SaveForMaterial(material, result, "Shadow", false);
        AssignToMaterial(material, path, "_ShadowReceiveMask");
        Object.DestroyImmediate(result);

        EditorUtility.DisplayDialog(MapGenL.L("完了", "Done"),
            MapGenL.L($"Shadow Map を生成しました。\n保存先: {path}",
                       $"Generated Shadow Map.\nSaved to: {path}"),
            MapGenL.L("閉じる", "Close"));
    }

    /// <summary>
    /// Generate Roughness map from material's _MainTex and assign to _RoughnessMap.
    /// </summary>
    public static void GenerateRoughnessForMaterial(Material material)
    {
        if (material == null) return;

        Texture2D albedo = GetAlbedo(material);
        if (albedo == null)
        {
            EditorUtility.DisplayDialog(MapGenL.L("マップジェネレーター", "Map Generator"),
                MapGenL.L("Albedo テクスチャ (_MainTex) が設定されていません。",
                           "Albedo texture (_MainTex) is not set."), "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar(MapGenL.L("マップジェネレーター", "Map Generator"),
                MapGenL.L("Roughness Map を生成中...", "Generating Roughness Map..."), 0.3f);
            Texture2D result = MapGeneratorEngine.GenerateRoughnessMap(albedo, _defaultSettings);
            if (result == null)
            {
                EditorUtility.DisplayDialog(MapGenL.L("マップジェネレーター", "Map Generator"),
                    MapGenL.L("Roughness Map の生成に失敗しました。", "Failed to generate Roughness Map."), "OK");
                return;
            }

            string path = SaveForMaterial(material, result, "Roughness", false);
            AssignToMaterial(material, path, "_RoughnessMap", "_UseRoughnessMap", "_USE_ROUGHNESS_MAP");
            Object.DestroyImmediate(result);

            EditorUtility.DisplayDialog(MapGenL.L("完了", "Done"),
                MapGenL.L($"Roughness Map を生成して _RoughnessMap に適用しました。\n保存先: {path}",
                           $"Generated Roughness Map and assigned to _RoughnessMap.\nSaved to: {path}"),
                MapGenL.L("閉じる", "Close"));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Generate all maps at once and assign to appropriate NataneToon properties.
    /// </summary>
    public static void GenerateAllForMaterial(Material material)
    {
        if (material == null) return;

        TryGetMeshAndRenderer(material, out Mesh mesh, out Renderer renderer, false);
        Texture2D albedo = GetAlbedo(material);

        if (mesh == null && albedo == null)
        {
            EditorUtility.DisplayDialog(MapGenL.L("マップジェネレーター", "Map Generator"),
                MapGenL.L("メッシュも Albedo テクスチャも見つかりません。\nシーンにオブジェクトを配置し、_MainTex を設定してください。",
                           "Neither mesh nor Albedo texture found.\nPlace an object in the scene and set _MainTex."), "OK");
            return;
        }

        string folder = GetOutputFolder(material);
        string baseName = SanitizeFileName(material.name);
        int generated = 0;

        try
        {
            Undo.RecordObject(material, "MapGenerator: Generate All Maps");

            // Normal (mesh geometry + albedo detail)
            if (albedo != null || mesh != null)
            {
                EditorUtility.DisplayProgressBar(MapGenL.L("マップジェネレーター", "Map Generator"),
                    MapGenL.L("Normal Map を生成中...", "Generating Normal Map..."), 0.0f);
                Texture2D normal = MapGeneratorEngine.GenerateNormalMap(albedo, _defaultSettings, mesh, renderer);
                if (normal != null)
                {
                    string path = MapGeneratorEngine.SaveTexture(normal, folder, $"{baseName}_Normal",
                        _defaultSettings.outputFormat, true);
                    AssignToMaterial(material, path, "_BumpMap", "_UseNormalMap", "_NORMALMAP", "_USE_NORMAL_MAP");
                    Object.DestroyImmediate(normal);
                    generated++;
                }
            }

            // AO
            if (mesh != null && renderer != null)
            {
                EditorUtility.DisplayProgressBar(MapGenL.L("マップジェネレーター", "Map Generator"),
                    MapGenL.L("AO Map を生成中...", "Generating AO Map..."), 0.15f);
                Texture2D ao = MapGeneratorEngine.GenerateAOMap(mesh, renderer, _defaultSettings);
                if (ao != null)
                {
                    string path = MapGeneratorEngine.SaveTexture(ao, folder, $"{baseName}_AO",
                        _defaultSettings.outputFormat, false);
                    AssignToMaterial(material, path, "_AOMap", "_UseAO", "_USE_AO");
                    Object.DestroyImmediate(ao);
                    generated++;
                }
            }

            // Curvature → _CavityMap
            if (mesh != null)
            {
                EditorUtility.DisplayProgressBar(MapGenL.L("マップジェネレーター", "Map Generator"),
                    MapGenL.L("Curvature Map を生成中...", "Generating Curvature Map..."), 0.4f);
                Texture2D curvature = MapGeneratorEngine.GenerateCurvatureMap(mesh, _defaultSettings);
                if (curvature != null)
                {
                    string path = MapGeneratorEngine.SaveTexture(curvature, folder, $"{baseName}_Curvature",
                        _defaultSettings.outputFormat, false);
                    AssignToMaterial(material, path, "_CavityMap");
                    Object.DestroyImmediate(curvature);
                    generated++;
                }
            }

            // Roughness
            if (albedo != null)
            {
                EditorUtility.DisplayProgressBar(MapGenL.L("マップジェネレーター", "Map Generator"),
                    MapGenL.L("Roughness Map を生成中...", "Generating Roughness Map..."), 0.6f);
                Texture2D roughness = MapGeneratorEngine.GenerateRoughnessMap(albedo, _defaultSettings);
                if (roughness != null)
                {
                    string path = MapGeneratorEngine.SaveTexture(roughness, folder, $"{baseName}_Roughness",
                        _defaultSettings.outputFormat, false);
                    AssignToMaterial(material, path, "_RoughnessMap", "_UseRoughnessMap", "_USE_ROUGHNESS_MAP");
                    Object.DestroyImmediate(roughness);
                    generated++;
                }
            }

            // Shadow
            if (mesh != null && renderer != null)
            {
                EditorUtility.DisplayProgressBar(MapGenL.L("マップジェネレーター", "Map Generator"),
                    MapGenL.L("Shadow Map を生成中...", "Generating Shadow Map..."), 0.8f);
                Texture2D shadow = MapGeneratorEngine.GenerateShadowMap(mesh, renderer, _defaultSettings);
                if (shadow != null)
                {
                    string path = MapGeneratorEngine.SaveTexture(shadow, folder, $"{baseName}_Shadow",
                        _defaultSettings.outputFormat, false);
                    AssignToMaterial(material, path, "_ShadowReceiveMask");
                    Object.DestroyImmediate(shadow);
                    generated++;
                }
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(MapGenL.L("完了", "Done"),
                MapGenL.L($"全マップ生成完了！\n生成数: {generated} マップ\n保存先: {folder}",
                           $"All maps generated!\nCount: {generated} maps\nOutput: {folder}"),
                MapGenL.L("閉じる", "Close"));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ===== Helpers =====

    private static Texture2D GetAlbedo(Material mat)
    {
        if (mat.HasProperty("_MainTex"))
            return mat.GetTexture("_MainTex") as Texture2D;
        return null;
    }

    private static bool TryGetMeshAndRenderer(Material material, out Mesh mesh, out Renderer renderer,
        bool showDialog = true)
    {
        mesh = null;
        renderer = null;

#if UNITY_2022_2_OR_NEWER
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
        var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
#endif
        foreach (var r in renderers)
        {
            if (r.sharedMaterials == null) continue;
            foreach (var m in r.sharedMaterials)
            {
                if (m != material) continue;
                renderer = r;
                mesh = MapGenUtils.GetMesh(r);
                if (mesh != null && mesh.uv != null && mesh.uv.Length > 0)
                    return true;
            }
        }

        if (mesh == null && showDialog)
        {
            EditorUtility.DisplayDialog(MapGenL.L("マップジェネレーター", "Map Generator"),
                MapGenL.L("シーンにこのマテリアルを使用しているメッシュが見つかりません。",
                           "No mesh using this material was found in the scene."), "OK");
        }
        return mesh != null;
    }

    private static string GetOutputFolder(Material material)
    {
        string materialPath = AssetDatabase.GetAssetPath(material);
        string baseFolder = !string.IsNullOrEmpty(materialPath) && materialPath.StartsWith("Assets/")
            ? Path.GetDirectoryName(materialPath)?.Replace("\\", "/")
            : null;
        baseFolder = baseFolder ?? "Assets/GeneratedMaps";
        return MapGeneratorEngine.EnsureFolderHierarchy($"{baseFolder}/Generated");
    }

    private static string SaveForMaterial(Material material, Texture2D tex, string suffix, bool isNormal)
    {
        string folder = GetOutputFolder(material);
        string baseName = SanitizeFileName(material.name);
        return MapGeneratorEngine.SaveTexture(tex, folder, $"{baseName}_{suffix}",
            _defaultSettings.outputFormat, isNormal);
    }

    private static void AssignToMaterial(Material mat, string savedPath,
        string texProp, string toggleFloat = null, params string[] keywords)
    {
        if (string.IsNullOrEmpty(savedPath)) return;

        Texture2D saved = AssetDatabase.LoadAssetAtPath<Texture2D>(savedPath);
        if (saved == null) return;

        Undo.RecordObject(mat, $"MapGenerator: Assign {texProp}");

        if (mat.HasProperty(texProp))
            mat.SetTexture(texProp, saved);

        if (toggleFloat != null && mat.HasProperty(toggleFloat))
            mat.SetFloat(toggleFloat, 1f);

        foreach (string kw in keywords)
        {
            if (!string.IsNullOrEmpty(kw))
                mat.EnableKeyword(kw);
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "Generated" : value;
    }
}
